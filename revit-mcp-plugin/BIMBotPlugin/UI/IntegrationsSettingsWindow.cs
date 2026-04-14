using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BIMBotPlugin.AI;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// Premium dark-themed Integrations Settings window — on/off toggles + config for each service.
    /// </summary>
    public class IntegrationsSettingsWindow : Window
    {
        public IntegrationSettings ResultSettings { get; private set; }

        // Toggle states
        private Border _notionToggle, _sheetsToggle, _ollamaToggle, _excelToggle, _sqliteToggle;

        // Notion config
        private StackPanel _notionConfig;
        private TextBox _notionApiKeyBox, _notionDbIdBox;

        // Google Sheets config
        private StackPanel _sheetsConfig;
        private TextBox _sheetsCredPathBox, _sheetsSpreadsheetIdBox;

        // Ollama config
        private StackPanel _ollamaConfig;
        private TextBox _ollamaUrlBox;
        private ComboBox _ollamaModelCombo;

        // Status dots
        private TextBlock _excelStatus, _notionStatus, _sheetsStatus, _sqliteStatus, _ollamaStatus;

        private static readonly string[] OllamaModels = {
            "qwen2.5:7b-instruct-q4_K_M", "qwen2.5:7b", "qwen2.5:14b",
            "llama3.1:8b", "mistral:7b", "codellama:7b", "gemma2:9b"
        };

        public IntegrationsSettingsWindow(IntegrationSettings current)
        {
            ResultSettings = current ?? new IntegrationSettings();

            Title = "Integrations Settings";
            Width = 520;
            Height = 680;
            MinWidth = 460;
            MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            DarkTheme.Apply(this);

            BuildUI();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // ── Header ──
            var header = DarkTheme.MakeGradientHeader("🔗 Integrations", "Enable services and configure connections", titleSize: 22);
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // ── Scrollable Content ──
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 16, 20, 16)
            };

            var content = new StackPanel();

            // 1. Excel card
            content.Children.Add(BuildExcelCard());
            // 2. Notion card
            content.Children.Add(BuildNotionCard());
            // 3. Google Sheets card
            content.Children.Add(BuildGoogleSheetsCard());
            // 4. SQLite card
            content.Children.Add(BuildSqliteCard());
            // 5. Ollama card
            content.Children.Add(BuildOllamaCard());

            scroller.Content = content;
            Grid.SetRow(scroller, 1);
            mainGrid.Children.Add(scroller);

            // ── Footer ──
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancelBtn = DarkTheme.MakeCancelButton();
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            var saveBtn = DarkTheme.MakePrimaryButton("💾 Save");
            saveBtn.Margin = new Thickness(10, 0, 0, 0);
            saveBtn.Click += SaveBtn_Click;
            btnPanel.Children.Add(saveBtn);
            footer.Child = btnPanel;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
        }

        // ════════════════════════════════════════════
        // Card Builders
        // ════════════════════════════════════════════

        private Border BuildExcelCard()
        {
            _excelStatus = MakeStatusDot(true, true);
            _excelToggle = DarkTheme.MakeToggleSwitch(ResultSettings.ExcelEnabled, on =>
            {
                UpdateStatusDot(_excelStatus, on, true);
            });
            return BuildIntegrationCard("📊", "Excel", "Export Revit data to .xlsx files on your local machine",
                DarkTheme.CatExport, _excelToggle, _excelStatus, null);
        }

        private Border BuildNotionCard()
        {
            _notionConfig = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            _notionConfig.Visibility = ResultSettings.NotionEnabled ? Visibility.Visible : Visibility.Collapsed;

            _notionConfig.Children.Add(DarkTheme.MakeLabel("API Key", true));
            _notionApiKeyBox = DarkTheme.MakeTextBox(ResultSettings.NotionApiKey, "Paste your Notion integration token...");
            _notionApiKeyBox.FontFamily = new FontFamily("Consolas");
            _notionApiKeyBox.Margin = new Thickness(0, 0, 0, 8);
            _notionConfig.Children.Add(_notionApiKeyBox);

            _notionConfig.Children.Add(DarkTheme.MakeLabel("Default Database ID"));
            _notionDbIdBox = DarkTheme.MakeTextBox(ResultSettings.NotionDatabaseId, "32-char hex from database URL...");
            _notionDbIdBox.FontFamily = new FontFamily("Consolas");
            _notionDbIdBox.Margin = new Thickness(0, 0, 0, 8);
            _notionConfig.Children.Add(_notionDbIdBox);

            // Get key link
            var link = MakeHelpLink("🔑 Get Integration Token →", "https://www.notion.so/my-integrations");
            _notionConfig.Children.Add(link);

            _notionStatus = MakeStatusDot(ResultSettings.NotionEnabled, ResultSettings.IsNotionConfigured);
            _notionToggle = DarkTheme.MakeToggleSwitch(ResultSettings.NotionEnabled, on =>
            {
                _notionConfig.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                UpdateNotionStatus();
            });

            return BuildIntegrationCard("📝", "Notion", "Sync elements to a Notion database",
                DarkTheme.B(0xFF, 0xA7, 0x26), _notionToggle, _notionStatus, _notionConfig);
        }

        private Border BuildGoogleSheetsCard()
        {
            _sheetsConfig = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            _sheetsConfig.Visibility = ResultSettings.GoogleSheetsEnabled ? Visibility.Visible : Visibility.Collapsed;

            // ── Sign-in section ──
            var signInRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            var signInBtn = new Button
            {
                Content = ResultSettings.GoogleSignedIn ? "✅ Signed In" : "🔑 Sign in with Google",
                Background = ResultSettings.GoogleSignedIn ? DarkTheme.FgGreen : DarkTheme.BgAccent,
                Foreground = DarkTheme.FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 8, 16, 8),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            var signInBtnRef = signInBtn; // capture for lambda
            signInBtn.MouseEnter += (s, e) => signInBtnRef.Background = ResultSettings.GoogleSignedIn ? DarkTheme.B(0x66, 0xBB, 0x6A) : DarkTheme.BgAccentHover;
            signInBtn.Click += async (s, e) =>
            {
                // Check if credentials are configured
                if (!GoogleOAuthHelper.HasValidCredentials())
                {
                    var result = MessageBox.Show(
                        "Google OAuth credentials are not configured yet.\n\n" +
                        "You need to:\n" +
                        "1. Go to console.cloud.google.com/apis/credentials\n" +
                        "2. Create an OAuth 2.0 Client ID (Desktop app)\n" +
                        "3. Copy Client ID and Client Secret\n" +
                        "4. Paste them in the MCP server's .env file\n\n" +
                        "Open Google Cloud Console now?",
                        "Setup Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try { Process.Start(new ProcessStartInfo("https://console.cloud.google.com/apis/credentials") { UseShellExecute = true }); }
                        catch { }
                    }
                    return;
                }

                try
                {
                    signInBtnRef.Content = "⏳ Opening browser...";
                    signInBtnRef.IsEnabled = false;

                    var email = await GoogleOAuthHelper.SignInAsync();

                    signInBtnRef.Content = $"✅ {email}";
                    signInBtnRef.Background = DarkTheme.FgGreen;
                    signInBtnRef.IsEnabled = true;

                    // Update the settings reference
                    ResultSettings.GoogleSignedIn = true;
                    ResultSettings.GoogleEmail = email ?? "";
                    ResultSettings.GoogleSheetsEnabled = true;

                    UpdateSheetsStatus();

                    MessageBox.Show(
                        $"Successfully signed in as:\n{email}\n\n" +
                        "You can now export data to Google Sheets!",
                        "Google Sign-In",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    signInBtnRef.Content = "🔑 Sign in with Google";
                    signInBtnRef.Background = DarkTheme.BgAccent;
                    signInBtnRef.IsEnabled = true;

                    MessageBox.Show(
                        $"Sign-in failed:\n\n{ex.Message}",
                        "Google Sign-In Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
            signInRow.Children.Add(signInBtn);

            // Signed-in email display
            if (ResultSettings.GoogleSignedIn && !string.IsNullOrEmpty(ResultSettings.GoogleEmail))
            {
                signInRow.Children.Add(new TextBlock
                {
                    Text = $"  {ResultSettings.GoogleEmail}",
                    FontSize = 12,
                    Foreground = DarkTheme.FgDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            }

            _sheetsConfig.Children.Add(signInRow);

            // ── Separator ──
            _sheetsConfig.Children.Add(new Border
            {
                Height = 1,
                Background = DarkTheme.BorderDim,
                Margin = new Thickness(0, 2, 0, 10)
            });

            // ── Fallback: Service Account (collapsed by default if signed in) ──
            var fallbackLabel = DarkTheme.MakeLabel(ResultSettings.GoogleSignedIn ? "Or use Service Account (optional)" : "Service Account Credentials");
            fallbackLabel.Foreground = DarkTheme.FgDim;
            fallbackLabel.FontSize = 11;
            _sheetsConfig.Children.Add(fallbackLabel);

            _sheetsCredPathBox = DarkTheme.MakeTextBox(ResultSettings.GoogleSheetsCredentialsPath, "./credentials.json");
            _sheetsCredPathBox.FontFamily = new FontFamily("Consolas");
            _sheetsCredPathBox.Margin = new Thickness(0, 0, 0, 8);
            _sheetsConfig.Children.Add(_sheetsCredPathBox);

            _sheetsConfig.Children.Add(DarkTheme.MakeLabel("Default Spreadsheet ID"));
            _sheetsSpreadsheetIdBox = DarkTheme.MakeTextBox(ResultSettings.GoogleSheetsSpreadsheetId, "From spreadsheet URL...");
            _sheetsSpreadsheetIdBox.FontFamily = new FontFamily("Consolas");
            _sheetsSpreadsheetIdBox.Margin = new Thickness(0, 0, 0, 8);
            _sheetsConfig.Children.Add(_sheetsSpreadsheetIdBox);

            var helpText = new TextBlock
            {
                Text = "💡 Tip: Ask the AI \"list my Google spreadsheets\" to browse your files",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _sheetsConfig.Children.Add(helpText);

            _sheetsStatus = MakeStatusDot(ResultSettings.GoogleSheetsEnabled, ResultSettings.IsGoogleSheetsConfigured);
            _sheetsToggle = DarkTheme.MakeToggleSwitch(ResultSettings.GoogleSheetsEnabled, on =>
            {
                _sheetsConfig.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                UpdateSheetsStatus();
            });

            return BuildIntegrationCard("📗", "Google Sheets", "Push data to Google Sheets — sign in with your Google account",
                DarkTheme.B(0x34, 0xA8, 0x53), _sheetsToggle, _sheetsStatus, _sheetsConfig);
        }

        private Border BuildSqliteCard()
        {
            _sqliteStatus = MakeStatusDot(true, true);
            _sqliteToggle = DarkTheme.MakeToggleSwitch(ResultSettings.SqliteEnabled, on =>
            {
                UpdateStatusDot(_sqliteStatus, on, true);
            });
            return BuildIntegrationCard("🗄️", "SQLite", "Save local snapshots for delta-sync and comparison",
                DarkTheme.CatQuickView, _sqliteToggle, _sqliteStatus, null);
        }

        private Border BuildOllamaCard()
        {
            _ollamaConfig = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            _ollamaConfig.Visibility = ResultSettings.OllamaEnabled ? Visibility.Visible : Visibility.Collapsed;

            _ollamaConfig.Children.Add(DarkTheme.MakeLabel("Server URL", true));
            _ollamaUrlBox = DarkTheme.MakeTextBox(ResultSettings.OllamaUrl, "http://localhost:11434");
            _ollamaUrlBox.FontFamily = new FontFamily("Consolas");
            _ollamaUrlBox.Margin = new Thickness(0, 0, 0, 8);
            _ollamaConfig.Children.Add(_ollamaUrlBox);

            _ollamaConfig.Children.Add(DarkTheme.MakeLabel("Model"));
            _ollamaModelCombo = DarkTheme.MakeComboBox(OllamaModels, ResultSettings.OllamaModel);
            _ollamaModelCombo.IsEditable = true;
            _ollamaModelCombo.Margin = new Thickness(0, 0, 0, 8);
            _ollamaConfig.Children.Add(_ollamaModelCombo);

            var helpText = new TextBlock
            {
                Text = "💡 Start Ollama: ollama serve • Pull model: ollama pull qwen2.5:7b",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _ollamaConfig.Children.Add(helpText);

            _ollamaStatus = MakeStatusDot(ResultSettings.OllamaEnabled, ResultSettings.IsOllamaConfigured);
            _ollamaToggle = DarkTheme.MakeToggleSwitch(ResultSettings.OllamaEnabled, on =>
            {
                _ollamaConfig.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                UpdateOllamaStatus();
            });

            return BuildIntegrationCard("🤖", "Ollama (Local AI)", "AI field mapping, analysis, and chat — runs locally",
                DarkTheme.CatViewSheet, _ollamaToggle, _ollamaStatus, _ollamaConfig);
        }

        // ════════════════════════════════════════════
        // Shared card builder
        // ════════════════════════════════════════════

        private Border BuildIntegrationCard(string icon, string name, string description,
            SolidColorBrush accentColor, Border toggle, TextBlock statusDot, StackPanel configPanel)
        {
            var cardContent = new StackPanel();

            // ── Top row: icon + name + status + toggle ──
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // icon
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name+desc
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // status
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // toggle

            // Icon circle
            var iconCircle = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(0x30,
                    ((SolidColorBrush)accentColor).Color.R,
                    ((SolidColorBrush)accentColor).Color.G,
                    ((SolidColorBrush)accentColor).Color.B)),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconCircle, 0);
            topRow.Children.Add(iconCircle);

            // Name + description
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgWhite
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(nameStack, 1);
            topRow.Children.Add(nameStack);

            // Status dot
            statusDot.Margin = new Thickness(8, 0, 8, 0);
            statusDot.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(statusDot, 2);
            topRow.Children.Add(statusDot);

            // Toggle switch
            toggle.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(toggle, 3);
            topRow.Children.Add(toggle);

            cardContent.Children.Add(topRow);

            // ── Config section (if any) ──
            if (configPanel != null)
            {
                cardContent.Children.Add(configPanel);
            }

            // ── Card border ──
            var card = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Child = cardContent,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 2,
                    Opacity = 0.2,
                    BlurRadius = 8,
                    Direction = 270
                }
            };

            // Card hover effect
            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = accentColor;
                card.Effect = new DropShadowEffect
                {
                    Color = ((SolidColorBrush)accentColor).Color,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    BlurRadius = 14,
                    Direction = 0
                };
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = DarkTheme.BorderDim;
                card.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 2,
                    Opacity = 0.2,
                    BlurRadius = 8,
                    Direction = 270
                };
            };

            return card;
        }

        // ════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════

        private TextBlock MakeStatusDot(bool enabled, bool configured)
        {
            var dot = new TextBlock { FontSize = 14 };
            UpdateStatusDot(dot, enabled, configured);
            return dot;
        }

        private void UpdateStatusDot(TextBlock dot, bool enabled, bool configured)
        {
            if (!enabled) { dot.Text = "⚪"; dot.ToolTip = "Disabled"; }
            else if (configured) { dot.Text = "🟢"; dot.ToolTip = "Ready"; }
            else { dot.Text = "🔴"; dot.ToolTip = "Not configured"; }
        }

        private void UpdateNotionStatus()
        {
            var on = (bool)_notionToggle.Tag;
            var key = GetTextBoxValue(_notionApiKeyBox);
            var configured = on && !string.IsNullOrWhiteSpace(key) && key != "your_notion_integration_token"
                && key != "Paste your Notion integration token...";
            UpdateStatusDot(_notionStatus, on, configured);
        }

        private void UpdateSheetsStatus()
        {
            var on = (bool)_sheetsToggle.Tag;
            var path = GetTextBoxValue(_sheetsCredPathBox);
            var configured = on && (ResultSettings.GoogleSignedIn || (!string.IsNullOrWhiteSpace(path) && path != "./credentials.json"));
            UpdateStatusDot(_sheetsStatus, on, configured);
        }

        private void UpdateOllamaStatus()
        {
            var on = (bool)_ollamaToggle.Tag;
            var url = GetTextBoxValue(_ollamaUrlBox);
            var configured = on && !string.IsNullOrWhiteSpace(url);
            UpdateStatusDot(_ollamaStatus, on, configured);
        }

        private TextBlock MakeHelpLink(string text, string url)
        {
            var link = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = DarkTheme.BgAccent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 0)
            };
            link.MouseLeftButtonUp += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            };
            link.MouseEnter += (s, e) => link.TextDecorations = TextDecorations.Underline;
            link.MouseLeave += (s, e) => link.TextDecorations = null;
            return link;
        }

        /// <summary>Read text from a textbox, ignoring placeholder text.</summary>
        private string GetTextBoxValue(TextBox box)
        {
            if (box == null) return "";
            var text = box.Text?.Trim() ?? "";
            // If the text color is dim it's a placeholder
            if (box.Foreground == DarkTheme.FgDim) return "";
            return text;
        }

        private string GetComboText(ComboBox combo)
        {
            if (combo.IsEditable && !string.IsNullOrWhiteSpace(combo.Text))
                return combo.Text.Trim();
            if (combo.SelectedItem is ComboBoxItem cbi)
                return cbi.Content?.ToString() ?? "";
            if (combo.SelectedItem is string s)
                return s;
            return combo.Text?.Trim() ?? "";
        }

        // ════════════════════════════════════════════
        // Save
        // ════════════════════════════════════════════

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            ResultSettings = new IntegrationSettings
            {
                // Excel
                ExcelEnabled = (bool)_excelToggle.Tag,

                // Notion
                NotionEnabled = (bool)_notionToggle.Tag,
                NotionApiKey = GetTextBoxValue(_notionApiKeyBox),
                NotionDatabaseId = GetTextBoxValue(_notionDbIdBox),

                // Google Sheets
                GoogleSheetsEnabled = (bool)_sheetsToggle.Tag,
                GoogleSignedIn = ResultSettings.GoogleSignedIn,
                GoogleEmail = ResultSettings.GoogleEmail,
                GoogleSheetsCredentialsPath = GetTextBoxValue(_sheetsCredPathBox),
                GoogleSheetsSpreadsheetId = GetTextBoxValue(_sheetsSpreadsheetIdBox),

                // SQLite
                SqliteEnabled = (bool)_sqliteToggle.Tag,

                // Ollama
                OllamaEnabled = (bool)_ollamaToggle.Tag,
                OllamaUrl = GetTextBoxValue(_ollamaUrlBox),
                OllamaModel = GetComboText(_ollamaModelCombo),
            };

            ResultSettings.Save();
            DialogResult = true;
            Close();
        }
    }
}
