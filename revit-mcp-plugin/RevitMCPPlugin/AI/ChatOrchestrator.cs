using System;
using System.Threading.Tasks;
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
    /// 5. Return text to UI
    /// 
    /// Events are raised for UI updates (status changes, intermediate results).
    /// </summary>
    public class ChatOrchestrator
    {
        private readonly GeminiClient _gemini;
        private const int MaxToolCalls = 30; // Prevent infinite loops

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
        /// Send a user message and get the final AI response.
        /// This handles the full function-calling loop automatically.
        /// </summary>
        public async Task<ChatResult> SendMessageAsync(string userMessage)
        {
            if (!_gemini.IsConfigured)
            {
                return new ChatResult
                {
                    Text = "⚠️ Gemini API key not configured.\n\nClick the ⚙️ button to add your API key from:\nhttps://aistudio.google.com/apikey",
                    IsError = true
                };
            }

            try
            {
                OnStatusChanged?.Invoke("Thinking...");

                var response = await _gemini.SendMessageAsync(userMessage);
                int toolCallCount = 0;

                // Function calling loop — Gemini may request multiple tool calls
                while (response.IsFunctionCall && toolCallCount < MaxToolCalls)
                {
                    toolCallCount++;
                    var fc = response.FunctionCall!;

                    OnStatusChanged?.Invoke($"Executing: {fc.Name}...");
                    OnToolExecuting?.Invoke(fc.Name, fc.Arguments);

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

                    OnToolCompleted?.Invoke(fc.Name, toolResult);

                    // Send tool result back to Gemini
                    OnStatusChanged?.Invoke("Processing results...");
                    response = await _gemini.SendFunctionResultAsync(fc.Name, toolResult);
                }

                if (toolCallCount >= MaxToolCalls)
                {
                    return new ChatResult
                    {
                        Text = response.Text + "\n\n⚠️ _Stopped after maximum tool calls reached._",
                        ToolCallCount = toolCallCount
                    };
                }

                OnStatusChanged?.Invoke("Ready");

                return new ChatResult
                {
                    Text = response.Text ?? "",
                    ToolCallCount = toolCallCount
                };
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke("Error");
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
        }
    }

    public class ChatResult
    {
        public string Text { get; set; } = "";
        public bool IsError { get; set; }
        public int ToolCallCount { get; set; }
    }
}
