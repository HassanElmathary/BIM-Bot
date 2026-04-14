using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.Antigravity
{
    /// <summary>
    /// Standalone chat window for communicating with the Antigravity IDE agent.
    /// Uses the DarkTheme, has its own message history, and is fully independent
    /// from the AI Chat window.
    /// </summary>
    public class AntigravityWindow : Window
    {
        private static AntigravityWindow? _instance;

        private readonly AntigravityBridge _bridge = new AntigravityBridge();
        private readonly List<AntigravityMessage> _messages = new List<AntigravityMessage>();
        private readonly StackPanel _chatPanel;
        private readonly ScrollViewer _scrollViewer;
        private readonly TextBox _inputBox;
        private readonly Button _sendBtn;
        private bool _isWaiting;

        // Theme colors — matched to AI Chat (warm Claude palette)
        private static readonly SolidColorBrush UserBg      = DarkTheme.B(0xD4, 0x7B, 0x4E);  // Terracotta
        private static readonly SolidColorBrush AssistantBg  = DarkTheme.B(0x2A, 0x27, 0x23);  // Warm dark
        private static readonly SolidColorBrush BrandAccent  = DarkTheme.B(0xD4, 0x7B, 0x4E);  // Terracotta accent
        private static readonly SolidColorBrush BrandHover   = DarkTheme.B(0xC1, 0x6F, 0x44);  // Terracotta hover

        public AntigravityWindow()
        {
            Title = "Antigravity";
            Width = 520;
            Height = 680;
            MinWidth = 380;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Input

            // ── Header ──
            var header = new Border
            {
                Background = new LinearGradientBrush(DarkTheme.BrandPrimary, DarkTheme.BrandDark, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // AG icon
            var icon = AntigravityIcons.HeaderIcon(28);
            icon.Margin = new Thickness(0, 0, 10, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);
            headerGrid.Children.Add(icon);

            // Title
            var title = new TextBlock
            {
                Text = "Antigravity",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgLight,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);
            headerGrid.Children.Add(title);

            // Clear button
            var clearBtn = new Button
            {
                Content = "🗑️",
                Background = Brushes.Transparent,
                Foreground = DarkTheme.FgDim,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = Cursors.Hand,
                ToolTip = "Clear chat history",
                Padding = new Thickness(6, 4, 6, 4)
            };
            clearBtn.Click += ClearChat_Click;
            Grid.SetColumn(clearBtn, 2);
            headerGrid.Children.Add(clearBtn);

            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            // ── Chat area ──
            _chatPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 8, 12, 8),
                Content = _chatPanel
            };
            Grid.SetRow(_scrollViewer, 1);
            rootGrid.Children.Add(_scrollViewer);

            // ── Input area ──
            var inputBorder = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(12, 10, 12, 10)
            };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _inputBox = new TextBox
            {
                Background = DarkTheme.BgInput,
                Foreground = DarkTheme.FgWhite,
                CaretBrush = DarkTheme.FgWhite,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 14,
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _inputBox.KeyDown += InputBox_KeyDown;
            Grid.SetColumn(_inputBox, 0);
            inputGrid.Children.Add(_inputBox);

            _sendBtn = new Button
            {
                Width = 40,
                Height = 40,
                Background = BrandAccent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                Content = new TextBlock { Text = "➤", FontSize = 18 },
                ToolTip = "Send message"
            };
            _sendBtn.Click += Send_Click;
            _sendBtn.MouseEnter += (s, e) => _sendBtn.Background = BrandHover;
            _sendBtn.MouseLeave += (s, e) => _sendBtn.Background = BrandAccent;
            Grid.SetColumn(_sendBtn, 1);
            inputGrid.Children.Add(_sendBtn);

            inputBorder.Child = inputGrid;
            Grid.SetRow(inputBorder, 2);
            rootGrid.Children.Add(inputBorder);

            Content = rootGrid;

            // Restore history
            RestoreChatHistory();

            // Focus input on load
            Loaded += (s, e) => _inputBox.Focus();
        }

        /// <summary>Opens or brings the singleton Antigravity window to front.</summary>
        public static void Open()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new AntigravityWindow();
                _instance.Closed += (s, e) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
            }
        }

        // ── Event Handlers ──

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e) => SendMessage();

        private async void SendMessage()
        {
            var text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _isWaiting) return;

            _inputBox.Text = "";
            _isWaiting = true;
            _sendBtn.IsEnabled = false;

            // Add user message
            var userMsg = new AntigravityMessage { Role = "user", Text = text };
            _messages.Add(userMsg);
            AddBubble(userMsg);

            // Show "thinking" indicator
            var thinkingBubble = AddThinkingBubble();

            try
            {
                var response = await Task.Run(() => _bridge.SendAsync(text, _messages.Count));

                // Remove thinking indicator
                _chatPanel.Children.Remove(thinkingBubble);

                // Add assistant response
                var assistantMsg = new AntigravityMessage
                {
                    Role = "assistant",
                    Text = response.Text
                };
                _messages.Add(assistantMsg);
                AddBubble(assistantMsg);

                // Save history
                AntigravityHistory.Save(_messages);
            }
            catch (OperationCanceledException)
            {
                _chatPanel.Children.Remove(thinkingBubble);
                AddBubble(new AntigravityMessage { Role = "assistant", Text = "⛔ Request cancelled." });
            }
            catch (Exception ex)
            {
                _chatPanel.Children.Remove(thinkingBubble);
                AddBubble(new AntigravityMessage { Role = "assistant", Text = $"❌ Error: {ex.Message}" });
            }
            finally
            {
                _isWaiting = false;
                _sendBtn.IsEnabled = true;
                _inputBox.Focus();
            }
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
            _chatPanel.Children.Clear();
            AntigravityHistory.Clear();
            AddWelcomeMessage();
        }

        // ── UI Helpers ──

        private void AddBubble(AntigravityMessage msg)
        {
            var isUser = msg.Role == "user";
            var bubble = new Border
            {
                Background = isUser ? UserBg : AssistantBg,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(isUser ? 60 : 0, 4, isUser ? 0 : 60, 4),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 400
            };

            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                Foreground = DarkTheme.FgLight,
                LineHeight = 20
            };

            // Simple markdown-like formatting
            FormatText(textBlock, msg.Text);
            bubble.Child = textBlock;
            _chatPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private FrameworkElement AddThinkingBubble()
        {
            var bubble = new Border
            {
                Background = AssistantBg,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 4, 60, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var dots = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < 3; i++)
            {
                dots.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = BrandAccent,
                    Opacity = 0.4 + (i * 0.2),
                    Margin = new Thickness(2, 0, 2, 0)
                });
            }

            bubble.Child = dots;
            _chatPanel.Children.Add(bubble);
            ScrollToBottom();
            return bubble;
        }

        private void AddWelcomeMessage()
        {
            var welcome = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(16, 40, 16, 40),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var iconContainer = AntigravityIcons.HeaderIcon(48);
            iconContainer.HorizontalAlignment = HorizontalAlignment.Center;
            iconContainer.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(iconContainer);

            stack.Children.Add(new TextBlock
            {
                Text = "Antigravity",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgLight,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Type anything naturally — I'll understand.\nPowered by AI with full Revit control.",
                FontSize = 13,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            welcome.Child = stack;
            _chatPanel.Children.Add(welcome);
        }

        private void RestoreChatHistory()
        {
            var saved = AntigravityHistory.Load();
            if (saved.Count == 0)
            {
                AddWelcomeMessage();
                return;
            }

            _messages.AddRange(saved);
            foreach (var msg in saved)
            {
                AddBubble(msg);
            }
        }

        private void FormatText(TextBlock tb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Simple bold/code formatting
            var parts = text.Split('`');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1)
                {
                    // Code span
                    tb.Inlines.Add(new Run(parts[i])
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = DarkTheme.B(0x1A, 0x19, 0x17),
                        Foreground = DarkTheme.B(0xD4, 0x94, 0x6B)
                    });
                }
                else
                {
                    // Handle **bold**
                    var boldParts = parts[i].Split(new[] { "**" }, StringSplitOptions.None);
                    for (int j = 0; j < boldParts.Length; j++)
                    {
                        if (j % 2 == 1)
                        {
                            tb.Inlines.Add(new Run(boldParts[j]) { FontWeight = FontWeights.Bold });
                        }
                        else
                        {
                            tb.Inlines.Add(new Run(boldParts[j]));
                        }
                    }
                }
            }
        }

        private void ScrollToBottom()
        {
            _scrollViewer.Dispatcher.InvokeAsync(() =>
                _scrollViewer.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
