using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Orchestrates the Gemini CLI conversation flow.
    /// Much simpler than ChatOrchestrator because the CLI handles the
    /// tool-calling loop internally via MCP — we just send a prompt and get the final response.
    /// </summary>
    public class GeminiCliOrchestrator
    {
        private readonly GeminiCliClient _client;
        private readonly GeminiCliManager _manager;
        private readonly GeminiCliSettings _settings;
        private readonly List<ChatLogEntry> _chatLog = new List<ChatLogEntry>();
        private string _projectPath;

        /// <summary>Status change callback (e.g. "Gemini CLI thinking...")</summary>
        public event Action<string> OnStatusChanged;

        public GeminiCliSettings Settings => _settings;
        public GeminiCliManager Manager => _manager;

        public GeminiCliOrchestrator()
        {
            _settings = GeminiCliSettings.Load();
            _manager = new GeminiCliManager();
            _client = new GeminiCliClient(_settings, _manager);
            _client.OnModelFallback += model =>
                OnStatusChanged?.Invoke($"⚡ Rate limited — switching to {model}...");
        }

        /// <summary>
        /// Update settings and recreate the client.
        /// </summary>
        public void UpdateSettings(GeminiCliSettings newSettings)
        {
            // Copy values
            _settings.GeminiApiKey = newSettings.GeminiApiKey;
            _settings.UseGeminiCli = newSettings.UseGeminiCli;
            _settings.NodePath = newSettings.NodePath;
            _settings.GeminiCliPath = newSettings.GeminiCliPath;
            _settings.AutoInstallCli = newSettings.AutoInstallCli;
            _settings.McpServerPath = newSettings.McpServerPath;
            _settings.Save();
        }

        /// <summary>
        /// Set the current project path for chat history storage.
        /// </summary>
        public void SetProjectPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                _projectPath = path;
            }
            else
            {
                // Auto-detect from Revit
                try
                {
                    var app = BIMBotPlugin.Core.Application.ActiveUIApp;
                    var doc = app?.ActiveUIDocument?.Document;
                    if (doc != null && !string.IsNullOrEmpty(doc.PathName))
                        _projectPath = Path.GetDirectoryName(doc.PathName);
                }
                catch { }
            }

            // Load chat history if available
            if (!string.IsNullOrEmpty(_projectPath))
                LoadChatHistory();
        }

        /// <summary>
        /// Send a user message via the Gemini CLI and return the response.
        /// The CLI internally handles MCP tool calls — we just get the final text.
        /// </summary>
        public async Task<ChatResult> SendMessageAsync(string userMessage, CancellationToken ct = default)
        {
            if (!_settings.IsConfigured)
            {
                return new ChatResult
                {
                    Text = "⚠️ Gemini API key not configured.\n\nClick the ⚙️ button to set up your API key.",
                    IsError = true
                };
            }

            // Ensure prerequisite config is generated
            EnsureConfigGenerated();

            try
            {
                OnStatusChanged?.Invoke("⚡ Gemini CLI thinking...");

                // Log user message
                _chatLog.Add(new ChatLogEntry
                {
                    Role = "user",
                    Content = userMessage,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                ct.ThrowIfCancellationRequested();

                // Execute via CLI
                var result = await _client.AskAsync(userMessage, ct);

                // Log AI response
                _chatLog.Add(new ChatLogEntry
                {
                    Role = "assistant",
                    Content = result.Text,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                // Save history
                SaveChatHistory();

                OnStatusChanged?.Invoke(result.IsError ? "Error" : "Ready");

                return new ChatResult
                {
                    Text = result.Text,
                    IsError = result.IsError,
                    ToolCallCount = 0 // CLI handles tools internally, count unknown
                };
            }
            catch (OperationCanceledException)
            {
                OnStatusChanged?.Invoke("Stopped");
                throw;
            }
            catch (Exception ex)
            {
                var errorResult = new ChatResult
                {
                    Text = $"❌ Error: {ex.Message}",
                    IsError = true
                };

                _chatLog.Add(new ChatLogEntry
                {
                    Role = "error",
                    Content = ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                OnStatusChanged?.Invoke("Error");
                return errorResult;
            }
        }

        /// <summary>Clear chat history.</summary>
        public void ClearHistory()
        {
            _chatLog.Clear();
            SaveChatHistory();
        }

        /// <summary>Get the chat log for display/review.</summary>
        public List<ChatLogEntry> GetChatLog() => new List<ChatLogEntry>(_chatLog);

        // ── Config Generation ──

        private bool _configGenerated = false;

        private void EnsureConfigGenerated()
        {
            if (_configGenerated) return;
            _manager.GenerateCliConfig(_settings);
            _configGenerated = true;
        }

        // ── Chat History Persistence ──

        private void SaveChatHistory()
        {
            if (string.IsNullOrEmpty(_projectPath)) return;
            try
            {
                var dir = Path.Combine(_projectPath, "BIMBot_Data");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "gemini-cli-chat.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(_chatLog, Formatting.Indented));
            }
            catch { }
        }

        private void LoadChatHistory()
        {
            if (string.IsNullOrEmpty(_projectPath)) return;
            try
            {
                var path = Path.Combine(_projectPath, "BIMBot_Data", "gemini-cli-chat.json");
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<List<ChatLogEntry>>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        _chatLog.Clear();
                        _chatLog.AddRange(loaded);
                    }
                }
            }
            catch { }
        }
    }
}
