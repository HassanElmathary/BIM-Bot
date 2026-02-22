using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RevitMCPPlugin.AI;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Premium dark-themed WPF chat window — "Chat with me" branding.
    /// </summary>
    public class ChatWindow : Window
    {
        private readonly ChatOrchestrator _orchestrator;
        private bool _isBusy;

        // UI controls
        private readonly TextBlock _modelLabel;
        private readonly StackPanel _messagesPanel;
        private readonly ScrollViewer _chatScroller;
        private readonly TextBlock _statusText;
        private readonly TextBox _inputBox;
        private readonly Button _sendBtn;

        // Chat-specific colors (extend DarkTheme)
        private static readonly SolidColorBrush BgUser = DarkTheme.BgAccent;     // User bubble = accent
        private static readonly SolidColorBrush BgAI = DarkTheme.BgCard;         // AI bubble = card
        private static readonly SolidColorBrush BgError = DarkTheme.B(0x3D, 0x1F, 0x1F);
        private static readonly SolidColorBrush BgCode = DarkTheme.B(0x15, 0x15, 0x15);
        private static readonly SolidColorBrush FgCode = DarkTheme.B(0x4E, 0xC9, 0xB0);

        public ChatWindow()
        {
            // Window setup
            Title = "Chat with me — Revit AI";
            Width = 500;
            Height = 740;
            MinWidth = 400;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            // Orchestrator
            _orchestrator = new ChatOrchestrator();
            _orchestrator.OnStatusChanged += status =>
                Dispatcher.Invoke(() => _statusText.Text = status);
            _orchestrator.OnToolExecuting += (name, args) =>
                Dispatcher.Invoke(() => AddToolMessage($"🔧 Executing: {name}"));
            _orchestrator.OnToolCompleted += (name, result) =>
                Dispatcher.Invoke(() => AddToolMessage($"✅ {name} completed"));

            // ===== Build UI =====
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });    // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // --- Header ---
            var header = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Logo circle
            var logoCircle = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = DarkTheme.BgAccent,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "💬",
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -1, 0, 0)
                }
            };
            Grid.SetColumn(logoCircle, 0);
            headerGrid.Children.Add(logoCircle);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Chat with me",
                FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White
            });
            _modelLabel = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgDim };
            titleStack.Children.Add(_modelLabel);
            Grid.SetColumn(titleStack, 1);
            headerGrid.Children.Add(titleStack);

            var clearBtn = MakeIconButton("🗑️", "Clear chat");
            clearBtn.Click += ClearChat_Click;
            Grid.SetColumn(clearBtn, 2);
            headerGrid.Children.Add(clearBtn);

            var settingsBtn = MakeIconButton("⚙️", "Settings");
            settingsBtn.Click += Settings_Click;
            settingsBtn.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(settingsBtn, 3);
            headerGrid.Children.Add(settingsBtn);

            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- Chat area ---
            _chatScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(14, 10, 14, 10)
            };
            _messagesPanel = new StackPanel();

            // Welcome message
            var welcomePanel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };

            welcomePanel.Children.Add(new TextBlock
            {
                Text = "👋 Hi there!",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _messagesPanel.Children.Add(welcomePanel);
            _messagesPanel.Children.Add(MakeAIBubble(
                "I'm your Revit AI assistant. I can read your model, create elements, modify parameters, and much more — just tell me what you need!\n\n" +
                "💡 Try saying:\n" +
                "• \"Show me all levels\"\n" +
                "• \"Select all walls\"\n" +
                "• \"Create a wall from 0,0 to 20,0\"\n" +
                "• \"Color doors by type\""));

            _chatScroller.Content = _messagesPanel;
            Grid.SetRow(_chatScroller, 1);
            mainGrid.Children.Add(_chatScroller);

            // --- Status bar ---
            var statusBorder = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(14, 5, 14, 5),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            _statusText = new TextBlock { Text = "Ready", FontSize = 11, Foreground = DarkTheme.FgGreen };
            statusBorder.Child = _statusText;
            Grid.SetRow(statusBorder, 2);
            mainGrid.Children.Add(statusBorder);

            // --- Input area ---
            var inputBorder = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(14, 10, 14, 10)
            };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var inputWrap = new Border
            {
                Background = DarkTheme.BgInput,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(4),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1)
            };
            _inputBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Padding = new Thickness(12, 8, 12, 8),
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _inputBox.KeyDown += InputBox_KeyDown;
            _inputBox.GotFocus += (s, e) => { inputWrap.BorderBrush = DarkTheme.BgAccent; };
            _inputBox.LostFocus += (s, e) => { inputWrap.BorderBrush = DarkTheme.BorderDim; };
            inputWrap.Child = _inputBox;
            Grid.SetColumn(inputWrap, 0);
            inputGrid.Children.Add(inputWrap);

            _sendBtn = new Button
            {
                Content = "➤",
                Margin = new Thickness(8, 0, 0, 0),
                Width = 44, Height = 44,
                FontSize = 18,
                Foreground = Brushes.White,
                Background = DarkTheme.BgAccent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            // Round send button
            _sendBtn.Template = CreateRoundButtonTemplate();
            _sendBtn.Click += Send_Click;
            _sendBtn.MouseEnter += (s, e) => _sendBtn.Background = DarkTheme.BgAccentHover;
            _sendBtn.MouseLeave += (s, e) => _sendBtn.Background = DarkTheme.BgAccent;
            Grid.SetColumn(_sendBtn, 1);
            inputGrid.Children.Add(_sendBtn);

            inputBorder.Child = inputGrid;
            Grid.SetRow(inputBorder, 3);
            mainGrid.Children.Add(inputBorder);

            // --- Footer (author credit) ---
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(14, 8, 14, 8),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            footerStack.Children.Add(new TextBlock
            {
                Text = "Developed by Hassan Ahmed Elmathary",
                FontSize = 10,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });
            var emailLink = new TextBlock
            {
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var emailRun = new Run("hassan.elmathary@gmail.com") { Foreground = DarkTheme.BgAccent };
            emailLink.Inlines.Add(emailRun);
            emailLink.MouseLeftButtonUp += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("mailto:hassan.elmathary@gmail.com") { UseShellExecute = true }); }
                catch { }
            };
            emailLink.MouseEnter += (s, e) => emailRun.TextDecorations = TextDecorations.Underline;
            emailLink.MouseLeave += (s, e) => emailRun.TextDecorations = null;
            footerStack.Children.Add(emailLink);
            footer.Child = footerStack;
            Grid.SetRow(footer, 4);
            mainGrid.Children.Add(footer);

            Content = mainGrid;

            UpdateModelLabel();
            Loaded += (s, e) => _inputBox.Focus();
        }

        private ControlTemplate CreateRoundButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(22));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        private Button MakeIconButton(string icon, string tooltip)
        {
            return new Button
            {
                Content = icon,
                ToolTip = tooltip,
                Width = 34, Height = 34,
                Background = Brushes.Transparent,
                Foreground = DarkTheme.FgDim,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 16
            };
        }

        private void UpdateModelLabel()
        {
            var settings = _orchestrator.Gemini.GetSettings();
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                _modelLabel.Text = "⚠️ API key not set — click ⚙️";
                _modelLabel.Foreground = DarkTheme.FgGold;
            }
            else
            {
                var provider = _orchestrator.Gemini.CurrentProvider;
                _modelLabel.Text = $"{provider} · {settings.Model}";
                _modelLabel.Foreground = DarkTheme.FgDim;
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();
        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _isBusy) return;

            _isBusy = true;
            _sendBtn.IsEnabled = false;
            _inputBox.Text = "";

            AddUserMessage(text);

            try
            {
                var result = await Task.Run(() => _orchestrator.SendMessageAsync(text));
                AddAIMessage(result.Text, result.IsError);

                if (result.ToolCallCount > 0)
                    _statusText.Text = $"Ready — {result.ToolCallCount} tool(s) executed";
            }
            catch (Exception ex)
            {
                AddAIMessage($"❌ Unexpected error: {ex.Message}", true);
            }
            finally
            {
                _isBusy = false;
                _sendBtn.IsEnabled = true;
                _inputBox.Focus();
            }
        }

        private void AddUserMessage(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = Brushes.White
            };

            var border = new Border
            {
                Background = BgUser,
                CornerRadius = new CornerRadius(16, 16, 4, 16),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(60, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 360,
                Child = tb
            };

            _messagesPanel.Children.Add(border);
            ScrollToBottom();
        }

        private Border MakeAIBubble(string text, bool isError = false)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = isError ? DarkTheme.FgRequired : DarkTheme.FgLight,
                LineHeight = 20
            };
            FormatText(tb, text ?? "");

            return new Border
            {
                Background = isError ? BgError : BgAI,
                CornerRadius = new CornerRadius(16, 16, 16, 4),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 4, 40, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 400,
                Child = tb
            };
        }

        private void AddAIMessage(string text, bool isError = false)
        {
            _messagesPanel.Children.Add(MakeAIBubble(text, isError));
            ScrollToBottom();
        }

        private void AddToolMessage(string text)
        {
            _messagesPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(8, 2, 8, 2),
                FontStyle = FontStyles.Italic
            });
            ScrollToBottom();
        }

        private void FormatText(TextBlock tb, string text)
        {
            var parts = text.Split(new[] { "```" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) // Code block
                {
                    var code = parts[i].Trim();
                    var firstNl = code.IndexOf('\n');
                    if (firstNl > 0 && firstNl < 20 && !code.Substring(0, firstNl).Contains(" "))
                        code = code.Substring(firstNl + 1);

                    tb.Inlines.Add(new Run(code)
                    {
                        FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                        FontSize = 12,
                        Background = BgCode,
                        Foreground = FgCode
                    });
                }
                else // Regular text — handle bold **text**
                {
                    var boldParts = parts[i].Split(new[] { "**" }, StringSplitOptions.None);
                    for (int j = 0; j < boldParts.Length; j++)
                    {
                        var run = new Run(boldParts[j]);
                        if (j % 2 == 1) run.FontWeight = FontWeights.Bold;
                        tb.Inlines.Add(run);
                    }
                }
            }
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => _chatScroller.ScrollToEnd()));
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            while (_messagesPanel.Children.Count > 1)
                _messagesPanel.Children.RemoveAt(_messagesPanel.Children.Count - 1);
            _orchestrator.ClearHistory();
            _statusText.Text = "Chat cleared";
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow(_orchestrator.Gemini.GetSettings()) { Owner = this };
            if (sw.ShowDialog() == true)
            {
                _orchestrator.Gemini.UpdateSettings(sw.ResultSettings);
                UpdateModelLabel();
                _statusText.Text = "Settings saved ✓";
            }
        }

        // ===== Static singleton for tool launcher integration =====
        private static ChatWindow? _instance;

        /// <summary>
        /// Opens (or reuses) the chat window and auto-sends a prompt to invoke a tool.
        /// Used by the ribbon Tools dropdown buttons.
        /// </summary>
        public static void OpenWithPrompt(string prompt)
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new ChatWindow();
                _instance.Closed += (s, e) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }

            // Set the text and auto-send
            _instance._inputBox.Text = prompt;
            _instance.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(async () => await _instance.SendMessage()));
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
            => DarkTheme.B(r, g, b);
    }
}
