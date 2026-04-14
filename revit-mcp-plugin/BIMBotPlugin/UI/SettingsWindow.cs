using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.AI;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// Settings dialog for AI provider, API key, and model selection — uses DarkTheme.
    /// </summary>
    public class SettingsWindow : Window
    {
        public GeminiSettings ResultSettings { get; private set; }

        private readonly TextBox _apiKeyBox;
        private readonly TextBlock _apiKeyLabel;
        private readonly TextBlock _modelLabel;

        private readonly ComboBox _modelCombo;
        private readonly ComboBox _providerCombo;
        private Button _getKeyBtn;

        // Model lists per provider
        private static readonly string[] GeminiModels = { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-pro" };
        private static readonly string[] DeepSeekModels = { "deepseek-chat", "deepseek-reasoner" };
        private static readonly string[] PerplexityModels = { "sonar", "sonar-pro", "sonar-reasoning", "sonar-reasoning-pro" };
        private static readonly string[] OpenRouterModels = {
            "openai/gpt-4o", "openai/gpt-4o-mini",
            "anthropic/claude-sonnet-4", "anthropic/claude-3.5-haiku",
            "google/gemini-2.5-flash", "google/gemini-2.5-pro",
            "deepseek/deepseek-chat", "deepseek/deepseek-reasoner",
            "meta-llama/llama-4-maverick",
            "qwen/qwen3-235b-a22b"
        };
        private static readonly string[] OllamaModels = { "qwen2.5:7b", "qwen2.5:14b", "llama3.1:8b", "mistral:7b", "codellama:7b" };
        private static readonly string[] CerebrasModels = { "llama-3.3-70b", "llama3.1-8b", "llama3.1-70b" };
        private static readonly string[] GroqModels = { "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it", "qwen-qwq-32b" };
        private static readonly string[] OpenAIModels = { "gpt-4o", "gpt-4o-mini", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "o3-mini" };

        public SettingsWindow(GeminiSettings currentSettings)
        {
            ResultSettings = currentSettings;

            Title = "AI Settings";
            Width = 440;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            var stack = new StackPanel { Margin = new Thickness(24) };

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = "⚙️ AI Provider Settings",
                FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // --- Provider selector ---
            stack.Children.Add(DarkTheme.MakeLabel("Provider"));

            _providerCombo = DarkTheme.MakeComboBox(
                new[] { "Gemini", "OpenAI", "DeepSeek", "Perplexity", "OpenRouter", "Cerebras", "Groq", "Ollama (Local)" },
                MatchProvider(currentSettings.Provider ?? "gemini")
            );
            _providerCombo.Margin = new Thickness(0, 0, 0, 16);
            _providerCombo.SelectionChanged += OnProviderChanged;
            stack.Children.Add(_providerCombo);

            // --- API Key ---
            _apiKeyLabel = DarkTheme.MakeLabel("API Key");
            stack.Children.Add(_apiKeyLabel);

            _apiKeyBox = DarkTheme.MakeTextBox(currentSettings.ApiKey, "Paste your API key here...");
            _apiKeyBox.FontFamily = new FontFamily("Consolas");
            _apiKeyBox.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(_apiKeyBox);

            // --- Model ---
            _modelLabel = DarkTheme.MakeLabel("Model");
            stack.Children.Add(_modelLabel);

            _modelCombo = DarkTheme.MakeComboBox(new string[0]);
            _modelCombo.Margin = new Thickness(0, 0, 0, 24);
            PopulateModels(currentSettings.Provider ?? "gemini", currentSettings.Model);
            stack.Children.Add(_modelCombo);



            // Buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var getKeyBtn = new Button
            {
                Content = "Get API Key ↗",
                Background = Brushes.Transparent,
                Foreground = DarkTheme.BgAccent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 8, 0)
            };
            getKeyBtn.Click += (s, e) =>
            {
                var provider = GetSelectedProvider();
                string url;
                if (provider == "deepseek")
                    url = "https://platform.deepseek.com/api_keys";
                else if (provider == "perplexity")
                    url = "https://www.perplexity.ai/settings/api";
                else if (provider == "openrouter")
                    url = "https://openrouter.ai/keys";
                else if (provider == "cerebras")
                    url = "https://cloud.cerebras.ai/";
                else if (provider == "groq")
                    url = "https://console.groq.com/keys";
                else if (provider == "openai")
                    url = "https://platform.openai.com/api-keys";
                else
                    url = "https://aistudio.google.com/apikey";
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            };
            _getKeyBtn = getKeyBtn;
            btnPanel.Children.Add(getKeyBtn);

            var cancelBtn = DarkTheme.MakeCancelButton();
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            var saveBtn = DarkTheme.MakePrimaryButton("Save");
            saveBtn.Margin = new Thickness(8, 0, 0, 0);
            saveBtn.Click += (s, e) =>
            {
                ResultSettings = new GeminiSettings
                {
                    ApiKey = _apiKeyBox.Text?.Trim() ?? "",
                    Model = GetSelectedModel(),
                    Provider = GetSelectedProvider()
                };
                DialogResult = true;
                Close();
            };
            btnPanel.Children.Add(saveBtn);

            stack.Children.Add(btnPanel);
            Content = stack;
        }

        private string MatchProvider(string provider)
        {
            if (string.IsNullOrEmpty(provider)) return "Gemini";
            switch (provider.ToLowerInvariant())
            {

                case "deepseek": return "DeepSeek";
                case "perplexity": return "Perplexity";
                case "openrouter": return "OpenRouter";
                case "ollama": return "Ollama (Local)";
                case "cerebras": return "Cerebras";
                case "groq": return "Groq";
                case "openai": return "OpenAI";
                default: return "Gemini";
            }
        }

        private string GetSelectedProvider()
        {
            var sel = GetComboText(_providerCombo) ?? "Gemini";
            // Map display names back to storage keys
            if (sel.StartsWith("Ollama", StringComparison.OrdinalIgnoreCase)) return "ollama";
            return sel.ToLowerInvariant();
        }

        private string GetSelectedModel()
        {
            return GetComboText(_modelCombo) ?? "gemini-2.0-flash";
        }

        private string GetComboText(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem cbi)
                return cbi.Content?.ToString();
            if (combo.SelectedItem is string s)
                return s;
            return combo.Text;
        }

        private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var provider = GetSelectedProvider();
            PopulateModels(provider, null);
            var hideFields = provider == "ollama";
            _apiKeyBox.Visibility = hideFields ? Visibility.Collapsed : Visibility.Visible;
            _apiKeyLabel.Visibility = hideFields ? Visibility.Collapsed : Visibility.Visible;
            _modelCombo.Visibility = Visibility.Visible;
            _modelLabel.Visibility = Visibility.Visible;
            if (_getKeyBtn != null)
                _getKeyBtn.Visibility = hideFields ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PopulateModels(string provider, string selectedModel)
        {
            _modelCombo.Items.Clear();
            string[] models;
            switch (provider?.ToLowerInvariant())
            {
                case "deepseek": models = DeepSeekModels; break;
                case "perplexity": models = PerplexityModels; break;
                case "openrouter": models = OpenRouterModels; break;
                case "ollama": models = GetInstalledOllamaModels(); break;
                case "cerebras": models = CerebrasModels; break;
                case "groq": models = GroqModels; break;
                case "openai": models = OpenAIModels; break;
                default: models = GeminiModels; break;
            }

            foreach (var m in models)
            {
                var item = new ComboBoxItem
                {
                    Content = m,
                    Background = DarkTheme.BgCard,
                    Foreground = DarkTheme.FgWhite,
                    Padding = new Thickness(8, 6, 8, 6)
                };
                if (m == selectedModel) item.IsSelected = true;
                _modelCombo.Items.Add(item);
            }
            if (_modelCombo.SelectedIndex < 0) _modelCombo.SelectedIndex = 0;
        }

        private string[] GetInstalledOllamaModels()
        {
            var installed = new HashSet<string>();
            string ollamaPath = null;
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(localApp, "Programs", "Ollama", "ollama.exe");
            if (File.Exists(path)) ollamaPath = path;
            else
            {
                try
                {
                    var psiCheck = new ProcessStartInfo("ollama", "--version") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    var pCheck = Process.Start(psiCheck);
                    pCheck?.WaitForExit(2000);
                    if (pCheck?.ExitCode == 0) ollamaPath = "ollama";
                }
                catch { }
            }

            if (ollamaPath != null)
            {
                try
                {
                    var psi = new ProcessStartInfo(ollamaPath, "list")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var p = Process.Start(psi);
                    var output = p?.StandardOutput.ReadToEnd() ?? "";
                    p?.WaitForExit(3000);

                    foreach (var line in output.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("NAME")) continue;
                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && !installed.Contains(parts[0]))
                        {
                            installed.Add(parts[0]);
                        }
                    }
                }
                catch { }
            }

            var finalModels = new List<string>(installed);
            foreach (var m in OllamaModels)
            {
                if (!installed.Contains(m))
                {
                    finalModels.Add(m);
                }
            }
            return finalModels.ToArray();
        }
    }
}
