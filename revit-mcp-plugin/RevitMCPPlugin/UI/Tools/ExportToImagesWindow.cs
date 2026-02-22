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
    /// Dedicated Export to Images window with 2-panel layout:
    /// Left = view selection, Right = image settings, Bottom = naming/output.
    /// </summary>
    public class ExportToImagesWindow : Window
    {
        private readonly List<CheckBox> _viewCheckboxes = new List<CheckBox>();
        private TextBlock _selectionStatus;

        // Settings controls
        private ComboBox _formatCombo;
        private ComboBox _dpiCombo;
        private ComboBox _visualStyleCombo;
        private TextBox _pixelSizeBox;
        private ComboBox _fitDirectionCombo;
        private Slider _jpegSlider;
        private TextBlock _jpegLabel;
        private StackPanel _fitToPagePanel;
        private CheckBox _exportShadows;
        private CheckBox _exportCropBounds;
        private CheckBox _exportRasterOnly;

        // Output
        private TextBox _namingPatternBox;
        private TextBox _outputFolderBox;
        private TextBlock _previewText;

        private static readonly string[][] SampleViews = new[]
        {
            new[] { "Level 0 - Plan",    "FP" },
            new[] { "Level 1 - Plan",    "FP" },
            new[] { "Level 2 - Plan",    "FP" },
            new[] { "Section A-A",       "SC" },
            new[] { "Section B-B",       "SC" },
            new[] { "East Elevation",    "EL" },
            new[] { "3D Overview",       "3D" },
            new[] { "Detail 1",          "DT" }
        };

        public ExportToImagesWindow()
        {
            Title = "🖼️ Export to Images";
            Width = 720;
            Height = 600;
            MinWidth = 620;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ══ HEADER ══
            var header = MakeHeader("🖼️ Export to Images", "Export views and sheets as image files");
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // ══ TWO-PANEL BODY ══
            var bodyGrid = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45, GridUnitType.Star) });

            // Left: View Selection
            var leftPanel = new StackPanel();
            leftPanel.Children.Add(DarkTheme.MakeSectionHeader("Views to Export", DarkTheme.CatExport));

            var searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search views...");
            searchBox.Margin = new Thickness(0, 0, 0, 6);
            leftPanel.Children.Add(searchBox);

            var filterCombo = DarkTheme.MakeComboBox(
                new[] { "All View Types", "Floor Plans", "Sections", "Elevations", "3D Views", "Sheets" },
                "All View Types");
            filterCombo.Margin = new Thickness(0, 0, 0, 6);
            leftPanel.Children.Add(filterCombo);

            var listBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 240
            };
            var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listPanel = new StackPanel();

            foreach (var view in SampleViews)
            {
                var row = new DockPanel { Margin = new Thickness(8, 3, 8, 3) };
                var cb = DarkTheme.MakeCheckBox("", false);
                cb.Checked += (s, e) => UpdateStatus();
                cb.Unchecked += (s, e) => UpdateStatus();
                _viewCheckboxes.Add(cb);
                cb.VerticalAlignment = VerticalAlignment.Center;
                cb.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(cb, Dock.Left);
                row.Children.Add(cb);

                var typeTb = new TextBlock { Text = view[1], FontSize = 11, Foreground = DarkTheme.FgDim, Width = 30, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(typeTb, Dock.Right);
                row.Children.Add(typeTb);

                row.Children.Add(new TextBlock { Text = view[0], FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center });
                row.Tag = view[0].ToLower();
                listPanel.Children.Add(row);
            }
            listScroll.Content = listPanel;
            listBorder.Child = listScroll;
            leftPanel.Children.Add(listBorder);

            var selRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var selAllBtn = MakeSmallBtn("☑ All");
            selAllBtn.Click += (s, e) => { foreach (var cb in _viewCheckboxes) cb.IsChecked = true; };
            var deselBtn = MakeSmallBtn("☐ None");
            deselBtn.Margin = new Thickness(8, 0, 0, 0);
            deselBtn.Click += (s, e) => { foreach (var cb in _viewCheckboxes) cb.IsChecked = false; };
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(selAllBtn);
            btns.Children.Add(deselBtn);
            DockPanel.SetDock(btns, Dock.Left);
            selRow.Children.Add(btns);

            _selectionStatus = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgDim, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_selectionStatus, Dock.Right);
            selRow.Children.Add(_selectionStatus);
            leftPanel.Children.Add(selRow);

            // Search filter
            searchBox.TextChanged += (s, e) =>
            {
                var q = searchBox.Foreground == DarkTheme.FgDim ? "" : (searchBox.Text ?? "").ToLower();
                foreach (UIElement child in listPanel.Children)
                    if (child is DockPanel dp && dp.Tag is string tag)
                        dp.Visibility = string.IsNullOrEmpty(q) || tag.Contains(q) ? Visibility.Visible : Visibility.Collapsed;
            };

            Grid.SetColumn(leftPanel, 0);
            bodyGrid.Children.Add(leftPanel);

            // Right: Image Settings
            var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rightPanel = new StackPanel();
            rightPanel.Children.Add(DarkTheme.MakeSectionHeader("Format & Quality", DarkTheme.CatExport));

            rightPanel.Children.Add(DarkTheme.MakeLabel("Image Format"));
            _formatCombo = DarkTheme.MakeComboBox(new[] { "PNG", "JPEG", "TIFF", "BMP" }, "PNG");
            _formatCombo.Margin = new Thickness(0, 0, 0, 8);
            _formatCombo.SelectionChanged += (s, e) =>
            {
                var fmt = (_formatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                var isJpeg = fmt == "JPEG";
                if (_jpegSlider != null) _jpegSlider.IsEnabled = isJpeg;
                if (_jpegLabel != null) _jpegLabel.Foreground = isJpeg ? DarkTheme.FgLight : DarkTheme.FgDim;
                UpdatePreview();
            };
            rightPanel.Children.Add(_formatCombo);

            rightPanel.Children.Add(DarkTheme.MakeSeparator());

            rightPanel.Children.Add(DarkTheme.MakeLabel("Sizing Method"));
            // Fit to Page settings
            _fitToPagePanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            var pixRow = new DockPanel();
            pixRow.Children.Add(DarkTheme.MakeLabel("Pixel Size"));
            _pixelSizeBox = DarkTheme.MakeTextBox("4000");
            _pixelSizeBox.Width = 80;
            _pixelSizeBox.Margin = new Thickness(8, 0, 0, 0);
            pixRow.Children.Add(_pixelSizeBox);
            _fitToPagePanel.Children.Add(pixRow);

            _fitToPagePanel.Children.Add(DarkTheme.MakeLabel("Fit Direction"));
            _fitDirectionCombo = DarkTheme.MakeComboBox(new[] { "Horizontal", "Vertical" }, "Horizontal");
            _fitToPagePanel.Children.Add(_fitDirectionCombo);
            rightPanel.Children.Add(_fitToPagePanel);

            rightPanel.Children.Add(DarkTheme.MakeSeparator());

            rightPanel.Children.Add(DarkTheme.MakeLabel("Resolution (DPI)"));
            _dpiCombo = DarkTheme.MakeComboBox(new[] { "72 DPI", "150 DPI", "300 DPI", "600 DPI" }, "300 DPI");
            _dpiCombo.Margin = new Thickness(0, 0, 0, 8);
            rightPanel.Children.Add(_dpiCombo);

            rightPanel.Children.Add(DarkTheme.MakeLabel("Visual Style Override"));
            _visualStyleCombo = DarkTheme.MakeComboBox(
                new[] { "Use View Setting", "Wireframe", "Hidden Line", "Shaded", "Realistic" },
                "Use View Setting");
            _visualStyleCombo.Margin = new Thickness(0, 0, 0, 8);
            rightPanel.Children.Add(_visualStyleCombo);

            rightPanel.Children.Add(DarkTheme.MakeSeparator());

            _exportShadows = DarkTheme.MakeCheckBox("Export shadows", true);
            _exportShadows.Margin = new Thickness(0, 0, 0, 4);
            rightPanel.Children.Add(_exportShadows);

            _exportCropBounds = DarkTheme.MakeCheckBox("Export crop boundaries", true);
            _exportCropBounds.Margin = new Thickness(0, 0, 0, 4);
            rightPanel.Children.Add(_exportCropBounds);

            _exportRasterOnly = DarkTheme.MakeCheckBox("Export as raster only", false);
            _exportRasterOnly.Margin = new Thickness(0, 0, 0, 8);
            rightPanel.Children.Add(_exportRasterOnly);

            rightPanel.Children.Add(DarkTheme.MakeSeparator());

            _jpegLabel = DarkTheme.MakeLabel("JPEG Quality");
            _jpegLabel.Foreground = DarkTheme.FgDim;
            rightPanel.Children.Add(_jpegLabel);
            var jpegRow = new DockPanel();
            _jpegSlider = DarkTheme.MakeSlider(1, 100, 85);
            _jpegSlider.IsEnabled = false;
            _jpegSlider.Width = 160;
            DockPanel.SetDock(_jpegSlider, Dock.Left);
            jpegRow.Children.Add(_jpegSlider);
            var jpegVal = new TextBlock { Text = "85%", FontSize = 12, Foreground = DarkTheme.FgLight, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _jpegSlider.ValueChanged += (s, e) => jpegVal.Text = $"{(int)_jpegSlider.Value}%";
            jpegRow.Children.Add(jpegVal);
            rightPanel.Children.Add(jpegRow);

            rightScroll.Content = rightPanel;
            Grid.SetColumn(rightScroll, 2);
            bodyGrid.Children.Add(rightScroll);

            Grid.SetRow(bodyGrid, 1);
            mainGrid.Children.Add(bodyGrid);

            // ══ NAMING & OUTPUT ══
            var outputSection = new StackPanel { Margin = new Thickness(20, 0, 20, 10) };
            outputSection.Children.Add(DarkTheme.MakeSeparator());
            outputSection.Children.Add(DarkTheme.MakeSectionHeader("Naming & Output", DarkTheme.CatExport));

            outputSection.Children.Add(DarkTheme.MakeLabel("File Naming Pattern"));
            _namingPatternBox = DarkTheme.MakeTextBox("{ViewName}_{Date}");
            _namingPatternBox.Margin = new Thickness(0, 0, 0, 4);
            _namingPatternBox.TextChanged += (s, e) => UpdatePreview();
            outputSection.Children.Add(_namingPatternBox);

            var tokenPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var t in new[] { "{ViewName}", "{ViewType}", "{Scale}", "{Date}", "{ProjectName}", "{Format}" })
            {
                var badge = MakeTokenBadge(t);
                badge.MouseLeftButtonUp += (s, e) =>
                {
                    var pos = _namingPatternBox.CaretIndex;
                    _namingPatternBox.Text = _namingPatternBox.Text.Insert(pos, t);
                    _namingPatternBox.CaretIndex = pos + t.Length;
                    _namingPatternBox.Focus();
                };
                tokenPanel.Children.Add(badge);
            }
            outputSection.Children.Add(tokenPanel);

            outputSection.Children.Add(DarkTheme.MakeLabel("Output Folder"));
            var folderRow = new DockPanel();
            var browseBtn = DarkTheme.MakeCancelButton("📁 Browse");
            browseBtn.Padding = new Thickness(12, 6, 12, 6);
            browseBtn.FontSize = 12;
            DockPanel.SetDock(browseBtn, Dock.Right);
            browseBtn.Margin = new Thickness(8, 0, 0, 0);
            browseBtn.Click += (s, e) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputFolderBox.Text = dlg.SelectedPath;
                    _outputFolderBox.Foreground = DarkTheme.FgWhite;
                }
            };
            folderRow.Children.Add(browseBtn);
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\Images");
            folderRow.Children.Add(_outputFolderBox);
            outputSection.Children.Add(folderRow);

            _previewText = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgGreen, Margin = new Thickness(0, 4, 0, 0) };
            outputSection.Children.Add(_previewText);

            Grid.SetRow(outputSection, 2);
            mainGrid.Children.Add(outputSection);

            // ══ FOOTER ══
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            Button cancelBtn, exportBtn;
            var bPanel = DarkTheme.MakeButtonPanel("🖼️ Export ▶", out cancelBtn, out exportBtn);
            cancelBtn.Click += (s, e) => Close();
            exportBtn.Click += ExportBtn_Click;
            footer.Child = bPanel;
            Grid.SetRow(footer, 3);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
            UpdateStatus();
            UpdatePreview();
        }

        private void UpdateStatus()
        {
            var count = _viewCheckboxes.Count(c => c.IsChecked == true);
            _selectionStatus.Text = $"{count} selected";
        }

        private void UpdatePreview()
        {
            var fmt = (_formatCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "png";
            var pattern = _namingPatternBox?.Text ?? "{ViewName}";
            var resolved = pattern.Replace("{ViewName}", "Level 0 - Plan")
                                   .Replace("{ViewType}", "FP")
                                   .Replace("{Scale}", "50")
                                   .Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd"))
                                   .Replace("{ProjectName}", "MyProject")
                                   .Replace("{Format}", fmt);
            if (_previewText != null)
                _previewText.Text = $"Preview: {resolved}.{fmt}";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewCheckboxes.Count(c => c.IsChecked == true);
            if (selected == 0)
            {
                MessageBox.Show("Please select at least one view.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sb = new StringBuilder("Please run export_to_images");
            var parts = new List<string>();

            var selectedViews = new List<string>();
            for (int i = 0; i < _viewCheckboxes.Count && i < SampleViews.Length; i++)
                if (_viewCheckboxes[i].IsChecked == true)
                    selectedViews.Add(SampleViews[i][0]);
            parts.Add($"views: {string.Join(", ", selectedViews)}");
            parts.Add($"format: {(_formatCombo.SelectedItem as ComboBoxItem)?.Content}");
            parts.Add($"DPI: {(_dpiCombo.SelectedItem as ComboBoxItem)?.Content}");
            parts.Add($"pixel size: {_pixelSizeBox.Text}");

            var vs = (_visualStyleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (vs != "Use View Setting") parts.Add($"visual style: {vs}");

            if (_formatCombo.SelectedItem is ComboBoxItem fi && fi.Content.ToString() == "JPEG")
                parts.Add($"JPEG quality: {(int)_jpegSlider.Value}%");

            var folder = _outputFolderBox.Foreground != DarkTheme.FgDim ? _outputFolderBox.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(folder)) parts.Add($"output folder: {folder}");

            sb.Append(" with ");
            sb.Append(string.Join(", ", parts));
            Close();
            DirectExecutor.RunAsync("export_to_images", DirectExecutor.Params(
                ("views", string.Join(", ", parts))
            ), "Export to Images");
        }

        // ── Shared helpers ────────────────────────────────────────────

        private static Border MakeHeader(string title, string subtitle)
        {
            var header = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(24, 12, 24, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            s.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) });
            header.Child = s;
            return header;
        }

        private static Border MakeTokenBadge(string token)
        {
            var badge = new Border
            {
                Background = DarkTheme.BgInput, CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 6, 4),
                BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), Cursor = Cursors.Hand
            };
            badge.Child = new TextBlock { Text = token, FontSize = 11, Foreground = DarkTheme.FgGold, FontFamily = new FontFamily("Consolas") };
            badge.MouseEnter += (s, e) => badge.BorderBrush = DarkTheme.BorderAccent;
            badge.MouseLeave += (s, e) => badge.BorderBrush = DarkTheme.BorderDim;
            return badge;
        }

        private static Button MakeSmallBtn(string text)
        {
            var btn = new Button
            {
                Content = text, Background = DarkTheme.BgCancel, Foreground = DarkTheme.FgLight,
                BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 11, Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = DarkTheme.BgCancelHover;
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgCancel;
            return btn;
        }
    }
}
