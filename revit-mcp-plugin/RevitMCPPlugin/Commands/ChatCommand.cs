using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPPlugin.Core;
using RevitMCPPlugin.UI;

namespace RevitMCPPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ChatCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Auto-start MCP service if not running
                if (!Application.IsServiceRunning)
                {
                    Application.StartService(commandData.Application);
                }

                ChatWindow.OpenWithPrompt(null);
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
