using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class AlignViewportsWindow : Window
    {
        private TextBox _refSheetId, _targetSheetIds;

        public AlignViewportsWindow()
        {
            Title = "📏 Align Viewports";
            Width = 540; Height = 420; MinWidth = 440; MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("📏 Align Viewports", "Align viewport positions across multiple sheets");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // Reference sheet
            var refContent = new StackPanel();
            refContent.Children.Add(DarkTheme.MakeLabel("Reference Sheet ID *"));
            refContent.Children.Add(new TextBlock { Text = "The sheet whose viewport positions will be copied", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            _refSheetId = DarkTheme.MakeTextBox(placeholder: "Enter sheet element ID"); refContent.Children.Add(_refSheetId);
            c.Children.Add(DarkTheme.MakeGroupBox("Reference Sheet", refContent));

            // Target sheets
            var tgtContent = new StackPanel();
            tgtContent.Children.Add(DarkTheme.MakeLabel("Target Sheet IDs *"));
            tgtContent.Children.Add(new TextBlock { Text = "Sheets that will receive the aligned viewport positions", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            _targetSheetIds = DarkTheme.MakeTextBox(placeholder: "Comma-separated sheet IDs");
            _targetSheetIds.Height = 60; _targetSheetIds.AcceptsReturn = true; _targetSheetIds.TextWrapping = TextWrapping.Wrap;
            tgtContent.Children.Add(_targetSheetIds);
            c.Children.Add(DarkTheme.MakeGroupBox("Target Sheets", tgtContent));

            // Info
            var infoBox = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x1F, 0x2A)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 0) };
            infoBox.Child = new TextBlock { Text = "💡 Viewports on target sheets will be repositioned to match the reference sheet's viewport locations. Matching is done by view name.", FontSize = 11, Foreground = DarkTheme.FgDim, TextWrapping = TextWrapping.Wrap };
            c.Children.Add(infoBox);

            scroll.Content = c; Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb, alignBtn; var bp = DarkTheme.MakeButtonPanel("Align ▶", out cb, out alignBtn);
            cb.Click += (s, e) => Close(); alignBtn.Click += AlignBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void AlignBtn_Click(object sender, RoutedEventArgs e)
        {
            var refId = _refSheetId?.Foreground != DarkTheme.FgDim ? _refSheetId?.Text?.Trim() : null;
            var tgtIds = _targetSheetIds?.Foreground != DarkTheme.FgDim ? _targetSheetIds?.Text?.Trim() : null;
            Close();
            DirectExecutor.RunAsync("align_viewports", DirectExecutor.Params(
                ("referenceSheetId", refId), ("targetSheetIds", tgtIds)
            ), "Align Viewports");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
