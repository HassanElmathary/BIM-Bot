using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RevitMCPInstaller
{
    /// <summary>
    /// Premium dark-themed installer window with step-based flow.
    /// </summary>
    public class InstallerWindow : Window
    {
        // Color palette
        private static readonly SolidColorBrush BgDark = B(0x0F, 0x0F, 0x1A);
        private static readonly SolidColorBrush BgCard = B(0x1A, 0x1A, 0x2E);
        private static readonly SolidColorBrush BgHeader = B(0x12, 0x1B, 0x35);
        private static readonly SolidColorBrush BgAccent = B(0x6C, 0x5C, 0xE7);
        private static readonly SolidColorBrush BgAccentHover = B(0x5A, 0x4D, 0xCF);
        private static readonly SolidColorBrush BgGreen = B(0x00, 0xB8, 0x94);
        private static readonly SolidColorBrush BgRed = B(0xE1, 0x44, 0x44);
        private static readonly SolidColorBrush BgInput = B(0x16, 0x16, 0x28);
        private static readonly SolidColorBrush FgDim = B(0x6B, 0x7E, 0x9A);
        private static readonly SolidColorBrush FgLight = B(0xE0, 0xE0, 0xEE);
        private static readonly SolidColorBrush FgWhite = Brushes.White;
        private static readonly SolidColorBrush FgGold = B(0xFF, 0xD4, 0x3B);

        // State
        private readonly InstallerLogic _installer = new InstallerLogic();
        private List<RevitVersion> _versions = new List<RevitVersion>();
        private readonly List<CheckBox> _versionCheckboxes = new List<CheckBox>();

        // UI Panels
        private readonly Grid _mainGrid;
        private readonly StackPanel _welcomePanel;
        private readonly StackPanel _selectPanel;
        private readonly StackPanel _progressPanel;
        private readonly StackPanel _completePanel;
        private readonly StackPanel _uninstallPanel;

        // Progress UI
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _progressText;

        // Complete UI
        private readonly TextBlock _completeTitle;
        private readonly TextBlock _completeMessage;

        public InstallerWindow()
        {
            Title = "Revit MCP Installer — Chat with me";
            Width = 600;
            Height = 520;
            MinWidth = 500;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgDark;
            Foreground = FgWhite;
            FontFamily = new FontFamily("Segoe UI");
            ResizeMode = ResizeMode.CanMinimize;

            _mainGrid = new Grid();

            // Create all panels
            _welcomePanel = CreateWelcomePanel();
            _selectPanel = CreateSelectPanel();
            _progressPanel = CreateProgressPanel(out _progressBar, out _progressText);
            _completePanel = CreateCompletePanel(out _completeTitle, out _completeMessage);
            _uninstallPanel = CreateUninstallPanel();

            // Add all panels (only welcome visible initially)
            _mainGrid.Children.Add(_welcomePanel);
            _mainGrid.Children.Add(_selectPanel);
            _mainGrid.Children.Add(_progressPanel);
            _mainGrid.Children.Add(_completePanel);
            _mainGrid.Children.Add(_uninstallPanel);

            _selectPanel.Visibility = Visibility.Collapsed;
            _progressPanel.Visibility = Visibility.Collapsed;
            _completePanel.Visibility = Visibility.Collapsed;
            _uninstallPanel.Visibility = Visibility.Collapsed;

            Content = _mainGrid;

            // Detect Revit versions on load
            Loaded += (s, e) =>
            {
                _versions = _installer.DetectRevitVersions();
                PopulateVersions();

                // If already installed, show uninstall option
                if (InstallerLogic.IsAlreadyInstalled())
                {
                    ShowPanel(_uninstallPanel);
                }
            };
        }

        // ==================== WELCOME PANEL ====================
        private StackPanel CreateWelcomePanel()
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            // Logo
            panel.Children.Add(new TextBlock
            {
                Text = "🏗️",
                FontSize = 56,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Chat with me",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "AI-Powered Revit Assistant",
                FontSize = 15,
                Foreground = BgAccent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "by Hassan Ahmed Elmathary",
                FontSize = 12,
                Foreground = FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "hassan.elmathary@gmail.com",
                FontSize = 11,
                Foreground = FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // LinkedIn icon button
            var linkedInBtn = new Button
            {
                Width = 32,
                Height = 32,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                Background = B(0x00, 0x77, 0xB5), // LinkedIn blue
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Content = "in",
                ToolTip = "Visit LinkedIn Profile"
            };
            linkedInBtn.Template = MakeRoundButtonTemplate(16);
            var linkedInNormal = B(0x00, 0x77, 0xB5);
            var linkedInHover = B(0x00, 0x5E, 0x93);
            linkedInBtn.MouseEnter += (s, e) => linkedInBtn.Background = linkedInHover;
            linkedInBtn.MouseLeave += (s, e) => linkedInBtn.Background = linkedInNormal;
            linkedInBtn.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://www.linkedin.com/in/hassan-elmathary/") { UseShellExecute = true }); }
                catch { }
            };
            panel.Children.Add(linkedInBtn);

            // Description
            var descBorder = new Border
            {
                Background = BgCard,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 24)
            };
            descBorder.Child = new TextBlock
            {
                Text = "This installer will set up the Revit MCP Plugin on your computer.\n\n" +
                       "✦  AI chat panel inside Revit\n" +
                       "✦  44+ tools for model analysis & modification\n" +
                       "✦  Supports Gemini & DeepSeek AI providers\n" +
                       "✦  Automatic Revit version detection",
                FontSize = 13,
                Foreground = FgLight,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            panel.Children.Add(descBorder);

            // Install button
            var installBtn = MakeButton("Install", BgAccent, BgAccentHover, 300);
            installBtn.Click += (s, e) => ShowPanel(_selectPanel);
            panel.Children.Add(installBtn);

            return panel;
        }

        // ==================== SELECT VERSIONS PANEL ====================
        private StackPanel CreateSelectPanel()
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Select Revit Versions",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "We detected the following Revit installations on your PC.\nSelect which versions to install the plugin for:",
                FontSize = 13,
                Foreground = FgDim,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Versions container (populated in Loaded)
            var versionsBorder = new Border
            {
                Background = BgCard,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 20),
                MinHeight = 80,
                Tag = "versionsContainer"
            };
            var versionsStack = new StackPanel();
            versionsBorder.Child = versionsStack;
            panel.Children.Add(versionsBorder);

            // Buttons row
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var backBtn = MakeButton("← Back", BgCard, B(0x2A, 0x2A, 0x4A), 120);
            backBtn.Click += (s, e) =>
            {
                if (InstallerLogic.IsAlreadyInstalled())
                    ShowPanel(_uninstallPanel);
                else
                    ShowPanel(_welcomePanel);
            };
            backBtn.Margin = new Thickness(0, 0, 12, 0);
            btnRow.Children.Add(backBtn);

            var nextBtn = MakeButton("Install Now →", BgAccent, BgAccentHover, 180);
            nextBtn.Click += async (s, e) => await RunInstall();
            btnRow.Children.Add(nextBtn);

            panel.Children.Add(btnRow);

            return panel;
        }

        private void PopulateVersions()
        {
            // Find the versions container
            var border = _selectPanel.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag?.ToString() == "versionsContainer");
            if (border?.Child is StackPanel stack)
            {
                stack.Children.Clear();
                _versionCheckboxes.Clear();

                if (_versions.Count == 0)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "⚠️ No Revit installations detected.\nYou can still install manually by selecting versions below.",
                        Foreground = FgGold,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12)
                    });

                    // Add manual year options
                    for (int year = 2022; year <= 2026; year++)
                    {
                        var cb = MakeVersionCheckbox(year, false);
                        _versionCheckboxes.Add(cb);
                        stack.Children.Add(cb);
                    }
                }
                else
                {
                    foreach (var v in _versions)
                    {
                        var cb = MakeVersionCheckbox(v.Year, v.IsSelected);
                        _versionCheckboxes.Add(cb);
                        stack.Children.Add(cb);
                    }
                }
            }
        }

        private CheckBox MakeVersionCheckbox(int year, bool isChecked)
        {
            return new CheckBox
            {
                Content = $"  Revit {year}",
                IsChecked = isChecked,
                Tag = year,
                FontSize = 14,
                Foreground = FgLight,
                Margin = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.Hand
            };
        }

        // ==================== PROGRESS PANEL ====================
        private StackPanel CreateProgressPanel(out ProgressBar bar, out TextBlock txt)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Installing...",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            });

            // Progress bar
            bar = new ProgressBar
            {
                Height = 8,
                Margin = new Thickness(0, 0, 0, 16),
                Background = BgCard,
                Foreground = BgAccent,
                Maximum = 100,
                Value = 0
            };
            panel.Children.Add(bar);

            txt = new TextBlock
            {
                Text = "Preparing...",
                FontSize = 13,
                Foreground = FgDim,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(txt);

            return panel;
        }

        // ==================== COMPLETE PANEL ====================
        private StackPanel CreateCompletePanel(out TextBlock title, out TextBlock message)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            panel.Children.Add(new TextBlock
            {
                Text = "✅",
                FontSize = 56,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            title = new TextBlock
            {
                Text = "Installation Complete!",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            panel.Children.Add(title);

            var msgBorder = new Border
            {
                Background = BgCard,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 24)
            };
            message = new TextBlock
            {
                Text = "",
                FontSize = 13,
                Foreground = FgLight,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            msgBorder.Child = message;
            panel.Children.Add(msgBorder);

            var closeBtn = MakeButton("Close", BgAccent, BgAccentHover, 200);
            closeBtn.Click += (s, e) => Close();
            panel.Children.Add(closeBtn);

            return panel;
        }

        // ==================== UNINSTALL PANEL ====================
        private StackPanel CreateUninstallPanel()
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            panel.Children.Add(new TextBlock
            {
                Text = "🏗️",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Revit MCP is already installed",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "What would you like to do?",
                FontSize = 13,
                Foreground = FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            });

            var btnStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var reinstallBtn = MakeButton("🔄  Reinstall / Update", BgAccent, BgAccentHover, 280);
            reinstallBtn.Click += (s, e) => ShowPanel(_selectPanel);
            reinstallBtn.Margin = new Thickness(0, 0, 0, 12);
            btnStack.Children.Add(reinstallBtn);

            var uninstallBtn = MakeButton("🗑️  Uninstall", BgRed, B(0xC0, 0x32, 0x32), 280);
            uninstallBtn.Click += (s, e) =>
            {
                var result = MessageBox.Show(
                    "Are you sure you want to uninstall Revit MCP?\n\n" +
                    "This will remove the plugin from all Revit versions.",
                    "Confirm Uninstall",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        InstallerLogic.Uninstall();
                        _completeTitle.Text = "Uninstalled Successfully";
                        _completeMessage.Text = "Revit MCP has been removed from your computer.\n\n" +
                                                "Restart Revit to complete the removal.";
                        ShowPanel(_completePanel);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error during uninstall:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            uninstallBtn.Margin = new Thickness(0, 0, 0, 12);
            btnStack.Children.Add(uninstallBtn);

            var cancelBtn = MakeButton("Cancel", BgCard, B(0x2A, 0x2A, 0x4A), 280);
            cancelBtn.Click += (s, e) => Close();
            btnStack.Children.Add(cancelBtn);

            panel.Children.Add(btnStack);

            return panel;
        }

        // ==================== INSTALL LOGIC ====================
        private async Task RunInstall()
        {
            var selectedYears = _versionCheckboxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (int)cb.Tag)
                .ToList();

            if (selectedYears.Count == 0)
            {
                MessageBox.Show("Please select at least one Revit version.", "No Version Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowPanel(_progressPanel);

            // Set source dirs — look relative to the installer EXE
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _installer.PluginSourceDir = FindDir(baseDir, "plugin",
                System.IO.Path.Combine(baseDir, "..", "..", "revit-mcp-plugin", "RevitMCPPlugin", "bin", "Release", "net48"));
            _installer.ServerSourceDir = FindDir(baseDir, "server",
                System.IO.Path.Combine(baseDir, "..", "..", "revit-mcp-server"));
            _installer.NodeSourceDir = FindDir(baseDir, "nodejs",
                System.IO.Path.Combine(baseDir, "..", "nodejs"));

            _installer.OnProgress += msg => Dispatcher.Invoke(() => _progressText.Text = msg);
            _installer.OnPercentChanged += pct => Dispatcher.Invoke(() =>
            {
                var anim = new DoubleAnimation(pct, TimeSpan.FromMilliseconds(300));
                _progressBar.BeginAnimation(ProgressBar.ValueProperty, anim);
            });

            try
            {
                await Task.Run(() => _installer.Install(selectedYears));

                var yearsList = string.Join(", ", selectedYears);
                _completeTitle.Text = "Installation Complete!";
                _completeMessage.Text =
                    $"✅ Plugin installed for Revit: {yearsList}\n\n" +
                    "Next steps:\n" +
                    "1. Open Revit — look for \"Chat with me\" in the Add-ins tab\n" +
                    "2. Click ⚙️ Settings to enter your API key\n" +
                    "3. Start chatting with your AI assistant!\n\n" +
                    "Enjoy! — Hassan Ahmed Elmathary";
                ShowPanel(_completePanel);
            }
            catch (Exception ex)
            {
                _completeTitle.Text = "Installation Failed";
                _completeMessage.Text = $"❌ Error: {ex.Message}\n\n" +
                    "Try running the installer as Administrator.";
                _completeMessage.Foreground = B(0xFF, 0x6B, 0x6B);
                ShowPanel(_completePanel);
            }
        }

        private string FindDir(string baseDir, string subName, string fallback)
        {
            var direct = System.IO.Path.Combine(baseDir, subName);
            if (System.IO.Directory.Exists(direct)) return direct;
            var fb = System.IO.Path.GetFullPath(fallback);
            if (System.IO.Directory.Exists(fb)) return fb;
            return direct;
        }

        // ==================== HELPERS ====================
        private void ShowPanel(StackPanel target)
        {
            _welcomePanel.Visibility = Visibility.Collapsed;
            _selectPanel.Visibility = Visibility.Collapsed;
            _progressPanel.Visibility = Visibility.Collapsed;
            _completePanel.Visibility = Visibility.Collapsed;
            _uninstallPanel.Visibility = Visibility.Collapsed;
            target.Visibility = Visibility.Visible;
        }

        private Button MakeButton(string text, SolidColorBrush bg, SolidColorBrush hoverBg, double width)
        {
            var btn = new Button
            {
                Content = text,
                Width = width,
                Height = 44,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgWhite,
                Background = bg,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            btn.Template = MakeRoundButtonTemplate(12);
            btn.MouseEnter += (s, e) => btn.Background = hoverBg;
            btn.MouseLeave += (s, e) => btn.Background = bg;
            return btn;
        }

        private ControlTemplate MakeRoundButtonTemplate(double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
