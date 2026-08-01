using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BIMBotPlugin.UI.Themes
{
    /// <summary>
    /// BIM-Bot Design System — "BIM Professional" Light Theme
    /// Single source of truth for light mode colors, fonts, and UI tokens.
    /// </summary>
    public static class LightTheme
    {
        // ══════════════════════════════════════════════════════════════
        //  BRAND PALETTE — synced with RibbonIcons.cs
        // ══════════════════════════════════════════════════════════════

        public static readonly Color BrandPrimary     = Color.FromRgb(0x25, 0x63, 0xEB);
        public static readonly Color BrandDark        = Color.FromRgb(0x1E, 0x40, 0xAF);
        public static readonly Color BrandTeal        = Color.FromRgb(0x06, 0xB6, 0xD4);
        public static readonly Color BrandAmber       = Color.FromRgb(0xF5, 0x9E, 0x0B);
        public static readonly Color BrandGreen       = Color.FromRgb(0x10, 0xB9, 0x81);
        public static readonly Color BrandRed         = Color.FromRgb(0xEF, 0x44, 0x44);

        // ══════════════════════════════════════════════════════════════
        //  SURFACE PALETTE — light mode backgrounds
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush BgDark        = B(0xF8, 0xFA, 0xFC);  // Canvas / window background (Slate 50)
        public static readonly SolidColorBrush BgCard        = B(0xFF, 0xFF, 0xFF);  // Card / panel fill (Pure White)
        public static readonly SolidColorBrush BgCardHover   = B(0xF1, 0xF5, 0xF9);  // Card hover (Slate 100)
        public static readonly SolidColorBrush BgHeader      = B(0xFF, 0xFF, 0xFF);  // Title bars, footers
        public static readonly SolidColorBrush BgInput       = B(0xF1, 0xF5, 0xF9);  // Input field background
        public static readonly SolidColorBrush BgFooter      = B(0xF8, 0xFA, 0xFC);  // Footer

        // ── Accent backgrounds ──
        public static readonly SolidColorBrush BgAccent      = new SolidColorBrush(BrandPrimary);
        public static readonly SolidColorBrush BgAccentHover = new SolidColorBrush(BrandDark);
        public static readonly SolidColorBrush BgCancel      = B(0xE2, 0xE8, 0xF0);  // Cancel / secondary button (Slate 200)
        public static readonly SolidColorBrush BgCancelHover = B(0xCB, 0xD5, 0xE1);  // Cancel hover (Slate 300)

        // ── Semantic surface tints ──
        public static readonly SolidColorBrush BgWarning     = B(0xFF, 0xFB, 0xEB);  // Amber light background
        public static readonly SolidColorBrush BgInfo        = B(0xF0, 0xFD, 0xFA);  // Teal light background
        public static readonly SolidColorBrush BgDanger      = new SolidColorBrush(BrandRed);
        public static readonly SolidColorBrush BgDeep        = B(0xF1, 0xF5, 0xF9);  // Code / deep background

        // ══════════════════════════════════════════════════════════════
        //  TEXT PALETTE
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush FgWhite       = B(0x1E, 0x29, 0x3B);  // High contrast primary text (Slate 800)
        public static readonly SolidColorBrush FgLight       = B(0x47, 0x55, 0x69);  // Secondary body text (Slate 600)
        public static readonly SolidColorBrush FgDim         = B(0x64, 0x74, 0x8B);  // Muted / placeholder (Slate 500)
        public static readonly SolidColorBrush FgRequired    = new SolidColorBrush(BrandRed);
        public static readonly SolidColorBrush FgGreen       = B(0x05, 0x96, 0x69);  // Emerald 600
        public static readonly SolidColorBrush FgGold        = B(0xD9, 0x77, 0x06);  // Amber 600
        public static readonly SolidColorBrush FgWarning     = new SolidColorBrush(BrandRed);

        // ══════════════════════════════════════════════════════════════
        //  BORDER & CATEGORY PALETTE
        // ══════════════════════════════════════════════════════════════

        public static readonly SolidColorBrush BorderDim     = B(0xE2, 0xE8, 0xF0);  // Subtle borders (Slate 200)
        public static readonly SolidColorBrush BorderAccent  = new SolidColorBrush(BrandPrimary);
        public static readonly SolidColorBrush BorderFocus   = new SolidColorBrush(BrandTeal);

        public static readonly SolidColorBrush CatExport     = new SolidColorBrush(BrandPrimary);
        public static readonly SolidColorBrush CatFamily     = new SolidColorBrush(BrandAmber);
        public static readonly SolidColorBrush CatQuickView  = new SolidColorBrush(BrandGreen);
        public static readonly SolidColorBrush CatViewSheet  = new SolidColorBrush(BrandTeal);

        // ══════════════════════════════════════════════════════════════
        //  TYPOGRAPHY & DIMENSIONS
        // ══════════════════════════════════════════════════════════════

        public static readonly FontFamily DefaultFont = new FontFamily("Segoe UI");

        public static readonly CornerRadius CardRadius   = new CornerRadius(8);
        public static readonly CornerRadius ButtonRadius = new CornerRadius(6);
        public static readonly CornerRadius InputRadius  = new CornerRadius(4);
        public static readonly Thickness    CardPadding  = new Thickness(14, 10, 14, 14);

        // ══════════════════════════════════════════════════════════════
        //  WINDOW SETUP
        // ══════════════════════════════════════════════════════════════

        public static void Apply(Window w)
        {
            w.Background = BgDark;
            w.Foreground = FgWhite;
            w.FontFamily = DefaultFont;
        }

        // ══════════════════════════════════════════════════════════════
        //  LAYOUT FACTORIES
        // ══════════════════════════════════════════════════════════════

        public static readonly LinearGradientBrush BrandGradient = new LinearGradientBrush(
            BrandPrimary, BrandDark, 0);

        public static Border MakeGradientHeader(string title, string subtitle = null,
            bool roundedTop = false, double titleSize = 20)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = titleSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
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

            var template = new ControlTemplate(typeof(ComboBox));
            var rootBorder = new FrameworkElementFactory(typeof(Border), "rootBorder");
            rootBorder.SetValue(Border.BackgroundProperty, BgInput);
            rootBorder.SetValue(Border.BorderBrushProperty, BorderDim);
            rootBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            rootBorder.SetValue(Border.CornerRadiusProperty, InputRadius);

            var rootGrid = new FrameworkElementFactory(typeof(Grid));

            var toggleBtn = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton), "toggleButton");
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.BackgroundProperty, Brushes.Transparent);
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.BorderThicknessProperty, new Thickness(0));
            toggleBtn.SetValue(System.Windows.Controls.Primitives.ToggleButton.FocusVisualStyleProperty, (Style)null);
            toggleBtn.SetValue(Grid.ColumnSpanProperty, 2);
            toggleBtn.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding("IsDropDownOpen") { Source = combo, Mode = System.Windows.Data.BindingMode.TwoWay });

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

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "contentPresenter");
            contentPresenter.SetValue(ContentPresenter.ContentTemplateProperty, combo.ItemTemplate);
            contentPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 6, 24, 6));
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);
            contentPresenter.SetBinding(ContentPresenter.ContentProperty,
                new System.Windows.Data.Binding("SelectionBoxItem") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            rootGrid.AppendChild(contentPresenter);

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

            combo.Resources[SystemColors.WindowBrushKey] = BgCard;
            combo.Resources[SystemColors.WindowTextBrushKey] = FgWhite;
            combo.Resources[SystemColors.HighlightBrushKey] = BgAccent;
            combo.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
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

        public static Button MakePrimaryButton(string text)
        {
            var btn = new Button
            {
                Content = text,
                Background = BgAccent,
                Foreground = Brushes.White,
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

        public static Border MakeSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = BorderDim,
                Margin = new Thickness(0, 8, 0, 8)
            };
        }

        public static DropShadowEffect MakeCardShadow()
        {
            return new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 2,
                Opacity = 0.08,
                BlurRadius = 8,
                Direction = 270
            };
        }

        public static DropShadowEffect MakeGlowShadow(Color color)
        {
            return new DropShadowEffect
            {
                Color = color,
                ShadowDepth = 0,
                Opacity = 0.2,
                BlurRadius = 16,
                Direction = 0
            };
        }

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

        public static Border MakeToggleSwitch(bool isOn, System.Action<bool> onChanged = null)
        {
            var knob = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = isOn ? new Thickness(22, 0, 0, 0) : new Thickness(2, 0, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 1,
                    Opacity = 0.2,
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
                Tag = isOn
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

            track.MouseEnter += (s, e) =>
            {
                var on = (bool)track.Tag;
                track.Background = on ? B(0x34, 0xD3, 0x99) : BgCancelHover;
            };
            track.MouseLeave += (s, e) =>
            {
                var on = (bool)track.Tag;
                track.Background = on ? FgGreen : BgCancel;
            };

            return track;
        }

        public static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
