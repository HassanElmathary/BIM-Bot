using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI.Tools
{
    /// <summary>
    /// Live progress dialog shown during multi-format export.
    /// Processes each format sequentially and updates the UI in real-time.
    /// </summary>
    public class ExportProgressWindow : Window
    {
        private readonly TextBlock _titleText;
        private readonly TextBlock _statusText;
        private readonly TextBlock _detailText;
        private readonly Border _progressFill;
        private readonly Border _progressTrack;
        private readonly StackPanel _logPanel;
        private readonly ScrollViewer _logScroller;
        private readonly Button _closeBtn;
        private bool _isRunning;

        public ExportProgressWindow()
        {
            Title = "Export Progress";
            Width = 560;
            Height = 420;
            MinWidth = 480;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            var mainStack = new StackPanel { Margin = new Thickness(0) };

            // ── Header ──
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    DarkTheme.BrandPrimary,
                    DarkTheme.BrandDark,
                    45),
                Padding = new Thickness(24, 16, 24, 16),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerStack = new StackPanel();
            _titleText = new TextBlock
            {
                Text = "⏳ Export in Progress...",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DarkTheme.FgWhite
            };
            _statusText = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 4, 0, 0)
            };
            headerStack.Children.Add(_titleText);
            headerStack.Children.Add(_statusText);
            header.Child = headerStack;
            mainStack.Children.Add(header);

            // ── Progress Bar ──
            var progressSection = new StackPanel { Margin = new Thickness(24, 16, 24, 8) };

            _progressTrack = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                Height = 12,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1)
            };
            _progressFill = new Border
            {
                Background = new LinearGradientBrush(
                    DarkTheme.BrandTeal,
                    DarkTheme.BrandPrimary,
                    0),
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            _progressTrack.Child = _progressFill;
            progressSection.Children.Add(_progressTrack);

            _detailText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            progressSection.Children.Add(_detailText);
            mainStack.Children.Add(progressSection);

            // ── Log Area ──
            var logBorder = new Border
            {
                Background = DarkTheme.BgDeep,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(24, 8, 24, 8),
                MaxHeight = 180
            };
            _logScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 8, 12, 8)
            };
            _logPanel = new StackPanel();
            _logScroller.Content = _logPanel;
            logBorder.Child = _logScroller;
            mainStack.Children.Add(logBorder);

            // ── Footer ──
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(24, 12, 24, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            _closeBtn = DarkTheme.MakeCancelButton("Cancel");
            _closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
            _closeBtn.Padding = new Thickness(24, 8, 24, 8);
            _closeBtn.Click += (s, e) =>
            {
                if (_isRunning)
                {
                    _isRunning = false;
                    AddLog("⚠️ Export cancelled by user.", DarkTheme.FgGold);
                    _statusText.Text = "Cancelled";
                    _titleText.Text = "⚠️ Export Cancelled";
                    _closeBtn.Content = "Close";
                }
                else
                {
                    Close();
                }
            };
            footer.Child = _closeBtn;
            mainStack.Children.Add(footer);

            Content = mainStack;
        }

        /// <summary>
        /// Run sequential exports for the given formats.
        /// Called from UI thread after ShowDialog is not suitable (we need modeless).
        /// </summary>
        public async void RunExports(List<string> formats, JObject baseParams)
        {
            _isRunning = true;
            int total = formats.Count;
            int completed = 0;
            var results = new List<string>();

            var mgr = Core.Application.EventManagerInstance;
            if (mgr == null)
            {
                AddLog("❌ Revit connection not available.", DarkTheme.FgWarning);
                _titleText.Text = "❌ Export Failed";
                _closeBtn.Content = "Close";
                _isRunning = false;
                return;
            }

            for (int i = 0; i < formats.Count; i++)
            {
                if (!_isRunning) break;

                var fmt = formats[i];
                var cmdName = GetCommandForFormat(fmt);

                // Update UI
                _statusText.Text = $"Exporting {fmt}... ({i + 1} of {total})";
                _detailText.Text = $"Step {i + 1}/{total} — {fmt} export";
                AddLog($"🔄 Starting {fmt} export...", DarkTheme.FgLight);
                UpdateProgress((double)i / total);

                // Allow UI to repaint
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                try
                {
                    var result = await mgr.ExecuteCommandAsync(cmdName, baseParams);
                    var msg = result?["message"]?.ToString() ?? $"{fmt} exported";
                    AddLog($"✅ {fmt}: {GetFirstLine(msg)}", DarkTheme.FgGreen);
                    results.Add(msg);
                    completed++;
                }
                catch (TimeoutException)
                {
                    AddLog($"⏱️ {fmt}: Timed out (Revit may be busy)", DarkTheme.FgGold);
                    results.Add($"⏱️ {fmt} timed out");
                }
                catch (Exception ex)
                {
                    AddLog($"❌ {fmt}: {ex.Message}", DarkTheme.FgWarning);
                    results.Add($"❌ {fmt}: {ex.Message}");
                }

                // Small delay between formats to let Revit settle
                if (i < formats.Count - 1 && _isRunning)
                {
                    await Task.Delay(500);
                }
            }

            // Final state
            UpdateProgress(1.0);
            _isRunning = false;
            _closeBtn.Content = "Close";

            if (completed == total)
            {
                _titleText.Text = "✅ Export Complete!";
                _statusText.Text = $"Successfully exported {completed} format(s)";
                AddLog($"\n🎉 All {completed} format(s) exported successfully!", DarkTheme.FgGreen);
            }
            else
            {
                _titleText.Text = $"⚠️ Export Finished ({completed}/{total})";
                _statusText.Text = $"{completed} of {total} format(s) completed";
                AddLog($"\n⚠️ {completed}/{total} format(s) completed.", DarkTheme.FgGold);
            }
        }

        private void UpdateProgress(double fraction)
        {
            var trackWidth = _progressTrack.ActualWidth > 0 ? _progressTrack.ActualWidth : 500;
            var targetWidth = Math.Max(0, Math.Min(trackWidth, trackWidth * fraction));

            var anim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _progressFill.BeginAnimation(WidthProperty, anim);
        }

        private void AddLog(string text, Brush color)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = color,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 2),
                TextWrapping = TextWrapping.Wrap
            };
            _logPanel.Children.Add(tb);
            _logScroller.ScrollToEnd();
        }

        private static string GetFirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var idx = s.IndexOf('\n');
            return idx > 0 ? s.Substring(0, idx) : s;
        }

        private static string GetCommandForFormat(string fmt)
        {
            switch (fmt.ToUpper())
            {
                case "PDF": return "export_to_pdf";
                case "DWG": return "export_to_dwg";
                case "DGN": return "export_to_dgn";
                case "DWF": return "export_to_dwf";
                case "NWC": return "export_to_nwc";
                case "IFC": return "export_to_ifc";
                case "IMG": return "export_to_images";
                default: return fmt.ToLower();
            }
        }
    }
}
