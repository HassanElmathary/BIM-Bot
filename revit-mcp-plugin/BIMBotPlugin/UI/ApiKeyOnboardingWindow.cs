using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.AI;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// One-time BYOK onboarding window for entering the Google AI Studio API key.
    /// Styled with the existing DarkTheme for consistency.
    /// </summary>
    public class ApiKeyOnboardingWindow : Window
    {
        private readonly TextBox _apiKeyBox;
        private readonly TextBlock _statusLabel;

        /// <summary>The settings result after the user saves.</summary>
        public GeminiCliSettings ResultSettings { get; private set; }

        public ApiKeyOnboardingWindow(GeminiCliSettings currentSettings)
        {
            ResultSettings = currentSettings;

            Title = "Gemini CLI Setup";
            Width = 480;
            Height = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            var stack = new StackPanel { Margin = new Thickness(28) };

            // ── Title ──
            stack.Children.Add(new TextBlock
            {
                Text = "⚡ Gemini CLI Setup",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Enter your free Google AI Studio API key to use the Gemini CLI.\n" +
                       "The CLI connects directly to your BIM-Bot tools for full model control.",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // ── Get Key Link ──
            var linkPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };
            linkPanel.Children.Add(new TextBlock
            {
                Text = "🔑 ",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });

            var linkText = new TextBlock
            {
                FontSize = 13,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var linkRun = new Run("Get your free API key from Google AI Studio ↗")
            {
                Foreground = DarkTheme.BgAccent,
                TextDecorations = TextDecorations.Underline
            };
            linkText.Inlines.Add(linkRun);
            linkText.MouseLeftButtonUp += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://aistudio.google.com/apikey")
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            linkText.MouseEnter += (s, e) => linkRun.Foreground = DarkTheme.BgAccentHover;
            linkText.MouseLeave += (s, e) => linkRun.Foreground = DarkTheme.BgAccent;
            linkPanel.Children.Add(linkText);
            stack.Children.Add(linkPanel);

            // ── API Key Input ──
            stack.Children.Add(DarkTheme.MakeLabel("Google AI Studio API Key", required: true));

            _apiKeyBox = DarkTheme.MakeTextBox(
                currentSettings.GeminiApiKey,
                "Paste your API key here...");
            _apiKeyBox.FontFamily = new FontFamily("Consolas");
            _apiKeyBox.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(_apiKeyBox);

            // ── Status Label ──
            _statusLabel = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_statusLabel);

            // ── Info Box ──
            var infoBox = DarkTheme.MakeGroupBox("ℹ️ How it works", new TextBlock
            {
                Text = "1. Your API key is stored locally in %APPDATA%\n" +
                       "2. The plugin checks for Node.js and Gemini CLI\n" +
                       "3. Prompts are sent via the Gemini CLI to your MCP tools\n" +
                       "4. Responses appear in the chat panel — no terminal needed",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            });
            stack.Children.Add(infoBox);

            // ── Buttons ──
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var cancelBtn = DarkTheme.MakeCancelButton();
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            var saveBtn = DarkTheme.MakePrimaryButton("Save & Continue");
            saveBtn.Margin = new Thickness(10, 0, 0, 0);
            saveBtn.Click += (s, e) =>
            {
                var key = GetApiKeyText();
                if (string.IsNullOrWhiteSpace(key))
                {
                    _statusLabel.Text = "⚠️ Please enter an API key.";
                    _statusLabel.Foreground = DarkTheme.FgGold;
                    return;
                }

                ResultSettings = new GeminiCliSettings
                {
                    GeminiApiKey = key,
                    UseGeminiCli = true,
                    NodePath = currentSettings.NodePath,
                    GeminiCliPath = currentSettings.GeminiCliPath,
                    AutoInstallCli = currentSettings.AutoInstallCli,
                    McpServerPath = currentSettings.McpServerPath
                };
                ResultSettings.Save();
                DialogResult = true;
                Close();
            };
            btnPanel.Children.Add(saveBtn);
            stack.Children.Add(btnPanel);

            Content = stack;

            Loaded += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(currentSettings.GeminiApiKey))
                    _statusLabel.Text = "No API key configured yet.";
                else
                    _statusLabel.Text = "✅ API key is configured. You can update it here.";
            };
        }

        /// <summary>Gets the API key text, accounting for the placeholder pattern.</summary>
        private string GetApiKeyText()
        {
            var text = _apiKeyBox.Text?.Trim();
            // DarkTheme.MakeTextBox uses FgDim color for placeholder text
            if (_apiKeyBox.Foreground == DarkTheme.FgDim)
                return "";
            return text ?? "";
        }
    }
}
