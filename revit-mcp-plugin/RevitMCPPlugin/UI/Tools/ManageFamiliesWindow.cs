using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class ManageFamiliesWindow : Window
    {
        private ComboBox _actionCombo;
        private readonly StackPanel _actionPanel = new StackPanel();
        private TextBox _findBox, _replaceBox, _prefixBox, _suffixBox, _renameBox;
        private CheckBox _applyFamilyNames, _applyTypeNames, _caseSensitive;

        private static readonly string[][] Families = {
            new[]{"Walls","Basic Wall,Curtain Wall,Stacked Wall"},
            new[]{"Doors","Single-Flush,Single-Panel,Double-Flush,Bifold-2_Panel,Sliding-2_Panel"},
            new[]{"Windows","Fixed,Casement,Awning,Sliding"},
            new[]{"Furniture","Desk,Chair,Table,Bookcase,Sofa,Cabinet,Bed,Lamp"}
        };

        public ManageFamiliesWindow()
        {
            Title = "📦 Manage Families";
            Width = 800; Height = 620; MinWidth = 680; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("📦 Manage Families", "Rename, prefix, suffix, or find & replace in family names");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            // Main content: left tree + right action
            var body = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left: Family browser
            var leftPanel = new StackPanel();
            leftPanel.Children.Add(DarkTheme.MakeSectionHeader("Family Browser", DarkTheme.CatFamily));
            var searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search families...");
            searchBox.Margin = new Thickness(0, 0, 0, 8);
            leftPanel.Children.Add(searchBox);

            var treeBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 350 };
            var ts = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var tp = new StackPanel();
            foreach (var cat in Families)
            {
                var catRow = new TextBlock { Text = $"▼ {cat[0]} ({cat[1].Split(',').Length})", FontSize = 12, Foreground = DarkTheme.FgGold, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 2) };
                tp.Children.Add(catRow);
                foreach (var fam in cat[1].Split(','))
                    tp.Children.Add(new TextBlock { Text = $"    ├ {fam.Trim()}", FontSize = 11, Foreground = DarkTheme.FgLight, Margin = new Thickness(8, 1, 8, 1) });
            }
            ts.Content = tp; treeBorder.Child = ts;
            leftPanel.Children.Add(treeBorder);
            Grid.SetColumn(leftPanel, 0); body.Children.Add(leftPanel);

            // Right: Action panel
            var rightPanel = new StackPanel();
            rightPanel.Children.Add(DarkTheme.MakeSectionHeader("Action", DarkTheme.CatFamily));

            _actionCombo = DarkTheme.MakeComboBox(new[] { "Find & Replace", "Add Prefix", "Add Suffix", "Rename" }, "Find & Replace");
            _actionCombo.Margin = new Thickness(0, 0, 0, 12);
            _actionCombo.SelectionChanged += (s, e) => UpdateActionPanel();
            rightPanel.Children.Add(_actionCombo);
            rightPanel.Children.Add(_actionPanel);

            // Preview section
            rightPanel.Children.Add(DarkTheme.MakeSeparator());
            rightPanel.Children.Add(DarkTheme.MakeSectionHeader("Rename Preview", DarkTheme.CatFamily));
            var prevBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), Padding = new Thickness(10), MaxHeight = 120 };
            var prevStack = new StackPanel();
            prevStack.Children.Add(MkPrevRow("Before", "After", true));
            prevStack.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(0, 4, 0, 4) });
            prevStack.Children.Add(MkPrevRow("CW_200", "CW-200", false));
            prevStack.Children.Add(MkPrevRow("CW_300", "CW-300", false));
            prevStack.Children.Add(new TextBlock { Text = "2 families will be renamed", FontSize = 10, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 4, 0, 0) });
            prevBorder.Child = prevStack;
            rightPanel.Children.Add(prevBorder);

            Grid.SetColumn(rightPanel, 2); body.Children.Add(rightPanel);
            Grid.SetRow(body, 1); mg.Children.Add(body);

            // Footer
            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, ab; var bp = DarkTheme.MakeButtonPanel("Apply Changes ▶", out cb, out ab);
            cb.Click += (s, e) => Close(); ab.Click += ApplyBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg; UpdateActionPanel();
        }

        private void UpdateActionPanel()
        {
            _actionPanel.Children.Clear();
            var act = (_actionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (act.Contains("Find"))
            {
                _actionPanel.Children.Add(DarkTheme.MakeLabel("Find"));
                _findBox = DarkTheme.MakeTextBox("CW_"); _findBox.Margin = new Thickness(0, 0, 0, 8); _actionPanel.Children.Add(_findBox);
                _actionPanel.Children.Add(DarkTheme.MakeLabel("Replace"));
                _replaceBox = DarkTheme.MakeTextBox("CW-"); _replaceBox.Margin = new Thickness(0, 0, 0, 8); _actionPanel.Children.Add(_replaceBox);
                _applyFamilyNames = DarkTheme.MakeCheckBox("Apply to family names", true); _applyFamilyNames.Margin = new Thickness(0, 0, 0, 4); _actionPanel.Children.Add(_applyFamilyNames);
                _applyTypeNames = DarkTheme.MakeCheckBox("Apply to type names", false); _applyTypeNames.Margin = new Thickness(0, 0, 0, 4); _actionPanel.Children.Add(_applyTypeNames);
                _caseSensitive = DarkTheme.MakeCheckBox("Case sensitive", true); _actionPanel.Children.Add(_caseSensitive);
            }
            else if (act.Contains("Prefix"))
            {
                _actionPanel.Children.Add(DarkTheme.MakeLabel("Prefix"));
                _prefixBox = DarkTheme.MakeTextBox("PRJ_"); _prefixBox.Margin = new Thickness(0, 0, 0, 8); _actionPanel.Children.Add(_prefixBox);
                _applyFamilyNames = DarkTheme.MakeCheckBox("Apply to family names", true); _applyFamilyNames.Margin = new Thickness(0, 0, 0, 4); _actionPanel.Children.Add(_applyFamilyNames);
                _applyTypeNames = DarkTheme.MakeCheckBox("Apply to type names", false); _actionPanel.Children.Add(_applyTypeNames);
            }
            else if (act.Contains("Suffix"))
            {
                _actionPanel.Children.Add(DarkTheme.MakeLabel("Suffix"));
                _suffixBox = DarkTheme.MakeTextBox("_v2"); _suffixBox.Margin = new Thickness(0, 0, 0, 8); _actionPanel.Children.Add(_suffixBox);
                _applyFamilyNames = DarkTheme.MakeCheckBox("Apply to family names", true); _applyFamilyNames.Margin = new Thickness(0, 0, 0, 4); _actionPanel.Children.Add(_applyFamilyNames);
                _applyTypeNames = DarkTheme.MakeCheckBox("Apply to type names", false); _actionPanel.Children.Add(_applyTypeNames);
            }
            else
            {
                _actionPanel.Children.Add(DarkTheme.MakeLabel("New Name"));
                _renameBox = DarkTheme.MakeTextBox(""); _actionPanel.Children.Add(_renameBox);
            }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            var act = (_actionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string action = null, find = null, replace = null, prefix = null, suffix = null, newName = null;
            if (act.Contains("Find")) { action = "find_replace"; find = _findBox?.Text; replace = _replaceBox?.Text; }
            else if (act.Contains("Prefix")) { action = "add_prefix"; prefix = _prefixBox?.Text; }
            else if (act.Contains("Suffix")) { action = "add_suffix"; suffix = _suffixBox?.Text; }
            else { action = "rename"; newName = _renameBox?.Text; }
            Close();
            DirectExecutor.RunAsync("manage_families", DirectExecutor.Params(
                ("action", action), ("find", find), ("replace", replace),
                ("prefix", prefix), ("suffix", suffix), ("newName", newName)
            ), "Manage Families");
        }

        static Grid MkPrevRow(string b, string a, bool hdr)
        {
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var t1 = new TextBlock { Text = b, FontSize = 11, Foreground = hdr ? DarkTheme.FgDim : DarkTheme.FgLight, FontWeight = hdr ? FontWeights.Bold : FontWeights.Normal }; Grid.SetColumn(t1, 0); g.Children.Add(t1);
            var t2 = new TextBlock { Text = "→", FontSize = 11, Foreground = DarkTheme.FgDim, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(t2, 1); g.Children.Add(t2);
            var t3 = new TextBlock { Text = a, FontSize = 11, Foreground = hdr ? DarkTheme.FgDim : DarkTheme.FgGreen, FontWeight = hdr ? FontWeights.Bold : FontWeights.Normal }; Grid.SetColumn(t3, 2); g.Children.Add(t3);
            return g;
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
