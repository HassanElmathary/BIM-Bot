using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Ensures Claude Desktop and Claude Code are configured to launch the
    /// BIM-Bot MCP server, and — critically — repairs stale entries whose
    /// paths no longer exist (e.g. after the repo or install folder moved).
    ///
    /// Called silently on Revit startup (self-heal) and interactively from
    /// the "Connect Claude" ribbon button.
    /// </summary>
    public static class ClaudeConfigService
    {
        private const string ServerKey = "BIM-Bot";

        public class ConfigureResult
        {
            public string Target = "";
            public bool Configured;      // entry is now valid
            public bool Changed;         // we wrote the file
            public string Detail = "";
        }

        // ────────────────────────────────────────────────────────
        // Path resolution
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Locate the MCP server entry point. Prefers the installed layout
        /// (…\BIMBot\server\build\index.js next to the plugin), falls back
        /// to the dev repo layout (revit-mcp-server\build\index.js found by
        /// walking up from the plugin DLL).
        /// </summary>
        public static string? ResolveServerIndexJs()
        {
            var pluginDllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (pluginDllDir == null) return null;

            // Installed layout: {app}\plugin\BIMBotPlugin.dll → {app}\server\build\index.js
            var appDir = Path.GetDirectoryName(pluginDllDir);
            if (appDir != null)
            {
                var installed = Path.Combine(appDir, "server", "build", "index.js");
                if (File.Exists(installed)) return installed;
            }

            // Dev layout: walk up from bin\Release\net48 looking for revit-mcp-server\build\index.js
            var dir = pluginDllDir;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                var dev = Path.Combine(dir, "revit-mcp-server", "build", "index.js");
                if (File.Exists(dev)) return dev;
                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        /// <summary>
        /// Locate node.exe. Prefers the bundled runtime shipped by the
        /// installer, then a system-wide install, then PATH.
        /// </summary>
        public static string ResolveNodeExe()
        {
            var pluginDllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var appDir = pluginDllDir != null ? Path.GetDirectoryName(pluginDllDir) : null;

            var candidates = new List<string>();
            if (appDir != null)
                candidates.Add(Path.Combine(appDir, "nodejs", "node.exe"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"));

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            // Search PATH for node.exe so the config gets an absolute path —
            // GUI apps like Claude Desktop don't always inherit the user PATH.
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    var probe = Path.Combine(p.Trim(), "node.exe");
                    if (File.Exists(probe)) return probe;
                }
                catch { /* malformed PATH segment */ }
            }

            return "node"; // last resort — rely on the client resolving it
        }

        // ────────────────────────────────────────────────────────
        // Entry validation & repair
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// True when an existing mcpServers entry is launchable:
        /// command exists (or is a bare name) AND the server script exists.
        /// </summary>
        private static bool IsEntryValid(JObject? entry)
        {
            if (entry == null) return false;

            var command = entry["command"]?.ToString();
            if (string.IsNullOrWhiteSpace(command)) return false;

            // Absolute command path must exist; bare names ("node") are risky
            // for GUI-launched clients, so treat them as needing repair too.
            if (!Path.IsPathRooted(command) || !File.Exists(command)) return false;

            var args = entry["args"] as JArray;
            var script = args != null && args.Count > 0 ? args[0]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(script) || !File.Exists(script)) return false;

            return true;
        }

        private static JObject BuildEntry(string nodeExe, string indexJs, bool stdioType)
        {
            var entry = new JObject
            {
                ["command"] = nodeExe,
                ["args"] = new JArray { indexJs },
                ["env"] = new JObject()
            };
            // VS Code requires an explicit transport type on each server entry.
            if (stdioType) entry["type"] = "stdio";
            return entry;
        }

        /// <summary>
        /// Ensure the BIM-Bot entry in a JSON config file's "mcpServers"
        /// object is present and valid. Repairs stale entries. Creates the
        /// file when <paramref name="createIfMissing"/> is set. A timestamped
        /// backup is written before any modification.
        /// </summary>
        private static ConfigureResult EnsureConfigFile(
            string target, string configPath, string nodeExe, string indexJs, bool createIfMissing,
            string serversKey = "mcpServers", bool stdioType = false)
        {
            var result = new ConfigureResult { Target = target };

            JObject config;
            if (File.Exists(configPath))
            {
                try
                {
                    config = JObject.Parse(File.ReadAllText(configPath));
                }
                catch (Exception ex)
                {
                    // Never destroy a config we can't parse.
                    result.Detail = $"Config exists but is not valid JSON ({ex.Message}) — left untouched. Fix it manually: {configPath}";
                    return result;
                }
            }
            else if (createIfMissing)
            {
                config = new JObject();
            }
            else
            {
                result.Detail = "Not installed (config file not found) — skipped.";
                return result;
            }

            if (config[serversKey] is not JObject mcpServers)
            {
                mcpServers = new JObject();
                config[serversKey] = mcpServers;
            }

            var existing = mcpServers[ServerKey] as JObject;
            if (IsEntryValid(existing))
            {
                result.Configured = true;
                result.Detail = "Already configured correctly.";
                return result;
            }

            // Missing or stale → write the correct entry (backup first)
            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", overwrite: true);

                var dir = Path.GetDirectoryName(configPath);
                if (dir != null) Directory.CreateDirectory(dir);

                mcpServers[ServerKey] = BuildEntry(nodeExe, indexJs, stdioType);
                File.WriteAllText(configPath, config.ToString(Formatting.Indented));

                result.Configured = true;
                result.Changed = true;
                result.Detail = existing == null
                    ? "Added BIM-Bot entry."
                    : "Repaired stale BIM-Bot entry (old path no longer existed).";
            }
            catch (Exception ex)
            {
                result.Detail = $"Failed to write config: {ex.Message}";
            }

            return result;
        }

        // ────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Ensure every detected Claude client is configured. Only writes
        /// when an entry is missing or broken, so it is safe to call on
        /// every Revit startup.
        /// </summary>
        public static List<ConfigureResult> EnsureAll()
        {
            var results = new List<ConfigureResult>();

            var indexJs = ResolveServerIndexJs();
            if (indexJs == null)
            {
                results.Add(new ConfigureResult
                {
                    Target = "BIM-Bot server",
                    Detail = "Could not locate the MCP server (server\\build\\index.js). Reinstall BIM-Bot or run 'npm run build' in revit-mcp-server."
                });
                return results;
            }

            var nodeExe = ResolveNodeExe();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Claude Desktop — create the config if missing (Desktop may be
            // installed but never configured; the file only appears after use).
            var desktopConfig = Path.Combine(appData, "Claude", "claude_desktop_config.json");
            var desktopInstalled = Directory.Exists(Path.Combine(appData, "Claude"))
                || Directory.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnthropicClaude"));
            results.Add(EnsureConfigFile("Claude Desktop", desktopConfig, nodeExe, indexJs,
                createIfMissing: desktopInstalled));

            // Claude Code — only touch ~/.claude.json if it already exists.
            var claudeCodeConfig = Path.Combine(userProfile, ".claude.json");
            results.Add(EnsureConfigFile("Claude Code", claudeCodeConfig, nodeExe, indexJs,
                createIfMissing: false));

            // Cursor — same schema as Claude ("mcpServers"). Create the config
            // when the app has been run (its ~/.cursor folder exists).
            var cursorDir = Path.Combine(userProfile, ".cursor");
            var cursorConfig = Path.Combine(cursorDir, "mcp.json");
            results.Add(EnsureConfigFile("Cursor", cursorConfig, nodeExe, indexJs,
                createIfMissing: Directory.Exists(cursorDir)));

            // Windsurf (Codeium) — also uses the "mcpServers" schema.
            var windsurfDir = Path.Combine(userProfile, ".codeium", "windsurf");
            var windsurfConfig = Path.Combine(windsurfDir, "mcp_config.json");
            results.Add(EnsureConfigFile("Windsurf", windsurfConfig, nodeExe, indexJs,
                createIfMissing: Directory.Exists(Path.Combine(userProfile, ".codeium"))));

            // VS Code (and Insiders) — different schema: top-level "servers" key
            // and each entry needs "type": "stdio". User-level mcp.json lives in
            // %APPDATA%\Code\User\mcp.json.
            foreach (var (label, codeDirName) in new[]
                     {
                         ("VS Code", "Code"),
                         ("VS Code Insiders", "Code - Insiders"),
                     })
            {
                var codeRoot = Path.Combine(appData, codeDirName);
                var codeConfig = Path.Combine(codeRoot, "User", "mcp.json");
                results.Add(EnsureConfigFile(label, codeConfig, nodeExe, indexJs,
                    createIfMissing: Directory.Exists(codeRoot),
                    serversKey: "servers", stdioType: true));
            }

            return results;
        }

        /// <summary>
        /// Silent self-heal used on Revit startup. Logs instead of showing UI.
        /// </summary>
        public static void EnsureAllSilent()
        {
            try
            {
                foreach (var r in EnsureAll())
                {
                    if (r.Changed)
                        Logger.Log($"Claude auto-setup [{r.Target}]: {r.Detail}");
                    else if (!r.Configured)
                        Logger.Log($"Claude auto-setup [{r.Target}]: {r.Detail}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Claude auto-setup failed (non-critical)", ex);
            }
        }
    }
}
