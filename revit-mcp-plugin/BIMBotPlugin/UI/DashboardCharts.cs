using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// Lightweight native chart helpers for the BIM dashboard.
    /// All charts are built from basic WPF Canvas + Shape primitives.
    /// No external NuGet dependencies.
    /// </summary>
    internal static class DashboardCharts
    {
        // ── Score Gauge (circular arc 0-100%) ──────────────────────

        public static Canvas MakeScoreGauge(int score, double size = 120)
        {
            var canvas = new Canvas { Width = size, Height = size };
            var cx = size / 2; var cy = size / 2; var r = size / 2 - 8;
            var thickness = 10.0;

            // Background ring
            var bgRing = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = DarkTheme.BorderDim, StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(bgRing, cx - r); Canvas.SetTop(bgRing, cy - r);
            canvas.Children.Add(bgRing);

            // Foreground arc
            if (score > 0)
            {
                var angle = score / 100.0 * 360.0;
                var path = CreateArcPath(cx, cy, r, -90, -90 + angle, thickness, ScoreColor(score));
                canvas.Children.Add(path);
            }

            // Center text
            var txt = new TextBlock
            {
                Text = score + "%",
                FontSize = size / 4.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = DarkTheme.DefaultFont
            };
            txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(txt, cx - txt.DesiredSize.Width / 2);
            Canvas.SetTop(txt, cy - txt.DesiredSize.Height / 2);
            canvas.Children.Add(txt);

            var sub = new TextBlock
            {
                Text = "Score",
                FontSize = 10, Foreground = DarkTheme.FgDim,
                FontFamily = DarkTheme.DefaultFont
            };
            sub.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(sub, cx - sub.DesiredSize.Width / 2);
            Canvas.SetTop(sub, cy + txt.DesiredSize.Height / 2 + 2);
            canvas.Children.Add(sub);

            return canvas;
        }

        // ── Horizontal Bar Chart ───────────────────────────────────

        public static FrameworkElement MakeBarChart(
            Dictionary<string, double> data, double width = 400, double barHeight = 22,
            SolidColorBrush barColor = null, bool showPercent = false)
        {
            if (data == null || data.Count == 0)
                return new TextBlock { Text = "No data", Foreground = DarkTheme.FgDim };

            var stack = new StackPanel();
            var maxVal = data.Values.Max();
            if (maxVal <= 0) maxVal = 1;
            barColor = barColor ?? new SolidColorBrush(DarkTheme.BrandPrimary);

            foreach (var kv in data)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

                var label = new TextBlock
                {
                    Text = Truncate(kv.Key, 18), FontSize = 11,
                    Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = DarkTheme.DefaultFont
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                var barW = (kv.Value / maxVal) * (width - 180);
                var bar = new Border
                {
                    Height = barHeight, Width = Math.Max(barW, 4),
                    Background = barColor, CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    ToolTip = $"{kv.Key}: {kv.Value:N0}"
                };
                Grid.SetColumn(bar, 1);
                row.Children.Add(bar);

                var val = new TextBlock
                {
                    Text = showPercent ? $"{kv.Value:N0}%" : $"{kv.Value:N0}",
                    FontSize = 11, Foreground = DarkTheme.FgDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Right,
                    FontFamily = DarkTheme.DefaultFont
                };
                Grid.SetColumn(val, 2);
                row.Children.Add(val);

                stack.Children.Add(row);
            }
            return stack;
        }

        // ── Doughnut Chart ─────────────────────────────────────────

        public static Canvas MakeDoughnut(Dictionary<string, double> data, double size = 160)
        {
            var canvas = new Canvas { Width = size + 120, Height = size + 20 };
            var cx = size / 2 + 10; var cy = size / 2 + 10;
            var r = size / 2 - 6; var thick = 20.0;

            if (data == null || data.Count == 0) return canvas;

            var total = data.Values.Sum();
            if (total <= 0) return canvas;

            var colors = new[] {
                DarkTheme.BrandPrimary, DarkTheme.BrandTeal, DarkTheme.BrandAmber,
                DarkTheme.BrandGreen, DarkTheme.BrandRed, Color.FromRgb(0xA7,0x8B,0xFA),
                Color.FromRgb(0xF4,0x72,0xB6), Color.FromRgb(0x38,0xBD,0xF8)
            };

            double startAngle = -90;
            int ci = 0;
            var legendY = 10.0;

            foreach (var kv in data)
            {
                var sweep = (kv.Value / total) * 360.0;
                if (sweep < 0.5) { ci++; continue; }

                var color = colors[ci % colors.Length];
                var path = CreateArcPath(cx, cy, r, startAngle, startAngle + sweep, thick,
                    new SolidColorBrush(color));
                path.ToolTip = $"{kv.Key}: {kv.Value:N0} ({kv.Value / total * 100:N0}%)";
                canvas.Children.Add(path);

                // Legend dot + label
                var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) };
                Canvas.SetLeft(dot, size + 18); Canvas.SetTop(dot, legendY);
                canvas.Children.Add(dot);

                var lbl = new TextBlock
                {
                    Text = $"{Truncate(kv.Key, 12)} ({kv.Value:N0})",
                    FontSize = 10, Foreground = DarkTheme.FgLight,
                    FontFamily = DarkTheme.DefaultFont
                };
                Canvas.SetLeft(lbl, size + 30); Canvas.SetTop(lbl, legendY - 2);
                canvas.Children.Add(lbl);

                legendY += 18;
                startAngle += sweep;
                ci++;
            }
            return canvas;
        }

        // ── Stacked Status Bar (pass/warn/fail) ────────────────────

        public static FrameworkElement MakeStatusBar(int pass, int warn, int fail, double width = 300)
        {
            var total = pass + warn + fail;
            if (total <= 0) total = 1;
            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            void AddSegment(int count, SolidColorBrush color, string label)
            {
                if (count <= 0) return;
                var w = (double)count / total * width;
                var seg = new Border
                {
                    Width = Math.Max(w, 3), Height = 14,
                    Background = color, ToolTip = $"{label}: {count}"
                };
                stack.Children.Add(seg);
            }

            AddSegment(pass, DarkTheme.FgGreen, "Pass");
            AddSegment(warn, DarkTheme.FgGold, "Warning");
            AddSegment(fail, new SolidColorBrush(DarkTheme.BrandRed), "Fail");

            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = stack,
                Margin = new Thickness(0, 4, 0, 4)
            };
            return border;
        }

        // ── Arc path helper ────────────────────────────────────────

        private static Path CreateArcPath(double cx, double cy, double r,
            double startDeg, double endDeg, double thickness, Brush stroke)
        {
            var startRad = startDeg * Math.PI / 180;
            var endRad = endDeg * Math.PI / 180;

            var x1 = cx + r * Math.Cos(startRad);
            var y1 = cy + r * Math.Sin(startRad);
            var x2 = cx + r * Math.Cos(endRad);
            var y2 = cy + r * Math.Sin(endRad);

            var largeArc = (endDeg - startDeg) > 180;

            var fig = new PathFigure { StartPoint = new Point(x1, y1), IsClosed = false };
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(x2, y2),
                Size = new Size(r, r),
                IsLargeArc = largeArc,
                SweepDirection = SweepDirection.Clockwise
            });

            var geom = new PathGeometry();
            geom.Figures.Add(fig);

            return new Path
            {
                Data = geom,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
        }

        // ── Utilities ──────────────────────────────────────────────

        public static SolidColorBrush ScoreColor(int score)
        {
            if (score >= 80) return DarkTheme.FgGreen;
            if (score >= 60) return DarkTheme.FgGold;
            return new SolidColorBrush(DarkTheme.BrandRed);
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max - 1) + "…";
        }
    }
}
