using System;
using System.IO;
using System.Windows.Forms;
using BIMBotNavisPlugin.Core;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// Navisworks Manage entry point — ADDITIVE-ONLY, Revit Application.cs untouched.
    /// Wiring on a Manage machine:
    ///   [Plugin("BIMBotNavisPlugin", "BIMB", DisplayName = "BIM-Bot")]
    ///   class Plugin : AddInPlugin (Autodesk.Navisworks.Api.Plugins)
    ///   + Ribbon CommandHandlerPlugin for Start/Stop + AI Chat + Settings.
    /// Service: TCP 8091 (scan to 8101), handshake service-navis.json.
    /// </summary>
    public static class NavisPluginApp
    {
        public const string Version = "2.7.6";
        public const int DefaultPort = 8091;

        private static NavisSocketService? _service;
        public static bool IsServiceRunning => _service?.IsRunning ?? false;
        public static int ActivePort => _service?.Port ?? DefaultPort;
        public static DateTime LastAutoStartUtc { get; private set; } = DateTime.MinValue;

        public static void StartService(bool manual = false)
        {
            if (_service == null)
                _service = new NavisSocketService(DefaultPort,
                    (method, pars) => NavisMainThread.Execute(() => NavisCommandExecutor.Execute(method, pars)));
            if (_service.IsRunning) return;
            _service.Start();
            if (!manual) LastAutoStartUtc = DateTime.UtcNow;
            NavisLogger.Log($"Service started on port {ActivePort} (manual={manual})");
        }

        public static void StopService()
        {
            if (_service == null || !_service.IsRunning) return;
            _service.Stop();
            NavisLogger.Log("Service stopped");
        }
    }
}
