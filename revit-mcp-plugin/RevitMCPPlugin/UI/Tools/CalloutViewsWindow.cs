using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class CalloutViewsWindow : Window
    {
        private TextBox _roomIds, _parentViewId, _viewTemplate;
        private ComboBox _scaleCombo;
        private CheckBox _allRooms;

        public CalloutViewsWindow()
        {
            Title = "🔍 Create Callout Views";
            Width = 500; Height = 480; MinWidth = 420; MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("🔍 Create Callout Views", "Generate callout views for rooms");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Room selection
            var roomContent = new StackPanel();
            _allRooms = DarkTheme.MakeCheckBox("All rooms (leave IDs empty)", true);
            _allRooms.Margin = new Thickness(0, 0, 0, 6);
            _allRooms.Checked += (s, e) => _roomIds.IsEnabled = false;
            _allRooms.Unchecked += (s, e) => _roomIds.IsEnabled = true;
            roomContent.Children.Add(_allRooms);
            roomContent.Children.Add(DarkTheme.MakeLabel("Room IDs (comma-separated)"));
            _roomIds = DarkTheme.MakeTextBox(placeholder: "e.g. 12345, 12346"); _roomIds.IsEnabled = false;
            roomContent.Children.Add(_roomIds);
            c.Children.Add(DarkTheme.MakeGroupBox("Room Selection", roomContent));

            // Parent view
            var parentContent = new StackPanel();
            parentContent.Children.Add(DarkTheme.MakeLabel("Parent View ID"));
            _parentViewId = DarkTheme.MakeTextBox(placeholder: "Element ID of the parent view");
            parentContent.Children.Add(_parentViewId);
            c.Children.Add(DarkTheme.MakeGroupBox("Parent View", parentContent));

            // View settings
            var settContent = new StackPanel();
            settContent.Children.Add(DarkTheme.MakeLabel("Scale"));
            _scaleCombo = DarkTheme.MakeComboBox(new[] { "1:10", "1:20", "1:50", "1:100" }, "1:20");
            _scaleCombo.Margin = new Thickness(0, 0, 0, 8); settContent.Children.Add(_scaleCombo);
            settContent.Children.Add(DarkTheme.MakeLabel("View Template"));
            _viewTemplate = DarkTheme.MakeTextBox(placeholder: "Template name (optional)");
            settContent.Children.Add(_viewTemplate);
            c.Children.Add(DarkTheme.MakeGroupBox("View Settings", settContent));

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, createBtn; var bp = DarkTheme.MakeButtonPanel("Create ▶", out cb, out createBtn);
            cb.Click += (s, e) => Close(); createBtn.Click += CreateBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            var roomIds = (_allRooms.IsChecked != true && !string.IsNullOrWhiteSpace(_roomIds?.Text) && _roomIds.Foreground != DarkTheme.FgDim) ? _roomIds.Text : null;
            var pid = _parentViewId?.Foreground != DarkTheme.FgDim ? _parentViewId?.Text?.Trim() : null;
            var scale = (_scaleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Split(':')[1];
            var tmpl = _viewTemplate?.Foreground != DarkTheme.FgDim ? _viewTemplate?.Text?.Trim() : null;
            Close();
            DirectExecutor.RunAsync("create_callout_views", DirectExecutor.Params(
                ("roomIds", roomIds), ("parentViewId", pid), ("scale", scale), ("viewTemplate", tmpl)
            ), "Create Callout Views");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
