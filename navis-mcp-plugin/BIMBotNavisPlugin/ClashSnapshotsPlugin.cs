using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;
using BIMBotNavisPlugin.UI;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// Ribbon button (Add-ins tab): exports a red/green, isolated image of every clash in a test
    /// plus clashes.csv. Works without any AI client connected.
    /// </summary>
    [Plugin("BIMBotClashSnapshots", "BIMB",
        DisplayName = "Clash Snapshots",
        ToolTip = "Save a red/green image of every clash in a clash test, plus a CSV list, to a folder.")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class ClashSnapshotsPlugin : AddInPlugin
    {
        public override int Execute(string[] parameters)
        {
            const string title = "BIM-Bot — Clash Snapshots";
            try
            {
                List<string> tests;
                try { tests = NavisCommandExecutor.ClashTestNames(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 0;
                }
                if (tests.Count == 0)
                {
                    MessageBox.Show("This model has no clash tests. Create and run one in Clash Detective first.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                using (var form = new ClashSnapshotsForm(tests)) form.ShowDialog();
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Clash Snapshots button failed", ex);
                MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
