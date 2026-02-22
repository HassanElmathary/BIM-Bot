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
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RevitMCP");

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

            Report("Installation complete!");
            OnPercentChanged?.Invoke(100);
        }

        private void InstallForRevit(int year)
        {
            var addinsDir = GetAddinsDir(year);
            Directory.CreateDirectory(addinsDir);

            // Copy DLLs to the per-version RevitMCP dir
            var mcpDir = Path.Combine(addinsDir, "RevitMCP");
            Directory.CreateDirectory(mcpDir);

            var pluginDir = Path.Combine(InstallDir, "plugin");
            if (Directory.Exists(pluginDir))
            {
                foreach (var file in Directory.GetFiles(pluginDir))
                {
                    File.Copy(file, Path.Combine(mcpDir, Path.GetFileName(file)), true);
                }
            }

            // Write .addin manifest
            var assemblyPath = Path.Combine(mcpDir, "RevitMCPPlugin.dll");
            var addinPath = Path.Combine(addinsDir, "RevitMCP.addin");
            var addinContent =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>Revit MCP Plugin</Name>
    <Assembly>{assemblyPath}</Assembly>
    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>Chat with me - AI-Powered Revit Plugin by Hassan Ahmed Elmathary</VendorDescription>
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
title Revit MCP Server
echo ========================================
echo   Revit MCP Server - Starting...
echo   by Hassan Ahmed Elmathary
echo ========================================
echo.
""{nodeExe}"" ""{serverEntry}""
pause
");
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
                    var addinPath = Path.Combine(GetAddinsDir(year), "RevitMCP.addin");
                    if (File.Exists(addinPath)) File.Delete(addinPath);

                    var mcpDir = Path.Combine(GetAddinsDir(year), "RevitMCP");
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
