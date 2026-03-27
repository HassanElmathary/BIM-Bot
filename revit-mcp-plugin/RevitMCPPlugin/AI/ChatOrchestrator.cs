using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPPlugin.Core;

namespace RevitMCPPlugin.AI
{
    /// <summary>
    /// Orchestrates the conversation flow between the Chat UI, Gemini API, and Revit.
    /// 
    /// Flow:
    /// 1. User types message → SendMessageAsync()
    /// 2. Gemini responds with text or function_call
    /// 3. If function_call → execute via CommandExecutor → send result back to Gemini
    /// 4. Repeat until Gemini returns final text
    /// 5. Save conversation to JSON → Return text to UI
    /// 
    /// Chat history is persisted per-project in the ProjectData folder.
    /// </summary>
    public class ChatOrchestrator
    {
        private readonly GeminiClient _gemini;
        private const int MaxToolCalls = 30; // Prevent infinite loops (raised for complex tasks)
        private const int MaxSameToolCalls = 10; // Max times same tool can be called in one turn
        private string _projectPath;
        private readonly List<ChatLogEntry> _chatLog = new List<ChatLogEntry>();

        /// <summary>Raised when the orchestrator status changes (for UI status bar).</summary>
        public event Action<string>? OnStatusChanged;

        /// <summary>Raised when a tool is being executed (for UI progress feedback).</summary>
        public event Action<string, JObject>? OnToolExecuting;

        /// <summary>Raised when a tool completes (for UI result feedback).</summary>
        public event Action<string, JToken>? OnToolCompleted;

        public GeminiClient Gemini => _gemini;

        public ChatOrchestrator()
        {
            _gemini = new GeminiClient();
        }

        /// <summary>
        /// Set the current project path for chat history storage.
        /// Call this whenever the active document changes.
        /// </summary>
        public void SetProjectPath(string projectPath)
        {
            // Auto-detect if null
            if (string.IsNullOrEmpty(projectPath))
            {
                try
                {
                    var app = Application.ActiveUIApp;
                    if (app?.ActiveUIDocument?.Document != null)
                        projectPath = app.ActiveUIDocument.Document.PathName ?? "Untitled";
                    else
                        projectPath = "Untitled";
                }
                catch { projectPath = "Untitled"; }
            }

            // Only reload if project actually changed
            if (_projectPath == projectPath) return;

            // Save current history before switching projects
            if (!string.IsNullOrEmpty(_projectPath) && _chatLog.Count > 0)
                SaveChatHistory();

            _projectPath = projectPath;
            _chatLog.Clear();
            _gemini.ClearHistory();

            // Load history for the new project
            LoadChatHistory();
        }

        /// <summary>
        /// Get the current project path (auto-detects from Revit if not set).
        /// </summary>
        private string GetProjectPath()
        {
            if (string.IsNullOrEmpty(_projectPath))
            {
                // Try to get the project path from Revit via the Application instance
                try
                {
                    var app = Application.ActiveUIApp;
                    if (app?.ActiveUIDocument?.Document != null)
                        _projectPath = app.ActiveUIDocument.Document.PathName ?? "Untitled";
                    else
                        _projectPath = "Untitled";
                }
                catch { _projectPath = "Untitled"; }
            }
            return _projectPath;
        }

        /// <summary>
        /// Send a user message and get the final AI response.
        /// This handles the full function-calling loop automatically.
        /// </summary>
        public async Task<ChatResult> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            if (!_gemini.IsConfigured)
            {
                return new ChatResult
                {
                    Text = "⚠️ Gemini API key not configured.\n\nClick the ⚙️ button to add your API key from:\nhttps://aistudio.google.com/apikey",
                    IsError = true
                };
            }

            // Ensure project path is set
            GetProjectPath();

