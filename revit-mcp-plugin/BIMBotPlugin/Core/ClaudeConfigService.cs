using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Ensures Claude Desktop, Claude Code, ChatGPT Desktop, OpenCode and other MCP
    /// clients are configured to launch the
    /// BIM-Bot MCP server, and — critically — repairs stale entries whose
    /// paths no longer exist (e.g. after the repo or install folder moved).
    ///
    /// Called silently on Revit startup (self-heal) and interactively from
    /// the "Connect Claude" ribbon button.
    /// </summary>
    public static class ClaudeConfigService
    {
        private const string ServerKey = "BIM-Bot";
        private const string NavisServerKey = "BIM-Bot-Navis";
        // Navisworks MCP endpoint: the Navis plugin listens on 8091 (+scan) and
        // publishes %LOCALAPPDATA%\BIMBot\service-navis.json. The env pin wins over
        // the handshake file (see navisEndpoint.ts); the plain BIM-Bot entry keeps
        // working for Revit on 8080 either way.

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

        private static JObject BuildEntry(string nodeExe, string indexJs, bool stdioType,
            IDictionary<string, string>? env = null)
        {
            var envObj = new JObject();
            if (env != null)
                foreach (var kv in env) envObj[kv.Key] = kv.Value;
            var entry = new JObject
            {
                ["command"] = nodeExe,
                ["args"] = new JArray { indexJs },
                ["env"] = envObj
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

            // Both entries: BIM-Bot (Revit, port 8080) and BIM-Bot-Navis
            // (Navisworks, BIMBOT_NAVIS_PORT=8091). Same server binary — the
            // navis_* tools route via withNavisConnection.
            var navisEnv = new Dictionary<string, string> { ["BIMBOT_NAVIS_PORT"] = "8091" };
            var existing = mcpServers[ServerKey] as JObject;
            var existingNavis = mcpServers[NavisServerKey] as JObject;
            if (IsEntryValid(existing) && IsEntryValid(existingNavis) && !hadBom)
            {
                result.Configured = true;
                result.Detail = "Already configured correctly.";
                return result;
            }

            // Missing, stale, BOM-prefixed, or rebuilt from a broken file →
            // write the correct entries (backup first).
            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", overwrite: true);

                var dir = Path.GetDirectoryName(configPath);
                if (dir != null) Directory.CreateDirectory(dir);

                mcpServers[ServerKey] = BuildEntry(nodeExe, indexJs, stdioType);
                mcpServers[NavisServerKey] = BuildEntry(nodeExe, indexJs, stdioType, navisEnv);
                // UTF-8 without BOM — Claude Desktop chokes on a BOM.
                File.WriteAllText(configPath, config.ToString(Formatting.Indented),
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                result.Configured = true;
                result.Changed = true;
                if (rebuiltFromBroken)
                    result.Detail = "Config was corrupt (invalid JSON) — backed it up and rebuilt it.";
                else if (existing == null && existingNavis == null)
                    result.Detail = "Added BIM-Bot + BIM-Bot-Navis entries.";
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
        // ChatGPT Desktop (TOML)
        // ────────────────────────────────────────────────────────
        //
        // ChatGPT Desktop runs local stdio MCP servers from the Codex config it
        // shares with the Codex CLI: %USERPROFILE%\.codex\config.toml (or
        // $CODEX_HOME\config.toml), table [mcp_servers.<name>]. The file holds
        // many settings the app owns, so it is edited line-wise: only the
        // BIM-Bot table (and its sub-tables) is replaced. Mirrors
        // revit-mcp-server/scripts/configure-claude.cjs — keep them in step.

        /// <summary>Key parts of a TOML table header line, or null if not a header.</summary>
        private static List<string>? TomlHeaderParts(string line)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*\[\[?(.+?)\]\]?\s*(#.*)?$");
            if (!m.Success) return null;
            var src = m.Groups[1].Value;
            var parts = new List<string>();
            int i = 0;
            while (i < src.Length)
            {
                while (i < src.Length && (src[i] == ' ' || src[i] == '\t')) i++;
                var part = new System.Text.StringBuilder();
                if (i < src.Length && src[i] == '"')
                {
                    i++;
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) { part.Append(src[i + 1]); i += 2; }
                        else part.Append(src[i++]);
                    }
                    i++;
                }
                else if (i < src.Length && src[i] == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'') part.Append(src[i++]);
                    i++;
                }
                else
                {
                    while (i < src.Length && (char.IsLetterOrDigit(src[i]) && src[i] < 128 || src[i] == '_' || src[i] == '-'))
                        part.Append(src[i++]);
                    if (part.Length == 0) return null; // not a key — e.g. an array value line
                }
                parts.Add(part.ToString());
                while (i < src.Length && (src[i] == ' ' || src[i] == '\t')) i++;
                if (i < src.Length)
                {
                    if (src[i] != '.') return null;
                    i++;
                }
            }
            return parts;
        }

        /// <summary>
        /// Split a TOML document into the BIM-Bot block and everything else.
        /// Inline forms (BIM-Bot = {...} under [mcp_servers], or root-level
        /// mcp_servers.BIM-Bot dotted keys) count as ours too, so the table we
        /// append can never collide with a duplicate key.
        /// </summary>
        private static void SplitTomlBlock(string text, List<string> keep, List<string> own,
            string serverKey = ServerKey)
        {
            var inlineKey = new System.Text.RegularExpressions.Regex($"^\\s*([\"']?){serverKey}\\1\\s*=");
            var rootDotted = new System.Text.RegularExpressions.Regex($"^\\s*mcp_servers\\s*\\.\\s*([\"']?){serverKey}\\1\\s*[.=]");
            var table = new List<string>();
            bool inOwn = false;
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                var parts = TomlHeaderParts(line);
                if (parts != null)
                {
                    table = parts;
                    inOwn = parts.Count >= 2 && parts[0] == "mcp_servers" && parts[1] == serverKey;
                }
                if (inOwn) { own.Add(line); continue; }
                if (parts == null && table.Count == 1 && table[0] == "mcp_servers" && inlineKey.IsMatch(line)) { own.Add(line); continue; }
                if (parts == null && table.Count == 0 && rootDotted.IsMatch(line)) { own.Add(line); continue; }
                keep.Add(line);
            }
        }

        private static string TomlString(string value)
        {
            // Literal strings need no escaping — ideal for Windows paths.
            if (value.IndexOfAny(new[] { '\'', '\r', '\n' }) < 0) return "'" + value + "'";
            return JsonConvert.ToString(value); // valid TOML basic string for plain text
        }

        private static string? ReadTomlString(string? src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            if (src![0] == '\'') return src.Substring(1, src.Length - 2);
            try { return JsonConvert.DeserializeObject<string>(src); } catch { return null; }
        }

        private static bool IsTomlBlockValid(List<string> own)
        {
            var text = string.Join("\n", own);
            const string str = "(\"(?:[^\"\\\\]|\\\\.)*\"|'[^']*')";
            var cmd = System.Text.RegularExpressions.Regex.Match(text, @"^\s*command\s*=\s*" + str,
                System.Text.RegularExpressions.RegexOptions.Multiline);
            var arg = System.Text.RegularExpressions.Regex.Match(text, @"^\s*args\s*=\s*\[\s*" + str,
                System.Text.RegularExpressions.RegexOptions.Multiline);
            var entry = new JObject
            {
                ["command"] = ReadTomlString(cmd.Success ? cmd.Groups[1].Value : null),
                ["args"] = new JArray { ReadTomlString(arg.Success ? arg.Groups[1].Value : null) }
            };
            return IsEntryValid(entry);
        }

        private static string CodexHome()
        {
            var env = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(env)) return env!;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        /// <summary>
        /// ChatGPT Desktop ships as an MSIX package (OpenAI.Codex_* /
        /// OpenAI.ChatGPT*); the Codex CLI leaves ~/.codex. Either means
        /// someone will read config.toml.
        /// </summary>
        private static bool ChatGptSeen(string codexDir)
        {
            if (Directory.Exists(codexDir)) return true;
            try
            {
                var packages = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                foreach (var dir in Directory.GetDirectories(packages, "OpenAI.*"))
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("OpenAI.ChatGPT", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { /* no Packages folder */ }
            return false;
        }

        private static ConfigureResult EnsureTomlConfigFile(
            string target, string configPath, string nodeExe, string indexJs, bool createIfMissing)
        {
            var result = new ConfigureResult { Target = target };

            string raw = "";
            bool hadBom = false;
            if (File.Exists(configPath))
            {
                try { raw = File.ReadAllText(configPath); }
                catch (Exception ex)
                {
                    result.Detail = $"Failed to read config: {ex.Message}";
                    return result;
                }
                if (raw.Length > 0 && raw[0] == '﻿')
                {
                    raw = raw.Substring(1);
                    hadBom = true;
                }
            }
            else if (!createIfMissing)
            {
                result.Detail = "Not installed (ChatGPT Desktop not found) — skipped.";
                return result;
            }

            var eol = raw.Contains("\r\n") ? "\r\n" : "\n";
            // Two passes — BIM-Bot (Revit) and BIM-Bot-Navis (Navisworks). Each
            // split is key-specific so the other table lands in `keep` and survives.
            var specs = new[]
            {
                (Key: ServerKey, Env: (string?)null),
                (Key: NavisServerKey, Env: "env = { BIMBOT_NAVIS_PORT = \"8091\" }"),
            };
            // A BOM alone forces a rewrite (some parsers choke on it).
            bool changed = hadBom;
            bool addedAny = false;
            string text = raw;
            foreach (var spec in specs)
            {
                var keep = new List<string>();
                var own = new List<string>();
                SplitTomlBlock(text, keep, own, spec.Key);
                if (own.Count > 0 && IsTomlBlockValid(own) && !hadBom) continue;
                addedAny = addedAny || own.Count == 0;
                changed = true;
                while (keep.Count > 0 && keep[keep.Count - 1].Trim().Length == 0) keep.RemoveAt(keep.Count - 1);
                var block = new List<string>
                {
                    $"[mcp_servers.{spec.Key}]",
                    $"command = {TomlString(nodeExe)}",
                    $"args = [{TomlString(indexJs)}]",
                };
                if (spec.Env != null) block.Add(spec.Env);
                block.AddRange(new[]
                {
                    // First launch of node can exceed the 10 s default while
                    // antivirus scans it; long Revit operations exceed the 60 s one.
                    "startup_timeout_sec = 30",
                    "tool_timeout_sec = 300",
                });
                text = (keep.Count > 0 ? string.Join(eol, keep) + eol + eol : "") +
                       string.Join(eol, block) + eol;
            }
            if (!changed)
            {
                result.Configured = true;
                result.Detail = "Already configured correctly.";
                return result;
            }

            var output = text;

            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", overwrite: true);
                var dir = Path.GetDirectoryName(configPath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(configPath, output,
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                result.Configured = true;
                result.Changed = true;
                result.Detail = hadBom
                    ? "Rewrote config without the UTF-8 BOM that was breaking it."
                    : addedAny
                        ? "Added BIM-Bot + BIM-Bot-Navis entries."
                        : "Repaired stale BIM-Bot entry (old path no longer existed).";
            }
            catch (Exception ex)
            {
                result.Detail = $"Failed to write config: {ex.Message}";
            }
            return result;
        }

        // ────────────────────────────────────────────────────────
        // OpenCode (JSON, "mcp" schema)
        // ────────────────────────────────────────────────────────
        //
        // ~/.config/opencode/opencode.json (same path on Windows; OPENCODE_CONFIG
        // overrides). Entry shape: { type: "local", command: [exe, script],
        // environment, enabled, timeout }. The tool scoping + the BIM-Bot
        // agent come from server\opencode\bimbot-opencode.json and are seeded
        // only when BIM-Bot is first added. Mirrors configure-claude.cjs.

        private static string OpenCodeConfigPath()
        {
            var env = Environment.GetEnvironmentVariable("OPENCODE_CONFIG");
            if (!string.IsNullOrWhiteSpace(env)) return env!;
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode");
            var json = Path.Combine(dir, "opencode.json");
            var jsonc = Path.Combine(dir, "opencode.jsonc");
            return !File.Exists(json) && File.Exists(jsonc) ? jsonc : json;
        }

        private static bool OpenCodeSeen()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Directory.Exists(Path.Combine(home, ".config", "opencode")) ||
                   Directory.Exists(Path.Combine(home, ".local", "share", "opencode")) ||
                   Directory.Exists(Path.Combine(home, ".opencode")) ||
                   File.Exists(Path.Combine(appData, "npm", "opencode.cmd"));
        }

        private static bool IsOpenCodeEntryValid(JObject? entry)
        {
            if (entry == null || entry["type"]?.ToString() != "local") return false;
            if (entry["command"] is not JArray cmd || cmd.Count < 2) return false;
            return IsEntryValid(new JObject
            {
                ["command"] = cmd[0]?.ToString(),
                ["args"] = new JArray { cmd[1]?.ToString() }
            });
        }

        private static ConfigureResult EnsureOpenCodeConfigFile(
            string configPath, string nodeExe, string indexJs, bool createIfMissing)
        {
            var result = new ConfigureResult { Target = "OpenCode" };

            JObject config;
            bool hadBom = false;
            if (File.Exists(configPath))
            {
                try
                {
                    var raw = File.ReadAllText(configPath);
                    if (raw.Length > 0 && raw[0] == '﻿') { raw = raw.Substring(1); hadBom = true; }
                    config = JObject.Parse(raw, new JsonLoadSettings { CommentHandling = CommentHandling.Load });
                    // OpenCode accepts JSONC; rewriting would drop the user's comments.
                    if (config.Descendants().Any(t => t.Type == JTokenType.Comment))
                        throw new JsonReaderException("file contains comments");
                }
                catch (Exception ex)
                {
                    result.Detail = $"Config has comments or is not plain JSON ({ex.Message}) — left untouched. Add the entries from server\\opencode\\opencode.sample.json by hand: {configPath}";
                    return result;
                }
            }
            else if (createIfMissing)
            {
                config = new JObject();
            }
            else
            {
                result.Detail = "Not installed (OpenCode not found) — skipped.";
                return result;
            }

            bool changed = hadBom;
            bool added = false;
            if (config["$schema"] == null) { config["$schema"] = "https://opencode.ai/config.json"; changed = true; }
            if (config["mcp"] is not JObject mcp) { mcp = new JObject(); config["mcp"] = mcp; }
            bool firstTime = mcp[ServerKey] == null;

            var specs = new[]
            {
                // Navisworks off by default: the same ~280 tools again, useful only
                // with Navisworks open. The user flips "enabled" when needed.
                (Key: ServerKey, Env: new JObject(), Enabled: true),
                (Key: NavisServerKey, Env: new JObject { ["BIMBOT_NAVIS_PORT"] = "8091" }, Enabled: false),
            };
            foreach (var spec in specs)
            {
                var existing = mcp[spec.Key] as JObject;
                if (IsOpenCodeEntryValid(existing)) continue;
                if (existing == null) added = true;
                changed = true;
                var keepEnabled = existing?["enabled"]?.Type == JTokenType.Boolean
                    ? existing["enabled"]!.Value<bool>() : spec.Enabled;
                mcp[spec.Key] = new JObject
                {
                    ["type"] = "local",
                    ["command"] = new JArray { nodeExe, indexJs },
                    ["environment"] = spec.Env,
                    ["enabled"] = keepEnabled,
                    // ~280 tool schemas plus a cold node start can exceed the 5 s default.
                    ["timeout"] = 30000
                };
            }

            // Seed scoping only on first registration — this runs on every Revit
            // start, and an agent the user deleted on purpose must stay deleted.
            if (firstTime)
            {
                try
                {
                    var template = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(indexJs)!)!,
                        "opencode", "bimbot-opencode.json");
                    if (File.Exists(template))
                    {
                        var defaults = JObject.Parse(File.ReadAllText(template));
                        foreach (var section in new[] { "tools", "agent" })
                        {
                            if (defaults[section] is not JObject src) continue;
                            if (config[section] is not JObject dst) { dst = new JObject(); config[section] = dst; }
                            foreach (var prop in src.Properties())
                                if (dst[prop.Name] == null) { dst[prop.Name] = prop.Value.DeepClone(); changed = true; }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"OpenCode defaults template unreadable (servers still added): {ex.Message}");
                }
            }

            if (!changed)
            {
                result.Configured = true;
                result.Detail = "Already configured correctly.";
                return result;
            }

            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configPath + ".bimbot-backup", overwrite: true);
                var dir = Path.GetDirectoryName(configPath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(configPath, config.ToString(Formatting.Indented),
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                result.Configured = true;
                result.Changed = true;
                result.Detail = hadBom
                    ? "Rewrote config without the UTF-8 BOM that was breaking it."
                    : added
                        ? "Added BIM-Bot + BIM-Bot-Navis entries and the BIM-Bot agent."
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

            // ChatGPT Desktop (and Codex CLI, which shares the file) — TOML.
            // Created once ChatGPT/Codex shows up for this user.
            var codexDir = CodexHome();
            results.Add(EnsureTomlConfigFile("ChatGPT Desktop", Path.Combine(codexDir, "config.toml"),
                nodeExe, indexJs, createIfMissing: ChatGptSeen(codexDir)));

            // OpenCode — created once OpenCode shows up for this user.
            results.Add(EnsureOpenCodeConfigFile(OpenCodeConfigPath(), nodeExe, indexJs,
                createIfMissing: OpenCodeSeen()));

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
            sb.AppendLine($"  Probe:       node \"<install>\\server\\scripts\\probe-revit.cjs\" (full Link-2 round-trip)");

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
