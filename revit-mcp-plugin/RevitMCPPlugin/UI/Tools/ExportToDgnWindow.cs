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
    /// Dedicated Export to DGN window: View selection, DGN settings, output.
    /// </summary>
    public class ExportToDgnWindow : Window
    {
        private readonly List<CheckBox> _viewCheckboxes = new List<CheckBox>();
        private TextBlock _selectionStatus;

        private ComboBox _dgnVersionCombo;
        private TextBox _seedFileBox;
        private ComboBox _layerMappingCombo;
        private ComboBox _exportLevelCombo;
        private ComboBox _propertyOverridesCombo;
        private CheckBox _exportRoomsMesh;
        private CheckBox _exportLinkedModels;
        private CheckBox _exportCurrentView;

        private TextBox _namingBox;
        private TextBox _outputFolderBox;
        private TextBlock _previewText;

        private static readonly string[][] SampleViews = new[]
        {
            new[] { "Level 0 - Plan",    "Floor Plan", "1:50" },
            new[] { "Level 1 - Plan",    "Floor Plan", "1:50" },
            new[] { "Section A-A",       "Section",    "1:50" },
            new[] { "East Elevation",    "Elevation",  "1:100" },
            new[] { "3D Overview",       "3D View",    "—" }
        };

        public ExportToDgnWindow()
        {
            Title = "📐 Export to DGN";
            Width = 680;
            Height = 560;
            MinWidth = 580;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = MakeHeader("📐 Export to DGN", "Export views to Bentley DGN format");
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Content
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var content = new StackPanel();

            // Section 1: View Selection
            content.Children.Add(DarkTheme.MakeSectionHeader("Views to Export", DarkTheme.CatExport));
            var listBorder = new Border
            {
                Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 180
            };
            var ls = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var lp = new StackPanel();
            foreach (var v in SampleViews)
            {
                var row = new DockPanel { Margin = new Thickness(8, 3, 8, 3) };
                var cb = DarkTheme.MakeCheckBox("", false);
                cb.Checked += (s, e) => UpdateStatus();
                cb.Unchecked += (s, e) => UpdateStatus();
                _viewCheckboxes.Add(cb);
                cb.Margin = new Thickness(0, 0, 8, 0);
                cb.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(cb, Dock.Left);
                row.Children.Add(cb);

                var scaleTb = new TextBlock { Text = v[2], FontSize = 11, Foreground = DarkTheme.FgDim, Width = 50, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(scaleTb, Dock.Right);
                row.Children.Add(scaleTb);

                var typeTb = new TextBlock { Text = v[1], FontSize = 11, Foreground = DarkTheme.FgDim, Width = 80, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(typeTb, Dock.Right);
                row.Children.Add(typeTb);

                row.Children.Add(new TextBlock { Text = v[0], FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center });
                lp.Children.Add(row);
            }
            ls.Content = lp;
            listBorder.Child = ls;
            content.Children.Add(listBorder);

            var selBar = new DockPanel { Margin = new Thickness(0, 4, 0, 12) };
            var sa = MakeSmallBtn("☑ All"); sa.Click += (s, e) => { foreach (var c in _viewCheckboxes) c.IsChecked = true; };
            var sn = MakeSmallBtn("☐ None"); sn.Margin = new Thickness(8, 0, 0, 0); sn.Click += (s, e) => { foreach (var c in _viewCheckboxes) c.IsChecked = false; };
            var sbp = new StackPanel { Orientation = Orientation.Horizontal }; sbp.Children.Add(sa); sbp.Children.Add(sn);
            DockPanel.SetDock(sbp, Dock.Left); selBar.Children.Add(sbp);
            _selectionStatus = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgDim, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_selectionStatus, Dock.Right); selBar.Children.Add(_selectionStatus);
            content.Children.Add(selBar);

            // Section 2: DGN Settings (two columns)
            content.Children.Add(DarkTheme.MakeSeparator());
            content.Children.Add(DarkTheme.MakeSectionHeader("DGN Settings", DarkTheme.CatExport));

            var settingsGrid = new Grid();
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lCol = new StackPanel();
            lCol.Children.Add(DarkTheme.MakeLabel("DGN File Version"));
            _dgnVersionCombo = DarkTheme.MakeComboBox(new[] { "V7", "V8 (MicroStation)" }, "V8 (MicroStation)");
            _dgnVersionCombo.Margin = new Thickness(0, 0, 0, 8);
            lCol.Children.Add(_dgnVersionCombo);

            lCol.Children.Add(DarkTheme.MakeLabel("Seed File"));
            var seedRow = new DockPanel();
            var seedBrowse = DarkTheme.MakeCancelButton("📁");
            seedBrowse.Padding = new Thickness(8, 4, 8, 4);
            seedBrowse.FontSize = 12;
            DockPanel.SetDock(seedBrowse, Dock.Right);
            seedBrowse.Margin = new Thickness(4, 0, 0, 0);
            seedRow.Children.Add(seedBrowse);
            _seedFileBox = DarkTheme.MakeTextBox("V8-Metric-Seed3D.dgn");
            seedRow.Children.Add(_seedFileBox);
            lCol.Children.Add(seedRow);
            lCol.Children.Add(new Border { Height = 8 });

            lCol.Children.Add(DarkTheme.MakeLabel("Layer/Level Mapping"));
            _layerMappingCombo = DarkTheme.MakeComboBox(
                new[] { "AIA Standard", "ISO 13567", "BS 1192", "Singapore SS83", "Custom" }, "AIA Standard");
            lCol.Children.Add(_layerMappingCombo);
            Grid.SetColumn(lCol, 0);
            settingsGrid.Children.Add(lCol);

            var rCol = new StackPanel();
            rCol.Children.Add(DarkTheme.MakeLabel("Export Level Options"));
            _exportLevelCombo = DarkTheme.MakeComboBox(
                new[] { "BYLEVEL, overrides BYELEMENT", "All BYLEVEL", "All BYELEMENT" }, "BYLEVEL, overrides BYELEMENT");
            _exportLevelCombo.Margin = new Thickness(0, 0, 0, 8);
            rCol.Children.Add(_exportLevelCombo);

            rCol.Children.Add(DarkTheme.MakeLabel("Property Overrides"));
            _propertyOverridesCombo = DarkTheme.MakeComboBox(new[] { "By Entity", "By Layer" }, "By Entity");
            _propertyOverridesCombo.Margin = new Thickness(0, 0, 0, 8);
            rCol.Children.Add(_propertyOverridesCombo);

            rCol.Children.Add(DarkTheme.MakeLabel("Options", fontSize: 12));
            _exportRoomsMesh = DarkTheme.MakeCheckBox("Export rooms as polymeshes", true);
            _exportRoomsMesh.Margin = new Thickness(0, 4, 0, 4);
            rCol.Children.Add(_exportRoomsMesh);
            _exportLinkedModels = DarkTheme.MakeCheckBox("Export linked models", true);
            _exportLinkedModels.Margin = new Thickness(0, 0, 0, 4);
            rCol.Children.Add(_exportLinkedModels);
            _exportCurrentView = DarkTheme.MakeCheckBox("Export only current view", false);
            rCol.Children.Add(_exportCurrentView);
            Grid.SetColumn(rCol, 2);
            settingsGrid.Children.Add(rCol);

            content.Children.Add(settingsGrid);

            // Section 3: Output
            content.Children.Add(DarkTheme.MakeSeparator());
            content.Children.Add(DarkTheme.MakeSectionHeader("Output", DarkTheme.CatExport));

            content.Children.Add(DarkTheme.MakeLabel("File Naming"));
            _namingBox = DarkTheme.MakeTextBox("{ViewName}");
            _namingBox.Margin = new Thickness(0, 0, 0, 4);
            _namingBox.TextChanged += (s, e) => UpdatePreview();
            content.Children.Add(_namingBox);

            var tp = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var t in new[] { "{ViewName}", "{ViewType}", "{Scale}", "{Date}", "{ProjectName}" })
            {
                var b = MakeTokenBadge(t);
                b.MouseLeftButtonUp += (s2, e2) => { var p = _namingBox.CaretIndex; _namingBox.Text = _namingBox.Text.Insert(p, t); _namingBox.CaretIndex = p + t.Length; _namingBox.Focus(); };
                tp.Children.Add(b);
            }
            content.Children.Add(tp);

            content.Children.Add(DarkTheme.MakeLabel("Output Folder"));
            var fRow = new DockPanel();
            var fBrowse = DarkTheme.MakeCancelButton("📁 Browse");
            fBrowse.Padding = new Thickness(12, 6, 12, 6); fBrowse.FontSize = 12;
            DockPanel.SetDock(fBrowse, Dock.Right); fBrowse.Margin = new Thickness(8, 0, 0, 0);
            fBrowse.Click += (s, e) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) { _outputFolderBox.Text = dlg.SelectedPath; _outputFolderBox.Foreground = DarkTheme.FgWhite; }
            };
            fRow.Children.Add(fBrowse);
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\DGN");
            fRow.Children.Add(_outputFolderBox);
            content.Children.Add(fRow);

            _previewText = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgGreen, Margin = new Thickness(0, 4, 0, 0) };
            content.Children.Add(_previewText);

            scroll.Content = content;
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            // Footer
            var footer = new Border
            {
                Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0)
            };
            Button cancelBtn, exportBtn;
            var bp = DarkTheme.MakeButtonPanel("📐 Export ▶", out cancelBtn, out exportBtn);
            cancelBtn.Click += (s, e) => Close();
            exportBtn.Click += ExportBtn_Click;
            footer.Child = bp;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
            UpdateStatus();
            UpdatePreview();
        }

        private void UpdateStatus() => _selectionStatus.Text = $"{_viewCheckboxes.Count(c => c.IsChecked == true)} views selected";

        private void UpdatePreview()
        {
            var pattern = _namingBox?.Text ?? "{ViewName}";
            var resolved = pattern.Replace("{ViewName}", "Level 0 - Plan").Replace("{ViewType}", "FloorPlan")
                .Replace("{Scale}", "50").Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd")).Replace("{ProjectName}", "MyProject");
            if (_previewText != null) _previewText.Text = $"Preview: {resolved}.dgn";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewCheckboxes.Count(c => c.IsChecked == true) == 0)
            {
                MessageBox.Show("Please select at least one view.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var sb = new StringBuilder("Please run export_to_dgn");
            var parts = new List<string>();
            var views = new List<string>();
            for (int i = 0; i < _viewCheckboxes.Count && i < SampleViews.Length; i++)
                if (_viewCheckboxes[i].IsChecked == true) views.Add(SampleViews[i][0]);
            parts.Add($"views: {string.Join(", ", views)}");
            parts.Add($"DGN version: {(_dgnVersionCombo.SelectedItem as ComboBoxItem)?.Content}");
            parts.Add($"layer mapping: {(_layerMappingCombo.SelectedItem as ComboBoxItem)?.Content}");
            var folder = _outputFolderBox.Foreground != DarkTheme.FgDim ? _outputFolderBox.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(folder)) parts.Add($"output folder: {folder}");
            sb.Append(" with "); sb.Append(string.Join(", ", parts));
            Close();
            DirectExecutor.RunAsync("export_to_dgn", DirectExecutor.Params(
                ("views", string.Join(", ", views)),
                ("dgnVersion", (_dgnVersionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("layerMapping", (_layerMappingCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("outputFolder", folder)
            ), "Export to DGN");
        }

        private static Border MakeHeader(string title, string sub)
        {
            var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) };
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            s.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) });
            h.Child = s; return h;
        }

        private static Border MakeTokenBadge(string token)
        {
            var b = new Border { Background = DarkTheme.BgInput, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 6, 4), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            b.Child = new TextBlock { Text = token, FontSize = 11, Foreground = DarkTheme.FgGold, FontFamily = new FontFamily("Consolas") };
            b.MouseEnter += (s, e) => b.BorderBrush = DarkTheme.BorderAccent;
            b.MouseLeave += (s, e) => b.BorderBrush = DarkTheme.BorderDim;
            return b;
        }

        private static Button MakeSmallBtn(string text)
        {
            var btn = new Button { Content = text, Background = DarkTheme.BgCancel, Foreground = DarkTheme.FgLight, BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 11, Cursor = Cursors.Hand };
            btn.MouseEnter += (s, e) => btn.Background = DarkTheme.BgCancelHover;
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgCancel;
            return btn;
        }
    }
}
