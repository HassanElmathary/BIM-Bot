using System;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// Navisworks Manage ribbon entry point (Add-ins tab). AddInPlugins are
    /// delay-loaded — this constructor only runs on the first click — so the
    /// startup auto-start lives in BIMBotAutoStart (EventWatcherPlugin).
    /// The constructor still starts the service if auto-start failed, and
    /// Execute toggles it.
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
                if (!NavisPluginApp.IsServiceRunning)
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
                {
                    NavisPluginApp.StopService();
                    MessageBox.Show(
                        "BIM-Bot service stopped. Claude / ChatGPT can no longer reach Navisworks.\n\nClick BIM-Bot again to restart it.",
                        "BIM-Bot",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    NavisMainThread.Init();
                    NavisPluginApp.StartService(manual: true);
                    MessageBox.Show(
                        $"BIM-Bot service is running on port {NavisPluginApp.ActivePort}.",
                        "BIM-Bot",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Toggle service failed", ex);
                MessageBox.Show(
                    $"BIM-Bot could not start: {ex.Message}",
                    "BIM-Bot",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
