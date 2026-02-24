using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.AI
{
    /// <summary>
    /// Settings for the AI integration — persisted to disk.
    /// </summary>
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gemini-2.5-flash";
        public string Provider { get; set; } = "gemini"; // "gemini", "deepseek", or "perplexity"

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "RevitMCP", "gemini-settings.json");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static GeminiSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<GeminiSettings>(File.ReadAllText(SettingsPath))
                           ?? new GeminiSettings();
            }
            catch { }
            return new GeminiSettings();
        }

        public bool IsDeepSeek => Provider?.Equals("deepseek", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPerplexity => Provider?.Equals("perplexity", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// HTTP client for AI APIs (Gemini + DeepSeek) with function calling support.
    /// Routes to the correct API format based on provider setting.
    /// </summary>
    public class GeminiClient
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly List<JObject> _history = new List<JObject>();
        private GeminiSettings _settings;
        private int _callIdCounter = 0;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);
        public string CurrentModel => _settings.Model;
        public string CurrentProvider => _settings.IsPerplexity ? "Perplexity" : _settings.IsDeepSeek ? "DeepSeek" : "Gemini";

        public GeminiClient()
        {
            _settings = GeminiSettings.Load();
        }

        public void UpdateSettings(GeminiSettings settings)
        {
            _settings = settings;
            _settings.Save();
        }

        public GeminiSettings GetSettings() => _settings;

        public void ClearHistory() => _history.Clear();

        /// <summary>
        /// Send a user message and get a response. Returns either:
        /// - A FunctionCall (Gemini wants to call a Revit tool)
        /// - A text response (Gemini has a final answer)
        /// </summary>
        public async Task<GeminiResponse> SendMessageAsync(string userMessage)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Gemini API key not configured. Go to Settings to add your key.");

            // Add user message to history
            _history.Add(new JObject
            {
                ["role"] = "user",
                ["parts"] = new JArray { new JObject { ["text"] = userMessage } }
            });

            return await CallApiAsync();
        }

        /// <summary>
        /// Send function results back to Gemini to continue the conversation.
        /// </summary>
        public async Task<GeminiResponse> SendFunctionResultAsync(string functionName, JToken result)
        {
            // Find the callId from the last function call in history
            string callId = null;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                if (entry["role"]?.ToString() == "model")
                {
                    var fc = (entry["parts"] as JArray)?[0]?["functionCall"];
                    if (fc != null)
                    {
                        callId = fc["_callId"]?.ToString();
                        break;
                    }
                }
            }

            // Add function response to history
            _history.Add(new JObject
            {
                ["role"] = "function",
                ["parts"] = new JArray
                {
                    new JObject
                    {
                        ["functionResponse"] = new JObject
                        {
                            ["name"] = functionName,
                            ["_callId"] = callId ?? $"call_{_callIdCounter}",
                            ["response"] = new JObject
                            {
                                ["result"] = result
                            }
                        }
                    }
                }
            });

            return await CallApiAsync();
        }

        private async Task<GeminiResponse> CallApiAsync()
        {
            TrimHistory();
            if (_settings.IsDeepSeek || _settings.IsPerplexity)
                return await CallOpenAICompatibleAsync();
            return await CallGeminiAsync();
        }

        // ============ GEMINI API ============
        private async Task<GeminiResponse> CallGeminiAsync()
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

            var requestBody = new JObject
            {
                ["contents"] = new JArray(_history.ToArray()),
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray
                    {
                        new JObject { ["text"] = GetSystemInstruction() }
                    }
                },
                ["tools"] = new JArray
                {
                    new JObject
                    {
                        ["functionDeclarations"] = GetFunctionDeclarations()
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.7,
                    ["maxOutputTokens"] = 8192
                }
            };

            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errJson = JObject.Parse(responseStr);
                    var errMsg = errJson["error"]?["message"]?.ToString() ?? responseStr;
                    throw new Exception($"Gemini API error ({(int)response.StatusCode}): {errMsg}");
                }
                catch (JsonException)
                {
                    throw new Exception($"Gemini API error ({(int)response.StatusCode}): {responseStr}");
                }
            }

            var result = JObject.Parse(responseStr);
            var candidate = result["candidates"]?[0];
            var contentParts = candidate?["content"]?["parts"];

            if (contentParts == null)
            {
                var finishReason = candidate?["finishReason"]?.ToString() ?? "UNKNOWN";
                return new GeminiResponse { Text = $"[No response — finish reason: {finishReason}]" };
            }

            // Add assistant response to history
            _history.Add(new JObject
            {
                ["role"] = "model",
                ["parts"] = contentParts.DeepClone()
            });

            // Check for function calls
            foreach (var part in contentParts)
            {
                var fc = part["functionCall"];
                if (fc != null)
                {
                    return new GeminiResponse
                    {
                        FunctionCall = new GeminiFunctionCall
                        {
                            Name = fc["name"]!.ToString(),
                            Arguments = fc["args"] as JObject ?? new JObject()
                        }
                    };
                }
            }

            // Extract text
            var textParts = contentParts
                .Where(p => p["text"] != null)
                .Select(p => p["text"]!.ToString());
            return new GeminiResponse { Text = string.Join("\n", textParts) };
        }

        // ============ OpenAI-compatible API (DeepSeek / Perplexity) ============
        private async Task<GeminiResponse> CallOpenAICompatibleAsync()
        {
            string url;
            string providerLabel;
            if (_settings.IsPerplexity)
            {
                url = "https://api.perplexity.ai/chat/completions";
                providerLabel = "Perplexity";
            }
            else
            {
                url = "https://api.deepseek.com/chat/completions";
                providerLabel = "DeepSeek";
            }

            // Convert Gemini history to OpenAI messages format
            var messages = new JArray();
            messages.Add(new JObject { ["role"] = "system", ["content"] = GetSystemInstruction() });

            foreach (var entry in _history)
            {
                var role = entry["role"]?.ToString();
                var parts = entry["parts"] as JArray;
                if (parts == null) continue;

                if (role == "user")
                {
                    var userText = parts[0]?["text"]?.ToString() ?? "";
                    messages.Add(new JObject { ["role"] = "user", ["content"] = userText });
                }
                else if (role == "model")
                {
                    var fc = parts[0]?["functionCall"];
                    if (fc != null)
                    {
                        var callId = fc["_callId"]?.ToString() ?? $"call_{fc["name"]}";
                        messages.Add(new JObject
                        {
                            ["role"] = "assistant",
                            ["content"] = (string)null,
                            ["tool_calls"] = new JArray
                            {
                                new JObject
                                {
                                    ["id"] = callId,
                                    ["type"] = "function",
                                    ["function"] = new JObject
                                    {
                                        ["name"] = fc["name"],
                                        ["arguments"] = (fc["args"] ?? new JObject()).ToString()
                                    }
                                }
                            }
                        });
                    }
                    else
                    {
                        var modelText = string.Join("\n", parts.Where(p => p["text"] != null).Select(p => p["text"]!.ToString()));
                        messages.Add(new JObject { ["role"] = "assistant", ["content"] = modelText });
                    }
                }
                else if (role == "function")
                {
                    var fr = parts[0]?["functionResponse"];
                    if (fr != null)
                    {
                        var callId = fr["_callId"]?.ToString() ?? $"call_{fr["name"]}";
                        messages.Add(new JObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = callId,
                            ["content"] = (fr["response"]?["result"] ?? new JObject()).ToString()
                        });
                    }
                }
            }

            // Convert function declarations to OpenAI tools format
            var tools = new JArray();
            foreach (var decl in GetFunctionDeclarations())
            {
                var tool = new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = decl["name"],
                        ["description"] = decl["description"],
                    }
                };
                if (decl["parameters"] != null)
                    tool["function"]!["parameters"] = decl["parameters"];
                else
                    tool["function"]!["parameters"] = new JObject { ["type"] = "object", ["properties"] = new JObject() };
                tools.Add(tool);
            }

            var requestBody = new JObject
            {
                ["model"] = _settings.Model,
                ["messages"] = messages,
                ["tools"] = tools,
                ["temperature"] = 0.7,
                ["max_tokens"] = 8192
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");

            var response = await _http.SendAsync(request);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errJson = JObject.Parse(responseStr);
                    var errMsg = errJson["error"]?["message"]?.ToString() ?? responseStr;
                    throw new Exception($"{providerLabel} API error ({(int)response.StatusCode}): {errMsg}");
                }
                catch (JsonException)
                {
                    throw new Exception($"{providerLabel} API error ({(int)response.StatusCode}): {responseStr}");
                }
            }

            var result = JObject.Parse(responseStr);
            var choice = result["choices"]?[0];
            var message = choice?["message"];

            if (message == null)
                return new GeminiResponse { Text = $"[No response from {providerLabel}]" };

            // Check for tool calls
            var toolCalls = message["tool_calls"] as JArray;
            if (toolCalls != null && toolCalls.Count > 0)
            {
                var tc = toolCalls[0];
                var funcName = tc?["function"]?["name"]?.ToString() ?? "";
                var argsStr = tc?["function"]?["arguments"]?.ToString() ?? "{}";
                var tcId = tc?["id"]?.ToString() ?? $"call_{++_callIdCounter}";
                JObject args;
                try { args = JObject.Parse(argsStr); } catch { args = new JObject(); }

                // Add to history in Gemini format with call ID for DeepSeek matching
                _history.Add(new JObject
                {
                    ["role"] = "model",
                    ["parts"] = new JArray
                    {
                        new JObject
                        {
                            ["functionCall"] = new JObject
                            {
                                ["name"] = funcName,
                                ["args"] = args,
                                ["_callId"] = tcId
                            }
                        }
                    }
                });

                return new GeminiResponse
                {
                    FunctionCall = new GeminiFunctionCall
                    {
                        Name = funcName,
                        Arguments = args
                    }
                };
            }

            // Text response
            var text = message["content"]?.ToString() ?? "[Empty response]";
            _history.Add(new JObject
            {
                ["role"] = "model",
                ["parts"] = new JArray { new JObject { ["text"] = text } }
            });

            return new GeminiResponse { Text = text };
        }

        // Keep history bounded
        private void TrimHistory()
        {
            if (_history.Count > 40) // ~20 turns
            {
                _history.RemoveRange(0, _history.Count - 40);
            }
        }

        private string GetSystemInstruction()
        {
            return @"You are an expert Revit BIM assistant embedded directly inside Autodesk Revit.
You have access to tools that directly control Revit. When the user asks you to do something in the model, USE THE TOOLS — do not just describe what to do.

Guidelines:
- Coordinates are in FEET. 1 meter ≈ 3.281 feet.
- Always confirm level names before creating geometry.
- For reading data, call the appropriate get_ tool first.
- For creating/modifying, call the tool directly.
- You can chain multiple tool calls to accomplish complex tasks.
- Report results clearly after each operation.
- If a tool returns an error, explain what went wrong and suggest a fix.

Be concise and helpful. You are talking to engineers and architects.";
        }

        /// <summary>
        /// Generate Gemini function declarations for all Revit MCP tools.
        /// Each declaration maps to a command in CommandExecutor.
        /// </summary>
        private JArray GetFunctionDeclarations()
        {
            var decls = new JArray();

            // ===== READING =====
            decls.Add(Fn("get_current_view_info", "Get information about the currently active view (name, type, scale, level)"));
            decls.Add(Fn("get_current_view_elements", "Get elements visible in the current view",
                Prop("category", "string", "Optional category filter (e.g. Walls, Doors, Windows)")));
            decls.Add(Fn("get_selected_elements", "Get the currently selected elements in Revit"));
            decls.Add(Fn("get_elements", "Query elements by category, type, or level",
                Prop("category", "string", "Category name (Walls, Floors, Doors, etc.)"),
                Prop("typeName", "string", "Optional type name filter"),
                Prop("levelName", "string", "Optional level name filter")));
            decls.Add(Fn("get_parameters", "Get all parameters of a specific element",
                PropReq("elementId", "integer", "The element ID")));
            decls.Add(Fn("get_project_info", "Get project information (name, number, client, address, status)"));
            decls.Add(Fn("get_views", "Get all views in the project",
                Prop("type", "string", "Optional view type filter (FloorPlan, Section, Elevation, 3D, etc.)")));
            decls.Add(Fn("get_sheets", "Get all sheets in the project"));
            decls.Add(Fn("get_levels", "Get all levels with their elevations"));
            decls.Add(Fn("get_grids", "Get all grid lines"));
            decls.Add(Fn("get_rooms", "Get all rooms with area, level, number"));
            decls.Add(Fn("get_available_family_types", "Get available family types",
                Prop("category", "string", "Category to filter (Walls, Doors, Windows, etc.)")));
            decls.Add(Fn("get_schedules", "Get all schedules in the project"));
            decls.Add(Fn("get_linked_models", "Get all linked Revit models"));
            decls.Add(Fn("get_warnings", "Get all warnings/errors in the model"));

            // ===== CREATING =====
            decls.Add(Fn("create_wall", "Create a wall between two points",
                PropReq("startX", "number", "Start X coordinate (feet)"),
                PropReq("startY", "number", "Start Y coordinate (feet)"),
                PropReq("endX", "number", "End X coordinate (feet)"),
                PropReq("endY", "number", "End Y coordinate (feet)"),
                PropReq("levelName", "string", "Level name to place wall on"),
                Prop("height", "number", "Wall height in feet (default: 10)")));
            decls.Add(Fn("create_level", "Create a new level",
                PropReq("name", "string", "Level name"),
                PropReq("elevation", "number", "Elevation in feet")));
            decls.Add(Fn("create_grid", "Create a grid line",
                PropReq("startX", "number", "Start X (feet)"),
                PropReq("startY", "number", "Start Y (feet)"),
                PropReq("endX", "number", "End X (feet)"),
                PropReq("endY", "number", "End Y (feet)"),
                Prop("name", "string", "Grid name")));
            decls.Add(Fn("create_room", "Create a room at a point on a level",
                PropReq("x", "number", "X coordinate (feet)"),
                PropReq("y", "number", "Y coordinate (feet)"),
                PropReq("levelName", "string", "Level name"),
                Prop("roomName", "string", "Room name"),
                Prop("roomNumber", "string", "Room number")));
            decls.Add(Fn("create_sheet", "Create a new sheet",
                Prop("sheetNumber", "string", "Sheet number"),
                Prop("sheetName", "string", "Sheet name"),
                Prop("titleBlockName", "string", "Title block family name")));

            // ===== EDITING =====
            decls.Add(Fn("modify_element", "Modify parameters of an element",
                PropReq("elementId", "integer", "Element ID to modify"),
                PropArrayReq("modifications", "Array of parameter modifications", new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["parameterName"] = new JObject { ["type"] = "string", ["description"] = "Parameter name" },
                        ["value"] = new JObject { ["type"] = "string", ["description"] = "New value" }
                    },
                    ["required"] = new JArray("parameterName", "value")
                })));
            decls.Add(Fn("move_element", "Move an element by a delta vector",
                PropReq("elementId", "integer", "Element ID to move"),
                PropReq("deltaX", "number", "Move distance in X (feet)"),
                PropReq("deltaY", "number", "Move distance in Y (feet)"),
                Prop("deltaZ", "number", "Move distance in Z (feet, default: 0)")));
            decls.Add(Fn("delete_elements", "Delete elements by their IDs",
                PropArrayReq("elementIds", "Array of element IDs to delete", new JObject { ["type"] = "integer" })));
            decls.Add(Fn("select_elements", "Select elements in the Revit UI",
                PropArrayReq("elementIds", "Array of element IDs to select", new JObject { ["type"] = "integer" })));

            // ===== QA/QC =====
            decls.Add(Fn("check_warnings", "Check model warnings and errors"));
            decls.Add(Fn("audit_model", "Audit the model for issues (element count, warnings, etc.)"));
            decls.Add(Fn("get_model_statistics", "Get model statistics (element counts by category)"));

            // ===== NEW TOOLS =====

            // 1. Bulk Rename Views/Sheets
            decls.Add(Fn("bulk_rename_views", "Rename multiple views or sheets using find/replace",
                PropReq("find", "string", "Text to find in view/sheet names"),
                PropReq("replace", "string", "Replacement text"),
                Prop("targetType", "string", "Target: views, sheets, or both (default: views)")));

            // 2. Select By Filter
            decls.Add(Fn("select_by_filter", "Select all elements matching a filter (category, family, type, level)",
                Prop("category", "string", "Category to filter (Walls, Doors, Windows, etc.)"),
                Prop("familyName", "string", "Family name to match"),
                Prop("typeName", "string", "Type name to match"),
                Prop("levelName", "string", "Level name to match")));

            // 3. Copy Parameter Value
            decls.Add(Fn("copy_parameter_value", "Copy a parameter value from one element to many others",
                PropReq("sourceElementId", "integer", "Source element ID to copy from"),
                PropReq("parameterName", "string", "Parameter name to copy"),
                PropArrayReq("targetElementIds", "Target element IDs to copy to", new JObject { ["type"] = "integer" })));

            // 4. Color By Parameter
            decls.Add(Fn("color_by_parameter", "Apply color overrides to elements in the current view based on a parameter value",
                PropReq("category", "string", "Category to color (Walls, Doors, etc.)"),
                PropReq("parameterName", "string", "Parameter name to group by")));

            // 5. Purge Unused
            decls.Add(Fn("purge_unused", "Delete unused families and types from the model",
                Prop("category", "string", "Optional category to limit purge to")));

            // 6. Duplicate Sheets
            decls.Add(Fn("duplicate_sheets", "Duplicate an existing sheet with its title block",
                PropReq("sheetId", "integer", "Source sheet element ID"),
                Prop("count", "integer", "Number of copies (default: 1)"),
                Prop("suffix", "string", "Name suffix (default: ' - Copy')")));

            // 7. Room Finishes
            decls.Add(Fn("create_room_finishes", "Get room boundary information for creating wall/floor finishes",
                PropReq("roomId", "integer", "Room element ID"),
                Prop("finishType", "string", "Finish type: floor or wall (default: floor)")));

            // 8. Align Elements
            decls.Add(Fn("align_elements", "Align elements to a reference element",
                PropArrayReq("elementIds", "Element IDs to align", new JObject { ["type"] = "integer" }),
                PropReq("alignment", "string", "Alignment: left, right, top, bottom, center-h, center-v"),
                Prop("referenceElementId", "integer", "Reference element ID (default: first element)")));

            // 9. Renumber Elements
            decls.Add(Fn("renumber_elements", "Auto-renumber elements by spatial order",
                PropReq("category", "string", "Category to renumber (Doors, Windows, Rooms, etc.)"),
                Prop("parameterName", "string", "Parameter to set (default: Mark)"),
                Prop("prefix", "string", "Number prefix"),
                Prop("startNumber", "integer", "Starting number (default: 1)")));

            // 10. Auto Section Box
            decls.Add(Fn("auto_section_box", "Create a 3D view with section box fitted to selected elements",
                PropArrayReq("elementIds", "Element IDs to fit section box around", new JObject { ["type"] = "integer" }),
                Prop("padding", "number", "Padding in feet around elements (default: 3)")));

            // 11. Isolate Warnings
            decls.Add(Fn("isolate_warnings", "Filter and select elements that have model warnings",
                Prop("filter", "string", "Optional text filter for warning descriptions")));

            // 12. Purge CADs
            decls.Add(Fn("purge_cads", "Remove all linked and imported DWG/CAD files from the model"));

            // 13. Copy View Filters
            decls.Add(Fn("copy_view_filters", "Copy view filters from one view to other views",
                PropReq("sourceViewId", "integer", "Source view element ID"),
                PropArrayReq("targetViewIds", "Target view element IDs", new JObject { ["type"] = "integer" })));

            // 14. Extend/Shrink Element
            decls.Add(Fn("extend_shrink_element", "Extend or shrink a line-based element (wall, pipe, duct)",
                PropReq("elementId", "integer", "Element ID to extend/shrink"),
                PropReq("delta", "number", "Distance in feet (positive=extend, negative=shrink)"),
                Prop("end", "string", "Which end to modify: start or end (default: end)")));

            // 15. Rotate Element
            decls.Add(Fn("rotate_element", "Rotate an element around its center point",
                PropReq("elementId", "integer", "Element ID to rotate"),
                PropReq("angle", "number", "Rotation angle in degrees")));

            // 16. Place Views On Sheet
            decls.Add(Fn("place_views_on_sheet", "Place one or more views onto a sheet",
                PropReq("sheetId", "integer", "Sheet element ID"),
                PropArrayReq("viewIds", "View element IDs to place", new JObject { ["type"] = "integer" }),
                Prop("startX", "number", "X offset in feet from the sheet left (default: 1)"),
                Prop("startY", "number", "Y offset in feet from the sheet bottom (default: 1)"),
                Prop("spacing", "number", "Spacing between views in feet (default: 0.5)")));

            // 17. Export to CAD (DWG/DXF)
            decls.Add(Fn("export_to_cad", "Export views to DWG or DXF files",
                PropArrayReq("viewIds", "View element IDs to export (default: current view)", new JObject { ["type"] = "integer" }),
                Prop("folder", "string", "Export folder path (default: Desktop/RevitExport)"),
                Prop("format", "string", "Export format: DWG or DXF (default: DWG)")));

            // ===== EXPORT TOOLS (ProSheets) =====

            // 18. Export to PDF
            decls.Add(Fn("export_to_pdf", "Batch export sheets or views to PDF files",
                Prop("sheetIds", "string", "Comma-separated sheet element IDs (default: all sheets)"),
                Prop("viewIds", "string", "Comma-separated view element IDs"),
                Prop("folder", "string", "Export folder path"),
                Prop("combinePdf", "boolean", "Combine into single PDF (default: false)")));

            // 19. Export to IFC
            decls.Add(Fn("export_to_ifc", "Export model to IFC format for BIM coordination",
                Prop("folder", "string", "Export folder path"),
                Prop("ifcVersion", "string", "IFC version: IFC2x3 or IFC4 (default: IFC2x3)"),
                Prop("fileName", "string", "Custom filename")));

            // 20. Export to Images
            decls.Add(Fn("export_to_images", "Export views to image files (PNG, JPEG, TIFF)",
                PropArrayReq("viewIds", "View element IDs to export", new JObject { ["type"] = "integer" }),
                Prop("folder", "string", "Export folder path"),
                Prop("format", "string", "Image format: PNG, JPEG, TIFF, BMP (default: PNG)"),
                Prop("resolution", "integer", "DPI: 72, 150, 300, 600 (default: 150)")));

            // 21. Export to DGN
            decls.Add(Fn("export_to_dgn", "Export views to MicroStation DGN format",
                PropArrayReq("viewIds", "View element IDs to export", new JObject { ["type"] = "integer" }),
                Prop("folder", "string", "Export folder path")));

            // 22. Export to NWC
            decls.Add(Fn("export_to_nwc", "Export model to Navisworks NWC format",
                Prop("folder", "string", "Export folder path"),
                Prop("fileName", "string", "Custom filename")));

            // 23. Export Schedule Data
            decls.Add(Fn("export_schedule_data", "Export a Revit schedule to CSV file",
                Prop("scheduleId", "integer", "Schedule element ID"),
                Prop("scheduleName", "string", "Schedule name to find"),
                Prop("folder", "string", "Export folder path")));

            // 24. Export Parameters to CSV
            decls.Add(Fn("export_parameters_to_csv", "Export element parameters to CSV for bulk editing (like SheetLink)",
                PropReq("category", "string", "Category to export (Walls, Doors, Rooms, etc.)"),
                Prop("parameterNames", "string", "Comma-separated parameter names to include"),
                Prop("folder", "string", "Export folder path"),
                Prop("levelName", "string", "Filter by level name")));

            // 25. Import Parameters from CSV
            decls.Add(Fn("import_parameters_from_csv", "Import and update parameters from CSV file (like SheetLink import)",
                PropReq("filePath", "string", "Path to CSV file with ElementId column"),
                Prop("dryRun", "boolean", "Preview changes without applying (default: false)")));

            // ===== FAMILY & PARAMETER MANAGEMENT (DiRoots) =====

            // 26. Manage Families
            decls.Add(Fn("manage_families", "Batch rename or organize loaded families. Add prefix, suffix, or find/replace",
                PropReq("action", "string", "Action: rename, add_prefix, add_suffix, find_replace"),
                Prop("category", "string", "Category filter (Doors, Windows, etc.)"),
                Prop("find", "string", "Text to find"),
                Prop("replace", "string", "Replacement text"),
                Prop("prefix", "string", "Prefix to add"),
                Prop("suffix", "string", "Suffix to add")));

            // 27. Get Family Info
            decls.Add(Fn("get_family_info", "Get info about loaded families: types, instance counts",
                Prop("category", "string", "Category filter"),
                Prop("familyName", "string", "Specific family name")));

            // 28. Create Project Parameter
            decls.Add(Fn("create_project_parameter", "Create a new project parameter on categories",
                PropReq("name", "string", "Parameter name"),
                PropArrayReq("categories", "Categories to assign to (Walls, Doors, etc.)", new JObject { ["type"] = "string" }),
                Prop("type", "string", "Type: Text, Integer, Number, Length, Area, Volume, YesNo (default: Text)"),
                Prop("isInstance", "boolean", "Instance (true) or Type (false) parameter (default: true)")));

            // 29. Batch Set Parameter
            decls.Add(Fn("batch_set_parameter", "Set a parameter value on all matching elements (like OneParameter)",
                PropReq("category", "string", "Category of elements"),
                PropReq("parameterName", "string", "Parameter name to set"),
                PropReq("value", "string", "Value to set"),
                Prop("filterParameterName", "string", "Only modify where this parameter..."),
                Prop("filterValue", "string", "...equals this value"),
                Prop("levelName", "string", "Only modify on this level")));

            // 30. Delete Unused Families
            decls.Add(Fn("delete_unused_families", "Find and delete families with zero instances placed",
                Prop("category", "string", "Category filter"),
                Prop("dryRun", "boolean", "List without deleting (default: false)")));

            // ===== QUICKVIEWS (DiRoots) =====

            // 31. Create Elevation Views
            decls.Add(Fn("create_elevation_views", "Generate interior elevation views for rooms (like QuickViews)",
                Prop("roomIds", "string", "Comma-separated room element IDs (default: all rooms)"),
                Prop("levelName", "string", "Only rooms on this level"),
                Prop("viewTemplate", "string", "View template name"),
                Prop("scale", "integer", "View scale e.g. 50 (default: 50)")));

            // 32. Create Section Views
            decls.Add(Fn("create_section_views", "Generate section views through rooms",
                Prop("roomIds", "string", "Comma-separated room element IDs"),
                Prop("direction", "string", "Section direction: horizontal or vertical (default: horizontal)"),
                Prop("viewTemplate", "string", "View template name"),
                Prop("scale", "integer", "View scale (default: 50)")));

            // 33. Create Callout Views
            decls.Add(Fn("create_callout_views", "Generate callout views for rooms",
                Prop("roomIds", "string", "Comma-separated room element IDs"),
                Prop("parentViewId", "integer", "Parent view ID"),
                Prop("viewTemplate", "string", "View template name"),
                Prop("scale", "integer", "View scale (default: 20)")));

            // ===== VIEW & SHEET MANAGEMENT (DiRoots) =====

            // 34. Align Viewports
            decls.Add(Fn("align_viewports", "Align viewport placement across sheets (like ViewAligner)",
                PropReq("referenceSheetId", "integer", "Reference sheet to copy alignment from"),
                PropArrayReq("targetSheetIds", "Target sheets to apply alignment to", new JObject { ["type"] = "integer" })));

            // 35. Batch Create Sheets
            decls.Add(Fn("batch_create_sheets", "Create multiple sheets with auto-incrementing numbers",
                PropReq("startNumber", "string", "Starting sheet number e.g. A101"),
                PropReq("count", "integer", "Number of sheets to create"),
                Prop("namePattern", "string", "Name pattern, use {n} for number"),
                Prop("titleBlockName", "string", "Title block family name")));

            // 36. Duplicate View
            decls.Add(Fn("duplicate_view", "Duplicate a view with options",
                PropReq("viewId", "integer", "View element ID to duplicate"),
                Prop("count", "integer", "Number of copies (default: 1)"),
                Prop("duplicateType", "string", "Type: independent, as_dependent, with_detailing (default: with_detailing)"),
                Prop("suffix", "string", "Name suffix (default: ' - Copy')")));

            // 37. Apply View Template
            decls.Add(Fn("apply_view_template", "Apply a view template to views",
                PropArrayReq("viewIds", "View element IDs to apply template to", new JObject { ["type"] = "integer" }),
                PropReq("templateName", "string", "View template name")));

            // ===== VIEW & PROJECT SETTINGS =====

            // 38. Set View Properties
            decls.Add(Fn("set_view_properties", "Modify view settings: scale, detail level, visual/display style, discipline, phase, name, crop box. Use this for changing view appearance.",
                Prop("viewId", "integer", "View ID (default: active view)"),
                Prop("scale", "integer", "View scale denominator, e.g. 100 for 1:100"),
                Prop("detailLevel", "string", "View detail level: Coarse, Medium, or Fine"),
                Prop("displayStyle", "string", "Visual/display style: Wireframe, HiddenLine, Shading, ShadingWithEdges, or Realistic"),
                Prop("discipline", "string", "View discipline: Architectural, Structural, Mechanical, Electrical, Plumbing, Coordination"),
                Prop("phaseName", "string", "Phase to show in this view"),
                Prop("viewName", "string", "New name for the view"),
                Prop("showCropBox", "boolean", "Enable/disable crop box")));

            // 39. Override Element in View
            decls.Add(Fn("override_element_in_view", "Apply graphic overrides to elements in the current view (color, line weight, transparency, halftone, hide)",
                PropArrayReq("elementIds", "Element IDs to override", new JObject { ["type"] = "integer" }),
                Prop("colorR", "integer", "Override color Red (0-255)"),
                Prop("colorG", "integer", "Override color Green (0-255)"),
                Prop("colorB", "integer", "Override color Blue (0-255)"),
                Prop("lineWeight", "integer", "Override line weight (1-16)"),
                Prop("transparency", "integer", "Surface transparency (0-100)"),
                Prop("halftone", "boolean", "Apply halftone effect"),
                Prop("visible", "boolean", "Set to false to hide elements in view")));

            // 40. Modify Object Styles
            decls.Add(Fn("modify_object_styles", "Modify default line weight and color for a category (Object Styles)",
                PropReq("category", "string", "Category name (Walls, Doors, Furniture, etc.)"),
                Prop("subcategory", "string", "Subcategory name"),
                Prop("lineWeight", "integer", "Projection line weight (1-16)"),
                Prop("colorR", "integer", "Line color Red (0-255)"),
                Prop("colorG", "integer", "Line color Green (0-255)"),
                Prop("colorB", "integer", "Line color Blue (0-255)")));

            // 39. Override Element in View
            decls.Add(Fn("override_element_in_view", "Apply graphic overrides to elements in the current view (color, line weight, transparency, halftone, hide)",
                PropArrayReq("elementIds", "Element IDs to override", new JObject { ["type"] = "integer" }),
                Prop("colorR", "integer", "Override color Red (0-255)"),
                Prop("colorG", "integer", "Override color Green (0-255)"),
                Prop("colorB", "integer", "Override color Blue (0-255)"),
                Prop("lineWeight", "integer", "Override line weight (1-16)"),
                Prop("transparency", "integer", "Surface transparency (0-100)"),
                Prop("halftone", "boolean", "Apply halftone effect"),
                Prop("visible", "boolean", "Set to false to hide elements in view")));

            // 40. Modify Object Styles
            decls.Add(Fn("modify_object_styles", "Modify default line weight and color for a category (Object Styles)",
                PropReq("category", "string", "Category name (Walls, Doors, Furniture, etc.)"),
                Prop("subcategory", "string", "Subcategory name"),
                Prop("lineWeight", "integer", "Projection line weight (1-16)"),
                Prop("colorR", "integer", "Line color Red (0-255)"),
                Prop("colorG", "integer", "Line color Green (0-255)"),
                Prop("colorB", "integer", "Line color Blue (0-255)")));

            // 41. Open View
            decls.Add(Fn("open_view", "Open a specific view in the Revit UI",
                PropReq("viewId", "integer", "The Element ID of the view to open")));

            // 42. Close View
            decls.Add(Fn("close_view", "Close a specific view in the Revit UI",
                Prop("viewId", "integer", "The Element ID of the view to close. If not provided, closes the active view.")));

            return decls;
        }

        // Helper: create a function declaration
        private JObject Fn(string name, string description, params JProperty[] properties)
        {
            var fn = new JObject
            {
                ["name"] = name,
                ["description"] = description
            };

            if (properties.Length > 0)
            {
                var required = new JArray();
                var props = new JObject();
                foreach (var p in properties)
                {
                    props.Add(p);
                    // Check if required (set via PropReq)
                    var propObj = p.Value as JObject;
                    if (propObj != null && propObj.ContainsKey("_required"))
                    {
                        required.Add(p.Name);
                        propObj.Remove("_required");
                    }
                }

                fn["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required
                };
            }

            return fn;
        }

        // Optional property
        private JProperty Prop(string name, string type, string desc)
        {
            return new JProperty(name, new JObject
            {
                ["type"] = type,
                ["description"] = desc
            });
        }

        // Required property (marked with _required flag, cleaned up in Fn())
        private JProperty PropReq(string name, string type, string desc)
        {
            return new JProperty(name, new JObject
            {
                ["type"] = type,
                ["description"] = desc,
                ["_required"] = true
            });
        }

        // Required array property with items schema
        private JProperty PropArrayReq(string name, string desc, JObject items)
        {
            return new JProperty(name, new JObject
            {
                ["type"] = "array",
                ["description"] = desc,
                ["items"] = items,
                ["_required"] = true
            });
        }
    }

    public class GeminiResponse
    {
        public string? Text { get; set; }
        public GeminiFunctionCall? FunctionCall { get; set; }
        public bool IsFunctionCall => FunctionCall != null;
    }

    public class GeminiFunctionCall
    {
        public string Name { get; set; } = "";
        public JObject Arguments { get; set; } = new JObject();
    }
}
