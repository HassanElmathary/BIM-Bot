using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BIMBotPlugin.AI;
using Regex = System.Text.RegularExpressions.Regex;

namespace BIMBotPlugin.Antigravity
{
    /// <summary>
    /// Direct TCP bridge to the BIM-Bot service on localhost:8080.
    /// No external watcher needed — resolves keywords to tools and calls Revit directly.
    /// </summary>
    public class AntigravityBridge
    {
        private CancellationTokenSource? _cts;
        private ChatOrchestrator? _orchestrator;

        /// <summary>
        /// Sends a message to Revit by resolving it to a tool call and executing via TCP.
        /// Dual-mode: keyword match → TCP (fast), or file bridge → Antigravity IDE (smart).
        /// </summary>
        public async Task<AntigravityResponse> SendAsync(string message, int historyLength)
        {
            _cts = new CancellationTokenSource();

            return await Task.Run(async () =>
            {
                // ── Mode 1: Try keyword NLP resolution ──
                var resolved = ResolveToolFromMessage(message);

                if (resolved.Method != null)
                {
                    // Matched! Call Revit directly via TCP
                    var result = CallRevitTcp(resolved.Method, resolved.Params);
                    return ParseResponse(result);
                }

                // ── Mode 2: Try fuzzy match — did they misspell something? ──
                var fuzzy = FuzzyMatch(message);
                if (fuzzy != null)
                {
                    var result = CallRevitTcp(fuzzy.Value.Method, fuzzy.Value.Params);
                    var response = ParseResponse(result);
                    response.Text = $"→ Interpreted as: {fuzzy.Value.Method}\n\n{response.Text}";
                    return response;
                }

                // ── Mode 3: AI reasoning (full power via ChatOrchestrator) ──
                var aiResult = await TryAiFallback(message);
                if (aiResult != null)
                    return aiResult;

                // ── Mode 4: File bridge → Antigravity IDE ──
                var bridgeResult = TryFileBridge(message, historyLength);
                if (bridgeResult != null)
                    return bridgeResult;

                // ── Mode 5: Nothing worked — conversational fallback ──
                return new AntigravityResponse
                {
                    Text = $"I couldn't understand \"{message}\".\n\n" +
                           "💡 Tip: Configure an API key in the AI Chat settings to enable full AI understanding.\n" +
                           "Or try being more specific, like:\n" +
                           "• \"show me the levels\" or \"list all rooms\"\n" +
                           "• \"create a new wall\" or \"add a sheet\"\n" +
                           "• \"set parameter\" or \"export to pdf\""
                };
            }, _cts.Token);
        }

        // ── AI Fallback: route to ChatOrchestrator for full AI reasoning ──

        private async Task<AntigravityResponse?> TryAiFallback(string message)
        {
            try
            {
                // Lazy-initialize the orchestrator (reuses GeminiClient settings)
                if (_orchestrator == null)
                    _orchestrator = new ChatOrchestrator();

                // Check if AI is configured (has API key)
                if (!_orchestrator.Gemini.IsConfigured)
                    return null; // Fall through to file bridge

                var result = await _orchestrator.SendMessageAsync(message);

                if (result.IsError)
                    return new AntigravityResponse { Text = result.Text };

                var prefix = result.ToolCallCount > 0
                    ? $"🧠 AI executed {result.ToolCallCount} tool(s):\n\n"
                    : "";

                return new AntigravityResponse { Text = prefix + result.Text };
            }
            catch (Exception ex)
            {
                return new AntigravityResponse { Text = $"❌ AI Error: {ex.Message}" };
            }
        }

        // ── File Bridge: route to Antigravity IDE for smart AI processing ──
        
        private static readonly string BridgeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BIMBot", "antigravity");

        private static AntigravityResponse? TryFileBridge(string message, int historyLength)
        {
            try
            {
                Directory.CreateDirectory(BridgeDir);
                var requestFile = Path.Combine(BridgeDir, "request.json");
                var responseFile = Path.Combine(BridgeDir, "response.json");

                // Clean up old response
                if (File.Exists(responseFile))
                    File.Delete(responseFile);

                // Write request for Antigravity IDE to pick up
                var request = new JObject
                {
                    ["message"] = message,
                    ["timestamp"] = DateTime.Now.ToString("o"),
                    ["historyLength"] = historyLength
                };
                File.WriteAllText(requestFile, request.ToString());

                // Poll for response (Antigravity IDE should pick it up)
                var timeout = DateTime.Now.AddSeconds(15);
                while (DateTime.Now < timeout)
                {
                    Thread.Sleep(500);
                    if (File.Exists(responseFile))
                    {
                        var raw = File.ReadAllText(responseFile);
                        File.Delete(responseFile);

                        try
                        {
                            var resp = JObject.Parse(raw);
                            return new AntigravityResponse
                            {
                                Text = resp["text"]?.ToString() ?? raw
                            };
                        }
                        catch
                        {
                            return new AntigravityResponse { Text = raw };
                        }
                    }
                }

                // Timeout — Antigravity IDE not connected
                if (File.Exists(requestFile))
                    File.Delete(requestFile);
                return null;
            }
            catch
            {
                return null;
            }
        }

