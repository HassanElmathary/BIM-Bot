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