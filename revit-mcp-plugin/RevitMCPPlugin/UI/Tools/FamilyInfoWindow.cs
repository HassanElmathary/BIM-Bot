using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class FamilyInfoWindow : Window
    {
        private int _selectedCat = 0;
        private static readonly string[] Categories = { "🧱 Walls", "🚪 Doors", "🪟 Windows", "🪑 Furniture", "📐 Columns", "🔩 Beams" };
        private static readonly string[][] SampleFamilies = {
            new[]{"Basic Wall (3 types, 45 inst)","Curtain Wall (2 types, 8 inst)","Stacked Wall (1 type, 2 inst)"},
            new[]{"Single-Flush (3 types, 12 inst)","Double-Flush (2 types, 8 inst)","Bifold-2P (1 type, 2 inst)","Sliding-2P (2 types, 4 inst)","Single-Panel (2 types, 6 inst)"},
            new[]{"Fixed (2 types, 10 inst)","Casement (3 types, 8 inst)","Awning (1 type, 4 inst)","Sliding (2 types, 6 inst)"},
            new[]{"Desk (2 types, 8 inst)","Chair (3 types, 14 inst)","Table (2 types, 6 inst)","Bookcase (1 type, 3 inst)"},
            new[]{"Concrete-Round (2 types, 12 inst)","Steel-Wide (3 types, 6 inst)"},
            new[]{"W-Shape (4 types, 8 inst)"}
        };

        public FamilyInfoWindow()
        {
            Title = "ℹ️ Family Info Browser";
            Width = 800; Height = 580; MinWidth = 680; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("ℹ️ Family Info Browser", "View loaded families, types, and instance counts");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            // 3-column layout
            var body = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            // Left: categories
            var leftPanel = new StackPanel();
            leftPanel.Children.Add(new TextBlock { Text = "Category", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var catBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1) };
            var catStack = new StackPanel();
            for (int i = 0; i < Categories.Length; i++)
            {
                var idx = i;
                var btn = new Button { Content = Categories[i], Background = i == 0 ? DarkTheme.BgAccent : Brushes.Transparent, Foreground = DarkTheme.FgLight, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8), FontSize = 12, Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left };
                btn.Click += (s, e) => { _selectedCat = idx; RefreshCategories(catStack); };
                catStack.Children.Add(btn);
            }
            catBorder.Child = catStack; leftPanel.Children.Add(catBorder);
            Grid.SetColumn(leftPanel, 0); body.Children.Add(leftPanel);

            // Center: families & types
            var centerPanel = new StackPanel();
            centerPanel.Children.Add(new TextBlock { Text = "Families & Types", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var famBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), MaxHeight = 380 };
            var fs = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fp = new StackPanel();
            foreach (var f in SampleFamilies[0])
                fp.Children.Add(new TextBlock { Text = $"📦 {f}", FontSize = 11, Foreground = DarkTheme.FgLight, Margin = new Thickness(8, 4, 8, 4) });
            fs.Content = fp; famBorder.Child = fs;
            centerPanel.Children.Add(famBorder);
            Grid.SetColumn(centerPanel, 2); body.Children.Add(centerPanel);

            // Right: details
            var rightPanel = new StackPanel();
            rightPanel.Children.Add(new TextBlock { Text = "Details", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 6) });
            var detBorder = new Border { Background = DarkTheme.BgCard, CornerRadius = new CornerRadius(6), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(1), Padding = new Thickness(12) };
            var ds = new StackPanel();
            ds.Children.Add(new TextBlock { Text = "📦 Basic Wall", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            ds.Children.Add(new TextBlock { Text = "Category: Walls", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 4, 0, 0) });
            ds.Children.Add(new TextBlock { Text = "Types: 3", FontSize = 11, Foreground = DarkTheme.FgDim });
            ds.Children.Add(new TextBlock { Text = "Total Instances: 45", FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 0, 0, 8) });
            ds.Children.Add(DarkTheme.MakeSeparator());
            ds.Children.Add(new TextBlock { Text = "Parameters:", FontSize = 11, Foreground = DarkTheme.FgGold, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 4) });
            foreach (var pr in new[] { "Width → 200 mm", "Height → 3000 mm", "Function → Interior", "Fire Rating → None" })
                ds.Children.Add(new TextBlock { Text = pr, FontSize = 11, Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 2, 0, 0) });
            detBorder.Child = ds; rightPanel.Children.Add(detBorder);
            Grid.SetColumn(rightPanel, 4); body.Children.Add(rightPanel);

            Grid.SetRow(body, 1); mg.Children.Add(body);

            // Footer
            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            var fd = new DockPanel();
            var info = new TextBlock { Text = "6 categories, 20 families, 156 instances", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(info, Dock.Left); fd.Children.Add(info);
            Button cb, eb; var bp = DarkTheme.MakeButtonPanel("📋 Query ▶", out cb, out eb);
            cb.Click += (s, e) => Close();
            eb.Click += (s, e) => { Close(); DirectExecutor.RunAsync("get_available_family_types", DirectExecutor.Params(("category", Categories[_selectedCat].Substring(2).Trim())), "Family Info"); };
            DockPanel.SetDock(bp, Dock.Right); fd.Children.Add(bp);
            ft.Child = fd; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg;
        }

        private void RefreshCategories(StackPanel catStack)
        {
            for (int i = 0; i < catStack.Children.Count; i++)
                if (catStack.Children[i] is Button b)
                    b.Background = i == _selectedCat ? DarkTheme.BgAccent : Brushes.Transparent;
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
    }
}
