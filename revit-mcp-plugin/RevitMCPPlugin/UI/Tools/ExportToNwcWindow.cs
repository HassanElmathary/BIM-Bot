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
    /// Dedicated Export to NWC (Navisworks) window: single-page with grouped settings.
    /// </summary>
    public class ExportToNwcWindow : Window
    {
        private ComboBox _coordsCombo;
        private ComboBox _convertParamsCombo;
        private CheckBox _exportElementIds;
        private CheckBox _constructionParts;
        private CheckBox _linkedRvt;
        private CheckBox _linkedCad;
        private CheckBox _roomGeometry;
        private CheckBox _roomAttribute;
        private CheckBox _divideByLevels;
        private Slider _facetingSlider;
        private TextBlock _facetingValue;
        private ComboBox _exportScopeCombo;

        private TextBox _fileNameBox;
        private TextBox _outputFolderBox;
        private TextBlock _previewText;

        public ExportToNwcWindow()
        {
            Title = "🔷 Export to NWC (Navisworks)";
            Width = 580;
            Height = 520;
            MinWidth = 500;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = MakeHeader("🔷 Export to NWC", "Export model to Navisworks Cache file");
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Content
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var content = new StackPanel();

            // Coordinate System
            var coordContent = new StackPanel();
            coordContent.Children.Add(DarkTheme.MakeLabel("Coordinates"));
            _coordsCombo = DarkTheme.MakeComboBox(new[] { "Shared Coordinates", "Project Internal" }, "Shared Coordinates");
            coordContent.Children.Add(_coordsCombo);
            content.Children.Add(DarkTheme.MakeGroupBox("Coordinate System", coordContent));

            // Element Export
            var elemContent = new StackPanel();
            elemContent.Children.Add(DarkTheme.MakeLabel("Convert Element Parameters"));
            _convertParamsCombo = DarkTheme.MakeComboBox(new[] { "None", "Elements", "All" }, "All");
            _convertParamsCombo.Margin = new Thickness(0, 0, 0, 8);
            elemContent.Children.Add(_convertParamsCombo);

            _exportElementIds = DarkTheme.MakeCheckBox("Export Element IDs", true);
            _exportElementIds.Margin = new Thickness(0, 0, 0, 4);
            elemContent.Children.Add(_exportElementIds);
            _constructionParts = DarkTheme.MakeCheckBox("Convert Construction Parts", false);
            elemContent.Children.Add(_constructionParts);
            content.Children.Add(DarkTheme.MakeGroupBox("Element Export", elemContent));

            // Links & Rooms
            var linksContent = new StackPanel();
            _linkedRvt = DarkTheme.MakeCheckBox("Convert Linked RVT Files", true);
            _linkedRvt.Margin = new Thickness(0, 0, 0, 4);
            linksContent.Children.Add(_linkedRvt);
            _linkedCad = DarkTheme.MakeCheckBox("Convert Linked CAD Formats (DWG, DXF, DGN)", false);
            _linkedCad.Margin = new Thickness(0, 0, 0, 4);
            linksContent.Children.Add(_linkedCad);
            _roomGeometry = DarkTheme.MakeCheckBox("Export Room Geometry", false);
            _roomGeometry.Margin = new Thickness(0, 0, 0, 4);
            linksContent.Children.Add(_roomGeometry);
            _roomAttribute = DarkTheme.MakeCheckBox("Convert Room as Attribute", false);
            linksContent.Children.Add(_roomAttribute);
            content.Children.Add(DarkTheme.MakeGroupBox("Links & Rooms", linksContent));

            // Structure & Quality
            var structContent = new StackPanel();
            _divideByLevels = DarkTheme.MakeCheckBox("Divide File into Levels", true);
            _divideByLevels.Margin = new Thickness(0, 0, 0, 8);
            structContent.Children.Add(_divideByLevels);

            structContent.Children.Add(DarkTheme.MakeLabel("Faceting Factor (tessellation quality)"));
            var sliderRow = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            _facetingSlider = new Slider
            {
                Minimum = 0.1, Maximum = 10, Value = 1.0, TickFrequency = 0.1,
                IsSnapToTickEnabled = true, Width = 200, VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_facetingSlider, Dock.Left);
            sliderRow.Children.Add(_facetingSlider);
            _facetingValue = new TextBlock { Text = "1.0", FontSize = 12, Foreground = DarkTheme.FgLight, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _facetingSlider.ValueChanged += (s, e) => _facetingValue.Text = _facetingSlider.Value.ToString("F1");
            sliderRow.Children.Add(_facetingValue);
            structContent.Children.Add(sliderRow);

            var scaleRow = new DockPanel();
            scaleRow.Children.Add(new TextBlock { Text = "Low (0.1)", FontSize = 10, Foreground = DarkTheme.FgDim });
            var highLabel = new TextBlock { Text = "High (10.0)", FontSize = 10, Foreground = DarkTheme.FgDim, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(highLabel, Dock.Right);
            scaleRow.Children.Add(highLabel);
            structContent.Children.Add(scaleRow);

            structContent.Children.Add(new Border { Height = 8 });
            structContent.Children.Add(DarkTheme.MakeLabel("Export Scope"));
            _exportScopeCombo = DarkTheme.MakeComboBox(new[] { "Entire Model", "Current View" }, "Entire Model");
            structContent.Children.Add(_exportScopeCombo);

            content.Children.Add(DarkTheme.MakeGroupBox("Structure & Quality", structContent));

            // Output
            var outContent = new StackPanel();
            outContent.Children.Add(DarkTheme.MakeLabel("File Name"));
            _fileNameBox = DarkTheme.MakeTextBox("{ProjectName}");
            _fileNameBox.Margin = new Thickness(0, 0, 0, 4);
            _fileNameBox.TextChanged += (s, e) => UpdatePreview();
            outContent.Children.Add(_fileNameBox);

            var tp = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var t in new[] { "{ProjectName}", "{Date}", "{Coordinates}" })
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
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\NWC");
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
            var bp = DarkTheme.MakeButtonPanel("🔷 Export ▶", out cancelBtn, out exportBtn);
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
            var fn = _fileNameBox?.Text ?? "{ProjectName}";
            var resolved = fn.Replace("{ProjectName}", "MyProject")
                              .Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd"))
                              .Replace("{Coordinates}", "SharedCoords");
            if (_previewText != null) _previewText.Text = $"Preview: {resolved}.nwc";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder("Please run export_to_nwc");
            var parts = new List<string>();

            parts.Add($"coordinates: {(_coordsCombo.SelectedItem as ComboBoxItem)?.Content}");
            parts.Add($"convert parameters: {(_convertParamsCombo.SelectedItem as ComboBoxItem)?.Content}");
            if (_exportElementIds.IsChecked == true) parts.Add("export element IDs");
            if (_linkedRvt.IsChecked == true) parts.Add("include linked RVT");
            if (_linkedCad.IsChecked == true) parts.Add("include linked CAD");
            if (_roomGeometry.IsChecked == true) parts.Add("export room geometry");
            if (_divideByLevels.IsChecked == true) parts.Add("divide by levels");
            parts.Add($"faceting factor: {_facetingSlider.Value:F1}");
            parts.Add($"scope: {(_exportScopeCombo.SelectedItem as ComboBoxItem)?.Content}");

            var fn = _fileNameBox.Foreground != DarkTheme.FgDim ? _fileNameBox.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(fn)) parts.Add($"file name: {fn}");
            var folder = _outputFolderBox.Foreground != DarkTheme.FgDim ? _outputFolderBox.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(folder)) parts.Add($"output folder: {folder}");

            sb.Append(" with "); sb.Append(string.Join(", ", parts));
            Close();
            DirectExecutor.RunAsync("export_to_nwc", DirectExecutor.Params(
                ("settings", string.Join(", ", parts)),
                ("fileName", fn), ("outputFolder", folder)
            ), "Export to NWC");
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