        // ── Fuzzy matching: handles typos and close matches ──
        
        private static (string Method, string Params)? FuzzyMatch(string message)
        {
            var lower = message.Trim().ToLowerInvariant();
            var words = lower.Split(new[] { ' ', '\t', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                             .Where(w => !StopWords.Contains(w)).ToArray();

            // Try to fuzzy-match each word against known objects
            foreach (var word in words)
            {
                if (word.Length < 3) continue;

                var bestObj = (string?)null;
                var bestDist = 3; // max edit distance

                foreach (var obj in ObjectActions.Keys)
                {
                    var dist = EditDistance(word, obj.ToLowerInvariant());
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestObj = obj;
                    }
                }

                if (bestObj != null && ObjectActions.TryGetValue(bestObj, out var actions))
                {
                    // Find the action
                    var action = "get";
                    foreach (var w in words)
                    {
                        if (ActionNorm.TryGetValue(w, out var norm)) { action = norm; break; }
                        if (new[] { "get", "create", "delete", "modify", "export", "set", "add" }.Contains(w))
                        { action = w; break; }
                    }

                    if (actions.TryGetValue(action, out var tool))
                        return (tool, "{}");
                    if (actions.TryGetValue("get", out tool))
                        return (tool, "{}");
                }
            }

            return null;
        }

        private static int EditDistance(string a, string b)
        {
            var n = a.Length; var m = b.Length;
            var d = new int[n + 1, m + 1];
            for (var i = 0; i <= n; i++) d[i, 0] = i;
            for (var j = 0; j <= m; j++) d[0, j] = j;
            for (var i = 1; i <= n; i++)
                for (var j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                       d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[n, m];
        }

        /// <summary>Cancels the current operation.</summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        // ── TCP Call ──

        private static string CallRevitTcp(string method, string paramsJson)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = JToken.Parse(paramsJson),
                ["id"] = 1
            };

            var requestStr = request.ToString(Formatting.None);

            try
            {
                using var client = new TcpClient();
                client.SendTimeout = 30000;
                client.ReceiveTimeout = 30000;

                var connectTask = client.ConnectAsync("127.0.0.1", 8080);
                if (!connectTask.Wait(5000))
                    return "{\"error\":{\"message\":\"Connection timed out. Start the MCP service in Revit first.\"}}";

                using var stream = client.GetStream();
                var bytes = Encoding.UTF8.GetBytes(requestStr);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                // Wait for response
                Thread.Sleep(200);

                var buffer = new byte[65536];
                var response = new StringBuilder();
                stream.ReadTimeout = 30000;

                do
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count > 0)
                        response.Append(Encoding.UTF8.GetString(buffer, 0, count));
                    Thread.Sleep(100);
                } while (stream.DataAvailable);

                return response.ToString();
            }
            catch (AggregateException ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return $"{{\"error\":{{\"message\":\"Cannot connect to BIM-Bot on port 8080. Start the MCP service first. ({inner})\"}}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":{{\"message\":\"Error calling Revit: {ex.Message}\"}}}}";
            }
        }

        // ── Response Parser ──

        private static AntigravityResponse ParseResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new AntigravityResponse { Text = "Empty response from Revit." };

            try
            {
                var json = JObject.Parse(raw);

                if (json["error"] != null)
                    return new AntigravityResponse { Text = $"❌ {json["error"]?["message"]?.ToString() ?? "Unknown error"}" };

                var result = json["result"];
                if (result == null)
                    return new AntigravityResponse { Text = raw };

                // Extract human-readable message
                if (result["message"] != null)
                    return new AntigravityResponse { Text = result["message"]!.ToString() };

                if (result.Type == JTokenType.String)
                    return new AntigravityResponse { Text = result.ToString() };

                // Format result nicely
                return new AntigravityResponse { Text = FormatResult(result) };
            }
            catch
            {
                return new AntigravityResponse { Text = raw };
            }
        }

