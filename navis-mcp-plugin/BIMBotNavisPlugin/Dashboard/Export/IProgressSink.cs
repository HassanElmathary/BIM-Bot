using System;
using Autodesk.Navisworks.Api;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>Progress + cancel channel for long exports. Report returns true when the user cancelled.</summary>
    public interface IProgressSink
    {
        bool Report(string message, int done, int total);
    }

    public sealed class NullProgress : IProgressSink
    {
        public static readonly NullProgress Instance = new NullProgress();
        public bool Report(string message, int done, int total) => false;
    }

    /// <summary>Navisworks status-bar progress with its Cancel button. Dispose ends it.</summary>
    public sealed class NavisProgress : IProgressSink, IDisposable
    {
        private readonly Progress? _p;

        public NavisProgress(string title)
        {
            try { _p = Application.BeginProgress(title); } catch { _p = null; }
        }

        public bool Report(string message, int done, int total)
        {
            if (_p == null) return false;
            try
            {
                _p.Update(total <= 0 ? 0 : Math.Min(1.0, (double)done / total));
                return _p.IsCanceled;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            if (_p == null) return;
            try { Application.EndProgress(); } catch { }
        }
    }
}
