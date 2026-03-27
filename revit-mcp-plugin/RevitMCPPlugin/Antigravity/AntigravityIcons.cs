using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitMCPPlugin.Antigravity
{
    /// <summary>
    /// Antigravity vector icons — drawn programmatically with WPF drawing.
    /// Deep blue/purple gradient "AG" branding for ribbon and chat header.
    /// </summary>
    public static class AntigravityIcons
    {
        // Antigravity brand colors
        private static readonly Color BrandDark   = Color.FromRgb(0x1A, 0x1A, 0x2E);  // Deep navy
        private static readonly Color BrandLight  = Color.FromRgb(0x53, 0x4B, 0xAE);  // Vivid purple
        private static readonly Color BrandAccent = Color.FromRgb(0x7C, 0x6F, 0xE0);  // Lavender accent

        /// <summary>
        /// Creates a ribbon-sized BitmapSource icon for the Antigravity button.
        /// Renders a rounded square with "AG" text in a deep blue-purple gradient.
        /// </summary>
        public static BitmapSource RibbonIcon(int size)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var rect = new Rect(0, 0, size, size);
                var cornerRadius = size / 5.0;

                // Background: gradient rounded rectangle
                var gradient = new LinearGradientBrush(BrandDark, BrandLight, 45);
                var geometry = new RectangleGeometry(rect, cornerRadius, cornerRadius);
                dc.DrawGeometry(gradient, null, geometry);

                // "AG" text
                var text = new FormattedText(
                    "AG",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    size * 0.42,
                    Brushes.White,
                    96);

                text.TextAlignment = TextAlignment.Center;
                var x = size / 2.0;
                var y = (size - text.Height) / 2.0;
                dc.DrawText(text, new Point(x, y));

                // Subtle inner glow line at top
                var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
                dc.DrawLine(glowPen,
                    new Point(cornerRadius, 1.5),
                    new Point(size - cornerRadius, 1.5));
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        /// <summary>
        /// Creates an inline UIElement icon for the chat window header.
        /// A small circle with "AG" for the conversation header.
        /// </summary>
        public static System.Windows.Controls.Canvas HeaderIcon(double size = 24)
        {
            var canvas = new System.Windows.Controls.Canvas { Width = size, Height = size };

            // Circle background
            var circle = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = new LinearGradientBrush(BrandDark, BrandLight, 45)
            };
            canvas.Children.Add(circle);

            // "AG" text
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "AG",
                FontSize = size * 0.36,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI")
            };

            // Center the text
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = textBlock.DesiredSize.Width;
            var textHeight = textBlock.DesiredSize.Height;
            System.Windows.Controls.Canvas.SetLeft(textBlock, (size - textWidth) / 2);
            System.Windows.Controls.Canvas.SetTop(textBlock, (size - textHeight) / 2);
            canvas.Children.Add(textBlock);

            return canvas;
        }
    }
}
