using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class ImportParamsCsvWindow : Window
    {
        private int _step = 0;
        private readonly List<FrameworkElement> _panels = new List<FrameworkElement>();
        private readonly List<Border> _indicators = new List<Border>();
        private TextBox _filePathBox;
        private CheckBox _skipReadOnly, _skipNotFound, _createBackup;

        public ImportParamsCsvWindow()
        {
            Title = "📥 Import Parameters from CSV";
            Width = 780; Height = 620; MinWidth = 640; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("📥 Import Parameters from CSV", "Update model parameters from a CSV file");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            // Step indicator
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Background = DarkTheme.BgDark, Margin = new Thickness(20, 8, 20, 0), HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var n in new[] { "1: Select File", "2: Preview", "3: Apply" })
            {
                if (_indicators.Count > 0) bar.Children.Add(new TextBlock { Text = " → ", FontSize = 12, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
                var ind = new Border { Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(12), Background = _indicators.Count == 0 ? DarkTheme.BgAccent : DarkTheme.BgCard };
                ind.Child = new TextBlock { Text = n, FontSize = 12, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
                _indicators.Add(ind); bar.Children.Add(ind);
            }
            Grid.SetRow(bar, 1); mg.Children.Add(bar);

            var pc = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            _panels.Add(Step1()); _panels.Add(Step2()); _panels.Add(Step3());
            foreach (var p in _panels) pc.Children.Add(p);
            Grid.SetRow(pc, 2); mg.Children.Add(pc);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            var fd = new DockPanel();
            var bk = DarkTheme.MakeCancelButton("◀ Back"); bk.Click += (s, e) => { if (_step > 0) Go(_step - 1); };
            DockPanel.SetDock(bk, Dock.Left); fd.Children.Add(bk);
            Button cb, nb; var bp = DarkTheme.MakeButtonPanel("Next ▶", out cb, out nb);
            nb.Click += (s, e) => { if (_step < 2) Go(_step + 1); else DoApply(); };
            cb.Click += (s, e) => Close();
            DockPanel.SetDock(bp, Dock.Right); fd.Children.Add(bp);
            ft.Child = fd; Grid.SetRow(ft, 3); mg.Children.Add(ft);
            Content = mg; Go(0);
        }

        private void Go(int s) { _step = s; for (int i = 0; i < _panels.Count; i++) { _panels[i].Visibility = i == s ? Visibility.Visible : Visibility.Collapsed; _indicators[i].Background = i == s ? DarkTheme.BgAccent : DarkTheme.BgCard; } }

        private FrameworkElement Step1()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Select CSV File", DarkTheme.CatExport));

            // Drop zone
            var dropZone = new Border
            {
                Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(8),
                BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(2),
                MinHeight = 120, Margin = new Thickness(0, 0, 0, 12), Cursor = Cursors.Hand
            };
            var dzContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20) };
            dzContent.Children.Add(new TextBlock { Text = "📄 CSV", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, Foreground = DarkTheme.FgDim });
            dzContent.Children.Add(new TextBlock { Text = "Drag & drop your CSV file here\nor click Browse below", FontSize = 12, Foreground = DarkTheme.FgDim, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            dropZone.Child = dzContent;
            c.Children.Add(dropZone);

            c.Children.Add(DarkTheme.MakeLabel("File Path"));
            var fr = new DockPanel();
            var fb = DarkTheme.MakeCancelButton("📁 Browse"); fb.Padding = new Thickness(12, 6, 12, 6); fb.FontSize = 12;
            DockPanel.SetDock(fb, Dock.Right); fb.Margin = new Thickness(8, 0, 0, 0);
            fb.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files|*.csv|All Files|*.*", DefaultExt = ".csv" };
                if (dlg.ShowDialog() == true) { _filePathBox.Text = dlg.FileName; _filePathBox.Foreground = DarkTheme.FgWhite; }
            };
            fr.Children.Add(fb);
            _filePathBox = DarkTheme.MakeTextBox(placeholder: "Select a CSV file...");
            fr.Children.Add(_filePathBox);
            c.Children.Add(fr);

            // File info preview
            var infoBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 12, 0, 0), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1) };
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock { Text = "📄 File info will appear after selection", FontSize = 12, Foreground = DarkTheme.FgDim });
            infoBorder.Child = infoStack;
            c.Children.Add(infoBorder);

            var warn = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x2E, 0x1A)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 12, 0, 0) };
            warn.Child = new TextBlock { Text = "⚠️ Ensure the CSV was exported using \"Export Params to CSV\" tool and contains an ElementID column for element matching.", FontSize = 11, Foreground = DarkTheme.FgGold, TextWrapping = TextWrapping.Wrap };
            c.Children.Add(warn);
            return c;
        }

        private FrameworkElement Step2()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Preview Changes (Dry Run)", DarkTheme.CatExport));

            // Change summary
            var sumBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1) };
            var sumStack = new StackPanel();
            sumStack.Children.Add(new TextBlock { Text = "🟢 Values to update: 23", FontSize = 12, Foreground = DarkTheme.FgGreen, Margin = new Thickness(0, 0, 0, 4) });
            sumStack.Children.Add(new TextBlock { Text = "⚪ Unchanged values: 287", FontSize = 12, Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 0, 0, 4) });
            sumStack.Children.Add(new TextBlock { Text = "🔴 Errors / Not found: 2", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66)), Margin = new Thickness(0, 0, 0, 4) });
            sumStack.Children.Add(new TextBlock { Text = "⚠️ Read-only skipped: 45", FontSize = 12, Foreground = DarkTheme.FgGold });
            sumBorder.Child = sumStack;
            c.Children.Add(sumBorder);

            // Sample diff table
            var tbl = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 200 };
            var ts = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var tp = new StackPanel();
            tp.Children.Add(MkTblRow("ElemID", "Param", "Current", "New", "Status", true));
            tp.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(8, 0, 8, 0) });
            tp.Children.Add(MkTblRow("456789", "Mark", "W01", "W-001", "🟢 Update", false));
            tp.Children.Add(MkTblRow("456789", "Comments", "(empty)", "Corner wall", "🟢 Update", false));
            tp.Children.Add(MkTblRow("456801", "Mark", "W02", "W-002", "🟢 Update", false));
            tp.Children.Add(MkTblRow("999999", "Mark", "—", "W-099", "🔴 Not Found", false));
            ts.Content = tp; tbl.Child = ts;
            c.Children.Add(tbl);

            _skipReadOnly = DarkTheme.MakeCheckBox("Skip read-only parameters", true);
            _skipReadOnly.Margin = new Thickness(0, 8, 0, 4);
            c.Children.Add(_skipReadOnly);
            _skipNotFound = DarkTheme.MakeCheckBox("Skip elements not found in model", true);
            c.Children.Add(_skipNotFound);
            return c;
        }

        private FrameworkElement Step3()
        {
            var c = new StackPanel();
            c.Children.Add(DarkTheme.MakeSectionHeader("Apply Changes", DarkTheme.CatExport));

            var confirmBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1F, 0x0A)), CornerRadius = new CornerRadius(6), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12) };
            var confirmStack = new StackPanel();
            confirmStack.Children.Add(new TextBlock { Text = "⚠️ You are about to update 23 parameter values across 15 elements.", FontSize = 13, Foreground = DarkTheme.FgGold, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            confirmStack.Children.Add(new TextBlock { Text = "This action can be undone using Revit's Undo (Ctrl+Z).", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 6, 0, 0) });
            confirmBorder.Child = confirmStack;
            c.Children.Add(confirmBorder);

            _createBackup = DarkTheme.MakeCheckBox("Create backup CSV before applying", true);
            _createBackup.Margin = new Thickness(0, 0, 0, 16);
            c.Children.Add(_createBackup);

            // Progress area
            var progLabel = new TextBlock { Text = "Ready to apply changes", FontSize = 12, Foreground = DarkTheme.FgDim };
            c.Children.Add(progLabel);
            var progBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Value = 0, Background = DarkTheme.BgInput, Foreground = DarkTheme.BgAccent, Margin = new Thickness(0, 8, 0, 16) };
            c.Children.Add(progBar);
            return c;
        }

        private void DoApply()
        {
            var sb = new StringBuilder("Please run import_params_csv");
            var parts = new List<string>();
            var fp = _filePathBox?.Foreground != DarkTheme.FgDim ? _filePathBox?.Text?.Trim() : null;
            if (!string.IsNullOrEmpty(fp)) parts.Add($"file: {fp}");
            if (_skipReadOnly?.IsChecked == true) parts.Add("skip read-only");
            if (_skipNotFound?.IsChecked == true) parts.Add("skip not found");
            if (_createBackup?.IsChecked == true) parts.Add("create backup");
            if (parts.Count > 0) { sb.Append(" with "); sb.Append(string.Join(", ", parts)); }
            Close();
            DirectExecutor.RunAsync("import_parameters_from_csv", DirectExecutor.Params(
                ("file", fp),
                ("skipReadOnly", _skipReadOnly?.IsChecked == true ? "true" : null),
                ("skipNotFound", _skipNotFound?.IsChecked == true ? "true" : null),
                ("createBackup", _createBackup?.IsChecked == true ? "true" : null)
            ), "Import Parameters CSV");
        }

        static Grid MkTblRow(string c1, string c2, string c3, string c4, string c5, bool isHdr)
        {
            var g = new Grid { Margin = new Thickness(8, 3, 8, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            var fg = isHdr ? DarkTheme.FgDim : DarkTheme.FgLight;
            var fw = isHdr ? FontWeights.Bold : FontWeights.Normal;
            for (int i = 0; i < 5; i++) { var tb = new TextBlock { Text = new[] { c1, c2, c3, c4, c5 }[i], FontSize = 11, Foreground = fg, FontWeight = fw, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(tb, i); g.Children.Add(tb); }
            return g;
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
