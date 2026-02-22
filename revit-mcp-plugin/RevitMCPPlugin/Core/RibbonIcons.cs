using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Generates ribbon button icons programmatically using WPF drawing.
    /// No external image files needed — all icons are vector-drawn at runtime.
    /// </summary>
    public static class RibbonIcons
    {
        // === Color Palette ===
        private static readonly Color Green   = Color.FromRgb(0x4C, 0xAF, 0x50);
        private static readonly Color Purple  = Color.FromRgb(0x7C, 0x4D, 0xFF);
        private static readonly Color Orange  = Color.FromRgb(0xFF, 0x98, 0x00);
        private static readonly Color Teal    = Color.FromRgb(0x00, 0xBC, 0xD4);
        private static readonly Color Amber   = Color.FromRgb(0xFF, 0xC1, 0x07);
        private static readonly Color Blue    = Color.FromRgb(0x21, 0x96, 0xF3);
        private static readonly Color Indigo  = Color.FromRgb(0x3F, 0x51, 0xB5);
        private static readonly Color Gray    = Color.FromRgb(0x9E, 0x9E, 0x9E);
        private static readonly Color Cyan    = Color.FromRgb(0x00, 0xAC, 0xC1);

        // ===== Public API =====

        /// <summary>Start MCP Service — green play triangle</summary>
        public static BitmapSource StartService(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Green);
                // Play triangle
                var m = s * 0.30;
                var pts = new[]
                {
                    new Point(s * 0.38, s * 0.25),
                    new Point(s * 0.38, s * 0.75),
                    new Point(s * 0.78, s * 0.50),
                };
                var geo = MakePolygon(pts);
                dc.DrawGeometry(Brushes.White, null, geo);
            });
        }

        /// <summary>AI Chat — purple speech bubble</summary>
        public static BitmapSource Chat(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Purple);
                // Speech bubble body (rounded rect)
                var rect = new Rect(s * 0.22, s * 0.22, s * 0.56, s * 0.40);
                dc.DrawRoundedRectangle(Brushes.White, null, rect, 3, 3);
                // Tail triangle
                var tail = MakePolygon(new[]
                {
                    new Point(s * 0.35, s * 0.62),
                    new Point(s * 0.30, s * 0.75),
                    new Point(s * 0.50, s * 0.62),
                });
                dc.DrawGeometry(Brushes.White, null, tail);
                // Three dots
                double dotR = s * 0.035;
                double dotY = s * 0.42;
                dc.DrawEllipse(new SolidColorBrush(Purple), null, new Point(s * 0.38, dotY), dotR, dotR);
                dc.DrawEllipse(new SolidColorBrush(Purple), null, new Point(s * 0.50, dotY), dotR, dotR);
                dc.DrawEllipse(new SolidColorBrush(Purple), null, new Point(s * 0.62, dotY), dotR, dotR);
            });
        }

        /// <summary>Tools Hub — orange grid/wrench</summary>
        public static BitmapSource ToolsHub(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Orange);
                // 2x2 grid of rounded squares
                double gap = s * 0.06;
                double cellW = (s * 0.50 - gap) / 2;
                double left = s * 0.25;
                double top = s * 0.25;
                var brush = Brushes.White;
                double r = 2;
                dc.DrawRoundedRectangle(brush, null, new Rect(left, top, cellW, cellW), r, r);
                dc.DrawRoundedRectangle(brush, null, new Rect(left + cellW + gap, top, cellW, cellW), r, r);
                dc.DrawRoundedRectangle(brush, null, new Rect(left, top + cellW + gap, cellW, cellW), r, r);
                dc.DrawRoundedRectangle(brush, null, new Rect(left + cellW + gap, top + cellW + gap, cellW, cellW), r, r);
            });
        }

        /// <summary>Export — teal upward arrow</summary>
        public static BitmapSource Export(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Teal);
                var pen = new Pen(Brushes.White, s * 0.08) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                // Vertical line
                dc.DrawLine(pen, new Point(s * 0.50, s * 0.30), new Point(s * 0.50, s * 0.70));
                // Arrow head
                dc.DrawLine(pen, new Point(s * 0.35, s * 0.45), new Point(s * 0.50, s * 0.30));
                dc.DrawLine(pen, new Point(s * 0.65, s * 0.45), new Point(s * 0.50, s * 0.30));
                // Base line
                dc.DrawLine(pen, new Point(s * 0.28, s * 0.72), new Point(s * 0.72, s * 0.72));
            });
        }

        /// <summary>Families — amber house shape</summary>
        public static BitmapSource Families(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Amber);
                // House roof
                var roof = MakePolygon(new[]
                {
                    new Point(s * 0.50, s * 0.22),
                    new Point(s * 0.25, s * 0.48),
                    new Point(s * 0.75, s * 0.48),
                });
                dc.DrawGeometry(Brushes.White, null, roof);
                // House body
                dc.DrawRectangle(Brushes.White, null, new Rect(s * 0.32, s * 0.48, s * 0.36, s * 0.30));
                // Door
                dc.DrawRectangle(new SolidColorBrush(Amber), null, new Rect(s * 0.43, s * 0.58, s * 0.14, s * 0.20));
            });
        }

        /// <summary>QuickViews — blue eye shape</summary>
        public static BitmapSource QuickViews(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Blue);
                // Eye outline (two arcs forming an eye)
                var eyeGeo = new StreamGeometry();
                using (var ctx = eyeGeo.Open())
                {
                    ctx.BeginFigure(new Point(s * 0.18, s * 0.50), true, true);
                    ctx.QuadraticBezierTo(new Point(s * 0.50, s * 0.25), new Point(s * 0.82, s * 0.50), true, false);
                    ctx.QuadraticBezierTo(new Point(s * 0.50, s * 0.75), new Point(s * 0.18, s * 0.50), true, false);
                }
                dc.DrawGeometry(Brushes.White, null, eyeGeo);
                // Pupil
                dc.DrawEllipse(new SolidColorBrush(Blue), null, new Point(s * 0.50, s * 0.50), s * 0.10, s * 0.10);
            });
        }

        /// <summary>Views & Sheets — indigo document</summary>
        public static BitmapSource ViewsSheets(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Indigo);
                // Document body
                var docGeo = new StreamGeometry();
                using (var ctx = docGeo.Open())
                {
                    ctx.BeginFigure(new Point(s * 0.28, s * 0.20), true, true);
                    ctx.LineTo(new Point(s * 0.60, s * 0.20), true, false);
                    ctx.LineTo(new Point(s * 0.72, s * 0.32), true, false);
                    ctx.LineTo(new Point(s * 0.72, s * 0.80), true, false);
                    ctx.LineTo(new Point(s * 0.28, s * 0.80), true, false);
                }
                dc.DrawGeometry(Brushes.White, null, docGeo);
                // Fold corner
                var fold = MakePolygon(new[]
                {
                    new Point(s * 0.60, s * 0.20),
                    new Point(s * 0.60, s * 0.32),
                    new Point(s * 0.72, s * 0.32),
                });
                dc.DrawGeometry(new SolidColorBrush(Indigo), new Pen(Brushes.White, 0.5), fold);
                // Text lines
                var lineBrush = new SolidColorBrush(Indigo);
                dc.DrawRectangle(lineBrush, null, new Rect(s * 0.35, s * 0.44, s * 0.30, s * 0.04));
                dc.DrawRectangle(lineBrush, null, new Rect(s * 0.35, s * 0.54, s * 0.25, s * 0.04));
                dc.DrawRectangle(lineBrush, null, new Rect(s * 0.35, s * 0.64, s * 0.28, s * 0.04));
            });
        }

        /// <summary>Settings — gray gear</summary>
        public static BitmapSource Settings(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Gray);
                double cx = s * 0.50, cy = s * 0.50;
                double outerR = s * 0.32, innerR = s * 0.22;
                int teeth = 8;

                // Gear outline
                var gear = new StreamGeometry();
                using (var ctx = gear.Open())
                {
                    bool first = true;
                    for (int i = 0; i < teeth; i++)
                    {
                        double a1 = (2 * Math.PI * i / teeth) - Math.PI / teeth * 0.4;
                        double a2 = (2 * Math.PI * i / teeth) + Math.PI / teeth * 0.4;
                        double a3 = (2 * Math.PI * (i + 0.5) / teeth) - Math.PI / teeth * 0.4;
                        double a4 = (2 * Math.PI * (i + 0.5) / teeth) + Math.PI / teeth * 0.4;

                        var p1 = new Point(cx + outerR * Math.Cos(a1), cy + outerR * Math.Sin(a1));
                        var p2 = new Point(cx + outerR * Math.Cos(a2), cy + outerR * Math.Sin(a2));
                        var p3 = new Point(cx + innerR * Math.Cos(a3), cy + innerR * Math.Sin(a3));
                        var p4 = new Point(cx + innerR * Math.Cos(a4), cy + innerR * Math.Sin(a4));

                        if (first) { ctx.BeginFigure(p1, true, true); first = false; }
                        else { ctx.LineTo(p1, true, false); }
                        ctx.LineTo(p2, true, false);
                        ctx.LineTo(p3, true, false);
                        ctx.LineTo(p4, true, false);
                    }
                }
                dc.DrawGeometry(Brushes.White, null, gear);
                // Center hole
                dc.DrawEllipse(new SolidColorBrush(Gray), null, new Point(cx, cy), s * 0.10, s * 0.10);
            });
        }

        /// <summary>Check Updates — cyan download arrow</summary>
        public static BitmapSource CheckUpdates(int size = 32)
        {
            return Render(size, (dc, s) =>
            {
                DrawCircleBg(dc, s, Cyan);
                var pen = new Pen(Brushes.White, s * 0.08) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                // Down arrow
                dc.DrawLine(pen, new Point(s * 0.50, s * 0.25), new Point(s * 0.50, s * 0.62));
                dc.DrawLine(pen, new Point(s * 0.35, s * 0.50), new Point(s * 0.50, s * 0.65));
                dc.DrawLine(pen, new Point(s * 0.65, s * 0.50), new Point(s * 0.50, s * 0.65));
                // Base
                dc.DrawLine(pen, new Point(s * 0.28, s * 0.75), new Point(s * 0.72, s * 0.75));
            });
        }

        // ===== Generic sub-item icons for pulldown menus =====

        /// <summary>Small colored circle icon for pulldown sub-items</summary>
        public static BitmapSource SubItem(int size, Color accent, string letter)
        {
            return Render(size, (dc, s) =>
            {
                // Colored circle
                dc.DrawEllipse(new SolidColorBrush(accent), null, new Point(s * 0.5, s * 0.5), s * 0.42, s * 0.42);
                // Letter
                var text = new FormattedText(
                    letter,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    s * 0.50,
                    Brushes.White
#if !NET40
                    , 96
#endif
                );
                dc.DrawText(text, new Point((s - text.Width) / 2, (s - text.Height) / 2));
            });
        }

        // ===== Helpers =====

        private static void DrawCircleBg(DrawingContext dc, double s, Color color)
        {
            dc.DrawEllipse(new SolidColorBrush(color), null, new Point(s * 0.5, s * 0.5), s * 0.48, s * 0.48);
        }

        private static StreamGeometry MakePolygon(Point[] pts)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++)
                    ctx.LineTo(pts[i], true, false);
            }
            geo.Freeze();
            return geo;
        }

        private static BitmapSource Render(int size, Action<DrawingContext, double> draw)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                draw(dc, size);
            }
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }
    }
}
