using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// "BIM-Bot Bridge" button (Add-ins tab). Click once: this Navisworks becomes the MASTER of a manual
    /// bridge session and lists the models of the open (federated) file, so Revit documents can join as
    /// slaves (Revit: BIM-Bot > Join Bridge). Click again: ends the session.
    /// </summary>
    [Plugin("BIMBotBridge", "BIMB",
        DisplayName = "BIM-Bot Bridge",
        ToolTip = "Start (or stop) a manual Navisworks-Revit bridge with this Navisworks as master. Then click Join Bridge in each Revit model.")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class BIMBotBridgeMaster : AddInPlugin
    {
        public override int Execute(string[] parameters)
        {
            try
            {
                if (NavisBridgeSession.IsActiveForThisProcess())
                {
                    var stop = MessageBox.Show(
                        "The BIM-Bot bridge is running with this Navisworks as MASTER.\n\nStop it? Revit slaves will be disconnected.",
                        "BIM-Bot Bridge", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (stop == DialogResult.Yes)
                    {
                        NavisBridgeSession.Stop();
                        MessageBox.Show("Bridge stopped.", "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    MessageBox.Show("Open the federated model first, then click BIM-Bot Bridge.",
                        "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                if (!NavisPluginApp.IsServiceRunning)
                {
                    NavisMainThread.Init();
                    NavisPluginApp.StartService(manual: true);
                }

                var session = NavisBridgeSession.Start(doc);
                var names = ((JArray)session["master"]!["models"]!).Select(m => "  • " + (string?)m["name"]).ToList();
                var shown = string.Join("\n", names.Take(15)) + (names.Count > 15 ? $"\n  … and {names.Count - 15} more" : "");
                MessageBox.Show(
                    $"Bridge started — this Navisworks is the MASTER.\n\n{names.Count} model(s):\n{shown}\n\n" +
                    "Now open each Revit model you want to work in and click BIM-Bot > Join Bridge.",
                    "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Bridge master button failed", ex);
                MessageBox.Show($"BIM-Bot Bridge failed: {ex.Message}", "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
