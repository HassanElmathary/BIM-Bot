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
    /// Dedicated Export to IFC window with 4-tab interface:
    /// General, Content (category filter), Property Sets, Output.
    /// </summary>
    public class ExportToIfcWindow : Window
    {
        // ── Tab system ────────────────────────────────────────────────
        private readonly List<Border> _tabHeaders = new List<Border>();
        private readonly List<FrameworkElement> _tabPanels = new List<FrameworkElement>();
        private int _activeTab = 0;

        // ── General tab controls ──────────────────────────────────────
        private ComboBox _ifcVersionCombo;
        private ComboBox _mvdCombo;
        private ComboBox _sitePlacementCombo;
        private CheckBox _includeSiteElevation;
        private CheckBox _exportBaseQuantities;
        private CheckBox _splitWallsByLevel;
        private CheckBox _exportCurrentViewOnly;
        private CheckBox _exportRooms;
        private CheckBox _export2dElements;
        private ComboBox _lodCombo;

        // ── Content tab controls ──────────────────────────────────────
        private readonly List<CheckBox> _categoryCheckboxes = new List<CheckBox>();
        private TextBlock _categoryStatus;

        // ── Property Sets tab controls ────────────────────────────────
        private CheckBox _exportCommonPsets;
        private CheckBox _exportRevitPsets;
        private CheckBox _exportUserPsets;
        private TextBox _psetMappingFile;
        private TextBox _familyMappingFile;
        private ComboBox _classificationCombo;

        // ── Output tab controls ───────────────────────────────────────
        private TextBox _fileNameBox;
        private TextBox _outputFolderBox;
        private TextBlock _previewText;

        // Category data
        private static readonly string[][] SampleCategories = new[]
        {
            new[] { "Walls",               "IfcWall",              "45" },
            new[] { "Doors",               "IfcDoor",              "32" },
            new[] { "Windows",             "IfcWindow",            "28" },
            new[] { "Floors",              "IfcSlab",              "12" },
            new[] { "Columns",             "IfcColumn",            "18" },
            new[] { "Beams",               "IfcBeam",              "8" },
            new[] { "Stairs",              "IfcStairFlight",       "4" },
            new[] { "Rooms",               "IfcSpace",             "22" },
            new[] { "Furniture",           "IfcFurnishingElement", "56" },
            new[] { "Structural Found.",   "IfcFooting",           "6" }
        };

        // ── Constructor ───────────────────────────────────────────────
        public ExportToIfcWindow()
        {
            Title = "🏗️ Export to IFC";
            Width = 680;
            Height = 620;
            MinWidth = 580;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });     // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });     // Tab bar
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
                Text = "🏗️ Export to IFC",
                FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Export model to IFC for BIM coordination",
                FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0)
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // ══════════════════ TAB BAR ══════════════════
            var tabBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = DarkTheme.BgDark,
                Margin = new Thickness(20, 0, 20, 0)
            };
            var tabNames = new[] { "General", "Content", "Property Sets", "Output" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                var idx = i;
                var tabHeader = new Border
                {
                    Padding = new Thickness(16, 8, 16, 8),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 2, 0)
                };
                tabHeader.Child = new TextBlock
                {
                    Text = tabNames[i],
                    FontSize = 13,
                    Foreground = DarkTheme.FgLight,
                    FontWeight = FontWeights.SemiBold
                };
                tabHeader.MouseLeftButtonUp += (s, e) => SwitchTab(idx);
                _tabHeaders.Add(tabHeader);
                tabBar.Children.Add(tabHeader);
            }
            Grid.SetRow(tabBar, 1);
            mainGrid.Children.Add(tabBar);

            // ══════════════════ TAB CONTENT ══════════════════
            var contentContainer = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            _tabPanels.Add(BuildGeneralTab());
            _tabPanels.Add(BuildContentTab());
            _tabPanels.Add(BuildPropertySetsTab());
            _tabPanels.Add(BuildOutputTab());
            foreach (var panel in _tabPanels)
                contentContainer.Children.Add(panel);
            Grid.SetRow(contentContainer, 2);
            mainGrid.Children.Add(contentContainer);

            // ══════════════════ FOOTER ══════════════════
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerDock = new DockPanel();

            var saveBtn = DarkTheme.MakeCancelButton("💾 Save Setup");
            DockPanel.SetDock(saveBtn, Dock.Left);
            footerDock.Children.Add(saveBtn);

            Button cancelBtn, exportBtn;
            var btnPanel = DarkTheme.MakeButtonPanel("🏗️ Export ▶", out cancelBtn, out exportBtn);
            DockPanel.SetDock(btnPanel, Dock.Right);
            cancelBtn.Click += (s, e) => Close();
            exportBtn.Click += ExportBtn_Click;
            footerDock.Children.Add(btnPanel);

            footer.Child = footerDock;
            Grid.SetRow(footer, 3);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
            SwitchTab(0);
        }

        // ── Tab Switching ─────────────────────────────────────────────

        private void SwitchTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < _tabPanels.Count; i++)
            {
                _tabPanels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
                _tabHeaders[i].Background = i == index ? DarkTheme.BgCard : Brushes.Transparent;
                _tabHeaders[i].BorderBrush = i == index ? DarkTheme.BgAccent : Brushes.Transparent;
                _tabHeaders[i].BorderThickness = new Thickness(0, 0, 0, i == index ? 2 : 0);
            }
        }

        // ── Tab 1: General ────────────────────────────────────────────

        private FrameworkElement BuildGeneralTab()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var stack = new StackPanel();

            // Export Setup
            var setupContent = new StackPanel();
            setupContent.Children.Add(DarkTheme.MakeLabel("Export Setup"));
            var setupCombo = DarkTheme.MakeComboBox(
                new[] { "IFC2x3 Coordination View 2.0", "IFC4 Reference View", "IFC4 Design Transfer", "IFC4x3" },
                "IFC2x3 Coordination View 2.0");
            setupCombo.Margin = new Thickness(0, 0, 0, 12);
            setupContent.Children.Add(setupCombo);

            // Version + MVD side by side
            var versionGrid = new Grid();
            versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var vLeft = new StackPanel();
            vLeft.Children.Add(DarkTheme.MakeLabel("IFC Version"));
            _ifcVersionCombo = DarkTheme.MakeComboBox(new[] { "IFC2x3", "IFC4", "IFC4x3" }, "IFC2x3");
            vLeft.Children.Add(_ifcVersionCombo);
            Grid.SetColumn(vLeft, 0);
            versionGrid.Children.Add(vLeft);

            var vRight = new StackPanel();
            vRight.Children.Add(DarkTheme.MakeLabel("MVD / Model View Definition"));
            _mvdCombo = DarkTheme.MakeComboBox(
                new[] { "Coordination View 2.0", "Basic FM Handover", "Reference View", "Design Transfer" },
                "Coordination View 2.0");
            vRight.Children.Add(_mvdCombo);
            Grid.SetColumn(vRight, 2);
            versionGrid.Children.Add(vRight);

            versionGrid.Margin = new Thickness(0, 0, 0, 12);
            setupContent.Children.Add(versionGrid);

            stack.Children.Add(DarkTheme.MakeGroupBox("IFC Configuration", setupContent));

            // Coordinate System
            var coordContent = new StackPanel();
            coordContent.Children.Add(DarkTheme.MakeLabel("Site Placement Base"));
            _sitePlacementCombo = DarkTheme.MakeComboBox(
                new[] { "Shared Coordinates", "Project Base Point", "Survey Point", "Internal" },
                "Shared Coordinates");
            _sitePlacementCombo.Margin = new Thickness(0, 0, 0, 8);
            coordContent.Children.Add(_sitePlacementCombo);

            _includeSiteElevation = DarkTheme.MakeCheckBox("Include IFCSITE elevation in site local placement origin", true);
            coordContent.Children.Add(_includeSiteElevation);

            stack.Children.Add(DarkTheme.MakeGroupBox("Coordinate System", coordContent));

            // Export Options
            var optContent = new StackPanel();
            _exportBaseQuantities = DarkTheme.MakeCheckBox("Export Base Quantities (QTO)", true);
            _exportBaseQuantities.Margin = new Thickness(0, 0, 0, 4);
            optContent.Children.Add(_exportBaseQuantities);

            _splitWallsByLevel = DarkTheme.MakeCheckBox("Split Walls and Columns by Level", true);
            _splitWallsByLevel.Margin = new Thickness(0, 0, 0, 4);
            optContent.Children.Add(_splitWallsByLevel);

            _exportCurrentViewOnly = DarkTheme.MakeCheckBox("Export only elements visible in current view", false);
            _exportCurrentViewOnly.Margin = new Thickness(0, 0, 0, 4);
            optContent.Children.Add(_exportCurrentViewOnly);

            _exportRooms = DarkTheme.MakeCheckBox("Export Rooms / Spaces", true);
            _exportRooms.Margin = new Thickness(0, 0, 0, 4);
            optContent.Children.Add(_exportRooms);

            _export2dElements = DarkTheme.MakeCheckBox("Export 2D Plan View Elements", true);
            _export2dElements.Margin = new Thickness(0, 0, 0, 10);
            optContent.Children.Add(_export2dElements);

            optContent.Children.Add(DarkTheme.MakeLabel("Level of Detail"));
            _lodCombo = DarkTheme.MakeComboBox(new[] { "Low", "Medium", "High", "Extra High" }, "Medium");
            optContent.Children.Add(_lodCombo);

            stack.Children.Add(DarkTheme.MakeGroupBox("Export Options", optContent));

            scroll.Content = stack;
            return scroll;
        }

        // ── Tab 2: Content ────────────────────────────────────────────

        private FrameworkElement BuildContentTab()
        {
            var stack = new StackPanel();

            // Search
            var searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search categories...");
            searchBox.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(searchBox);

            // Select All / Deselect All
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var selAllBtn = MakeSmallBtn("☑ Select All");
            selAllBtn.Click += (s, e) => { foreach (var cb in _categoryCheckboxes) cb.IsChecked = true; };
            var deselBtn = MakeSmallBtn("☐ Deselect All");
            deselBtn.Margin = new Thickness(8, 0, 0, 0);
            deselBtn.Click += (s, e) => { foreach (var cb in _categoryCheckboxes) cb.IsChecked = false; };
            actionRow.Children.Add(selAllBtn);
            actionRow.Children.Add(deselBtn);
            stack.Children.Add(actionRow);

            // Category list
            var listBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 320
            };
            var listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var listPanel = new StackPanel();

            // Header row
            var hdr = new Grid { Margin = new Thickness(8, 6, 8, 4) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            AddGridText(hdr, "", 0, true);
            AddGridText(hdr, "Category", 1, true);
            AddGridText(hdr, "IFC Class", 2, true);
            AddGridText(hdr, "Elements", 3, true);
            listPanel.Children.Add(hdr);
            listPanel.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(8, 0, 8, 0) });

            foreach (var cat in SampleCategories)
            {
                var row = new Grid { Margin = new Thickness(8, 3, 8, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                var cb = DarkTheme.MakeCheckBox("", true);
                cb.Checked += (s, e) => UpdateCategoryStatus();
                cb.Unchecked += (s, e) => UpdateCategoryStatus();
                _categoryCheckboxes.Add(cb);
                Grid.SetColumn(cb, 0);
                row.Children.Add(cb);

                AddGridText(row, "▶ " + cat[0], 1, false);
                AddGridText(row, cat[1], 2, false, DarkTheme.FgDim);
                AddGridText(row, cat[2], 3, false, DarkTheme.FgDim);

                row.Tag = cat[0].ToLower();
                listPanel.Children.Add(row);
            }

            listScroll.Content = listPanel;
            listBorder.Child = listScroll;
            stack.Children.Add(listBorder);

            // Search filter
            searchBox.TextChanged += (s, e) =>
            {
                var q = searchBox.Foreground == DarkTheme.FgDim ? "" : (searchBox.Text ?? "").ToLower();
                foreach (UIElement child in listPanel.Children)
                {
                    if (child is Grid g && g.Tag is string tag)
                        g.Visibility = string.IsNullOrEmpty(q) || tag.Contains(q) ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            // Status
            _categoryStatus = new TextBlock
            {
                FontSize = 11, Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(_categoryStatus);
            UpdateCategoryStatus();

            return stack;
        }

        // ── Tab 3: Property Sets ──────────────────────────────────────

        private FrameworkElement BuildPropertySetsTab()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var stack = new StackPanel();

            // Standard Property Sets
            var psetContent = new StackPanel();
            _exportCommonPsets = DarkTheme.MakeCheckBox("Export IFC Common Property Sets", true);
            _exportCommonPsets.Margin = new Thickness(0, 0, 0, 4);
            psetContent.Children.Add(_exportCommonPsets);

            _exportRevitPsets = DarkTheme.MakeCheckBox("Export Revit Property Sets", true);
            _exportRevitPsets.Margin = new Thickness(0, 0, 0, 4);
            psetContent.Children.Add(_exportRevitPsets);

            _exportUserPsets = DarkTheme.MakeCheckBox("Export User-Defined Property Sets", false);
            psetContent.Children.Add(_exportUserPsets);

            stack.Children.Add(DarkTheme.MakeGroupBox("Standard Property Sets", psetContent));

            // Custom Property Set Mapping
            var mapContent = new StackPanel();
            mapContent.Children.Add(DarkTheme.MakeLabel("Custom Property Set Mapping File (optional)"));
            var psetRow = new DockPanel();
            var psetBrowse = DarkTheme.MakeCancelButton("📁 Browse");
            psetBrowse.Padding = new Thickness(12, 6, 12, 6);
            psetBrowse.FontSize = 12;
            DockPanel.SetDock(psetBrowse, Dock.Right);
            psetBrowse.Margin = new Thickness(8, 0, 0, 0);
            psetRow.Children.Add(psetBrowse);
            _psetMappingFile = DarkTheme.MakeTextBox(placeholder: "Path to .txt mapping file");
            psetRow.Children.Add(_psetMappingFile);
            mapContent.Children.Add(psetRow);

            mapContent.Children.Add(new Border { Height = 8 });

            mapContent.Children.Add(DarkTheme.MakeLabel("Family Mapping File (optional)"));
            var famRow = new DockPanel();
            var famBrowse = DarkTheme.MakeCancelButton("📁 Browse");
            famBrowse.Padding = new Thickness(12, 6, 12, 6);
            famBrowse.FontSize = 12;
            DockPanel.SetDock(famBrowse, Dock.Right);
            famBrowse.Margin = new Thickness(8, 0, 0, 0);
            famRow.Children.Add(famBrowse);
            _familyMappingFile = DarkTheme.MakeTextBox(placeholder: "Path to .txt mapping file");
            famRow.Children.Add(_familyMappingFile);
            mapContent.Children.Add(famRow);

            stack.Children.Add(DarkTheme.MakeGroupBox("Mapping Files", mapContent));

            // Classification
            var classContent = new StackPanel();
            classContent.Children.Add(DarkTheme.MakeLabel("Classification System"));
            _classificationCombo = DarkTheme.MakeComboBox(
                new[] { "None", "Uniclass", "OmniClass", "NBS" }, "None");
            classContent.Children.Add(_classificationCombo);

            stack.Children.Add(DarkTheme.MakeGroupBox("Classification", classContent));

            scroll.Content = stack;
            return scroll;
        }

        // ── Tab 4: Output ─────────────────────────────────────────────

        private FrameworkElement BuildOutputTab()
        {
            var stack = new StackPanel();

            var outContent = new StackPanel();

            // File Name
            outContent.Children.Add(DarkTheme.MakeLabel("File Name"));
            _fileNameBox = DarkTheme.MakeTextBox("{ProjectName}_IFC");
            _fileNameBox.Margin = new Thickness(0, 0, 0, 6);
            _fileNameBox.TextChanged += (s, e) => UpdatePreview();
            outContent.Children.Add(_fileNameBox);

            // Token badges
            outContent.Children.Add(new TextBlock
            {
                Text = "Available Tokens — click to insert:",
                FontSize = 10, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 4)
            });
            var tokenPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var token in new[] { "{ProjectName}", "{Date}", "{IFCVersion}", "{CoordSystem}" })
            {
                var badge = MakeTokenBadge(token);
                badge.MouseLeftButtonUp += (s, e) =>
                {
                    var pos = _fileNameBox.CaretIndex;
                    _fileNameBox.Text = _fileNameBox.Text.Insert(pos, token);
                    _fileNameBox.CaretIndex = pos + token.Length;
                    _fileNameBox.Focus();
                };
                tokenPanel.Children.Add(badge);
            }
            outContent.Children.Add(tokenPanel);

            // Output folder
            outContent.Children.Add(DarkTheme.MakeLabel("Output Folder"));
            var folderRow = new DockPanel();
            var browseBtn = DarkTheme.MakeCancelButton("📁 Browse");
            browseBtn.Padding = new Thickness(12, 6, 12, 6);
            browseBtn.FontSize = 12;
            DockPanel.SetDock(browseBtn, Dock.Right);
            browseBtn.Margin = new Thickness(8, 0, 0, 0);
            folderRow.Children.Add(browseBtn);
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\IFC");
            _outputFolderBox.TextChanged += (s, e) => UpdatePreview();
            folderRow.Children.Add(_outputFolderBox);

            browseBtn.Click += (s, e) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select output folder for IFC export"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputFolderBox.Text = dlg.SelectedPath;
                    _outputFolderBox.Foreground = DarkTheme.FgWhite;
                }
            };
            outContent.Children.Add(folderRow);

            outContent.Children.Add(DarkTheme.MakeSeparator());

            // Preview
            outContent.Children.Add(DarkTheme.MakeLabel("Preview"));
            _previewText = new TextBlock
            {
                FontSize = 12, Foreground = DarkTheme.FgGreen,
                TextWrapping = TextWrapping.Wrap
            };
            outContent.Children.Add(_previewText);

            stack.Children.Add(DarkTheme.MakeGroupBox("Output Settings", outContent));

            UpdatePreview();
            return stack;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void UpdateCategoryStatus()
        {
            var selected = _categoryCheckboxes.Count(cb => cb.IsChecked == true);
            var total = _categoryCheckboxes.Count;
            var elements = 0;
            for (int i = 0; i < selected && i < SampleCategories.Length; i++)
            {
                if (_categoryCheckboxes[i].IsChecked == true)
                    int.TryParse(SampleCategories[i][2], out var n);
            }
            // Simple count for display
            int elemCount = 0;
            for (int i = 0; i < _categoryCheckboxes.Count && i < SampleCategories.Length; i++)
            {
                if (_categoryCheckboxes[i].IsChecked == true && int.TryParse(SampleCategories[i][2], out var n))
                    elemCount += n;
            }
            _categoryStatus.Text = $"Selected: {selected} of {total} categories ({elemCount} elements)";
        }

        private void UpdatePreview()
        {
            var fileName = _fileNameBox?.Text ?? "output";
            var folder = (_outputFolderBox?.Foreground == DarkTheme.FgDim) ? @"C:\Export\IFC" : (_outputFolderBox?.Text ?? @"C:\Export\IFC");
            if (string.IsNullOrWhiteSpace(folder)) folder = @"C:\Export\IFC";
            var resolved = fileName.Replace("{ProjectName}", "MyProject")
                                    .Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd"))
                                    .Replace("{IFCVersion}", "IFC2x3")
                                    .Replace("{CoordSystem}", "SharedCoords");
            if (_previewText != null)
                _previewText.Text = $"📄 {resolved}.ifc → {folder}\\{resolved}.ifc";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.Append("Please run export_to_ifc");

            var parts = new List<string>();

            // IFC Version
            parts.Add($"IFC version: {GetCombo(_ifcVersionCombo)}");
            parts.Add($"MVD: {GetCombo(_mvdCombo)}");
            parts.Add($"site placement: {GetCombo(_sitePlacementCombo)}");
            parts.Add($"level of detail: {GetCombo(_lodCombo)}");

            // Options
            if (_exportBaseQuantities.IsChecked == true) parts.Add("export base quantities");
            if (_splitWallsByLevel.IsChecked == true) parts.Add("split walls by level");
            if (_exportCurrentViewOnly.IsChecked == true) parts.Add("current view only");
            if (_exportRooms.IsChecked == true) parts.Add("include rooms/spaces");

            // Categories
            var excludedCats = new List<string>();
            for (int i = 0; i < _categoryCheckboxes.Count && i < SampleCategories.Length; i++)
            {
                if (_categoryCheckboxes[i].IsChecked != true)
                    excludedCats.Add(SampleCategories[i][0]);
            }
            if (excludedCats.Count > 0)
                parts.Add($"exclude categories: {string.Join(", ", excludedCats)}");

            // Property sets
            if (_exportCommonPsets.IsChecked == true) parts.Add("include common property sets");
            if (_exportRevitPsets.IsChecked == true) parts.Add("include Revit property sets");

            var classification = GetCombo(_classificationCombo);
            if (classification != "None") parts.Add($"classification: {classification}");

            // Output
            var fileName = GetText(_fileNameBox);
            if (!string.IsNullOrEmpty(fileName)) parts.Add($"file name: {fileName}");

            var folder = GetText(_outputFolderBox);
            if (!string.IsNullOrEmpty(folder)) parts.Add($"output folder: {folder}");

            if (parts.Count > 0)
            {
                sb.Append(" with ");
                sb.Append(string.Join(", ", parts));
            }

            Close();
            DirectExecutor.RunAsync("export_to_ifc", DirectExecutor.Params(
                ("settings", string.Join(", ", parts))
            ), "Export to IFC");
        }

        private static string GetCombo(ComboBox c) => (c.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        private string GetText(TextBox tb) => tb.Foreground == DarkTheme.FgDim ? null : tb.Text?.Trim();

        private static void AddGridText(Grid grid, string text, int col, bool isHeader, SolidColorBrush fg = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = fg ?? (isHeader ? DarkTheme.FgDim : DarkTheme.FgLight),
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
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

        private static Button MakeSmallBtn(string text)
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
