using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.Core;
using BIMBotPlugin.UI;

namespace BIMBotPlugin.Commands
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
                }
                else
                {
                    Application.StartService(commandData.Application);
                }
                // Button icon + text is updated automatically by Application.UpdateToggleButtonState()
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
            var info = $"BIM-Bot Plugin v{Application.Version}\n\n" +
                       $"Service Status: {(Application.IsServiceRunning ? "Running ✅" : "Stopped ❌")}\n" +
                       $"Port: 8080\n" +
                       $"Protocol: JSON-RPC 2.0\n\n" +
                       $"To connect an AI client, configure it to use:\n" +
                       $"  Server: bim-bot\n" +
                       $"  Command: node <install-path>/build/index.js";
            TaskDialog.Show("BIM-Bot Settings", info);
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
                    TaskDialog.Show("BIM-Bot", $"✅ You're up to date! (v{Application.Version})");
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
