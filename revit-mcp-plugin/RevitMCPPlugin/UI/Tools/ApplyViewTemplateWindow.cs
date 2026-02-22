using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class ApplyViewTemplateWindow : Window
    {
        private TextBox _viewIds;
        private ComboBox _templateCombo;

        private static readonly string[] SampleTemplates = { "Architectural Plan", "Architectural Section", "Architectural Elevation", "Structural Plan", "MEP Plan", "3D Default", "Detail View", "Legend" };
        private static readonly string[][] SampleViews = {
            new[]{"Level 0 - Floor Plan","FloorPlan"},
            new[]{"Level 1 - Floor Plan","FloorPlan"},
            new[]{"Level 2 - Floor Plan","FloorPlan"},
            new[]{"North Elevation","Elevation"},
            new[]{"Section A-A","Section"},
            new[]{"3D - Working","3D View"}
        };

        public ApplyViewTemplateWindow()
        {
            Title = "🎨 Apply View Template";
            Width = 600; Height = 540; MinWidth = 500; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("🎨 Apply View Template", "Apply a view template to one or more views");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Template selection
            var tmplContent = new StackPanel();
            tmplContent.Children.Add(DarkTheme.MakeLabel("Template *"));
            _templateCombo = DarkTheme.MakeComboBox(SampleTemplates, "Architectural Plan");
            tmplContent.Children.Add(_templateCombo);
            c.Children.Add(DarkTheme.MakeGroupBox("View Template", tmplContent));

            // View selection
            var viewContent = new StackPanel();
            viewContent.Children.Add(DarkTheme.MakeLabel("View IDs *"));
            viewContent.Children.Add(new TextBlock { Text = "Enter comma-separated view element IDs", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            _viewIds = DarkTheme.MakeTextBox(placeholder: "e.g. 12345, 12346, 12347");
            _viewIds.Height = 50; _viewIds.AcceptsReturn = true; _viewIds.TextWrapping = TextWrapping.Wrap;
            viewContent.Children.Add(_viewIds);
            c.Children.Add(DarkTheme.MakeGroupBox("Target Views", viewContent));

            // Available views reference
            var refContent = new StackPanel();
            refContent.Children.Add(new TextBlock { Text = "Available views in project (for reference):", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var refBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 140 };
            var rs = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rp = new StackPanel();
            foreach (var v in SampleViews)
            {
                var row = new Grid { Margin = new Thickness(8, 3, 8, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                var t1 = new TextBlock { Text = v[0], FontSize = 11, Foreground = DarkTheme.FgLight }; Grid.SetColumn(t1, 0); row.Children.Add(t1);
                var t2 = new TextBlock { Text = v[1], FontSize = 11, Foreground = DarkTheme.FgDim }; Grid.SetColumn(t2, 1); row.Children.Add(t2);
                rp.Children.Add(row);
            }
            rs.Content = rp; refBorder.Child = rs; refContent.Children.Add(refBorder);
            c.Children.Add(DarkTheme.MakeGroupBox("Reference", refContent));

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, applyBtn; var bp = DarkTheme.MakeButtonPanel("Apply ▶", out cb, out applyBtn);
            cb.Click += (s, e) => Close(); applyBtn.Click += ApplyBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            var ids = _viewIds?.Foreground != DarkTheme.FgDim ? _viewIds?.Text?.Trim() : null;
            var template = (_templateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            Close();
            DirectExecutor.RunAsync("apply_view_template", DirectExecutor.Params(
                ("viewIds", ids), ("templateName", template)
            ), "Apply View Template");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
