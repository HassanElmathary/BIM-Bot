using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.AI;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    /// <summary>
    /// Model manager window for Ollama local AI — shows available models with install status.
    /// ✅ = installed, ☁️ = available to download. Users can download and activate models.
    /// </summary>
    public class OllamaModelManagerWindow : Window
    {
        // Curated model catalog
        private static readonly ModelInfo[] ModelCatalog = new[]
        {
            new ModelInfo("qwen2.5:7b",        "4.7 GB", "Best tool calling — recommended for Revit"),
            new ModelInfo("qwen2.5:14b",       "9 GB",   "Smarter reasoning, needs 16 GB RAM"),
            new ModelInfo("llama3.1:8b",        "4.7 GB", "General purpose, good all-rounder"),
            new ModelInfo("mistral:7b",         "4 GB",   "Fast & lightweight"),
            new ModelInfo("codellama:7b",       "3.8 GB", "Code-focused tasks"),
            new ModelInfo("qwen2.5-coder:7b",   "4.7 GB", "Code generation specialist"),
        };

        private readonly StackPanel _modelListPanel;
        private readonly TextBlock _statusText;
        private readonly Dictionary<string, ModelCard> _cards = new Dictionary<string, ModelCard>();
        private string _ollamaPath;

        public OllamaModelManagerWindow()
        {
            Title = "Local AI Models (Ollama)";
            Width = 520;
            Height = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            _ollamaPath = FindOllamaPath();

            var root = new StackPanel { Margin = new Thickness(24) };

            // Title
            root.Children.Add(new TextBlock
            {
                Text = "🤖 Local AI Models",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 0, 0, 4)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Free • Unlimited • Private — powered by Ollama",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Status
            _statusText = new TextBlock
            {
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(_statusText);

            // Scrollable model list
            _modelListPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = _modelListPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 380
            };
            root.Children.Add(scroll);

            // Close button
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var closeBtn = DarkTheme.MakeCancelButton("Close");
            closeBtn.Click += (s, e) => Close();
            btnPanel.Children.Add(closeBtn);
            root.Children.Add(btnPanel);

            Content = root;

            // Load model status
            Loaded += (s, e) => RefreshModelStatus();
        }

        private string FindOllamaPath()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = System.IO.Path.Combine(localApp, "Programs", "Ollama", "ollama.exe");
            if (System.IO.File.Exists(path)) return path;

            // Try PATH
            try
            {
                var psi = new ProcessStartInfo("ollama", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0) return "ollama";
            }
            catch { }

            return null;
        }

        private void RefreshModelStatus()
        {
            _modelListPanel.Children.Clear();
            _cards.Clear();

            if (_ollamaPath == null)
            {
                _statusText.Text = "⚠️ Ollama not found — install from ollama.com";
                _statusText.Foreground = DarkTheme.FgWarning;

                var installBtn = DarkTheme.MakePrimaryButton("Download Ollama");
                installBtn.Margin = new Thickness(0, 8, 0, 0);
                installBtn.Click += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo("https://ollama.com/download") { UseShellExecute = true }); }
                    catch { }
                };
                _modelListPanel.Children.Add(installBtn);
                return;
            }

            _statusText.Text = "Checking installed models...";
            _statusText.Foreground = DarkTheme.FgDim;

            // Get installed models
            var installed = GetInstalledModels();

            _statusText.Text = $"Ollama ready — {installed.Count} model{(installed.Count != 1 ? "s" : "")} installed";
            _statusText.Foreground = DarkTheme.FgGreen;

            foreach (var model in ModelCatalog)
            {
                var isInstalled = installed.Contains(model.Name);
                var card = new ModelCard(model, isInstalled, this);
                _cards[model.Name] = card;
                _modelListPanel.Children.Add(card.Element);
            }
        }

        private HashSet<string> GetInstalledModels()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo(_ollamaPath, "list")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(5000);

                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("NAME")) continue;
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                        installed.Add(parts[0]);
                }
            }
            catch { }
            return installed;
        }

        internal void PullModel(string modelName, ModelCard card)
        {
            card.SetStatus("⏳ Downloading...", DarkTheme.FgGold);
            card.DisableActions();

            var worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                try
                {
                    var psi = new ProcessStartInfo(_ollamaPath, $"pull {modelName}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var p = Process.Start(psi);
                    p?.WaitForExit(); // This blocks until download completes
                    e.Result = p?.ExitCode == 0;
                }
                catch
                {
                    e.Result = false;
                }
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                var success = e.Result is bool b && b;
                if (success)
                {
                    card.SetInstalled(true);
                    card.SetStatus("✅ Installed", DarkTheme.FgGreen);
                    // Update the status bar
                    var installed = GetInstalledModels();
                    _statusText.Text = $"Ollama ready — {installed.Count} model{(installed.Count != 1 ? "s" : "")} installed";
                }
                else
                {
                    card.SetStatus("❌ Download failed", DarkTheme.FgWarning);
                    card.EnableActions();
                }
            };
            worker.RunWorkerAsync();
        }

        internal void UseModel(string modelName)
        {
            var settings = GeminiSettings.Load();
            settings.Provider = "ollama";
            settings.Model = modelName;
            settings.Save();

            MessageBox.Show(
                $"✅ AI provider set to Ollama (Local)\nModel: {modelName}\n\nYour AI Chat will now use this model — free & unlimited!",
                "Local AI Activated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    // ── Model Info ──────────────────────────────────────────

    internal class ModelInfo
    {
        public string Name { get; }
        public string Size { get; }
        public string Description { get; }

        public ModelInfo(string name, string size, string description)
        {
            Name = name;
            Size = size;
            Description = description;
        }
    }

    // ── Model Card UI ──────────────────────────────────────

    internal class ModelCard
    {
        public Border Element { get; }
        private readonly TextBlock _statusIcon;
        private readonly TextBlock _statusLabel;
        private readonly Button _actionBtn;
        private bool _isInstalled;
        private readonly string _modelName;
        private readonly OllamaModelManagerWindow _parent;

        public ModelCard(ModelInfo model, bool isInstalled, OllamaModelManagerWindow parent)
        {
            _modelName = model.Name;
            _isInstalled = isInstalled;
            _parent = parent;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Status icon (✅ or ☁️)
            _statusIcon = new TextBlock
            {
                Text = isInstalled ? "✅" : "☁️",
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(_statusIcon, 0);
            grid.Children.Add(_statusIcon);

            // Model info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text = model.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgWhite
            });
            nameRow.Children.Add(new TextBlock
            {
                Text = $"  ({model.Size})",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            });
            infoStack.Children.Add(nameRow);

            infoStack.Children.Add(new TextBlock
            {
                Text = model.Description,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Status label (shown during download)
            _statusLabel = new TextBlock
            {
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed
            };
            infoStack.Children.Add(_statusLabel);

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Action button
            if (isInstalled)
            {
                _actionBtn = new Button
                {
                    Content = "Use",
                    Background = DarkTheme.BgAccent,
                    Foreground = DarkTheme.FgWhite,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(16, 6, 16, 6),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _actionBtn.MouseEnter += (s, e) => _actionBtn.Background = DarkTheme.BgAccentHover;
                _actionBtn.MouseLeave += (s, e) => _actionBtn.Background = DarkTheme.BgAccent;
                _actionBtn.Click += (s, e) => _parent.UseModel(_modelName);
            }
            else
            {
                _actionBtn = new Button
                {
                    Content = "⬇ Download",
                    Background = DarkTheme.BgCancel,
                    Foreground = DarkTheme.FgWhite,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 6, 12, 6),
                    FontSize = 12,
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _actionBtn.MouseEnter += (s, e) => _actionBtn.Background = DarkTheme.BgCancelHover;
                _actionBtn.MouseLeave += (s, e) => _actionBtn.Background = DarkTheme.BgCancel;
                _actionBtn.Click += (s, e) => _parent.PullModel(_modelName, this);
            }

            Grid.SetColumn(_actionBtn, 2);
            grid.Children.Add(_actionBtn);

            // Card border
            Element = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Child = grid
            };

            // Hover effect
            Element.MouseEnter += (s, e) => Element.Background = DarkTheme.BgCardHover;
            Element.MouseLeave += (s, e) => Element.Background = DarkTheme.BgCard;
        }

        public void SetInstalled(bool installed)
        {
            _isInstalled = installed;
            _statusIcon.Text = installed ? "✅" : "☁️";
            if (installed)
            {
                _actionBtn.Content = "Use";
                _actionBtn.Background = DarkTheme.BgAccent;
                _actionBtn.IsEnabled = true;
                _actionBtn.Click -= OnDownloadClick;
                _actionBtn.MouseEnter += (s, e) => _actionBtn.Background = DarkTheme.BgAccentHover;
                _actionBtn.MouseLeave += (s, e) => _actionBtn.Background = DarkTheme.BgAccent;
                _actionBtn.Click += (s, e) => _parent.UseModel(_modelName);
            }
        }

        public void SetStatus(string text, Brush color)
        {
            _statusLabel.Text = text;
            _statusLabel.Foreground = color;
            _statusLabel.Visibility = Visibility.Visible;
        }

        public void DisableActions() => _actionBtn.IsEnabled = false;
        public void EnableActions() => _actionBtn.IsEnabled = true;

        private void OnDownloadClick(object sender, RoutedEventArgs e)
        {
            _parent.PullModel(_modelName, this);
        }
    }
}
