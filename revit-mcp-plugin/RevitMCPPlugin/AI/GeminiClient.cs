using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPPlugin.Core;

namespace RevitMCPPlugin.AI
{
    /// <summary>
    /// Settings for the AI integration — persisted to disk.
    /// </summary>
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gemini-2.0-flash";
        public string Provider { get; set; } = "gemini"; // "gemini", "deepseek", "perplexity", "openrouter", or "ollama"

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
        public bool IsOpenRouter => Provider?.Equals("openrouter", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsOllama => Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsCerebras => Provider?.Equals("cerebras", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsGroq => Provider?.Equals("groq", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsOpenAI => Provider?.Equals("openai", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Tool group categories for lazy loading (Strategy A).
    /// </summary>
    public enum ToolGroup { Reading, Creating, Editing, Export, QA, Views, Data }

    /// <summary>
    /// HTTP client for AI APIs with function calling and token optimization.
    /// </summary>
    public class GeminiClient
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private static readonly HttpClient _ollamaHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(180) }; // Longer timeout for slow local models
        private readonly List<JObject> _history = new List<JObject>();
        private GeminiSettings _settings;
        private int _callIdCounter = 0;
        private HashSet<ToolGroup> _currentGroups;  // Strategy A: cached groups for current turn
        private const int MaxHistoryMessages = 14;  // Strategy C: was 40
        private const int MaxResultChars = 800;      // Strategy B: truncation limit

        public bool IsConfigured => _settings.IsOllama || !string.IsNullOrWhiteSpace(_settings.ApiKey);
        public string CurrentModel => _settings.Model;
        public string CurrentProvider => _settings.IsOllama ? "Ollama (Local)" : _settings.IsGroq ? "Groq" : _settings.IsCerebras ? "Cerebras" : _settings.IsOpenAI ? "OpenAI" : _settings.IsOpenRouter ? "OpenRouter" : _settings.IsPerplexity ? "Perplexity" : _settings.IsDeepSeek ? "DeepSeek" : "Gemini";

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

        /// <summary>Get a copy of the conversation history for persistence.</summary>
        public List<JObject> GetHistory() => new List<JObject>(_history);

        /// <summary>Replace the conversation history (for loading saved chats).</summary>
        public void SetHistory(List<JObject> history)
        {
            _history.Clear();
            if (history != null)
                _history.AddRange(history);
        }

        /// <summary>
        /// Send a user message and get a response. Returns either:
        /// - A FunctionCall (Gemini wants to call a Revit tool)
        /// - A text response (Gemini has a final answer)
        /// </summary>
        public async Task<GeminiResponse> SendMessageAsync(string userMessage)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API key not configured. Go to Settings to add your key.");

            // Strategy A: Ollama uses keyword-based filtering; all other providers get ALL tools
            _currentGroups = _settings.IsOllama ? GetRelevantGroups(userMessage) : null;

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

            // Strategy B: truncate result before storing in history
            var truncatedResult = TruncateResult(result);

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
                                ["result"] = truncatedResult
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
            if (_settings.IsDeepSeek || _settings.IsPerplexity || _settings.IsOpenRouter || _settings.IsOllama || _settings.IsCerebras || _settings.IsGroq || _settings.IsOpenAI)
                return await CallOpenAICompatibleAsync();
            return await CallGeminiAsync();
        }

        // ============ GEMINI API ============
        private bool _retried = false;
        private async Task<GeminiResponse> CallGeminiAsync()
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

            // Sanitize history to enforce valid turn ordering
            SanitizeHistory();

            // Deep-clone history and strip internal _callId fields
            var cleanedHistory = new JArray();
            foreach (var entry in _history)
            {
                var clone = (JObject)entry.DeepClone();
                var parts = clone["parts"] as JArray;
                if (parts != null)
                {
                    foreach (var part in parts)
                    {
                        var fc = part["functionCall"] as JObject;
                        if (fc != null) fc.Remove("_callId");

                        var fr = part["functionResponse"] as JObject;
                        if (fr != null) fr.Remove("_callId");
                    }
                }
                cleanedHistory.Add(clone);
            }

            var requestBody = new JObject
            {
                ["contents"] = cleanedHistory,
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
                        ["functionDeclarations"] = GetFunctionDeclarations(_currentGroups)
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.7,
                    ["maxOutputTokens"] = 8192
                }
            };

            // Debug: save request to file
            DebugLog("request", requestBody.ToString(Formatting.Indented));

            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            // Debug: save response to file
            DebugLog("response", responseStr);

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

                // Check for block reason in promptFeedback
                var blockReason = result["promptFeedback"]?["blockReason"]?.ToString();
                if (!string.IsNullOrEmpty(blockReason))
                    return new GeminiResponse { Text = $"Blocked by API: {blockReason}" };

                // Auto-fallback: try gemini-2.0-flash if current model is different
                if (!_retried && _settings.Model != "gemini-2.0-flash")
                {
                    _retried = true;
                    var origModel = _settings.Model;
                    _settings.Model = "gemini-2.0-flash";
                    try
                    {
                        var fallbackResult = await CallGeminiAsync();
                        return fallbackResult;
                    }
                    finally
                    {
                        _settings.Model = origModel; // Restore original
                        _retried = false;
                    }
                }

                // Retry once with clean history
                if (!_retried)
                {
                    _retried = true;
                    var lastUserMsg = _history.LastOrDefault(h => h["role"]?.ToString() == "user");
                    if (lastUserMsg != null)
                    {
                        _history.Clear();
                        _history.Add(lastUserMsg);
                        return await CallGeminiAsync();
                    }
                }
                _retried = false;

                return new GeminiResponse { Text = $"[No response — finish reason: {finishReason}]" };
            }
            _retried = false;

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
            if (_settings.IsOllama)
            {
                url = "http://localhost:11434/v1/chat/completions";
                providerLabel = "Ollama (Local)";
            }
            else if (_settings.IsCerebras)
            {
                url = "https://api.cerebras.ai/v1/chat/completions";
                providerLabel = "Cerebras";
            }
            else if (_settings.IsGroq)
            {
                url = "https://api.groq.com/openai/v1/chat/completions";
                providerLabel = "Groq";
            }
            else if (_settings.IsOpenAI)
            {
                url = "https://api.openai.com/v1/chat/completions";
                providerLabel = "OpenAI";
            }
            else if (_settings.IsOpenRouter)
            {
                url = "https://openrouter.ai/api/v1/chat/completions";
                providerLabel = "OpenRouter";
            }
            else if (_settings.IsPerplexity)
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
            foreach (var decl in GetFunctionDeclarations(_currentGroups))
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
                ["temperature"] = _settings.IsOllama ? 0.2 : 0.7,
                ["max_tokens"] = _settings.IsOllama ? 4096 : 8192
            };

            // Ollama-specific: generous context window and tuned parameters
            if (_settings.IsOllama)
            {
                requestBody["options"] = new JObject
                {
                    ["num_ctx"] = 16384,
                    ["num_predict"] = 4096,
                    ["repeat_penalty"] = 1.1,
                    ["top_p"] = 0.9
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

            // Ollama doesn't need an API key; others do
            if (!_settings.IsOllama)
                request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");

            // OpenRouter requires these headers
            if (_settings.IsOpenRouter)
            {
                request.Headers.Add("HTTP-Referer", "https://revitmcp.com");
                request.Headers.Add("X-Title", "Revit MCP");
            }

            // Use Ollama-specific HttpClient with longer timeout
            var httpClient = _settings.IsOllama ? _ollamaHttp : _http;
            var response = await httpClient.SendAsync(request);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errJson = JObject.Parse(responseStr);
                    var errMsg = errJson["error"]?["message"]?.ToString() ?? "";
                    // OpenRouter often includes upstream error in metadata.raw
                    var rawErr = errJson["error"]?["metadata"]?["raw"]?.ToString();
                    if (!string.IsNullOrEmpty(rawErr)) errMsg += $" | Details: {rawErr}";
                    if (string.IsNullOrEmpty(errMsg)) errMsg = responseStr;
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
            {
                if (_settings.IsOllama)
                    return new GeminiResponse { Text = "⚠️ Ollama returned no response. This usually means the model is too small or overloaded.\n\nTry:\n• Use a larger model (qwen2.5:14b or mistral-nemo)\n• Restart Ollama\n• Simplify your request" };
                return new GeminiResponse { Text = $"[No response from {providerLabel}]" };
            }

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
            var text = message["content"]?.ToString() ?? "";

            // Ollama fallback: if response is empty and we sent tools, retry WITHOUT tools
            if (string.IsNullOrWhiteSpace(text) && _settings.IsOllama && tools.Count > 0)
            {
                try
                {
                    // Retry without tools — many local models work fine for text but choke on tool schemas
                    var fallbackBody = new JObject
                    {
                        ["model"] = _settings.Model,
                        ["messages"] = messages,
                        ["temperature"] = 0.3,
                        ["max_tokens"] = 4096,
                        ["options"] = new JObject
                        {
                            ["num_ctx"] = 16384,
                            ["num_predict"] = 4096,
                            ["repeat_penalty"] = 1.1
                        }
                    };
                    var fallbackReq = new HttpRequestMessage(HttpMethod.Post, url);
                    fallbackReq.Content = new StringContent(fallbackBody.ToString(), Encoding.UTF8, "application/json");
                    var fallbackRes = await _ollamaHttp.SendAsync(fallbackReq);
                    var fallbackStr = await fallbackRes.Content.ReadAsStringAsync();
                    if (fallbackRes.IsSuccessStatusCode)
                    {
                        var fallbackResult = JObject.Parse(fallbackStr);
                        var fallbackText = fallbackResult["choices"]?[0]?["message"]?["content"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(fallbackText))
                            text = fallbackText;
                    }
                }
                catch { /* Fallback failed too — use whatever we got */ }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = _settings.IsOllama
                    ? "⚠️ Ollama returned an empty response. Try a larger model (qwen2.5:14b, mistral-nemo) or simplify your request."
                    : "[Empty response]";
            }

            _history.Add(new JObject
            {
                ["role"] = "model",
                ["parts"] = new JArray { new JObject { ["text"] = text } }
            });

            return new GeminiResponse { Text = text };
        }

        /// <summary>
        /// Direct analysis call — NO tools, NO history. Just a focused prompt → response.
        /// Much faster for Ollama since it skips all 160+ tool declarations.
        /// </summary>
        public async Task<string> AnalyzeDirectAsync(string prompt)
        {
            string url;
            if (_settings.IsOllama)
                url = "http://localhost:11434/v1/chat/completions";
            else if (_settings.IsCerebras)
                url = "https://api.cerebras.ai/v1/chat/completions";
            else if (_settings.IsGroq)
                url = "https://api.groq.com/openai/v1/chat/completions";
            else if (_settings.IsOpenAI)
                url = "https://api.openai.com/v1/chat/completions";
            else if (_settings.IsOpenRouter)
                url = "https://openrouter.ai/api/v1/chat/completions";
            else if (_settings.IsPerplexity)
                url = "https://api.perplexity.ai/chat/completions";
            else if (_settings.Provider == "deepseek")
                url = "https://api.deepseek.com/chat/completions";
            else
                url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

            // For Gemini, use the native API format
            if (_settings.Provider == "gemini")
            {
                var geminiBody = new JObject
                {
                    ["contents"] = new JArray
                    {
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray { new JObject { ["text"] = prompt } }
                        }
                    },
                    ["generationConfig"] = new JObject
                    {
                        ["temperature"] = 0.3,
                        ["maxOutputTokens"] = 4096
                    }
                };
                var geminiReq = new HttpRequestMessage(HttpMethod.Post, url);
                geminiReq.Content = new StringContent(geminiBody.ToString(), Encoding.UTF8, "application/json");
                var geminiRes = await _http.SendAsync(geminiReq);
                var geminiStr = await geminiRes.Content.ReadAsStringAsync();
                if (!geminiRes.IsSuccessStatusCode) throw new Exception($"Gemini error: {geminiStr}");
                var geminiResult = JObject.Parse(geminiStr);
                return geminiResult["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "[No response]";
            }

            // OpenAI-compatible path (Ollama, DeepSeek, etc.)
            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a construction and BIM data analyst. Analyze data concisely. Use tables. Be specific with numbers. No tool calls."
                },
                new JObject { ["role"] = "user", ["content"] = prompt }
            };

            var requestBody = new JObject
            {
                ["model"] = _settings.Model,
                ["messages"] = messages,
                ["temperature"] = 0.2,
                ["max_tokens"] = _settings.IsOllama ? 2048 : 4096
            };

            // Ollama: optimize for speed
            if (_settings.IsOllama)
            {
                requestBody["options"] = new JObject
                {
                    ["num_ctx"] = 4096,
                    ["repeat_penalty"] = 1.1
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            if (!_settings.IsOllama)
                request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
            if (_settings.IsOpenRouter)
            {
                request.Headers.Add("HTTP-Referer", "https://revitmcp.com");
                request.Headers.Add("X-Title", "Revit MCP");
            }

            var response = await _http.SendAsync(request);
            var responseStr = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"API error: {responseStr}");

            var result = JObject.Parse(responseStr);
            return result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "[No response]";
        }


        /// <summary>Saves debug info to %APPDATA%/RevitMCP/ for API diagnostics.</summary>
        private static void DebugLog(string name, string content)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RevitMCP");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"debug_{name}.json"), content);
            }
            catch { /* ignore */ }
        }

        // Strategy C: Keep history bounded — trim at valid boundaries only
        private void TrimHistory()
        {
            if (_history.Count <= MaxHistoryMessages) return;

            var cutTarget = _history.Count - MaxHistoryMessages;
            // Find the first safe cut point — must start with a "user" role
            // and never leave an orphaned function call without its response
            var cutAt = cutTarget;
            while (cutAt < _history.Count)
            {
                var role = _history[cutAt]["role"]?.ToString();
                if (role == "user") break;
                cutAt++;
            }
            if (cutAt > 0 && cutAt < _history.Count)
                _history.RemoveRange(0, cutAt);
        }

        /// <summary>
        /// Sanitizes conversation history to enforce Gemini's strict turn ordering:
        /// user → model (functionCall) → function (functionResponse) → model ...
        /// Removes orphaned function calls/responses that would cause 400 errors.
        /// </summary>
        private void SanitizeHistory()
        {
            var sanitized = new List<JObject>();

            for (int i = 0; i < _history.Count; i++)
            {
                var entry = _history[i];
                var role = entry["role"]?.ToString();

                if (role == "user")
                {
                    sanitized.Add(entry);
                }
                else if (role == "model")
                {
                    // Check if this is a function call — if so, verify the next entry is a function response
                    var parts = entry["parts"] as JArray;
                    var hasFunctionCall = parts != null && parts.Any(p => p["functionCall"] != null);

                    if (hasFunctionCall)
                    {
                        // Only keep if the next entry is a matching function response
                        if (i + 1 < _history.Count && _history[i + 1]["role"]?.ToString() == "function")
                        {
                            sanitized.Add(entry);
                        }
                        // else: skip orphaned function call
                    }
                    else
                    {
                        sanitized.Add(entry); // Regular model text response
                    }
                }
                else if (role == "function")
                {
                    // Only keep if the previous sanitized entry is a model with functionCall
                    if (sanitized.Count > 0)
                    {
                        var prev = sanitized[sanitized.Count - 1];
                        var prevParts = prev["parts"] as JArray;
                        var prevHasFc = prev["role"]?.ToString() == "model" &&
                                        prevParts != null && prevParts.Any(p => p["functionCall"] != null);
                        if (prevHasFc)
                        {
                            sanitized.Add(entry);
                        }
                        // else: skip orphaned function response
                    }
                }
            }

            // Ensure history starts with a user message
            while (sanitized.Count > 0 && sanitized[0]["role"]?.ToString() != "user")
                sanitized.RemoveAt(0);

            // Ensure history doesn't end with a dangling function call (no response)
            while (sanitized.Count > 0)
            {
                var lastRole = sanitized[sanitized.Count - 1]["role"]?.ToString();
                var lastParts = sanitized[sanitized.Count - 1]["parts"] as JArray;
                var lastHasFc = lastRole == "model" && lastParts != null && lastParts.Any(p => p["functionCall"] != null);
                if (lastHasFc)
                    sanitized.RemoveAt(sanitized.Count - 1);
                else
                    break;
            }

            _history.Clear();
            _history.AddRange(sanitized);
        }

        // Strategy B: Truncate large tool results to save tokens
        private JToken TruncateResult(JToken result)
        {
            if (result == null) return new JObject();
            var str = result.ToString(Formatting.None);
            if (str.Length <= MaxResultChars) return result;

            int itemCount = 0;
            if (result is JArray arr) itemCount = arr.Count;
            else if (result is JObject obj && obj["items"] is JArray items) itemCount = items.Count;

            var truncated = str.Substring(0, MaxResultChars);
            var suffix = itemCount > 0 ? $"...(truncated, {itemCount} items total)" : "...(truncated)";
            return JToken.FromObject(truncated + suffix);
        }

        private string GetSystemInstruction()
        {
            var toolCatalog = GetToolCatalog();

            if (_settings.IsOllama)
            {
                return @"You are a Revit BIM assistant inside Autodesk Revit. You have FULL CONTROL and AUTHORITY over the Revit model. You can query, create, modify, and delete any element without asking for permission.

CRITICAL RULES:
1. Call only ONE tool per step. Wait for the result before deciding what to do next.
2. After receiving a tool result, ALWAYS respond with a text message summarizing what happened. Do NOT call another tool unless the user asks for more.
3. NEVER call the same tool twice in a row.
4. Coordinates are in FEET (1 meter = 3.281 feet).
5. Be concise in your responses. Summarize tool results for the user.
6. If a tool returns an error, explain the error to the user and suggest a fix.
7. NEVER suggest Dynamo scripts or manual steps — use your tools directly.
8. BEFORE attempting any task, check your available tools below. Always use the right tool for the job." + toolCatalog;
            }

            return @"You are a Revit BIM assistant inside Autodesk Revit with FULL CONTROL and AUTHORITY over the model. You can read, create, modify, delete, export, and manage ANY element or setting directly using your tools.

WORKFLOW (follow this for EVERY request):
1. CHECK TOOLS FIRST — Look at the AVAILABLE TOOLS list below. Can any existing tool handle the request?
2. USE TOOL — If a tool fits, call it directly. Chain multiple tools for complex tasks.
3. WRITE CODE — If NO tool fits, use execute_code to write C# code using the Revit API. You have access to __doc__ (Document), __uidoc__ (UIDocument), and __uiapp__ (UIApplication). The code runs inside a transaction automatically.

CORE RULES:
- Coordinates are in FEET (1m ≈ 3.281ft). Confirm levels before creating geometry.
- You have FULL AUTHORITY — act directly, NEVER ask for permission. Just do it.
- NEVER suggest Dynamo scripts, Python scripts, or manual steps.
- For complex tasks, chain multiple tool calls. You can call up to 30 tools per turn.

CODE EXECUTION (execute_code):
When no built-in tool fits, write C# code. You have access to:
- __doc__ = the active Revit Document
- __uidoc__ = the active UIDocument
- __uiapp__ = the UIApplication
- All Revit API namespaces are pre-imported (Autodesk.Revit.DB, .Architecture, .Structure, .UI)
- Your code runs inside a Transaction automatically
- Return a value to send results back (e.g., return new { count = 5, message = ""done"" })

IMPORTANT API NOTES (Revit 2024+):
- Use new ElementId((long)value) — NOT new ElementId((int)value) 
- Use element.Id.Value — NOT element.Id.Value (deprecated)
- Cast to long when creating ElementId from numbers

DATA STORAGE:
Use save_project_data/load_project_data for persistent data between sessions.

INTEGRATIONS:
Use get_integration_status to check configured integrations (Excel, Notion, Google Sheets, SQLite)." + toolCatalog;
        }

        /// <summary>
        /// Generates a compact tool catalog for injection into the system prompt.
        /// This lets the AI know ALL available tools upfront so it can pick the right one.
        /// Now reads from the Authority Bank (ToolRegistry).
        /// </summary>
        private string GetToolCatalog()
        {
            return ToolRegistry.GetToolCatalog();
        }

        // Strategy A: delegates to ToolRegistry for keyword-based category selection
        private HashSet<ToolGroup> GetRelevantGroups(string userMessage)
        {
            var cats = ToolRegistry.GetRelevantCategories(userMessage);
            var groups = new HashSet<ToolGroup>();

            // Map ToolCategory → ToolGroup
            if (cats.Contains(ToolCategory.Reading)) groups.Add(ToolGroup.Reading);
            if (cats.Contains(ToolCategory.Creating)) groups.Add(ToolGroup.Creating);
            if (cats.Contains(ToolCategory.Editing)) groups.Add(ToolGroup.Editing);
            if (cats.Contains(ToolCategory.Export) || cats.Contains(ToolCategory.Integrations)) groups.Add(ToolGroup.Export);
            if (cats.Contains(ToolCategory.QAQC)) groups.Add(ToolGroup.QA);
            if (cats.Contains(ToolCategory.Views)) groups.Add(ToolGroup.Views);
            if (cats.Contains(ToolCategory.Data) || cats.Contains(ToolCategory.ProjectFiles)) groups.Add(ToolGroup.Data);
            if (cats.Contains(ToolCategory.MEP) || cats.Contains(ToolCategory.Structural) ||
                cats.Contains(ToolCategory.Architecture) || cats.Contains(ToolCategory.Site)) groups.Add(ToolGroup.Creating);
            if (cats.Contains(ToolCategory.Annotation)) groups.Add(ToolGroup.Views);

            return groups;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var kw in keywords)
                if (text.Contains(kw)) return true;
            return false;
        }

        // ============ STRATEGIES A+D: Grouped & compressed tool declarations ============
        private JArray GetFunctionDeclarations(HashSet<ToolGroup> groups = null)
        {
            // Ollama local models: send only essential tools to avoid overwhelming the small model
            if (_settings.IsOllama)
                return GetOllamaFunctionDeclarations(groups);

            var d = new JArray();

            // ===== READING (16 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Reading))
            {
                d.Add(Fn("get_current_view_info", "Active view info"));
                d.Add(Fn("get_current_view_elements", "Elements in active view",
                    Prop("category", "string", "Category filter")));
                d.Add(Fn("get_selected_elements", "Selected elements"));
                d.Add(Fn("get_elements", "Query elements",
                    Prop("category", "string", "Category"),
                    Prop("typeName", "string", "Type filter"),
                    Prop("levelName", "string", "Level filter")));
                d.Add(Fn("get_parameters", "Element parameters",
                    PropReq("elementId", "integer", "Element ID")));
                d.Add(Fn("get_project_info", "Project info"));
                d.Add(Fn("get_views", "List views",
                    Prop("type", "string", "Type filter")));
                d.Add(Fn("get_sheets", "List sheets"));
                d.Add(Fn("get_levels", "List levels"));
                d.Add(Fn("get_grids", "List grids"));
                d.Add(Fn("get_rooms", "List rooms"));
                d.Add(Fn("get_available_family_types", "Available family types",
                    Prop("category", "string", "Category")));
                d.Add(Fn("get_schedules", "List schedules"));
                d.Add(Fn("get_linked_models", "Linked models"));
                d.Add(Fn("get_warnings", "Model warnings"));
                d.Add(Fn("get_family_info", "Family info",
                    Prop("category", "string", "Category"),
                    Prop("familyName", "string", "Family name")));
            }

            // ===== CREATING (17 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Creating))
            {
                d.Add(Fn("create_wall", "Create wall",
                    PropReq("startX", "number", "Start X (ft)"),
                    PropReq("startY", "number", "Start Y (ft)"),
                    PropReq("endX", "number", "End X (ft)"),
                    PropReq("endY", "number", "End Y (ft)"),
                    PropReq("levelName", "string", "Level"),
                    Prop("height", "number", "Height (ft)")));
                d.Add(Fn("create_floor", "Create floor from boundary points",
                    PropArrayReq("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                    PropReq("levelName", "string", "Level"),
                    Prop("typeName", "string", "Floor type")));
                d.Add(Fn("create_ceiling", "Create ceiling from boundary points",
                    PropArrayReq("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                    PropReq("levelName", "string", "Level"),
                    Prop("typeName", "string", "Ceiling type")));
                d.Add(Fn("create_roof", "Create roof from boundary points",
                    PropArrayReq("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                    PropReq("levelName", "string", "Level"),
                    Prop("typeName", "string", "Roof type")));
                d.Add(Fn("create_level", "Create level",
                    PropReq("name", "string", "Name"),
                    PropReq("elevation", "number", "Elevation (ft)")));
                d.Add(Fn("create_grid", "Create grid",
                    PropReq("startX", "number", "Start X (ft)"),
                    PropReq("startY", "number", "Start Y (ft)"),
                    PropReq("endX", "number", "End X (ft)"),
                    PropReq("endY", "number", "End Y (ft)"),
                    Prop("name", "string", "Name")));
                d.Add(Fn("create_room", "Create room",
                    PropReq("x", "number", "X (ft)"),
                    PropReq("y", "number", "Y (ft)"),
                    PropReq("levelName", "string", "Level"),
                    Prop("roomName", "string", "Name"),
                    Prop("roomNumber", "string", "Number")));
                d.Add(Fn("create_sheet", "Create sheet",
                    Prop("sheetNumber", "string", "Number"),
                    Prop("sheetName", "string", "Name"),
                    Prop("titleBlockName", "string", "Title block")));
                d.Add(Fn("create_view", "Create a new view",
                    PropReq("viewType", "string", "FloorPlan/CeilingPlan/Section/Elevation/ThreeD/Drafting"),
                    Prop("levelName", "string", "Level (for plan views)"),
                    Prop("name", "string", "View name")));
                d.Add(Fn("create_schedule", "Create a schedule view",
                    PropReq("category", "string", "Category"),
                    Prop("name", "string", "Schedule name"),
                    Prop("fields", "string", "Parameter names (comma-sep)")));
                d.Add(Fn("create_tag", "Place a tag on an element",
                    PropReq("elementId", "integer", "Element ID"),
                    Prop("tagType", "string", "Tag type name"),
                    Prop("hasLeader", "boolean", "Show leader")));
                d.Add(Fn("create_dimension", "Create a dimension between elements",
                    PropArrayReq("elementIds", "Element IDs to dimension", new JObject { ["type"] = "integer" }),
                    Prop("dimensionType", "string", "Dimension type")));
                d.Add(Fn("create_text_note", "Place a text note",
                    PropReq("text", "string", "Text content"),
                    PropReq("x", "number", "X position (ft)"),
                    PropReq("y", "number", "Y position (ft)")));
                d.Add(Fn("create_project_parameter", "Create project parameter",
                    PropReq("name", "string", "Name"),
                    PropArrayReq("categories", "Categories", new JObject { ["type"] = "string" }),
                    Prop("type", "string", "Text/Integer/Number/Length/Area/Volume/YesNo"),
                    Prop("isInstance", "boolean", "Instance param (default: true)")));
                d.Add(Fn("create_point_based_element", "Place point-based family instance",
                    PropReq("familyName", "string", "Family name"),
                    PropReq("typeName", "string", "Type name"),
                    PropReq("x", "number", "X (ft)"),
                    PropReq("y", "number", "Y (ft)"),
                    PropReq("levelName", "string", "Level")));
                d.Add(Fn("create_line_based_element", "Place line-based family instance",
                    PropReq("familyName", "string", "Family name"),
                    PropReq("typeName", "string", "Type name"),
                    PropReq("startX", "number", "Start X (ft)"),
                    PropReq("startY", "number", "Start Y (ft)"),
                    PropReq("endX", "number", "End X (ft)"),
                    PropReq("endY", "number", "End Y (ft)"),
                    PropReq("levelName", "string", "Level")));
                d.Add(Fn("room_to_floor", "Create floors from room boundaries",
                    Prop("roomIds", "string", "Room IDs (comma-sep, or all)"),
                    Prop("floorType", "string", "Floor type name")));
                d.Add(Fn("add_shared_parameter", "Add shared parameter to category",
                    PropReq("parameterName", "string", "Parameter name"),
                    PropReq("category", "string", "Category"),
                    Prop("groupName", "string", "Group (default: Data)"),
                    Prop("paramType", "string", "Text/Number/Length/Area/Volume/YesNo"),
                    Prop("isInstance", "boolean", "Instance param (default: true)")));
                d.Add(Fn("generate_legend", "Generate legend view (Doors/Windows)",
                    Prop("category", "string", "Doors or Windows"),
                    Prop("legendName", "string", "Legend name")));
                d.Add(Fn("convert_category", "Convert elements to different family/type",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("targetFamily", "string", "Target family"),
                    Prop("targetType", "string", "Target type")));
            }

            // ===== EDITING (21 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Editing))
            {
                d.Add(Fn("modify_element", "Modify element parameters",
                    PropReq("elementId", "integer", "Element ID"),
                    PropArrayReq("modifications", "Parameter changes", new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["parameterName"] = new JObject { ["type"] = "string" },
                            ["value"] = new JObject { ["type"] = "string" }
                        },
                        ["required"] = new JArray("parameterName", "value")
                    })));
                d.Add(Fn("move_element", "Move element",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("deltaX", "number", "Delta X (ft)"),
                    PropReq("deltaY", "number", "Delta Y (ft)"),
                    Prop("deltaZ", "number", "Delta Z (ft)")));
                d.Add(Fn("delete_elements", "Delete elements",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("copy_element", "Copy element to new location",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("deltaX", "number", "Delta X (ft)"),
                    PropReq("deltaY", "number", "Delta Y (ft)"),
                    Prop("deltaZ", "number", "Delta Z (ft)")));
                d.Add(Fn("mirror_element", "Mirror element about an axis",
                    PropReq("elementId", "integer", "Element ID"),
                    Prop("axisStartX", "number", "Axis start X"),
                    Prop("axisStartY", "number", "Axis start Y"),
                    Prop("axisEndX", "number", "Axis end X"),
                    Prop("axisEndY", "number", "Axis end Y")));
                d.Add(Fn("select_elements", "Select elements in UI",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("bulk_rename_views", "Find/replace in view/sheet names",
                    PropReq("find", "string", "Find text"),
                    PropReq("replace", "string", "Replace text"),
                    Prop("targetType", "string", "views/sheets/both")));
                d.Add(Fn("select_by_filter", "Select elements by filter",
                    Prop("category", "string", "Category"),
                    Prop("familyName", "string", "Family"),
                    Prop("typeName", "string", "Type"),
                    Prop("levelName", "string", "Level")));
                d.Add(Fn("copy_parameter_value", "Copy parameter between elements",
                    PropReq("sourceElementId", "integer", "Source ID"),
                    PropReq("parameterName", "string", "Parameter"),
                    PropArrayReq("targetElementIds", "Target IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("color_by_parameter", "Color elements by parameter value",
                    PropReq("category", "string", "Category"),
                    PropReq("parameterName", "string", "Parameter")));
                d.Add(Fn("align_elements", "Align elements",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("alignment", "string", "left/right/top/bottom/center-h/center-v"),
                    Prop("referenceElementId", "integer", "Reference ID")));
                d.Add(Fn("group_elements", "Group elements together",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    Prop("groupName", "string", "Group name")));
                d.Add(Fn("change_type", "Change element type",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("newTypeName", "string", "New type name")));
                d.Add(Fn("set_workset", "Move elements to workset",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("worksetName", "string", "Workset name")));
                d.Add(Fn("renumber_elements", "Renumber by spatial order",
                    PropReq("category", "string", "Category"),
                    Prop("parameterName", "string", "Parameter (default: Mark)"),
                    Prop("prefix", "string", "Prefix"),
                    Prop("startNumber", "integer", "Start number")));
                d.Add(Fn("extend_shrink_element", "Extend/shrink line element",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("delta", "number", "+extend/-shrink (ft)"),
                    Prop("end", "string", "start or end")));
                d.Add(Fn("rotate_element", "Rotate element",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("angle", "number", "Angle (degrees)")));
                d.Add(Fn("manage_families", "Batch rename families",
                    PropReq("action", "string", "rename/add_prefix/add_suffix/find_replace"),
                    Prop("category", "string", "Category"),
                    Prop("find", "string", "Find"),
                    Prop("replace", "string", "Replace"),
                    Prop("prefix", "string", "Prefix"),
                    Prop("suffix", "string", "Suffix")));
                d.Add(Fn("batch_set_parameter", "Set parameter on matching elements",
                    PropReq("category", "string", "Category"),
                    PropReq("parameterName", "string", "Parameter"),
                    PropReq("value", "string", "Value"),
                    Prop("filterParameterName", "string", "Filter parameter"),
                    Prop("filterValue", "string", "Filter value"),
                    Prop("levelName", "string", "Level filter")));
                d.Add(Fn("set_phase", "Set element phase",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("phaseName", "string", "Phase name")));
                d.Add(Fn("set_material", "Set element material",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("materialName", "string", "Material name"),
                    Prop("parameterName", "string", "Material parameter name")));
                d.Add(Fn("batch_modify_parameters", "Set parameter on specific elements by ID",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("parameterName", "string", "Parameter"),
                    PropReq("value", "string", "Value")));
                d.Add(Fn("parameter_case_convert", "Convert parameter text case (UPPER/lower/Title)",
                    PropReq("category", "string", "Category"),
                    PropReq("parameterName", "string", "Parameter"),
                    Prop("caseType", "string", "UPPER/lower/Title")));
                d.Add(Fn("select_by_workset", "Select all elements on a workset",
                    PropReq("worksetName", "string", "Workset name")));
                d.Add(Fn("filter_selection", "Filter current selection by category/level",
                    Prop("category", "string", "Category"),
                    Prop("levelName", "string", "Level")));
                d.Add(Fn("inverse_selection", "Invert current selection in active view"));
            }

            // ===== EXPORT (9 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Export))
            {
                d.Add(Fn("export_to_cad", "Export to DWG/DXF",
                    PropArrayReq("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                    Prop("folder", "string", "Folder"),
                    Prop("format", "string", "DWG or DXF")));
                d.Add(Fn("export_to_pdf", "Export to PDF",
                    Prop("sheetIds", "string", "Sheet IDs (comma-sep)"),
                    Prop("viewIds", "string", "View IDs (comma-sep)"),
                    Prop("folder", "string", "Folder"),
                    Prop("combinePdf", "boolean", "Combine into one PDF")));
                d.Add(Fn("export_to_ifc", "Export to IFC",
                    Prop("folder", "string", "Folder"),
                    Prop("ifcVersion", "string", "IFC2x3 or IFC4"),
                    Prop("fileName", "string", "Filename")));
                d.Add(Fn("export_to_images", "Export to images",
                    PropArrayReq("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                    Prop("folder", "string", "Folder"),
                    Prop("format", "string", "PNG/JPEG/TIFF/BMP"),
                    Prop("resolution", "integer", "DPI")));
                d.Add(Fn("export_to_dgn", "Export to DGN",
                    PropArrayReq("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                    Prop("folder", "string", "Folder")));
                d.Add(Fn("export_to_nwc", "Export to NWC",
                    Prop("folder", "string", "Folder"),
                    Prop("fileName", "string", "Filename")));
                d.Add(Fn("export_schedule_data", "Export schedule to CSV",
                    Prop("scheduleId", "integer", "Schedule ID"),
                    Prop("scheduleName", "string", "Schedule name"),
                    Prop("folder", "string", "Folder")));
                d.Add(Fn("export_parameters_to_csv", "Export parameters to CSV",
                    PropReq("category", "string", "Category"),
                    Prop("parameterNames", "string", "Parameters (comma-sep)"),
                    Prop("folder", "string", "Folder"),
                    Prop("levelName", "string", "Level filter")));
                d.Add(Fn("import_parameters_from_csv", "Import parameters from CSV",
                    PropReq("filePath", "string", "CSV file path"),
                    Prop("dryRun", "boolean", "Preview only")));
                d.Add(Fn("export_to_dwf", "Export views to DWF",
                    Prop("sheetIds", "string", "Sheet IDs (comma-sep)"),
                    Prop("viewIds", "string", "View IDs (comma-sep)"),
                    Prop("outputFolder", "string", "Output folder")));

                // ── Integration Tools ──
                d.Add(Fn("get_integration_status", "Get the status of all integrations (Excel, Notion, Google Sheets, SQLite, Ollama) — shows which are enabled and configured"));
                d.Add(Fn("export_to_excel_integration", "Export Revit elements to an Excel file via the Data Bridge",
                    PropReq("category", "string", "Revit category to export (e.g. Walls, Doors)"),
                    Prop("filePath", "string", "Output .xlsx file path")));
                d.Add(Fn("export_to_notion_integration", "Export Revit elements to a Notion database via the Data Bridge",
                    PropReq("category", "string", "Revit category to export"),
                    Prop("databaseId", "string", "Notion database ID")));
                d.Add(Fn("export_to_google_sheets_integration", "Export Revit elements to Google Sheets via the Data Bridge",
                    PropReq("category", "string", "Revit category to export"),
                    Prop("spreadsheetId", "string", "Google Sheets spreadsheet ID")));
            }

            // ===== QA/QC (10 tools) =====
            if (groups == null || groups.Contains(ToolGroup.QA))
            {
                d.Add(Fn("check_warnings", "Check model warnings"));
                d.Add(Fn("audit_model", "Audit model"));
                d.Add(Fn("get_model_statistics", "Model statistics"));
                d.Add(Fn("purge_unused", "Purge unused families/types (runs deep purge)",
                    Prop("category", "string", "Category")));
                d.Add(Fn("deep_purge", "Deep multi-pass purge of all unused elements"));
                d.Add(Fn("isolate_warnings", "Select warning elements",
                    Prop("filter", "string", "Warning text filter")));
                d.Add(Fn("purge_cads", "Remove all CAD imports"));
                d.Add(Fn("delete_unused_families", "Delete zero-instance families",
                    Prop("category", "string", "Category"),
                    Prop("dryRun", "boolean", "Preview only")));
                d.Add(Fn("find_cad_imports", "Find/delete CAD imports",
                    Prop("delete", "boolean", "Delete found imports")));
                d.Add(Fn("delete_empty_groups", "Delete empty model groups"));
                d.Add(Fn("resolve_warnings", "List or auto-resolve warnings (duplicate marks, unenclosed rooms)",
                    Prop("action", "string", "list or resolve"),
                    Prop("warningType", "string", "Warning text filter")));
                d.Add(Fn("auto_join_elements", "Auto-join geometry between categories",
                    Prop("category1", "string", "First category (default: Walls)"),
                    Prop("category2", "string", "Second category (default: Floors)")));
                d.Add(Fn("wall_floor_sync", "Sync wall-floor connections by joining intersecting geometry",
                    Prop("levelName", "string", "Level filter")));
                d.Add(Fn("import_data_from_csv", "Import parameter data from CSV to matching elements",
                    PropReq("filePath", "string", "CSV file path"),
                    Prop("category", "string", "Category"),
                    Prop("keyParameter", "string", "Key column (default: Number)")));
            }

            // ===== VIEWS & SHEETS (22 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Views))
            {
                d.Add(Fn("duplicate_sheets", "Duplicate sheet",
                    PropReq("sheetId", "integer", "Sheet ID"),
                    Prop("count", "integer", "Copies"),
                    Prop("suffix", "string", "Suffix")));
                d.Add(Fn("auto_section_box", "3D section box around elements",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    Prop("padding", "number", "Padding (ft)")));
                d.Add(Fn("copy_view_filters", "Copy view filters",
                    PropReq("sourceViewId", "integer", "Source view ID"),
                    PropArrayReq("targetViewIds", "Target view IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("place_views_on_sheet", "Place views on sheet",
                    PropReq("sheetId", "integer", "Sheet ID"),
                    PropArrayReq("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                    Prop("startX", "number", "X offset (ft)"),
                    Prop("startY", "number", "Y offset (ft)"),
                    Prop("spacing", "number", "Spacing (ft)")));
                d.Add(Fn("tag_all_in_view", "Tag all elements of a category in view",
                    PropReq("category", "string", "Category"),
                    Prop("tagType", "string", "Tag type")));
                d.Add(Fn("create_elevation_views", "Create room elevations",
                    Prop("roomIds", "string", "Room IDs (comma-sep)"),
                    Prop("levelName", "string", "Level"),
                    Prop("viewTemplate", "string", "Template"),
                    Prop("scale", "integer", "Scale")));
                d.Add(Fn("create_section_views", "Create room sections",
                    Prop("roomIds", "string", "Room IDs (comma-sep)"),
                    Prop("direction", "string", "horizontal/vertical"),
                    Prop("viewTemplate", "string", "Template"),
                    Prop("scale", "integer", "Scale")));
                d.Add(Fn("create_callout_views", "Create room callouts",
                    Prop("roomIds", "string", "Room IDs (comma-sep)"),
                    Prop("parentViewId", "integer", "Parent view ID"),
                    Prop("viewTemplate", "string", "Template"),
                    Prop("scale", "integer", "Scale")));
                d.Add(Fn("align_viewports", "Align viewports across sheets",
                    PropReq("referenceSheetId", "integer", "Reference sheet ID"),
                    PropArrayReq("targetSheetIds", "Target sheet IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("batch_create_sheets", "Create multiple sheets",
                    PropReq("startNumber", "string", "Start number"),
                    PropReq("count", "integer", "Count"),
                    Prop("namePattern", "string", "Pattern ({n})"),
                    Prop("titleBlockName", "string", "Title block")));
                d.Add(Fn("duplicate_view", "Duplicate view",
                    PropReq("viewId", "integer", "View ID"),
                    Prop("count", "integer", "Copies"),
                    Prop("duplicateType", "string", "independent/as_dependent/with_detailing"),
                    Prop("suffix", "string", "Suffix")));
                d.Add(Fn("apply_view_template", "Apply view template",
                    PropArrayReq("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                    PropReq("templateName", "string", "Template name")));
                d.Add(Fn("set_view_properties", "Set view properties (scale, detail, style, phase, name, crop)",
                    Prop("viewId", "integer", "View ID"),
                    Prop("scale", "integer", "Scale (e.g. 100)"),
                    Prop("detailLevel", "string", "Coarse/Medium/Fine"),
                    Prop("displayStyle", "string", "Wireframe/HiddenLine/Shading/ShadingWithEdges/Realistic"),
                    Prop("discipline", "string", "Discipline"),
                    Prop("phaseName", "string", "Phase"),
                    Prop("viewName", "string", "New name"),
                    Prop("showCropBox", "boolean", "Show crop box")));
                d.Add(Fn("override_element_in_view", "Graphic overrides in view",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    Prop("colorR", "integer", "Red (0-255)"),
                    Prop("colorG", "integer", "Green (0-255)"),
                    Prop("colorB", "integer", "Blue (0-255)"),
                    Prop("lineWeight", "integer", "Line weight (1-16)"),
                    Prop("transparency", "integer", "Transparency (0-100)"),
                    Prop("halftone", "boolean", "Halftone"),
                    Prop("visible", "boolean", "Visible")));
                d.Add(Fn("modify_object_styles", "Modify category object styles",
                    PropReq("category", "string", "Category"),
                    Prop("subcategory", "string", "Subcategory"),
                    Prop("lineWeight", "integer", "Line weight (1-16)"),
                    Prop("colorR", "integer", "Red"),
                    Prop("colorG", "integer", "Green"),
                    Prop("colorB", "integer", "Blue")));
                d.Add(Fn("open_view", "Open view",
                    PropReq("viewId", "integer", "View ID")));
                d.Add(Fn("close_view", "Close view",
                    Prop("viewId", "integer", "View ID (default: active)")));
                d.Add(Fn("set_visibility_graphics", "Show/hide categories or linked models in view (Visibility/Graphics)",
                    Prop("viewId", "integer", "View ID (default: active)"),
                    Prop("category", "string", "Category to show/hide (Walls, Doors, etc.)"),
                    Prop("visible", "boolean", "Show (true) or hide (false) the category"),
                    Prop("hideLinks", "boolean", "Hide all Revit links (true) or show (false)"),
                    Prop("linkName", "string", "Hide/show specific link by name"),
                    Prop("halftone", "boolean", "Apply halftone to category"),
                    Prop("transparency", "integer", "Category transparency (0-100)")));
                d.Add(Fn("crop_region_sync", "Sync crop regions between views",
                    PropReq("sourceViewId", "integer", "Source view ID"),
                    PropArrayReq("targetViewIds", "Target view IDs", new JObject { ["type"] = "integer" })));
                d.Add(Fn("get_line_styles", "Get available line styles"));
                d.Add(Fn("set_line_style", "Set line style on detail lines",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("lineStyleName", "string", "Line style name")));
                d.Add(Fn("get_phases", "Get project phases"));
                d.Add(Fn("get_materials", "Get project materials"));
                d.Add(Fn("cad_to_lines", "Convert CAD imports to Revit detail lines",
                    Prop("deleteAfter", "boolean", "Delete CAD import after conversion"),
                    Prop("importIds", "string", "CAD import IDs (comma-sep, or all)")));
            }

            // ===== DATA MANAGEMENT (9 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Data))
            {
                d.Add(Fn("save_project_data", "Save JSON data to persist between sessions",
                    PropReq("key", "string", "Data key (e.g. 'wall_specs', 'naming_rules')"),
                    PropReq("data", "string", "Data to save (JSON or text)")));
                d.Add(Fn("load_project_data", "Load previously saved project data",
                    PropReq("key", "string", "Data key")));
                d.Add(Fn("list_project_data", "List all saved data for this project"));
                d.Add(Fn("delete_project_data", "Delete saved data",
                    PropReq("key", "string", "Data key")));
                d.Add(Fn("save_snapshot", "Capture model state (element counts, warnings, rooms, etc.)"));
                d.Add(Fn("create_view_filter", "Create parameter-based view filter",
                    PropReq("category", "string", "Category"),
                    PropReq("parameterName", "string", "Parameter to filter by"),
                    Prop("filterName", "string", "Filter name"),
                    Prop("ruleType", "string", "equals/contains/greater/less"),
                    Prop("value", "string", "Rule value"),
                    Prop("applyToView", "boolean", "Apply to active view (default: true)")));
                d.Add(Fn("get_worksets", "List worksets in workshared project"));
                d.Add(Fn("get_areas", "List areas and area plans"));
                d.Add(Fn("get_design_options", "List design options"));
                d.Add(Fn("reassign_level", "Reassign elements to a different level",
                    PropArrayReq("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                    PropReq("targetLevel", "string", "Target level name"),
                    Prop("maintainOffset", "boolean", "Maintain elevation offset (default: true)")));
                d.Add(Fn("batch_modify_thickness", "Modify wall/floor type thickness",
                    PropReq("typeName", "string", "Type name to modify"),
                    PropReq("thickness", "number", "New thickness (ft)"),
                    Prop("category", "string", "Walls or Floors")));
                d.Add(Fn("copy_from_linked", "Copy elements from linked model",
                    PropReq("category", "string", "Category to copy"),
                    Prop("linkName", "string", "Link name filter")));
                d.Add(Fn("snap_beams_to_columns", "Snap beam endpoints to column centerlines",
                    Prop("tolerance", "number", "Snap tolerance in feet (default: 2)")));
                d.Add(Fn("category_to_workset", "Move category elements to workset",
                    PropArrayReq("mappings", "Category-workset pairs", new JObject {
                        ["type"] = "object",
                        ["properties"] = new JObject {
                            ["category"] = new JObject { ["type"] = "string" },
                            ["worksetName"] = new JObject { ["type"] = "string" }
                        }
                    })));
            }

            // ===== CODE EXECUTION (always available) =====
            d.Add(Fn("execute_code", "Execute custom C# code against the Revit API when no built-in tool can handle the task. Code has access to __doc__ (Document), __uidoc__ (UIDocument), __uiapp__ (UIApplication). All Revit namespaces pre-imported. Code runs inside a transaction. Return a value to send results back.",
                PropReq("code", "string", "C# code body to execute. Use __doc__, __uidoc__, __uiapp__ for Revit access. Must return a value."),
                Prop("description", "string", "Brief description of what the code does")));
            // Hard cap: limit to 20 tools max to prevent empty responses from API
            const int MaxTools = 20;
            if (d.Count > MaxTools)
            {
                // Keep execute_code (last one) + first N-1 tools
                var execCode = d[d.Count - 1];
                while (d.Count > MaxTools - 1) d.RemoveAt(d.Count - 1);
                d.Add(execCode);
            }

            return d;
        }

        // ============ OLLAMA: Expanded tool set for local models ============
        private JArray GetOllamaFunctionDeclarations(HashSet<ToolGroup> groups = null)
        {
            var d = new JArray();

            // ===== READING (10 core tools — always included) =====
            d.Add(Fn("get_current_view_info", "Get active view info"));
            d.Add(Fn("get_current_view_elements", "Elements in active view",
                Prop("category", "string", "Category filter")));
            d.Add(Fn("get_elements", "Query elements by category",
                Prop("category", "string", "Category name like Walls, Doors, Columns, Floors")));
            d.Add(Fn("get_selected_elements", "Get currently selected elements"));
            d.Add(Fn("get_parameters", "Get element parameters",
                PropReq("elementId", "integer", "Element ID")));
            d.Add(Fn("get_project_info", "Get project info"));
            d.Add(Fn("get_views", "List views",
                Prop("type", "string", "Type filter")));
            d.Add(Fn("get_sheets", "List sheets"));
            d.Add(Fn("get_levels", "List all levels"));
            d.Add(Fn("get_rooms", "List rooms"));
            d.Add(Fn("get_warnings", "List model warnings"));

            // ===== CREATING (4 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Creating))
            {
                d.Add(Fn("create_wall", "Create a wall",
                    PropReq("startX", "number", "Start X in feet"),
                    PropReq("startY", "number", "Start Y in feet"),
                    PropReq("endX", "number", "End X in feet"),
                    PropReq("endY", "number", "End Y in feet"),
                    PropReq("levelName", "string", "Level name")));
                d.Add(Fn("create_room", "Create room",
                    PropReq("x", "number", "X (ft)"),
                    PropReq("y", "number", "Y (ft)"),
                    PropReq("levelName", "string", "Level")));
                d.Add(Fn("create_schedule", "Create a schedule view",
                    PropReq("category", "string", "Category like Structural Columns, Walls, Doors"),
                    Prop("name", "string", "Schedule name")));
                d.Add(Fn("create_sheet", "Create a sheet",
                    Prop("sheetNumber", "string", "Sheet number"),
                    Prop("sheetName", "string", "Sheet name")));
            }

            // ===== EDITING (7 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Editing))
            {
                d.Add(Fn("modify_element", "Modify element parameters",
                    PropReq("elementId", "integer", "Element ID"),
                    PropArrayReq("modifications", "Parameter changes", new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["parameterName"] = new JObject { ["type"] = "string" },
                            ["value"] = new JObject { ["type"] = "string" }
                        }
                    })));
                d.Add(Fn("move_element", "Move element",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("deltaX", "number", "Delta X (ft)"),
                    PropReq("deltaY", "number", "Delta Y (ft)")));
                d.Add(Fn("delete_elements", "Delete elements",
                    PropArrayReq("elementIds", "Element IDs to delete", new JObject { ["type"] = "integer" })));
                d.Add(Fn("select_elements", "Select elements by IDs",
                    PropArrayReq("elementIds", "Element IDs to select", new JObject { ["type"] = "integer" })));
                d.Add(Fn("batch_set_parameter", "Set parameter on matching elements",
                    PropReq("category", "string", "Category"),
                    PropReq("parameterName", "string", "Parameter"),
                    PropReq("value", "string", "Value")));
                d.Add(Fn("change_type", "Change element type",
                    PropReq("elementId", "integer", "Element ID"),
                    PropReq("newTypeName", "string", "New type name")));
                d.Add(Fn("select_by_filter", "Select elements by filter",
                    Prop("category", "string", "Category"),
                    Prop("levelName", "string", "Level")));
            }

            // ===== EXPORT (2 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Export))
            {
                d.Add(Fn("export_to_pdf", "Export to PDF",
                    Prop("sheetIds", "string", "Sheet IDs (comma-sep)")));
                d.Add(Fn("export_schedule_data", "Export schedule to CSV",
                    Prop("scheduleName", "string", "Schedule name")));
            }

            // ===== QA (2 tools) =====
            if (groups == null || groups.Contains(ToolGroup.QA))
            {
                d.Add(Fn("audit_model", "Audit model"));
                d.Add(Fn("purge_unused", "Purge unused families/types"));
            }

            // ===== DATA (3 tools) =====
            if (groups == null || groups.Contains(ToolGroup.Data))
            {
                d.Add(Fn("save_project_data", "Save data to persist between sessions",
                    PropReq("key", "string", "Data key"),
                    PropReq("data", "string", "Data to save")));
                d.Add(Fn("load_project_data", "Load saved project data",
                    PropReq("key", "string", "Data key")));
                d.Add(Fn("list_project_data", "List all saved data"));
            }

            // ===== CODE EXECUTION (always available) =====
            d.Add(Fn("execute_code", "Execute custom C# code against the Revit API when no tool fits. Use __doc__, __uidoc__, __uiapp__ for Revit access. Return a value.",
                PropReq("code", "string", "C# code body to execute"),
                Prop("description", "string", "What the code does")));

            return d;
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
