using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using BIMBotPlugin.UI.Themes;
using ClosedXML.Excel;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// Native WPF BIM Compliance Dashboard — 5-tab interactive window.
    /// Built entirely in C# code-behind (no XAML), consistent with the
    /// BIM-Bot codebase pattern (ChatWindow, SettingsWindow, etc.).
    ///
    /// Tabs: Overview · Compliance · Schedules · Issues · Analytics
    /// Features: Dark/Light toggle, Excel export, Refresh, search/filter/sort.
    /// </summary>
    public partial class BimDashboardWindow : Window
    {
        private BimDashboardData _data;
        private bool _isDarkMode = true;

        // Theme-sensitive brushes (swapped on toggle)
        private SolidColorBrush _bgCanvas;
        private SolidColorBrush _bgCard;
        private SolidColorBrush _bgCardHover;
        private SolidColorBrush _bgInput;
        private SolidColorBrush _fgPrimary;
        private SolidColorBrush _fgSecondary;
        private SolidColorBrush _fgDim;
        private SolidColorBrush _borderBrush;

        // Tab system
        private readonly Dictionary<string, Border> _tabButtons = new Dictionary<string, Border>();
        private readonly Dictionary<string, UIElement> _tabPanels = new Dictionary<string, UIElement>();
        private string _activeTab = "overview";

        // Filterable grids
        private List<ElementRowModel> _filteredElements;
        private StackPanel _scheduleRows;
        private TextBlock _scheduleCount;
        private string _searchText = "";
        private string _filterCategory = "All";
        private string _filterStatus = "All";

        // Root containers needing theme refresh
        private Grid _rootGrid;
        private Border _headerBorder;
        private Border _toolbarBorder;
        private Border _footerBorder;
        private StackPanel _tabBar;

        public BimDashboardWindow(BimDashboardData data)
        {
            _data = data ?? new BimDashboardData();
            _filteredElements = new List<ElementRowModel>(_data.Elements ?? new List<ElementRowModel>());

            ApplyThemeBrushes();
            InitializeWindow();
            BuildUI();
        }

        // ══════════════════════════════════════════════════════════════
        //  WINDOW INIT
        // ══════════════════════════════════════════════════════════════

        private void InitializeWindow()
        {
            Title = "BIM Compliance Dashboard — " + (_data.ProjectName ?? "Project");
            Width = 1280;
            Height = 820;
            MinWidth = 900;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = _bgCanvas;
            Foreground = _fgPrimary;
            FontFamily = DarkTheme.DefaultFont;
        }

        // ══════════════════════════════════════════════════════════════
        //  THEME MANAGEMENT
        // ══════════════════════════════════════════════════════════════

        private void ApplyThemeBrushes()
        {
            if (_isDarkMode)
            {
                _bgCanvas = DarkTheme.BgDark;
                _bgCard = DarkTheme.BgCard;
                _bgCardHover = DarkTheme.BgCardHover;
                _bgInput = DarkTheme.BgInput;
                _fgPrimary = Brushes.White;
                _fgSecondary = DarkTheme.FgLight;
                _fgDim = DarkTheme.FgDim;
                _borderBrush = DarkTheme.BorderDim;
            }
            else
            {
                _bgCanvas = B(0xF8, 0xFA, 0xFC);
                _bgCard = Brushes.White;
                _bgCardHover = B(0xF1, 0xF5, 0xF9);
                _bgInput = B(0xF1, 0xF5, 0xF9);
                _fgPrimary = B(0x1E, 0x29, 0x3B);
                _fgSecondary = B(0x47, 0x55, 0x69);
                _fgDim = B(0x64, 0x74, 0x8B);
                _borderBrush = B(0xE2, 0xE8, 0xF0);
            }
        }

        private void ToggleTheme()
        {
            _isDarkMode = !_isDarkMode;
            ApplyThemeBrushes();
            // Rebuild entire UI (simplest approach for full theme swap)
            _tabButtons.Clear();
            _tabPanels.Clear();
            Background = _bgCanvas;
            Content = null;
            BuildUI();
        }

        // ══════════════════════════════════════════════════════════════
        //  MAIN UI BUILDER
        // ══════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Header
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Toolbar
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Tab bar
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Footer

            BuildHeader();
            BuildToolbar();
            BuildTabBar();
            BuildTabPanels();
            BuildFooter();

            Content = _rootGrid;
            SwitchTab("overview");
        }

        // ── Header ─────────────────────────────────────────────────

        private void BuildHeader()
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "📊 BIM Compliance Dashboard",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, FontFamily = DarkTheme.DefaultFont
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{_data.ProjectName ?? "Project"} · BEP v{_data.BepVersion ?? "1.0"} · MIDP v{_data.MidpVersion ?? "1.0"}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 3, 0, 0), FontFamily = DarkTheme.DefaultFont
            });

            _headerBorder = new Border
            {
                Background = new LinearGradientBrush(DarkTheme.BrandPrimary, DarkTheme.BrandDark, 0),
                Padding = new Thickness(24, 16, 24, 16),
                Child = stack
            };
            Grid.SetRow(_headerBorder, 0);
            _rootGrid.Children.Add(_headerBorder);
        }

        // ── Toolbar ────────────────────────────────────────────────

        private void BuildToolbar()
        {
            var panel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Theme toggle
            var themeLabel = new TextBlock
            {
                Text = _isDarkMode ? "🌙 Dark" : "☀️ Light",
                Foreground = _fgSecondary, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            panel.Children.Add(themeLabel);

            var toggle = DarkTheme.MakeToggleSwitch(_isDarkMode, (on) => ToggleTheme());
            panel.Children.Add(toggle);

            AddToolbarSpacer(panel, 16);

            // Refresh
            var refreshBtn = MakeToolbarButton("🔄 Refresh");
            refreshBtn.ToolTip = "Re-extract data from Revit model";
            // Refresh requires re-invoking the MCP tool — placeholder click
            refreshBtn.MouseLeftButtonUp += (s, e) =>
                MessageBox.Show("To refresh data, re-run the generate_bim_dashboard MCP tool.", "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
            panel.Children.Add(refreshBtn);

            AddToolbarSpacer(panel, 8);

            // Export Excel
            var excelBtn = MakeToolbarButton("📥 Export Excel");
            excelBtn.MouseLeftButtonUp += (s, e) => ExportToExcel();
            panel.Children.Add(excelBtn);

            AddToolbarSpacer(panel, 8);

            // Export CSV
            var csvBtn = MakeToolbarButton("📄 Export CSV");
            csvBtn.MouseLeftButtonUp += (s, e) => ExportToCsv();
            panel.Children.Add(csvBtn);

            _toolbarBorder = new Border
            {
                Background = _bgCard,
                Padding = new Thickness(24, 8, 24, 8),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = panel
            };
            Grid.SetRow(_toolbarBorder, 1);
            _rootGrid.Children.Add(_toolbarBorder);
        }

        private Border MakeToolbarButton(string text)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 12,
                Foreground = _fgSecondary, FontFamily = DarkTheme.DefaultFont,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Border
            {
                Background = _bgCardHover,
                CornerRadius = DarkTheme.ButtonRadius,
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Child = tb
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(DarkTheme.BrandPrimary);
            btn.MouseLeave += (s, e) => btn.Background = _bgCardHover;
            return btn;
        }

        private void AddToolbarSpacer(Panel p, double width)
        {
            p.Children.Add(new Border { Width = width });
        }

        // ── Tab Bar ────────────────────────────────────────────────

        private void BuildTabBar()
        {
            _tabBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = _bgCard
            };

            var tabs = new[] {
                ("overview", "📊 Overview"),
                ("compliance", "✅ Compliance"),
                ("schedules", "📋 Schedules"),
                ("issues", "⚠️ Issues"),
                ("analytics", "📈 Analytics")
            };

            foreach (var (key, label) in tabs)
            {
                var tb = new TextBlock
                {
                    Text = label, FontSize = 13,
                    Foreground = _fgDim, FontFamily = DarkTheme.DefaultFont,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var btn = new Border
                {
                    Padding = new Thickness(18, 10, 18, 10),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0, 0, 0, 3),
                    BorderBrush = Brushes.Transparent,
                    Child = tb,
                    Tag = key
                };
                btn.MouseLeftButtonUp += (s, e) => SwitchTab((string)((Border)s).Tag);
                btn.MouseEnter += (s, e) =>
                {
                    if ((string)((Border)s).Tag != _activeTab)
                        ((Border)s).Background = _bgCardHover;
                };
                btn.MouseLeave += (s, e) =>
                {
                    if ((string)((Border)s).Tag != _activeTab)
                        ((Border)s).Background = Brushes.Transparent;
                };

                _tabButtons[key] = btn;
                _tabBar.Children.Add(btn);
            }

            var tabBorder = new Border
            {
                Child = _tabBar,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(tabBorder, 2);
            _rootGrid.Children.Add(tabBorder);
        }

        private void SwitchTab(string key)
        {
            _activeTab = key;
            foreach (var kv in _tabButtons)
            {
                var isActive = kv.Key == key;
                kv.Value.BorderBrush = isActive ? new SolidColorBrush(DarkTheme.BrandPrimary) : Brushes.Transparent;
                kv.Value.Background = Brushes.Transparent;
                ((TextBlock)kv.Value.Child).Foreground = isActive ? _fgPrimary : _fgDim;
                ((TextBlock)kv.Value.Child).FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            }
            foreach (var kv in _tabPanels)
                kv.Value.Visibility = kv.Key == key ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Footer ─────────────────────────────────────────────────

        private void BuildFooter()
        {
            var panel = new DockPanel();
            panel.Children.Add(new TextBlock
            {
                Text = $"Generated: {_data.GeneratedAt ?? "—"}",
                FontSize = 11, Foreground = _fgDim, FontFamily = DarkTheme.DefaultFont,
                VerticalAlignment = VerticalAlignment.Center
            });

            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(right, Dock.Right);

            right.Children.Add(MakeScorePill(_data.OverallScore));
            right.Children.Add(new TextBlock
            {
                Text = $" · {_data.TotalElements} elements",
                FontSize = 11, Foreground = _fgDim,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
            panel.Children.Add(right);

            _footerBorder = new Border
            {
                Background = _isDarkMode ? DarkTheme.BgFooter : B(0xF1, 0xF5, 0xF9),
                Padding = new Thickness(24, 8, 24, 8),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = panel
            };
            Grid.SetRow(_footerBorder, 4);
            _rootGrid.Children.Add(_footerBorder);
        }

        private Border MakeScorePill(int score)
        {
            return new Border
            {
                Background = DashboardCharts.ScoreColor(score),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Child = new TextBlock
                {
                    Text = $"{score}%",
                    FontSize = 11, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White, FontFamily = DarkTheme.DefaultFont
                }
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB PANEL BUILDER
        // ══════════════════════════════════════════════════════════════

        private void BuildTabPanels()
        {
            var container = new Grid();

            var overviewPanel = BuildOverviewTab();
            var compliancePanel = BuildComplianceTab();
            var schedulesPanel = BuildSchedulesTab();
            var issuesPanel = BuildIssuesTab();
            var analyticsPanel = BuildAnalyticsTab();

            _tabPanels["overview"] = overviewPanel;
            _tabPanels["compliance"] = compliancePanel;
            _tabPanels["schedules"] = schedulesPanel;
            _tabPanels["issues"] = issuesPanel;
            _tabPanels["analytics"] = analyticsPanel;

            foreach (var panel in _tabPanels.Values)
            {
                panel.Visibility = Visibility.Collapsed;
                container.Children.Add(panel);
            }

            Grid.SetRow(container, 3);
            _rootGrid.Children.Add(container);
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB 1: OVERVIEW
        // ══════════════════════════════════════════════════════════════

        private ScrollViewer BuildOverviewTab()
        {
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Summary Cards Row ──
            var cardsRow = new WrapPanel { Orientation = Orientation.Horizontal };

            cardsRow.Children.Add(MakeSummaryCard("Overall Score", $"{_data.OverallScore}%",
                DashboardCharts.ScoreColor(_data.OverallScore)));
            cardsRow.Children.Add(MakeSummaryCard("Total Elements", $"{_data.TotalElements}",
                new SolidColorBrush(DarkTheme.BrandPrimary)));
            cardsRow.Children.Add(MakeSummaryCard("✅ Pass", $"{_data.TotalPass}",
                DarkTheme.FgGreen));
            cardsRow.Children.Add(MakeSummaryCard("⚠️ Warning", $"{_data.TotalWarn}",
                DarkTheme.FgGold));
            cardsRow.Children.Add(MakeSummaryCard("❌ Fail", $"{_data.TotalFail}",
                new SolidColorBrush(DarkTheme.BrandRed)));

            content.Children.Add(cardsRow);
            content.Children.Add(new Border { Height = 16 });

            // ── Charts Row ──
            var chartsGrid = new Grid();
            chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Score gauge + status bar
            var gaugeCard = MakeCard("Compliance Score");
            var gaugeContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            gaugeContent.Children.Add(DashboardCharts.MakeScoreGauge(_data.OverallScore, 140));
            gaugeContent.Children.Add(new Border { Height = 12 });
            gaugeContent.Children.Add(DashboardCharts.MakeStatusBar(_data.TotalPass, _data.TotalWarn, _data.TotalFail, 260));
            ((StackPanel)((Border)gaugeCard).Child).Children.Add(gaugeContent);
            Grid.SetColumn(gaugeCard, 0);
            chartsGrid.Children.Add(gaugeCard);

            // Category bar chart
            var catData = new Dictionary<string, double>();
            if (_data.Categories != null)
                foreach (var c in _data.Categories)
                    catData[c.Category] = c.ComplianceScore;
            var barCard = MakeCard("Category Compliance");
            ((StackPanel)((Border)barCard).Child).Children.Add(
                DashboardCharts.MakeBarChart(catData, 380, 20, null, true));
            Grid.SetColumn(barCard, 2);
            chartsGrid.Children.Add(barCard);

            content.Children.Add(chartsGrid);
            content.Children.Add(new Border { Height = 16 });

            // ── Level Distribution Doughnut ──
            var levelData = new Dictionary<string, double>();
            if (_data.LevelDistribution != null)
                foreach (var kv in _data.LevelDistribution)
                    levelData[kv.Key] = kv.Value;
            if (levelData.Count > 0)
            {
                var doughCard = MakeCard("Element Distribution by Level");
                ((StackPanel)((Border)doughCard).Child).Children.Add(
                    DashboardCharts.MakeDoughnut(levelData, 160));
                content.Children.Add(doughCard);
            }

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            };
        }

        private Border MakeSummaryCard(string title, string value, SolidColorBrush accent)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = title, FontSize = 11, Foreground = _fgDim,
                TextAlignment = TextAlignment.Center, FontFamily = DarkTheme.DefaultFont
            });
            stack.Children.Add(new TextBlock
            {
                Text = value, FontSize = 26, FontWeight = FontWeights.Bold,
                Foreground = accent, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0), FontFamily = DarkTheme.DefaultFont
            });

            return new Border
            {
                Background = _bgCard,
                CornerRadius = DarkTheme.CardRadius,
                Padding = new Thickness(20, 14, 20, 14),
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                Effect = DarkTheme.MakeCardShadow(),
                Child = stack
            };
        }

        private FrameworkElement MakeCard(string title)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = _fgPrimary, Margin = new Thickness(0, 0, 0, 12),
                FontFamily = DarkTheme.DefaultFont
            });

            return new Border
            {
                Background = _bgCard,
                CornerRadius = DarkTheme.CardRadius,
                Padding = DarkTheme.CardPadding,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                Effect = DarkTheme.MakeCardShadow(),
                Child = stack
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB 2: COMPLIANCE
        // ══════════════════════════════════════════════════════════════

        private ScrollViewer BuildComplianceTab()
        {
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            if (_data.Categories == null || _data.Categories.Count == 0)
            {
                content.Children.Add(new TextBlock { Text = "No category data available.", Foreground = _fgDim });
                return new ScrollViewer { Content = content };
            }

            // Header row
            var headerGrid = MakeComplianceRow("Category", "Elements", "Pass", "Warn", "Fail", "Score", "Fill Rate", true);
            content.Children.Add(headerGrid);

            foreach (var cat in _data.Categories.OrderByDescending(c => c.TotalElements))
            {
                var row = MakeComplianceRow(
                    cat.Category,
                    cat.TotalElements.ToString(),
                    cat.PassCount.ToString(),
                    cat.WarnCount.ToString(),
                    cat.FailCount.ToString(),
                    cat.ComplianceScore + "%",
                    cat.ParameterFillRate + "%",
                    false
                );
                content.Children.Add(row);

                // Missing params detail
                if (cat.MissingParams != null && cat.MissingParams.Count > 0)
                {
                    var detailPanel = new WrapPanel
                    {
                        Margin = new Thickness(24, 2, 0, 8),
                        Orientation = Orientation.Horizontal
                    };
                    foreach (var mp in cat.MissingParams)
                    {
                        detailPanel.Children.Add(new Border
                        {
                            Background = _isDarkMode ? DarkTheme.BgWarning : B(0xFF, 0xF7, 0xED),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 6, 4),
                            Child = new TextBlock
                            {
                                Text = $"{mp.Key}: {mp.Value} missing",
                                FontSize = 10, Foreground = DarkTheme.FgGold,
                                FontFamily = DarkTheme.DefaultFont
                            }
                        });
                    }
                    content.Children.Add(detailPanel);
                }
            }

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            };
        }

        private Grid MakeComplianceRow(string cat, string elems, string pass,
            string warn, string fail, string score, string fill, bool isHeader)
        {
            var g = new Grid
            {
                Margin = new Thickness(0, 1, 0, 1),
                Background = isHeader ? Brushes.Transparent : _bgCard
            };

            var widths = new[] { 160.0, 80, 60, 60, 60, 70, 80 };
            var texts = new[] { cat, elems, pass, warn, fail, score, fill };

            for (int i = 0; i < widths.Length; i++)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < texts.Length; i++)
            {
                Brush fg;
                if (isHeader) fg = _fgDim;
                else if (i == 5) fg = ScoreFg(score);
                else fg = _fgSecondary;

                var tb = new TextBlock
                {
                    Text = texts[i],
                    FontSize = isHeader ? 11 : 12,
                    FontWeight = isHeader ? FontWeights.SemiBold : (i == 0 ? FontWeights.Medium : FontWeights.Normal),
                    Foreground = fg,
                    Padding = new Thickness(8, 6, 8, 6),
                    FontFamily = DarkTheme.DefaultFont
                };
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }

            if (!isHeader)
            {
                // Status bar spanning last column
                int p = 0, w = 0, f = 0;
                int.TryParse(pass, out p); int.TryParse(warn, out w); int.TryParse(fail, out f);
                var bar = DashboardCharts.MakeStatusBar(p, w, f, 120);
                ((FrameworkElement)bar).Margin = new Thickness(4, 6, 4, 6);
                Grid.SetColumn((UIElement)bar, 7);
                g.Children.Add((UIElement)bar);
            }

            return g;
        }

        private Brush ScoreFg(string scoreStr)
        {
            int s;
            var clean = scoreStr?.Replace("%", "") ?? "0";
            int.TryParse(clean, out s);
            return DashboardCharts.ScoreColor(s);
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB 3: SCHEDULES
        // ══════════════════════════════════════════════════════════════

        private ScrollViewer BuildSchedulesTab()
        {
            var root = new DockPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Filter toolbar ──
            var filters = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(filters, Dock.Top);

            // Search box
            var searchBox = new TextBox
            {
                Width = 220, FontSize = 12, Padding = new Thickness(8, 6, 8, 6),
                Background = _bgInput, Foreground = _fgDim,
                BorderBrush = _borderBrush, BorderThickness = new Thickness(1),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Text = "🔍 Search elements…"
            };
            searchBox.GotFocus += (s, e) =>
            {
                if (searchBox.Text == "🔍 Search elements…")
                { searchBox.Text = ""; searchBox.Foreground = _fgPrimary; }
            };
            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(searchBox.Text))
                { searchBox.Text = "🔍 Search elements…"; searchBox.Foreground = _fgDim; }
            };
            searchBox.TextChanged += (s, e) => { _searchText = searchBox.Text == "🔍 Search elements…" ? "" : searchBox.Text; ApplyScheduleFilters(); };
            filters.Children.Add(searchBox);
            filters.Children.Add(new Border { Width = 12 });

            // Category filter
            var catCombo = new ComboBox
            {
                Width = 160, FontSize = 12, Padding = new Thickness(6, 4, 6, 4),
                Background = _bgInput, Foreground = _fgPrimary, BorderBrush = _borderBrush,
                FontFamily = DarkTheme.DefaultFont
            };
            catCombo.Items.Add("All Categories");
            if (_data.Categories != null)
                foreach (var c in _data.Categories) catCombo.Items.Add(c.Category);
            catCombo.SelectedIndex = 0;
            catCombo.SelectionChanged += (s, e) =>
            {
                _filterCategory = catCombo.SelectedIndex == 0 ? "All" : catCombo.SelectedItem.ToString();
                ApplyScheduleFilters();
            };
            filters.Children.Add(catCombo);
            filters.Children.Add(new Border { Width = 12 });

            // Status filter
            var statusCombo = new ComboBox
            {
                Width = 120, FontSize = 12, Padding = new Thickness(6, 4, 6, 4),
                Background = _bgInput, Foreground = _fgPrimary, BorderBrush = _borderBrush,
                FontFamily = DarkTheme.DefaultFont
            };
            foreach (var s in new[] { "All Status", "pass", "warning", "fail" }) statusCombo.Items.Add(s);
            statusCombo.SelectedIndex = 0;
            statusCombo.SelectionChanged += (s, e) =>
            {
                _filterStatus = statusCombo.SelectedIndex == 0 ? "All" : statusCombo.SelectedItem.ToString();
                ApplyScheduleFilters();
            };
            filters.Children.Add(statusCombo);
            filters.Children.Add(new Border { Width = 16 });

            // Count label
            _scheduleCount = new TextBlock
            {
                FontSize = 11, Foreground = _fgDim, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DarkTheme.DefaultFont
            };
            filters.Children.Add(_scheduleCount);
            root.Children.Add(filters);

            // ── Grid header ──
            var header = MakeScheduleGridRow("ID", "Name", "Category", "Type", "Level", "Mark", "Status", true);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // ── Scrollable rows ──
            _scheduleRows = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _scheduleRows
            };
            root.Children.Add(scroll);

            ApplyScheduleFilters();
            return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private void ApplyScheduleFilters()
        {
            if (_scheduleRows == null) return;
            _scheduleRows.Children.Clear();

            var source = _data.Elements ?? new List<ElementRowModel>();
            _filteredElements = source.Where(el =>
            {
                if (_filterCategory != "All" && !string.Equals(el.Category, _filterCategory, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (_filterStatus != "All" && !string.Equals(el.Status, _filterStatus, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrEmpty(_searchText))
                {
                    var q = _searchText.ToLowerInvariant();
                    return (el.Name ?? "").ToLower().Contains(q)
                        || (el.Id ?? "").Contains(q)
                        || (el.TypeName ?? "").ToLower().Contains(q)
                        || (el.Mark ?? "").ToLower().Contains(q);
                }
                return true;
            }).ToList();

            foreach (var el in _filteredElements.Take(500))
                _scheduleRows.Children.Add(MakeScheduleGridRow(
                    el.Id ?? "", el.Name ?? "", el.Category ?? "", el.TypeName ?? "",
                    el.Level ?? "", el.Mark ?? "", el.Status ?? "", false));

            if (_scheduleCount != null)
                _scheduleCount.Text = $"Showing {Math.Min(_filteredElements.Count, 500)} of {_filteredElements.Count} elements";
        }

        private Grid MakeScheduleGridRow(string id, string name, string cat, string type,
            string level, string mark, string status, bool isHeader)
        {
            var g = new Grid { Background = isHeader ? Brushes.Transparent : _bgCard, Margin = new Thickness(0, 1, 0, 0) };
            var widths = new[] { 80.0, 180, 120, 140, 100, 80 };
            for (int i = 0; i < widths.Length; i++)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var vals = new[] { id, name, cat, type, level, mark };
            for (int i = 0; i < vals.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = DashboardCharts.Truncate(vals[i], 24), FontSize = isHeader ? 11 : 12,
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isHeader ? _fgDim : _fgSecondary,
                    Padding = new Thickness(6, 5, 6, 5), FontFamily = DarkTheme.DefaultFont
                };
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }

            // Status pill
            var statusBrush = status == "pass" ? DarkTheme.FgGreen
                : status == "warning" ? DarkTheme.FgGold
                : status == "fail" ? new SolidColorBrush(DarkTheme.BrandRed) : _fgDim;
            var pill = new Border
            {
                Background = statusBrush, CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = isHeader ? "Status" : (status ?? "—"), FontSize = 10,
                    Foreground = isHeader ? _fgDim : Brushes.White,
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    FontFamily = DarkTheme.DefaultFont
                }
            };
            if (isHeader) pill.Background = Brushes.Transparent;
            Grid.SetColumn(pill, 6);
            g.Children.Add(pill);

            return g;
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB 4: ISSUES
        // ══════════════════════════════════════════════════════════════

        private ScrollViewer BuildIssuesTab()
        {
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            var issues = _data.Issues ?? new List<ComplianceIssueModel>();

            if (issues.Count == 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "✅ No compliance issues found.", FontSize = 14,
                    Foreground = DarkTheme.FgGreen, FontFamily = DarkTheme.DefaultFont,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            }

            // Summary bar
            int crit = issues.Count(i => i.Severity == "critical");
            int warn = issues.Count(i => i.Severity == "warning");
            int info = issues.Count(i => i.Severity == "info");

            var summaryBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            summaryBar.Children.Add(MakeIssueBadge($"🔴 {crit} Critical", DarkTheme.BrandRed));
            summaryBar.Children.Add(MakeIssueBadge($"🟡 {warn} Warning", DarkTheme.BrandAmber));
            summaryBar.Children.Add(MakeIssueBadge($"🔵 {info} Info", DarkTheme.BrandPrimary));
            content.Children.Add(summaryBar);

            // Issue cards grouped by severity
            foreach (var sev in new[] { "critical", "warning", "info" })
            {
                var group = issues.Where(i => i.Severity == sev).ToList();
                if (group.Count == 0) continue;

                foreach (var issue in group.Take(100))
                    content.Children.Add(MakeIssueCard(issue));
            }

            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private Border MakeIssueBadge(string text, Color color)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color), FontFamily = DarkTheme.DefaultFont
                }
            };
        }

        private Border MakeIssueCard(ComplianceIssueModel issue)
        {
            var sevColor = issue.Severity == "critical" ? DarkTheme.BrandRed
                : issue.Severity == "warning" ? DarkTheme.BrandAmber : DarkTheme.BrandPrimary;

            var stack = new StackPanel();

            // Top row: severity icon + rule
            var topRow = new WrapPanel();
            var icon = issue.Severity == "critical" ? "🔴" : issue.Severity == "warning" ? "🟡" : "🔵";
            topRow.Children.Add(new TextBlock
            {
                Text = $"{icon} {issue.Rule ?? "Rule"}", FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(sevColor), FontFamily = DarkTheme.DefaultFont
            });
            stack.Children.Add(topRow);

            // Element info
            stack.Children.Add(new TextBlock
            {
                Text = $"{issue.ElementName ?? "—"} (ID: {issue.ElementId ?? "—"}) · {issue.Category ?? ""} · {issue.Level ?? ""}",
                FontSize = 11, Foreground = _fgDim, Margin = new Thickness(0, 4, 0, 0),
                FontFamily = DarkTheme.DefaultFont
            });

            // Message
            if (!string.IsNullOrEmpty(issue.Message))
                stack.Children.Add(new TextBlock
                {
                    Text = issue.Message, FontSize = 12, Foreground = _fgSecondary,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
                    FontFamily = DarkTheme.DefaultFont
                });

            // Suggestion
            if (!string.IsNullOrEmpty(issue.Suggestion))
                stack.Children.Add(new Border
                {
                    Background = _isDarkMode ? DarkTheme.BgInput : B(0xF0, 0xF9, 0xFF),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 6, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "💡 " + issue.Suggestion, FontSize = 11, Foreground = _fgSecondary,
                        TextWrapping = TextWrapping.Wrap, FontFamily = DarkTheme.DefaultFont
                    }
                });

            return new Border
            {
                Background = _bgCard, CornerRadius = DarkTheme.CardRadius,
                Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, sevColor.R, sevColor.G, sevColor.B)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Effect = DarkTheme.MakeCardShadow(), Child = stack
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB 5: ANALYTICS
        // ══════════════════════════════════════════════════════════════

        private ScrollViewer BuildAnalyticsTab()
        {
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Row 1: Score gauge + Status distribution ──
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var gaugeCard = MakeCard("Overall Compliance");
            var gc = (StackPanel)((Border)gaugeCard).Child;
            gc.Children.Add(DashboardCharts.MakeScoreGauge(_data.OverallScore, 160));
            gc.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(gaugeCard, 0);
            row1.Children.Add(gaugeCard);

            var statusData = new Dictionary<string, double>
            {
                ["Pass"] = _data.TotalPass, ["Warning"] = _data.TotalWarn, ["Fail"] = _data.TotalFail
            };
            var statusCard = MakeCard("Status Distribution");
            ((StackPanel)((Border)statusCard).Child).Children.Add(
                DashboardCharts.MakeDoughnut(statusData, 140));
            Grid.SetColumn(statusCard, 2);
            row1.Children.Add(statusCard);
            content.Children.Add(row1);
            content.Children.Add(new Border { Height = 16 });

            // ── Row 2: Category bar chart ──
            if (_data.Categories != null && _data.Categories.Count > 0)
            {
                var catScores = new Dictionary<string, double>();
                var catCounts = new Dictionary<string, double>();
                foreach (var c in _data.Categories.OrderByDescending(x => x.TotalElements))
                {
                    catScores[c.Category] = c.ComplianceScore;
                    catCounts[c.Category] = c.TotalElements;
                }

                var row2 = new Grid();
                row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var scoreCard = MakeCard("Compliance by Category");
                ((StackPanel)((Border)scoreCard).Child).Children.Add(
                    DashboardCharts.MakeBarChart(catScores, 380, 20, null, true));
                Grid.SetColumn(scoreCard, 0);
                row2.Children.Add(scoreCard);

                var countCard = MakeCard("Element Count by Category");
                ((StackPanel)((Border)countCard).Child).Children.Add(
                    DashboardCharts.MakeBarChart(catCounts, 380, 20, new SolidColorBrush(DarkTheme.BrandTeal)));
                Grid.SetColumn(countCard, 2);
                row2.Children.Add(countCard);
                content.Children.Add(row2);
                content.Children.Add(new Border { Height = 16 });
            }

            // ── Row 3: Parameter fill rate ──
            if (_data.Categories != null && _data.Categories.Count > 0)
            {
                var fillRates = new Dictionary<string, double>();
                foreach (var c in _data.Categories.OrderByDescending(x => x.ParameterFillRate))
                    fillRates[c.Category] = c.ParameterFillRate;

                var fillCard = MakeCard("Parameter Fill Rate by Category");
                ((StackPanel)((Border)fillCard).Child).Children.Add(
                    DashboardCharts.MakeBarChart(fillRates, 600, 20,
                        new SolidColorBrush(DarkTheme.BrandGreen), true));
                content.Children.Add(fillCard);
                content.Children.Add(new Border { Height = 16 });
            }

            // ── Row 4: Level distribution ──
            if (_data.LevelDistribution != null && _data.LevelDistribution.Count > 0)
            {
                var lvl = _data.LevelDistribution.ToDictionary(k => k.Key, k => (double)k.Value);
                var lvlCard = MakeCard("Element Distribution by Level");
                ((StackPanel)((Border)lvlCard).Child).Children.Add(
                    DashboardCharts.MakeDoughnut(lvl, 160));
                content.Children.Add(lvlCard);
            }

            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ══════════════════════════════════════════════════════════════
        //  EXPORT: EXCEL (ClosedXML)
        // ══════════════════════════════════════════════════════════════

        private void ExportToExcel()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Excel Workbook|*.xlsx",
                    FileName = $"BIM_Compliance_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != true) return;

                using (var wb = new XLWorkbook())
                {
                    // Summary sheet
                    var ws = wb.AddWorksheet("Summary");
                    ws.Cell(1, 1).Value = "BIM Compliance Report"; ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Value = "Project"; ws.Cell(2, 2).Value = _data.ProjectName ?? "";
                    ws.Cell(3, 1).Value = "Generated"; ws.Cell(3, 2).Value = _data.GeneratedAt ?? "";
                    ws.Cell(4, 1).Value = "Overall Score"; ws.Cell(4, 2).Value = _data.OverallScore;
                    ws.Cell(5, 1).Value = "Total Elements"; ws.Cell(5, 2).Value = _data.TotalElements;
                    ws.Cell(6, 1).Value = "Pass"; ws.Cell(6, 2).Value = _data.TotalPass;
                    ws.Cell(7, 1).Value = "Warning"; ws.Cell(7, 2).Value = _data.TotalWarn;
                    ws.Cell(8, 1).Value = "Fail"; ws.Cell(8, 2).Value = _data.TotalFail;
                    ws.Columns().AdjustToContents();

                    // Categories sheet
                    if (_data.Categories?.Count > 0)
                    {
                        var cs = wb.AddWorksheet("Categories");
                        var headers = new[] { "Category", "Elements", "Pass", "Warning", "Fail", "Score %", "Fill Rate %" };
                        for (int i = 0; i < headers.Length; i++)
                        { cs.Cell(1, i + 1).Value = headers[i]; cs.Cell(1, i + 1).Style.Font.Bold = true; }
                        int row = 2;
                        foreach (var c in _data.Categories)
                        {
                            cs.Cell(row, 1).Value = c.Category; cs.Cell(row, 2).Value = c.TotalElements;
                            cs.Cell(row, 3).Value = c.PassCount; cs.Cell(row, 4).Value = c.WarnCount;
                            cs.Cell(row, 5).Value = c.FailCount; cs.Cell(row, 6).Value = c.ComplianceScore;
                            cs.Cell(row, 7).Value = c.ParameterFillRate; row++;
                        }
                        cs.Columns().AdjustToContents();
                    }

                    // Elements sheet
                    if (_data.Elements?.Count > 0)
                    {
                        var es = wb.AddWorksheet("Elements");
                        var h = new[] { "ID", "Name", "Category", "Type", "Level", "Mark", "Status", "Missing Params", "Issues" };
                        for (int i = 0; i < h.Length; i++)
                        { es.Cell(1, i + 1).Value = h[i]; es.Cell(1, i + 1).Style.Font.Bold = true; }
                        int row = 2;
                        foreach (var el in _data.Elements)
                        {
                            es.Cell(row, 1).Value = el.Id; es.Cell(row, 2).Value = el.Name;
                            es.Cell(row, 3).Value = el.Category; es.Cell(row, 4).Value = el.TypeName;
                            es.Cell(row, 5).Value = el.Level; es.Cell(row, 6).Value = el.Mark;
                            es.Cell(row, 7).Value = el.Status;
                            es.Cell(row, 8).Value = string.Join(", ", el.MissingParams ?? new List<string>());
                            es.Cell(row, 9).Value = string.Join(", ", el.Issues ?? new List<string>());
                            row++;
                        }
                        es.Columns().AdjustToContents();
                    }

                    // Issues sheet
                    if (_data.Issues?.Count > 0)
                    {
                        var iss = wb.AddWorksheet("Issues");
                        var h = new[] { "Severity", "Rule", "Element", "ID", "Category", "Level", "Message", "Suggestion" };
                        for (int i = 0; i < h.Length; i++)
                        { iss.Cell(1, i + 1).Value = h[i]; iss.Cell(1, i + 1).Style.Font.Bold = true; }
                        int row = 2;
                        foreach (var issue in _data.Issues)
                        {
                            iss.Cell(row, 1).Value = issue.Severity; iss.Cell(row, 2).Value = issue.Rule;
                            iss.Cell(row, 3).Value = issue.ElementName; iss.Cell(row, 4).Value = issue.ElementId;
                            iss.Cell(row, 5).Value = issue.Category; iss.Cell(row, 6).Value = issue.Level;
                            iss.Cell(row, 7).Value = issue.Message; iss.Cell(row, 8).Value = issue.Suggestion;
                            row++;
                        }
                        iss.Columns().AdjustToContents();
                    }

                    wb.SaveAs(dlg.FileName);
                }

                MessageBox.Show($"Report exported to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  EXPORT: CSV
        // ══════════════════════════════════════════════════════════════

        private void ExportToCsv()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "CSV File|*.csv",
                    FileName = $"BIM_Elements_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (dlg.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("ID,Name,Category,Type,Level,Mark,Status,MissingParams,Issues");

                foreach (var el in _data.Elements ?? new List<ElementRowModel>())
                {
                    sb.AppendLine(string.Join(",",
                        Csv(el.Id), Csv(el.Name), Csv(el.Category), Csv(el.TypeName),
                        Csv(el.Level), Csv(el.Mark), Csv(el.Status),
                        Csv(string.Join("; ", el.MissingParams ?? new List<string>())),
                        Csv(string.Join("; ", el.Issues ?? new List<string>()))
                    ));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"CSV exported to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string v) =>
            "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";

        // ── Utility ────────────────────────────────────────────────

        private static SolidColorBrush B(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
