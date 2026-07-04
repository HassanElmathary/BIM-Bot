using System;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.Core;

namespace BIMBotPlugin.Commands
{
    /// <summary>
    /// Configures (or repairs) the BIM-Bot MCP server entry in Claude
    /// Desktop and Claude Code. All the heavy lifting lives in
    /// ClaudeConfigService — this command just reports the outcome.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ConnectToClaudeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var results = ClaudeConfigService.EnsureAll();

                var sb = new StringBuilder();
                bool anyConfigured = false;
                bool anyChanged = false;

                foreach (var r in results)
                {
                    var icon = r.Configured ? "✅" : "⚠️";
                    sb.AppendLine($"{icon} {r.Target}: {r.Detail}");
                    anyConfigured |= r.Configured;
                    anyChanged |= r.Changed;
                }

                if (anyConfigured)
                {
                    sb.AppendLine();
                    if (anyChanged)
                    {
                        sb.AppendLine("To apply the changes:");
                        sb.AppendLine("1. Fully restart Claude (File → Exit, then reopen)");
                        sb.AppendLine("2. Keep Revit open — the BIM-Bot service starts automatically");
                        sb.AppendLine("3. Ask Claude: \"Use BIM-Bot to list the walls in the model\"");
                    }
                    else
                    {
                        sb.AppendLine("Everything is already set up — no changes were needed.");
                        sb.AppendLine("If Claude still can't see BIM-Bot, fully restart the Claude app.");
                    }
                }

                TaskDialog.Show("Connect to Claude", sb.ToString().TrimEnd());
                return anyConfigured ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Connect to Claude Error", $"Failed to configure Claude:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
