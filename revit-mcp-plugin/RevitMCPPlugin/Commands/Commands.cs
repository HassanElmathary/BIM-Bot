using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPPlugin.Core;
using RevitMCPPlugin.UI;

namespace RevitMCPPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleServiceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Application.IsServiceRunning)
                {
                    Application.StopService();
                    TaskDialog.Show("Revit MCP", "MCP Service stopped.");
                }
                else
                {
                    Application.StartService(commandData.Application);
                    TaskDialog.Show("Revit MCP", "MCP Service started on port 8080.\nAI can now connect to Revit!");
                }
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var info = $"Revit MCP Plugin v{Application.Version}\n\n" +
                       $"Service Status: {(Application.IsServiceRunning ? "Running ✅" : "Stopped ❌")}\n" +
                       $"Port: 8080\n" +
                       $"Protocol: JSON-RPC 2.0\n\n" +
                       $"To connect an AI client, configure it to use:\n" +
                       $"  Server: revit-mcp\n" +
                       $"  Command: node <install-path>/build/index.js";
            TaskDialog.Show("Revit MCP Settings", info);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CheckUpdateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var checker = new UpdateChecker();
                var updateInfo = checker.CheckForUpdate();

                if (updateInfo.UpdateAvailable)
                {
                    // Show the modern update notification window
                    var window = new UpdateNotificationWindow(updateInfo);
                    window.ShowDialog();
                }
                else
                {
                    TaskDialog.Show("Revit MCP", $"✅ You're up to date! (v{Application.Version})");
                }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Update Error", $"Failed to check for updates:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
