using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class BatchCreateSheetsWindow : Window
    {
        private TextBox _startNumber, _namePattern, _titleBlock;
        private ComboBox _countCombo, _disciplineCombo;

        public BatchCreateSheetsWindow()
        {
            Title = "📑 Batch Create Sheets";
            Width = 580; Height = 520; MinWidth = 480; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("📑 Batch Create Sheets", "Create multiple sheets with auto-incrementing numbers");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Title block
            var tbContent = new StackPanel();
            tbContent.Children.Add(DarkTheme.MakeLabel("Title Block *"));
            _titleBlock = DarkTheme.MakeTextBox("A1 metric"); tbContent.Children.Add(_titleBlock);
            c.Children.Add(DarkTheme.MakeGroupBox("Title Block", tbContent));

            // Sheet info
            var siContent = new StackPanel();
            var numRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            numRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            numRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            numRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            var snPanel = new StackPanel();
            snPanel.Children.Add(DarkTheme.MakeLabel("Start Number *"));
            _startNumber = DarkTheme.MakeTextBox("A101"); snPanel.Children.Add(_startNumber);
            Grid.SetColumn(snPanel, 0); numRow.Children.Add(snPanel);
            var cntPanel = new StackPanel();
            cntPanel.Children.Add(DarkTheme.MakeLabel("Count *"));
            _countCombo = DarkTheme.MakeComboBox(new[] { "1", "2", "3", "5", "10", "15", "20" }, "5");
            cntPanel.Children.Add(_countCombo);
            Grid.SetColumn(cntPanel, 2); numRow.Children.Add(cntPanel);
            siContent.Children.Add(numRow);

            siContent.Children.Add(DarkTheme.MakeLabel("Name Pattern"));
            siContent.Children.Add(new TextBlock { Text = "Use {n} for sequential number", FontSize = 10, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 4) });
            _namePattern = DarkTheme.MakeTextBox("Floor Plan {n}"); _namePattern.Margin = new Thickness(0, 0, 0, 8); siContent.Children.Add(_namePattern);

            siContent.Children.Add(DarkTheme.MakeLabel("Discipline"));
            _disciplineCombo = DarkTheme.MakeComboBox(new[] { "Architectural", "Structural", "Mechanical", "Electrical", "Plumbing" }, "Architectural");
            siContent.Children.Add(_disciplineCombo);
            c.Children.Add(DarkTheme.MakeGroupBox("Sheet Info", siContent));

            // Preview
            var prevContent = new StackPanel();
            var prevBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), Padding = new Thickness(10), MaxHeight = 120 };
            var tp = new StackPanel();
            tp.Children.Add(MkPrevRow("Sheet #", "Sheet Name", true));
            tp.Children.Add(new Border { Height = 1, Background = DarkTheme.BorderDim, Margin = new Thickness(0, 3, 0, 3) });
            tp.Children.Add(MkPrevRow("A101", "Floor Plan 1", false));
            tp.Children.Add(MkPrevRow("A102", "Floor Plan 2", false));
            tp.Children.Add(MkPrevRow("A103", "Floor Plan 3", false));
            tp.Children.Add(new TextBlock { Text = "... 2 more sheets", FontSize = 10, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) });
            prevBorder.Child = tp; prevContent.Children.Add(prevBorder);
            c.Children.Add(DarkTheme.MakeGroupBox("Preview", prevContent));

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, createBtn; var bp = DarkTheme.MakeButtonPanel("Create ▶", out cb, out createBtn);
            cb.Click += (s, e) => Close(); createBtn.Click += CreateBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            var sn = _startNumber?.Foreground != DarkTheme.FgDim ? _startNumber?.Text?.Trim() : null;
            var count = (_countCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var np = _namePattern?.Foreground != DarkTheme.FgDim ? _namePattern?.Text?.Trim() : null;
            var tb = _titleBlock?.Foreground != DarkTheme.FgDim ? _titleBlock?.Text?.Trim() : null;
            Close();
            DirectExecutor.RunAsync("batch_create_sheets", DirectExecutor.Params(
                ("startNumber", sn), ("count", count), ("namePattern", np), ("titleBlockName", tb)
            ), "Batch Create Sheets");
        }

        static Grid MkPrevRow(string num, string name, bool hdr)
        {
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var fg = hdr ? DarkTheme.FgDim : DarkTheme.FgLight; var fw = hdr ? FontWeights.Bold : FontWeights.Normal;
            var t1 = new TextBlock { Text = num, FontSize = 11, Foreground = fg, FontWeight = fw }; Grid.SetColumn(t1, 0); g.Children.Add(t1);
            var t2 = new TextBlock { Text = name, FontSize = 11, Foreground = fg, FontWeight = fw }; Grid.SetColumn(t2, 1); g.Children.Add(t2);
            return g;
        }
        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
