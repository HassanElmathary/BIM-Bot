using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Executes Gemini CLI commands as hidden background processes.
    /// Auto-falls back between models on rate limits (429).
    /// Compresses prompts to minimize token usage.
    /// </summary>
    public class GeminiCliClient
    {
        private readonly GeminiCliSettings _settings;
        private readonly GeminiCliManager _manager;
        private const int DefaultTimeoutMs = 120_000; // 2 minutes

        // Model fallback chain — ordered by free tier generosity
        private static readonly string[] ModelFallbackChain = new[]
        {
            "gemini-2.0-flash",       // Highest free tier limit
            "gemini-2.0-flash-lite",  // Even lighter / higher limits
            "gemini-1.5-flash",       // Older but generous limits
        };

        /// <summary>Fires when falling back to another model.</summary>
        public event Action<string> OnModelFallback;

        public GeminiCliClient(GeminiCliSettings settings, GeminiCliManager manager)
        {
            _settings = settings;
            _manager = manager;
        }

        /// <summary>
        /// Send a prompt to the Gemini CLI and return the response text.
        /// Auto-retries with fallback models on 429 rate limits.
        /// </summary>
        public async Task<GeminiCliResult> AskAsync(string prompt, CancellationToken ct = default)
        {
            if (!_settings.IsConfigured)
            {
                return new GeminiCliResult
                {
                    Text = "⚠️ Gemini API key not configured.\nClick the ⚙️ button to add your API key.",
                    IsError = true
                };
            }

            // Compress prompt to reduce tokens
            var compressedPrompt = CompressPrompt(prompt);

            // Try each model in the fallback chain
            for (int i = 0; i < ModelFallbackChain.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var model = ModelFallbackChain[i];
                var result = await RunCliAsync(compressedPrompt, model, ct);

                // If rate-limited and we have more models to try, fallback
                if (result.IsRateLimited && i < ModelFallbackChain.Length - 1)
                {
                    var nextModel = ModelFallbackChain[i + 1];
                    OnModelFallback?.Invoke(nextModel);
                    continue;
                }

                return result;
            }

            // Should never reach here, but safety fallback
            return new GeminiCliResult
            {
                Text = "❌ All models are rate-limited. Please wait a minute and try again.",
                IsError = true
            };
        }

        /// <summary>Run a single CLI invocation with a specific model.</summary>
        private async Task<GeminiCliResult> RunCliAsync(string prompt, string model, CancellationToken ct)
        {
            var (command, baseArgs) = _manager.GetCliInvocation(_settings);

            var escapedPrompt = prompt.Replace("\"", "\\\"");
            var modelFlag = $"--model {model}";
            var fullArgs = string.IsNullOrEmpty(baseArgs)
                ? $"ask {modelFlag} \"{escapedPrompt}\""
                : $"{baseArgs} ask {modelFlag} \"{escapedPrompt}\"";

            // Route through cmd.exe /c so PATH is resolved properly
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command} {fullArgs}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            // Security note: The API key is set on the child process's environment only
            // (not the user/system environment). It is NOT visible via `set` or `echo %GEMINI_API_KEY%`
            // in parent shells. Inspecting another process's environment requires admin privileges
            // on Windows (via PEB access). This is the standard approach for CLI tools.
            psi.EnvironmentVariables["GEMINI_API_KEY"] = _settings.GeminiApiKey;

            if (!string.IsNullOrWhiteSpace(_settings.NodePath))
            {
                var nodePath = System.IO.Path.GetDirectoryName(_settings.NodePath);
                if (!string.IsNullOrEmpty(nodePath))
                {
                    var existingPath = psi.EnvironmentVariables["PATH"] ?? "";
                    psi.EnvironmentVariables["PATH"] = nodePath + ";" + existingPath;
                }
            }

            try
            {
                using var proc = new Process { StartInfo = psi };
                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) stderrBuilder.AppendLine(e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using var timeoutCts = new CancellationTokenSource(DefaultTimeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    await Task.Run(() =>
                    {
                        while (!proc.HasExited)
                        {
                            linkedCts.Token.ThrowIfCancellationRequested();
                            proc.WaitForExit(500);
                        }
                    }, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(); } catch { }

                    if (ct.IsCancellationRequested)
                        throw;

                    return new GeminiCliResult
                    {
                        Text = "⏱️ Timed out after 2 minutes. Try a simpler prompt.",
                        IsError = true
                    };
                }

                var stdout = stdoutBuilder.ToString().Trim();
                var stderr = stderrBuilder.ToString().Trim();
                var combined = stdout + "\n" + stderr;

                // Detect rate limiting (429)
                bool isRateLimited = combined.Contains("429") &&
                    (combined.Contains("quota") || combined.Contains("rate") || combined.Contains("exceeded"));

                if (isRateLimited)
                {
                    return new GeminiCliResult
                    {
                        Text = $"Rate limited on {model}",
                        IsError = true,
                        IsRateLimited = true
                    };
                }

                if (proc.ExitCode != 0)
                {
                    var errorMsg = !string.IsNullOrEmpty(stderr) ? stderr : stdout;
                    return new GeminiCliResult
                    {
                        Text = $"❌ CLI error (exit {proc.ExitCode}):\n{errorMsg}",
                        IsError = true
                    };
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    return new GeminiCliResult
                    {
                        Text = !string.IsNullOrEmpty(stderr)
                            ? $"⚠️ No output.\nStderr: {stderr}"
                            : "⚠️ No output from Gemini CLI.",
                        IsError = true
                    };
                }

                return new GeminiCliResult
                {
                    Text = stdout,
                    IsError = false,
                    ModelUsed = model
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new GeminiCliResult
                {
                    Text = $"❌ Failed to launch Gemini CLI:\n{ex.Message}\n\n" +
                           "Make sure Node.js and the Gemini CLI are installed.\n" +
                           "Run: npm install -g @google/gemini-cli",
                    IsError = true
                };
            }
        }

        /// <summary>
        /// Compresses a prompt to minimize token usage:
        /// - Collapses multiple spaces/newlines
        /// - Trims unnecessary whitespace
        /// - Keeps it under a reasonable size
        /// </summary>
        private static string CompressPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return prompt;

            // Collapse multiple blank lines into one
            var compressed = Regex.Replace(prompt, @"\n{3,}", "\n\n");

            // Collapse multiple spaces into one (but not in code blocks)
            compressed = Regex.Replace(compressed, @"[ \t]{2,}", " ");

            // Trim each line
            var lines = compressed.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].Trim();
            compressed = string.Join("\n", lines);

            // Cap at 4000 chars to avoid massive token usage
            if (compressed.Length > 4000)
                compressed = compressed.Substring(0, 4000) + "\n...(truncated)";

            return compressed.Trim();
        }
    }

    /// <summary>Result from a Gemini CLI invocation.</summary>
    public class GeminiCliResult
    {
        public string Text { get; set; } = "";
        public bool IsError { get; set; }
        public bool IsRateLimited { get; set; }
        public string ModelUsed { get; set; } = "";
    }
}

