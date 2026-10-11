using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Marshals socket-thread commands onto the Navisworks main thread.
    /// Same role as Revit's ExternalEventManager: Navisworks .NET API calls
    /// must run on the main thread. Work is pumped by Application.Idle;
    /// if Idle never fires (e.g. headless Automation), we fall back to
    /// direct execution after a timeout and surface any error as JSON.
    /// </summary>
    public static class NavisMainThread
    {
        private sealed class WorkItem
        {
            public Func<JObject> Func = null!;
            public JObject? Result;
            public Exception? Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static int _mainThreadId;
        private static bool _initialized;
        private static readonly object _lock = new object();
        private static readonly Queue<WorkItem> _queue = new Queue<WorkItem>();
        private static bool _idleHooked;

        private const int IdlePumpTimeoutMs = 10000;

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
                if (!_idleHooked)
                {
                    Application.Idle += OnIdle;
                    _idleHooked = true;
                }
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Failed to hook Application.Idle (fallback: direct execution)", ex);
            }
            NavisLogger.Log($"NavisMainThread ready on thread {_mainThreadId}");
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            try
            {
                while (true)
                {
                    WorkItem? item = null;
                    lock (_lock)
                    {
                        if (_queue.Count == 0) break;
                        item = _queue.Dequeue();
                    }
                    try { item.Result = item.Func(); }
                    catch (Exception ex) { item.Error = ex; }
                    finally { item.Done.Set(); }
                }
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Idle pump failed", ex);
            }
        }

        public static JObject Execute(Func<JObject> func)
        {
            if (!_initialized || Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                return func();

            var item = new WorkItem { Func = func };
            lock (_lock) { _queue.Enqueue(item); }

            if (!item.Done.Wait(IdlePumpTimeoutMs))
            {
                NavisLogger.Log("Idle pump timed out — executing directly on caller thread");
                try { return func(); }
                catch (Exception ex)
                {
                    throw new Exception("Navisworks command failed (Idle pump timed out, direct fallback also failed): " + ex.Message, ex);
                }
            }
            if (item.Error != null) throw item.Error;
            return item.Result ?? new JObject { ["ok"] = true };
        }
    }
}