        private static string FormatResult(JToken result)
        {
            var sb = new StringBuilder();

            // Handle arrays (levels, rooms, views, etc.)
            if (result is JObject obj)
            {
                // Show count if present
                var count = obj["count"]?.Value<int>();
                var totalCount = obj["totalCount"]?.Value<int>();

                // Find the array property (levels, rooms, views, elements, etc.)
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is JArray arr && arr.Count > 0)
                    {
                        if (count.HasValue)
                            sb.AppendLine($"Found {count} {prop.Name}" + (totalCount.HasValue && totalCount > count ? $" (of {totalCount} total)" : "") + ":\n");

                        foreach (var item in arr.Take(30))
                        {
                            if (item is JObject itemObj)
                            {
                                var name = itemObj["name"]?.ToString() ?? itemObj["viewName"]?.ToString() ?? "";
                                var id = itemObj["id"]?.ToString() ?? "";
                                var extra = "";

                                // Add contextual info
                                if (itemObj["elevation"] != null) extra = $" — elevation: {itemObj["elevation"]}";
                                if (itemObj["area"] != null) extra = $" — area: {itemObj["area"]}";
                                if (itemObj["viewType"] != null) extra = $" ({itemObj["viewType"]})";
                                if (itemObj["number"] != null && itemObj["category"] == null) extra = $" #{itemObj["number"]}";
                                if (itemObj["category"] != null) extra = $" [{itemObj["category"]}]";
                                if (itemObj["level"] != null) extra += $" on {itemObj["level"]}";

                                sb.AppendLine($"• {name}{extra}" + (id != "" ? $"  (ID: {id})" : ""));
                            }
                            else
                            {
                                sb.AppendLine($"• {item}");
                            }
                        }

                        if (arr.Count > 30)
                            sb.AppendLine($"... and {arr.Count - 30} more");

                        return sb.ToString().TrimEnd();
                    }
                }

