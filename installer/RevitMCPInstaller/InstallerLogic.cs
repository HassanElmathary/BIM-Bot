using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCPInstaller
{
    /// <summary>
    /// Core installer logic: detect Revit versions, copy files, manage manifests.
    /// </summary>
    public class InstallerLogic
    {
        public string PluginSourceDir { get; set; } = "";
        public string ServerSourceDir { get; set; } = "";
        public string NodeSourceDir { get; set; } = "";

        public static string InstallDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BIMBot");

        public event Action<string>? OnProgress;
        public event Action<int>? OnPercentChanged;

        /// <summary>
        /// Detect installed Revit versions by checking standard install paths.
        /// </summary>
        public List<RevitVersion> DetectRevitVersions()
        {
            var versions = new List<RevitVersion>();
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            for (int year = 2020; year <= 2027; year++)
            {
                var revitPath = Path.Combine(pf, "Autodesk", $"Revit {year}");
                var exists = Directory.Exists(revitPath);
                if (exists)
                {
                    versions.Add(new RevitVersion
                    {
                        Year = year,
                        IsInstalled = true,
                        IsSelected = true,
                        InstallPath = revitPath
                    });
                }
            }

            return versions;
        }

        /// <summary>
        /// Get the user's Revit AddIns directory for a year.
        /// </summary>
        public static string GetAddinsDir(int year) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", year.ToString());

        /// <summary>
        /// Run the full installation.
        /// </summary>
        public void Install(List<int> selectedYears)
        {
            int totalSteps = 3 + selectedYears.Count;
            int step = 0;

            // 1. Create install directory
            Report("Preparing installation directory...");
            Directory.CreateDirectory(InstallDir);
            step++;
            OnPercentChanged?.Invoke(step * 100 / totalSteps);

            // 2. Copy plugin files to install dir
            Report("Copying plugin files...");
            var pluginDest = Path.Combine(InstallDir, "plugin");
            CopyDirectory(PluginSourceDir, pluginDest);
            step++;
            OnPercentChanged?.Invoke(step * 100 / totalSteps);

            // 3. Copy MCP server + Node.js if available
            if (!string.IsNullOrEmpty(ServerSourceDir) && Directory.Exists(ServerSourceDir))
            {
                Report("Copying MCP server...");
                CopyDirectory(ServerSourceDir, Path.Combine(InstallDir, "server"));
            }
            if (!string.IsNullOrEmpty(NodeSourceDir) && Directory.Exists(NodeSourceDir))
            {
                Report("Copying Node.js runtime...");
                CopyDirectory(NodeSourceDir, Path.Combine(InstallDir, "nodejs"));
            }
            step++;
            OnPercentChanged?.Invoke(step * 100 / totalSteps);

            // 4. Install to each Revit version
            foreach (var year in selectedYears)
            {
                Report($"Installing for Revit {year}...");
                InstallForRevit(year);
                step++;
                OnPercentChanged?.Invoke(step * 100 / totalSteps);
            }

            // 5. Create launcher
            CreateLauncher();

            // 6. Auto-configure Claude Desktop MCP
            Report("Configuring Claude Desktop...");
            ConfigureClaudeMCP();

            Report("Installation complete!");
            OnPercentChanged?.Invoke(100);
        }

        private void InstallForRevit(int year)
        {
            var addinsDir = GetAddinsDir(year);
            Directory.CreateDirectory(addinsDir);

            // Copy DLLs to the per-version BIMBot dir
            var mcpDir = Path.Combine(addinsDir, "BIMBot");
            Directory.CreateDirectory(mcpDir);

            string fw = "net8";
            if (year <= 2024) fw = "net48";
            else if (year >= 2027) fw = "net10";

            var pluginDir = Path.Combine(InstallDir, "plugin", fw);
            if (Directory.Exists(pluginDir))
            {
                CopyDirectory(pluginDir, mcpDir);
            }

            // Write .addin manifest
            var assemblyPath = Path.Combine(mcpDir, "BIMBotPlugin.dll");
            var addinPath = Path.Combine(addinsDir, "BIMBot.addin");
            var addinContent =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>BIM-Bot Plugin</Name>
    <Assembly>{assemblyPath}</Assembly>
    <FullClassName>BIMBotPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>AI-Powered BIM-Bot Plugin by Hassan Ahmed Elmathary</VendorDescription>
  </AddIn>
</RevitAddIns>";
            File.WriteAllText(addinPath, addinContent);
        }

        private void CreateLauncher()
        {
            var nodeExe = Path.Combine(InstallDir, "nodejs", "node.exe");
            if (!File.Exists(nodeExe)) return;

            var batPath = Path.Combine(InstallDir, "Start MCP Server.bat");
            var serverEntry = Path.Combine(InstallDir, "server", "build", "index.js");
            File.WriteAllText(batPath,
$@"@echo off
title BIM-Bot Server
echo ========================================
echo   BIM-Bot Server - Starting...
echo   by Hassan Ahmed Elmathary
echo ========================================
echo.
""{nodeExe}"" ""{serverEntry}""
pause
");
        }

        /// <summary>
        /// Auto-configure Claude Desktop MCP connection.
        /// First tries bundled configure-claude.cjs, then falls back to direct JSON editing.
        /// </summary>
        private void ConfigureClaudeMCP()
        {
            var nodeExe = Path.Combine(InstallDir, "nodejs", "node.exe");
            var script = Path.Combine(InstallDir, "server", "scripts", "configure-claude.cjs");

            // Try Node.js script first (handles Claude Desktop + Claude Code)
            if (File.Exists(nodeExe) && File.Exists(script))
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = nodeExe,
                        Arguments = $"\"{script}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(15000);
                        if (proc.ExitCode == 0)
                        {
                            Report("Claude Desktop configured via script.");
                            return;
                        }
                    }
                }
                catch { /* fall through to manual config */ }
            }

            // Fallback: directly write Claude Desktop config
            ConfigureClaudeFallback(nodeExe);
        }

        private void ConfigureClaudeFallback(string nodeExe)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var claudeDir = Path.Combine(appData, "Claude");
                var configPath = Path.Combine(claudeDir, "claude_desktop_config.json");
                var serverJs = Path.Combine(InstallDir, "server", "build", "index.js");

                // Check if Claude Desktop is installed
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var claudeInstalled = Directory.Exists(claudeDir) ||
                                      Directory.Exists(Path.Combine(localAppData, "AnthropicClaude"));

                if (!claudeInstalled)
                {
                    Report("Claude Desktop not detected — skipped MCP config.");
                    return;
                }

                // Read existing config or create new
                string json = "{}";
                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                    // Strip BOM
                    if (json.Length > 0 && json[0] == '\uFEFF')
                        json = json.Substring(1);
                }

                // Parse with Newtonsoft.Json (already a dependency)
                var config = Newtonsoft.Json.Linq.JObject.Parse(json);

                if (config["mcpServers"] == null)
                    config["mcpServers"] = new Newtonsoft.Json.Linq.JObject();

                var servers = (Newtonsoft.Json.Linq.JObject)config["mcpServers"]!;

                // Check if already configured and valid
                if (servers["BIM-Bot"] is Newtonsoft.Json.Linq.JObject existing)
                {
                    var cmd = existing["command"]?.ToString();
                    var args = existing["args"]?[0]?.ToString();
                    if (!string.IsNullOrEmpty(cmd) && File.Exists(cmd) &&
                        !string.IsNullOrEmpty(args) && File.Exists(args))
                    {
                        Report("Claude Desktop already configured.");
                        return;
                    }
                }

                // Add/update BIM-Bot entry
                servers["BIM-Bot"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["command"] = nodeExe,
                    ["args"] = new Newtonsoft.Json.Linq.JArray { serverJs },
                    ["env"] = new Newtonsoft.Json.Linq.JObject()
                };

                // Backup existing config
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", true);

                Directory.CreateDirectory(claudeDir);
                File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));
                Report("Claude Desktop MCP configured successfully.");
            }
            catch (Exception ex)
            {
                Report($"Claude config: {ex.Message} (non-fatal)");
            }
        }

        /// <summary>
        /// Uninstall everything.
        /// </summary>
        public static void Uninstall()
        {
            for (int year = 2020; year <= 2027; year++)
            {
                try
                {
                    var addinPath = Path.Combine(GetAddinsDir(year), "BIMBot.addin");
                    if (File.Exists(addinPath)) File.Delete(addinPath);

                    var mcpDir = Path.Combine(GetAddinsDir(year), "BIMBot");
                    if (Directory.Exists(mcpDir)) Directory.Delete(mcpDir, true);
                }
                catch { }
            }

            try
            {
                if (Directory.Exists(InstallDir))
                    Directory.Delete(InstallDir, true);
            }
            catch { }
        }

        /// <summary>
        /// Check if the plugin is currently installed.
        /// </summary>
        public static bool IsAlreadyInstalled()
        {
            return Directory.Exists(InstallDir) &&
                   Directory.Exists(Path.Combine(InstallDir, "plugin"));
        }

        private void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        private void Report(string msg) => OnProgress?.Invoke(msg);
    }

    public class RevitVersion
    {
        public int Year { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsSelected { get; set; }
        public string InstallPath { get; set; } = "";
    }
}
