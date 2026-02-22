using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPPlugin.Core;
using RevitMCPPlugin.UI.Tools;

namespace RevitMCPPlugin.Commands
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

    // ===== EXPORT TOOLS =====
    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToPdf : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToPdfWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToIfc : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToIfcWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToImages : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToImagesWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDgn : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToDgnWindow().ShowDialog(); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDwg : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { DirectExecutor.RunAsync("export_to_dwg", DirectExecutor.Params(), "Export to DWG"); return Result.Succeeded; }
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToDwf : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToPdfWindow().ShowDialog(); return Result.Succeeded; } // DWF uses PDF-style export
            catch (System.Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Tool_ExportToNwc : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { new ExportToNwcWindow().ShowDialog(); return Result.Succeeded; }
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
}
