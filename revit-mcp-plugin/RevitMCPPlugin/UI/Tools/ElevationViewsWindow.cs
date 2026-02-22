using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class ElevationViewsWindow : Window
    {
        private TextBox _roomIds, _viewTemplate;
        private ComboBox _levelCombo, _scaleCombo;
        private CheckBox _allRooms;
        private readonly string[] _directions = { "N", "E", "S", "W" };
        private readonly List<CheckBox> _dirCbs = new List<CheckBox>();

        public ElevationViewsWindow()
        {
            Title = "🏠 Create Elevation Views";
            Width = 500; Height = 520; MinWidth = 440; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("🏠 Create Elevation Views", "Auto-generate interior elevations for rooms");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Room selection
            var roomContent = new StackPanel();
            _allRooms = DarkTheme.MakeCheckBox("All rooms in project", true);
            _allRooms.Margin = new Thickness(0, 0, 0, 6);
            _allRooms.Checked += (s, e) => _roomIds.IsEnabled = false;
            _allRooms.Unchecked += (s, e) => _roomIds.IsEnabled = true;
            roomContent.Children.Add(_allRooms);
            roomContent.Children.Add(DarkTheme.MakeLabel("Room IDs (comma-separated)"));
            _roomIds = DarkTheme.MakeTextBox(placeholder: "e.g. 12345, 12346"); _roomIds.IsEnabled = false;
            roomContent.Children.Add(_roomIds);
            roomContent.Children.Add(DarkTheme.MakeLabel("Filter by Level"));
            _levelCombo = DarkTheme.MakeComboBox(new[] { "All Levels", "Level 0", "Level 1", "Level 2", "Level 3" }, "All Levels");
            roomContent.Children.Add(_levelCombo);
            c.Children.Add(DarkTheme.MakeGroupBox("Room Selection", roomContent));

            // Directions
            var dirContent = new StackPanel();
            dirContent.Children.Add(new TextBlock { Text = "Generate elevations facing:", FontSize = 12, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var dirRow = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var d in _directions)
            {
                var cb = DarkTheme.MakeCheckBox(d, true); cb.Margin = new Thickness(0, 0, 16, 0);
                _dirCbs.Add(cb); dirRow.Children.Add(cb);
            }
            dirContent.Children.Add(dirRow);
            c.Children.Add(DarkTheme.MakeGroupBox("Elevation Directions", dirContent));

            // View settings
            var settContent = new StackPanel();
            settContent.Children.Add(DarkTheme.MakeLabel("Scale"));
            _scaleCombo = DarkTheme.MakeComboBox(new[] { "1:20", "1:50", "1:100", "1:200" }, "1:50");
            _scaleCombo.Margin = new Thickness(0, 0, 0, 8); settContent.Children.Add(_scaleCombo);
            settContent.Children.Add(DarkTheme.MakeLabel("View Template"));
            _viewTemplate = DarkTheme.MakeTextBox(placeholder: "e.g. Interior Elevation (optional)");
            settContent.Children.Add(_viewTemplate);
            c.Children.Add(DarkTheme.MakeGroupBox("View Settings", settContent));

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb2, createBtn; var bp = DarkTheme.MakeButtonPanel("Create ▶", out cb2, out createBtn);
            cb2.Click += (s, e) => Close(); createBtn.Click += CreateBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            var roomIds = (_allRooms.IsChecked != true && !string.IsNullOrWhiteSpace(_roomIds.Text) && _roomIds.Foreground != DarkTheme.FgDim) ? _roomIds.Text : null;
            var lvl = (_levelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var scale = (_scaleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Split(':')[1];
            var tmpl = _viewTemplate?.Foreground != DarkTheme.FgDim ? _viewTemplate?.Text?.Trim() : null;
            Close();
            DirectExecutor.RunAsync("create_elevation_views", DirectExecutor.Params(
                ("roomIds", roomIds),
                ("levelName", lvl != null && !lvl.Contains("All") ? lvl : null),
                ("scale", scale),
                ("viewTemplate", tmpl)
            ), "Create Elevation Views");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
