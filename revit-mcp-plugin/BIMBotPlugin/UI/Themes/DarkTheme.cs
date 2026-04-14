using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BIMBotPlugin.UI.Themes
{
    /// <summary>
    /// ╔══════════════════════════════════════════════════════════════════╗
    /// ║  BIM-Bot Design System — "BIM Professional" Theme              ║
    /// ╠══════════════════════════════════════════════════════════════════╣
    /// ║  This is the single source of truth for ALL colors, fonts,     ║
    /// ║  and UI tokens across every window in BIM-Bot.                 ║
    /// ║                                                                ║
    /// ║  Palette aligned with RibbonIcons.cs vector icon colors:       ║
    /// ║                                                                ║
    /// ║  BRAND COLORS (used in icons + UI accents)                     ║
    /// ║  ─────────────────────────────────────────                     ║
    /// ║  Primary    #2563EB   Vivid Blue — main actions & accents      ║
    /// ║  Dark       #1E40AF   Dark Blue  — depth, pressed states       ║
    /// ║  Teal       #06B6D4   Teal       — secondary accents           ║
    /// ║  Amber      #F59E0B   Amber      — highlights, badges          ║
    /// ║  Green      #10B981   Emerald    — success, running, ON        ║
    /// ║  Red        #EF4444   Red        — error, stopped, OFF         ║
    /// ║                                                                ║
    /// ║  SURFACE COLORS (dark mode backgrounds)                        ║
    /// ║  ────────────────────────────────────────                      ║
    /// ║  Canvas     #1A1B1E   Near-black — window background           ║
    /// ║  Card       #25262B   Dark gray  — panel / card fill           ║
    /// ║  CardHover  #2C2E33   Lighter    — hover state                 ║
    /// ║  Input      #1F2024   Darkest    — input field bg              ║
    /// ║  Header     #141517   Blackish   — title bars, footers         ║
    /// ║  Border     #373A40   Subtle     — borders, dividers           ║
    /// ║                                                                ║
    /// ║  TEXT COLORS                                                   ║
    /// ║  ───────────                                                   ║
    /// ║  White      #FFFFFF   High contrast text                       ║
    /// ║  Light      #C1C2C5   Primary body text                        ║
    /// ║  Dim        #909296   Secondary / placeholder                  ║
    /// ╚══════════════════════════════════════════════════════════════════╝
    /// </summary>
    public static class DarkTheme
    {
        // ══════════════════════════════════════════════════════════════
        //  BRAND PALETTE — synced with RibbonIcons.cs
        // ══════════════════════════════════════════════════════════════

        /// <summary>Primary vivid blue — main accent for buttons, links, active states. Matches ribbon icon primary.</summary>
        public static readonly Color BrandPrimary     = Color.FromRgb(0x25, 0x63, 0xEB);
        /// <summary>Dark blue — pressed states, deeper accents. Matches ribbon icon dark.</summary>
        public static readonly Color BrandDark        = Color.FromRgb(0x1E, 0x40, 0xAF);
        /// <summary>Teal — secondary accent, info badges. Matches ribbon icon teal.</summary>
        public static readonly Color BrandTeal        = Color.FromRgb(0x06, 0xB6, 0xD4);
        /// <summary>Amber — highlights, warnings, attention. Matches ribbon icon amber.</summary>
        public static readonly Color BrandAmber       = Color.FromRgb(0xF5, 0x9E, 0x0B);
        /// <summary>Emerald green — success, online, enabled. Matches ribbon icon green.</summary>
        public static readonly Color BrandGreen       = Color.FromRgb(0x10, 0xB9, 0x81);
        /// <summary>Red — errors, destructive actions, offline. Matches ribbon icon red.</summary>
        public static readonly Color BrandRed         = Color.FromRgb(0xEF, 0x44, 0x44);

        // ══════════════════════════════════════════════════════════════
        //  SURFACE PALETTE — dark mode backgrounds
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush BgDark        = B(0x1A, 0x1B, 0x1E);  // Canvas / window background
        public static readonly SolidColorBrush BgCard        = B(0x25, 0x26, 0x2B);  // Card / panel fill
        public static readonly SolidColorBrush BgCardHover   = B(0x2C, 0x2E, 0x33);  // Card hover
        public static readonly SolidColorBrush BgHeader      = B(0x14, 0x15, 0x17);  // Title bars, footers
        public static readonly SolidColorBrush BgInput       = B(0x1F, 0x20, 0x24);  // Input field background
        public static readonly SolidColorBrush BgFooter      = B(0x14, 0x15, 0x17);  // Footer (same as header)

        // ── Accent backgrounds (derived from brand colors) ──
        public static readonly SolidColorBrush BgAccent      = new SolidColorBrush(BrandPrimary);   // Primary action bg
        public static readonly SolidColorBrush BgAccentHover = new SolidColorBrush(BrandDark);      // Primary action hover
        public static readonly SolidColorBrush BgCancel      = B(0x37, 0x3A, 0x40);  // Cancel / secondary button
        public static readonly SolidColorBrush BgCancelHover = B(0x42, 0x45, 0x4A);  // Cancel hover

        // ── Semantic surface tints (semi-transparent overlays on dark) ──
        public static readonly SolidColorBrush BgWarning     = B(0x3B, 0x2E, 0x1A);  // Amber-tinted bg for warnings
        public static readonly SolidColorBrush BgInfo        = B(0x0A, 0x1F, 0x2A);  // Teal-tinted bg for info panels
        public static readonly SolidColorBrush BgDanger      = new SolidColorBrush(BrandRed);       // Destructive action bg
        public static readonly SolidColorBrush BgDeep        = B(0x0A, 0x0A, 0x14);  // Extra-deep bg (progress, code)

        // ══════════════════════════════════════════════════════════════
        //  TEXT PALETTE
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush FgWhite       = Brushes.White;
        public static readonly SolidColorBrush FgLight       = B(0xC1, 0xC2, 0xC5);  // Primary body text
        public static readonly SolidColorBrush FgDim         = B(0x90, 0x92, 0x96);  // Secondary / placeholder
        public static readonly SolidColorBrush FgRequired    = new SolidColorBrush(BrandRed);       // Required marker
        public static readonly SolidColorBrush FgGreen       = new SolidColorBrush(BrandGreen);     // Success text
        public static readonly SolidColorBrush FgGold        = new SolidColorBrush(BrandAmber);     // Highlight text
        public static readonly SolidColorBrush FgWarning     = new SolidColorBrush(BrandRed);       // Warning text

        // ══════════════════════════════════════════════════════════════
        //  BORDER & CATEGORY PALETTE
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush BorderDim     = B(0x37, 0x3A, 0x40);  // Subtle borders
        public static readonly SolidColorBrush BorderAccent  = new SolidColorBrush(BrandPrimary);   // Accent border
        public static readonly SolidColorBrush BorderFocus   = new SolidColorBrush(BrandTeal);      // Focus ring

        // Category accent colors — aligned with brand palette
        public static readonly SolidColorBrush CatExport     = new SolidColorBrush(BrandPrimary);   // Export tools
        public static readonly SolidColorBrush CatFamily     = new SolidColorBrush(BrandAmber);     // Family tools
        public static readonly SolidColorBrush CatQuickView  = new SolidColorBrush(BrandGreen);     // Quick view tools
        public static readonly SolidColorBrush CatViewSheet  = new SolidColorBrush(BrandTeal);      // View/sheet tools

        // ══════════════════════════════════════════════════════════════
        //  TYPOGRAPHY
        // ══════════════════════════════════════════════════════════════

        public static readonly FontFamily DefaultFont = new FontFamily("Segoe UI");

        // ══════════════════════════════════════════════════════════════
        //  DIMENSIONS & RADII
        // ══════════════════════════════════════════════════════════════

        public static readonly CornerRadius CardRadius   = new CornerRadius(8);
        public static readonly CornerRadius ButtonRadius = new CornerRadius(6);
        public static readonly CornerRadius InputRadius  = new CornerRadius(4);
        public static readonly Thickness    CardPadding  = new Thickness(14, 10, 14, 14);

        // ══════════════════════════════════════════════════════════════
        //  WINDOW SETUP
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies the BIM Professional dark theme to a Window.
        /// </summary>
        public static void Apply(Window w)
        {
            w.Background = BgDark;
            w.Foreground = FgWhite;
            w.FontFamily = DefaultFont;
        }

        // ══════════════════════════════════════════════════════════════
        //  GRADIENT HEADER & WINDOW LAYOUT FACTORIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Brand gradient brush: Vivid Blue → Dark Blue (left to right).</summary>
        public static readonly LinearGradientBrush BrandGradient = new LinearGradientBrush(
            BrandPrimary, BrandDark, 0);

        /// <summary>
        /// Creates a premium gradient header bar with title and subtitle.
        /// Blue → Dark Blue gradient with rounded top corners (for chromeless windows).
        /// </summary>
        public static Border MakeGradientHeader(string title, string subtitle = null,
            bool roundedTop = false, double titleSize = 20)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = titleSize,
                FontWeight = FontWeights.Bold,
                Foreground = FgWhite,
                FontFamily = DefaultFont
            });
            if (!string.IsNullOrEmpty(subtitle))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 3, 0, 0),
                    FontFamily = DefaultFont
                });
            }

            return new Border
            {
                Background = new LinearGradientBrush(BrandPrimary, BrandDark, 0),
                Padding = new Thickness(24, 16, 24, 16),
                CornerRadius = roundedTop ? new CornerRadius(16, 16, 0, 0) : new CornerRadius(0),
                Child = stack
            };
        }

        /// <summary>
        /// Creates a standard 3-row Grid layout: gradient header, scrollable content, footer.
        /// Returns (grid, contentPanel) so callers can populate content.
        /// </summary>
        public static (Grid grid, StackPanel content) MakeWindowLayout(
            string title, string subtitle = null, double headerHeight = 68)
        {
            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = MakeGradientHeader(title, subtitle);
            Grid.SetRow(header, 0);
            mg.Children.Add(header);

            var content = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(20, 12, 20, 12),
                Content = content
            };
            Grid.SetRow(scroll, 1);
            mg.Children.Add(scroll);

            return (mg, content);
        }

        /// <summary>
        /// Creates a standard footer bar with button panel.
        /// </summary>
        public static Border MakeFooterBar(params UIElement[] children)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            foreach (var child in children)
                panel.Children.Add(child);

            return new Border
            {
                Background = BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = panel
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  CONTROL FACTORY METHODS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Creates a styled TextBox with dark input styling and optional placeholder.</summary>
        public static TextBox MakeTextBox(string text = "", string placeholder = null)
        {
            var tb = new TextBox
            {
                Text = string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(placeholder) ? placeholder : (text ?? ""),
                Background = BgInput,
                Foreground = string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(placeholder) ? FgDim : FgWhite,
                CaretBrush = FgWhite,
                BorderBrush = BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13
            };

            if (!string.IsNullOrEmpty(placeholder) && string.IsNullOrEmpty(text))
            {
                tb.GotFocus += (s, e) =>
                {
                    if (tb.Foreground == FgDim)
                    {
                        tb.Text = "";
                        tb.Foreground = FgWhite;
                    }
                };
                tb.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(tb.Text))
                    {
                        tb.Text = placeholder;
                        tb.Foreground = FgDim;
                    }
                };
            }

            return tb;
        }

        /// <summary>Creates a fully dark-themed ComboBox with custom ControlTemplate.</summary>
        public static ComboBox MakeComboBox(string[] options, string selectedValue = null)
        {
            var combo = new ComboBox
            {
                Background = BgInput,
                Foreground = FgWhite,
                BorderBrush = BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13
            };

            // ── Build a custom dark ControlTemplate ──
            // The default WPF ComboBox template uses system chrome which ignores our colors.
            var template = new ControlTemplate(typeof(ComboBox));

            // Root: Border with our dark background
            var rootBorder = new FrameworkElementFactory(typeof(Border), "rootBorder");
            rootBorder.SetValue(Border.BackgroundProperty, BgInput);
            rootBorder.SetValue(Border.BorderBrushProperty, BorderDim);
            rootBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            rootBorder.SetValue(Border.CornerRadiusProperty, InputRadius);

            var rootGrid = new FrameworkElementFactory(typeof(Grid));
            rootGrid.AppendChild(CreateColumnDefs());

            // ToggleButton for dropdown arrow
            var toggleBtn = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton), "toggleButton");
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.BackgroundProperty, Brushes.Transparent);
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.BorderThicknessProperty, new Thickness(0));
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.FocusVisualStyleProperty, (Style)null);
            toggleBtn.SetValue(Grid.ColumnSpanProperty, 2);
            toggleBtn.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding("IsDropDownOpen") { Source = combo, Mode = System.Windows.Data.BindingMode.TwoWay });

            // Use a simple arrow template for the toggle button
            var toggleTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var arrowPath = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            arrowPath.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
            arrowPath.SetValue(System.Windows.Shapes.Path.FillProperty, FgDim);
            arrowPath.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrowPath.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowPath.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            toggleBorder.AppendChild(arrowPath);
            toggleTemplate.VisualTree = toggleBorder;
            toggleBtn.SetValue(Control.TemplateProperty, toggleTemplate);

            rootGrid.AppendChild(toggleBtn);

            // ContentPresenter for selected item text
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "contentPresenter");
            contentPresenter.SetValue(ContentPresenter.ContentTemplateProperty, combo.ItemTemplate);
            contentPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 6, 24, 6));
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);
            contentPresenter.SetBinding(ContentPresenter.ContentProperty,
                new System.Windows.Data.Binding("SelectionBoxItem") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            rootGrid.AppendChild(contentPresenter);

            // Popup for dropdown
            var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup), "PART_Popup");
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
            popup.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                new System.Windows.Data.Binding("IsDropDownOpen") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, BgCard);
            popupBorder.SetValue(Border.BorderBrushProperty, BorderDim);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, InputRadius);
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, 120.0);
            popupBorder.SetValue(FrameworkElement.MaxHeightProperty, 300.0);

            var popupScroll = new FrameworkElementFactory(typeof(ScrollViewer));
            popupScroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            popupScroll.AppendChild(itemsPresenter);
            popupBorder.AppendChild(popupScroll);
            popup.AppendChild(popupBorder);

            rootGrid.AppendChild(popup);
            rootBorder.AppendChild(rootGrid);
            template.VisualTree = rootBorder;

            combo.Template = template;

            // Override SystemColors for popup items
            combo.Resources[SystemColors.WindowBrushKey] = BgCard;
            combo.Resources[SystemColors.WindowTextBrushKey] = FgWhite;
            combo.Resources[SystemColors.HighlightBrushKey] = BgAccent;
            combo.Resources[SystemColors.HighlightTextBrushKey] = FgWhite;
            combo.Resources[SystemColors.ControlBrushKey] = BgCard;
            combo.Resources[SystemColors.ControlTextBrushKey] = FgWhite;

            if (options != null)
            {
                foreach (var opt in options)
                {
                    var item = new ComboBoxItem
                    {
                        Content = opt,
                        Background = BgCard,
                        Foreground = FgWhite,
                        Padding = new Thickness(8, 6, 8, 6)
                    };
                    if (opt == selectedValue) item.IsSelected = true;
                    combo.Items.Add(item);
                }
            }

            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            return combo;
        }

        /// <summary>Creates a styled CheckBox.</summary>
        public static CheckBox MakeCheckBox(string label, bool isChecked = false)
        {
            return new CheckBox
            {
                Content = label,
                IsChecked = isChecked,
                Foreground = FgLight,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>Creates a styled label TextBlock.</summary>
        public static TextBlock MakeLabel(string text, bool required = false, double fontSize = 12)
        {
            var tb = new TextBlock
            {
                FontSize = fontSize,
                Foreground = FgLight,
                Margin = new Thickness(0, 0, 0, 4)
            };

            if (required)
            {
                tb.Inlines.Add(new System.Windows.Documents.Run(text));
                tb.Inlines.Add(new System.Windows.Documents.Run(" *") { Foreground = FgRequired });
            }
            else
            {
                tb.Text = text;
            }

            return tb;
        }

        /// <summary>Creates a section header with an icon and colored title.</summary>
        public static FrameworkElement MakeSectionHeader(string text, SolidColorBrush color = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = color ?? FgLight,
                Margin = new Thickness(0, 12, 0, 8)
            };
        }

        /// <summary>Creates a styled group box border with title.</summary>
        public static Border MakeGroupBox(string title, UIElement content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgDim,
                Margin = new Thickness(0, 0, 0, 8)
            });
            if (content != null)
                stack.Children.Add(content);

            return new Border
            {
                Background = BgCard,
                CornerRadius = CardRadius,
                Padding = CardPadding,
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = BorderDim,
                BorderThickness = new Thickness(1),
                Child = stack
            };
        }

        /// <summary>Creates the primary action button (brand blue).</summary>
        public static Button MakePrimaryButton(string text)
        {
            var btn = new Button
            {
                Content = text,
                Background = BgAccent,
                Foreground = FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 10, 24, 10),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = BgAccentHover;
            btn.MouseLeave += (s, e) => btn.Background = BgAccent;
            return btn;
        }

        /// <summary>Creates a secondary/cancel button.</summary>
        public static Button MakeCancelButton(string text = "Cancel")
        {
            var btn = new Button
            {
                Content = text,
                Background = BgCancel,
                Foreground = FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 10, 20, 10),
                FontSize = 13,
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = BgCancelHover;
            btn.MouseLeave += (s, e) => btn.Background = BgCancel;
            return btn;
        }

        /// <summary>Creates a horizontal separator line.</summary>
        public static Border MakeSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = BorderDim,
                Margin = new Thickness(0, 8, 0, 8)
            };
        }

        /// <summary>Creates a drop shadow effect for cards.</summary>
        public static DropShadowEffect MakeCardShadow()
        {
            return new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 2,
                Opacity = 0.25,
                BlurRadius = 8,
                Direction = 270
            };
        }

        /// <summary>Creates a glow shadow effect for hover states.</summary>
        public static DropShadowEffect MakeGlowShadow(Color color)
        {
            return new DropShadowEffect
            {
                Color = color,
                ShadowDepth = 0,
                Opacity = 0.3,
                BlurRadius = 16,
                Direction = 0
            };
        }

        /// <summary>Creates a standard button panel with Cancel + Primary action buttons.</summary>
        public static StackPanel MakeButtonPanel(string primaryText, out Button cancelBtn, out Button primaryBtn)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            cancelBtn = MakeCancelButton();
            primaryBtn = MakePrimaryButton(primaryText);
            primaryBtn.Margin = new Thickness(10, 0, 0, 0);

            panel.Children.Add(cancelBtn);
            panel.Children.Add(primaryBtn);

            return panel;
        }

        /// <summary>Creates a styled Slider control.</summary>
        public static Slider MakeSlider(double min, double max, double value, double tickFrequency = 1)
        {
            return new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = tickFrequency,
                IsSnapToTickEnabled = true,
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  TOGGLE SWITCH
        // ══════════════════════════════════════════════════════════════

        /// <summary>Creates a premium animated toggle switch (on/off pill).</summary>
        /// <param name="isOn">Initial state</param>
        /// <param name="onChanged">Callback when toggled. Receives new state.</param>
        /// <returns>A Border containing the toggle switch. Tag = current bool state.</returns>
        public static Border MakeToggleSwitch(bool isOn, System.Action<bool> onChanged = null)
        {
            var knob = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = FgWhite,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = isOn ? new Thickness(22, 0, 0, 0) : new Thickness(2, 0, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 1,
                    Opacity = 0.3,
                    BlurRadius = 4,
                    Direction = 270
                }
            };

            var track = new Border
            {
                Width = 44,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = isOn ? FgGreen : BgCancel,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = knob,
                Tag = isOn   // Store state in Tag
            };

            track.MouseLeftButtonUp += (s, e) =>
            {
                var current = (bool)track.Tag;
                var newState = !current;
                track.Tag = newState;
                track.Background = newState ? FgGreen : BgCancel;
                knob.Margin = newState ? new Thickness(22, 0, 0, 0) : new Thickness(2, 0, 0, 0);
                onChanged?.Invoke(newState);
            };

            // Hover glow
            track.MouseEnter += (s, e) =>
            {
                var on = (bool)track.Tag;
                track.Background = on ? B(0x34, 0xD3, 0x99) : BgCancelHover; // lighter emerald on hover
            };
            track.MouseLeave += (s, e) =>
            {
                var on = (bool)track.Tag;
                track.Background = on ? FgGreen : BgCancel;
            };

            return track;
        }

        // ══════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════

        /// <summary>Creates Grid ColumnDefinitions for ComboBox template (content + arrow).</summary>
        private static FrameworkElementFactory CreateColumnDefs()
        {
            // We can't add ColumnDefinitions via FrameworkElementFactory directly.
            // Instead, use a DockPanel-like approach - the Grid will auto-size.
            // Return an invisible spacer element.
            var spacer = new FrameworkElementFactory(typeof(Border));
            spacer.SetValue(FrameworkElement.WidthProperty, 0.0);
            spacer.SetValue(FrameworkElement.HeightProperty, 0.0);
            return spacer;
        }


        /// <summary>Create a SolidColorBrush from RGB bytes.</summary>
        public static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
