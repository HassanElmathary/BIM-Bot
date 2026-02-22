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
    /// Export Schedule Data window: schedule picker, data preview, export settings, output.
    /// </summary>
    public class ExportScheduleWindow : Window
    {
        private readonly List<RadioButton> _scheduleRadios = new List<RadioButton>();
        private int _selectedIndex = 0;
        private ComboBox _formatCombo;
        private ComboBox _delimiterCombo;
        private ComboBox _qualifierCombo;
        private CheckBox _exportHeaders;
        private ComboBox _headerRowsCombo;
        private CheckBox _exportTitle;
        private CheckBox _exportGroupHeaders;
        private CheckBox _exportBlankLines;
        private TextBox _fileNameBox;
        private TextBox _outputFolderBox;
        private TextBlock _previewText;

        private static readonly string[][] SampleSchedules = new[]
        {
            new[] { "Wall Schedule",                "124", "8" },
            new[] { "Door Schedule",                "32",  "12" },
            new[] { "Room Schedule",                "58",  "6" },
            new[] { "Window Schedule",              "28",  "10" },
            new[] { "Structural Column Schedule",   "18",  "7" },
            new[] { "Material Takeoff - Concrete",  "45",  "5" }
        };

        public ExportScheduleWindow()
        {
            Title = "📊 Export Schedule Data";
            Width = 750;
            Height = 600;
            MinWidth = 640;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = MakeHeader("📊 Export Schedule Data", "Export schedule data to CSV, TXT, or Excel");
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Content
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var content = new StackPanel();

            // Section 1: Schedule Selection
            var selContent = new StackPanel();
            var searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search schedules...");
            searchBox.Margin = new Thickness(0, 0, 0, 6);
            selContent.Children.Add(searchBox);

            var listBorder = new Border
            {
                Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 180
            };
            var ls = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var lp = new StackPanel();

            // Header row
            var hdr = new Grid { Margin = new Thickness(8, 6, 8, 4) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            AddGridText(hdr, "", 0, true); AddGridText(hdr, "Schedule Name", 1, true);
            AddGridText(hdr, "Rows", 2, true); AddGridText(hdr, "Cols", 3, true);
            lp.Children.Add(hdr);
            lp.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(8, 0, 8, 0) });

            for (int i = 0; i < SampleSchedules.Length; i++)
            {
                var idx = i;
                var sch = SampleSchedules[i];
                var row = new Grid { Margin = new Thickness(8, 3, 8, 3), Tag = sch[0].ToLower() };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

                var rb = new RadioButton { IsChecked = i == 0, GroupName = "schedule", VerticalAlignment = VerticalAlignment.Center };
                rb.Checked += (s, e) => { _selectedIndex = idx; UpdatePreview(); };
                _scheduleRadios.Add(rb);
                Grid.SetColumn(rb, 0); row.Children.Add(rb);
                AddGridText(row, sch[0], 1, false);
                AddGridText(row, sch[1], 2, false, DarkTheme.FgDim);
                AddGridText(row, sch[2], 3, false, DarkTheme.FgDim);
                lp.Children.Add(row);
            }

            searchBox.TextChanged += (s, e) =>
            {
                var q = searchBox.Foreground == DarkTheme.FgDim ? "" : (searchBox.Text ?? "").ToLower();
                foreach (UIElement child in lp.Children)
                    if (child is Grid g && g.Tag is string tag)
                        g.Visibility = string.IsNullOrEmpty(q) || tag.Contains(q) ? Visibility.Visible : Visibility.Collapsed;
            };

            ls.Content = lp; listBorder.Child = ls;
            selContent.Children.Add(listBorder);
            content.Children.Add(DarkTheme.MakeGroupBox("Select Schedule", selContent));

            // Section 2: Export Settings (two columns)
            var settingsGrid = new Grid();
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lCol = new StackPanel();
            lCol.Children.Add(DarkTheme.MakeLabel("Format"));
            _formatCombo = DarkTheme.MakeComboBox(new[] { "CSV (Comma Delimited)", "TXT (Tab Delimited)", "Excel (.xlsx)" }, "CSV (Comma Delimited)");
            _formatCombo.Margin = new Thickness(0, 0, 0, 8);
            _formatCombo.SelectionChanged += (s, e) =>
            {
                var isExcel = (_formatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Contains("Excel") == true;
                _delimiterCombo.IsEnabled = !isExcel;
                _qualifierCombo.IsEnabled = !isExcel;
                UpdatePreview();
            };
            lCol.Children.Add(_formatCombo);

            lCol.Children.Add(DarkTheme.MakeLabel("Field Delimiter"));
            _delimiterCombo = DarkTheme.MakeComboBox(new[] { "Comma (,)", "Tab", "Semicolon (;)", "Space" }, "Comma (,)");
            _delimiterCombo.Margin = new Thickness(0, 0, 0, 8);
            lCol.Children.Add(_delimiterCombo);

            lCol.Children.Add(DarkTheme.MakeLabel("Text Qualifier"));
            _qualifierCombo = DarkTheme.MakeComboBox(new[] { "None", "Single Quotes (')", "Double Quotes (\")" }, "Double Quotes (\")");
            lCol.Children.Add(_qualifierCombo);
            Grid.SetColumn(lCol, 0); settingsGrid.Children.Add(lCol);

            var rCol = new StackPanel();
            rCol.Children.Add(DarkTheme.MakeLabel("Content Options"));
            _exportHeaders = DarkTheme.MakeCheckBox("Export Column Headers", true);
            _exportHeaders.Margin = new Thickness(0, 4, 0, 4);
            rCol.Children.Add(_exportHeaders);
            _headerRowsCombo = DarkTheme.MakeComboBox(new[] { "One Row", "Multiple Rows" }, "One Row");
            _headerRowsCombo.Margin = new Thickness(16, 0, 0, 8);
            rCol.Children.Add(_headerRowsCombo);

            _exportTitle = DarkTheme.MakeCheckBox("Export Schedule Title", true);
            _exportTitle.Margin = new Thickness(0, 0, 0, 4);
            rCol.Children.Add(_exportTitle);
            _exportGroupHeaders = DarkTheme.MakeCheckBox("Export Group Headers/Footers", false);
            _exportGroupHeaders.Margin = new Thickness(0, 0, 0, 4);
            rCol.Children.Add(_exportGroupHeaders);
            _exportBlankLines = DarkTheme.MakeCheckBox("Export Blank Lines", false);
            rCol.Children.Add(_exportBlankLines);
            Grid.SetColumn(rCol, 2); settingsGrid.Children.Add(rCol);

            content.Children.Add(DarkTheme.MakeGroupBox("Export Settings", settingsGrid));

            // Section 3: Output
            var outContent = new StackPanel();
            outContent.Children.Add(DarkTheme.MakeLabel("File Name"));
            _fileNameBox = DarkTheme.MakeTextBox("{ScheduleName}");
            _fileNameBox.Margin = new Thickness(0, 0, 0, 4);
            _fileNameBox.TextChanged += (s, e) => UpdatePreview();
            outContent.Children.Add(_fileNameBox);

            var tp = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var t in new[] { "{ScheduleName}", "{Date}", "{ProjectName}", "{RowCount}" })
            {
                var badge = MakeTokenBadge(t);
                badge.MouseLeftButtonUp += (s, e) => { var p = _fileNameBox.CaretIndex; _fileNameBox.Text = _fileNameBox.Text.Insert(p, t); _fileNameBox.CaretIndex = p + t.Length; _fileNameBox.Focus(); };
                tp.Children.Add(badge);
            }
            outContent.Children.Add(tp);

            outContent.Children.Add(DarkTheme.MakeLabel("Output Folder"));
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
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\Schedules");
            fRow.Children.Add(_outputFolderBox);
            outContent.Children.Add(fRow);

            _previewText = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgGreen, Margin = new Thickness(0, 4, 0, 0) };
            outContent.Children.Add(_previewText);
            content.Children.Add(DarkTheme.MakeGroupBox("Output", outContent));

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
            var bp = DarkTheme.MakeButtonPanel("📊 Export ▶", out cancelBtn, out exportBtn);
            cancelBtn.Click += (s, e) => Close();
            exportBtn.Click += ExportBtn_Click;
            footer.Child = bp;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var schName = _selectedIndex < SampleSchedules.Length ? SampleSchedules[_selectedIndex][0] : "Schedule";
            var fmt = (_formatCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CSV";
            var ext = fmt.Contains("Excel") ? "xlsx" : fmt.Contains("TXT") ? "txt" : "csv";
            var pattern = _fileNameBox?.Text ?? "{ScheduleName}";
            var resolved = pattern.Replace("{ScheduleName}", schName)
                                   .Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd"))
                                   .Replace("{ProjectName}", "MyProject")
                                   .Replace("{RowCount}", _selectedIndex < SampleSchedules.Length ? SampleSchedules[_selectedIndex][1] : "0");
            if (_previewText != null) _previewText.Text = $"Preview: {resolved}.{ext}";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder("Please run export_schedule");
            var parts = new List<string>();
            var schName = _selectedIndex < SampleSchedules.Length ? SampleSchedules[_selectedIndex][0] : "";
            parts.Add($"schedule: {schName}");
            parts.Add($"format: {(_formatCombo.SelectedItem as ComboBoxItem)?.Content}");
            if (_exportHeaders.IsChecked == true) parts.Add("include column headers");
            if (_exportTitle.IsChecked == true) parts.Add("include schedule title");
            var folder = _outputFolderBox.Foreground != DarkTheme.FgDim ? _outputFolderBox.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(folder)) parts.Add($"output folder: {folder}");
            sb.Append(" with "); sb.Append(string.Join(", ", parts));
            Close();
            DirectExecutor.RunAsync("export_schedule", DirectExecutor.Params(
                ("schedule", schName),
                ("format", (_formatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("includeHeaders", _exportHeaders.IsChecked == true ? "true" : null),
                ("includeTitle", _exportTitle.IsChecked == true ? "true" : null),
                ("outputFolder", folder)
            ), "Export Schedule");
        }

        private static void AddGridText(Grid grid, string text, int col, bool isHeader, SolidColorBrush fg = null)
        {
            var tb = new TextBlock { Text = text, FontSize = 11, Foreground = fg ?? (isHeader ? DarkTheme.FgDim : DarkTheme.FgLight), FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, col); grid.Children.Add(tb);
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
    }
}
