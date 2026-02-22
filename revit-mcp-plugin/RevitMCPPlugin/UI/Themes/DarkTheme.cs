using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RevitMCPPlugin.UI.Themes
{
    /// <summary>
    /// Centralized dark theme palette and reusable control factory methods.
    /// All tool windows should reference these instead of defining their own brushes.
    /// </summary>
    public static class DarkTheme
    {
        // ── Pro Dark Theme Palette ────────────────────────────────────
        // Canvas Background: Deep Charcoal (#202020)
        // Primary UI Panels: Dark Gray (#2D2D2D)
        // Selection/Accent:  Cyan / Electric Blue (#00BFFF)
        // Text/Icons:        Off-White (#E0E0E0)
        // Warning/Alert:     Soft Red (#FF5252)

        public static readonly SolidColorBrush BgDark       = B(0x20, 0x20, 0x20);  // Deep Charcoal - canvas background
        public static readonly SolidColorBrush BgCard       = B(0x2D, 0x2D, 0x2D);  // Dark Gray - primary panels/cards
        public static readonly SolidColorBrush BgCardHover  = B(0x38, 0x38, 0x38);  // Lighter gray - card hover
        public static readonly SolidColorBrush BgHeader     = B(0x1A, 0x1A, 0x1A);  // Darker than canvas - headers
        public static readonly SolidColorBrush BgInput      = B(0x2D, 0x2D, 0x2D);  // Dark Gray - input fields
        public static readonly SolidColorBrush BgAccent     = B(0x00, 0xBF, 0xFF);  // Cyan / Electric Blue - selection/accent
        public static readonly SolidColorBrush BgAccentHover= B(0x00, 0xA3, 0xE0);  // Darker cyan - accent hover
        public static readonly SolidColorBrush BgFooter     = B(0x1A, 0x1A, 0x1A);  // Same as header - footers
        public static readonly SolidColorBrush BgCancel     = B(0x3A, 0x3A, 0x3A);  // Mid gray - cancel/secondary buttons
        public static readonly SolidColorBrush BgCancelHover= B(0x45, 0x45, 0x45);  // Lighter gray - cancel hover

        public static readonly SolidColorBrush FgWhite      = Brushes.White;
        public static readonly SolidColorBrush FgLight      = B(0xE0, 0xE0, 0xE0);  // Off-White - primary text/icons
        public static readonly SolidColorBrush FgDim        = B(0x80, 0x80, 0x80);  // Medium gray - secondary text
        public static readonly SolidColorBrush FgRequired   = B(0xFF, 0x52, 0x52);  // Soft Red - required/error
        public static readonly SolidColorBrush FgGreen      = B(0x4C, 0xAF, 0x50);  // Muted green - success
        public static readonly SolidColorBrush FgGold       = B(0xFF, 0xD5, 0x4F);  // Gold - highlight
        public static readonly SolidColorBrush FgWarning    = B(0xFF, 0x52, 0x52);  // Soft Red - warning/alert

        public static readonly SolidColorBrush BorderDim    = B(0x3A, 0x3A, 0x3A);  // Subtle border
        public static readonly SolidColorBrush BorderAccent = B(0x00, 0xBF, 0xFF);  // Cyan - accent border

        // Category accent colors
        public static readonly SolidColorBrush CatExport    = B(0x00, 0xBF, 0xFF);  // Cyan - matches accent
        public static readonly SolidColorBrush CatFamily    = B(0xFF, 0xA7, 0x26);  // Warm orange
        public static readonly SolidColorBrush CatQuickView = B(0x4C, 0xAF, 0x50);  // Green
        public static readonly SolidColorBrush CatViewSheet = B(0xAB, 0x47, 0xBC);  // Purple

        public static readonly FontFamily DefaultFont = new FontFamily("Segoe UI");

        // ── Window Setup ──────────────────────────────────────────────

        /// <summary>
        /// Applies the dark theme to a Window.
        /// </summary>
        public static void Apply(Window w)
        {
            w.Background = BgDark;
            w.Foreground = FgWhite;
            w.FontFamily = DefaultFont;
        }

        // ── Control Factory Methods ───────────────────────────────────

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
            rootBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

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
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
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
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 14),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = BorderDim,
                BorderThickness = new Thickness(1),
                Child = stack
            };
        }

        /// <summary>Creates the primary action button (accent colored).</summary>
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

        // ── Utility ───────────────────────────────────────────────────

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
