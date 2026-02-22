using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class DuplicateViewWindow : Window
    {
        private TextBox _viewId, _prefix, _suffix;
        private ComboBox _countCombo;
        private RadioButton _rbDuplicate, _rbWithDetailing, _rbAsDependent;

        public DuplicateViewWindow()
        {
            Title = "📋 Duplicate View";
            Width = 550; Height = 500; MinWidth = 460; MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("📋 Duplicate View", "Duplicate views with various options");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // View selection
            var viewContent = new StackPanel();
            var vRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            vRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            vRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            var vidPanel = new StackPanel();
            vidPanel.Children.Add(DarkTheme.MakeLabel("View ID *"));
            _viewId = DarkTheme.MakeTextBox(placeholder: "Enter view element ID"); vidPanel.Children.Add(_viewId);
            Grid.SetColumn(vidPanel, 0); vRow.Children.Add(vidPanel);
            var cntPanel = new StackPanel();
            cntPanel.Children.Add(DarkTheme.MakeLabel("Copies"));
            _countCombo = DarkTheme.MakeComboBox(new[] { "1", "2", "3", "5", "10" }, "1");
            cntPanel.Children.Add(_countCombo);
            Grid.SetColumn(cntPanel, 2); vRow.Children.Add(cntPanel);
            viewContent.Children.Add(vRow);
            c.Children.Add(DarkTheme.MakeGroupBox("View", viewContent));

            // Duplication options
            var optContent = new StackPanel();
            optContent.Children.Add(new TextBlock { Text = "Mode:", FontSize = 12, Foreground = DarkTheme.FgLight, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            _rbDuplicate = new RadioButton { Content = "Duplicate — view only, no annotations", GroupName = "mode", Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 0, 0, 6) };
            _rbWithDetailing = new RadioButton { Content = "With Detailing — includes annotations & details", GroupName = "mode", IsChecked = true, Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 0, 0, 6) };
            _rbAsDependent = new RadioButton { Content = "As Dependent — linked dependent view", GroupName = "mode", Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 0, 0, 8) };
            optContent.Children.Add(_rbDuplicate); optContent.Children.Add(_rbWithDetailing); optContent.Children.Add(_rbAsDependent);

            optContent.Children.Add(DarkTheme.MakeSeparator());
            optContent.Children.Add(new TextBlock { Text = "Naming:", FontSize = 12, Foreground = DarkTheme.FgLight, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 6) });
            var nameRow = new Grid();
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var prefPanel = new StackPanel();
            prefPanel.Children.Add(DarkTheme.MakeLabel("Prefix"));
            _prefix = DarkTheme.MakeTextBox(placeholder: "(optional)"); prefPanel.Children.Add(_prefix);
            Grid.SetColumn(prefPanel, 0); nameRow.Children.Add(prefPanel);
            var sufPanel = new StackPanel();
            sufPanel.Children.Add(DarkTheme.MakeLabel("Suffix"));
            _suffix = DarkTheme.MakeTextBox(" - Copy"); sufPanel.Children.Add(_suffix);
            Grid.SetColumn(sufPanel, 2); nameRow.Children.Add(sufPanel);
            optContent.Children.Add(nameRow);
            c.Children.Add(DarkTheme.MakeGroupBox("Duplication Options", optContent));

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, dupBtn; var bp = DarkTheme.MakeButtonPanel("Duplicate ▶", out cb, out dupBtn);
            cb.Click += (s, e) => Close(); dupBtn.Click += DupBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void DupBtn_Click(object sender, RoutedEventArgs e)
        {
            var vid = _viewId?.Foreground != DarkTheme.FgDim ? _viewId?.Text?.Trim() : null;
            var count = (_countCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var mode = _rbDuplicate.IsChecked == true ? "independent" : _rbAsDependent.IsChecked == true ? "as_dependent" : "with_detailing";
            var suf = _suffix?.Foreground != DarkTheme.FgDim ? _suffix?.Text?.Trim() : null;
            Close();
            DirectExecutor.RunAsync("duplicate_view", DirectExecutor.Params(
                ("viewId", vid), ("count", count), ("duplicateType", mode), ("suffix", suf)
            ), "Duplicate View");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
