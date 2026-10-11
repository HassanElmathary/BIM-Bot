using System;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// Navisworks Manage entry point. Navisworks auto-loads every AddInPlugin
    /// found in the Plugins folder at startup — the constructor runs on the
    /// main thread, so the socket service starts automatically: this is what
    /// makes Claude/ChatGPT "automatically connect" (same role as Revit's
    /// Application.OnStartup + Idling auto-start).
    /// The ribbon button (Add-ins tab) toggles the service via Execute.
    /// </summary>
    [Plugin("BIMBotNavis", "BIMB",
        DisplayName = "BIM-Bot",
        ToolTip = "AI coordination for Navisworks Manage (Claude / ChatGPT). Click to start or stop the service.")]
    [AddInPlugin(AddInLocation.AddIn, CanToggle = true)]
    public class BIMBotNavisPlugin : AddInPlugin
    {
        public BIMBotNavisPlugin()
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

        public override int Execute(string[] parameters)
        {
            try
            {
                // Guard: explicit Execute arriving within seconds of the
                // constructor's auto-start (e.g. Automation AddPluginAssembly
                // instantiates then executes) must keep the service ON.
                if (NavisPluginApp.IsServiceRunning
                    && (DateTime.UtcNow - NavisPluginApp.LastAutoStartUtc).TotalSeconds < 10)
                    return 0;
                if (NavisPluginApp.IsServiceRunning)
                    NavisPluginApp.StopService();
                else
                {
                    NavisMainThread.Init();
                    NavisPluginApp.StartService(manual: true);
                }
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Toggle service failed", ex);
                return 1;
            }
        }

        public override bool TryShowHelp()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/HassanElmathary/BIM-Bot#readme",
                    UseShellExecute = true,
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
