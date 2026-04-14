using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMBotPlugin.UI;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Main entry point for the BIM-Bot Plugin.
    /// Registers the ribbon panel with Start/Stop service and Settings buttons.
    /// </summary>
    public class Application : IExternalApplication
    {
        public static UIControlledApplication? UiApp { get; private set; }
        public static UIApplication? ActiveUIApp { get; set; }
        private static SocketService? _socketService;
        private static ExternalEventManager? _eventManager;
        private static bool _startupUpdateChecked = false;

        // Reference for toggle-state button
        private static PushButton? _toggleServiceButton;

        public static SocketService? SocketServiceInstance => _socketService;
        public static ExternalEventManager? EventManagerInstance => _eventManager;

        public static string Version => "2.0.3";

        public Result OnStartup(UIControlledApplication application)
        {
            UiApp = application;

            try
            {
                var asm = Assembly.GetExecutingAssembly().Location;

                // ========================================
                // Create custom "BIM-Bot" tab in the ribbon
                // ========================================
                const string tabName = "BIM-Bot";
                application.CreateRibbonTab(tabName);

                // ========================================
                // Panel 1: Core
                // ========================================
                var corePanel = application.CreateRibbonPanel(tabName, "BIM-Bot");

                // --- Start BIM-Bot (toggle) ---
                var toggleData = new PushButtonData("MCPToggle", "Start\nBIM-Bot", asm,
                    "BIMBotPlugin.Commands.ToggleServiceCommand")
                {
                    ToolTip = "Start or stop the BIM-Bot service for AI integration",
                    LargeImage = RibbonIcons.WithCrossBadge(RibbonIcons.StartService(32), 32),
                    Image = RibbonIcons.WithCrossBadge(RibbonIcons.StartService(16), 16)
                };
                _toggleServiceButton = corePanel.AddItem(toggleData) as PushButton;

                // --- AI Chat ---
                var chatData = new PushButtonData("MCPChat", "AI Chat", asm,
                    "BIMBotPlugin.Commands.ChatCommand")
                {
                    ToolTip = "Open the AI Chat window to interact with your Revit model using natural language",
                    LargeImage = RibbonIcons.Chat(32),
                    Image = RibbonIcons.Chat(16)
                };
                corePanel.AddItem(chatData);

                // --- Project Files ---
                var projectFilesData = new PushButtonData("MCPProjectFiles", "Project\nFiles", asm,
                    "BIMBotPlugin.Commands.Tool_ProjectFiles")
                {
                    ToolTip = "Manage project files — add documents, send to AI for analysis, and work with results",
                    LargeImage = RibbonIcons.ProjectFiles(32),
                    Image = RibbonIcons.ProjectFiles(16)
                };
                corePanel.AddItem(projectFilesData);

                // --- Connect Claude ---
                var claudeData = new PushButtonData("MCPClaudeConnect", "Connect\nClaude", asm,
                    "BIMBotPlugin.Commands.ConnectToClaudeCommand")
                {
                    ToolTip = "Automatically configure the Claude Desktop app to connect to BIM-Bot",
                    LargeImage = RibbonIcons.ConnectClaude(32),
                    Image = RibbonIcons.ConnectClaude(16)
                };
                corePanel.AddItem(claudeData);

                // --- Local AI ---
                var localAiData = new PushButtonData("MCPLocalAI", "Local\nAI", asm,
                    "BIMBotPlugin.Commands.LocalAICommand")
                {
                    ToolTip = "Manage local AI models (Ollama) — free, unlimited, private",
                    LargeImage = RibbonIcons.LocalAI(32),
                    Image = RibbonIcons.LocalAI(16)
                };
                corePanel.AddItem(localAiData);

                // --- Tools Hub ---
                var hubData = new PushButtonData("MCPToolsHub", "Tools\nHub", asm,
                    "BIMBotPlugin.Commands.ToolsHubCommand")
                {
                    ToolTip = "Browse and launch all 160+ BIM-Bot tools from a visual dashboard",
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
                    "BIMBotPlugin.Commands.Tool_ExportManager", "Unified export manager — PDF, DWG, DWF, DGN, IFC, NWC, Images");
                AddPulldownItem(exportPd, "ExportSchedule", "📊 Export Schedule", asm,
                    "BIMBotPlugin.Commands.Tool_ExportScheduleData", "Export schedule data to CSV");
                AddPulldownItem(exportPd, "ExportParams", "📋 Export Parameters", asm,
                    "BIMBotPlugin.Commands.Tool_ExportParametersToCsv", "Export element parameters to CSV");
                AddPulldownItem(exportPd, "ImportParams", "📥 Import Parameters", asm,
                    "BIMBotPlugin.Commands.Tool_ImportParametersFromCsv", "Import parameters from CSV file");
                AddPulldownItem(exportPd, "ExportPowerBI", "⚡ Export to Power BI", asm,
                    "BIMBotPlugin.Commands.Tool_ExportToPowerBI", "Export 3D model with geometry to SQLite for Power BI visualization");

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
                    "BIMBotPlugin.Commands.Tool_ManageFamilies", "Rename & organize families");
                AddPulldownItem(familiesPd, "FamilyInfo", "ℹ️ Family Info", asm,
                    "BIMBotPlugin.Commands.Tool_GetFamilyInfo", "Get detailed family information");
                AddPulldownItem(familiesPd, "CreateParam", "➕ Create Parameter", asm,
                    "BIMBotPlugin.Commands.Tool_CreateProjectParameter", "Create a new project parameter");
                AddPulldownItem(familiesPd, "BatchSetParam", "✏️ Batch Set Parameter", asm,
                    "BIMBotPlugin.Commands.Tool_BatchSetParameter", "Set parameter values in batch");
                AddPulldownItem(familiesPd, "DeleteUnused", "🗑️ Delete Unused", asm,
                    "BIMBotPlugin.Commands.Tool_DeleteUnusedFamilies", "Remove unused families from project");

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
                    "BIMBotPlugin.Commands.Tool_CreateElevationViews", "Auto-generate elevation views");
                AddPulldownItem(viewsPd, "Sections", "✂️ Create Sections", asm,
                    "BIMBotPlugin.Commands.Tool_CreateSectionViews", "Auto-generate section views");
                AddPulldownItem(viewsPd, "Callouts", "🔍 Create Callouts", asm,
                    "BIMBotPlugin.Commands.Tool_CreateCalloutViews", "Auto-generate callout views");

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
                    "BIMBotPlugin.Commands.Tool_AlignViewports", "Align viewports across sheets");
                AddPulldownItem(sheetsPd, "BatchSheets", "📑 Batch Create Sheets", asm,
                    "BIMBotPlugin.Commands.Tool_BatchCreateSheets", "Create multiple sheets at once");
                AddPulldownItem(sheetsPd, "DuplicateView", "📋 Duplicate View", asm,
                    "BIMBotPlugin.Commands.Tool_DuplicateView", "Duplicate views with options");
                AddPulldownItem(sheetsPd, "ApplyTemplate", "🎨 Apply View Template", asm,
                    "BIMBotPlugin.Commands.Tool_ApplyViewTemplate", "Apply view template to views");

                corePanel.AddSeparator();

                // ========================================
                // Utility buttons (Settings + Updates) stacked
                // ========================================
                var settingsData = new PushButtonData("MCPSettings", "Settings", asm,
                    "BIMBotPlugin.Commands.SettingsCommand")
                {
                    ToolTip = "Configure BIM-Bot connection settings",
                    LargeImage = RibbonIcons.Settings(32),
                    Image = RibbonIcons.Settings(16)
                };

                var updateData = new PushButtonData("MCPUpdate", "Check\nUpdates", asm,
                    "BIMBotPlugin.Commands.CheckUpdateCommand")
                {
                    ToolTip = "Check for plugin updates on GitHub",
                    LargeImage = RibbonIcons.CheckUpdates(32),
                    Image = RibbonIcons.CheckUpdates(16)
                };

                corePanel.AddItem(settingsData);
                corePanel.AddItem(updateData);

                // Initialize the external event manager
                _eventManager = new ExternalEventManager();

                // Register Idling event for one-time startup update check
                application.Idling += OnRevitIdling;

                Logger.Log("BIM-Bot Plugin started successfully — all ribbon buttons registered");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start BIM-Bot Plugin", ex);
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
                Logger.Log("BIM-Bot Plugin shut down");
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
            UpdateToggleButtonState();
            Logger.Log("BIM-Bot Service started on port 8080");
        }

        public static void StopService()
        {
            _socketService?.Stop();
            UpdateToggleButtonState();
            Logger.Log("BIM-Bot Service stopped");
        }

        public static bool IsServiceRunning => _socketService?.IsRunning ?? false;

        // ========================================
        // Dynamic button state updates
        // ========================================

        /// <summary>Update the Start BIM-Bot button icon/text based on service state.</summary>
        public static void UpdateToggleButtonState()
        {
            if (_toggleServiceButton == null) return;
            try
            {
                bool running = IsServiceRunning;
                var baseIcon32 = RibbonIcons.StartService(32);
                var baseIcon16 = RibbonIcons.StartService(16);
                _toggleServiceButton.ItemText = running ? "BIM-Bot\n✓ ON" : "Start\nBIM-Bot";
                _toggleServiceButton.LargeImage = running
                    ? RibbonIcons.WithCheckBadge(baseIcon32, 32)
                    : RibbonIcons.WithCrossBadge(baseIcon32, 32);
                _toggleServiceButton.Image = running
                    ? RibbonIcons.WithCheckBadge(baseIcon16, 16)
                    : RibbonIcons.WithCrossBadge(baseIcon16, 16);
                _toggleServiceButton.ToolTip = running
                    ? "BIM-Bot service is running — click to stop"
                    : "Start the BIM-Bot service for AI integration";
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update toggle button state", ex);
            }
        }


        /// <summary>
        /// One-time Idling handler that checks for updates in the background on Revit startup.
        /// </summary>
        private static async void OnRevitIdling(object? sender, IdlingEventArgs e)
        {
            if (_startupUpdateChecked) return;
            _startupUpdateChecked = true;

            // Unsubscribe immediately — only need this once
            if (sender is UIApplication uiApp)
            {
                ActiveUIApp = uiApp;
                uiApp.Idling -= OnRevitIdling;

                // Auto-start BIM-Bot service so users don't need to click "Start BIM-Bot" manually
                try
                {
                    StartService(uiApp);
                    Logger.Log("BIM-Bot service auto-started on Revit idle");
                }
                catch (Exception startEx)
                {
                    Logger.LogError("BIM-Bot service auto-start failed (can start manually from ribbon)", startEx);
                }
            }

            try
            {
                var checker = new UpdateChecker();
                var updateInfo = await Task.Run(() => checker.CheckForUpdateAsync());

                if (updateInfo.UpdateAvailable)
                {
                    // Check if user already skipped this version
                    var skippedVersion = UpdateChecker.GetSkippedVersion();
                    if (skippedVersion == updateInfo.LatestVersion)
                    {
                        Logger.Log($"Update {updateInfo.LatestVersion} available but skipped by user.");
                        return;
                    }

                    Logger.Log($"Update available: {updateInfo.LatestVersion}");

                    // Show notification window on the UI thread
                    var window = new UpdateNotificationWindow(updateInfo);
                    window.ShowDialog();
                }
                else
                {
                    Logger.Log("Plugin is up to date.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Startup update check failed (non-critical)", ex);
            }
        }
    }
}

