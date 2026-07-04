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

        public static string Version => "2.1.0";

        public Result OnStartup(UIControlledApplication application)
        {
            UiApp = application;

            // ── Diagnostic: write before anything else so we can see where the crash is ──
            var _diagLog = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIMBot", "logs", $"startup-diag-{DateTime.Now:HHmmss}.log");
            Action<string> _diag = msg =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_diagLog)!);
                    System.IO.File.AppendAllText(_diagLog, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
                }
                catch { }
            };
            _diag("OnStartup entered");

            try
            {
                _diag("try block entered");
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
                    "BIMBotPlugin.Commands.Tool_ExportToPowerBI", "Export 3D model + parameters to a ready-to-open Power BI dashboard (.pbit)");

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

                // ========================================
                // QA/QC Pulldown
                // ========================================
                var qaqcPd = corePanel.AddItem(
                    new PulldownButtonData("MCPQaQc", "QA/QC")
                    {
                        ToolTip = "Quality assurance tools — clash detection, warnings, model audit",
                        LargeImage = RibbonIcons.ToolsHub(32),
                        Image = RibbonIcons.ToolsHub(16)
                    }) as PulldownButton;

                AddPulldownItem(qaqcPd, "ClashDetection", "⚡ Clash Report Viewer", asm,
                    "BIMBotPlugin.Commands.Tool_ClashDetection",
                    "Load a Navisworks clash report HTML and zoom/select clashing elements in Revit");

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
                _diag("OnStartup succeeded");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _diag($"CAUGHT EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
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
        /// Idling handler that auto-starts the BIM-Bot service on first Revit idle,
        /// retries if it fails, and periodically health-checks the service.
        /// </summary>
        private static int _autoStartAttempts = 0;
        private const int MaxAutoStartAttempts = 10;
        private static DateTime _lastHealthCheck = DateTime.MinValue;
        private const double HealthCheckIntervalSeconds = 30;

        private static async void OnRevitIdling(object? sender, IdlingEventArgs e)
        {
            if (sender is not UIApplication uiApp) return;
            ActiveUIApp = uiApp;

            // ── Phase 1: One-time auto-start with retry ──
            if (!_startupUpdateChecked)
            {
                _autoStartAttempts++;

                if (!IsServiceRunning)
                {
                    try
                    {
                        StartService(uiApp);
                        Logger.Log($"BIM-Bot service auto-started on Revit idle (attempt {_autoStartAttempts})");
                    }
                    catch (Exception startEx)
                    {
                        Logger.LogError($"Auto-start attempt {_autoStartAttempts}/{MaxAutoStartAttempts} failed", startEx);

                        if (_autoStartAttempts < MaxAutoStartAttempts)
                        {
                            // Stay subscribed — will retry on next Idling event (~100ms later)
                            return;
                        }

                        Logger.LogError("All auto-start attempts exhausted. User can start manually from ribbon.");
                    }
                }

                // Mark startup phase complete (whether service started or all retries exhausted)
                _startupUpdateChecked = true;

                // Don't unsubscribe from Idling — we keep it for the health check below

                // Self-heal Claude configs in the background: repairs stale
                // BIM-Bot entries (e.g. after the install/repo path moved).
                // Only writes when an entry is missing or broken.
                _ = Task.Run(() => ClaudeConfigService.EnsureAllSilent());

                // Background update check (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var checker = new UpdateChecker();
                        var updateInfo = await checker.CheckForUpdateAsync();

                        if (updateInfo.UpdateAvailable)
                        {
                            var skippedVersion = UpdateChecker.GetSkippedVersion();
                            if (skippedVersion == updateInfo.LatestVersion)
                            {
                                Logger.Log($"Update {updateInfo.LatestVersion} available but skipped by user.");
                                return;
                            }

                            Logger.Log($"Update available: {updateInfo.LatestVersion}");
                            // Queue UI notification for next idle
                            _pendingUpdateInfo = updateInfo;
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
                });

                return;
            }

            // ── Show pending update notification on UI thread ──
            if (_pendingUpdateInfo != null)
            {
                var info = _pendingUpdateInfo;
                _pendingUpdateInfo = null;
                try
                {
                    var window = new UpdateNotificationWindow(info);
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to show update notification", ex);
                }
                return;
            }

            // ── Phase 2: Periodic health check (runs every 30s) ──
            var now = DateTime.Now;
            if ((now - _lastHealthCheck).TotalSeconds < HealthCheckIntervalSeconds)
                return;
            _lastHealthCheck = now;

            if (!IsServiceRunning)
            {
                Logger.Log("Health check: service is down — auto-restarting...");
                try
                {
                    StartService(uiApp);
                    Logger.Log("Health check: service restarted successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Health check: auto-restart failed", ex);
                }
            }
        }

        private static UpdateInfo? _pendingUpdateInfo;
    }
}

