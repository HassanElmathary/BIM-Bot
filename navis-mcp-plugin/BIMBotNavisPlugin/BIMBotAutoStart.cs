using System;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// True auto-start. Unlike AddInPlugin (delay-loaded — its constructor
    /// only runs when the ribbon button is first clicked), EventWatcherPlugin
    /// is instantiated during Navisworks startup, so the socket service is
    /// listening on 8091 before the user does anything. The AddInPlugin
    /// ribbon toggle (BIMBotNavisPlugin) shares the same NavisPluginApp
    /// singleton: clicking it while the service runs stops it, clicking
    /// again restarts it.
    /// </summary>
    [Plugin("BIMBotNavisAutoStart", "BIMB",
        DisplayName = "BIM-Bot Auto-Start",
        ToolTip = "Starts the BIM-Bot coordination service automatically.")]
    public class BIMBotAutoStart : EventWatcherPlugin
    {
        public override void OnLoaded()
        {
            try
            {
                NavisMainThread.Init();
                NavisPluginApp.StartService();
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Auto-start failed (non-fatal — use the ribbon button to retry)", ex);
            }
        }

        public override void OnUnloading()
        {
            try { NavisBridgeSession.Stop(); NavisPluginApp.StopService(); }
            catch (Exception ex) { NavisLogger.LogError("Stop on unload failed", ex); }
        }
    }
}
