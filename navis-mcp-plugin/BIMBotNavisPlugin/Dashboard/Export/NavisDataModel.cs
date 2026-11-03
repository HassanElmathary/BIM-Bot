using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// The Power BI data model (tables, measures, relationships) behind every Navis dashboard,
    /// and the field list the customizer offers. Column order must match NavisCsvWriter.
    ///
    ///   Clashes 1─* ClashElements *─1 Elements 1─* Geometry
    ///                                 Elements 1─* Properties
    ///                                 Elements *─1 CategoryColors
    /// All relationships filter both ways, so picking a clash row isolates its two elements in
    /// the 3D viewer, and clicking an element in the viewer filters the clash visuals.
    /// </summary>
    public static class NavisDataModel
    {
        public sealed class Column
        {
            public string Name; public string Type; // int64 | string | double
            public Column(string name, string type) { Name = name; Type = type; }
        }

        public sealed class Measure
        {
            public string Name; public string Expression; public string Format;
            public Measure(string name, string expr, string format = "#,0") { Name = name; Expression = expr; Format = format; }
        }

        public sealed class Table
        {
            public string Name = ""; public string File = ""; public bool Hidden;
            public List<Column> Columns = new List<Column>();
            public List<Measure> Measures = new List<Measure>();
        }

        private static Column S(string n) => new Column(n, "string");
        private static Column N(string n) => new Column(n, "int64");
        private static Column F(string n) => new Column(n, "double");

        private static string StatusCount(string status) =>
            $"CALCULATE(COUNTROWS('Clashes'), 'Clashes'[Status] = \"{status}\")";

        public static readonly IReadOnlyList<Table> Tables = new List<Table>
        {
            new Table
            {
                Name = "Elements", File = "Elements.csv",
                Columns = { N("ElementKey"), S("RevitElementId"), S("InstanceGuid"), S("Model"), S("Discipline"),
                            S("Category"), S("Family"), S("Type"), S("Level"), S("Name") },
                Measures =
                {
                    new Measure("Element Count", "COUNTROWS('Elements')"),
                    new Measure("Category Count", "DISTINCTCOUNT('Elements'[Category])"),
                    new Measure("Model Count", "DISTINCTCOUNT('Elements'[Model])"),
                },
            },
            new Table { Name = "Properties", File = "Properties.csv", Columns = { N("ElementKey"), S("Tab"), S("Name"), S("Value") } },
            new Table { Name = "Geometry", File = "Geometry.csv", Hidden = false, Columns = { N("ElementKey"), N("ChunkIndex"), S("MeshJSON") } },
            new Table { Name = "CategoryColors", File = "CategoryColors.csv", Columns = { S("Category"), N("R"), N("G"), N("B"), S("ColorHex") } },
            new Table
            {
                Name = "Clashes", File = "Clashes.csv",
                Columns = { S("ClashGuid"), S("Test"), S("ClashName"), S("Status"), F("DistanceM"), F("CenterX"), F("CenterY"), F("CenterZ"), S("Found") },
                Measures =
                {
                    new Measure("Clash Count", "COUNTROWS('Clashes')"),
                    new Measure("Open Clashes", "CALCULATE(COUNTROWS('Clashes'), 'Clashes'[Status] IN {\"New\", \"Active\"})"),
                    new Measure("New Clashes", StatusCount("New")),
                    new Measure("Active Clashes", StatusCount("Active")),
                    new Measure("Reviewed Clashes", StatusCount("Reviewed")),
                    new Measure("Approved Clashes", StatusCount("Approved")),
                    new Measure("Resolved Clashes", StatusCount("Resolved")),
                },
            },
            new Table { Name = "ClashElements", File = "ClashElements.csv", Hidden = true, Columns = { S("ClashGuid"), N("ElementKey"), S("Side") } },
            new Table { Name = "ModelInfo", File = "ModelInfo.csv", Hidden = true, Columns = { S("Key"), S("Value") } },
        };

        private static readonly (string from, string fromCol, string to, string toCol)[] Relationships =
        {
            ("Geometry", "ElementKey", "Elements", "ElementKey"),
            ("Properties", "ElementKey", "Elements", "ElementKey"),
            ("Elements", "Category", "CategoryColors", "Category"),
            ("ClashElements", "ElementKey", "Elements", "ElementKey"),
            ("ClashElements", "ClashGuid", "Clashes", "ClashGuid"),
        };

        /// <summary>Fields the customizer can bind: "Table.Column" and "Table.[Measure]".</summary>
        public static IEnumerable<(string field, bool isMeasure, string table)> Fields()
        {
            foreach (var t in Tables.Where(t => !t.Hidden && t.Name != "Geometry" && t.Name != "CategoryColors"))
            {
                foreach (var m in t.Measures) yield return ($"{t.Name}.[{m.Name}]", true, t.Name);
                foreach (var c in t.Columns) yield return ($"{t.Name}.{c.Name}", false, t.Name);
            }
        }

        public static bool IsMeasure(string table, string name) =>
            Tables.Any(t => t.Name == table && t.Measures.Any(m => m.Name == name));

        /// <summary>DataModelSchema JSON with every table reading its CSV from dataFolder.</summary>
        public static string BuildSchema(string dataFolder)
        {
            if (dataFolder.IndexOf('"') >= 0)
                throw new InvalidOperationException("The dashboard data folder path contains a double quote, which cannot be used in Power BI queries. Choose another output folder.");
            var folder = dataFolder.TrimEnd('\\');

            var tables = new JArray();
            foreach (var t in Tables)
            {
                var cols = new JArray(t.Columns.Select(c => new JObject
                {
                    ["name"] = c.Name, ["dataType"] = c.Type, ["sourceColumn"] = c.Name, ["summarizeBy"] = "none",
                }));
                var types = string.Join(", ", t.Columns.Select(c => $"{{\"{c.Name}\", {MType(c.Type)}}}"));
                var expr = new JArray
                {
                    "let",
                    $"    Source = Csv.Document(File.Contents(\"{folder}\\{t.File}\"),[Delimiter=\",\", Columns={t.Columns.Count}, Encoding=65001, QuoteStyle=QuoteStyle.Csv]),",
                    "    #\"Promoted Headers\" = Table.PromoteHeaders(Source, [PromoteAllScalars=true]),",
                    $"    #\"Changed Type\" = Table.TransformColumnTypes(#\"Promoted Headers\",{{{types}}}, \"en-US\")",
                    "in",
                    "    #\"Changed Type\"",
                };
                var table = new JObject
                {
                    ["name"] = t.Name,
                    ["columns"] = cols,
                    ["partitions"] = new JArray(new JObject
                    {
                        ["name"] = t.Name,
                        ["mode"] = "import",
                        ["source"] = new JObject { ["type"] = "m", ["expression"] = expr },
                    }),
                    ["annotations"] = new JArray(new JObject { ["name"] = "PBI_ResultType", ["value"] = "Table" }),
                };
                if (t.Hidden) table["isHidden"] = true;
                if (t.Measures.Count > 0)
                    table["measures"] = new JArray(t.Measures.Select(m => new JObject
                    {
                        ["name"] = m.Name, ["expression"] = m.Expression, ["formatString"] = m.Format,
                    }));
                tables.Add(table);
            }

            var rels = new JArray(Relationships.Select((r, i) => new JObject
            {
                ["name"] = $"bimbot-rel-{i + 1}",
                ["fromTable"] = r.from, ["fromColumn"] = r.fromCol,
                ["toTable"] = r.to, ["toColumn"] = r.toCol,
                ["crossFilteringBehavior"] = "bothDirections",
            }));

            var schema = new JObject
            {
                ["name"] = Guid.NewGuid().ToString(),
                ["compatibilityLevel"] = 1550,
                ["model"] = new JObject
                {
                    ["culture"] = "en-US",
                    ["dataAccessOptions"] = new JObject { ["legacyRedirects"] = true, ["returnErrorValuesAsNull"] = true },
                    ["defaultPowerBIDataSourceVersion"] = "powerBI_V3",
                    ["sourceQueryCulture"] = "en-US",
                    ["tables"] = tables,
                    ["relationships"] = rels,
                    ["annotations"] = new JArray
                    {
                        new JObject { ["name"] = "PBI_QueryOrder", ["value"] = JsonConvert.SerializeObject(Tables.Select(t => t.Name)) },
                        new JObject { ["name"] = "__PBI_TimeIntelligenceEnabled", ["value"] = "0" },
                    },
                },
            };
            return schema.ToString(Formatting.Indented);
        }

        private static string MType(string t) => t == "int64" ? "Int64.Type" : t == "double" ? "type number" : "type text";
    }
}
