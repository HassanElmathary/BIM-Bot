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
        /// Directories to search for the install root, nearest first: the folder
        /// holding BIMBotPlugin.dll, then each of its ancestors.
        ///
        /// Walking is not optional. The installer deploys one build per Revit
        /// year, so the DLL sits at {app}\plugin\R2024\BIMBotPlugin.dll — two
        /// levels below {app}, not one. Code that assumed a single level probed
        /// {app}\plugin\server\build\index.js, never found it, and so left every
        /// real installation unable to configure Claude at all.
        /// </summary>
        private static IEnumerable<string> InstallRootCandidates()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                yield return dir!;
                dir = Path.GetDirectoryName(dir);
            }
        }

        /// <summary>
        /// Locate the MCP server entry point: the installed layout
        /// ({app}\server\build\index.js) or the dev repo layout
        /// ({repo}\revit-mcp-server\build\index.js).
        /// </summary>
        public static string? ResolveServerIndexJs()
        {
            foreach (var root in InstallRootCandidates())
            {
                var installed = Path.Combine(root, "server", "build", "index.js");
                if (File.Exists(installed)) return installed;

                var dev = Path.Combine(root, "revit-mcp-server", "build", "index.js");
                if (File.Exists(dev)) return dev;
            }

            return null;
        }

        /// <summary>
        /// Locate node.exe. Prefers the bundled runtime shipped by the
        /// installer, then a system-wide install, then PATH.
        /// </summary>
        public static string ResolveNodeExe()
        {
            var candidates = new List<string>();
            foreach (var root in InstallRootCandidates())
                candidates.Add(Path.Combine(root, "nodejs", "node.exe"));
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
            bool rebuiltFromBroken = false;
            bool hadBom = false;
            if (File.Exists(configPath))
            {
                try
                {
                    // Strip a UTF-8 BOM before parsing — several editors write one
                    // and it makes the file unreadable to some JSON parsers. A file
                    // that has one gets rewritten below even if its entry is fine.
                    var raw = File.ReadAllText(configPath);
                    if (raw.Length > 0 && raw[0] == '﻿')
                    {
                        raw = raw.Substring(1);
                        hadBom = true;
                    }
                    config = JObject.Parse(raw);
                }
                catch (Exception ex)
                {
                    // A config Claude cannot parse is already broken from the
                    // user's point of view — "left untouched, fix it yourself"
                    // is a dead end for a non-developer. Quarantine it with a
                    // timestamped copy and rebuild a clean one instead.
                    try
                    {
                        var quarantine = configPath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
                        File.Copy(configPath, quarantine, overwrite: true);
                        config = new JObject();
                        rebuiltFromBroken = true;
                        Logger.Log($"Claude config was invalid JSON ({ex.Message}); quarantined to {quarantine}");
                    }
                    catch (Exception copyEx)
                    {
                        result.Detail = $"Config is not valid JSON ({ex.Message}) and could not be backed up ({copyEx.Message}) — left untouched: {configPath}";
                        return result;
                    }
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
            if (IsEntryValid(existing) && !hadBom)
            {
                result.Configured = true;
                result.Detail = "Already configured correctly.";
                return result;
            }

            // Missing, stale, BOM-prefixed, or rebuilt from a broken file →
            // write the correct entry (backup first).
            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", overwrite: true);

                var dir = Path.GetDirectoryName(configPath);
                if (dir != null) Directory.CreateDirectory(dir);

                mcpServers[ServerKey] = BuildEntry(nodeExe, indexJs, stdioType);
                // UTF-8 without BOM — Claude Desktop chokes on a BOM.
                File.WriteAllText(configPath, config.ToString(Formatting.Indented),
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                result.Configured = true;
                result.Changed = true;
                if (rebuiltFromBroken)
                    result.Detail = "Config was corrupt (invalid JSON) — backed it up and rebuilt it.";
                else if (existing == null)
                    result.Detail = "Added BIM-Bot entry.";
                else if (hadBom)
                    result.Detail = "Rewrote config without the UTF-8 BOM that was breaking it.";
                else
                    result.Detail = "Repaired stale BIM-Bot entry (old path no longer existed).";
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

            // Claude Desktop — always create the config, even when we cannot see
            // the app yet. %APPDATA%\Claude only appears after Claude Desktop has
            // been run at least once, so gating on it meant anyone who installed
            // BIM-Bot first (the normal order on a fresh laptop) got silently
            // skipped and had to hand-write the config. Writing the file early is
            // harmless: Claude Desktop reads it on first launch.
            var desktopConfig = Path.Combine(appData, "Claude", "claude_desktop_config.json");
            results.Add(EnsureConfigFile("Claude Desktop", desktopConfig, nodeExe, indexJs,
                createIfMissing: true));

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
        /// Human-readable health report for the "Connect Claude" dialog. Answers
        /// the three questions a stuck user actually has: is the server there, is
        /// the Revit-side service listening, and where did the config go.
        /// </summary>
        public static string Diagnose()
        {
            var sb = new System.Text.StringBuilder();

            var indexJs = ResolveServerIndexJs();
            var nodeExe = ResolveNodeExe();

            sb.AppendLine("Diagnostics");
            sb.AppendLine($"  Plugin:      {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"  MCP server:  {indexJs ?? "NOT FOUND — reinstall BIM-Bot (the server component was not deployed)"}");
            sb.AppendLine($"  Node.js:     {(File.Exists(nodeExe) ? nodeExe : nodeExe + "  (not found on disk — reinstall BIM-Bot)")}");

            var running = Application.IsServiceRunning;
            var port = Application.SocketServiceInstance?.Port;
            sb.AppendLine($"  BIM-Bot service: {(running ? $"running on 127.0.0.1:{port}" : "STOPPED — click \"Start BIM-Bot\" on this ribbon")}");
            sb.AppendLine($"  Handshake:   {(File.Exists(ServiceEndpoint.FilePath) ? ServiceEndpoint.FilePath : "not written yet")}");
            sb.AppendLine($"  Log file:    {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMBot", "logs")}");

            return sb.ToString().TrimEnd();
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
