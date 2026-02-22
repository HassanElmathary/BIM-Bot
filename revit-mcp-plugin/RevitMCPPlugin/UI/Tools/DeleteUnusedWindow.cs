using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class DeleteUnusedWindow : Window
    {
        private ComboBox _catFilter;
        private CheckBox _includeNested, _includeInPlace;
        private readonly List<CheckBox> _familyCbs = new List<CheckBox>();
        private TextBlock _summaryText;

        private static readonly string[][] SampleFamilies = {
            new[]{"Old_Door_Style","Doors","2"},
            new[]{"Unused_Window_v1","Windows","1"},
            new[]{"Test_Column","Columns","3"},
            new[]{"Backup_WallType","Walls","1"},
            new[]{"Deprecated_Bracket","Gen.Model","1"},
            new[]{"Legacy_Furniture_01","Furniture","4"},
            new[]{"Legacy_Furniture_02","Furniture","2"}
        };

        public DeleteUnusedWindow()
        {
            Title = "🗑️ Delete Unused Families";
            Width = 620; Height = 560; MinWidth = 520; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("🗑️ Delete Unused Families", "Find and remove families with zero placed instances");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Scan options
            var scanContent = new StackPanel();
            scanContent.Children.Add(DarkTheme.MakeLabel("Filter"));
            _catFilter = DarkTheme.MakeComboBox(new[] { "All Categories", "Doors", "Windows", "Walls", "Furniture", "Columns", "Gen.Model" }, "All Categories");
            _catFilter.Margin = new Thickness(0, 0, 0, 8); scanContent.Children.Add(_catFilter);
            var chkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _includeNested = DarkTheme.MakeCheckBox("Include nested families", true); _includeNested.Margin = new Thickness(0, 0, 16, 0); chkRow.Children.Add(_includeNested);
            _includeInPlace = DarkTheme.MakeCheckBox("Include in-place families", false); chkRow.Children.Add(_includeInPlace);
            scanContent.Children.Add(chkRow);
            c.Children.Add(DarkTheme.MakeGroupBox("Scan Options", scanContent));

            // Results
            var resContent = new StackPanel();
            resContent.Children.Add(new TextBlock { Text = $"Found {SampleFamilies.Length} unused families (0 placed instances)", FontSize = 12, Foreground = DarkTheme.FgGold, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });

            var tbl = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 200 };
            var ts = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var tp = new StackPanel();
            foreach (var fam in SampleFamilies)
            {
                var row = new DockPanel { Margin = new Thickness(8, 3, 8, 3) };
                var cb = DarkTheme.MakeCheckBox("", true);
                cb.Checked += (s, e) => UpdateSummary();
                cb.Unchecked += (s, e) => UpdateSummary();
                _familyCbs.Add(cb);
                DockPanel.SetDock(cb, Dock.Left); row.Children.Add(cb);
                var infoText = new TextBlock { Text = $"{fam[0]}    {fam[1]}    {fam[2]} types    0 instances", FontSize = 11, Foreground = DarkTheme.FgLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
                row.Children.Add(infoText);
                tp.Children.Add(row);
            }
            ts.Content = tp; tbl.Child = ts;
            resContent.Children.Add(tbl);

            var selRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var allBtn = MkSmall("☑ All"); allBtn.Click += (s, e) => { foreach (var cb in _familyCbs) cb.IsChecked = true; };
            var noneBtn = MkSmall("☐ None"); noneBtn.Click += (s, e) => { foreach (var cb in _familyCbs) cb.IsChecked = false; };
            selRow.Children.Add(allBtn); selRow.Children.Add(noneBtn);
            resContent.Children.Add(selRow);
            c.Children.Add(DarkTheme.MakeGroupBox("Scan Results", resContent));

            // Summary
            var sumBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1F, 0x0A)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 0) };
            var sumStack = new StackPanel();
            _summaryText = new TextBlock { FontSize = 12, Foreground = DarkTheme.FgGold, TextWrapping = TextWrapping.Wrap };
            sumStack.Children.Add(_summaryText);
            sumStack.Children.Add(new TextBlock { Text = "⚠️ This action cannot be undone after saving the project.", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66)), Margin = new Thickness(0, 6, 0, 0) });
            sumBorder.Child = sumStack;
            c.Children.Add(sumBorder);

            scroll.Content = c;
            Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb2, delBtn; var bp = DarkTheme.MakeButtonPanel("🗑️ Delete Selected", out cb2, out delBtn);
            delBtn.Background = new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
            cb2.Click += (s, e) => Close(); delBtn.Click += DeleteBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg; UpdateSummary();
        }

        private void UpdateSummary()
        {
            int count = 0, types = 0;
            for (int i = 0; i < _familyCbs.Count; i++)
                if (_familyCbs[i].IsChecked == true) { count++; types += int.Parse(SampleFamilies[i][2]); }
            if (_summaryText != null)
                _summaryText.Text = $"🗑️ {count} families ({types} types) selected for deletion";
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var cat = (_catFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
            Close();
            DirectExecutor.RunAsync("delete_unused_families", DirectExecutor.Params(
                ("category", cat != null && !cat.Contains("All") ? cat : null),
                ("dryRun", false)
            ), "Delete Unused Families");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
        static Button MkSmall(string t) { var b = new Button { Content = t, Background = DarkTheme.BgCancel, Foreground = DarkTheme.FgLight, BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0) }; b.MouseEnter += (s, e) => b.Background = DarkTheme.BgCancelHover; b.MouseLeave += (s, e) => b.Background = DarkTheme.BgCancel; return b; }
    }
}
