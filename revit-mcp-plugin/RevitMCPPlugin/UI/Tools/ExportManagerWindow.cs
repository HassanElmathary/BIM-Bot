using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DB = Autodesk.Revit.DB;
using RevitMCPPlugin.Core;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    /// <summary>
    /// Unified Export Manager — ProSheets-style multi-format batch export.
    /// 3 tabs: Selection → Format → Create.
    /// </summary>
    public class ExportManagerWindow : Window
    {
        // ── Tabs ──────────────────────────────────────────────────────
        private readonly Border _tab1Indicator, _tab2Indicator, _tab3Indicator;
        private readonly Border _tab1Content, _tab2Content, _tab3Content;
        private readonly TextBlock _tab1Text, _tab2Text, _tab3Text;

        // ── Tab 1: Selection ──────────────────────────────────────────
        private StackPanel _itemListPanel;
        private TextBox _searchBox;
        private TextBlock _selectionStatus;
        private readonly List<CheckBox> _itemCheckboxes = new List<CheckBox>();
        private bool _showingSheets = true;
        private TextBlock _validationMsg;

        // ── Tab 2: Format checkboxes ──────────────────────────────────
        private CheckBox _fmtPdf, _fmtDwg, _fmtDgn, _fmtDwf, _fmtNwc, _fmtIfc, _fmtImg;
        private StackPanel _formatSettingsPanel;

        // PDF settings
        private ComboBox _pdfPaperSize, _pdfOrientation, _pdfDpi, _pdfColor, _pdfRasterQuality;
        private CheckBox _pdfCombine, _pdfHideCrop, _pdfHideScope, _pdfHideRef;

        // DWG settings
        private ComboBox _dwgVersion;

        // IFC settings
        private ComboBox _ifcVersion;

        // IMG settings
        private ComboBox _imgFormat, _imgDpi;

        // ── Tab 3: Create ─────────────────────────────────────────────
        private TextBox _outputFolderBox, _namingPatternBox;
        private RadioButton _radioSameFolder, _radioSplitByFormat;
        private CheckBox _keepPaperSize;
        private TextBlock _progressText;
        private Border _progressBarFill;
        private StackPanel _queuePanel;

        // Nav buttons
        private Button _backBtn, _nextBtn;

        // State
        private int _activeTab = 0;

        // Real Revit data (populated from document)
        private List<string[]> _realSheets = new List<string[]>();
        private List<string[]> _realViews = new List<string[]>();

        // ══════════════════════════════════════════════════════════════
        // ██ Constructor ██
        // ══════════════════════════════════════════════════════════════
        public ExportManagerWindow()
        {
            Title = "Export Manager — Revit MCP";
            Width = 880;
            Height = 700;
            MinWidth = 750;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });     // tab bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });     // footer

            // ═══════════════ TAB BAR ═══════════════
            var tabBar = new Border
            {
                Background = DarkTheme.BgHeader,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };

            _tab1Text = new TextBlock(); _tab2Text = new TextBlock(); _tab3Text = new TextBlock();
            _tab1Indicator = new Border(); _tab2Indicator = new Border(); _tab3Indicator = new Border();

            tabRow.Children.Add(MakeTab("Selection", 0, ref _tab1Text, ref _tab1Indicator));
            tabRow.Children.Add(MakeTab("Format", 1, ref _tab2Text, ref _tab2Indicator));
            tabRow.Children.Add(MakeTab("Create", 2, ref _tab3Text, ref _tab3Indicator));

            tabBar.Child = tabRow;
            Grid.SetRow(tabBar, 0);
            mainGrid.Children.Add(tabBar);

            // ═══════════════ TAB CONTENT PANELS ═══════════════
            var contentArea = new Grid();

            _tab1Content = BuildSelectionTab();
            _tab2Content = BuildFormatTab();
            _tab3Content = BuildCreateTab();

            contentArea.Children.Add(_tab1Content);
            contentArea.Children.Add(_tab2Content);
            contentArea.Children.Add(_tab3Content);

            Grid.SetRow(contentArea, 1);
            mainGrid.Children.Add(contentArea);

            // ═══════════════ FOOTER ═══════════════
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 10, 20, 10),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            var footerPanel = new DockPanel();

            _selectionStatus = new TextBlock
            {
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_selectionStatus, Dock.Left);
            footerPanel.Children.Add(_selectionStatus);

            var navPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            _backBtn = DarkTheme.MakeCancelButton("◀ Back");
            _backBtn.Padding = new Thickness(16, 8, 16, 8);
            _backBtn.Click += (s, e) => SwitchTab(_activeTab - 1);

            _nextBtn = DarkTheme.MakePrimaryButton("Next ▶");
            _nextBtn.Padding = new Thickness(16, 8, 16, 8);
            _nextBtn.Margin = new Thickness(10, 0, 0, 0);
            _nextBtn.Click += NextBtn_Click;

            navPanel.Children.Add(_backBtn);
            navPanel.Children.Add(_nextBtn);
            DockPanel.SetDock(navPanel, Dock.Right);
            footerPanel.Children.Add(navPanel);

            footer.Child = footerPanel;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;

            // Initialize
            SwitchTab(0);
            PopulateItems();
        }

        // ══════════════════════════════════════════════════════════════
        // ██ TAB 1: Selection ██
        // ══════════════════════════════════════════════════════════════
        private Border BuildSelectionTab()
        {
            var wrapper = new Border();
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Radio: Sheets / Views + View/Sheet Set ──
            var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            var sheetsRadio = new RadioButton
            {
                Content = "● Sheets",
                IsChecked = true,
                Foreground = DarkTheme.FgLight,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 12, 0),
                GroupName = "ViewType",
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var viewsRadio = new RadioButton
            {
                Content = "○ Views",
                Foreground = DarkTheme.FgLight,
                FontSize = 13,
                GroupName = "ViewType",
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var radioPanel = new StackPanel { Orientation = Orientation.Horizontal };
            radioPanel.Children.Add(sheetsRadio);
            radioPanel.Children.Add(viewsRadio);
            DockPanel.SetDock(radioPanel, Dock.Left);
            topRow.Children.Add(radioPanel);

            // View/Sheet Set label + Search
            var rightControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var vssLabel = new TextBlock
            {
                Text = "View/Sheet Set",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            rightControls.Children.Add(vssLabel);

            var filterCombo = DarkTheme.MakeComboBox(new[] { "Filter by V/S Sets" }, "Filter by V/S Sets");
            filterCombo.Width = 140;
            filterCombo.FontSize = 11;
            rightControls.Children.Add(filterCombo);

            var saveSetBtn = DarkTheme.MakeCancelButton("Save V/S Set");
            saveSetBtn.Padding = new Thickness(10, 4, 10, 4);
            saveSetBtn.FontSize = 11;
            saveSetBtn.Margin = new Thickness(8, 0, 0, 0);
            rightControls.Children.Add(saveSetBtn);

            DockPanel.SetDock(rightControls, Dock.Right);
            topRow.Children.Add(rightControls);

            content.Children.Add(topRow);

            // ── Search bar ──
            var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

            var searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search...");
            searchBox.TextChanged += (s, e) => FilterItems(searchBox);
            searchRow.Children.Add(searchBox);

            // Store ref
            var fieldSearchBox = searchBox;

            content.Children.Add(searchRow);

            // ── Column headers ──
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // checkbox
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Sheet Number
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Sheet Name
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // Revision
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // Size
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // Custom File Name

            var allCheck = DarkTheme.MakeCheckBox("", false);
            allCheck.HorizontalAlignment = HorizontalAlignment.Center;
            allCheck.Checked += (s, e) => SetAllChecked(true);
            allCheck.Unchecked += (s, e) => SetAllChecked(false);
            Grid.SetColumn(allCheck, 0);
            headerRow.Children.Add(allCheck);

            AddColumnHeader(headerRow, "Sheet Number", 1);
            AddColumnHeader(headerRow, "Sheet Name", 2);
            AddColumnHeader(headerRow, "Revision", 3);
            AddColumnHeader(headerRow, "Size", 4);
            AddColumnHeader(headerRow, "Custom File Name", 5);

            var headerBorder = new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1, 1, 1, 0),
                Child = headerRow
            };
            content.Children.Add(headerBorder);

            // ── Item list ──
            var listBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x20)),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 380
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

            // ── Show only active checkbox ──
            var bottomRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var showActiveCheck = DarkTheme.MakeCheckBox("Show only active Sheets/Views", false);
            showActiveCheck.HorizontalAlignment = HorizontalAlignment.Right;
            showActiveCheck.FontSize = 11;
            DockPanel.SetDock(showActiveCheck, Dock.Right);
            bottomRow.Children.Add(showActiveCheck);
            content.Children.Add(bottomRow);

            // ── Validation message (hidden by default) ──
            _validationMsg = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00)),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            content.Children.Add(_validationMsg);

            sheetsRadio.Checked += (s, e) => { _showingSheets = true; PopulateItems(); };
            viewsRadio.Checked += (s, e) => { _showingSheets = false; PopulateItems(); };

            scroller.Content = content;
            wrapper.Child = scroller;
            return wrapper;
        }

        // ══════════════════════════════════════════════════════════════
        // ██ TAB 2: Format ██
        // ══════════════════════════════════════════════════════════════
        private Border BuildFormatTab()
        {
            var wrapper = new Border();
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Format checkbox row ──
            var fmtRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };

            _fmtPdf = MakeFormatToggle("PDF", true);
            _fmtDwg = MakeFormatToggle("DWG", false);
            _fmtDgn = MakeFormatToggle("DGN", false);
            _fmtDwf = MakeFormatToggle("DWF", false);
            _fmtNwc = MakeFormatToggle("NWC", false);
            _fmtIfc = MakeFormatToggle("IFC", false);
            _fmtImg = MakeFormatToggle("IMG", false);

            fmtRow.Children.Add(WrapFormatToggle(_fmtPdf, "PDF"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtDwg, "DWG"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtDgn, "DGN"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtDwf, "DWF"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtNwc, "NWC"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtIfc, "IFC"));
            fmtRow.Children.Add(WrapFormatToggle(_fmtImg, "IMG"));

            content.Children.Add(fmtRow);

            // ── Active format indicator bar ──
            var indicatorBar = new Border
            {
                Height = 3,
                Background = DarkTheme.CatExport,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 80,
                Margin = new Thickness(0, 0, 0, 16),
                CornerRadius = new CornerRadius(2)
            };
            content.Children.Add(indicatorBar);

            // ── Format settings area (3 columns) ──
            var settingsGrid = new Grid();
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // spacer
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // spacer
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ── Left Column: Paper & Zoom ──
            var leftCol = new StackPanel();

            var ppGroup = new StackPanel();
            ppGroup.Children.Add(MakeGroupLabel("Paper Placement"));
            var radioCenter = new RadioButton { Content = "Center", IsChecked = true, Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "Placement", Margin = new Thickness(0, 4, 0, 2) };
            var radioOffset = new RadioButton { Content = "Offset from corner", Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "Placement" };
            ppGroup.Children.Add(radioCenter);
            ppGroup.Children.Add(radioOffset);

            var offsetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 4, 0, 0) };
            var marginCombo = DarkTheme.MakeComboBox(new[] { "No Margin" }, "No Margin");
            marginCombo.Width = 120;
            marginCombo.FontSize = 11;
            offsetRow.Children.Add(marginCombo);
            ppGroup.Children.Add(offsetRow);

            var xyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 6, 0, 0) };
            xyRow.Children.Add(new TextBlock { Text = "X -", Foreground = DarkTheme.FgDim, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var xBox = DarkTheme.MakeTextBox("0.00mm");
            xBox.Width = 70;
            xBox.FontSize = 11;
            xyRow.Children.Add(xBox);
            xyRow.Children.Add(new TextBlock { Text = "  Y -", Foreground = DarkTheme.FgDim, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
            var yBox = DarkTheme.MakeTextBox("0.00mm");
            yBox.Width = 70;
            yBox.FontSize = 11;
            xyRow.Children.Add(yBox);
            ppGroup.Children.Add(xyRow);

            leftCol.Children.Add(DarkTheme.MakeGroupBox("Paper Placement", ppGroup));

            // Zoom group
            var zoomGroup = new StackPanel();
            var radioFit = new RadioButton { Content = "Fit to Page", IsChecked = true, Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "Zoom", Margin = new Thickness(0, 0, 0, 2) };
            var radioZoom = new RadioButton { Content = "Zoom", Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "Zoom" };
            zoomGroup.Children.Add(radioFit);
            zoomGroup.Children.Add(radioZoom);
            leftCol.Children.Add(DarkTheme.MakeGroupBox("Zoom", zoomGroup));

            // Printer group
            var printerGroup = new StackPanel();
            printerGroup.Children.Add(MakeGroupLabel("Printer"));
            var printerCombo = DarkTheme.MakeComboBox(new[] { "PDF24", "Microsoft Print to PDF", "Adobe PDF" }, "PDF24");
            printerCombo.FontSize = 12;
            printerGroup.Children.Add(printerCombo);
            leftCol.Children.Add(DarkTheme.MakeGroupBox("Printer", printerGroup));

            Grid.SetColumn(leftCol, 0);
            settingsGrid.Children.Add(leftCol);

            // ── Middle Column: Hidden Line Views + Appearance ──
            var midCol = new StackPanel();

            var hlvGroup = new StackPanel();
            hlvGroup.Children.Add(new TextBlock { Text = "Remove Lines Using", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var radioVector = new RadioButton { Content = "Vector Processing", IsChecked = true, Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "HLV", Margin = new Thickness(0, 0, 0, 2) };
            var radioRaster = new RadioButton { Content = "Raster Processing", Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "HLV" };
            hlvGroup.Children.Add(radioVector);
            hlvGroup.Children.Add(radioRaster);
            midCol.Children.Add(DarkTheme.MakeGroupBox("Hidden Line Views", hlvGroup));

            var appGroup = new StackPanel();
            appGroup.Children.Add(MakeGroupLabel("Raster Quality"));
            _pdfRasterQuality = DarkTheme.MakeComboBox(new[] { "Low", "Medium", "High" }, "Low");
            _pdfRasterQuality.FontSize = 12;
            _pdfRasterQuality.Margin = new Thickness(0, 0, 0, 10);
            appGroup.Children.Add(_pdfRasterQuality);
            appGroup.Children.Add(MakeGroupLabel("Colors"));
            _pdfColor = DarkTheme.MakeComboBox(new[] { "Color", "Grayscale", "Black & White" }, "Color");
            _pdfColor.FontSize = 12;
            appGroup.Children.Add(_pdfColor);
            midCol.Children.Add(DarkTheme.MakeGroupBox("Appearance", appGroup));

            Grid.SetColumn(midCol, 2);
            settingsGrid.Children.Add(midCol);

            // ── Right Column: Options + File ──
            var rightCol = new StackPanel();

            var optGroup = new StackPanel();
            var optViewLinks = DarkTheme.MakeCheckBox("View links in blue (Color prints only)", true);
            optViewLinks.FontSize = 11; optViewLinks.Margin = new Thickness(0, 2, 0, 2);
            var optHideRef = DarkTheme.MakeCheckBox("Hide ref/work planes", true);
            optHideRef.FontSize = 11; optHideRef.Margin = new Thickness(0, 2, 0, 2);
            _pdfHideRef = optHideRef;
            var optHideTags = DarkTheme.MakeCheckBox("Hide unreferenced view tags", true);
            optHideTags.FontSize = 11; optHideTags.Margin = new Thickness(0, 2, 0, 2);
            var optHideScope = DarkTheme.MakeCheckBox("Hide scope boxes", true);
            optHideScope.FontSize = 11; optHideScope.Margin = new Thickness(0, 2, 0, 2);
            _pdfHideScope = optHideScope;
            var optHideCrop = DarkTheme.MakeCheckBox("Hide crop boundaries", true);
            optHideCrop.FontSize = 11; optHideCrop.Margin = new Thickness(0, 2, 0, 2);
            _pdfHideCrop = optHideCrop;
            var optReplace = DarkTheme.MakeCheckBox("Replace halftone with thin lines", false);
            optReplace.FontSize = 11; optReplace.Margin = new Thickness(0, 2, 0, 2);
            var optRegion = DarkTheme.MakeCheckBox("Region edges mask coincident lines", false);
            optRegion.FontSize = 11; optRegion.Margin = new Thickness(0, 2, 0, 2);

            optGroup.Children.Add(optViewLinks);
            optGroup.Children.Add(optHideRef);
            optGroup.Children.Add(optHideTags);
            optGroup.Children.Add(optHideScope);
            optGroup.Children.Add(optHideCrop);
            optGroup.Children.Add(optReplace);
            optGroup.Children.Add(optRegion);
            rightCol.Children.Add(DarkTheme.MakeGroupBox("Options", optGroup));

            var fileGroup = new StackPanel();
            var radioSeparate = new RadioButton { Content = "Create separate files", IsChecked = true, Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "FileMode", Margin = new Thickness(0, 0, 0, 2) };
            _pdfCombine = new CheckBox();
            var radioCombine = new RadioButton { Content = "Combine multiple views/sheets into a single file", Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "FileMode" };
            radioCombine.Checked += (s, e) => _pdfCombine.IsChecked = true;
            radioSeparate.Checked += (s, e) => _pdfCombine.IsChecked = false;
            fileGroup.Children.Add(radioSeparate);
            fileGroup.Children.Add(radioCombine);

            _keepPaperSize = DarkTheme.MakeCheckBox("Keep Paper Size & Orientation", false);
            _keepPaperSize.FontSize = 11;
            _keepPaperSize.Margin = new Thickness(0, 8, 0, 4);
            fileGroup.Children.Add(_keepPaperSize);

            var customNameBtn = DarkTheme.MakeCancelButton("Custom File Name");
            customNameBtn.Padding = new Thickness(12, 6, 12, 6);
            customNameBtn.FontSize = 11;
            customNameBtn.Margin = new Thickness(0, 4, 0, 4);
            customNameBtn.HorizontalAlignment = HorizontalAlignment.Left;
            fileGroup.Children.Add(customNameBtn);

            var orderBtn = DarkTheme.MakeCancelButton("Order sheets and views");
            orderBtn.Padding = new Thickness(12, 6, 12, 6);
            orderBtn.FontSize = 11;
            orderBtn.HorizontalAlignment = HorizontalAlignment.Left;
            fileGroup.Children.Add(orderBtn);

            rightCol.Children.Add(DarkTheme.MakeGroupBox("File", fileGroup));

            Grid.SetColumn(rightCol, 4);
            settingsGrid.Children.Add(rightCol);

            content.Children.Add(settingsGrid);

            scroller.Content = content;
            wrapper.Child = scroller;
            return wrapper;
        }

        // ══════════════════════════════════════════════════════════════
        // ██ TAB 3: Create ██
        // ══════════════════════════════════════════════════════════════
        private Border BuildCreateTab()
        {
            var wrapper = new Border();
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // ── Export Rules ──
            var rulesGroup = new StackPanel();

            // Folder selection
            var folderRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            folderRow.Children.Add(new TextBlock { Text = "Folder Selection", FontSize = 12, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Width = 100 });
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"C:\Users\...\Downloads\Export\");
            _outputFolderBox.Margin = new Thickness(4, 0, 0, 0);

            var browseBtn = DarkTheme.MakeCancelButton("📁");
            browseBtn.Padding = new Thickness(10, 5, 10, 5);
            browseBtn.FontSize = 12;
            browseBtn.Margin = new Thickness(4, 0, 0, 0);
            browseBtn.Click += (s, e) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputFolderBox.Text = dlg.SelectedPath;
                    _outputFolderBox.Foreground = DarkTheme.FgWhite;
                }
            };
            DockPanel.SetDock(browseBtn, Dock.Right);
            folderRow.Children.Add(browseBtn);
            folderRow.Children.Add(_outputFolderBox);
            rulesGroup.Children.Add(folderRow);

            // Env vars hint
            var hintText = new TextBlock
            {
                Text = "Supports environment variables.  Learn More.",
                FontSize = 10,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(100, 0, 0, 10)
            };
            rulesGroup.Children.Add(hintText);

            // Save options
            _radioSameFolder = new RadioButton { Content = "Save all files in the same folder location", IsChecked = true, Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "SaveMode", Margin = new Thickness(0, 0, 0, 4) };
            _radioSplitByFormat = new RadioButton { Content = "Save and split files by file format", Foreground = DarkTheme.FgLight, FontSize = 12, GroupName = "SaveMode" };
            rulesGroup.Children.Add(_radioSameFolder);
            rulesGroup.Children.Add(_radioSplitByFormat);

            // Report dropdown
            var reportRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var reportCombo = DarkTheme.MakeComboBox(new[] { "Don't Save Report", "Save Report as CSV", "Save Report as TXT" }, "Don't Save Report");
            reportCombo.Width = 200;
            reportCombo.FontSize = 11;
            DockPanel.SetDock(reportCombo, Dock.Right);
            reportRow.Children.Add(reportCombo);
            rulesGroup.Children.Add(reportRow);

            content.Children.Add(DarkTheme.MakeGroupBox("Export Rules", rulesGroup));

            // ── Progress area ──
            var progressPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            // Paper size & orientation controls
            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var paperSizeCombo = DarkTheme.MakeComboBox(new[] { "Set Paper Size" }, "Set Paper Size");
            paperSizeCombo.Width = 140;
            paperSizeCombo.FontSize = 11;
            sizeRow.Children.Add(paperSizeCombo);
            var orientCombo = DarkTheme.MakeComboBox(new[] { "Set Orientation" }, "Set Orientation");
            orientCombo.Width = 140;
            orientCombo.FontSize = 11;
            orientCombo.Margin = new Thickness(8, 0, 0, 0);
            sizeRow.Children.Add(orientCombo);
            progressPanel.Children.Add(sizeRow);

            // Export queue table header
            var queueHeader = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddColumnHeader(queueHeader, "View/Sheet Number", 0);
            AddColumnHeader(queueHeader, "View/Sheet Name", 1);
            AddColumnHeader(queueHeader, "Format", 2);
            AddColumnHeader(queueHeader, "Size", 3);
            AddColumnHeader(queueHeader, "Orientation", 4);
            AddColumnHeader(queueHeader, "Progress", 5);

            var queueHeaderBorder = new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1, 1, 1, 0),
                Child = queueHeader
            };
            progressPanel.Children.Add(queueHeaderBorder);

            // Queue list — populated when switching to Create tab
            _queuePanel = new StackPanel();
            var queueScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 200,
                Content = _queuePanel
            };
            var queueBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x20)),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MinHeight = 120,
                Child = queueScroller
            };
            progressPanel.Children.Add(queueBorder);

            // Progress bar row
            var progressRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

            _progressText = new TextBlock
            {
                Text = "Completed 0%",
                FontSize = 12,
                Foreground = DarkTheme.FgLight,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 120
            };
            DockPanel.SetDock(_progressText, Dock.Left);
            progressRow.Children.Add(_progressText);

            var progressTrack = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(4),
                Height = 14,
                Margin = new Thickness(8, 0, 8, 0)
            };
            _progressBarFill = new Border
            {
                Background = DarkTheme.FgGreen,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 16
            };
            progressTrack.Child = _progressBarFill;
            progressRow.Children.Add(progressTrack);

            var exportCountText = new TextBlock
            {
                Text = "[0] of [1] exports used",
                FontSize = 10,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(exportCountText, Dock.Right);
            progressRow.Children.Add(exportCountText);

            progressPanel.Children.Add(progressRow);

            content.Children.Add(progressPanel);

            // ── Scheduling Assistant ──
            var schedGroup = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            var schedToggle = DarkTheme.MakeCheckBox("Schedule Publish", false);
            schedToggle.FontSize = 12;
            schedToggle.Margin = new Thickness(0, 0, 0, 6);
            schedGroup.Children.Add(new TextBlock { Text = "The Scheduling Assistant is Off", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 8) });

            var schedRow = new StackPanel { Orientation = Orientation.Horizontal };
            schedRow.Children.Add(new TextBlock { Text = "Choose Starting Date", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            var dateCombo = DarkTheme.MakeComboBox(new[] { System.DateTime.Now.ToString("dddd MMMM dd yyyy") });
            dateCombo.Width = 200;
            dateCombo.FontSize = 11;
            schedRow.Children.Add(dateCombo);

            schedRow.Children.Add(new TextBlock { Text = "  Choose Time", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) });
            var timeCombo = DarkTheme.MakeComboBox(new[] { System.DateTime.Now.ToString("hh:mm:tt") });
            timeCombo.Width = 100;
            timeCombo.FontSize = 11;
            schedRow.Children.Add(timeCombo);
            schedGroup.Children.Add(schedRow);

            // Day checkboxes
            var dayRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var repeatCombo = DarkTheme.MakeComboBox(new[] { "Does not repeat" }, "Does not repeat");
            repeatCombo.Width = 140;
            repeatCombo.FontSize = 11;
            repeatCombo.Margin = new Thickness(0, 0, 12, 0);
            dayRow.Children.Add(repeatCombo);

            foreach (var day in new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" })
            {
                var dayCheck = DarkTheme.MakeCheckBox(day, false);
                dayCheck.FontSize = 11;
                dayCheck.Margin = new Thickness(0, 0, 8, 0);
                dayRow.Children.Add(dayCheck);
            }
            schedGroup.Children.Add(dayRow);

            content.Children.Add(DarkTheme.MakeGroupBox("Scheduling Assistant ☆", schedGroup));

            scroller.Content = content;
            wrapper.Child = scroller;
            return wrapper;
        }

        // ══════════════════════════════════════════════════════════════
        // ██ Tab Navigation ██
        // ══════════════════════════════════════════════════════════════
        private void SwitchTab(int index)
        {
            if (index < 0 || index > 2) return;
            _activeTab = index;

            _tab1Content.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            _tab2Content.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            _tab3Content.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

            // Tab text colors
            _tab1Text.Foreground = index == 0 ? DarkTheme.FgWhite : DarkTheme.FgDim;
            _tab2Text.Foreground = index == 1 ? DarkTheme.FgWhite : DarkTheme.FgDim;
            _tab3Text.Foreground = index == 2 ? DarkTheme.FgWhite : DarkTheme.FgDim;

            // Tab indicator
            _tab1Indicator.Background = index == 0 ? DarkTheme.CatExport : Brushes.Transparent;
            _tab2Indicator.Background = index == 1 ? DarkTheme.CatExport : Brushes.Transparent;
            _tab3Indicator.Background = index == 2 ? DarkTheme.CatExport : Brushes.Transparent;

            // Font weight
            _tab1Text.FontWeight = index == 0 ? FontWeights.SemiBold : FontWeights.Normal;
            _tab2Text.FontWeight = index == 1 ? FontWeights.SemiBold : FontWeights.Normal;
            _tab3Text.FontWeight = index == 2 ? FontWeights.SemiBold : FontWeights.Normal;

            // Button states
            _backBtn.Visibility = index == 0 ? Visibility.Hidden : Visibility.Visible;
            _nextBtn.Content = index == 2 ? "🚀 Create" : "Next ▶";

            if (index == 2) PopulateQueue();
            UpdateSelectionStatus();
        }

        private FrameworkElement MakeTab(string text, int index, ref TextBlock textBlock, ref Border indicator)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand,
                Width = 100
            };

            textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 8)
            };

            indicator = new Border
            {
                Height = 3,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(2, 2, 0, 0)
            };

            panel.Children.Add(textBlock);
            panel.Children.Add(indicator);

            var idx = index;
            panel.MouseLeftButtonUp += (s, e) =>
            {
                // Validate: can't leave Selection tab without selecting something
                if (_activeTab == 0 && idx > 0)
                {
                    var selectedCount = _itemCheckboxes.Count(cb => cb.IsChecked == true);
                    if (selectedCount == 0)
                    {
                        ShowValidationMessage("⚠ Please select at least one sheet or view before proceeding.");
                        return;
                    }
                    HideValidationMessage();
                }
                SwitchTab(idx);
            };

            return panel;
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validate selection before leaving Tab 1
            if (_activeTab == 0)
            {
                var selectedCount = _itemCheckboxes.Count(cb => cb.IsChecked == true);
                if (selectedCount == 0)
                {
                    ShowValidationMessage("⚠ Please select at least one sheet or view before proceeding.");
                    return;
                }
                HideValidationMessage();
            }

            if (_activeTab < 2)
            {
                SwitchTab(_activeTab + 1);
            }
            else
            {
                // Create / Export
                ExportBtn_Click();
            }
        }

        private void ShowValidationMessage(string msg)
        {
            if (_validationMsg != null)
            {
                _validationMsg.Text = msg;
                _validationMsg.Visibility = Visibility.Visible;
            }
        }

        private void HideValidationMessage()
        {
            if (_validationMsg != null)
                _validationMsg.Visibility = Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════
        // ██ Data Population ██
        // ══════════════════════════════════════════════════════════════
        private void LoadRevitData()
        {
            _realSheets.Clear();
            _realViews.Clear();

            try
            {
                var uiApp = Core.Application.ActiveUIApp;
                if (uiApp?.ActiveUIDocument?.Document == null) return;
                var doc = uiApp.ActiveUIDocument.Document;

                // Get all sheets
                var sheets = new DB.FilteredElementCollector(doc)
                    .OfClass(typeof(DB.ViewSheet))
                    .Cast<DB.ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                foreach (var sheet in sheets)
                {
                    var rev = "";
                    try { rev = sheet.get_Parameter(DB.BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? ""; } catch { }
                    _realSheets.Add(new[] { sheet.SheetNumber, sheet.Name, rev, "", sheet.Id.ToString() });
                }

                // Get all printable views (exclude templates, sheets, and internal views)
                var views = new DB.FilteredElementCollector(doc)
                    .OfClass(typeof(DB.View))
                    .Cast<DB.View>()
                    .Where(v => !v.IsTemplate && !(v is DB.ViewSheet) && v.CanBePrinted)
                    .OrderBy(v => v.ViewType.ToString())
                    .ThenBy(v => v.Name)
                    .ToList();

                foreach (var view in views)
                {
                    _realViews.Add(new[] { view.Name, view.ViewType.ToString(), view.Id.ToString() });
                }

                Logger.Log($"Export Manager loaded {_realSheets.Count} sheets and {_realViews.Count} views from document.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load Revit data for Export Manager", ex);
            }
        }

        private void PopulateItems()
        {
            _itemListPanel.Children.Clear();
            _itemCheckboxes.Clear();
            HideValidationMessage();

            // Load real data from Revit document if not yet loaded
            if (_realSheets.Count == 0 && _realViews.Count == 0)
                LoadRevitData();

            if (_showingSheets)
            {
                if (_realSheets.Count == 0)
                {
                    _itemListPanel.Children.Add(new TextBlock
                    {
                        Text = "No sheets found in the current document.",
                        FontSize = 12,
                        Foreground = DarkTheme.FgDim,
                        Margin = new Thickness(12, 20, 12, 20),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    UpdateSelectionStatus();
                    return;
                }

                foreach (var sheet in _realSheets)
                {
                    var cb = DarkTheme.MakeCheckBox("", false);
                    cb.Checked += (s, e) => { UpdateSelectionStatus(); HideValidationMessage(); };
                    cb.Unchecked += (s, e) => UpdateSelectionStatus();
                    _itemCheckboxes.Add(cb);

                    var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

                    var rowBorder = new Border
                    {
                        Background = Brushes.Transparent,
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    rowBorder.MouseEnter += (s, e) => rowBorder.Background = DarkTheme.BgCardHover;
                    rowBorder.MouseLeave += (s, e) => rowBorder.Background = Brushes.Transparent;

                    cb.HorizontalAlignment = HorizontalAlignment.Center;
                    cb.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(cb, 0);
                    row.Children.Add(cb);

                    var numText = new TextBlock { Text = sheet[0], FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(numText, 1);
                    row.Children.Add(numText);

                    var nameText = new TextBlock { Text = sheet[1], FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetColumn(nameText, 2);
                    row.Children.Add(nameText);

                    var revBox = new TextBox
                    {
                        Text = sheet[2],
                        FontSize = 11,
                        Foreground = DarkTheme.FgLight,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = DarkTheme.BorderDim,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(2, 1, 2, 1)
                    };
                    revBox.GotFocus += (s, e) => revBox.BorderBrush = DarkTheme.CatExport;
                    revBox.LostFocus += (s, e) => revBox.BorderBrush = DarkTheme.BorderDim;
                    Grid.SetColumn(revBox, 3);
                    row.Children.Add(revBox);

                    var sizeBox = new TextBox
                    {
                        Text = sheet[3],
                        FontSize = 11,
                        Foreground = DarkTheme.FgLight,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = DarkTheme.BorderDim,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(2, 1, 2, 1)
                    };
                    sizeBox.GotFocus += (s, e) => sizeBox.BorderBrush = DarkTheme.CatExport;
                    sizeBox.LostFocus += (s, e) => sizeBox.BorderBrush = DarkTheme.BorderDim;
                    Grid.SetColumn(sizeBox, 4);
                    row.Children.Add(sizeBox);

                    var customNameBox = new TextBox
                    {
                        Text = "",
                        FontSize = 11,
                        Foreground = DarkTheme.FgLight,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = DarkTheme.BorderDim,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(2, 1, 2, 1)
                    };
                    customNameBox.GotFocus += (s, e) => customNameBox.BorderBrush = DarkTheme.CatExport;
                    customNameBox.LostFocus += (s, e) => customNameBox.BorderBrush = DarkTheme.BorderDim;
                    Grid.SetColumn(customNameBox, 5);
                    row.Children.Add(customNameBox);

                    row.Tag = $"{sheet[0]} {sheet[1]}".ToLower();

                    rowBorder.Child = row;
                    _itemListPanel.Children.Add(rowBorder);
                }
            }
            else
            {
                if (_realViews.Count == 0)
                {
                    _itemListPanel.Children.Add(new TextBlock
                    {
                        Text = "No printable views found in the current document.",
                        FontSize = 12,
                        Foreground = DarkTheme.FgDim,
                        Margin = new Thickness(12, 20, 12, 20),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    UpdateSelectionStatus();
                    return;
                }

                foreach (var view in _realViews)
                {
                    var cb = DarkTheme.MakeCheckBox("", false);
                    cb.Checked += (s, e) => { UpdateSelectionStatus(); HideValidationMessage(); };
                    cb.Unchecked += (s, e) => UpdateSelectionStatus();
                    _itemCheckboxes.Add(cb);

                    var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

                    var rowBorder = new Border
                    {
                        Background = Brushes.Transparent,
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    rowBorder.MouseEnter += (s, e) => rowBorder.Background = DarkTheme.BgCardHover;
                    rowBorder.MouseLeave += (s, e) => rowBorder.Background = Brushes.Transparent;

                    cb.HorizontalAlignment = HorizontalAlignment.Center;
                    cb.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(cb, 0);
                    row.Children.Add(cb);

                    var nameText = new TextBlock { Text = view[0], FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(nameText, 1);
                    row.Children.Add(nameText);

                    var typeText = new TextBlock { Text = view[1], FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(typeText, 2);
                    row.Children.Add(typeText);

                    var customNameBox = new TextBox
                    {
                        Text = "",
                        FontSize = 11,
                        Foreground = DarkTheme.FgLight,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = DarkTheme.BorderDim,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(2, 1, 2, 1)
                    };
                    customNameBox.GotFocus += (s, e) => customNameBox.BorderBrush = DarkTheme.CatExport;
                    customNameBox.LostFocus += (s, e) => customNameBox.BorderBrush = DarkTheme.BorderDim;
                    Grid.SetColumn(customNameBox, 3);
                    row.Children.Add(customNameBox);

                    row.Tag = $"{view[0]} {view[1]}".ToLower();

                    rowBorder.Child = row;
                    _itemListPanel.Children.Add(rowBorder);
                }
            }

            UpdateSelectionStatus();
        }

        // ══════════════════════════════════════════════════════════════
        // ██ Selection Helpers ██
        // ══════════════════════════════════════════════════════════════
        private void SetAllChecked(bool isChecked)
        {
            foreach (var cb in _itemCheckboxes)
                cb.IsChecked = isChecked;
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            var sheets = _showingSheets ? _itemCheckboxes.Count(cb => cb.IsChecked == true) : 0;
            var views = !_showingSheets ? _itemCheckboxes.Count(cb => cb.IsChecked == true) : 0;
            var total = sheets + views;
            _selectionStatus.Text = $"{sheets} sheets and {views} views selected. Total: {total}";
        }

        private void FilterItems(TextBox searchBox)
        {
            var searchText = searchBox.Foreground == DarkTheme.FgDim
                ? ""
                : (searchBox.Text ?? "").ToLower();

            foreach (UIElement child in _itemListPanel.Children)
            {
                if (child is Border b && b.Child is Grid g && g.Tag is string tag)
                {
                    var visible = string.IsNullOrEmpty(searchText) || tag.Contains(searchText);
                    b.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void PopulateQueue()
        {
            if (_queuePanel == null) return;
            _queuePanel.Children.Clear();

            // Gather selected formats
            var fmts = new List<string>();
            if (_fmtPdf?.IsChecked == true) fmts.Add("PDF");
            if (_fmtDwg?.IsChecked == true) fmts.Add("DWG");
            if (_fmtDgn?.IsChecked == true) fmts.Add("DGN");
            if (_fmtDwf?.IsChecked == true) fmts.Add("DWF");
            if (_fmtNwc?.IsChecked == true) fmts.Add("NWC");
            if (_fmtIfc?.IsChecked == true) fmts.Add("IFC");
            if (_fmtImg?.IsChecked == true) fmts.Add("IMG");
            var fmtStr = fmts.Count > 0 ? string.Join(", ", fmts) : "PDF";

            if (_showingSheets)
            {
                for (int i = 0; i < _itemCheckboxes.Count && i < _realSheets.Count; i++)
                {
                    if (_itemCheckboxes[i].IsChecked != true) continue;
                    var s = _realSheets[i];
                    AddQueueRow(s[0], s[1], fmtStr, s[3], "Auto");
                }
            }
            else
            {
                for (int i = 0; i < _itemCheckboxes.Count && i < _realViews.Count; i++)
                {
                    if (_itemCheckboxes[i].IsChecked != true) continue;
                    var v = _realViews[i];
                    AddQueueRow("", v[0], fmtStr, "—", "—");
                }
            }
        }

        private void AddQueueRow(string number, string name, string format, string size, string orient)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var numTb = new TextBlock { Text = number, FontSize = 11, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(numTb, 0); row.Children.Add(numTb);

            var nameTb = new TextBlock { Text = name, FontSize = 11, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(nameTb, 1); row.Children.Add(nameTb);

            var fmtTb = new TextBlock { Text = format, FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(fmtTb, 2); row.Children.Add(fmtTb);

            var sizeTb = new TextBlock { Text = size, FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sizeTb, 3); row.Children.Add(sizeTb);

            var orientTb = new TextBlock { Text = orient, FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(orientTb, 4); row.Children.Add(orientTb);

            var progTb = new TextBlock { Text = "Pending", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(progTb, 5); row.Children.Add(progTb);

            var rowBorder = new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row
            };
            _queuePanel.Children.Add(rowBorder);
        }

        // ══════════════════════════════════════════════════════════════
        // ██ Export ██
        // ══════════════════════════════════════════════════════════════
        private void ExportBtn_Click()
        {
            var selectedCount = _itemCheckboxes.Count(cb => cb.IsChecked == true);
            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one sheet or view to export.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Collect selected formats
            var formats = new List<string>();
            if (_fmtPdf.IsChecked == true) formats.Add("PDF");
            if (_fmtDwg.IsChecked == true) formats.Add("DWG");
            if (_fmtDgn.IsChecked == true) formats.Add("DGN");
            if (_fmtDwf.IsChecked == true) formats.Add("DWF");
            if (_fmtNwc.IsChecked == true) formats.Add("NWC");
            if (_fmtIfc.IsChecked == true) formats.Add("IFC");
            if (_fmtImg.IsChecked == true) formats.Add("IMG");

            if (formats.Count == 0)
            {
                MessageBox.Show("Please select at least one export format.",
                    "No Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build parameters
            var folder = GetTextValue(_outputFolderBox);
            var baseParams = DirectExecutor.Params(
                ("outputFolder", folder ?? "")
            );

            // Close Export Manager and show Progress Window
            Close();

            var progressWin = new ExportProgressWindow();
            progressWin.Show();
            progressWin.RunExports(formats, baseParams);
        }

        // ══════════════════════════════════════════════════════════════
        // ██ Helpers ██
        // ══════════════════════════════════════════════════════════════
        private string GetTextValue(TextBox tb)
        {
            if (tb.Foreground == DarkTheme.FgDim) return null;
            return tb.Text?.Trim();
        }

        private static void AddColumnHeader(Grid grid, string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        private static TextBlock MakeGroupLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private CheckBox MakeFormatToggle(string label, bool isChecked)
        {
            return new CheckBox
            {
                IsChecked = isChecked,
                Tag = label,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private Border WrapFormatToggle(CheckBox cb, string label)
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };

            cb.Margin = new Thickness(0, 0, 0, 4);
            cb.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(cb);

            var icon = new Border
            {
                Width = 50,
                Height = 50,
                Background = cb.IsChecked == true ? DarkTheme.BgCardHover : DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                BorderBrush = cb.IsChecked == true ? DarkTheme.CatExport : DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 4),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = cb.IsChecked == true ? DarkTheme.CatExport : DarkTheme.FgDim,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // Update icon on check/uncheck
            cb.Checked += (s, e) =>
            {
                icon.Background = DarkTheme.BgCardHover;
                icon.BorderBrush = DarkTheme.CatExport;
                ((TextBlock)icon.Child).Foreground = DarkTheme.CatExport;
            };
            cb.Unchecked += (s, e) =>
            {
                icon.Background = DarkTheme.BgCard;
                icon.BorderBrush = DarkTheme.BorderDim;
                ((TextBlock)icon.Child).Foreground = DarkTheme.FgDim;
            };

            stack.Children.Add(icon);

            var wrapper = new Border
            {
                Margin = new Thickness(0, 0, 12, 0),
                Child = stack
            };

            // Click icon to toggle checkbox
            icon.MouseLeftButtonUp += (s, e) => cb.IsChecked = !cb.IsChecked;

            return wrapper;
        }
    }
}
