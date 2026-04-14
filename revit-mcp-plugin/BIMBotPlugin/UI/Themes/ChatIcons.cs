using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BIMBotPlugin.UI.Themes
{
    /// <summary>
    /// Sketch-style vector icons for the chat UI — drawn with WPF Path geometry.
    /// Colors aligned with the BIM Professional design system (DarkTheme.cs).
    /// </summary>
    public static class ChatIcons
    {
        // ── Icon Colors — aligned with DarkTheme.cs brand palette ──
        private static readonly SolidColorBrush Warm     = new SolidColorBrush(DarkTheme.BrandTeal);    // #06B6D4 Teal
        private static readonly SolidColorBrush Muted    = DarkTheme.FgDim;                             // #909296 Cool gray
        private static readonly SolidColorBrush Soft     = DarkTheme.FgLight;                           // #C1C2C5 Light gray
        private static readonly SolidColorBrush Accent   = new SolidColorBrush(DarkTheme.BrandPrimary); // #2563EB Vivid Blue
        private static readonly SolidColorBrush Success  = new SolidColorBrush(DarkTheme.BrandGreen);   // #10B981 Emerald
        private static readonly SolidColorBrush Danger   = new SolidColorBrush(DarkTheme.BrandRed);     // #EF4444 Red

        /// <summary>AI assistant sparkle icon — sketch style.</summary>
        public static UIElement Sparkle(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;

            // Main sparkle — 4-point star with rounded feel
            canvas.Children.Add(MakePath(
                $"M {10 * s},{1 * s} C {11 * s},{5 * s} {15 * s},{9 * s} {19 * s},{10 * s} " +
                $"C {15 * s},{11 * s} {11 * s},{15 * s} {10 * s},{19 * s} " +
                $"C {9 * s},{15 * s} {5 * s},{11 * s} {1 * s},{10 * s} " +
                $"C {5 * s},{9 * s} {9 * s},{5 * s} {10 * s},{1 * s} Z",
                fill ?? Accent, strokeThickness: 0));

            // Small accent dot
            canvas.Children.Add(MakeEllipse(2.5 * s, 2.5 * s, 16 * s, 3 * s, fill ?? Accent, 0.5));

            return canvas;
        }

        /// <summary>Send arrow icon — organic hand-drawn style.</summary>
        public static UIElement Send(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;

            canvas.Children.Add(MakePath(
                $"M {3 * s},{10 * s} L {17 * s},{10 * s} M {12 * s},{5 * s} L {17 * s},{10 * s} L {12 * s},{15 * s}",
                null, fill ?? Brushes.White, 2.0 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Stop square icon — rounded corners.</summary>
        public static UIElement Stop(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;

            canvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 10 * s, Height = 10 * s,
                RadiusX = 2 * s, RadiusY = 2 * s,
                Fill = fill ?? Brushes.White,
                Margin = new Thickness(5 * s, 5 * s, 0, 0)
            });

            return canvas;
        }

        /// <summary>Settings gear — simplified sketch style.</summary>
        public static UIElement Gear(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;
            var stroke = fill ?? Muted;

            // Outer gear teeth (simplified as a circle with notches)
            canvas.Children.Add(MakeEllipse(12 * s, 12 * s, 4 * s, 4 * s, null, 1.0, stroke, 1.5 * s));
            // Inner dot
            canvas.Children.Add(MakeEllipse(4 * s, 4 * s, 8 * s, 8 * s, null, 1.0, stroke, 1.5 * s));

            // Tick marks at compass points
            foreach (var angle in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
            {
                var rad = angle * System.Math.PI / 180;
                var x1 = 10 * s + 6 * s * System.Math.Cos(rad);
                var y1 = 10 * s + 6 * s * System.Math.Sin(rad);
                var x2 = 10 * s + 8.5 * s * System.Math.Cos(rad);
                var y2 = 10 * s + 8.5 * s * System.Math.Sin(rad);
                canvas.Children.Add(MakePath(
                    $"M {x1},{y1} L {x2},{y2}",
                    null, stroke, 2.0 * s, PenLineCap.Round));
            }

            return canvas;
        }

        /// <summary>Trash/clear icon — sketch wastebasket.</summary>
        public static UIElement Trash(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;
            var stroke = fill ?? Muted;

            // Lid
            canvas.Children.Add(MakePath(
                $"M {5 * s},{6 * s} L {15 * s},{6 * s}", null, stroke, 1.5 * s, PenLineCap.Round));
            canvas.Children.Add(MakePath(
                $"M {8 * s},{6 * s} L {8 * s},{4 * s} L {12 * s},{4 * s} L {12 * s},{6 * s}",
                null, stroke, 1.2 * s, PenLineCap.Round));
            // Body
            canvas.Children.Add(MakePath(
                $"M {6 * s},{6 * s} L {7 * s},{16 * s} L {13 * s},{16 * s} L {14 * s},{6 * s}",
                null, stroke, 1.2 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Link/chain icon for integrations.</summary>
        public static UIElement Link(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;
            var stroke = fill ?? Muted;

            canvas.Children.Add(MakePath(
                $"M {7 * s},{13 * s} L {4.5 * s},{13 * s} " +
                $"C {2 * s},{13 * s} {2 * s},{7 * s} {4.5 * s},{7 * s} L {7 * s},{7 * s}",
                null, stroke, 1.5 * s, PenLineCap.Round));
            canvas.Children.Add(MakePath(
                $"M {13 * s},{7 * s} L {15.5 * s},{7 * s} " +
                $"C {18 * s},{7 * s} {18 * s},{13 * s} {15.5 * s},{13 * s} L {13 * s},{13 * s}",
                null, stroke, 1.5 * s, PenLineCap.Round));
            canvas.Children.Add(MakePath(
                $"M {7 * s},{10 * s} L {13 * s},{10 * s}",
                null, stroke, 1.5 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>File/document icon.</summary>
        public static UIElement File(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;
            var stroke = fill ?? Muted;

            canvas.Children.Add(MakePath(
                $"M {5 * s},{3 * s} L {12 * s},{3 * s} L {15 * s},{6 * s} L {15 * s},{17 * s} " +
                $"L {5 * s},{17 * s} Z",
                null, stroke, 1.2 * s, PenLineCap.Round));
            canvas.Children.Add(MakePath(
                $"M {12 * s},{3 * s} L {12 * s},{6 * s} L {15 * s},{6 * s}",
                null, stroke, 1.2 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Lightning bolt for CLI mode.</summary>
        public static UIElement Bolt(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;

            canvas.Children.Add(MakePath(
                $"M {11 * s},{2 * s} L {5 * s},{11 * s} L {9 * s},{11 * s} L {8 * s},{18 * s} " +
                $"L {15 * s},{8 * s} L {11 * s},{8 * s} Z",
                fill ?? DarkTheme.FgGold, strokeThickness: 0));

            return canvas;
        }

        /// <summary>Robot icon for API mode.</summary>
        public static UIElement Robot(double size = 20, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 20.0;
            var stroke = fill ?? Soft;

            // Head
            canvas.Children.Add(MakePath(
                $"M {5 * s},{7 * s} L {5 * s},{14 * s} L {15 * s},{14 * s} L {15 * s},{7 * s} " +
                $"Q {15 * s},{5 * s} {13 * s},{5 * s} L {7 * s},{5 * s} Q {5 * s},{5 * s} {5 * s},{7 * s}",
                null, stroke, 1.2 * s, PenLineCap.Round));
            // Eyes
            canvas.Children.Add(MakeEllipse(2 * s, 2 * s, 7.5 * s, 8.5 * s, stroke));
            canvas.Children.Add(MakeEllipse(2 * s, 2 * s, 11.5 * s, 8.5 * s, stroke));
            // Antenna
            canvas.Children.Add(MakePath(
                $"M {10 * s},{5 * s} L {10 * s},{2 * s}",
                null, stroke, 1.2 * s, PenLineCap.Round));
            canvas.Children.Add(MakeEllipse(2 * s, 2 * s, 9 * s, 1 * s, stroke));
            // Mouth
            canvas.Children.Add(MakePath(
                $"M {8 * s},{12 * s} L {12 * s},{12 * s}",
                null, stroke, 1.0 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Tool/wrench icon for tool execution status.</summary>
        public static UIElement Tool(double size = 14, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 14.0;
            var stroke = fill ?? Muted;

            canvas.Children.Add(MakePath(
                $"M {3 * s},{11 * s} L {8 * s},{6 * s} " +
                $"C {9 * s},{4 * s} {11 * s},{3 * s} {12 * s},{4 * s} " +
                $"L {10 * s},{6 * s} L {11 * s},{7 * s} L {13 * s},{5 * s} " +
                $"C {14 * s},{7 * s} {12 * s},{10 * s} {10 * s},{10 * s} L {5 * s},{13 * s} Z",
                null, stroke, 1.0 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Checkmark for completed status.</summary>
        public static UIElement Check(double size = 14, Brush fill = null)
        {
            var canvas = MakeCanvas(size);
            var s = size / 14.0;

            canvas.Children.Add(MakePath(
                $"M {3 * s},{7 * s} L {6 * s},{10 * s} L {11 * s},{4 * s}",
                null, fill ?? Success, 1.8 * s, PenLineCap.Round));

            return canvas;
        }

        /// <summary>Creates a pulsing dot animation indicator.</summary>
        public static UIElement ThinkingDots(double size = 40)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = size / 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            for (int i = 0; i < 3; i++)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = Soft,
                    Opacity = 0.4 + (i * 0.2),
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(dot);
            }

            return panel;
        }

        // ── Helpers ──

        private static Canvas MakeCanvas(double size)
        {
            return new Canvas { Width = size, Height = size };
        }

        private static Path MakePath(string data, Brush fill = null, Brush stroke = null,
            double strokeThickness = 1.0, PenLineCap lineCap = PenLineCap.Flat)
        {
            return new Path
            {
                Data = Geometry.Parse(data),
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                StrokeStartLineCap = lineCap,
                StrokeEndLineCap = lineCap,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        private static Ellipse MakeEllipse(double w, double h, double left, double top,
            Brush fill = null, double opacity = 1.0, Brush stroke = null, double strokeThickness = 0)
        {
            var e = new Ellipse
            {
                Width = w, Height = h,
                Fill = fill,
                Opacity = opacity,
                Margin = new Thickness(left, top, 0, 0)
            };
            if (stroke != null)
            {
                e.Stroke = stroke;
                e.StrokeThickness = strokeThickness;
            }
            return e;
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
