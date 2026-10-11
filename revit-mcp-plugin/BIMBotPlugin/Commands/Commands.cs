using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.AI;
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
            try
            {
                var aiSettings = GeminiSettings.Load();
                var window = new SettingsWindow(aiSettings)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                if (window.ShowDialog() == true)
                    window.ResultSettings?.Save();
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

                // Reset the ribbon button badge back to normal after user has seen the result
                Application.UpdateCheckButtonState(false);

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
