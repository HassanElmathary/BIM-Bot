IMPLEMENTATION PLAN — Navisworks main-thread marshalling fix
Audience: coding agent. Follow this literally. Do not improvise outside the listed file.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP

======================================================================
1. PROBLEM (measured live on 2026-10-15)
======================================================================
File: navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisMainThread.cs

The Navisworks .NET API must only be called on the Navisworks UI (main) thread.
NavisMainThread.Execute() is called from socket worker threads. Today it:
  1. puts the work item in a queue,
  2. waits for Autodesk.Navisworks.Api.Application.Idle to fire and drain the queue,
  3. after 10 s (IdlePumpTimeoutMs) gives up and runs the command DIRECTLY ON THE
     WORKER THREAD.

Application.Idle only fires after the UI thread has processed messages. When Navisworks
is sitting idle nothing posts a message, so nothing wakes the UI thread. Result:
  - First command after a quiet period takes ~10 s (measured: navis_ping 9540 ms,
    next call 16 ms).
  - Then the command runs off the UI thread (log: "Idle pump timed out — executing
    directly on caller thread"). That is unsupported by the Navisworks API and can
    crash Navisworks or corrupt results.

======================================================================
2. THE FIX (design)
======================================================================
- In Init() (always called on the UI thread) create a hidden
  System.Windows.Forms.Control and force its window handle to be created.
  That control is bound to the UI thread.
- Execute() enqueues the work item and calls _marshal.BeginInvoke(DrainQueue).
  BeginInvoke posts a Windows message, which WAKES the UI thread immediately,
  even when Navisworks is idle and even while a modal dialog is open
  (modal dialogs pump messages).
- Keep Application.Idle hooked as a secondary pump (it also calls DrainQueue).
- NEVER execute a command on the worker thread. Remove the direct fallback.
- Two-phase wait:
    a. Wait up to StartTimeoutMs (15 s) for the UI thread to START the item.
       If it doesn't start: mark it abandoned (so it will be skipped if it runs
       later) and throw a clear "Navisworks is busy" error.
    b. Once started, wait up to RunTimeoutMs (290 s, just under the MCP server's
       300 s socket timeout) for it to finish. If it doesn't finish, throw a
       "still running" error (cannot cancel a running API call; it will finish
       on its own).
- Guard against re-entrancy: if a command pumps messages while running (e.g. a
  progress dialog), a nested DrainQueue must NOT start another command.

======================================================================
3. HARD RULES
======================================================================
1. Only edit ONE file: navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisMainThread.cs
2. Keep the public API exactly: `public static void Init()` and
   `public static JObject Execute(Func<JObject> func)`. Callers in
   NavisPluginApp.cs, BIMBotNavisPlugin.cs and BIMBotAutoStart.cs must not change.
3. Do not touch any other file, any .csproj, or anything under revit-mcp-server/.
   The working tree has other UNCOMMITTED changes — do not revert, reformat or
   clean them up. Do not run git checkout / stash / reset / clean. Do not commit.
4. No new NuGet packages. System.Windows.Forms is already referenced by the project.
5. Do NOT copy/deploy the DLL into C:\Program Files — the reviewer will do that.
   Navisworks may be open while you work; that is fine because you only build into bin\.

======================================================================
4. TASK — replace the whole file with exactly this content
======================================================================
Overwrite navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisMainThread.cs with:

