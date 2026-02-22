using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Main entry point for the Revit MCP Plugin.
    /// Registers the ribbon panel with Start/Stop service and Settings buttons.
    /// </summary>
    public class Application : IExternalApplication
    {
        public static UIControlledApplication? UiApp { get; private set; }
        public static UIApplication? ActiveUIApp { get; set; }
        private static SocketService? _socketService;
        private static ExternalEventManager? _eventManager;

        public static SocketService? SocketServiceInstance => _socketService;
        public static ExternalEventManager? EventManagerInstance => _eventManager;

        public static string Version => "1.0.0";

        public Result OnStartup(UIControlledApplication application)
        {
            UiApp = application;

            try
            {
                var asm = Assembly.GetExecutingAssembly().Location;

                // ========================================
                // Panel 1: Core
                // ========================================
                var corePanel = application.CreateRibbonPanel("Revit MCP");

                // --- Start MCP Service ---
                var toggleData = new PushButtonData("MCPToggle", "Start MCP\nService", asm,
                    "RevitMCPPlugin.Commands.ToggleServiceCommand")
                {
                    ToolTip = "Start or stop the MCP service for AI integration",
                    LargeImage = RibbonIcons.StartService(32),
                    Image = RibbonIcons.StartService(16)
                };
                corePanel.AddItem(toggleData);

                // --- AI Chat ---
                var chatData = new PushButtonData("MCPChat", "AI Chat", asm,
                    "RevitMCPPlugin.Commands.ChatCommand")
                {
                    ToolTip = "Open the AI Chat window to interact with your Revit model using natural language",
                    LargeImage = RibbonIcons.Chat(32),
                    Image = RibbonIcons.Chat(16)
                };
                corePanel.AddItem(chatData);

                // --- Tools Hub ---
                var hubData = new PushButtonData("MCPToolsHub", "Tools\nHub", asm,
                    "RevitMCPPlugin.Commands.ToolsHubCommand")
                {
                    ToolTip = "Browse and launch all 60+ MCP tools from a visual dashboard",
                    LargeImage = RibbonIcons.ToolsHub(32),
                    Image = RibbonIcons.ToolsHub(16)
                };
                corePanel.AddItem(hubData);

                corePanel.AddSeparator();

                // ========================================
                // Export Pulldown
                // ========================================
                var exportPd = corePanel.AddItem(
                    new PulldownButtonData("MCPExport", "Export")
                    {
                        ToolTip = "Export tools — PDF, DWG, DWF, DGN, IFC, NWC, Images, Schedules, CSV",
                        LargeImage = RibbonIcons.Export(32),
                        Image = RibbonIcons.Export(16)
                    }) as PulldownButton;

                AddPulldownItem(exportPd, "ExportManager", "📦 Export Manager", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportManager", "Unified export manager with multi-format support");
                AddPulldownItem(exportPd, "ExportPdf", "📄 Export to PDF", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToPdf", "Export sheets/views to PDF");
                AddPulldownItem(exportPd, "ExportDwg", "📐 Export to DWG", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToDwg", "Export views to DWG (AutoCAD)");
                AddPulldownItem(exportPd, "ExportDwf", "📋 Export to DWF", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToDwf", "Export sheets/views to DWF");
                AddPulldownItem(exportPd, "ExportDgn", "📊 Export to DGN", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToDgn", "Export views to DGN");
                AddPulldownItem(exportPd, "ExportIfc", "🏗️ Export to IFC", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToIfc", "Export model to IFC");
                AddPulldownItem(exportPd, "ExportNwc", "🔗 Export to NWC", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToNwc", "Export model to NWC (Navisworks)");
                AddPulldownItem(exportPd, "ExportImages", "🖼️ Export to Images", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportToImages", "Export views to PNG, JPEG, TIFF, BMP");
                AddPulldownItem(exportPd, "ExportSchedule", "📊 Export Schedule", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportScheduleData", "Export schedule data to CSV");
                AddPulldownItem(exportPd, "ExportParams", "📋 Export Parameters", asm,
                    "RevitMCPPlugin.Commands.Tool_ExportParametersToCsv", "Export element parameters to CSV");
                AddPulldownItem(exportPd, "ImportParams", "📥 Import Parameters", asm,
                    "RevitMCPPlugin.Commands.Tool_ImportParametersFromCsv", "Import parameters from CSV file");

                // ========================================
                // Families Pulldown
                // ========================================
                var familiesPd = corePanel.AddItem(
                    new PulldownButtonData("MCPFamilies", "Families")
                    {
                        ToolTip = "Family & parameter management tools",
                        LargeImage = RibbonIcons.Families(32),
                        Image = RibbonIcons.Families(16)
                    }) as PulldownButton;

                AddPulldownItem(familiesPd, "ManageFamilies", "📁 Manage Families", asm,
                    "RevitMCPPlugin.Commands.Tool_ManageFamilies", "Rename & organize families");
                AddPulldownItem(familiesPd, "FamilyInfo", "ℹ️ Family Info", asm,
                    "RevitMCPPlugin.Commands.Tool_GetFamilyInfo", "Get detailed family information");
                AddPulldownItem(familiesPd, "CreateParam", "➕ Create Parameter", asm,
                    "RevitMCPPlugin.Commands.Tool_CreateProjectParameter", "Create a new project parameter");
                AddPulldownItem(familiesPd, "BatchSetParam", "✏️ Batch Set Parameter", asm,
                    "RevitMCPPlugin.Commands.Tool_BatchSetParameter", "Set parameter values in batch");
                AddPulldownItem(familiesPd, "DeleteUnused", "🗑️ Delete Unused", asm,
                    "RevitMCPPlugin.Commands.Tool_DeleteUnusedFamilies", "Remove unused families from project");

                // ========================================
                // QuickViews Pulldown
                // ========================================
                var viewsPd = corePanel.AddItem(
                    new PulldownButtonData("MCPQuickViews", "Quick\nViews")
                    {
                        ToolTip = "Auto-generate elevation, section, and callout views",
                        LargeImage = RibbonIcons.QuickViews(32),
                        Image = RibbonIcons.QuickViews(16)
                    }) as PulldownButton;

                AddPulldownItem(viewsPd, "Elevations", "📐 Create Elevations", asm,
                    "RevitMCPPlugin.Commands.Tool_CreateElevationViews", "Auto-generate elevation views");
                AddPulldownItem(viewsPd, "Sections", "✂️ Create Sections", asm,
                    "RevitMCPPlugin.Commands.Tool_CreateSectionViews", "Auto-generate section views");
                AddPulldownItem(viewsPd, "Callouts", "🔍 Create Callouts", asm,
                    "RevitMCPPlugin.Commands.Tool_CreateCalloutViews", "Auto-generate callout views");

                // ========================================
                // Views & Sheets Pulldown
                // ========================================
                var sheetsPd = corePanel.AddItem(
                    new PulldownButtonData("MCPSheets", "Views &\nSheets")
                    {
                        ToolTip = "View and sheet management tools",
                        LargeImage = RibbonIcons.ViewsSheets(32),
                        Image = RibbonIcons.ViewsSheets(16)
                    }) as PulldownButton;

                AddPulldownItem(sheetsPd, "AlignViewports", "📏 Align Viewports", asm,
                    "RevitMCPPlugin.Commands.Tool_AlignViewports", "Align viewports across sheets");
                AddPulldownItem(sheetsPd, "BatchSheets", "📑 Batch Create Sheets", asm,
                    "RevitMCPPlugin.Commands.Tool_BatchCreateSheets", "Create multiple sheets at once");
                AddPulldownItem(sheetsPd, "DuplicateView", "📋 Duplicate View", asm,
                    "RevitMCPPlugin.Commands.Tool_DuplicateView", "Duplicate views with options");
                AddPulldownItem(sheetsPd, "ApplyTemplate", "🎨 Apply View Template", asm,
                    "RevitMCPPlugin.Commands.Tool_ApplyViewTemplate", "Apply view template to views");

                corePanel.AddSeparator();

                // ========================================
                // Utility buttons (Settings + Updates) stacked
                // ========================================
                var settingsData = new PushButtonData("MCPSettings", "Settings", asm,
                    "RevitMCPPlugin.Commands.SettingsCommand")
                {
                    ToolTip = "Configure MCP connection settings",
                    LargeImage = RibbonIcons.Settings(32),
                    Image = RibbonIcons.Settings(16)
                };

                var updateData = new PushButtonData("MCPUpdate", "Check\nUpdates", asm,
                    "RevitMCPPlugin.Commands.CheckUpdateCommand")
                {
                    ToolTip = "Check for plugin updates on GitHub",
                    LargeImage = RibbonIcons.CheckUpdates(32),
                    Image = RibbonIcons.CheckUpdates(16)
                };

                corePanel.AddItem(settingsData);
                corePanel.AddItem(updateData);

                // Initialize the external event manager
                _eventManager = new ExternalEventManager();

                Logger.Log("Revit MCP Plugin started successfully — all ribbon buttons registered");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start Revit MCP Plugin", ex);
                return Result.Failed;
            }
        }

        /// <summary>Helper to add a sub-item to a pulldown button.</summary>
        private static void AddPulldownItem(PulldownButton pd, string name, string text,
            string asm, string className, string tooltip)
        {
            var data = new PushButtonData(name, text, asm, className) { ToolTip = tooltip };
            pd.AddPushButton(data);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _socketService?.Stop();
                Logger.Log("Revit MCP Plugin shut down");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during shutdown", ex);
                return Result.Failed;
            }
        }

        public static void StartService(UIApplication uiApp)
        {
            ActiveUIApp = uiApp;
            if (_socketService == null)
            {
                _socketService = new SocketService(8080, _eventManager!);
            }
            _socketService.Start();
            Logger.Log("MCP Service started on port 8080");
        }

        public static void StopService()
        {
            _socketService?.Stop();
            Logger.Log("MCP Service stopped");
        }

        public static bool IsServiceRunning => _socketService?.IsRunning ?? false;
    }
}
