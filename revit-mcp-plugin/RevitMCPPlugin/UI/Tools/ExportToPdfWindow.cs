using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    /// <summary>
    /// Dedicated Export to PDF window with sheet/view selection grid,
    /// export settings, naming pattern with tokens, and output folder.
    /// </summary>
    public class ExportToPdfWindow : Window
    {
        // ── Fields ────────────────────────────────────────────────────
        private readonly StackPanel _itemListPanel;
        private readonly TextBox _searchBox;
        private readonly TextBlock _selectionStatus;
        private readonly List<CheckBox> _itemCheckboxes = new List<CheckBox>();
        private bool _showingSheets = true; // true = Sheets mode, false = Views mode

        // Export settings controls
        private ComboBox _paperSizeCombo;
        private ComboBox _orientationCombo;
        private ComboBox _dpiCombo;
        private ComboBox _colorCombo;
        private CheckBox _combineCheck;
        private TextBox _combinedNameBox;
        private CheckBox _hideCropCheck;
        private CheckBox _hideScopeCheck;
        private CheckBox _hideRefCheck;
        private ComboBox _rasterQualityCombo;

        // Naming & Output controls
        private TextBox _namingPatternBox;
        private TextBox _outputFolderBox;

        // Sample data (populated from project at runtime)
        private static readonly string[][] SampleSheets = new[]
        {
            new[] { "A101", "Ground Floor Plan", "A1", "" },
            new[] { "A102", "First Floor Plan", "A1", "" },
            new[] { "A103", "Second Floor Plan", "A1", "" },
            new[] { "A104", "Roof Plan", "A1", "" },
            new[] { "A201", "North Elevation", "A1", "" },
            new[] { "A202", "South Elevation", "A1", "" },
            new[] { "A301", "Section A-A", "A2", "" },
            new[] { "A401", "Wall Details", "A3", "" }
        };

        private static readonly string[][] SampleViews = new[]
        {
            new[] { "Level 0 - Floor Plan", "FloorPlan", "" },
            new[] { "Level 1 - Floor Plan", "FloorPlan", "" },
            new[] { "Level 2 - Floor Plan", "FloorPlan", "" },
            new[] { "North Elevation", "Elevation", "" },
            new[] { "South Elevation", "Elevation", "" },
            new[] { "Section A-A", "Section", "" },
            new[] { "3D - Working", "3D View", "" }
        };

        // ── Constructor ───────────────────────────────────────────────
        public ExportToPdfWindow()
        {
            Title = "📄 Export to PDF";
            Width = 750;
            Height = 680;
            MinWidth = 650;
            MinHeight = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            // Main grid: Header | Content | Footer
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });     // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });        // Footer

            // ══════════════════ HEADER ══════════════════
            var header = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(24, 12, 24, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "📄 Export to PDF",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Batch export sheets and views to PDF files",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 2, 0, 0)
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // ══════════════════ SCROLLABLE CONTENT ══════════════════
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var content = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

            // ───── Section 1: Sheet & View Selection ─────
            content.Children.Add(DarkTheme.MakeSectionHeader("Sheet & View Selection", DarkTheme.CatExport));

            // Radio toggle + Search
            var toggleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

            var sheetsRadio = new RadioButton
            {
                Content = "Sheets",
                IsChecked = true,
                Foreground = DarkTheme.FgLight,
                FontSize = 13,
                Margin = new Thickness(0, 0, 16, 0),
                GroupName = "ViewType",
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var viewsRadio = new RadioButton
            {
                Content = "Views",
                Foreground = DarkTheme.FgLight,
                FontSize = 13,
                GroupName = "ViewType",
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var radioPanel = new StackPanel { Orientation = Orientation.Horizontal };
            radioPanel.Children.Add(sheetsRadio);
            radioPanel.Children.Add(viewsRadio);
            DockPanel.SetDock(radioPanel, Dock.Left);
            toggleRow.Children.Add(radioPanel);

            _searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search...");
            _searchBox.Width = 220;
            _searchBox.HorizontalAlignment = HorizontalAlignment.Right;
            _searchBox.TextChanged += (s, e) => FilterItems();
            DockPanel.SetDock(_searchBox, Dock.Right);
            toggleRow.Children.Add(_searchBox);

            content.Children.Add(toggleRow);

            // Item list (simulated grid with checkboxes)
            var listBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 220,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _itemListPanel = new StackPanel();
            listScroll.Content = _itemListPanel;
            listBorder.Child = listScroll;
            content.Children.Add(listBorder);

            // Select All / Deselect All + status
            var selectionBar = new DockPanel { Margin = new Thickness(0, 4, 0, 12) };

            var selAllBtn = MakeSmallButton("☑ Select All");
            selAllBtn.Click += (s, e) => SetAllChecked(true);
            var deselAllBtn = MakeSmallButton("☐ Deselect All");
            deselAllBtn.Margin = new Thickness(8, 0, 0, 0);
            deselAllBtn.Click += (s, e) => SetAllChecked(false);

            var selBtns = new StackPanel { Orientation = Orientation.Horizontal };
            selBtns.Children.Add(selAllBtn);
            selBtns.Children.Add(deselAllBtn);
            DockPanel.SetDock(selBtns, Dock.Left);
            selectionBar.Children.Add(selBtns);

            _selectionStatus = new TextBlock
            {
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_selectionStatus, Dock.Right);
            selectionBar.Children.Add(_selectionStatus);

            content.Children.Add(selectionBar);

            // Radio change handlers
            sheetsRadio.Checked += (s, e) => { _showingSheets = true; PopulateItems(); };
            viewsRadio.Checked += (s, e) => { _showingSheets = false; PopulateItems(); };

            // ───── Section 2: Export Settings ─────
            content.Children.Add(DarkTheme.MakeSeparator());
            content.Children.Add(DarkTheme.MakeSectionHeader("Export Settings", DarkTheme.CatExport));

            var settingsGrid = new Grid();
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // spacer
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left column — Page Settings
            var leftCol = new StackPanel();
            leftCol.Children.Add(DarkTheme.MakeLabel("Paper Size"));
            _paperSizeCombo = DarkTheme.MakeComboBox(
                new[] { "Auto (Use Sheet Size)", "A0", "A1", "A2", "A3", "A4", "Letter", "Legal", "Tabloid" },
                "Auto (Use Sheet Size)");
            _paperSizeCombo.Margin = new Thickness(0, 0, 0, 10);
            leftCol.Children.Add(_paperSizeCombo);

            leftCol.Children.Add(DarkTheme.MakeLabel("Orientation"));
            _orientationCombo = DarkTheme.MakeComboBox(new[] { "Auto", "Portrait", "Landscape" }, "Auto");
            _orientationCombo.Margin = new Thickness(0, 0, 0, 10);
            leftCol.Children.Add(_orientationCombo);

            leftCol.Children.Add(DarkTheme.MakeLabel("Export Quality (DPI)"));
            _dpiCombo = DarkTheme.MakeComboBox(new[] { "72 DPI", "150 DPI", "300 DPI", "600 DPI" }, "300 DPI");
            _dpiCombo.Margin = new Thickness(0, 0, 0, 10);
            leftCol.Children.Add(_dpiCombo);

            leftCol.Children.Add(DarkTheme.MakeLabel("Color Depth"));
            _colorCombo = DarkTheme.MakeComboBox(new[] { "Color", "Grayscale", "Black & White" }, "Color");
            leftCol.Children.Add(_colorCombo);

            Grid.SetColumn(leftCol, 0);
            settingsGrid.Children.Add(leftCol);

            // Right column — Options
            var rightCol = new StackPanel();

            _combineCheck = DarkTheme.MakeCheckBox("Combine into Single PDF", false);
            _combineCheck.Margin = new Thickness(0, 0, 0, 8);
            rightCol.Children.Add(_combineCheck);

            rightCol.Children.Add(DarkTheme.MakeLabel("Combined File Name"));
            _combinedNameBox = DarkTheme.MakeTextBox(placeholder: "e.g. Project_AllSheets");
            _combinedNameBox.IsEnabled = false;
            _combinedNameBox.Margin = new Thickness(0, 0, 0, 10);
            rightCol.Children.Add(_combinedNameBox);

            _combineCheck.Checked += (s, e) => _combinedNameBox.IsEnabled = true;
            _combineCheck.Unchecked += (s, e) => _combinedNameBox.IsEnabled = false;

            _hideCropCheck = DarkTheme.MakeCheckBox("Hide Crop Boundaries", true);
            _hideCropCheck.Margin = new Thickness(0, 4, 0, 0);
            rightCol.Children.Add(_hideCropCheck);

            _hideScopeCheck = DarkTheme.MakeCheckBox("Hide Scope Boxes", true);
            _hideScopeCheck.Margin = new Thickness(0, 4, 0, 0);
            rightCol.Children.Add(_hideScopeCheck);

            _hideRefCheck = DarkTheme.MakeCheckBox("Hide Reference Planes", true);
            _hideRefCheck.Margin = new Thickness(0, 8, 0, 8);
            rightCol.Children.Add(_hideRefCheck);

            rightCol.Children.Add(DarkTheme.MakeLabel("Raster Quality"));
            _rasterQualityCombo = DarkTheme.MakeComboBox(new[] { "Low", "Medium", "High" }, "High");
            rightCol.Children.Add(_rasterQualityCombo);

            Grid.SetColumn(rightCol, 2);
            settingsGrid.Children.Add(rightCol);

            content.Children.Add(settingsGrid);

            // ───── Section 3: Naming & Output ─────
            content.Children.Add(DarkTheme.MakeSeparator());
            content.Children.Add(DarkTheme.MakeSectionHeader("Naming & Output", DarkTheme.CatExport));

            content.Children.Add(DarkTheme.MakeLabel("File Naming Pattern (when not combined)"));
            _namingPatternBox = DarkTheme.MakeTextBox("{SheetNumber}-{SheetName}");
            _namingPatternBox.Margin = new Thickness(0, 0, 0, 6);
            content.Children.Add(_namingPatternBox);

            // Token badges
            content.Children.Add(new TextBlock
            {
                Text = "Available Tokens — click to insert:",
                FontSize = 10,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 4)
            });
            var tokenPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var token in new[] { "{SheetNumber}", "{SheetName}", "{Revision}", "{Date}", "{ProjectName}" })
            {
                var tokenBtn = MakeTokenBadge(token);
                tokenBtn.MouseLeftButtonUp += (s, e) =>
                {
                    var pos = _namingPatternBox.CaretIndex;
                    _namingPatternBox.Text = _namingPatternBox.Text.Insert(pos, token);
                    _namingPatternBox.CaretIndex = pos + token.Length;
                    _namingPatternBox.Focus();
                };
                tokenPanel.Children.Add(tokenBtn);
            }
            content.Children.Add(tokenPanel);

            // Output folder
            content.Children.Add(DarkTheme.MakeLabel("Output Folder"));
            var folderRow = new DockPanel();
            var browseBtn = DarkTheme.MakeCancelButton("📁 Browse");
            browseBtn.Padding = new Thickness(12, 6, 12, 6);
            browseBtn.FontSize = 12;
            DockPanel.SetDock(browseBtn, Dock.Right);
            browseBtn.Margin = new Thickness(8, 0, 0, 0);
            folderRow.Children.Add(browseBtn);

            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\PDF");
            folderRow.Children.Add(_outputFolderBox);

            browseBtn.Click += (s, e) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select output folder for PDF export"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputFolderBox.Text = dlg.SelectedPath;
                    _outputFolderBox.Foreground = DarkTheme.FgWhite;
                }
            };

            content.Children.Add(folderRow);

            scroller.Content = content;
            Grid.SetRow(scroller, 1);
            mainGrid.Children.Add(scroller);

            // ══════════════════ FOOTER ══════════════════
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            Button cancelBtn, exportBtn;
            var btnPanel = DarkTheme.MakeButtonPanel("📄 Export ▶", out cancelBtn, out exportBtn);
            cancelBtn.Click += (s, e) => Close();
            exportBtn.Click += ExportBtn_Click;
            footer.Child = btnPanel;

            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;

            // Populate initial data
            PopulateItems();
        }

        // ── Data Population ───────────────────────────────────────────

        private void PopulateItems()
        {
            _itemListPanel.Children.Clear();
            _itemCheckboxes.Clear();

            if (_showingSheets)
            {
                // Column headers
                var headerRow = MakeItemRow("Sheet Number", "Sheet Name", "Size", isHeader: true);
                _itemListPanel.Children.Add(headerRow);

                foreach (var sheet in SampleSheets)
                {
                    var cb = DarkTheme.MakeCheckBox("", false);
                    cb.Checked += (s, e) => UpdateSelectionStatus();
                    cb.Unchecked += (s, e) => UpdateSelectionStatus();
                    _itemCheckboxes.Add(cb);

                    var row = new DockPanel
                    {
                        Margin = new Thickness(8, 2, 8, 2)
                    };

                    DockPanel.SetDock(cb, Dock.Left);
                    cb.Margin = new Thickness(0, 0, 8, 0);
                    cb.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(cb);

                    // Size badge
                    var sizeTb = new TextBlock
                    {
                        Text = sheet[2],
                        FontSize = 11,
                        Foreground = DarkTheme.FgDim,
                        Width = 40,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(sizeTb, Dock.Right);
                    row.Children.Add(sizeTb);

                    // Sheet number
                    var numTb = new TextBlock
                    {
                        Text = sheet[0],
                        FontSize = 12,
                        Foreground = DarkTheme.FgGold,
                        Width = 120,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(numTb, Dock.Left);
                    row.Children.Add(numTb);

                    // Sheet name
                    var nameTb = new TextBlock
                    {
                        Text = sheet[1],
                        FontSize = 12,
                        Foreground = DarkTheme.FgLight,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    row.Children.Add(nameTb);

                    // Store tag for search
                    row.Tag = $"{sheet[0]} {sheet[1]}".ToLower();

                    _itemListPanel.Children.Add(row);
                }
            }
            else
            {
                // Views mode
                var headerRow = MakeItemRow("View Name", "Type", "", isHeader: true);
                _itemListPanel.Children.Add(headerRow);

                foreach (var view in SampleViews)
                {
                    var cb = DarkTheme.MakeCheckBox("", false);
                    cb.Checked += (s, e) => UpdateSelectionStatus();
                    cb.Unchecked += (s, e) => UpdateSelectionStatus();
                    _itemCheckboxes.Add(cb);

                    var row = new DockPanel
                    {
                        Margin = new Thickness(8, 2, 8, 2)
                    };

                    DockPanel.SetDock(cb, Dock.Left);
                    cb.Margin = new Thickness(0, 0, 8, 0);
                    cb.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(cb);

                    // View type
                    var typeTb = new TextBlock
                    {
                        Text = view[1],
                        FontSize = 11,
                        Foreground = DarkTheme.FgDim,
                        Width = 80,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(typeTb, Dock.Right);
                    row.Children.Add(typeTb);

                    // View name
                    var nameTb = new TextBlock
                    {
                        Text = view[0],
                        FontSize = 12,
                        Foreground = DarkTheme.FgLight,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    row.Children.Add(nameTb);

                    row.Tag = $"{view[0]} {view[1]}".ToLower();

                    _itemListPanel.Children.Add(row);
                }
            }

            UpdateSelectionStatus();
        }

        private FrameworkElement MakeItemRow(string col1, string col2, string col3, bool isHeader = false)
        {
            var row = new DockPanel
            {
                Margin = new Thickness(8, 4, 8, 4)
            };

            var spacer = new Border { Width = 28 }; // checkbox space
            DockPanel.SetDock(spacer, Dock.Left);
            row.Children.Add(spacer);

            var c3 = new TextBlock
            {
                Text = col3,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Width = isHeader && string.IsNullOrEmpty(col3) ? 0 : 40,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(c3, Dock.Right);
            row.Children.Add(c3);

            var c1 = new TextBlock
            {
                Text = col1,
                FontSize = 11,
                Foreground = isHeader ? DarkTheme.FgDim : DarkTheme.FgLight,
                Width = 120,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(c1, Dock.Left);
            row.Children.Add(c1);

            var c2 = new TextBlock
            {
                Text = col2,
                FontSize = 11,
                Foreground = isHeader ? DarkTheme.FgDim : DarkTheme.FgLight,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(c2);

            // Underline for header
            if (isHeader)
            {
                var wrapper = new StackPanel();
                wrapper.Children.Add(row);
                wrapper.Children.Add(new Border
                {
                    Height = 1,
                    Background = DarkTheme.BorderDim,
                    Margin = new Thickness(8, 2, 8, 2)
                });
                return wrapper;
            }

            return row;
        }

        // ── Selection Helpers ─────────────────────────────────────────

        private void SetAllChecked(bool isChecked)
        {
            foreach (var cb in _itemCheckboxes)
                cb.IsChecked = isChecked;
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            var selected = _itemCheckboxes.Count(cb => cb.IsChecked == true);
            var total = _itemCheckboxes.Count;
            var typeLabel = _showingSheets ? "sheets" : "views";
            _selectionStatus.Text = $"{selected} of {total} {typeLabel} selected";
        }

        private void FilterItems()
        {
            var searchText = _searchBox.Foreground == DarkTheme.FgDim
                ? "" // placeholder is showing
                : (_searchBox.Text ?? "").ToLower();

            int cbIndex = 0;
            foreach (UIElement child in _itemListPanel.Children)
            {
                if (child is DockPanel dp && dp.Tag is string tag)
                {
                    var visible = string.IsNullOrEmpty(searchText) || tag.Contains(searchText);
                    dp.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    if (cbIndex < _itemCheckboxes.Count)
                    {
                        // keep checkbox in sync with visibility
                    }
                    cbIndex++;
                }
            }
        }

        // ── Export Click ──────────────────────────────────────────────

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = _itemCheckboxes.Count(cb => cb.IsChecked == true);
            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one sheet or view to export.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build the prompt string for the AI
            var sb = new StringBuilder();
            sb.Append("Please run export_to_pdf");

            var parts = new List<string>();

            // Collect selected item info
            var selectedItems = new List<string>();
            int idx = 0;
            foreach (UIElement child in _itemListPanel.Children)
            {
                if (child is DockPanel dp && dp.Tag != null)
                {
                    if (idx < _itemCheckboxes.Count && _itemCheckboxes[idx].IsChecked == true)
                    {
                        selectedItems.Add(dp.Tag.ToString());
                    }
                    idx++;
                }
            }

            if (selectedItems.Count > 0)
            {
                var typeLabel = _showingSheets ? "sheets" : "views";
                parts.Add($"{typeLabel}: {string.Join(", ", selectedItems)}");
            }

            // Paper size
            var paperSize = GetComboValue(_paperSizeCombo);
            if (paperSize != "Auto (Use Sheet Size)")
                parts.Add($"paper size: {paperSize}");

            // Orientation
            var orientation = GetComboValue(_orientationCombo);
            if (orientation != "Auto")
                parts.Add($"orientation: {orientation}");

            // DPI
            parts.Add($"DPI: {GetComboValue(_dpiCombo)}");

            // Color
            var color = GetComboValue(_colorCombo);
            if (color != "Color")
                parts.Add($"color mode: {color}");

            // Combine
            if (_combineCheck.IsChecked == true)
            {
                parts.Add("combine into single PDF");
                var combinedName = GetTextValue(_combinedNameBox);
                if (!string.IsNullOrEmpty(combinedName))
                    parts.Add($"combined file name: {combinedName}");
            }

            // Visibility toggles
            if (_hideCropCheck.IsChecked == true) parts.Add("hide crop boundaries");
            if (_hideScopeCheck.IsChecked == true) parts.Add("hide scope boxes");
            if (_hideRefCheck.IsChecked == true) parts.Add("hide reference planes");

            // Raster quality
            parts.Add($"raster quality: {GetComboValue(_rasterQualityCombo)}");

            // Naming pattern
            var naming = GetTextValue(_namingPatternBox);
            if (!string.IsNullOrEmpty(naming) && naming != "{SheetNumber}-{SheetName}")
                parts.Add($"naming pattern: {naming}");

            // Output folder
            var folder = GetTextValue(_outputFolderBox);
            if (!string.IsNullOrEmpty(folder))
                parts.Add($"output folder: {folder}");

            if (parts.Count > 0)
            {
                sb.Append(" with ");
                sb.Append(string.Join(", ", parts));
            }

            Close();
            DirectExecutor.RunAsync("export_to_pdf", DirectExecutor.Params(
                ("sheets", string.Join(", ", selectedItems)),
                ("paperSize", paperSize), ("orientation", orientation),
                ("dpi", GetComboValue(_dpiCombo)), ("colorMode", color),
                ("combine", _combineCheck.IsChecked == true ? "true" : null),
                ("combinedName", _combineCheck.IsChecked == true ? GetTextValue(_combinedNameBox) : null),
                ("hideCrop", _hideCropCheck.IsChecked == true ? "true" : null),
                ("hideScope", _hideScopeCheck.IsChecked == true ? "true" : null),
                ("hideRef", _hideRefCheck.IsChecked == true ? "true" : null),
                ("rasterQuality", GetComboValue(_rasterQualityCombo)),
                ("namingPattern", naming), ("outputFolder", folder)
            ), "Export to PDF");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private string GetComboValue(ComboBox combo)
        {
            return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        }

        private string GetTextValue(TextBox tb)
        {
            if (tb.Foreground == DarkTheme.FgDim) return null; // placeholder showing
            return tb.Text?.Trim();
        }

        private static Border MakeTokenBadge(string token)
        {
            var badge = new Border
            {
                Background = DarkTheme.BgInput,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 4),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            badge.Child = new TextBlock
            {
                Text = token,
                FontSize = 11,
                Foreground = DarkTheme.FgGold,
                FontFamily = new FontFamily("Consolas")
            };
            badge.MouseEnter += (s, e) => badge.BorderBrush = DarkTheme.BorderAccent;
            badge.MouseLeave += (s, e) => badge.BorderBrush = DarkTheme.BorderDim;
            return badge;
        }

        private static Button MakeSmallButton(string text)
        {
            var btn = new Button
            {
                Content = text,
                Background = DarkTheme.BgCancel,
                Foreground = DarkTheme.FgLight,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = DarkTheme.BgCancelHover;
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgCancel;
            return btn;
        }
    }
}
