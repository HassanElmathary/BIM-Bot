using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMBotNavisPlugin.Dashboard
{
    /// <summary>
    /// The visuals a dashboard can contain. Key = what VisualSpec.Type stores; PowerBiType = the
    /// report visualType written into the layout. Roles say which fields each visual binds.
    /// </summary>
    public static class VisualCatalog
    {
        public enum FieldKind { Column, Measure, Any }

        public sealed class Role
        {
            public string Name { get; }
            public string DisplayName { get; }
            public FieldKind Kind { get; }
            public bool Multiple { get; }
            public Role(string name, string display, FieldKind kind, bool multiple = false)
            { Name = name; DisplayName = display; Kind = kind; Multiple = multiple; }
        }

        public sealed class Entry
        {
            public string Key { get; }
            public string DisplayName { get; }
            public string PowerBiType { get; }
            public string Group { get; }
            public IReadOnlyList<Role> Roles { get; }
            public Entry(string key, string display, string pbiType, string group, params Role[] roles)
            { Key = key; DisplayName = display; PowerBiType = pbiType; Group = group; Roles = roles; }
        }

        public const string Viewer3D = "viewer3d";

        public static readonly IReadOnlyList<Entry> Entries = new List<Entry>
        {
            new Entry("text", "Title / text", "textbox", "Layout"),
            new Entry("card", "KPI card", "card", "Cards", new Role("Values", "Value", FieldKind.Measure)),
            new Entry("multiRowCard", "Multi-row card", "multiRowCard", "Cards", new Role("Values", "Values", FieldKind.Any, true)),
            new Entry("bar", "Bar chart", "clusteredBarChart", "Charts",
                new Role("Category", "Axis", FieldKind.Column), new Role("Y", "Value", FieldKind.Measure, true)),
            new Entry("column", "Column chart", "clusteredColumnChart", "Charts",
                new Role("Category", "Axis", FieldKind.Column), new Role("Y", "Value", FieldKind.Measure, true)),
            new Entry("pie", "Pie chart", "pieChart", "Charts",
                new Role("Category", "Legend", FieldKind.Column), new Role("Y", "Value", FieldKind.Measure)),
            new Entry("donut", "Donut chart", "donutChart", "Charts",
                new Role("Category", "Legend", FieldKind.Column), new Role("Y", "Value", FieldKind.Measure)),
            new Entry("table", "Data table", "tableEx", "Tables", new Role("Values", "Columns", FieldKind.Any, true)),
            new Entry("slicer", "Slicer", "slicer", "Filters", new Role("Values", "Field", FieldKind.Column)),
        };

        public static Entry Get(string key) =>
            Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"Unknown visual type '{key}'. Valid: {string.Join(", ", Entries.Select(e => e.Key))}.");

        public static bool IsCard(string key) => key == "card" || key == "multiRowCard";
    }
}
