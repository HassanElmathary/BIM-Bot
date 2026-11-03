using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BIMBotNavisPlugin.Dashboard.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// Turns a DashboardConfig into a Power BI Report/Layout (legacy format: nested config and
    /// filters are stringified JSON — opens in every Power BI Desktop version).
    ///
    /// Every container it writes is named "bimbot…": that tag is how the update flow tells
    /// BIM-Bot visuals from visuals a user added by hand in Power BI.
    ///
    /// Auto layout on a 1280×720 page (visuals with an explicit position keep it):
    ///   header  title at left, up to 4 KPI cards at right
    ///   viewer  left 760×648 when the page shows the 3D viewer, everything else stacks in the
    ///           472-wide right column (slicers 56 high, the rest share what is left)
    ///   no viewer: slicers in one row, the rest in a two-column grid
    /// Pages grow taller when their visuals do not fit.
    /// </summary>
    public static class LayoutBuilder
    {
        public const string NamePrefix = "bimbot";
        public const string VisualGuid = NavisPbitGenerator.VisualGuid;

        private const int PageW = 1280, PageH = 720, Margin = 16, Gap = 8, HeaderH = 48, SlicerH = 56;
        private const string PageBg = "#0D0D17", PanelBg = "#16162A", Text = "#E8EAF0", Muted = "#A8ABBE", Accent = "#7C6FF0";
        private static readonly string[] ClashTables = { "Clashes", "ClashElements" };

        public static string Build(DashboardConfig cfg, string projectName, bool includeClashes)
        {
            var sections = new JArray();
            int ordinal = 0;
            foreach (var page in cfg.Pages)
            {
                var visuals = page.Visuals.Where(v => includeClashes || !UsesTables(v, ClashTables)).ToList();
                bool viewer = cfg.Viewer.Enabled && page.ShowViewer;
                if (!viewer && visuals.All(v => v.Type == "text")) continue;
                sections.Add(BuildSection(cfg, page, visuals, viewer, projectName, ordinal++));
            }
            if (sections.Count == 0) throw new Exception("The dashboard has no visuals to export. Add visuals or enable the 3D viewer.");

            var layout = new JObject
            {
                ["id"] = 0,
                ["resourcePackages"] = new JArray(new JObject
                {
                    ["resourcePackage"] = new JObject
                    {
                        ["name"] = VisualGuid, ["type"] = 0, ["disabled"] = false,
                        ["items"] = new JArray(new JObject { ["name"] = VisualGuid + ".pbiviz.json", ["path"] = VisualGuid + ".pbiviz.json", ["type"] = 5 }),
                    },
                }),
                ["sections"] = sections,
                ["config"] = Str(new JObject
                {
                    ["version"] = "5.43",
                    ["activeSectionIndex"] = 0,
                    ["defaultDrillFilterOtherVisuals"] = true,
                    ["settings"] = new JObject
                    {
                        ["useStylableVisualContainerHeader"] = true, ["exportDataMode"] = 1,
                        ["useNewFilterPaneExperience"] = true, ["allowChangeFilterTypes"] = true,
                    },
                    ["objects"] = new JObject { ["outspacePane"] = Objs(new JObject { ["expanded"] = Lit("false") }) },
                }),
                ["layoutOptimization"] = 0,
                ["pods"] = new JArray(),
            };
            return layout.ToString(Formatting.None);
        }

        private static bool UsesTables(VisualSpec v, string[] tables) =>
            v.Fields.Values.SelectMany(f => f).Any(f => tables.Any(t => f.StartsWith(t + ".", StringComparison.OrdinalIgnoreCase)));

        // ── placement ──

        private sealed class Placed { public VisualSpec? Spec; public bool IsViewer; public int X, Y, W, H; }

        private static JObject BuildSection(DashboardConfig cfg, PageSpec page, List<VisualSpec> visuals, bool viewer, string projectName, int ordinal)
        {
            var placed = new List<Placed>();
            var auto = visuals.Where(v => !v.HasExplicitPosition).ToList();
            foreach (var v in visuals.Where(v => v.HasExplicitPosition))
                placed.Add(new Placed { Spec = v, X = v.X!.Value, Y = v.Y!.Value, W = v.Width!.Value, H = v.Height!.Value });

            var texts = auto.Where(v => v.Type == "text").ToList();
            var cards = auto.Where(v => VisualCatalog.IsCard(v.Type)).ToList();
            var slicers = auto.Where(v => v.Type == "slicer").ToList();
            var others = auto.Where(v => v.Type != "text" && v.Type != "slicer" && !VisualCatalog.IsCard(v.Type)).ToList();

            // header
            var headerCards = cards.Take(4).ToList();
            others.InsertRange(0, cards.Skip(4));
            int cardsW = headerCards.Count == 0 ? 0 : headerCards.Count * 160 + (headerCards.Count - 1) * Gap;
            if (texts.Count > 0) placed.Add(new Placed { Spec = texts[0], X = Margin, Y = 8, W = PageW - 2 * Margin - cardsW - Gap, H = HeaderH });
            others.AddRange(texts.Skip(1));
            int cx = PageW - Margin - cardsW;
            foreach (var c in headerCards) { placed.Add(new Placed { Spec = c, X = cx, Y = 8, W = 160, H = HeaderH + 8 }); cx += 160 + Gap; }

            int top = 8 + HeaderH + Gap + 8, bottom = PageH - Margin;
            int pageH = PageH;
            if (viewer)
            {
                int vw = 760;
                placed.Add(new Placed { IsViewer = true, X = Margin, Y = top, W = vw, H = bottom - top });
                int colX = Margin + vw + Gap * 2, colW = PageW - Margin - colX, y = top;
                foreach (var s in slicers) { placed.Add(new Placed { Spec = s, X = colX, Y = y, W = colW, H = SlicerH }); y += SlicerH + Gap; }
                if (others.Count > 0)
                {
                    int each = Math.Max(140, (bottom - y - (others.Count - 1) * Gap) / others.Count);
                    foreach (var o in others) { placed.Add(new Placed { Spec = o, X = colX, Y = y, W = colW, H = each }); y += each + Gap; }
                }
                pageH = Math.Max(PageH, y - Gap + Margin);
            }
            else
            {
                int y = top, contentW = PageW - 2 * Margin;
                if (slicers.Count > 0)
                {
                    int w = Math.Min(300, (contentW - (slicers.Count - 1) * Gap) / slicers.Count), x = Margin;
                    foreach (var s in slicers) { placed.Add(new Placed { Spec = s, X = x, Y = y, W = w, H = SlicerH }); x += w + Gap; }
                    y += SlicerH + Gap;
                }
                if (others.Count > 0)
                {
                    int cols = others.Count == 1 ? 1 : 2, rows = (others.Count + cols - 1) / cols;
                    int w = (contentW - (cols - 1) * Gap) / cols;
                    int h = Math.Max(220, (bottom - y - (rows - 1) * Gap) / rows);
                    for (int i = 0; i < others.Count; i++)
                        placed.Add(new Placed { Spec = others[i], X = Margin + (i % cols) * (w + Gap), Y = y + (i / cols) * (h + Gap), W = w, H = h });
                    y += rows * (h + Gap);
                }
                pageH = Math.Max(PageH, y - Gap + Margin);
            }

            var containers = new JArray();
            int z = 0;
            foreach (var p in placed.OrderBy(p => p.Y).ThenBy(p => p.X))
            {
                z += 100;
                containers.Add(p.IsViewer
                    ? Container(NamePrefix + "Viewer" + Safe(page.Id), p, z, ViewerVisual(cfg.Viewer), "[]")
                    : Container(NamePrefix + Safe(p.Spec!.Id), p, z, SingleVisual(p.Spec, page, projectName), "[]"));
            }

            return new JObject
            {
                ["name"] = "ReportSection" + Safe(page.Id),
                ["displayName"] = string.IsNullOrWhiteSpace(page.Name) ? $"Page {ordinal + 1}" : page.Name,
                ["filters"] = "[]",
                ["ordinal"] = ordinal,
                ["visualContainers"] = containers,
                ["config"] = Str(new JObject
                {
                    ["objects"] = new JObject
                    {
                        ["background"] = Objs(new JObject { ["color"] = Color(PageBg), ["transparency"] = Lit("0D") }),
                        ["outspace"] = Objs(new JObject { ["color"] = Color(PageBg), ["transparency"] = Lit("0D") }),
                    },
                }),
                ["displayOption"] = 1,
                ["width"] = PageW,
                ["height"] = pageH,
            };
        }

        private static JObject Container(string name, Placed p, int z, JObject singleVisual, string filters) => new JObject
        {
            ["x"] = p.X, ["y"] = p.Y, ["z"] = z, ["width"] = p.W, ["height"] = p.H,
            ["filters"] = filters,
            ["config"] = Str(new JObject
            {
                ["name"] = name,
                ["layouts"] = new JArray(new JObject
                {
                    ["id"] = 0,
                    ["position"] = new JObject { ["x"] = p.X, ["y"] = p.Y, ["z"] = z, ["width"] = p.W, ["height"] = p.H, ["tabOrder"] = z },
                }),
                ["singleVisual"] = singleVisual,
            }),
        };

        // ── visuals ──

        private sealed class Query
        {
            public readonly JArray From = new JArray();
            public readonly JArray Select = new JArray();
            private readonly Dictionary<string, string> _alias = new Dictionary<string, string>();

            public string Add(string field, out bool isMeasure, out string table, out string name)
            {
                Parse(field, out table, out name, out isMeasure);
                if (!_alias.TryGetValue(table, out var a))
                {
                    a = "t" + _alias.Count;
                    _alias[table] = a;
                    From.Add(new JObject { ["Name"] = a, ["Entity"] = table, ["Type"] = 0 });
                }
                var queryRef = table + "." + name;
                if (Select.Any(s => (string?)s["Name"] == queryRef)) return queryRef;
                var src = new JObject { ["Expression"] = new JObject { ["SourceRef"] = new JObject { ["Source"] = a } }, ["Property"] = name };
                Select.Add(isMeasure ? new JObject { ["Measure"] = src, ["Name"] = queryRef } : new JObject { ["Column"] = src, ["Name"] = queryRef });
                return queryRef;
            }

            public JObject? Ref(string field)
            {
                Parse(field, out var table, out var name, out var isMeasure);
                if (!_alias.TryGetValue(table, out var a)) return null;
                var src = new JObject { ["Expression"] = new JObject { ["SourceRef"] = new JObject { ["Source"] = a } }, ["Property"] = name };
                return isMeasure ? new JObject { ["Measure"] = src } : new JObject { ["Column"] = src };
            }
        }

        /// <summary>"Table.Column" or "Table.[Measure]" (a bare name that is a model measure also counts).</summary>
        public static void Parse(string field, out string table, out string name, out bool isMeasure)
        {
            var dot = field.IndexOf('.');
            if (dot <= 0 || dot == field.Length - 1) throw new Exception($"Invalid field '{field}'. Use Table.Column or Table.[Measure].");
            table = field.Substring(0, dot);
            name = field.Substring(dot + 1);
            isMeasure = name.StartsWith("[") && name.EndsWith("]");
            if (isMeasure) name = name.Substring(1, name.Length - 2);
            else isMeasure = NavisDataModel.IsMeasure(table, name);
        }

        private static JObject SingleVisual(VisualSpec v, PageSpec page, string projectName)
        {
            var entry = VisualCatalog.Get(v.Type);
            if (v.Type == "text") return TextVisual(string.IsNullOrWhiteSpace(v.Title) ? $"{projectName} — {page.Name}" : v.Title.Replace("{{PROJECT_NAME}}", projectName));

            var q = new Query();
            var projections = new JObject();
            var columnProps = new JObject();
            string? firstMeasure = null;
            foreach (var role in entry.Roles)
            {
                if (!v.Fields.TryGetValue(role.Name, out var fields) || fields.Count == 0) continue;
                var refs = new JArray();
                foreach (var f in role.Multiple ? fields : fields.Take(1))
                {
                    var queryRef = q.Add(f, out var isMeasure, out _, out _);
                    var r = new JObject { ["queryRef"] = queryRef };
                    if (v.Type == "slicer") r["active"] = true;
                    refs.Add(r);
                    if (isMeasure && firstMeasure == null) firstMeasure = f;
                    if (v.Type == "slicer" && !string.IsNullOrWhiteSpace(v.Title)) columnProps[queryRef] = new JObject { ["displayName"] = v.Title };
                }
                projections[role.Name] = refs;
            }
            if (projections.Count == 0) throw new Exception($"Visual '{v.Title}' ({v.Type}) has no fields bound.");

            var query = new JObject { ["Version"] = 2, ["From"] = q.From, ["Select"] = q.Select };
            bool sortable = v.Type == "bar" || v.Type == "column" || v.Type == "pie" || v.Type == "donut";
            if (sortable && firstMeasure != null)
                query["OrderBy"] = new JArray(new JObject { ["Direction"] = 2, ["Expression"] = q.Ref(firstMeasure) });

            var sv = new JObject
            {
                ["visualType"] = entry.PowerBiType,
                ["projections"] = projections,
                ["prototypeQuery"] = query,
                ["drillFilterOtherVisuals"] = true,
            };
            if (sortable) sv["hasDefaultSort"] = true;
            if (columnProps.Count > 0) sv["columnProperties"] = columnProps;

            bool hasTitle = !string.IsNullOrWhiteSpace(v.Title) && v.Type != "slicer";
            sv["objects"] = Objects(v.Type, hasTitle);
            sv["vcObjects"] = new JObject
            {
                ["background"] = Objs(v.Type == "slicer"
                    ? new JObject { ["show"] = Lit("false") }
                    : new JObject { ["show"] = Lit("true"), ["color"] = Color(PanelBg), ["transparency"] = Lit("0D") }),
                ["title"] = Objs(hasTitle
                    ? new JObject { ["show"] = Lit("true"), ["text"] = Lit(Quote(v.Title)), ["fontColor"] = Color(Text), ["fontSize"] = Lit(VisualCatalog.IsCard(v.Type) ? "9D" : "11D") }
                    : new JObject { ["show"] = Lit("false") }),
            };
            return sv;
        }

        private static JObject Objects(string type, bool hasTitle)
        {
            switch (type)
            {
                case "card":
                    return new JObject
                    {
                        ["labels"] = Objs(new JObject { ["color"] = Color(Text), ["fontSize"] = Lit("20D") }),
                        ["categoryLabels"] = Objs(new JObject { ["show"] = Lit(hasTitle ? "false" : "true"), ["color"] = Color(Muted), ["fontSize"] = Lit("10D") }),
                    };
                case "multiRowCard":
                    return new JObject
                    {
                        ["dataLabels"] = Objs(new JObject { ["color"] = Color(Text) }),
                        ["categoryLabels"] = Objs(new JObject { ["color"] = Color(Muted) }),
                    };
                case "bar":
                case "column":
                    return new JObject
                    {
                        ["dataPoint"] = Objs(new JObject { ["defaultColor"] = Color(Accent) }),
                        ["categoryAxis"] = Objs(new JObject { ["labelColor"] = Color(Muted), ["fontSize"] = Lit("9D") }),
                        ["valueAxis"] = Objs(new JObject { ["show"] = Lit("false") }),
                        ["labels"] = Objs(new JObject { ["show"] = Lit("true"), ["color"] = Color(Text) }),
                    };
                case "pie":
                case "donut":
                    return new JObject
                    {
                        ["legend"] = Objs(new JObject { ["show"] = Lit("true"), ["labelColor"] = Color(Muted), ["position"] = Lit("'Right'") }),
                        ["labels"] = Objs(new JObject { ["color"] = Color(Text) }),
                    };
                case "table":
                    return new JObject
                    {
                        ["columnHeaders"] = Objs(new JObject { ["fontColor"] = Color(Text), ["backColor"] = Color("#22223A") }),
                        ["values"] = Objs(new JObject
                        {
                            ["fontColorPrimary"] = Color(Text), ["backColorPrimary"] = Color(PanelBg),
                            ["fontColorSecondary"] = Color(Text), ["backColorSecondary"] = Color("#1C1C33"),
                        }),
                    };
                case "slicer":
                    return new JObject
                    {
                        ["data"] = Objs(new JObject { ["mode"] = Lit("'Dropdown'") }),
                        ["header"] = Objs(new JObject { ["show"] = Lit("true"), ["fontColor"] = Color(Muted), ["textSize"] = Lit("10D") }),
                        ["items"] = Objs(new JObject { ["fontColor"] = Color(Text), ["background"] = Color(PanelBg), ["textSize"] = Lit("10D") }),
                    };
                default:
                    return new JObject();
            }
        }

        private static JObject TextVisual(string title) => new JObject
        {
            ["visualType"] = "textbox",
            ["drillFilterOtherVisuals"] = true,
            ["objects"] = new JObject
            {
                ["general"] = Objs(new JObject
                {
                    ["paragraphs"] = new JArray
                    {
                        new JObject { ["textRuns"] = new JArray(new JObject { ["value"] = title, ["textStyle"] = new JObject { ["fontWeight"] = "bold", ["fontSize"] = "18pt", ["color"] = Text } }) },
                        new JObject { ["textRuns"] = new JArray(new JObject { ["value"] = "Generated by BIM-Bot · Click the 3D model or any visual to filter", ["textStyle"] = new JObject { ["fontSize"] = "9pt", ["color"] = Muted } }) },
                    },
                }),
            },
            ["vcObjects"] = new JObject { ["background"] = Objs(new JObject { ["show"] = Lit("false") }) },
        };

        private static JObject ViewerVisual(ViewerSettings s)
        {
            var q = new Query();
            var projections = new JObject
            {
                ["elementId"] = Refs(q.Add("Geometry.ElementKey", out _, out _, out _)),
                ["chunkIndex"] = Refs(q.Add("Geometry.ChunkIndex", out _, out _, out _)),
                ["category"] = Refs(q.Add("Elements." + (string.IsNullOrWhiteSpace(s.ColorBy) ? "Category" : s.ColorBy), out _, out _, out _)),
                ["meshJson"] = Refs(q.Add("Geometry.MeshJSON", out _, out _, out _)),
            };
            // Colour channels: Min() of CategoryColors R/G/B, exactly as the Revit template binds them.
            q.Add("CategoryColors.R", out _, out _, out _); // registers the alias
            var alias = (string)q.From.Last!["Name"]!;
            q.Select.RemoveAt(q.Select.Count - 1);
            foreach (var ch in new[] { "R", "G", "B" })
            {
                var name = $"Min(CategoryColors.{ch})";
                q.Select.Add(new JObject
                {
                    ["Aggregation"] = new JObject
                    {
                        ["Expression"] = new JObject { ["Column"] = new JObject { ["Expression"] = new JObject { ["SourceRef"] = new JObject { ["Source"] = alias } }, ["Property"] = ch } },
                        ["Function"] = 3,
                    },
                    ["Name"] = name,
                });
                projections["color" + ch] = Refs(name);
            }

            return new JObject
            {
                ["visualType"] = VisualGuid,
                ["projections"] = projections,
                ["prototypeQuery"] = new JObject
                {
                    ["Version"] = 2, ["From"] = q.From, ["Select"] = q.Select,
                    ["OrderBy"] = new JArray(new JObject { ["Direction"] = 1, ["Expression"] = q.Ref("Geometry.ElementKey") }),
                },
                ["drillFilterOtherVisuals"] = true,
                ["hasDefaultSort"] = true,
                ["objects"] = new JObject
                {
                    ["rendering"] = Objs(new JObject
                    {
                        ["backgroundColor"] = Color(s.BackgroundColor),
                        ["ghostOpacity"] = Lit(Math.Max(0, Math.Min(1, s.GhostOpacity)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "D"),
                        ["hideUnselected"] = Lit(s.HideUnselected ? "true" : "false"),
                    }),
                },
                ["vcObjects"] = new JObject
                {
                    ["background"] = Objs(new JObject { ["show"] = Lit("true"), ["color"] = Color(PanelBg), ["transparency"] = Lit("0D") }),
                    ["title"] = Objs(new JObject { ["show"] = Lit("false") }),
                },
            };
        }

        // ── JSON helpers ──

        private static JArray Refs(string queryRef) => new JArray(new JObject { ["queryRef"] = queryRef });
        private static JObject Lit(string value) => new JObject { ["expr"] = new JObject { ["Literal"] = new JObject { ["Value"] = value } } };
        private static JObject Color(string hex) => new JObject { ["solid"] = new JObject { ["color"] = Lit("'" + hex + "'") } };
        private static JArray Objs(JObject properties) => new JArray(new JObject { ["properties"] = properties });
        private static string Quote(string s) => "'" + (s ?? "").Replace("'", "''") + "'";
        private static string Str(JObject o) => o.ToString(Formatting.None);
        private static string Safe(string id) => Regex.Replace(id ?? "", "[^A-Za-z0-9]", "");
    }
}
