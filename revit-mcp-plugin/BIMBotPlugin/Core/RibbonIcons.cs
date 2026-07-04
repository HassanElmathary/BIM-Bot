using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Programmatic vector ribbon icons — pixel-perfect at any size.
    /// Color palette (BIM Professional):
    ///   Primary   #2563EB  (vivid blue)
    ///   Dark      #1E40AF  (depth)
    ///   Teal      #06B6D4  (accent)
    ///   Amber     #F59E0B  (highlight)
    ///   White     #FFFFFF  (surface details)
    /// </summary>
    public static class RibbonIcons
    {
        // ── Palette ──────────────────────────────────────────
        static readonly Color CPrimary = Color.FromRgb(0x25, 0x63, 0xEB);
        static readonly Color CDark    = Color.FromRgb(0x1E, 0x40, 0xAF);
        static readonly Color CTeal    = Color.FromRgb(0x06, 0xB6, 0xD4);
        static readonly Color CAmber   = Color.FromRgb(0xF5, 0x9E, 0x0B);
        static readonly Color CGreen   = Color.FromRgb(0x10, 0xB9, 0x81);
        static readonly Color CRed     = Color.FromRgb(0xEF, 0x44, 0x44);
        static readonly Color CSlate   = Color.FromRgb(0x47, 0x55, 0x69);

        static Brush B(Color c) => new SolidColorBrush(c);
        static Pen P(Color c, double w) => new Pen(new SolidColorBrush(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

        // ════════════════════════════════════════════════════
        // 1. START SERVICE — play triangle inside a circle
        // ════════════════════════════════════════════════════
        public static BitmapSource StartService(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.08; // margin
            double cx = s / 2.0, cy = s / 2.0, r = (s - 2 * m) / 2.0;

            // Outer circle
            dc.DrawEllipse(B(CPrimary), null, new Point(cx, cy), r, r);

            // Play triangle (white, centered slightly right)
            double ts = r * 0.55; // triangle half-size
            var tri = new StreamGeometry();
            using (var ctx = tri.Open())
            {
                ctx.BeginFigure(new Point(cx - ts * 0.4, cy - ts), true, true);
                ctx.LineTo(new Point(cx + ts * 0.8, cy), true, false);
                ctx.LineTo(new Point(cx - ts * 0.4, cy + ts), true, false);
            }
            tri.Freeze();
            dc.DrawGeometry(Brushes.White, null, tri);
        });

        // ════════════════════════════════════════════════════
        // 2. AI CHAT — speech bubble with sparkle
        // ════════════════════════════════════════════════════
        public static BitmapSource Chat(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.06;
            // Rounded rectangle bubble
            double bx = m, by = m, bw = s - 2 * m, bh = s * 0.68;
            double cr = s * 0.12; // corner radius
            var bubbleRect = new Rect(bx, by, bw, bh);
            dc.DrawRoundedRectangle(B(CPrimary), null, bubbleRect, cr, cr);

            // Tail triangle
            var tail = new StreamGeometry();
            using (var ctx = tail.Open())
            {
                ctx.BeginFigure(new Point(s * 0.22, by + bh), true, true);
                ctx.LineTo(new Point(s * 0.15, s - m), true, false);
                ctx.LineTo(new Point(s * 0.42, by + bh), true, false);
            }
            tail.Freeze();
            dc.DrawGeometry(B(CPrimary), null, tail);

            // Sparkle star (amber) — 4-point star
            double sx = s * 0.55, sy = s * 0.38, sr = s * 0.14;
            DrawStar4(dc, sx, sy, sr, CAmber);
        });

        // ════════════════════════════════════════════════════
        // 3. PROJECT FILES — folder with document
        // ════════════════════════════════════════════════════
        public static BitmapSource ProjectFiles(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.08;
            // Folder back
            var folderBack = new StreamGeometry();
            using (var ctx = folderBack.Open())
            {
                double fx = m, fy = s * 0.22, fw = s - 2 * m, fh = s * 0.62;
                ctx.BeginFigure(new Point(fx, fy + s * 0.06), true, true);
                ctx.LineTo(new Point(fx, fy + fh), true, false);
                ctx.LineTo(new Point(fx + fw, fy + fh), true, false);
                ctx.LineTo(new Point(fx + fw, fy + s * 0.06), true, false);
                ctx.LineTo(new Point(fx + fw * 0.52, fy + s * 0.06), true, false);
                ctx.LineTo(new Point(fx + fw * 0.42, fy), true, false);
                ctx.LineTo(new Point(fx, fy), true, false);
            }
            folderBack.Freeze();
            dc.DrawGeometry(B(CDark), null, folderBack);

            // Folder front (lighter)
            var folderFront = new Rect(m, s * 0.38, s - 2 * m, s * 0.48);
            dc.DrawRoundedRectangle(B(CPrimary), null, folderFront, s * 0.04, s * 0.04);

            // Small doc page (white)
            double dx = s * 0.55, dy = s * 0.12, dw = s * 0.28, dh = s * 0.35;
            dc.DrawRoundedRectangle(Brushes.White, P(CSlate, s * 0.02), new Rect(dx, dy, dw, dh), s * 0.02, s * 0.02);
            // Lines on doc
            double lx = dx + dw * 0.15, lw = dw * 0.7;
            dc.DrawLine(P(CSlate, s * 0.02), new Point(lx, dy + dh * 0.3), new Point(lx + lw, dy + dh * 0.3));
            dc.DrawLine(P(CSlate, s * 0.02), new Point(lx, dy + dh * 0.5), new Point(lx + lw, dy + dh * 0.5));
            dc.DrawLine(P(CSlate, s * 0.02), new Point(lx, dy + dh * 0.7), new Point(lx + lw * 0.6, dy + dh * 0.7));
        });

        // ════════════════════════════════════════════════════
        // 4. CONNECT CLAUDE — two connected nodes
        // ════════════════════════════════════════════════════
        public static BitmapSource ConnectClaude(int size = 32) => Render(size, (dc, s) =>
        {
            double nr = s * 0.15; // node radius
            var p1 = new Point(s * 0.28, s * 0.32);
            var p2 = new Point(s * 0.72, s * 0.68);

            // Connection line
            dc.DrawLine(P(CTeal, s * 0.08), p1, p2);

            // Node 1 (primary)
            dc.DrawEllipse(B(CPrimary), P(Colors.White, s * 0.03), p1, nr, nr);
            // Node 2 (teal)
            dc.DrawEllipse(B(CTeal), P(Colors.White, s * 0.03), p2, nr, nr);

            // Small sparkle between
            DrawStar4(dc, s * 0.50, s * 0.42, s * 0.07, CAmber);
        });

        // ════════════════════════════════════════════════════
        // 5. LOCAL AI — chip / CPU
        // ════════════════════════════════════════════════════
        public static BitmapSource LocalAI(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.22;
            // Main chip body
            dc.DrawRoundedRectangle(B(CPrimary), null, new Rect(m, m, s - 2 * m, s - 2 * m), s * 0.06, s * 0.06);

            // Inner circle (teal accent)
            dc.DrawEllipse(B(CTeal), null, new Point(s / 2.0, s / 2.0), s * 0.12, s * 0.12);

            // Pins — top, bottom, left, right (3 each)
            var pinPen = P(CDark, s * 0.06);
            double pinL = s * 0.10;
            for (int i = 0; i < 3; i++)
            {
                double offset = s * 0.34 + i * s * 0.14;
                // Top pins
                dc.DrawLine(pinPen, new Point(offset, m), new Point(offset, m - pinL));
                // Bottom pins
                dc.DrawLine(pinPen, new Point(offset, s - m), new Point(offset, s - m + pinL));
                // Left pins
                dc.DrawLine(pinPen, new Point(m, offset), new Point(m - pinL, offset));
                // Right pins
                dc.DrawLine(pinPen, new Point(s - m, offset), new Point(s - m + pinL, offset));
            }
        });

        // ════════════════════════════════════════════════════
        // 6. TOOLS HUB — 2×2 grid of squares
        // ════════════════════════════════════════════════════
        public static BitmapSource ToolsHub(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.10, gap = s * 0.08;
            double cellW = (s - 2 * m - gap) / 2.0;
            double cr = s * 0.06;

            Color[] colors = { CPrimary, CTeal, CAmber, CDark };
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 2; col++)
                {
                    double x = m + col * (cellW + gap);
                    double y = m + row * (cellW + gap);
                    dc.DrawRoundedRectangle(B(colors[row * 2 + col]), null, new Rect(x, y, cellW, cellW), cr, cr);
                }
        });

        // ════════════════════════════════════════════════════
        // 7. EXPORT — arrow up from tray
        // ════════════════════════════════════════════════════
        public static BitmapSource Export(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.12;
            // Tray (U shape)
            var tray = new StreamGeometry();
            using (var ctx = tray.Open())
            {
                double ty = s * 0.52;
                ctx.BeginFigure(new Point(m, ty), false, false);
                ctx.LineTo(new Point(m, s - m), true, false);
                ctx.LineTo(new Point(s - m, s - m), true, false);
                ctx.LineTo(new Point(s - m, ty), true, false);
            }
            tray.Freeze();
            dc.DrawGeometry(null, P(CPrimary, s * 0.07), tray);

            // Arrow shaft
            double cx = s / 2.0;
            dc.DrawLine(P(CPrimary, s * 0.07), new Point(cx, s * 0.58), new Point(cx, s * 0.14));

            // Arrow head
            var head = new StreamGeometry();
            using (var ctx = head.Open())
            {
                ctx.BeginFigure(new Point(cx - s * 0.15, s * 0.30), true, true);
                ctx.LineTo(new Point(cx, s * 0.10), true, false);
                ctx.LineTo(new Point(cx + s * 0.15, s * 0.30), true, false);
            }
            head.Freeze();
            dc.DrawGeometry(B(CPrimary), null, head);
        });

        // ════════════════════════════════════════════════════
        // 8. FAMILIES — nested component squares
        // ════════════════════════════════════════════════════
        public static BitmapSource Families(int size = 32) => Render(size, (dc, s) =>
        {
            double m = s * 0.08;
            double cr = s * 0.05;
            // Back large square
            dc.DrawRoundedRectangle(B(CDark), null, new Rect(m, m, s * 0.60, s * 0.60), cr, cr);
            // Middle square (offset)
            dc.DrawRoundedRectangle(B(CPrimary), null, new Rect(s * 0.20, s * 0.20, s * 0.55, s * 0.55), cr, cr);
            // Front small square (bright)
            dc.DrawRoundedRectangle(B(CTeal), null, new Rect(s * 0.35, s * 0.35, s * 0.55, s * 0.55), cr, cr);

            // Small "+" on front
            double px = s * 0.62, py = s * 0.62, ps = s * 0.12;
            dc.DrawLine(P(Colors.White, s * 0.05), new Point(px - ps, py), new Point(px + ps, py));
            dc.DrawLine(P(Colors.White, s * 0.05), new Point(px, py - ps), new Point(px, py + ps));
        });

        // ════════════════════════════════════════════════════
        // 9. QUICK VIEWS — eye shape
        // ════════════════════════════════════════════════════
        public static BitmapSource QuickViews(int size = 32) => Render(size, (dc, s) =>
        {
            double cx = s / 2.0, cy = s / 2.0;
            // Eye outline (almond shape using two arcs)
            var eye = new StreamGeometry();
            using (var ctx = eye.Open())
            {
                double ew = s * 0.42, eh = s * 0.25;
                ctx.BeginFigure(new Point(cx - ew, cy), true, true);
                ctx.ArcTo(new Point(cx + ew, cy), new Size(ew * 1.2, eh * 1.8), 0, false, SweepDirection.Clockwise, true, false);
                ctx.ArcTo(new Point(cx - ew, cy), new Size(ew * 1.2, eh * 1.8), 0, false, SweepDirection.Clockwise, true, false);
            }
            eye.Freeze();
            dc.DrawGeometry(B(CPrimary), null, eye);

            // Iris (white circle)
            dc.DrawEllipse(Brushes.White, null, new Point(cx, cy), s * 0.14, s * 0.14);
            // Pupil (dark)
            dc.DrawEllipse(B(CDark), null, new Point(cx, cy), s * 0.08, s * 0.08);
            // Highlight
            dc.DrawEllipse(Brushes.White, null, new Point(cx + s * 0.03, cy - s * 0.03), s * 0.03, s * 0.03);

            // Speed lines (amber)
            dc.DrawLine(P(CAmber, s * 0.03), new Point(s * 0.06, s * 0.18), new Point(s * 0.24, s * 0.26));
            dc.DrawLine(P(CAmber, s * 0.03), new Point(s * 0.76, s * 0.26), new Point(s * 0.94, s * 0.18));
        });

        // ════════════════════════════════════════════════════
        // 10. VIEWS & SHEETS — stacked pages
        // ════════════════════════════════════════════════════
        public static BitmapSource ViewsSheets(int size = 32) => Render(size, (dc, s) =>
        {
            double cr = s * 0.04;
            // Back page
            dc.DrawRoundedRectangle(B(CSlate), null, new Rect(s * 0.18, s * 0.06, s * 0.70, s * 0.72), cr, cr);
            // Middle page
            dc.DrawRoundedRectangle(B(CDark), null, new Rect(s * 0.12, s * 0.14, s * 0.70, s * 0.72), cr, cr);
            // Front page (primary)
            dc.DrawRoundedRectangle(B(CPrimary), null, new Rect(s * 0.06, s * 0.22, s * 0.70, s * 0.72), cr, cr);

            // Lines on front page (white)
            double lx = s * 0.16, ly = s * 0.38, lw = s * 0.48;
            dc.DrawLine(P(Colors.White, s * 0.03), new Point(lx, ly), new Point(lx + lw, ly));
            dc.DrawLine(P(Colors.White, s * 0.03), new Point(lx, ly + s * 0.12), new Point(lx + lw, ly + s * 0.12));
            dc.DrawLine(P(Colors.White, s * 0.03), new Point(lx, ly + s * 0.24), new Point(lx + lw * 0.7, ly + s * 0.24));
        });

        // ════════════════════════════════════════════════════
        // 11. SETTINGS — gear cog
        // ════════════════════════════════════════════════════
        public static BitmapSource Settings(int size = 32) => Render(size, (dc, s) =>
        {
            double cx = s / 2.0, cy = s / 2.0;
            // Gear body with teeth
            int teeth = 8;
            double outerR = s * 0.42, innerR = s * 0.32, toothW = 0.28;

            var gear = new StreamGeometry();
            using (var ctx = gear.Open())
            {
                bool first = true;
                for (int i = 0; i < teeth; i++)
                {
                    double angle = i * 2 * Math.PI / teeth;
                    double a1 = angle - toothW / 2;
                    double a2 = angle + toothW / 2;
                    double a3 = angle + Math.PI / teeth - toothW / 2;
                    double a4 = angle + Math.PI / teeth + toothW / 2;

                    var p1 = new Point(cx + outerR * Math.Cos(a1), cy + outerR * Math.Sin(a1));
                    var p2 = new Point(cx + outerR * Math.Cos(a2), cy + outerR * Math.Sin(a2));
                    var p3 = new Point(cx + innerR * Math.Cos(a3), cy + innerR * Math.Sin(a3));
                    var p4 = new Point(cx + innerR * Math.Cos(a4), cy + innerR * Math.Sin(a4));

                    if (first) { ctx.BeginFigure(p1, true, true); first = false; }
                    else ctx.LineTo(p1, true, false);
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(p3, true, false);
                    ctx.LineTo(p4, true, false);
                }
            }
            gear.Freeze();
            dc.DrawGeometry(B(CPrimary), null, gear);

            // Center hole
            dc.DrawEllipse(B(Colors.White), null, new Point(cx, cy), s * 0.11, s * 0.11);
            dc.DrawEllipse(B(CDark), null, new Point(cx, cy), s * 0.06, s * 0.06);
        });

        // ════════════════════════════════════════════════════
        // 12. CHECK UPDATES — circular refresh arrows
        // ════════════════════════════════════════════════════
        public static BitmapSource CheckUpdates(int size = 32) => Render(size, (dc, s) =>
        {
            double cx = s / 2.0, cy = s / 2.0, r = s * 0.32;
            var arcPen = P(CPrimary, s * 0.07);

            // Top arc (clockwise, ~270 degrees)
            var arc1 = new StreamGeometry();
            using (var ctx = arc1.Open())
            {
                double startAngle = -Math.PI * 0.8;
                double endAngle = Math.PI * 0.3;
                ctx.BeginFigure(new Point(cx + r * Math.Cos(startAngle), cy + r * Math.Sin(startAngle)), false, false);
                ctx.ArcTo(new Point(cx + r * Math.Cos(endAngle), cy + r * Math.Sin(endAngle)),
                    new Size(r, r), 0, true, SweepDirection.Clockwise, true, false);
            }
            arc1.Freeze();
            dc.DrawGeometry(null, arcPen, arc1);

            // Arrowhead at end of arc
            double aAngle = Math.PI * 0.3;
            var aPos = new Point(cx + r * Math.Cos(aAngle), cy + r * Math.Sin(aAngle));
            var arrow1 = new StreamGeometry();
            using (var ctx = arrow1.Open())
            {
                double aSize = s * 0.12;
                ctx.BeginFigure(new Point(aPos.X + aSize * 0.2, aPos.Y - aSize), true, true);
                ctx.LineTo(aPos, true, false);
                ctx.LineTo(new Point(aPos.X + aSize * 1.1, aPos.Y + aSize * 0.1), true, false);
            }
            arrow1.Freeze();
            dc.DrawGeometry(B(CPrimary), null, arrow1);

            // Small download arrow in center (teal)
            double as2 = s * 0.10;
            dc.DrawLine(P(CTeal, s * 0.06), new Point(cx, cy - as2), new Point(cx, cy + as2));
            var head2 = new StreamGeometry();
            using (var ctx = head2.Open())
            {
                ctx.BeginFigure(new Point(cx - s * 0.08, cy + as2 * 0.3), true, true);
                ctx.LineTo(new Point(cx, cy + as2 + s * 0.04), true, false);
                ctx.LineTo(new Point(cx + s * 0.08, cy + as2 * 0.3), true, false);
            }
            head2.Freeze();
            dc.DrawGeometry(B(CTeal), null, head2);
        });

        // ═══════════════════════════════════════════════════
        // Badge overlays for toggle state
        // ═══════════════════════════════════════════════════

        /// <summary>Overlay a green checkmark badge on the bottom-right of a base icon.</summary>
        public static BitmapSource WithCheckBadge(BitmapSource baseIcon, int size)
        {
            if (baseIcon == null) return null;
            return Render(size, (dc, s) =>
            {
                dc.DrawImage(baseIcon, new Rect(0, 0, s, s));

                double badgeR = s * 0.22;
                var center = new Point(s - badgeR - s * 0.02, s - badgeR - s * 0.02);
                dc.DrawEllipse(B(CGreen), P(Colors.White, s * 0.03), center, badgeR, badgeR);

                var pen = P(Colors.White, s * 0.05);
                double bx = center.X, by = center.Y, br = badgeR * 0.50;
                dc.DrawLine(pen, new Point(bx - br * 0.5, by), new Point(bx - br * 0.1, by + br * 0.5));
                dc.DrawLine(pen, new Point(bx - br * 0.1, by + br * 0.5), new Point(bx + br * 0.6, by - br * 0.45));
            });
        }

        /// <summary>Overlay a red cross badge on the bottom-right of a base icon.</summary>
        public static BitmapSource WithCrossBadge(BitmapSource baseIcon, int size)
        {
            if (baseIcon == null) return null;
            return Render(size, (dc, s) =>
            {
                dc.DrawImage(baseIcon, new Rect(0, 0, s, s));

                double badgeR = s * 0.22;
                var center = new Point(s - badgeR - s * 0.02, s - badgeR - s * 0.02);
                dc.DrawEllipse(B(CRed), P(Colors.White, s * 0.03), center, badgeR, badgeR);

                var pen = P(Colors.White, s * 0.05);
                double bx = center.X, by = center.Y, br = badgeR * 0.40;
                dc.DrawLine(pen, new Point(bx - br, by - br), new Point(bx + br, by + br));
                dc.DrawLine(pen, new Point(bx + br, by - br), new Point(bx - br, by + br));
            });
        }

        // ═══════════════════════════════════════════════════
        // Generic sub-item icons for pulldown menus
        // ═══════════════════════════════════════════════════

        /// <summary>Small colored circle icon for pulldown sub-items</summary>
        public static BitmapSource SubItem(int size, Color accent, string letter)
        {
            return Render(size, (dc, s) =>
            {
                dc.DrawEllipse(new SolidColorBrush(accent), null, new Point(s * 0.5, s * 0.5), s * 0.42, s * 0.42);
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

        // ═══════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════

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

        /// <summary>QA/QC icon — shield with a checkmark.</summary>
        public static BitmapSource QaQc(int size) => Render(size, (dc, s) =>
        {
            double m = s * 0.08, cx = s / 2.0;
            // Shield body
            var shield = new StreamGeometry();
            using (var ctx = shield.Open())
            {
                ctx.BeginFigure(new Point(cx, m), true, true);
                ctx.LineTo(new Point(s - m, m * 2.2), true, false);
                ctx.LineTo(new Point(s - m, s * 0.55), true, false);
                ctx.BezierTo(new Point(s - m, s * 0.78), new Point(cx, s - m), new Point(cx, s - m), true, false);
                ctx.BezierTo(new Point(cx, s - m), new Point(m, s * 0.78), new Point(m, s * 0.55), true, false);
                ctx.LineTo(new Point(m, m * 2.2), true, false);
            }
            shield.Freeze();
            dc.DrawGeometry(B(CPrimary), null, shield);
            dc.DrawGeometry(null, P(CDark, s * 0.04), shield);
            // Checkmark
            var pen = P(Colors.White, s * 0.1);
            double t = s * 0.3, mid = s * 0.56, bot = s * 0.7;
            dc.DrawLine(pen, new Point(cx - t * 0.5, mid), new Point(cx - t * 0.05, bot - s * 0.05));
            dc.DrawLine(pen, new Point(cx - t * 0.05, bot - s * 0.05), new Point(cx + t * 0.6, t * 0.85));
        });

        /// <summary>Draw a 4-point sparkle star.</summary>
        private static void DrawStar4(DrawingContext dc, double cx, double cy, double r, Color color)
        {
            var star = new StreamGeometry();
            using (var ctx = star.Open())
            {
                double ir = r * 0.3; // inner radius
                ctx.BeginFigure(new Point(cx, cy - r), true, true);
                ctx.LineTo(new Point(cx + ir, cy - ir), true, false);
                ctx.LineTo(new Point(cx + r, cy), true, false);
                ctx.LineTo(new Point(cx + ir, cy + ir), true, false);
                ctx.LineTo(new Point(cx, cy + r), true, false);
                ctx.LineTo(new Point(cx - ir, cy + ir), true, false);
                ctx.LineTo(new Point(cx - r, cy), true, false);
                ctx.LineTo(new Point(cx - ir, cy - ir), true, false);
            }
            star.Freeze();
            dc.DrawGeometry(B(color), null, star);
        }
    }
}
