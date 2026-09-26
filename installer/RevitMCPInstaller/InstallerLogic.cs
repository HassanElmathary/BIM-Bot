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

        // Per-user install dir — writable without elevation so updates launched
        // from inside Revit never hit a UAC admin-password prompt.
        public static string InstallDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "BIMBot");

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
        /// Uninstall everything this product ever created, in every scope:
        /// Revit manifests + copied DLLs (both Addins scopes, all folder names
        /// ever used), the app folder, per-user runtime data, and the BIM-Bot
        /// entry in every supported MCP client config. Files locked by a
        /// running Revit/node are scheduled for deletion on reboot so no
        /// residue survives.
        /// </summary>
        public static void Uninstall()
        {
            StopBimBotServers();

            // 1. MCP client configs FIRST (Node remover needs InstallDir files).
            RemoveMcpConfigsViaNode();
            RemoveMcpClientEntries();

            // 2. Revit addins: both scopes + both manifest names + all three
            //    plugin folder names (BIMBot, BIMBotPlugin, RevitMCP).
            var addinRoots = new List<string>();
            try
            {
                addinRoots.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins"));
                var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrEmpty(common))
                    addinRoots.Add(Path.Combine(common, "Autodesk", "Revit", "Addins"));
                // Other users' per-user addins (best effort, admin only matters).
                var usersRoot = Path.GetDirectoryName(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                if (!string.IsNullOrEmpty(usersRoot) && Directory.Exists(usersRoot))
                {
                    foreach (var dir in Directory.GetDirectories(usersRoot))
                    {
                        var roaming = Path.Combine(dir, "AppData", "Roaming",
                            "Autodesk", "Revit", "Addins");
                        if (Directory.Exists(roaming) && !addinRoots.Contains(roaming))
                            addinRoots.Add(roaming);
                    }
                }
            }
            catch { }

            string[] manifests = { "BIMBot.addin", "RevitMCP.addin" };
            string[] pluginDirs = { "BIMBot", "BIMBotPlugin", "RevitMCP" };
            for (int year = 2020; year <= 2027; year++)
            {
                foreach (var root in addinRoots)
                {
                    try
                    {
                        var yearDir = Path.Combine(root, year.ToString());
                        if (!Directory.Exists(yearDir)) continue;
                        foreach (var m in manifests)
                        {
                            var f = Path.Combine(yearDir, m);
                            if (File.Exists(f)) DeleteFileOrSchedule(f);
                        }
                        foreach (var d in pluginDirs)
                        {
                            var dir = Path.Combine(yearDir, d);
                            if (Directory.Exists(dir)) DeleteDirOrSchedule(dir);
                        }
                    }
                    catch { }
                }
            }

            // 3. App folders: this installer's per-user dir + the legacy
            //    install.ps1 per-user layout + runtime data dirs.
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            DeleteDirOrSchedule(InstallDir);
            if (!string.IsNullOrEmpty(localApp))
                DeleteDirOrSchedule(Path.Combine(localApp, "BIMBot"));
            if (!string.IsNullOrEmpty(roamingApp))
                DeleteDirOrSchedule(Path.Combine(roamingApp, "BIMBot"));

            // 4. Legacy product folder (admin scope only, best effort).
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var legacy = Path.Combine(pf, "RevitMCP");
                if (Directory.Exists(legacy)) DeleteDirOrSchedule(legacy);
            }
            catch { }
        }

        /// <summary>
        /// Stop BIM-Bot node servers started from our InstallDir so their
        /// files are not locked during deletion. Only touches node processes
        /// whose executable lives under InstallDir.
        /// </summary>
        private static void StopBimBotServers()
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("node"))
                {
                    try
                    {
                        var file = proc.MainModule?.FileName ?? "";
                        if (file.StartsWith(InstallDir, StringComparison.OrdinalIgnoreCase))
                            proc.Kill();
                    }
                    catch { /* access denied / already exited */ }
                }
            }
            catch { }
        }

        /// <summary>
        /// Run the bundled configure-claude.cjs --remove before InstallDir is
        /// deleted. Falls through silently — the managed JSON cleanup below
        /// covers a missing runtime.
        /// </summary>
        private static void RemoveMcpConfigsViaNode()
        {
            try
            {
                var nodeExe = Path.Combine(InstallDir, "nodejs", "node.exe");
                var script = Path.Combine(InstallDir, "server", "scripts", "configure-claude.cjs");
                if (!File.Exists(nodeExe) || !File.Exists(script)) return;
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = nodeExe,
                    Arguments = $"\"{script}\" --remove",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(15000);
            }
            catch { }
        }

        /// <summary>
        /// Remove the BIM-Bot entry from every supported MCP client config in
        /// the current profile. Other servers are preserved; each touched file
        /// is backed up to *.bimbot-backup first.
        /// </summary>
        private static void RemoveMcpClientEntries()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                RemoveMcpEntry(Path.Combine(appData, "Claude", "claude_desktop_config.json"), "mcpServers");
                RemoveMcpEntry(Path.Combine(userProfile, ".claude.json"), "mcpServers");
                RemoveMcpEntry(Path.Combine(userProfile, ".cursor", "mcp.json"), "mcpServers");
                RemoveMcpEntry(Path.Combine(userProfile, ".codeium", "windsurf", "mcp_config.json"), "mcpServers");
                RemoveMcpEntry(Path.Combine(userProfile, ".gemini", "settings.json"), "mcpServers");
                RemoveMcpEntry(Path.Combine(appData, "Code", "User", "mcp.json"), "servers");
                RemoveMcpEntry(Path.Combine(appData, "Code - Insiders", "User", "mcp.json"), "servers");

                // MS-Store Claude variants.
                try
                {
                    var pkgs = Path.Combine(localApp, "Packages");
                    if (Directory.Exists(pkgs))
                    {
                        foreach (var dir in Directory.GetDirectories(pkgs))
                        {
                            var name = Path.GetFileName(dir);
                            if (name.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase) ||
                                name.StartsWith("AnthropicClaude", StringComparison.OrdinalIgnoreCase))
                            {
                                RemoveMcpEntry(Path.Combine(dir, "LocalCache", "Roaming",
                                    "Claude", "claude_desktop_config.json"), "mcpServers");
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private static void RemoveMcpEntry(string configPath, string serversKey)
        {
            try
            {
                if (!File.Exists(configPath)) return;
                var raw = File.ReadAllText(configPath);
                if (raw.Length > 0 && raw[0] == '\uFEFF')
                    raw = raw.Substring(1);
                var config = Newtonsoft.Json.Linq.JObject.Parse(raw);
                var servers = config[serversKey] as Newtonsoft.Json.Linq.JObject;
                if (servers == null || servers["BIM-Bot"] == null) return;
                servers.Remove("BIM-Bot");
                try { File.Copy(configPath, configPath + ".bimbot-backup", true); } catch { }
                File.WriteAllText(configPath,
                    config.ToString(Newtonsoft.Json.Formatting.Indented),
                    new System.Text.UTF8Encoding(false));
            }
            catch { /* corrupt config: leave untouched */ }
        }

        private static void DeleteFileOrSchedule(string file)
        {
            try { File.Delete(file); }
            catch { ScheduleDeleteOnReboot(file); }
        }

        private static void DeleteDirOrSchedule(string dir)
        {
            if (!Directory.Exists(dir)) return;
            try { Directory.Delete(dir, true); }
            catch
            {
                // Best effort: delete what we can, schedule the rest. Files
                // that are individually locked are scheduled one by one.
                try
                {
                    foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); } catch { ScheduleDeleteOnReboot(f); }
                    }
                    Directory.Delete(dir, true);
                }
                catch { ScheduleDeleteOnReboot(dir); }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existing, string @new, int flags);

        private static void ScheduleDeleteOnReboot(string path)
        {
            try { MoveFileEx(path, null, 0x4); } catch { } // MOVEFILE_DELAY_UNTIL_REBOOT
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
