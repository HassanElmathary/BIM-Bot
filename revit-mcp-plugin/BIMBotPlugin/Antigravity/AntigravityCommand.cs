using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMBotPlugin.Antigravity
{
    /// <summary>
    /// Revit external command that opens the Antigravity chat window.
    /// Registered as a ribbon button in Application.cs.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AntigravityCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                AntigravityWindow.Open();
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
