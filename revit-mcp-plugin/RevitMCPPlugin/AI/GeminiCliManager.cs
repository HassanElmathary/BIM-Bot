using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.AI
{
    /// <summary>
    /// Manages Gemini CLI prerequisites: Node.js detection, CLI installation,
    /// and dynamic generation of the .gemini/settings.json config file.
    /// </summary>
    public class GeminiCliManager
    {
        private string _detectedNodePath;
        private string _detectedCliPath;

        /// <summary>Check if Node.js is installed and reachable.</summary>
        public bool CheckNodeInstalled()
        {
            try
            {
                var result = RunQuickProcess("node", "--version");
                if (result.ExitCode == 0 && result.Output.TrimStart().StartsWith("v"))
                {
                    _detectedNodePath = "node";
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Check if the Gemini CLI is installed (globally or via npx).</summary>
        public bool CheckGeminiCliInstalled()
        {
            // Try global install first
            try
            {
                var result = RunQuickProcess("gemini", "--version");
                if (result.ExitCode == 0)
                {
                    _detectedCliPath = "gemini";
                    return true;
                }
            }
            catch { }

            // Try npx
            try
            {
                var result = RunQuickProcess("npx", "@google/gemini-cli --version");
                if (result.ExitCode == 0)
                {
                    _detectedCliPath = "npx";
                    return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>Install the Gemini CLI globally via npm.</summary>
        public async Task<bool> InstallGeminiCliAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "install -g @google/gemini-cli",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return false;

                await Task.Run(() => proc.WaitForExit());
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates the ~/.gemini/settings.json config file pointing to the Revit MCP server.
        /// </summary>
        public bool GenerateCliConfig(GeminiCliSettings settings)
        {
            try
            {
                var geminiDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gemini");
                if (!Directory.Exists(geminiDir))
                    Directory.CreateDirectory(geminiDir);

                var configPath = Path.Combine(geminiDir, "settings.json");

                // Resolve MCP server path
                var serverPath = ResolveServerPath(settings.McpServerPath);

                var config = new JObject
                {
                    ["mcpServers"] = new JObject
                    {
                        ["revit-mcp"] = new JObject
                        {
                            ["command"] = "node",
                            ["args"] = new JArray { serverPath },
                            ["env"] = new JObject()
                        }
                    }
                };

                // If settings.json already exists, merge our server into it
                if (File.Exists(configPath))
                {
                    try
                    {
                        var existing = JObject.Parse(File.ReadAllText(configPath));
                        var servers = existing["mcpServers"] as JObject ?? new JObject();
                        servers["revit-mcp"] = config["mcpServers"]!["revit-mcp"];
                        existing["mcpServers"] = servers;
                        config = existing;
                    }
                    catch
                    {
                        // Existing file is corrupt — overwrite
                    }
                }

                File.WriteAllText(configPath, config.ToString(Formatting.Indented));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Get a comprehensive status report for the UI.</summary>
        public PrerequisiteStatus GetPrerequisiteStatus()
        {
            var status = new PrerequisiteStatus
            {
                NodeInstalled = CheckNodeInstalled(),
                CliInstalled = CheckGeminiCliInstalled(),
                DetectedNodePath = _detectedNodePath ?? "",
                DetectedCliPath = _detectedCliPath ?? ""
            };

            // Check for config file
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gemini", "settings.json");
            status.ConfigExists = File.Exists(configPath);

            return status;
        }

        /// <summary>
        /// Get the command and arguments to invoke the Gemini CLI.
        /// Returns (command, baseArgs) — e.g. ("gemini", "") or ("npx", "@google/gemini-cli").
        /// </summary>
        public (string Command, string BaseArgs) GetCliInvocation(GeminiCliSettings settings)
        {
            // User-specified path takes priority
            if (!string.IsNullOrWhiteSpace(settings.GeminiCliPath))
                return (settings.GeminiCliPath, "");

            // Global install
            if (_detectedCliPath == "gemini")
                return ("gemini", "");

            // npx fallback
            return ("npx", "-y @google/gemini-cli");
        }

        // ── Helpers ──

        /// <summary>Resolve the absolute path to the MCP server's index.js.</summary>
        private string ResolveServerPath(string customPath)
        {
            // Custom override
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                return customPath.Replace("\\", "/");

            // Auto-detect: relative to this plugin assembly
            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(pluginDir))
            {
                // Check common locations
                var candidates = new[]
                {
                    Path.Combine(pluginDir, "..", "server", "build", "index.js"),
                    Path.Combine(pluginDir, "..", "..", "server", "build", "index.js"),
                    Path.Combine(pluginDir, "..", "..", "revit-mcp-server", "build", "index.js"),
                };

                foreach (var candidate in candidates)
                {
                    var resolved = Path.GetFullPath(candidate);
                    if (File.Exists(resolved))
                        return resolved.Replace("\\", "/");
                }
            }

            // Installed location (dist folder)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var installedPath = Path.Combine(appData, "RevitMCP", "server", "build", "index.js");
            if (File.Exists(installedPath))
                return installedPath.Replace("\\", "/");

            // Fallback: return a placeholder — user will need to set McpServerPath manually
            return "build/index.js";
        }

        private (int ExitCode, string Output) RunQuickProcess(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (-1, "");

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return (proc.ExitCode, output);
        }
    }

    /// <summary>Aggregate prerequisite status for UI display.</summary>
    public class PrerequisiteStatus
    {
        public bool NodeInstalled { get; set; }
        public bool CliInstalled { get; set; }
        public bool ConfigExists { get; set; }
        public string DetectedNodePath { get; set; } = "";
        public string DetectedCliPath { get; set; } = "";

        public bool AllReady => NodeInstalled && CliInstalled && ConfigExists;

        public string Summary
        {
            get
            {
                if (AllReady) return "✅ All prerequisites met — ready to use Gemini CLI.";
                var parts = new System.Collections.Generic.List<string>();
                if (!NodeInstalled) parts.Add("❌ Node.js not found");
                if (!CliInstalled) parts.Add("❌ Gemini CLI not installed");
                if (!ConfigExists) parts.Add("⚠️ MCP config not generated yet");
                return string.Join("\n", parts);
            }
        }
    }
}