----- BEGIN FILE -----
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Marshals socket-thread commands onto the Navisworks main (UI) thread.
    /// Same role as Revit's ExternalEventManager: Navisworks .NET API calls
    /// must run on the main thread. A hidden WinForms control created on the
    /// UI thread receives BeginInvoke posts, which wake the UI thread at once
    /// (Application.Idle alone never fires while Navisworks sits idle).
    /// Application.Idle stays hooked as a secondary pump. Commands are never
    /// executed off the UI thread: if the UI thread cannot start one in time,
    /// the caller gets a clear "busy" error instead.
    /// </summary>
    public static class NavisMainThread
    {
        private sealed class WorkItem
        {
            public Func<JObject> Func = null!;
            public JObject? Result;
            public Exception? Error;
            public bool Started;
            public bool Abandoned;
            public readonly ManualResetEventSlim StartedEvent = new ManualResetEventSlim(false);
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static int _mainThreadId;
        private static bool _initialized;
        private static readonly object _lock = new object();
        private static readonly Queue<WorkItem> _queue = new Queue<WorkItem>();
        private static bool _idleHooked;
        private static bool _draining;
        private static Control? _marshal;

        private const int StartTimeoutMs = 15000;
        private const int RunTimeoutMs = 290000;

        public static void Init()
        {
            lock (_lock)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                if (_initialized) return;
                _initialized = true;
            }
            try
            {
                var c = new Control();
                c.CreateControl();
                var _ = c.Handle; // force the HWND onto this (UI) thread
                _marshal = c;
            }
            catch (Exception ex)
            {
                _marshal = null;
                NavisLogger.LogError("Failed to create UI marshal control (Idle pump only)", ex);
            }
            try
            {
                if (!_idleHooked)
                {
                    Autodesk.Navisworks.Api.Application.Idle += OnIdle;
                    _idleHooked = true;
                }
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Failed to hook Application.Idle", ex);
            }
            NavisLogger.Log($"NavisMainThread ready on thread {_mainThreadId} (marshal={(_marshal != null ? "on" : "off")})");
        }

        private static void OnIdle(object sender, EventArgs e) => DrainQueue();

        private static void DrainQueue()
        {
            if (_draining) return; // re-entrancy guard: a running command pumped messages
            _draining = true;
            try
            {
                while (true)
                {
                    WorkItem? item = null;
                    lock (_lock)
                    {
                        while (_queue.Count > 0)
                        {
                            var next = _queue.Dequeue();
                            if (next.Abandoned) continue;
                            next.Started = true;
                            item = next;
                            break;
                        }
                    }
                    if (item == null) break;
                    item.StartedEvent.Set();
                    try { item.Result = item.Func(); }
                    catch (Exception ex) { item.Error = ex; }
                    finally { item.Done.Set(); }
                }
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Main-thread pump failed", ex);
            }
            finally
            {
                _draining = false;
            }
        }

        private static void Wake()
        {
            var m = _marshal;
            if (m == null || m.IsDisposed || !m.IsHandleCreated) return;
            try { m.BeginInvoke(new MethodInvoker(DrainQueue)); }
            catch (Exception ex) { NavisLogger.LogError("BeginInvoke wake failed", ex); }
        }

        public static JObject Execute(Func<JObject> func)
        {
            if (!_initialized || Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                return func();

            var item = new WorkItem { Func = func };
            lock (_lock) { _queue.Enqueue(item); }
            Wake();

            if (!item.StartedEvent.Wait(StartTimeoutMs))
            {
                bool abandoned;
                lock (_lock)
                {
                    abandoned = !item.Started;
                    if (abandoned) item.Abandoned = true;
                }
                if (abandoned)
                {
                    NavisLogger.Log($"Main thread did not start the command within {StartTimeoutMs / 1000}s — abandoned (not run)");
                    throw new Exception(
                        $"Navisworks is busy and did not start the command within {StartTimeoutMs / 1000} s " +
                        "(a dialog, a long operation, or model loading may be blocking it). The command was NOT run. " +
                        "Close any open Navisworks dialog and retry.");
                }
            }

            if (!item.Done.Wait(RunTimeoutMs))
            {
                NavisLogger.Log($"Command still running after {RunTimeoutMs / 1000}s on the main thread");
                throw new Exception(
                    $"Navisworks command is still running after {RunTimeoutMs / 1000} s. " +
                    "It will finish on its own; retry or check Navisworks before sending more commands.");
            }
            if (item.Error != null) throw item.Error;
            return item.Result ?? new JObject { ["ok"] = true };
        }
    }
}
----- END FILE -----

Notes for compiling:
- `Application.Idle` is written fully qualified on purpose (both Navisworks and
  WinForms have an `Application` class). Keep it that way. If the compiler still
  reports CS0104 (ambiguous reference) anywhere else, fully qualify that one
  usage the same way. Change nothing else.
- If `MethodInvoker` is not found, use `new Action(DrainQueue)` instead.

======================================================================
5. BUILD
======================================================================
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release

Must succeed with 0 errors. If it fails for any reason other than the two notes above,
STOP and report the exact compiler output — do not change other files or the .csproj.

======================================================================
6. SELF-CHECKS (no Navisworks needed)
======================================================================
Run and report output:
  A. git diff --stat
     Expect: the only NEW change versus before you started is NavisMainThread.cs.
  B. grep -n "executing directly\|Idle pump timed out" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisMainThread.cs
     Expect: no output (the off-thread fallback is gone).
  C. grep -n "BeginInvoke\|StartTimeoutMs\|RunTimeoutMs\|_draining" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisMainThread.cs
     Expect: all four present.

(The reviewer will deploy the DLL and test live: first call after 60 s idle must answer
in < 1 s, and the log must never again show "executing directly on caller thread".)

======================================================================
7. REPORT BACK
======================================================================
1. Files changed.
2. Last ~20 lines of the build output.
3. Output of self-checks A, B, C.
4. Anything you could not do exactly as written, and why.