            try
            {
                OnStatusChanged?.Invoke("Thinking...");

                // Log user message
                var userEntry = new ChatLogEntry
                {
                    Role = "user",
                    Content = userMessage,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                _chatLog.Add(userEntry);

                cancellationToken.ThrowIfCancellationRequested();
                var response = await _gemini.SendMessageAsync(userMessage);
                int toolCallCount = 0;

                // Loop detection: track consecutive identical tool calls
                string lastToolSignature = null;
                int consecutiveRepeats = 0;
                const int MaxConsecutiveRepeats = 3;

                // Loop detection: track total calls per tool name (catches varied-args loops)
                var toolCallCounts = new Dictionary<string, int>();

                // Function calling loop — Gemini may request multiple tool calls
                while (response.IsFunctionCall && toolCallCount < MaxToolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    toolCallCount++;
                    var fc = response.FunctionCall!;

                    // Track per-tool call count
                    if (!toolCallCounts.ContainsKey(fc.Name))
                        toolCallCounts[fc.Name] = 0;
                    toolCallCounts[fc.Name]++;

                    // Detect repeated identical calls (same tool + same args)
                    var currentSignature = $"{fc.Name}:{fc.Arguments?.ToString(Formatting.None) ?? ""}";
                    if (currentSignature == lastToolSignature)
                    {
                        consecutiveRepeats++;
                        if (consecutiveRepeats >= MaxConsecutiveRepeats)
                        {
                            // Break the loop — send a corrective message to the AI
                            var loopMsg = new JObject
                            {
                                ["error"] = $"Loop detected: '{fc.Name}' was called {MaxConsecutiveRepeats + 1} times in a row with the same arguments. Stop repeating this call. Summarize what you've done and respond to the user."
                            };
                            _chatLog.Add(new ChatLogEntry
                            {
                                Role = "tool_result",
                                ToolName = fc.Name,
                                Content = "Loop detected — stopping repeated calls.",
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                            OnStatusChanged?.Invoke("Loop detected, recovering...");
                            response = await _gemini.SendFunctionResultAsync(fc.Name, loopMsg);
                            break;
                        }
                    }
                    else
                    {
                        consecutiveRepeats = 0;
                    }
                    lastToolSignature = currentSignature;

                    // Detect same tool called too many times total (even with different args)
                    if (toolCallCounts[fc.Name] >= MaxSameToolCalls)
                    {
                        var loopMsg = new JObject
                        {
                            ["error"] = $"Loop detected: '{fc.Name}' has been called {toolCallCounts[fc.Name]} times this turn. This is too many. Stop calling this tool. Summarize what you've done and give a final answer to the user."
                        };
                        _chatLog.Add(new ChatLogEntry
                        {
                            Role = "tool_result",
                            ToolName = fc.Name,
                            Content = $"Loop detected — '{fc.Name}' called {toolCallCounts[fc.Name]} times, stopping.",
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                        OnStatusChanged?.Invoke("Loop detected, recovering...");
                        response = await _gemini.SendFunctionResultAsync(fc.Name, loopMsg);
                        break;
                    }

                    OnStatusChanged?.Invoke($"Executing: {fc.Name}...");
                    OnToolExecuting?.Invoke(fc.Name, fc.Arguments);

                    // Log tool call
                    _chatLog.Add(new ChatLogEntry
                    {
                        Role = "tool_call",
                        ToolName = fc.Name,
                        ToolArgs = fc.Arguments,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });

                    JToken toolResult;
                    try
                    {
                        // Execute the tool via Revit's external event mechanism
                        var eventManager = Application.EventManagerInstance;
                        if (eventManager == null)
                            throw new InvalidOperationException("MCP Service not started. Click 'Start MCP' first.");

                        toolResult = await eventManager.ExecuteCommandAsync(fc.Name, fc.Arguments);
                    }
                    catch (Exception ex)
                    {
                        // Send error back to Gemini so it can inform the user
                        toolResult = new JObject
                        {
                            ["error"] = ex.Message
                        };
                    }

                    // Log tool result
                    _chatLog.Add(new ChatLogEntry
                    {
                        Role = "tool_result",
                        ToolName = fc.Name,
                        Content = TruncateForLog(toolResult),
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });

                    OnToolCompleted?.Invoke(fc.Name, toolResult);

                    // Send tool result back to Gemini
                    cancellationToken.ThrowIfCancellationRequested();
                    OnStatusChanged?.Invoke("Processing results...");
                    response = await _gemini.SendFunctionResultAsync(fc.Name, toolResult);
                }

                var responseText = response.Text ?? "";

                if (toolCallCount >= MaxToolCalls)
                {
                    responseText += "\n\n⚠️ _Stopped after maximum tool calls reached._";
                }

                // Log AI response
                _chatLog.Add(new ChatLogEntry
                {
                    Role = "assistant",
                    Content = responseText,
                    ToolCallCount = toolCallCount,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                // Auto-save chat history after each complete turn
                SaveChatHistory();

                OnStatusChanged?.Invoke("Ready");

                return new ChatResult
                {
                    Text = responseText,
                    ToolCallCount = toolCallCount
                };
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke("Error");

                // Log the error
                _chatLog.Add(new ChatLogEntry
                {
                    Role = "error",
                    Content = ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                SaveChatHistory();

                return new ChatResult
                {
                    Text = $"❌ Error: {ex.Message}",
                    IsError = true
                };
            }
        }

        public void ClearHistory()
        {
            _gemini.ClearHistory();
            _chatLog.Clear();
            SaveChatHistory(); // Save the cleared state
        }

        /// <summary>Get the chat log for display/review.</summary>
        public List<ChatLogEntry> GetChatLog() => new List<ChatLogEntry>(_chatLog);

        // ===== Chat History Persistence =====

        private void SaveChatHistory()
        {
            try
            {
                var projectPath = GetProjectPath();
                var folder = ProjectDataService.GetProjectFolder(projectPath);
                var historyFile = Path.Combine(folder, "_chat_history.json");

                var data = new JObject
                {
                    ["projectPath"] = projectPath,
                    ["lastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["messageCount"] = _chatLog.Count,
                    ["chatLog"] = JArray.FromObject(_chatLog.Skip(Math.Max(0, _chatLog.Count - 100)).ToList()), // Keep last 100 entries
                    ["geminiHistory"] = JArray.FromObject(_gemini.GetHistory())  // Save raw Gemini history for continuity
                };

                File.WriteAllText(historyFile, data.ToString(Formatting.Indented));
            }
            catch { /* Don't crash the app if saving fails */ }
        }

        private void LoadChatHistory()
        {
            try
            {
                var projectPath = GetProjectPath();
                var folder = ProjectDataService.GetProjectFolder(projectPath);
                var historyFile = Path.Combine(folder, "_chat_history.json");

                if (!File.Exists(historyFile)) return;

                var content = File.ReadAllText(historyFile);
                var data = JObject.Parse(content);

                // Restore chat log
                var logArray = data["chatLog"] as JArray;
                if (logArray != null)
                {
                    _chatLog.Clear();
                    foreach (var entry in logArray)
                    {
                        _chatLog.Add(new ChatLogEntry
                        {
                            Role = entry["Role"]?.ToString() ?? "",
                            Content = entry["Content"]?.ToString() ?? "",
                            ToolName = entry["ToolName"]?.ToString(),
                            ToolArgs = entry["ToolArgs"] as JObject,
                            ToolCallCount = entry["ToolCallCount"]?.Value<int>() ?? 0,
                            Timestamp = entry["Timestamp"]?.ToString() ?? ""
                        });
                    }
                }

                // Restore Gemini conversation history for AI continuity
                var geminiHistory = data["geminiHistory"] as JArray;
                if (geminiHistory != null && geminiHistory.Count > 0)
                {
                    var historyList = geminiHistory.Select(j => (JObject)j).ToList();
                    _gemini.SetHistory(historyList);
                }
            }
            catch { /* Don't crash if loading fails */ }
        }

        private string TruncateForLog(JToken result)
        {
            var str = result?.ToString(Formatting.None) ?? "";
            return str.Length > 500 ? str.Substring(0, 500) + "..." : str;
        }
    }

    public class ChatLogEntry
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public string? ToolName { get; set; }
        public JObject? ToolArgs { get; set; }
        public int ToolCallCount { get; set; }
        public string Timestamp { get; set; } = "";
    }

    public class ChatResult
    {
        public string Text { get; set; } = "";
        public bool IsError { get; set; }
        public int ToolCallCount { get; set; }
    }
}
