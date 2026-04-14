using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.UI.Tools;

namespace BIMBotPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LocalAICommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new OllamaModelManagerWindow();
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Local AI Error", $"Failed to open model manager:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