                // No array found — show key-value pairs
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value.Type != JTokenType.Object && prop.Value.Type != JTokenType.Array)
                        sb.AppendLine($"**{prop.Name}**: {prop.Value}");
                }

                return sb.Length > 0 ? sb.ToString().TrimEnd() : result.ToString(Formatting.Indented);
            }

            return result.ToString(Formatting.Indented);
        }

        // ── Keyword → Tool Resolution (reads from Authority Bank) ──

        private static readonly Dictionary<string, string> ToolAliases = Core.ToolRegistry.GetKeywordMap();

        // ── Action word normalization ──
        private static readonly Dictionary<string, string> ActionNorm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"list", "get"}, {"show", "get"}, {"display", "get"}, {"tell", "get"}, {"what", "get"}, {"find", "get"}, {"fetch", "get"}, {"retrieve", "get"}, {"query", "get"},
            {"make", "create"}, {"draw", "create"}, {"place", "create"}, {"build", "create"}, {"insert", "create"}, {"new", "create"},
            {"remove", "delete"}, {"erase", "delete"}, {"trash", "delete"},
            {"edit", "modify"}, {"update", "modify"}, {"change", "modify"}, {"adjust", "modify"}, {"fix", "modify"},
            {"shift", "move"}, {"relocate", "move"}, {"reposition", "move"},
            {"duplicate", "copy"}, {"clone", "copy"},
            {"turn", "rotate"}, {"spin", "rotate"}, {"flip", "mirror"},
        };

        // ── Object word → tool mapping (action + object = tool name) ──
        private static readonly Dictionary<string, Dictionary<string, string>> ObjectActions = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            {"view", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_views"}, {"create", "create_view"}, {"open", "open_view"}, {"close", "close_view"}, {"duplicate", "duplicate_view"}}},
            {"views", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_views"}, {"create", "create_view"}, {"open", "open_view"}, {"close", "close_view"}}},
            {"level", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_levels"}, {"create", "create_level"}}},
            {"levels", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_levels"}, {"create", "create_level"}}},
            {"wall", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_wall"}, {"delete", "delete_elements"}, {"move", "move_element"}}},
            {"walls", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_wall"}, {"delete", "delete_elements"}}},
            {"floor", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_floor"}}},
            {"floors", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_floor"}}},
            {"room", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_rooms"}, {"create", "create_room"}}},
            {"rooms", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_rooms"}, {"create", "create_room"}}},
            {"sheet", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_sheets"}, {"create", "create_sheet"}}},
            {"sheets", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_sheets"}, {"create", "create_sheet"}, {"print", "print_sheets"}, {"export", "export_manager"}}},
            {"grid", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_grids"}, {"create", "create_grid"}}},
            {"grids", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_grids"}, {"create", "create_grid"}}},
            {"schedule", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_schedules"}, {"create", "create_schedule"}, {"export", "export_schedule"}}},
            {"schedules", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_schedules"}, {"create", "create_schedule"}, {"export", "export_schedule"}}},
            {"family", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_available_family_types"}, {"manage", "manage_families"}, {"delete", "delete_unused_families"}}},
            {"families", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_available_family_types"}, {"manage", "manage_families"}, {"delete", "delete_unused_families"}}},
            {"warning", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_warnings"}, {"check", "check_warnings"}, {"resolve", "resolve_warnings"}, {"fix", "resolve_warnings"}}},
            {"warnings", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_warnings"}, {"check", "check_warnings"}, {"resolve", "resolve_warnings"}, {"fix", "resolve_warnings"}}},
            {"element", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"modify", "modify_element"}, {"move", "move_element"}, {"delete", "delete_elements"},
                {"copy", "copy_element"}, {"rotate", "rotate_element"}, {"mirror", "mirror_element"}, {"select", "select_elements"}}},
            {"elements", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"delete", "delete_elements"}, {"select", "select_elements"}, {"color", "color_elements"}}},
            {"parameter", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_parameters"}, {"modify", "modify_element"}, {"set", "modify_element"}, {"add", "create_project_parameter"},
                {"create", "create_project_parameter"}, {"validate", "validate_parameters"}, {"export", "export_parameters_to_csv"}}},
            {"parameters", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_parameters"}, {"modify", "batch_modify_parameters"}, {"set", "batch_set_parameter"}, {"add", "create_project_parameter"},
                {"validate", "validate_parameters"}, {"export", "export_parameters_to_csv"}, {"import", "import_parameters_from_csv"}}},
            {"tag", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_tag"}, {"add", "create_tag"}}},
            {"tags", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "tag_all_in_view"}, {"add", "tag_all_in_view"}}},
            {"dimension", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_dimension"}, {"add", "create_dimension"}}},
            {"ceiling", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_ceiling"}}},
            {"roof", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_roof"}}},
            {"door", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_point_based_element"}, {"place", "create_point_based_element"}}},
            {"window", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_point_based_element"}, {"place", "create_point_based_element"}}},
            {"beam", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_elements"}, {"create", "create_line_based_element"}}},
            {"legend", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_legend"}, {"generate", "generate_legend"}}},
            {"workset", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_worksets"}, {"set", "set_workset"}}},
            {"worksets", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_worksets"}}},
            {"material", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_materials"}, {"set", "set_material"}, {"apply", "set_material"}}},
            {"materials", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_materials"}}},
            {"area", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_areas"}}},
            {"areas", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_areas"}}},
            {"selection", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_selected_elements"}, {"filter", "filter_selection"}, {"inverse", "inverse_selection"}, {"invert", "inverse_selection"}}},
            {"project", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_project_info"}}},
            {"model", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "get_model_statistics"}, {"audit", "audit_model"}, {"purge", "purge_unused"}, {"save", "save_snapshot"}}},
            {"viewport", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_viewport"}, {"add", "create_viewport"}, {"align", "align_viewports"}}},
            {"section", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_section_views"}, {"get", "get_views"}}},
            {"elevation", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_elevation_views"}, {"get", "get_views"}}},
            {"callout", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"create", "create_callout_views"}}},
            {"pdf", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"export", "export_to_pdf"}}},
            {"dwg", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"export", "export_to_dwg"}}},
            {"ifc", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"export", "export_to_ifc"}}},
            {"excel", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"export", "export_elements_to_excel"}, {"create", "excel_create_workbook"},
                {"get", "excel_get_info"}, {"import", "import_from_project_file"},
                {"write", "excel_write_cells"}, {"format", "excel_format_cells"},
                {"add", "excel_add_sheet"}, {"read", "excel_read_range"},
                {"analyze", "analyze_project_file"}, {"info", "excel_get_info"}}},
            {"csv", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"export", "export_elements_to_csv"}, {"import", "import_from_project_file"},
                {"read", "read_project_file"}}},
            {"code", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"send", "send_code_to_revit"}, {"run", "send_code_to_revit"}, {"execute", "execute_code"}}},
            {"snapshot", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"save", "save_snapshot"}, {"create", "save_snapshot"}}},
            {"file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "list_project_files"}, {"read", "read_project_file"}, {"analyze", "analyze_project_file"},
                {"search", "search_project_files"}, {"import", "import_from_project_file"}, {"open", "read_project_file"}}},
            {"files", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"get", "list_project_files"}, {"read", "read_project_file"}, {"search", "search_project_files"},
                {"list", "list_project_files"}, {"analyze", "analyze_project_file"}}},
        };

        // ── Stop words to ignore during matching ──
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "in", "on", "of", "to", "for", "is", "it", "my", "this", "that", "with", "from",
            "all", "and", "or", "me", "can", "you", "please", "i", "want", "need", "would", "like", "could",
            "how", "do", "does", "model", "project", "revit", "current", "active", "them", "those", "these"
        };

        private static (string Method, string Params) ResolveToolFromMessage(string message)
        {
            var lower = message.Trim().ToLowerInvariant();
            var words = lower.Split(new[] { ' ', '\t', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

            // 1. Direct tool name (e.g. "get_levels")
            if (words.Length == 1 && Regex.IsMatch(lower, @"^[a-z_]+$"))
                return (lower, "{}");

            // 2. "3d view" detection with context
            if (Regex.IsMatch(lower, @"3d\s*view"))
            {
                var hasListVerb = words.Any(w => w == "list" || w == "show" || w == "get" || w == "display" || w == "find");
                var hasOpenVerb = words.Any(w => w == "open" || w == "activate" || w == "switch" || w == "go");
                if (hasListVerb)
                    return ("get_views", "{\"viewType\":\"ThreeD\"}");
                if (hasOpenVerb)
                    return ("get_views", "{\"viewType\":\"ThreeD\"}");
                return ("create_view", "{\"viewType\":\"3D\"}");
            }

            // 3. Normalize action words and detect intent
            var normalizedAction = (string?)null;
            var rawAction = (string?)null;
            foreach (var w in words)
            {
                if (StopWords.Contains(w)) continue;
                if (ActionNorm.TryGetValue(w, out var norm))
                {
                    normalizedAction = norm;
                    rawAction = w;
                    break;
                }
                // Direct action verbs
                if (new[] { "get", "create", "delete", "modify", "move", "copy", "rotate", "mirror",
                           "align", "group", "select", "color", "purge", "audit", "export", "import",
                           "open", "close", "print", "set", "add", "check", "manage", "batch" }.Contains(w))
                {
                    normalizedAction = w;
                    rawAction = w;
                    break;
                }
            }

            // 4. Find the object word
            foreach (var w in words)
            {
                if (StopWords.Contains(w)) continue;
                if (w == rawAction) continue;

                if (ObjectActions.TryGetValue(w, out var actions))
                {
                    var action = normalizedAction ?? "get"; // default to "get" for queries

                    if (actions.TryGetValue(action, out var tool))
                    {
                        var parms = "{}";
                        // Smart param extraction for category-based queries
                        if (tool == "get_elements")
                            parms = $"{{\"category\":\"{Capitalize(w)}\",\"limit\":20}}";
                        return (tool, parms);
                    }
                    // Try the raw action too
                    if (rawAction != null && rawAction != normalizedAction && actions.TryGetValue(rawAction, out tool))
                        return (tool, "{}");
                    // Default to the "get" action for that object
                    if (actions.TryGetValue("get", out tool))
                        return (tool, "{}");
                }
            }

            // 5. Fall back to keyword alias dictionary
            var bestMatch = ToolAliases
                .Where(kvp => lower.Contains(kvp.Key))
                .OrderByDescending(kvp => kvp.Key.Length)
                .FirstOrDefault();

            if (bestMatch.Key != null)
            {
                var method = bestMatch.Value;
                var parms = "{}";

                if (method == "get_elements")
                {
                    var m = Regex.Match(lower, @"elements?\s+(\w+)");
                    if (m.Success)
                        parms = $"{{\"category\":\"{Capitalize(m.Groups[1].Value)}\",\"limit\":20}}";
                }
                else if (method == "get_views")
                {
                    var m = Regex.Match(lower, @"views?\s+(\w+)");
                    if (m.Success)
                        parms = $"{{\"viewType\":\"{m.Groups[1].Value}\"}}";
                }

                return (method, parms);
            }

            // 6. No match
            return (null!, "{}");
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>
    /// Response from Revit.
    /// </summary>
    public class AntigravityResponse
    {
        public string Text { get; set; } = "";
        public string? ToolName { get; set; }
        public JObject? ToolArgs { get; set; }
    }
}
