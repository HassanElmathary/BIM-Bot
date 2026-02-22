using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.AI;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Settings dialog for AI provider, API key, and model selection — uses DarkTheme.
    /// </summary>
    public class SettingsWindow : Window
    {
        public GeminiSettings ResultSettings { get; private set; }

        private readonly TextBox _apiKeyBox;
        private readonly ComboBox _modelCombo;
        private readonly ComboBox _providerCombo;

        // Model lists per provider
        private static readonly string[] GeminiModels = { "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash", "gemini-2.0-pro" };
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
                new[] { "Gemini", "DeepSeek", "Perplexity", "OpenRouter" },
                MatchProvider(currentSettings.Provider ?? "gemini")
            );
            _providerCombo.Margin = new Thickness(0, 0, 0, 16);
            _providerCombo.SelectionChanged += OnProviderChanged;
            stack.Children.Add(_providerCombo);

            // --- API Key ---
            stack.Children.Add(DarkTheme.MakeLabel("API Key"));

            _apiKeyBox = DarkTheme.MakeTextBox(currentSettings.ApiKey, "Paste your API key here...");
            _apiKeyBox.FontFamily = new FontFamily("Consolas");
            _apiKeyBox.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(_apiKeyBox);

            // --- Model ---
            stack.Children.Add(DarkTheme.MakeLabel("Model"));

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
                else
                    url = "https://aistudio.google.com/apikey";
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            };
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
                default: return "Gemini";
            }
        }

        private string GetSelectedProvider()
        {
            var sel = GetComboText(_providerCombo) ?? "Gemini";
            return sel.ToLowerInvariant();
        }

        private string GetSelectedModel()
        {
            return GetComboText(_modelCombo) ?? "gemini-2.5-flash";
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
    }
}
