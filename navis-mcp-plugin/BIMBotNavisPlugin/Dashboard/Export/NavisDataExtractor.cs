using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using BIMBotNavisPlugin.Core;
using BIMBotNavisPlugin.Dashboard.Config;
using X = BIMBotNavisPlugin.Core.NavisCommandExecutor;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// Reads elements, properties and clash results from the open federated model.
    ///
    /// What counts as an element (same rule walking down from a model root and walking up
    /// from a clashed item, so both always land on the same node):
    ///   the TOP-MOST node below a file node that is not a file / Revit category / Revit level /
    ///   family / type collection and either carries a Revit Element Id, is a composite object,
    ///   or is a bare geometry leaf. Children of an element are its geometry.
    /// Must run on the Navisworks main thread.
    /// </summary>
    public sealed class NavisDataExtractor
    {
        private readonly Document _doc;
        private readonly ExportOptions _options;
        private readonly ElementKeyMap _keys;
        private readonly IProgressSink _progress;
        private readonly HashSet<string> _tabs;

        public NavisDataExtractor(Document doc, ExportOptions options, ElementKeyMap keys, IProgressSink progress)
        {
            _doc = doc;
            _options = options;
            _keys = keys;
            _progress = progress;
            _tabs = new HashSet<string>(options.PropertyTabs ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public ExportData Extract(string projectName)
        {
            var data = new ExportData
            {
                ProjectName = projectName,
                Units = _doc.Units.ToString(),
                ToMeters = UnitConversion.ScaleFactor(_doc.Units, Units.Meters),
            };
            foreach (Model m in _doc.Models)
                data.ModelFiles.Add(ModelFileName(m));

            var seen = new HashSet<int>();
            if (string.Equals(_options.Scope, "selection", StringComparison.OrdinalIgnoreCase))
            {
                var scope = ScopeItems();
                if (scope.Count == 0)
                    throw new Exception("Data scope is 'selection' but nothing is selected. Select items (or set a selection set) first.");
                int i = 0;
                foreach (var item in scope)
                {
                    if (_progress.Report("Reading selected elements", ++i, scope.Count)) throw new OperationCanceledException();
                    var element = ElementOf(item);
                    if (element != null) AddElement(data, element, seen);
                    else Walk(data, item, ContextOf(item), seen);
                }
            }
            else
            {
                int i = 0, n = _doc.Models.Count;
                foreach (Model m in _doc.Models)
                {
                    if (_progress.Report($"Reading {ModelFileName(m)}", ++i, n)) throw new OperationCanceledException();
                    if (m.RootItem != null)
                        Walk(data, m.RootItem, new Ctx { Model = ModelFileName(m) }, seen);
                }
            }

            if (_options.IncludeClashes) ExtractClashes(data);
            AssignColors(data);
            return data;
        }

        // ── element discovery ──

        private sealed class Ctx
        {
            public string Model = "";
            public string Level = "";
            public string Category = "";
            public Ctx With() => (Ctx)MemberwiseClone();
        }

        private static bool IsFile(string t) => t == "LcOaPartition";
        private static bool IsStructural(string t) => t == "LcOaPartition" || t == "LcRevitCollection" || t == "LcRevitLayer";

        private static bool QualifiesAsElement(ModelItem m, string itype)
        {
            if (IsStructural(itype)) return false;
            if (X.DashRevitId(m) != "") return true;
            if (m.IsComposite) return true;
            return m.HasGeometry && m.Children.Count() == 0;
        }

        private void Walk(ExportData data, ModelItem node, Ctx ctx, HashSet<int> seen)
        {
            var t = X.DashInternalType(node);
            if (IsFile(t)) { ctx = ctx.With(); ctx.Model = PartitionName(node, ctx.Model); }
            else if (t == "LcRevitLayer") { ctx = ctx.With(); ctx.Level = node.DisplayName ?? ""; }
            else if (t == "LcRevitCollection" && ctx.Category == "") { ctx = ctx.With(); ctx.Category = node.DisplayName ?? ""; }

            if (QualifiesAsElement(node, t))
            {
                AddElement(data, node, seen, ctx);
                return;
            }
            foreach (ModelItem child in node.Children) Walk(data, child, ctx, seen);
        }

        /// <summary>Top-most element node at or above item (below its file node), or null.</summary>
        public static ModelItem? ElementOf(ModelItem item)
        {
            ModelItem? found = null;
            for (var cur = item; cur != null; cur = cur.Parent)
            {
                var t = X.DashInternalType(cur);
                if (IsFile(t)) break;
                if (QualifiesAsElement(cur, t)) found = cur;
            }
            return found;
        }

        private Ctx ContextOf(ModelItem item)
        {
            var ctx = new Ctx();
            var chain = new List<ModelItem>();
            for (var cur = item; cur != null; cur = cur.Parent) chain.Add(cur);
            chain.Reverse(); // root → item
            foreach (var n in chain)
            {
                var t = X.DashInternalType(n);
                if (IsFile(t)) ctx.Model = PartitionName(n, ctx.Model);
                else if (t == "LcRevitLayer") ctx.Level = n.DisplayName ?? "";
                else if (t == "LcRevitCollection" && ctx.Category == "") ctx.Category = n.DisplayName ?? "";
            }
            if (ctx.Model == "") ctx.Model = ModelOfRoot(item);
            return ctx;
        }

        private void AddElement(ExportData data, ModelItem element, HashSet<int> seen, Ctx? ctx = null)
        {
            ctx ??= ContextOf(element);
            var rid = X.DashRevitId(element);
            var key = _keys.KeyFor(ElementKeyMap.Identity(ctx.Model, rid, element.InstanceGuid, TreePath(element)));
            if (!seen.Add(key)) return;

            var parent = element.Parent;
            var grand = parent?.Parent;
            bool revitTree = parent != null && X.DashInternalType(parent) == "LcRevitCollection";

            var row = new ElementRow
            {
                Key = key,
                Item = element,
                RevitElementId = rid,
                InstanceGuid = element.InstanceGuid == Guid.Empty ? "" : element.InstanceGuid.ToString(),
                Model = ctx.Model,
                Discipline = DisciplineOf(ctx.Model),
                Name = element.DisplayName ?? "",
                Category = First(element, ("Element", "Category")),
                Family = First(element, ("Element", "Family"), ("Element", "Family Name")),
                Type = First(element, ("Element", "Type"), ("Element", "Type Name")),
                Level = First(element, ("Element", "Level"), ("Element", "Base Constraint"),
                              ("Element", "Reference Level"), ("Element", "Schedule Level")),
            };
            if (row.Category == "") row.Category = ctx.Category != "" ? ctx.Category : (element.ClassDisplayName ?? "");
            if (row.Type == "" && revitTree) row.Type = parent!.DisplayName ?? "";
            if (row.Family == "" && revitTree && grand != null && X.DashInternalType(grand) == "LcRevitCollection")
                row.Family = grand.DisplayName ?? "";
            if (row.Level == "") row.Level = ctx.Level;
            data.Elements.Add(row);

            AddProperties(data, element, key);
        }

        private void AddProperties(ExportData data, ModelItem element, int key)
        {
            int count = 0, max = Math.Max(1, _options.MaxPropertiesPerElement);
            foreach (PropertyCategory pc in element.PropertyCategories)
            {
                var tab = pc.DisplayName ?? "";
                if (_tabs.Count > 0 && !_tabs.Contains(tab)) continue;
                foreach (DataProperty dp in pc.Properties)
                {
                    var v = ValueText(dp.Value);
                    if (v == "") continue;
                    data.Properties.Add(new PropertyRow { Key = key, Tab = tab, Name = dp.DisplayName ?? "", Value = v });
                    if (++count >= max) return;
                }
            }
        }

        // ── clashes ──

        private void ExtractClashes(ExportData data)
        {
            DocumentClashTests clash;
            try { clash = X.DashClashData(_doc); }
            catch (Exception ex) { NavisLogger.Log("Dashboard: clash data unavailable — " + ex.Message); return; }

            var tests = X.DashAllTests(clash);
            int ti = 0;
            foreach (var test in tests)
            {
                if (_progress.Report($"Reading clash test {test.DisplayName}", ++ti, tests.Count)) throw new OperationCanceledException();
                var results = new List<ClashResult>();
                X.DashCollectResults(test.Children, results);
                foreach (var r in results)
                {
                    var c = r.Center;
                    data.Clashes.Add(new ClashRow
                    {
                        ClashGuid = r.Guid.ToString(),
                        Test = test.DisplayName ?? "",
                        ClashName = r.DisplayName ?? "",
                        Status = r.Status.ToString(),
                        DistanceM = Math.Round(r.Distance * data.ToMeters, 4),
                        CenterX = c == null ? 0 : Math.Round(c.X * data.ToMeters, 3),
                        CenterY = c == null ? 0 : Math.Round(c.Y * data.ToMeters, 3),
                        CenterZ = c == null ? 0 : Math.Round(c.Z * data.ToMeters, 3),
                        Found = r.CreatedTime.HasValue ? r.CreatedTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "",
                        KeyA = KeyOfClashItem(r.Item1),
                        KeyB = KeyOfClashItem(r.Item2),
                    });
                }
            }
        }

        private int KeyOfClashItem(ModelItem? item)
        {
            if (item == null) return 0;
            var element = ElementOf(item) ?? item;
            var ctx = ContextOf(element);
            return _keys.KeyFor(ElementKeyMap.Identity(ctx.Model, X.DashRevitId(element), element.InstanceGuid, TreePath(element)));
        }

        // ── helpers ──

        private List<ModelItem> ScopeItems()
        {
            if (!string.IsNullOrWhiteSpace(_options.SelectionSet))
            {
                var set = X.DashFindSet(_doc, _options.SelectionSet);
                if (set.HasExplicitModelItems) return set.ExplicitModelItems.Cast<ModelItem>().ToList();
                if (set.HasSearch) return set.Search.FindAll(_doc, false).Cast<ModelItem>().ToList();
                return new List<ModelItem>();
            }
            return _doc.CurrentSelection.SelectedItems.Cast<ModelItem>().ToList();
        }

        private static string First(ModelItem m, params (string tab, string prop)[] candidates)
        {
            foreach (var (tab, prop) in candidates)
            {
                var v = X.DashPropText(m, tab, prop);
                if (v != "") return v;
            }
            return "";
        }

        private static string PartitionName(ModelItem partition, string fallback)
        {
            var name = partition.DisplayName ?? "";
            if (name == "")
            {
                var src = X.DashPropText(partition, "Item", "Source File Name");
                if (src != "") name = Path.GetFileName(src);
            }
            return name == "" ? fallback : name;
        }

        private string ModelOfRoot(ModelItem item)
        {
            var root = item;
            while (root.Parent != null) root = root.Parent;
            foreach (Model m in _doc.Models)
                if (m.RootItem != null && m.RootItem.Equals(root)) return ModelFileName(m);
            return "";
        }

        private static string ModelFileName(Model m)
        {
            var f = !string.IsNullOrWhiteSpace(m.SourceFileName) ? m.SourceFileName : m.FileName;
            return string.IsNullOrWhiteSpace(f) ? (m.RootItem?.DisplayName ?? "") : Path.GetFileName(f);
        }

        /// <summary>Child-index path below the model root ("3/0/12") — the identity of last resort.</summary>
        private static string TreePath(ModelItem item)
        {
            var parts = new List<string>();
            for (var cur = item; cur.Parent != null; cur = cur.Parent)
            {
                int i = 0, idx = -1;
                foreach (ModelItem c in cur.Parent.Children) { if (c.Equals(cur)) { idx = i; break; } i++; }
                parts.Add(idx.ToString(CultureInfo.InvariantCulture));
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static readonly (string token, string code)[] DisciplineTokens =
        {
            ("struct", "STR"), ("archi", "ARC"), ("facade", "FAC"), ("hvac", "HVAC"), ("mech", "MEC"),
            ("plumb", "PLU"), ("drain", "PLU"), ("electr", "ELE"), ("light", "ELE"), ("fire", "FPR"),
            ("sprink", "FPR"), ("site", "SITE"), ("civil", "CIV"), ("landsc", "LAN"), ("interior", "INT"),
        };

        /// <summary>Discipline code from the model file name (keyword match), else the file name stem.</summary>
        public static string DisciplineOf(string modelFile)
        {
            var lower = (modelFile ?? "").ToLowerInvariant();
            foreach (var (token, code) in DisciplineTokens)
                if (lower.Contains(token)) return code;
            return Path.GetFileNameWithoutExtension(modelFile ?? "");
        }

        /// <summary>Readable text of a property value in any Navisworks variant type; "" when empty.</summary>
        public static string ValueText(VariantData? v)
        {
            if (v == null) return "";
            try
            {
                if (v.IsDisplayString) return v.ToDisplayString() ?? "";
                if (v.IsInt32) return v.ToInt32().ToString(CultureInfo.InvariantCulture);
                if (v.IsDouble) return v.ToDouble().ToString("0.####", CultureInfo.InvariantCulture);
                if (v.IsDoubleLength) return v.ToDoubleLength().ToString("0.####", CultureInfo.InvariantCulture);
                if (v.IsDoubleArea) return v.ToDoubleArea().ToString("0.####", CultureInfo.InvariantCulture);
                if (v.IsDoubleVolume) return v.ToDoubleVolume().ToString("0.####", CultureInfo.InvariantCulture);
                if (v.IsDoubleAngle) return v.ToDoubleAngle().ToString("0.####", CultureInfo.InvariantCulture);
                if (v.IsBoolean) return v.ToBoolean() ? "Yes" : "No";
                if (v.IsDateTime) return v.ToDateTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (v.IsIdentifierString) return v.ToIdentifierString() ?? "";
                if (v.IsNamedConstant) return v.ToNamedConstant()?.DisplayName ?? "";
            }
            catch { }
            return "";
        }

        private static readonly (int R, int G, int B)[] Palette =
        {
            (124, 111, 240), (72, 187, 174), (240, 160, 70), (230, 95, 110), (90, 160, 230),
            (160, 200, 90), (200, 120, 200), (240, 210, 90), (110, 130, 160), (60, 200, 120),
            (220, 140, 120), (130, 90, 200), (90, 210, 220), (200, 170, 140), (170, 170, 180),
            (250, 120, 60), (120, 180, 140), (180, 100, 140), (100, 120, 220), (210, 210, 120),
        };

        private static void AssignColors(ExportData data)
        {
            var cats = data.Elements.Select(e => e.Category).Distinct(StringComparer.OrdinalIgnoreCase)
                           .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < cats.Count; i++) data.CategoryColors[cats[i]] = Palette[i % Palette.Length];
        }
    }
}
