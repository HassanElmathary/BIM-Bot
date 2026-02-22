using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class BatchSetParamWindow : Window
    {
        private ComboBox _catCombo, _paramCombo, _filterParamCombo, _filterLevelCombo;
        private TextBox _valueBox, _filterValueBox;
        private CheckBox _enableFilter;

        public BatchSetParamWindow()
        {
            Title = "✏️ Batch Set Parameter";
            Width = 750; Height = 620; MinWidth = 640; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("✏️ Batch Set Parameter", "Set a parameter value on all matching elements");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Target group
            var tgt = new StackPanel();
            tgt.Children.Add(DarkTheme.MakeLabel("Category *"));
            _catCombo = DarkTheme.MakeComboBox(new[] { "Walls", "Doors", "Windows", "Rooms", "Floors", "Columns", "Beams", "Pipes", "Ducts" }, "Walls");
            _catCombo.Margin = new Thickness(0, 0, 0, 8); tgt.Children.Add(_catCombo);

            tgt.Children.Add(DarkTheme.MakeLabel("Parameter *"));
            _paramCombo = DarkTheme.MakeComboBox(new[] { "Comments", "Mark", "Type Name", "Level", "Length", "Area", "Volume" }, "Comments");
            _paramCombo.Margin = new Thickness(0, 0, 0, 8); tgt.Children.Add(_paramCombo);

            tgt.Children.Add(DarkTheme.MakeLabel("New Value *"));
            _valueBox = DarkTheme.MakeTextBox("Exterior Wall"); _valueBox.Margin = new Thickness(0, 0, 0, 4); tgt.Children.Add(_valueBox);
            c.Children.Add(DarkTheme.MakeGroupBox("Target", tgt));

            // Filter group
            var flt = new StackPanel();
            _enableFilter = DarkTheme.MakeCheckBox("Filter elements before applying", true);
            _enableFilter.Margin = new Thickness(0, 0, 0, 8); flt.Children.Add(_enableFilter);
            flt.Children.Add(DarkTheme.MakeLabel("Filter by Parameter"));
            _filterParamCombo = DarkTheme.MakeComboBox(new[] { "Type Name", "Mark", "Comments", "Level" }, "Type Name");
            _filterParamCombo.Margin = new Thickness(0, 0, 0, 8); flt.Children.Add(_filterParamCombo);
            flt.Children.Add(DarkTheme.MakeLabel("Filter Value"));
            _filterValueBox = DarkTheme.MakeTextBox("CW-200"); _filterValueBox.Margin = new Thickness(0, 0, 0, 8); flt.Children.Add(_filterValueBox);
            flt.Children.Add(DarkTheme.MakeLabel("Filter by Level"));
            _filterLevelCombo = DarkTheme.MakeComboBox(new[] { "All Levels", "Level 0", "Level 1", "Level 2" }, "All Levels");
            flt.Children.Add(_filterLevelCombo);
            c.Children.Add(DarkTheme.MakeGroupBox("Filter (optional)", flt));

            // Preview
            var prev = new StackPanel();
            var tbl = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 150 };
            var ts = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var tp = new StackPanel();
            tp.Children.Add(MkRow("ElemID", "Family", "Type", "Current", "New", true));
            tp.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(8, 0, 8, 0) });
            tp.Children.Add(MkRow("456789", "Basic Wall", "CW-200", "(empty)", "Exterior...", false));
            tp.Children.Add(MkRow("456801", "Basic Wall", "CW-200", "Interior", "Exterior...", false));
            tp.Children.Add(MkRow("456812", "Basic Wall", "CW-200", "(empty)", "Exterior...", false));
            ts.Content = tp; tbl.Child = ts; prev.Children.Add(tbl);
            prev.Children.Add(new TextBlock { Text = "⚠️ 5 of 45 walls match — 3 will change, 2 will overwrite", FontSize = 11, Foreground = DarkTheme.FgGold, Margin = new Thickness(0, 6, 0, 0) });
            c.Children.Add(DarkTheme.MakeGroupBox("Affected Elements Preview", prev));

            scroll.Content = c;
            Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, ab; var bp = DarkTheme.MakeButtonPanel("Apply to 5 ▶", out cb, out ab);
            cb.Click += (s, e) => Close(); ab.Click += ApplyBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            var cat = (_catCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var param = (_paramCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var val = _valueBox?.Text;
            string filterParam = null, filterVal = null, levelName = null;
            if (_enableFilter?.IsChecked == true)
            {
                filterParam = (_filterParamCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                filterVal = _filterValueBox?.Text;
                var lvl = (_filterLevelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (lvl != null && !lvl.Contains("All")) levelName = lvl;
            }
            Close();
            DirectExecutor.RunAsync("batch_set_parameter", DirectExecutor.Params(
                ("category", cat), ("parameterName", param), ("value", val),
                ("filterParameterName", filterParam), ("filterValue", filterVal), ("levelName", levelName)
            ), "Batch Set Parameter");
        }

        static Grid MkRow(string c1, string c2, string c3, string c4, string c5, bool hdr)
        {
            var g = new Grid { Margin = new Thickness(8, 3, 8, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var fg = hdr ? DarkTheme.FgDim : DarkTheme.FgLight; var fw = hdr ? FontWeights.Bold : FontWeights.Normal;
            var vals = new[] { c1, c2, c3, c4, c5 };
            for (int i = 0; i < 5; i++) { var t = new TextBlock { Text = vals[i], FontSize = 11, Foreground = fg, FontWeight = fw, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(t, i); g.Children.Add(t); }
            return g;
        }
        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
