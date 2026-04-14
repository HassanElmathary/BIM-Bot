using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using BIMBotPlugin.AI;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// BIM-Bot branded chat window with sketch-style icons
    /// and modern AI assistant aesthetics — blue-teal design system.
    /// </summary>
    public class ChatWindow : Window
    {
        private readonly ChatOrchestrator _orchestrator;
        private readonly GeminiCliOrchestrator _cliOrchestrator;
        private GeminiCliSettings _cliSettings;
        private bool _isBusy;
        private CancellationTokenSource _cts;

        // UI controls
        private readonly TextBlock _modelLabel;
        private readonly StackPanel _messagesPanel;
        private readonly ScrollViewer _chatScroller;
        private readonly TextBlock _statusText;
        private readonly Border _statusDot;
        private readonly TextBox _inputBox;
        private readonly Button _sendBtn;
        private readonly Button _fileBtn;
        private readonly Button _modeToggleBtn;

        // ── BIM-Bot Blue Design System Palette ──
        // Cool dark mode aligned with DESIGN_SYSTEM.md brand identity
        private static readonly SolidColorBrush BgCanvas    = DarkTheme.BgDark;               // #1A1B1E canvas
        private static readonly SolidColorBrush BgSurface   = DarkTheme.BgCard;               // #25262B surface
        private static readonly SolidColorBrush BgElevated  = DarkTheme.BgCardHover;           // #2C2E33 elevated
        private static readonly SolidColorBrush BgInput_    = DarkTheme.BgInput;               // #1F2024 input bg
        private static readonly SolidColorBrush BgUser_     = DarkTheme.BgAccent;              // Brand blue user bubbles
        private static readonly SolidColorBrush BgUserHover = DarkTheme.BgAccentHover;         // Dark blue hover
        private static readonly SolidColorBrush BgAI_       = B(0x1E, 0x20, 0x25);             // AI bubble — slightly blue-tinted
        private static readonly SolidColorBrush BgError_    = B(0x3A, 0x1A, 0x1A);             // Red-tinted error bg
        private static readonly SolidColorBrush BgCode_     = DarkTheme.BgDeep;                // Deep code bg
        private static readonly SolidColorBrush BgAccent_   = DarkTheme.BgAccent;              // Brand blue accent
        private static readonly SolidColorBrush BgAccHover  = DarkTheme.BgAccentHover;         // Blue hover
        private static readonly SolidColorBrush BgMuted     = DarkTheme.BgCancel;              // Muted button
        private static readonly SolidColorBrush BgMutedHvr  = DarkTheme.BgCancelHover;         // Muted hover

        private static readonly SolidColorBrush FgPrimary   = B(0xE4, 0xE6, 0xEB);             // Cool off-white — primary text
        private static readonly SolidColorBrush FgSecondary = DarkTheme.FgLight;               // #C1C2C5 secondary
        private static readonly SolidColorBrush FgMuted_    = DarkTheme.FgDim;                 // #909296 muted
        private static readonly SolidColorBrush FgCode_     = new SolidColorBrush(DarkTheme.BrandTeal);  // Teal code text
        private static readonly SolidColorBrush FgSuccess   = DarkTheme.FgGreen;               // Brand emerald
        private static readonly SolidColorBrush FgError_    = new SolidColorBrush(DarkTheme.BrandRed);   // Brand red
        private static readonly SolidColorBrush FgGold_     = DarkTheme.FgGold;                // Brand amber

        private static readonly SolidColorBrush BorderSoft  = DarkTheme.BorderDim;             // #373A40 borders
        private static readonly SolidColorBrush BorderFocus = new SolidColorBrush(DarkTheme.BrandTeal);  // Teal focus ring

        public ChatWindow()
        {
            // Window setup
            Title = "BIM-Bot";
            Width = 520;
            Height = 780;
            MinWidth = 420;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgCanvas;
            Foreground = FgPrimary;
            FontFamily = new FontFamily("Segoe UI");

            // Orchestrators
            _orchestrator = new ChatOrchestrator();
            _orchestrator.OnStatusChanged += status =>
                Dispatcher.Invoke(() => _statusText.Text = status);
            _orchestrator.OnToolExecuting += (name, args) =>
                Dispatcher.Invoke(() => AddToolMessage(name, false));
            _orchestrator.OnToolCompleted += (name, result) =>
                Dispatcher.Invoke(() => AddToolMessage(name, true));

            // CLI mode orchestrator
            _cliSettings = GeminiCliSettings.Load();
            _cliOrchestrator = new GeminiCliOrchestrator();
            _cliOrchestrator.OnStatusChanged += status =>
                Dispatcher.Invoke(() => _statusText.Text = status);

            // ===== Build UI =====
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // ═══ HEADER ═══
            var header = new Border
            {
                Background = new LinearGradientBrush(DarkTheme.BrandPrimary, DarkTheme.BrandDark, 0),
                Padding = new Thickness(18, 0, 18, 0),
                BorderBrush = BorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Logo
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Mode toggle
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Clear
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Integrations
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Settings

            // AI Sparkle logo
            var logoBorder = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(10),
                Background = BgElevated,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Viewbox
                {
                    Width = 20, Height = 20,
                    Child = ChatIcons.Sparkle(20)
                }
            };
            Grid.SetColumn(logoBorder, 0);
            headerGrid.Children.Add(logoBorder);

            // Title + model info
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "BIM-Bot",
                FontSize = 16, FontWeight = FontWeights.SemiBold,
                Foreground = FgPrimary,
                FontFamily = new FontFamily("Segoe UI")
            });
            _modelLabel = new TextBlock
            {
                FontSize = 11,
                Foreground = FgMuted_,
                FontFamily = new FontFamily("Segoe UI")
            };
            titleStack.Children.Add(_modelLabel);
            Grid.SetColumn(titleStack, 1);
            headerGrid.Children.Add(titleStack);

            // Mode toggle button
            _modeToggleBtn = MakeHeaderButton(
                _cliSettings.UseGeminiCli ? "CLI" : "API",
                _cliSettings.UseGeminiCli
                    ? "Using Gemini CLI — click to switch"
                    : "Using API — click to switch");
            _modeToggleBtn.Click += ModeToggle_Click;
            if (_cliSettings.UseGeminiCli)
                _modeToggleBtn.Background = B(0x1A, 0x33, 0x2A);  // Subtle brand-green tint
            Grid.SetColumn(_modeToggleBtn, 2);
            headerGrid.Children.Add(_modeToggleBtn);

            // Header icon buttons
            var clearBtn = MakeIconBtn(ChatIcons.Trash(16), "Clear chat");
            clearBtn.Click += ClearChat_Click;
            Grid.SetColumn(clearBtn, 3);
            headerGrid.Children.Add(clearBtn);

            var integBtn = MakeIconBtn(ChatIcons.Link(16), "Integrations");
            integBtn.Click += Integrations_Click;
            Grid.SetColumn(integBtn, 4);
            headerGrid.Children.Add(integBtn);

            var settingsBtn = MakeIconBtn(ChatIcons.Gear(16), "Settings");
            settingsBtn.Click += Settings_Click;
            Grid.SetColumn(settingsBtn, 5);
            headerGrid.Children.Add(settingsBtn);

            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // ═══ CHAT AREA ═══
            _chatScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(16, 12, 16, 12),
                Background = BgCanvas
            };
            _messagesPanel = new StackPanel();

            // Welcome section
            var welcome = new StackPanel { Margin = new Thickness(0, 16, 0, 12) };

            var welcomeIcon = new Viewbox
            {
                Width = 40, Height = 40,
                Child = ChatIcons.Sparkle(40),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12)
            };
            welcome.Children.Add(welcomeIcon);

            welcome.Children.Add(new TextBlock
            {
                Text = "How can I help you today?",
                FontSize = 22, FontWeight = FontWeights.Light,
                Foreground = FgPrimary,
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new FontFamily("Segoe UI Light, Segoe UI")
            });

            welcome.Children.Add(MakeHintChip("Show me all levels"));
            welcome.Children.Add(MakeHintChip("Select all walls"));
            welcome.Children.Add(MakeHintChip("Color doors by type"));
            welcome.Children.Add(MakeHintChip("Export to PDF"));

            _messagesPanel.Children.Add(welcome);
            _chatScroller.Content = _messagesPanel;
            Grid.SetRow(_chatScroller, 1);
            mainGrid.Children.Add(_chatScroller);

            // ═══ INPUT AREA ═══
            var inputArea = new Border
            {
                Background = BgSurface,
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = BorderSoft,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            var inputStack = new StackPanel();

            // Status bar (inline, above input)
            var statusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 0, 0, 6),
            };
            _statusDot = new Border
            {
                Width = 6, Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = FgSuccess,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            statusRow.Children.Add(_statusDot);
            _statusText = new TextBlock
            {
                Text = "Ready",
                FontSize = 11,
                Foreground = FgMuted_,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(_statusText);
            inputStack.Children.Add(statusRow);

            // Input row
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // File btn
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Input
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Send btn

            // File button
            _fileBtn = new Button
            {
                Width = 38, Height = 38,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Attach a file",
                Margin = new Thickness(0, 0, 6, 0),
                Content = new Viewbox { Width = 18, Height = 18, Child = ChatIcons.File(18) },
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _fileBtn.MouseEnter += (s, e) => _fileBtn.Background = BgMuted;
            _fileBtn.MouseLeave += (s, e) => _fileBtn.Background = Brushes.Transparent;
            _fileBtn.Click += FileBtn_Click;
            Grid.SetColumn(_fileBtn, 0);
            inputGrid.Children.Add(_fileBtn);

            // Input textbox with rounded border
            var inputBorder = new Border
            {
                Background = BgInput_,
                CornerRadius = new CornerRadius(12),
                BorderBrush = BorderSoft,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2)
            };
            _inputBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = FgPrimary,
                CaretBrush = BgAccent_,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Padding = new Thickness(14, 10, 14, 10),
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Segoe UI")
            };
            _inputBox.KeyDown += InputBox_KeyDown;
            _inputBox.GotFocus += (s, e) => inputBorder.BorderBrush = BorderFocus;
            _inputBox.LostFocus += (s, e) => inputBorder.BorderBrush = BorderSoft;
            inputBorder.Child = _inputBox;
            Grid.SetColumn(inputBorder, 1);
            inputGrid.Children.Add(inputBorder);

            // Send button
            _sendBtn = new Button
            {
                Width = 38, Height = 38,
                Background = BgAccent_,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Content = new Viewbox { Width = 18, Height = 18, Child = ChatIcons.Send(18) }
            };
            // Round send button
            _sendBtn.Template = CreateRoundButtonTemplate(20);
            _sendBtn.Click += Send_Click;
            _sendBtn.MouseEnter += SendBtn_HoverEnter;
            _sendBtn.MouseLeave += SendBtn_HoverLeave;
            Grid.SetColumn(_sendBtn, 2);
            inputGrid.Children.Add(_sendBtn);

            inputStack.Children.Add(inputGrid);
            inputArea.Child = inputStack;
            Grid.SetRow(inputArea, 2);
            mainGrid.Children.Add(inputArea);

            // ═══ FOOTER ═══
            var footer = new Border
            {
                Background = BgSurface,
                Padding = new Thickness(16, 6, 16, 6),
                BorderBrush = BorderSoft,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerText = new TextBlock
            {
                FontSize = 10,
                Foreground = FgMuted_,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            };
            footerText.Inlines.Add(new Run("Built by "));
            var nameRun = new Run("Hassan Ahmed Elmathary") { Foreground = FgSecondary };
            footerText.Inlines.Add(nameRun);
            var emailRun = new Run(" · hassan.elmathary@gmail.com")
            {
                Foreground = BgAccent_,
                Cursor = Cursors.Hand
            };
            footerText.Inlines.Add(emailRun);
            footerText.MouseLeftButtonUp += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("mailto:hassan.elmathary@gmail.com") { UseShellExecute = true }); }
                catch { }
            };
            footer.Child = footerText;
            Grid.SetRow(footer, 3);
            mainGrid.Children.Add(footer);

            Content = mainGrid;

            UpdateModelLabel();
            Loaded += (s, e) =>
            {
                _inputBox.Focus();
                RestoreChatHistory();
            };
        }

        // ═══ UI FACTORY METHODS ═══

        private ControlTemplate CreateRoundButtonTemplate(double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        /// <summary>Creates a clickable hint chip for the welcome area.</summary>
        private Border MakeHintChip(string text)
        {
            var chip = new Border
            {
                Background = BgElevated,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand,
                BorderBrush = BorderSoft,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var tb = new TextBlock
            {
                Text = "→  " + text,
                FontSize = 13,
                Foreground = FgSecondary,
                FontFamily = new FontFamily("Segoe UI")
            };
            chip.Child = tb;

            chip.MouseEnter += (s, e) =>
            {
                chip.Background = BgMutedHvr;
                chip.BorderBrush = BgAccent_;
                tb.Foreground = FgPrimary;
            };
            chip.MouseLeave += (s, e) =>
            {
                chip.Background = BgElevated;
                chip.BorderBrush = BorderSoft;
                tb.Foreground = FgSecondary;
            };
            chip.MouseLeftButtonUp += (s, e) =>
            {
                _inputBox.Text = text;
                _inputBox.Focus();
                _inputBox.CaretIndex = text.Length;
            };

            return chip;
        }

        /// <summary>Creates a small icon button for the header.</summary>
        private Button MakeIconBtn(UIElement icon, string tooltip)
        {
            var btn = new Button
            {
                Width = 32, Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new Viewbox { Width = 16, Height = 16, Child = icon }
            };
            btn.MouseEnter += (s, e) => btn.Background = BgMuted;
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
            return btn;
        }

        /// <summary>Creates a text button for the header (API/CLI toggle).</summary>
        private Button MakeHeaderButton(string text, string tooltip)
        {
            var btn = new Button
            {
                Content = text,
                ToolTip = tooltip,
                Background = BgMuted,
                Foreground = FgSecondary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.MouseEnter += (s, e) =>
            {
                if (btn.Background != B(0x1A, 0x33, 0x2A))
                    btn.Background = BgMutedHvr;
            };
            btn.MouseLeave += (s, e) =>
            {
                if (btn.Background == BgMutedHvr)
                    btn.Background = BgMuted;
            };
            return btn;
        }

        // ═══ MODEL LABEL ═══

        private void UpdateModelLabel()
        {
            if (_cliSettings.UseGeminiCli)
            {
                if (!_cliSettings.IsConfigured)
                {
                    _modelLabel.Text = "CLI key not set — open Settings";
                    _modelLabel.Foreground = FgGold_;
                }
                else
                {
                    _modelLabel.Text = "Gemini CLI · MCP";
                    _modelLabel.Foreground = FgMuted_;
                }
            }
            else
            {
                var settings = _orchestrator.Gemini.GetSettings();
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    _modelLabel.Text = "API key not set — open Settings";
                    _modelLabel.Foreground = FgGold_;
                }
                else
                {
                    var provider = _orchestrator.Gemini.CurrentProvider;
                    _modelLabel.Text = $"{provider} · {settings.Model}";
                    _modelLabel.Foreground = FgMuted_;
                }
            }
        }

        // ═══ SEND / INPUT HANDLERS ═══

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
            {
                _cts?.Cancel();
                return;
            }
            await SendMessage();
        }

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
            _inputBox.Text = "";

            // Switch to Stop mode
            _sendBtn.Content = new Viewbox { Width = 16, Height = 16, Child = ChatIcons.Stop(16) };
            _sendBtn.Background = FgError_;
            _sendBtn.MouseEnter -= SendBtn_HoverEnter;
            _sendBtn.MouseLeave -= SendBtn_HoverLeave;
            _sendBtn.MouseEnter += StopBtn_HoverEnter;
            _sendBtn.MouseLeave += StopBtn_HoverLeave;
            _sendBtn.ToolTip = "Stop generation";
            _statusDot.Background = FgGold_;

            _cts = new CancellationTokenSource();

            AddUserMessage(text);

            try
            {
                ChatResult result;
                if (_cliSettings.UseGeminiCli)
                    result = await Task.Run(() => _cliOrchestrator.SendMessageAsync(text, _cts.Token));
                else
                    result = await Task.Run(() => _orchestrator.SendMessageAsync(text, _cts.Token));

                AddAIMessage(result.Text, result.IsError);

                if (result.ToolCallCount > 0)
                    _statusText.Text = $"Done — {result.ToolCallCount} tool(s) used";
            }
            catch (OperationCanceledException)
            {
                AddAIMessage("Stopped by user.", false);
                _statusText.Text = "Stopped";
            }
            catch (Exception ex)
            {
                AddAIMessage($"Error: {ex.Message}", true);
            }
            finally
            {
                _isBusy = false;
                _cts?.Dispose();
                _cts = null;

                // Restore Send button
                _sendBtn.Content = new Viewbox { Width = 18, Height = 18, Child = ChatIcons.Send(18) };
                _sendBtn.Background = BgAccent_;
                _sendBtn.ToolTip = null;
                _sendBtn.MouseEnter -= StopBtn_HoverEnter;
                _sendBtn.MouseLeave -= StopBtn_HoverLeave;
                _sendBtn.MouseEnter += SendBtn_HoverEnter;
                _sendBtn.MouseLeave += SendBtn_HoverLeave;
                _statusDot.Background = FgSuccess;
                _inputBox.Focus();
            }
        }

        // Button hover handlers
        private void SendBtn_HoverEnter(object s, MouseEventArgs e) => _sendBtn.Background = BgAccHover;
        private void SendBtn_HoverLeave(object s, MouseEventArgs e) => _sendBtn.Background = BgAccent_;
        private void StopBtn_HoverEnter(object s, MouseEventArgs e) => _sendBtn.Background = B(0xAA, 0x55, 0x55);
        private void StopBtn_HoverLeave(object s, MouseEventArgs e) => _sendBtn.Background = FgError_;

        // ═══ FILE PICKER ═══

        private void FileBtn_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a file to process with AI",
                Filter = "All supported|*.xlsx;*.xls;*.csv;*.txt;*.json;*.xml;*.pdf;*.docx|" +
                         "Excel|*.xlsx;*.xls|CSV|*.csv|Text|*.txt|JSON|*.json|All|*.*"
            };

            try
            {
                var app = BIMBotPlugin.Core.Application.ActiveUIApp;
                var doc = app?.ActiveUIDocument?.Document;
                if (doc != null && !string.IsNullOrEmpty(doc.PathName))
                {
                    var projFolder = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(doc.PathName), "BIMBot_Files");
                    if (System.IO.Directory.Exists(projFolder))
                        ofd.InitialDirectory = projFolder;
                }
            }
            catch { }

            if (ofd.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileName(ofd.FileName);
                string content;
                try
                {
                    content = System.IO.File.ReadAllText(ofd.FileName);
                    if (content.Length > 15000)
                        content = content.Substring(0, 15000) + "\n... (truncated)";
                }
                catch (Exception ex)
                {
                    _statusText.Text = $"Error: {ex.Message}";
                    return;
                }

                _inputBox.Text = $"Analyze this file '{fileName}':\n\n```\n{content}\n```\n\n" +
                                 "Please provide a detailed analysis of this data.";
                _inputBox.Focus();
                _inputBox.CaretIndex = 0;
                _statusText.Text = $"Attached: {fileName}";
            }
        }

        // ═══ MESSAGE RENDERING ═══

        private void AddUserMessage(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                Foreground = DarkTheme.FgWhite,
                LineHeight = 20,
                FontFamily = new FontFamily("Segoe UI")
            };

            var bubble = new Border
            {
                Background = BgUser_,
                CornerRadius = new CornerRadius(14, 14, 4, 14),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(80, 6, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 380,
                Child = tb,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, ShadowDepth = 1,
                    Opacity = 0.15, BlurRadius = 6, Direction = 270
                }
            };

            _messagesPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private Border MakeAIBubble(string text, bool isError = false, bool showSave = false)
        {
            // AI label row
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            headerRow.Children.Add(new Viewbox
            {
                Width = 16, Height = 16,
                Child = ChatIcons.Sparkle(16),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            headerRow.Children.Add(new TextBlock
            {
                Text = "BIM-Bot",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgMuted_,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Message content
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                Foreground = isError ? FgError_ : FgPrimary,
                LineHeight = 22,
                FontFamily = new FontFamily("Segoe UI")
            };
            FormatText(tb, text ?? "");

            var contentStack = new StackPanel();
            contentStack.Children.Add(headerRow);
            contentStack.Children.Add(tb);

            // Save button for substantial responses
            if (showSave && !isError && !string.IsNullOrWhiteSpace(text) && text.Length > 50)
            {
                var saveBtn = new Button
                {
                    Content = "Save",
                    FontSize = 11,
                    Foreground = FgSecondary,
                    Background = BgMuted,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 4, 12, 4),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                saveBtn.MouseEnter += (s, e) => { saveBtn.Background = BgMutedHvr; saveBtn.Foreground = FgPrimary; };
                saveBtn.MouseLeave += (s, e) => { saveBtn.Background = BgMuted; saveBtn.Foreground = FgSecondary; };
                var responseText = text;
                saveBtn.Click += (s, e) => SaveAIResponse(responseText);
                contentStack.Children.Add(saveBtn);
            }

            return new Border
            {
                Background = isError ? BgError_ : BgAI_,
                CornerRadius = new CornerRadius(14, 14, 14, 4),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 6, 60, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 420,
                Child = contentStack,
                BorderBrush = isError ? FgError_ : BorderSoft,
                BorderThickness = new Thickness(1)
            };
        }

        private void AddAIMessage(string text, bool isError = false)
        {
            _messagesPanel.Children.Add(MakeAIBubble(text, isError, showSave: !isError));
            ScrollToBottom();
        }

        /// <summary>Adds a compact tool execution message with sketch icon.</summary>
        private void AddToolMessage(string toolName, bool completed)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 2, 20, 2)
            };

            row.Children.Add(new Viewbox
            {
                Width = 12, Height = 12,
                Child = completed ? ChatIcons.Check(12) : ChatIcons.Tool(12),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            row.Children.Add(new TextBlock
            {
                Text = completed ? $"{toolName}" : $"{toolName}...",
                FontSize = 11,
                Foreground = completed ? FgSuccess : FgMuted_,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            });

            _messagesPanel.Children.Add(row);
            ScrollToBottom();
        }

        // ═══ TEXT FORMATTING ═══

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
                        Background = BgCode_,
                        Foreground = FgCode_
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

        // ═══ SAVE AI RESPONSE ═══

        private void SaveAIResponse(string text)
        {
            string projectFolder;
            try
            {
                var app = BIMBotPlugin.Core.Application.ActiveUIApp;
                var doc = app?.ActiveUIDocument?.Document;
                if (doc != null && !string.IsNullOrEmpty(doc.PathName))
                    projectFolder = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(doc.PathName)!, "BIMBot_Files");
                else
                    projectFolder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BIMBot_Files");
            }
            catch
            {
                projectFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BIMBot_Files");
            }

            if (!System.IO.Directory.Exists(projectFolder))
                System.IO.Directory.CreateDirectory(projectFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Analysis",
                InitialDirectory = projectFolder,
                FileName = $"analysis_{timestamp}",
                Filter = "JSON|*.json|Text|*.txt|CSV|*.csv",
                DefaultExt = ".json"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var ext = System.IO.Path.GetExtension(sfd.FileName).ToLower();
                    if (ext == ".json")
                    {
                        var jsonObj = new Newtonsoft.Json.Linq.JObject
                        {
                            ["analysis_date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            ["content"] = text
                        };
                        System.IO.File.WriteAllText(sfd.FileName,
                            jsonObj.ToString(Newtonsoft.Json.Formatting.Indented));
                    }
                    else
                    {
                        System.IO.File.WriteAllText(sfd.FileName, text);
                    }
                    _statusText.Text = $"Saved: {System.IO.Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    _statusText.Text = $"Save error: {ex.Message}";
                }
            }
        }

        // ═══ CHAT MANAGEMENT ═══

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            while (_messagesPanel.Children.Count > 1)
                _messagesPanel.Children.RemoveAt(_messagesPanel.Children.Count - 1);

            if (_cliSettings.UseGeminiCli)
                _cliOrchestrator.ClearHistory();
            else
                _orchestrator.ClearHistory();

            _statusText.Text = "Chat cleared";
        }

        /// <summary>Restore previous chat messages from saved history.</summary>
        private void RestoreChatHistory()
        {
            try
            {
                _orchestrator.SetProjectPath(null);
                var chatLog = _orchestrator.GetChatLog();
                if (chatLog == null || chatLog.Count == 0) return;

                // Session divider
                var divider = new Border
                {
                    Background = BorderSoft,
                    Height = 1,
                    Margin = new Thickness(20, 12, 20, 4)
                };
                _messagesPanel.Children.Add(divider);

                var bannerText = new TextBlock
                {
                    Text = "Previous session",
                    FontSize = 11,
                    Foreground = FgMuted_,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontFamily = new FontFamily("Segoe UI")
                };
                _messagesPanel.Children.Add(bannerText);

                foreach (var entry in chatLog)
                {
                    switch (entry.Role)
                    {
                        case "user":
                            AddUserMessage(entry.Content);
                            break;
                        case "assistant":
                            AddAIMessage(entry.Content, false);
                            break;
                        case "tool_call":
                            AddToolMessage(entry.ToolName, false);
                            break;
                        case "tool_result":
                            AddToolMessage(entry.ToolName, true);
                            break;
                        case "error":
                            AddAIMessage(entry.Content, true);
                            break;
                    }
                }

                // New session divider
                var newDiv = new Border
                {
                    Background = BorderSoft,
                    Height = 1,
                    Margin = new Thickness(20, 8, 20, 12)
                };
                _messagesPanel.Children.Add(newDiv);

                _statusText.Text = $"Restored {chatLog.Count} message(s)";
            }
            catch { }
        }

        // ═══ SETTINGS & MODE ═══

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_cliSettings.UseGeminiCli)
            {
                var ow = new ApiKeyOnboardingWindow(_cliSettings) { Owner = this };
                if (ow.ShowDialog() == true)
                {
                    _cliSettings = ow.ResultSettings;
                    _cliOrchestrator.UpdateSettings(_cliSettings);
                    UpdateModelLabel();
                    _statusText.Text = "CLI settings saved";
                }
            }
            else
            {
                var sw = new SettingsWindow(_orchestrator.Gemini.GetSettings()) { Owner = this };
                if (sw.ShowDialog() == true)
                {
                    _orchestrator.Gemini.UpdateSettings(sw.ResultSettings);
                    UpdateModelLabel();
                    _statusText.Text = "Settings saved";
                }
            }
        }

        private void Integrations_Click(object sender, RoutedEventArgs e)
        {
            var current = IntegrationSettings.Load();
            var iw = new IntegrationsSettingsWindow(current) { Owner = this };
            if (iw.ShowDialog() == true)
            {
                _statusText.Text = "Integrations saved";
            }
        }

        private void ModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _cliSettings.UseGeminiCli = !_cliSettings.UseGeminiCli;
            _cliSettings.Save();

            _modeToggleBtn.Content = _cliSettings.UseGeminiCli ? "CLI" : "API";
            _modeToggleBtn.ToolTip = _cliSettings.UseGeminiCli
                ? "Using Gemini CLI — click to switch"
                : "Using API — click to switch";
            _modeToggleBtn.Background = _cliSettings.UseGeminiCli
                ? B(0x1A, 0x33, 0x2A)
                : BgMuted;

            UpdateModelLabel();

            var mode = _cliSettings.UseGeminiCli ? "Gemini CLI" : "API";
            _statusText.Text = $"Switched to {mode} mode";

            if (_cliSettings.UseGeminiCli && !_cliSettings.IsConfigured)
            {
                var ow = new ApiKeyOnboardingWindow(_cliSettings) { Owner = this };
                if (ow.ShowDialog() == true)
                {
                    _cliSettings = ow.ResultSettings;
                    _cliOrchestrator.UpdateSettings(_cliSettings);
                    UpdateModelLabel();
                    _statusText.Text = "Gemini CLI configured";
                }
            }
        }

        // ═══ STATIC SINGLETON ═══

        private static ChatWindow? _instance;

        /// <summary>Opens the chat window and auto-sends a prompt.</summary>
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

            _instance._inputBox.Text = prompt;
            _instance.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(async () => await _instance.SendMessage()));
        }

        /// <summary>Opens the chat window and shows a result directly.</summary>
        public static void OpenWithResult(string result)
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

            _instance.AddAIMessage(result, false);
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
