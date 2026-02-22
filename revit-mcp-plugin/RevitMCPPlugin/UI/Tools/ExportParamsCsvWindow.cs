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
    public class ExportParamsCsvWindow : Window
    {
        private int _currentStep = 0;
        private readonly List<FrameworkElement> _stepPanels = new List<FrameworkElement>();
        private readonly List<Border> _stepIndicators = new List<Border>();
        private readonly List<RadioButton> _categoryRadios = new List<RadioButton>();
        private int _selectedCatIdx = 0;
        private ComboBox _sourceCombo;
        private readonly List<CheckBox> _paramCbs = new List<CheckBox>();
        private ComboBox _formatCombo3;
        private CheckBox _includeElemId, _includeFamilyName, _includeTypeName;
        private TextBox _fileNameBox, _outputFolderBox;
        private TextBlock _previewText;

        private static readonly string[][] Cats = {
            new[]{"Walls","45","12","8"}, new[]{"Doors","32","15","10"},
            new[]{"Windows","28","14","9"}, new[]{"Rooms","58","8","—"},
            new[]{"Floors","12","10","6"}, new[]{"Structural Columns","18","11","7"}
        };
        private static readonly string[] Params = {
            "🟢 Mark","🟢 Comments","🟢 Length","🟢 Area","🟢 Volume","🟢 Level",
            "🟢 Base Constraint","🟢 Top Constraint","🟡 Type Name","🟡 Width",
            "🟡 Function","🔴 Area (read-only)","🔴 Volume (read-only)"
        };

        public ExportParamsCsvWindow()
        {
            Title = "📋 Export Parameters to CSV";
            Width = 780; Height = 640; MinWidth = 660; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkHeader("📋 Export Parameters to CSV", "Export element parameters to CSV or Excel");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            // Step indicator
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Background = DarkTheme.BgDark, Margin = new Thickness(20, 8, 20, 0), HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var nm in new[] { "1: Category", "2: Parameters", "3: Output" })
            {
                if (_stepIndicators.Count > 0) bar.Children.Add(new TextBlock { Text = " → ", FontSize = 12, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
                var ind = new Border { Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(12), Background = _stepIndicators.Count == 0 ? DarkTheme.BgAccent : DarkTheme.BgCard };
                ind.Child = new TextBlock { Text = nm, FontSize = 12, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
                _stepIndicators.Add(ind); bar.Children.Add(ind);
            }
            Grid.SetRow(bar, 1); mg.Children.Add(bar);

            var pc = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            _stepPanels.Add(BuildStep1()); _stepPanels.Add(BuildStep2()); _stepPanels.Add(BuildStep3());
            foreach (var p in _stepPanels) pc.Children.Add(p);
            Grid.SetRow(pc, 2); mg.Children.Add(pc);

            // Footer
            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            var fd = new DockPanel();
            var bk = DarkTheme.MakeCancelButton("◀ Back"); bk.Click += (s, e) => { if (_currentStep > 0) SwitchStep(_currentStep - 1); };
            DockPanel.SetDock(bk, Dock.Left); fd.Children.Add(bk);
            Button cb, nb; var bp = DarkTheme.MakeButtonPanel("Next ▶", out cb, out nb);
            nb.Click += (s, e) => { if (_currentStep < 2) SwitchStep(_currentStep + 1); else DoExport(); };
            cb.Click += (s, e) => Close();
            DockPanel.SetDock(bp, Dock.Right); fd.Children.Add(bp);
            ft.Child = fd; Grid.SetRow(ft, 3); mg.Children.Add(ft);
            Content = mg; SwitchStep(0);
        }

        private void SwitchStep(int step)
        {
            _currentStep = step;
            for (int i = 0; i < _stepPanels.Count; i++)
            {
                _stepPanels[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
                _stepIndicators[i].Background = i == step ? DarkTheme.BgAccent : DarkTheme.BgCard;
            }
        }

        private FrameworkElement BuildStep1()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Select Category", DarkTheme.CatExport));
            var sr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            sr.Children.Add(DarkTheme.MakeLabel("Source:"));
            _sourceCombo = DarkTheme.MakeComboBox(new[] { "Whole Model", "Active View", "Current Selection" }, "Whole Model");
            _sourceCombo.Width = 180; _sourceCombo.Margin = new Thickness(8, 0, 0, 0); sr.Children.Add(_sourceCombo);
            c.Children.Add(sr);

            var lb = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 300 };
            var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var lp = new StackPanel();
            for (int i = 0; i < Cats.Length; i++)
            {
                var idx = i; var cat = Cats[i];
                var row = new DockPanel { Margin = new Thickness(8, 4, 8, 4) };
                var rb = new RadioButton { IsChecked = i == 0, GroupName = "cat", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                rb.Checked += (s, e) => _selectedCatIdx = idx;
                _categoryRadios.Add(rb); DockPanel.SetDock(rb, Dock.Left); row.Children.Add(rb);
                row.Children.Add(new TextBlock { Text = $"{cat[0]}  ({cat[1]} elements, {cat[2]} inst, {cat[3]} type)", FontSize = 12, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center });
                lp.Children.Add(row);
            }
            sc.Content = lp; lb.Child = sc; c.Children.Add(lb);
            return c;
        }

        private FrameworkElement BuildStep2()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Select Parameters", DarkTheme.CatExport));
            var br = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var ab = MkSmall("All"); ab.Click += (s, e) => { foreach (var cb in _paramCbs) cb.IsChecked = true; };
            var clb = MkSmall("Clear"); clb.Click += (s, e) => { foreach (var cb in _paramCbs) cb.IsChecked = false; };
            br.Children.Add(ab); br.Children.Add(clb); c.Children.Add(br);

            var lb = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 320 };
            var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var lp = new StackPanel();
            foreach (var p in Params) { var cb = DarkTheme.MakeCheckBox(p, true); cb.Margin = new Thickness(8, 4, 8, 4); _paramCbs.Add(cb); lp.Children.Add(cb); }
            sc.Content = lp; lb.Child = sc; c.Children.Add(lb);
            c.Children.Add(new TextBlock { Text = "Legend: 🟢 Instance  🟡 Type  🔴 Read-only", FontSize = 10, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 6, 0, 0) });
            return c;
        }

        private FrameworkElement BuildStep3()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Output Settings", DarkTheme.CatExport));
            c.Children.Add(DarkTheme.MakeLabel("Format"));
            _formatCombo3 = DarkTheme.MakeComboBox(new[] { "CSV", "Excel (.xlsx)" }, "CSV");
            _formatCombo3.Margin = new Thickness(0, 0, 0, 8); c.Children.Add(_formatCombo3);
            _includeElemId = DarkTheme.MakeCheckBox("Include Element ID column", true); _includeElemId.Margin = new Thickness(0, 0, 0, 4); c.Children.Add(_includeElemId);
            _includeFamilyName = DarkTheme.MakeCheckBox("Include Family Name column", true); _includeFamilyName.Margin = new Thickness(0, 0, 0, 4); c.Children.Add(_includeFamilyName);
            _includeTypeName = DarkTheme.MakeCheckBox("Include Type Name column", true); _includeTypeName.Margin = new Thickness(0, 0, 0, 8); c.Children.Add(_includeTypeName);

            c.Children.Add(DarkTheme.MakeSeparator());
            c.Children.Add(DarkTheme.MakeLabel("File Name"));
            _fileNameBox = DarkTheme.MakeTextBox("Walls_Parameters"); _fileNameBox.Margin = new Thickness(0, 0, 0, 8); c.Children.Add(_fileNameBox);
            c.Children.Add(DarkTheme.MakeLabel("Output Folder"));
            var fr = new DockPanel();
            var fb = DarkTheme.MakeCancelButton("📁 Browse"); fb.Padding = new Thickness(12, 6, 12, 6); fb.FontSize = 12;
            DockPanel.SetDock(fb, Dock.Right); fb.Margin = new Thickness(8, 0, 0, 0);
            fb.Click += (s, e) => { var d = new System.Windows.Forms.FolderBrowserDialog(); if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) { _outputFolderBox.Text = d.SelectedPath; _outputFolderBox.Foreground = DarkTheme.FgWhite; } };
            fr.Children.Add(fb);
            _outputFolderBox = DarkTheme.MakeTextBox(placeholder: @"e.g. C:\Export\Parameters"); fr.Children.Add(_outputFolderBox);
            c.Children.Add(fr);
            _previewText = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgGreen, Margin = new Thickness(0, 4, 0, 0) }; c.Children.Add(_previewText);
            return c;
        }

        private void DoExport()
        {
            var sb = new StringBuilder("Please run export_params_csv");
            var parts = new List<string>();
            parts.Add($"category: {Cats[_selectedCatIdx][0]}");
            parts.Add($"source: {(_sourceCombo.SelectedItem as ComboBoxItem)?.Content}");
            var sel = new List<string>();
            for (int i = 0; i < _paramCbs.Count; i++) if (_paramCbs[i].IsChecked == true) sel.Add(Params[i].Substring(2).Trim());
            if (sel.Count > 0) parts.Add($"parameters: {string.Join(", ", sel)}");
            var fn = _fileNameBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(fn)) parts.Add($"file name: {fn}");
            var fo = _outputFolderBox?.Foreground != DarkTheme.FgDim ? _outputFolderBox?.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(fo)) parts.Add($"output folder: {fo}");
            sb.Append(" with "); sb.Append(string.Join(", ", parts));
            Close();
            DirectExecutor.RunAsync("export_parameters_to_csv", DirectExecutor.Params(
                ("category", Cats[_selectedCatIdx][0]),
                ("source", (_sourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("parameters", string.Join(", ", sel)),
                ("fileName", fn), ("outputFolder", fo)
            ), "Export Parameters CSV");
        }

        static Border MkHeader(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
        static Button MkSmall(string t) { var b = new Button { Content = t, Background = DarkTheme.BgCancel, Foreground = DarkTheme.FgLight, BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0) }; b.MouseEnter += (s, e) => b.Background = DarkTheme.BgCancelHover; b.MouseLeave += (s, e) => b.Background = DarkTheme.BgCancel; return b; }
    }
}
