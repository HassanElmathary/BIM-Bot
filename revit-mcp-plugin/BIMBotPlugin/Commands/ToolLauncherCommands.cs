using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.Core;
using BIMBotPlugin.UI.Tools;

namespace BIMBotPlugin.Commands
{
    /// <summary>
    /// Each tool command opens its dedicated UI window directly.
    /// No AI or internet required — works fully offline.
    /// </summary>

    // ===== EXPORT MANAGER (Unified) =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                new ExportManagerWindow().ShowDialog();
                return Result.Succeeded;
            }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== EXPORT TOOLS (All redirect to Export Manager) =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToPdf : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToIfc : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToImages : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDgn : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDwg : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDwf : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToNwc : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportManagerWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportScheduleData : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportScheduleWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportParametersToCsv : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportParamsCsvWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ImportParametersFromCsv : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ImportParamsCsvWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== POWER BI EXPORT =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToPowerBI : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uidoc = uiApp.ActiveUIDocument;
                var doc = uidoc?.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Power BI Export", "No active document. Please open a Revit project first.");
                    return Result.Failed;
                }

                // Check if active view is 3D
                var is3D = uidoc.ActiveView is View3D;

                // Build parameters (default format: CSV data + ready-to-open .pbit)
                var parameters = new Newtonsoft.Json.Linq.JObject
                {
                    ["exportScope"] = is3D ? "currentView" : "allModel",
                    ["format"] = "pbit"
                };

                // Execute the export via CommandExecutor
                var result = CommandExecutor.Execute(uiApp, "export_to_powerbi", parameters);
                var msg = result?["message"]?.ToString() ?? "Export completed";
                var pbitPath = result?["pbitPath"]?.ToString() ?? "";
                var elemCount = result?["elementCount"]?.ToString() ?? "0";
                var paramCount = result?["parameterCount"]?.ToString() ?? "0";
                var fileSize = result?["fileSize"]?.ToString() ?? "";

                var dialog = new TaskDialog("Power BI Export ✅")
                {
                    MainInstruction = "3D dashboard exported",
                    MainContent =
                        $"{msg}\n\n" +
                        $"📊 Elements: {elemCount}   🔖 Parameters: {paramCount}   📦 Data: {fileSize}\n" +
                        $"📁 {pbitPath}\n\n" +
                        "Open the dashboard now? Power BI Desktop will load the 3D model with slicers for every parameter.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };

                if (dialog.Show() == TaskDialogResult.Yes && System.IO.File.Exists(pbitPath))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(pbitPath) { UseShellExecute = true });
                }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Power BI Export ❌", $"Export failed:\n{ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    // ===== FAMILY & PARAMETER TOOLS =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ManageFamilies : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ManageFamiliesWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_GetFamilyInfo : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new FamilyInfoWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_CreateProjectParameter : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new CreateParameterWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_BatchSetParameter : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new BatchSetParamWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_DeleteUnusedFamilies : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new DeleteUnusedWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== QUICKVIEWS TOOLS =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_CreateElevationViews : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ElevationViewsWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_CreateSectionViews : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new SectionViewsWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_CreateCalloutViews : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new CalloutViewsWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== VIEW & SHEET TOOLS =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_AlignViewports : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new AlignViewportsWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_BatchCreateSheets : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new BatchCreateSheetsWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_DuplicateView : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new DuplicateViewWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ApplyViewTemplate : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ApplyViewTemplateWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== PROJECT FILES =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ProjectFiles : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ProjectFilesWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    // ===== QA/QC =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ClashDetection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ClashDetectionWindow().Show(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
