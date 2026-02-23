using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Premium dark-themed Tools Hub window — shows all tools organized by category
    /// with clickable cards that open per-tool parameter dialogs.
    /// </summary>
    public class ToolsHubWindow : Window
    {
        // Category header colors — use DarkTheme palette
        private static readonly Dictionary<string, SolidColorBrush> CategoryColors = new Dictionary<string, SolidColorBrush>
        {
            { "Export", DarkTheme.CatExport },
            { "Family & Parameters", DarkTheme.CatFamily },
            { "QuickViews", DarkTheme.CatQuickView },
            { "View & Sheet", DarkTheme.CatViewSheet }
        };

        private static readonly Dictionary<string, string> CategoryIcons = new Dictionary<string, string>
        {
            { "Export", "📤" },
            { "Family & Parameters", "📦" },
            { "QuickViews", "👁️" },
            { "View & Sheet", "📄" }
        };

        public ToolsHubWindow()
        {
            Title = "🧰 Tools Hub — Revit MCP";
            Width = 700;
            Height = 680;
            MinWidth = 550;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            BuildUI();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });     // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });        // Footer

            // --- Header ---
            var header = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(24, 14, 24, 14),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "🧰 Tools Hub",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Select a tool to configure and run",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 2, 0, 0)
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- Scrollable Content ---
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 16, 20, 16)
            };

            var content = new StackPanel();
            var allTools = ToolCatalog.GetAll();
            var categories = allTools.GroupBy(t => t.Category).ToList();

            foreach (var cat in categories)
            {
                content.Children.Add(BuildCategorySection(cat.Key, cat.ToList()));
            }

            scroller.Content = content;
            Grid.SetRow(scroller, 1);
            mainGrid.Children.Add(scroller);

            // --- Footer ---
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(24, 8, 24, 8),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerText = new TextBlock
            {
                Text = $"{allTools.Count} tools available • Click any tool to configure",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            footer.Child = footerText;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
        }

        private FrameworkElement BuildCategorySection(string categoryName, List<ToolInfo> tools)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            // Category header
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 0, 0, 10)
            };

            var catIcon = CategoryIcons.ContainsKey(categoryName) ? CategoryIcons[categoryName] : "🔧";
            var catColor = CategoryColors.ContainsKey(categoryName) ? CategoryColors[categoryName] : DarkTheme.FgLight;

            headerPanel.Children.Add(new TextBlock
            {
                Text = catIcon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = categoryName.ToUpper(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = catColor,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = $" ({tools.Count})",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            });

            section.Children.Add(headerPanel);

            // Tool cards in WrapPanel
            var wrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var tool in tools)
            {
                wrapPanel.Children.Add(BuildToolCard(tool, catColor));
            }

            section.Children.Add(wrapPanel);
            return section;
        }

        private FrameworkElement BuildToolCard(ToolInfo tool, SolidColorBrush accentColor)
        {
            var card = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 10, 10),
                Width = 195,
                MinHeight = 90,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 2,
                    Opacity = 0.25,
                    BlurRadius = 8,
                    Direction = 270
                }
            };

            var cardContent = new StackPanel();

            // Icon + name row
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            topRow.Children.Add(new TextBlock
            {
                Text = tool.Icon,
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            topRow.Children.Add(new TextBlock
            {
                Text = tool.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            cardContent.Children.Add(topRow);

            // Description
            cardContent.Children.Add(new TextBlock
            {
                Text = tool.Description,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            });

            // Parameter count badge
            var paramCount = tool.Parameters?.Count ?? 0;
            var badge = new Border
            {
                Background = DarkTheme.BgHeader,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            badge.Child = new TextBlock
            {
                Text = paramCount > 0 ? $"⚙ {paramCount} options" : "▶ Quick run",
                FontSize = 10,
                Foreground = paramCount > 0 ? DarkTheme.FgDim : DarkTheme.FgGreen
            };
            cardContent.Children.Add(badge);

            card.Child = cardContent;

            // Hover effects
            card.MouseEnter += (s, e) =>
            {
                card.Background = DarkTheme.BgCardHover;
                card.BorderBrush = accentColor;
                card.Effect = new DropShadowEffect
                {
                    Color = ((SolidColorBrush)accentColor).Color,
                    ShadowDepth = 0,
                    Opacity = 0.3,
                    BlurRadius = 16,
                    Direction = 0
                };
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = DarkTheme.BgCard;
                card.BorderBrush = DarkTheme.BorderDim;
                card.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    ShadowDepth = 2,
                    Opacity = 0.25,
                    BlurRadius = 8,
                    Direction = 270
                };
            };

            // Click handler — open dedicated tool window (or fallback to generic dialog)
            card.MouseLeftButtonUp += (s, e) =>
            {
                Window dialog;
                switch (tool.Name)
                {
                    case "export_to_pdf":
                    case "export_to_ifc":
                    case "export_to_images":
                    case "export_to_dgn":
                    case "export_to_dwg":
                    case "export_to_dwf":
                    case "export_to_nwc":
                    case "export_manager":
                        dialog = new Tools.ExportManagerWindow { Owner = this };
                        break;
                    case "export_schedule_data":
                        dialog = new Tools.ExportScheduleWindow { Owner = this };
                        break;
                    case "export_parameters_to_csv":
                        dialog = new Tools.ExportParamsCsvWindow { Owner = this };
                        break;
                    case "import_parameters_from_csv":
                        dialog = new Tools.ImportParamsCsvWindow { Owner = this };
                        break;
                    case "manage_families":
                        dialog = new Tools.ManageFamiliesWindow { Owner = this };
                        break;
                    case "get_family_info":
                        dialog = new Tools.FamilyInfoWindow { Owner = this };
                        break;
                    case "create_project_parameter":
                        dialog = new Tools.CreateParameterWindow { Owner = this };
                        break;
                    case "batch_set_parameter":
                        dialog = new Tools.BatchSetParamWindow { Owner = this };
                        break;
                    case "delete_unused_families":
                        dialog = new Tools.DeleteUnusedWindow { Owner = this };
                        break;
                    case "create_elevation_views":
                        dialog = new Tools.ElevationViewsWindow { Owner = this };
                        break;
                    case "create_section_views":
                        dialog = new Tools.SectionViewsWindow { Owner = this };
                        break;
                    case "create_callout_views":
                        dialog = new Tools.CalloutViewsWindow { Owner = this };
                        break;
                    case "align_viewports":
                        dialog = new Tools.AlignViewportsWindow { Owner = this };
                        break;
                    case "batch_create_sheets":
                        dialog = new Tools.BatchCreateSheetsWindow { Owner = this };
                        break;
                    case "duplicate_view":
                        dialog = new Tools.DuplicateViewWindow { Owner = this };
                        break;
                    case "apply_view_template":
                        dialog = new Tools.ApplyViewTemplateWindow { Owner = this };
                        break;
                    default:
                        dialog = new ToolDialogWindow(tool) { Owner = this };
                        break;
                }
                dialog.ShowDialog();
            };

            return card;
        }

        // Using DarkTheme.B for any remaining inline colors
        private static SolidColorBrush B(byte r, byte g, byte b)
            => DarkTheme.B(r, g, b);
    }
}
