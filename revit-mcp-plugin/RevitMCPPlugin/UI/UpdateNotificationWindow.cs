using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using RevitMCPPlugin.Core;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Modern WPF window that notifies the user about available plugin updates.
    /// </summary>
    public class UpdateNotificationWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private Button? _downloadBtn;
        private TextBlock? _statusText;

        public UpdateNotificationWindow(UpdateInfo updateInfo)
        {
            _updateInfo = updateInfo;
            InitializeWindow();
            BuildUI();
        }

        private void InitializeWindow()
        {
            Title = "Revit MCP — Update Available";
            Width = 520;
            Height = 460;
            MinWidth = 420;
            MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }

        private void BuildUI()
        {
            // Main border with rounded corners and shadow
            var mainBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 40)),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 4,
                    Opacity = 0.5
                }
            };

            var mainStack = new StackPanel { Margin = new Thickness(0) };

            // ====== Top header bar with gradient ======
            var headerBorder = new Border
            {
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Padding = new Thickness(28, 20, 28, 18),
                Background = new LinearGradientBrush(
                    Color.FromRgb(56, 120, 230),
                    Color.FromRgb(90, 60, 200),
                    45)
            };

            var headerStack = new StackPanel();

            // Close button
            var closeBtn = new Button
            {
                Content = "✕",
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(4)
            };
            closeBtn.Click += (s, e) => Close();
            headerStack.Children.Add(closeBtn);

            // Update icon + title
            headerStack.Children.Add(new TextBlock
            {
                Text = "🚀",
                FontSize = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            headerStack.Children.Add(new TextBlock
            {
                Text = "Update Available!",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            headerStack.Children.Add(new TextBlock
            {
                Text = $"v{Core.Application.Version}  →  {_updateInfo.LatestVersion}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });

            headerBorder.Child = headerStack;
            mainStack.Children.Add(headerBorder);

            // ====== Body content ======
            var bodyStack = new StackPanel { Margin = new Thickness(28, 20, 28, 12) };

            // What's new label
            bodyStack.Children.Add(new TextBlock
            {
                Text = "What's New",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 180)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Changelog scroll area
            var changelogBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 30)),
                Padding = new Thickness(14),
                MaxHeight = 130
            };

            var changelogScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 110
            };

            var changelogLines = _updateInfo.Changelog.Split(new[] { '\n' }, StringSplitOptions.None);
            var changelogText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 205, 220)),
                FontSize = 12,
                LineHeight = 20
            };

            foreach (var line in changelogLines)
            {
                if (changelogText.Inlines.Count > 0)
                    changelogText.Inlines.Add(new System.Windows.Documents.LineBreak());

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    changelogText.Inlines.Add(new System.Windows.Documents.Run("  • " + trimmed.Substring(2))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 205, 220))
                    });
                }
                else
                {
                    changelogText.Inlines.Add(new System.Windows.Documents.Run(line));
                }
            }

            changelogScroll.Content = changelogText;
            changelogBorder.Child = changelogScroll;
            bodyStack.Children.Add(changelogBorder);

            // Status text
            _statusText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 120)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            bodyStack.Children.Add(_statusText);

            mainStack.Children.Add(bodyStack);

            // ====== Bottom buttons ======
            var buttonPanel = new StackPanel { Margin = new Thickness(28, 4, 28, 24) };

            // Download button (primary)
            _downloadBtn = CreateButton("⬇  Download & Update", true);
            _downloadBtn.Click += OnDownloadClicked;
            buttonPanel.Children.Add(_downloadBtn);

            // Secondary buttons row
            var secondaryRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            secondaryRow.ColumnDefinitions.Add(new ColumnDefinition());
            secondaryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            secondaryRow.ColumnDefinitions.Add(new ColumnDefinition());

            var remindBtn = CreateButton("Remind Me Later", false);
            remindBtn.Click += (s, e) => Close();
            Grid.SetColumn(remindBtn, 0);
            secondaryRow.Children.Add(remindBtn);

            var skipBtn = CreateButton("Skip This Version", false);
            skipBtn.Click += OnSkipClicked;
            Grid.SetColumn(skipBtn, 2);
            secondaryRow.Children.Add(skipBtn);

            buttonPanel.Children.Add(secondaryRow);

            // View on GitHub link
            var linkText = new TextBlock
            {
                Text = "View release on GitHub →",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 230)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                TextDecorations = TextDecorations.Underline
            };
            linkText.MouseLeftButtonDown += (s, e) =>
            {
                try { Process.Start(_updateInfo.ReleaseUrl); } catch { }
            };
            buttonPanel.Children.Add(linkText);

            mainStack.Children.Add(buttonPanel);

            mainBorder.Child = mainStack;
            Content = mainBorder;

            // Enable window dragging from the header
            headerBorder.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
        }

        private Button CreateButton(string text, bool isPrimary)
        {
            var btn = new Button
            {
                Content = text,
                Height = 40,
                FontSize = 14,
                FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(isPrimary ? 0 : 1),
                Padding = new Thickness(16, 0, 16, 0)
            };

            if (isPrimary)
            {
                btn.Background = new LinearGradientBrush(
                    Color.FromRgb(56, 120, 230),
                    Color.FromRgb(70, 90, 210),
                    90);
                btn.Foreground = Brushes.White;
                btn.BorderBrush = Brushes.Transparent;
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 58));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(180, 185, 200));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 75));
            }

            // Rounded corners via template
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BackgroundProperty, btn.Background);
            borderFactory.SetValue(Border.BorderBrushProperty, btn.BorderBrush);
            borderFactory.SetValue(Border.BorderThicknessProperty, btn.BorderThickness);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;
            btn.Template = template;

            return btn;
        }

        private async void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            if (_downloadBtn == null || _statusText == null) return;

            _downloadBtn.IsEnabled = false;
            _downloadBtn.Content = "⏳  Downloading...";
            _statusText.Text = "Downloading update from GitHub...";
            _statusText.Visibility = Visibility.Visible;

            try
            {
                // If there's a direct asset download URL, download it
                if (!string.IsNullOrEmpty(_updateInfo.AssetFileName) &&
                    _updateInfo.DownloadUrl != _updateInfo.ReleaseUrl)
                {
                    var checker = new UpdateChecker();
                    var filePath = await checker.DownloadUpdateAsync(_updateInfo.DownloadUrl, _updateInfo.AssetFileName);

                    _statusText.Text = "✅ Download complete! Opening installer...";
                    _downloadBtn.Content = "✅  Downloaded!";

                    // Launch the installer / open the downloaded file
                    await Task.Delay(800);
                    Process.Start(filePath);
                    Close();
                }
                else
                {
                    // No direct asset — open the GitHub release page
                    _statusText.Text = "Opening GitHub release page...";
                    Process.Start(_updateInfo.ReleaseUrl);
                    await Task.Delay(1000);
                    Close();
                }
            }
            catch (Exception ex)
            {
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(230, 100, 100));
                _statusText.Text = $"❌ Download failed: {ex.Message}";
                _downloadBtn.Content = "⬇  Retry Download";
                _downloadBtn.IsEnabled = true;
            }
        }

        private void OnSkipClicked(object sender, RoutedEventArgs e)
        {
            UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
            Close();
        }
    }
}
