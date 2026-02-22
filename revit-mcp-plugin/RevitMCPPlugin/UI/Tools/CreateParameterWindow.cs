using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    public class CreateParameterWindow : Window
    {
        private TextBox _paramName, _tooltipBox, _searchBox;
        private ComboBox _paramType, _paramGroup;
        private RadioButton _rbInstance, _rbType;
        private RadioButton _rbProject, _rbShared;
        private CheckBox _addTooltip;
        private readonly List<CheckBox> _categoryCbs = new List<CheckBox>();
        private readonly List<string> _categoryNames = new List<string>();
        private TextBlock _statusText;
        private StackPanel _catPanel;

        // ── All Revit built-in categories ──
        private static readonly string[] AllCategories = new[]
        {
            // ── Architectural ──
            "Walls", "Doors", "Windows", "Rooms", "Areas", "Floors", "Ceilings",
            "Roofs", "Stairs", "Railings", "Ramps", "Curtain Walls", "Curtain Panels",
            "Curtain Wall Mullions", "Columns", "Generic Models", "Mass",
            "Model Groups", "Detail Groups", "Furniture", "Furniture Systems",
            "Casework", "Planting", "Topography", "Parking", "Entourage",
            
            // ── Structural ──
            "Structural Columns", "Structural Framing", "Structural Foundations",
            "Structural Connections", "Structural Rebar", "Structural Fabric Areas",
            "Structural Fabric Reinforcement", "Structural Area Reinforcement",
            "Structural Path Reinforcement", "Structural Beam Systems",
            "Structural Stiffeners", "Structural Trusses",
            
            // ── MEP ──
            "Pipes", "Pipe Fittings", "Pipe Accessories", "Pipe Insulations",
            "Ducts", "Duct Fittings", "Duct Accessories", "Duct Insulations",
            "Duct Linings", "Flex Ducts", "Flex Pipes",
            "Cable Trays", "Cable Tray Fittings", "Conduits", "Conduit Fittings",
            "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures",
            "Lighting Devices", "Communication Devices", "Data Devices",
            "Fire Alarm Devices", "Nurse Call Devices", "Security Devices",
            "Telephone Devices", "Mechanical Equipment", "Plumbing Fixtures",
            "Sprinklers", "Air Terminals",
            
            // ── Site & Infrastructure ──
            "Site", "Roads", "Pads",
            
            // ── Annotation & Tags ──
            "Grids", "Levels", "Reference Planes", "Scope Boxes",
            "Matchline", "Views", "Sheets", "Viewports",
            
            // ── Schedule / Tags ──
            "Assemblies", "Parts",
            
            // ── Family/System Misc ──
            "Specialty Equipment", "Materials", "Project Information",
            "Revision Clouds", "Detail Items", "Model Lines", "Model Text",
            "Filled Region",
            
            // ── Space & Zone ──
            "Spaces", "Zones", "HVAC Zones"
        };

        // ── Category groups for quick-select ──
        private static readonly Dictionary<string, string[]> CategoryGroups = new Dictionary<string, string[]>
        {
            { "Architectural", new[] { "Walls", "Doors", "Windows", "Rooms", "Areas", "Floors", "Ceilings", "Roofs", "Stairs", "Railings", "Ramps", "Curtain Walls", "Curtain Panels", "Curtain Wall Mullions", "Columns", "Generic Models", "Furniture", "Furniture Systems", "Casework" } },
            { "Structural", new[] { "Structural Columns", "Structural Framing", "Structural Foundations", "Structural Connections", "Structural Rebar", "Structural Fabric Areas", "Structural Beam Systems", "Structural Trusses" } },
            { "MEP", new[] { "Pipes", "Pipe Fittings", "Pipe Accessories", "Ducts", "Duct Fittings", "Duct Accessories", "Cable Trays", "Conduits", "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Sprinklers", "Air Terminals" } },
            { "Annotation", new[] { "Grids", "Levels", "Reference Planes", "Scope Boxes", "Views", "Sheets", "Viewports" } }
        };

        public CreateParameterWindow()
        {
            Title = "➕ Create Parameter";
            Width = 600; Height = 680; MinWidth = 520; MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = MkH("➕ Create Parameter", "Create a new project or shared parameter on categories");
            Grid.SetRow(hdr, 0); mg.Children.Add(hdr);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20, 10, 20, 10) };
            var c = new StackPanel();

            // ── Parameter Name ──
            c.Children.Add(DarkTheme.MakeLabel("Parameter Name *"));
            _paramName = DarkTheme.MakeTextBox("Room_Code"); _paramName.Margin = new Thickness(0, 0, 0, 12); c.Children.Add(_paramName);
            c.Children.Add(DarkTheme.MakeSeparator());

            // ── Shared / Project toggle ──
            var paramKindRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            c.Children.Add(DarkTheme.MakeLabel("Parameter Kind"));
            _rbProject = new RadioButton
            {
                Content = "Project Parameter",
                GroupName = "paramKind",
                IsChecked = true,
                Foreground = DarkTheme.FgLight,
                FontSize = 12,
                Margin = new Thickness(0, 0, 24, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _rbShared = new RadioButton
            {
                Content = "Shared Parameter",
                GroupName = "paramKind",
                Foreground = DarkTheme.FgLight,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            paramKindRow.Children.Add(_rbProject);
            paramKindRow.Children.Add(_rbShared);
            c.Children.Add(paramKindRow);
            c.Children.Add(DarkTheme.MakeSeparator());

            // ── Type + Binding row ──
            var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lc = new StackPanel();
            lc.Children.Add(DarkTheme.MakeLabel("Parameter Type"));
            _paramType = DarkTheme.MakeComboBox(new[] { "Text", "Integer", "Number", "Length", "Area", "Volume", "Angle", "YesNo", "URL" }, "Text");
            lc.Children.Add(_paramType);
            Grid.SetColumn(lc, 0); row.Children.Add(lc);

            var rc = new StackPanel();
            rc.Children.Add(DarkTheme.MakeLabel("Binding"));
            var bindPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _rbInstance = new RadioButton { Content = "Instance", GroupName = "bind", IsChecked = true, Foreground = DarkTheme.FgLight, Margin = new Thickness(0, 0, 16, 0) };
            _rbType = new RadioButton { Content = "Type", GroupName = "bind", Foreground = DarkTheme.FgLight };
            bindPanel.Children.Add(_rbInstance); bindPanel.Children.Add(_rbType);
            rc.Children.Add(bindPanel);
            Grid.SetColumn(rc, 2); row.Children.Add(rc);
            c.Children.Add(row);

            c.Children.Add(DarkTheme.MakeLabel("Parameter Group"));
            _paramGroup = DarkTheme.MakeComboBox(new[] { "Identity Data", "Construction", "Structural", "Mechanical", "Electrical", "Plumbing", "Other" }, "Identity Data");
            _paramGroup.Margin = new Thickness(0, 0, 0, 12); c.Children.Add(_paramGroup);

            c.Children.Add(DarkTheme.MakeSeparator());

            // ── Apply to Categories ──
            c.Children.Add(DarkTheme.MakeLabel("Apply to Categories *"));

            // Search box for categories
            _searchBox = DarkTheme.MakeTextBox(placeholder: "🔍 Search categories...");
            _searchBox.Margin = new Thickness(0, 0, 0, 6);
            _searchBox.TextChanged += (s, e) => FilterCategories();
            c.Children.Add(_searchBox);

            // Quick-select group buttons
            var groupRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var grp in CategoryGroups)
            {
                var groupName = grp.Key;
                var groupCats = grp.Value;
                var btn = MkSmall(groupName);
                btn.Click += (s, e) =>
                {
                    for (int i = 0; i < _categoryCbs.Count; i++)
                    {
                        if (groupCats.Contains(_categoryNames[i]))
                            _categoryCbs[i].IsChecked = true;
                    }
                };
                groupRow.Children.Add(btn);
            }
            c.Children.Add(groupRow);

            // Category list
            var catBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = new CornerRadius(6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 200
            };
            var cs = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _catPanel = new StackPanel();

            foreach (var cat in AllCategories)
            {
                var cb = DarkTheme.MakeCheckBox(cat, false);
                cb.Margin = new Thickness(8, 3, 8, 3);
                cb.Checked += (s, e) => UpdateStatus();
                cb.Unchecked += (s, e) => UpdateStatus();
                _categoryCbs.Add(cb);
                _categoryNames.Add(cat);
                _catPanel.Children.Add(cb);
            }

            cs.Content = _catPanel; catBorder.Child = cs; c.Children.Add(catBorder);

            // All / None + status
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            var allBtn = MkSmall("☑ All"); allBtn.Click += (s, e) => { foreach (var cb in _categoryCbs) cb.IsChecked = true; };
            var noneBtn = MkSmall("☐ None"); noneBtn.Click += (s, e) => { foreach (var cb in _categoryCbs) cb.IsChecked = false; };
            _statusText = new TextBlock { FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            btnRow.Children.Add(allBtn); btnRow.Children.Add(noneBtn); btnRow.Children.Add(_statusText);
            c.Children.Add(btnRow);

            c.Children.Add(DarkTheme.MakeSeparator());
            _addTooltip = DarkTheme.MakeCheckBox("Add tooltip description", true); _addTooltip.Margin = new Thickness(0, 6, 0, 4); c.Children.Add(_addTooltip);
            _tooltipBox = DarkTheme.MakeTextBox("Parameter for room coding system"); c.Children.Add(_tooltipBox);

            scroll.Content = c;
            Grid.SetRow(scroll, 1); mg.Children.Add(scroll);

            var ft = new Border { Background = DarkTheme.BgFooter, Padding = new Thickness(20, 12, 20, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 1, 0, 0) };
            Button cb2, createBtn; var bp = DarkTheme.MakeButtonPanel("Create ▶", out cb2, out createBtn);
            cb2.Click += (s, e) => Close(); createBtn.Click += CreateBtn_Click;
            ft.Child = bp; Grid.SetRow(ft, 2); mg.Children.Add(ft);
            Content = mg; UpdateStatus();
        }

        private void FilterCategories()
        {
            var query = _searchBox?.Text?.ToLowerInvariant() ?? "";
            for (int i = 0; i < _categoryCbs.Count; i++)
            {
                _categoryCbs[i].Visibility = string.IsNullOrEmpty(query) || _categoryNames[i].ToLowerInvariant().Contains(query)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateStatus()
        {
            int count = 0; foreach (var cb in _categoryCbs) if (cb.IsChecked == true) count++;
            if (_statusText != null) _statusText.Text = $"{count} of {_categoryCbs.Count} categories selected";
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder("Please run create_project_parameter");
            var parts = new List<string>();
            parts.Add($"name: {_paramName?.Text}");

            // Parameter kind
            var kind = _rbShared?.IsChecked == true ? "shared" : "project";
            parts.Add($"parameterKind: {kind}");

            var cats = new List<string>();
            for (int i = 0; i < _categoryCbs.Count; i++)
                if (_categoryCbs[i].IsChecked == true) cats.Add(_categoryNames[i]);
            parts.Add($"categories: {string.Join(", ", cats)}");
            parts.Add($"type: {(_paramType.SelectedItem as ComboBoxItem)?.Content}");
            parts.Add($"isInstance: {_rbInstance.IsChecked}");
            parts.Add($"group: {(_paramGroup.SelectedItem as ComboBoxItem)?.Content}");

            if (_addTooltip?.IsChecked == true && !string.IsNullOrWhiteSpace(_tooltipBox?.Text))
                parts.Add($"description: {_tooltipBox.Text}");

            if (parts.Count > 0) { sb.Append(" with "); sb.Append(string.Join(", ", parts)); }
            Close();
            DirectExecutor.RunAsync("create_project_parameter", DirectExecutor.Params(
                ("name", _paramName?.Text),
                ("parameterKind", kind),
                ("categories", string.Join(", ", cats)),
                ("type", (_paramType.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("isInstance", _rbInstance.IsChecked == true ? "true" : "false"),
                ("group", (_paramGroup.SelectedItem as ComboBoxItem)?.Content?.ToString()),
                ("description", _addTooltip?.IsChecked == true ? _tooltipBox?.Text : null)
            ), "Create Parameter");
        }

        static Border MkH(string t, string s) { var h = new Border { Background = DarkTheme.BgHeader, Padding = new Thickness(24, 12, 24, 12), BorderBrush = DarkTheme.BorderDim, BorderThickness = new Thickness(0, 0, 0, 1) }; var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = t, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White }); sp.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = DarkTheme.FgDim, Margin = new Thickness(0, 2, 0, 0) }); h.Child = sp; return h; }
        static Button MkSmall(string t) { var b = new Button { Content = t, Background = DarkTheme.BgCancel, Foreground = DarkTheme.FgLight, BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0) }; b.MouseEnter += (s, e) => b.Background = DarkTheme.BgCancelHover; b.MouseLeave += (s, e) => b.Background = DarkTheme.BgCancel; return b; }
    }
}
