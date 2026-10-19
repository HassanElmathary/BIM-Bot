using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Revit bridge: exposes clash items with their Revit Element IDs and source file,
    /// and finds Navisworks items from Revit Element IDs.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        private const int RevitIdSearchDepth = 4;

        /// <summary>Item > Internal Type, e.g. LcOaPartition (file), LcRevitCollection (category), LcRevitLayer (level).</summary>
        private static string InternalType(ModelItem m) => PropText(m, "Item", "Internal Type");

        /// <summary>File, Revit category or Revit level node: never the element itself.</summary>
        private static bool IsStructuralNode(ModelItem m)
        {
            var t = InternalType(m);
            return t == "LcOaPartition" || t == "LcRevitCollection" || t == "LcRevitLayer";
        }

        /// <summary>
        /// The appended file an item comes from: the NEAREST ancestor (or self) that is a file node
        /// (LcOaPartition) — in a federated NWD that is the discipline NWC, not the NWD itself.
        /// Returns its display name and, when known, the original Revit file name (Source File Name).
        /// Falls back to the model's file when no file node is found.
        /// </summary>
        private static (string file, string revitFile) SourceInfoOf(Document doc, ModelItem item)
        {
            for (var cur = item; cur != null; cur = cur.Parent)
            {
                if (InternalType(cur) != "LcOaPartition") continue;
                var src = PropText(cur, "Item", "Source File Name");
                var file = cur.DisplayName ?? "";
                if (file == "" && src != "") file = Path.GetFileName(src);
                var rvt = src.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(src) : "";
                return (file, rvt);
            }
            return (SourceFileOf(doc, item), "");
        }

        /// <summary>True when the item's NWC or original RVT names the same model as src.</summary>
        private static bool MatchesSource(Document doc, ModelItem m, string src)
        {
            var (file, rvt) = SourceInfoOf(doc, m);
            return SameModel(file, src) || (rvt != "" && SameModel(rvt, src));
        }

        /// <summary>Walks up from item until a node carries Element > Id. Returns (id, carrierItem) or ("", item).</summary>
        private static (string id, ModelItem carrier) RevitIdUp(ModelItem item)
        {
            var cur = item;
            for (int i = 0; i <= RevitIdSearchDepth && cur != null; i++)
            {
                if (i > 0 && IsStructuralNode(cur)) break; // never take a level's / category's / file's id
                var id = PropText(cur, "Element", "Id");
                if (id == "") id = PropText(cur, "Element ID", "Value");
                if (id != "") return (id, cur);
                cur = cur.Parent;
            }
            return ("", item);
        }

        private static string SourceFileOf(Document doc, ModelItem item)
        {
            var root = item;
            while (root.Parent != null) root = root.Parent;
            foreach (Model m in doc.Models)
            {
                if (m.RootItem != null && m.RootItem.Equals(root))
                {
                    var f = !string.IsNullOrWhiteSpace(m.SourceFileName) ? m.SourceFileName : m.FileName;
                    return string.IsNullOrWhiteSpace(f) ? "" : Path.GetFileName(f);
                }
            }
            return "";
        }

        private static JToken BridgeItemJson(Document doc, ModelItem? item)
        {
            if (item == null) return JValue.CreateNull();
            var (rid, carrier) = RevitIdUp(item);
            var o = ItemJson(carrier);
            o["clashedDisplayId"] = ItemRef(doc, item);
            o["revitElementId"] = rid;
            var (file, rvt) = SourceInfoOf(doc, item);
            o["sourceFile"] = file;
            o["revitSourceFile"] = rvt;
            if (o["revitCategory"] == null)
            {
                for (var cur = item; cur != null; cur = cur.Parent)
                {
                    if (InternalType(cur) == "LcRevitCollection") { o["revitCategory"] = cur.DisplayName ?? ""; break; }
                }
            }
            return o;
        }

        private static ClashResult FindResult(ClashTest test, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception("clashId is required (from navis_get_clash_results).");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            return all.FirstOrDefault(x =>
                       string.Equals(x.Guid.ToString(), id, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                   ?? throw new Exception($"Clash '{id}' not found in test '{test.DisplayName}'.");
        }

        private static JObject ClashDetail(JObject p)
        {
            var doc = ActiveDoc();
            var test = FindTest(ClashData(doc), Str(p, "test"));
            var r = FindResult(test, Str(p, "clashId"));
            return new JObject
            {
                ["test"] = test.DisplayName ?? "",
                ["clash"] = ResultJson(r),
                ["item1"] = BridgeItemJson(doc, r.Item1),
                ["item2"] = BridgeItemJson(doc, r.Item2),
            };
        }

        private static JObject ClashPairs(JObject p)
        {
            var doc = ActiveDoc();
            var test = FindTest(ClashData(doc), Str(p, "test"));
            var limit = Math.Max(1, Math.Min(1000, Int(p, "limit", 200)));
            var statusFilter = Str(p, "statusFilter");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            var arr = new JArray();
            int matched = 0;
            foreach (var r in all)
            {
                if (!string.IsNullOrWhiteSpace(statusFilter)
                    && !r.Status.ToString().Equals(statusFilter, StringComparison.OrdinalIgnoreCase)) continue;
                matched++;
                if (arr.Count >= limit) continue;
                arr.Add(new JObject
                {
                    ["clash"] = ResultJson(r),
                    ["item1"] = BridgeItemJson(doc, r.Item1),
                    ["item2"] = BridgeItemJson(doc, r.Item2),
                });
            }
            return new JObject
            {
                ["test"] = test.DisplayName ?? "",
                ["total"] = all.Count,
                ["matched"] = matched,
                ["returned"] = arr.Count,
                ["pairs"] = arr,
            };
        }

        private static JObject FindByRevitIds(JObject p)
        {
            var doc = ActiveDoc();
            var ids = (p["elementIds"] as JArray)?.Select(t => t.ToString().Trim()).Where(s => s != "").Distinct().ToList()
                      ?? new List<string>();
            if (ids.Count == 0) throw new Exception("elementIds is required (non-empty).");
            if (ids.Count > 500) throw new Exception("At most 500 elementIds per call.");
            var src = Str(p, "sourceFile");
            var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
            // <= 3 ids: one targeted search each. More: ONE search for every item that has Element > Id.
            List<ModelItem> candidates;
            bool singlePass = ids.Count > 3;
            if (!singlePass)
            {
                candidates = new List<ModelItem>();
                foreach (var id in ids)
                    candidates.AddRange(RunPropertySearch(doc, "Element", "Id", id, false).Cast<ModelItem>());
            }
            else
            {
                candidates = ItemsWithRevitId(doc);
            }
            var found = new JObject();
            var hits = new List<ModelItem>();
            int otherModels = 0;
            foreach (var m in candidates)
            {
                var id = PropText(m, "Element", "Id");
                if (!wanted.Contains(id)) continue;
                if (src != "" && !MatchesSource(doc, m, src)) { otherModels++; continue; }
                var arr = found[id] as JArray;
                if (arr == null) { arr = new JArray(); found[id] = arr; }
                arr.Add(ItemRef(doc, m));
                hits.Add(m);
            }
            var notFound = new JArray(ids.Where(i => found[i] == null));
            if (hits.Count > 0 && Bool(p, "select", true))
            {
                var coll = new ModelItemCollection();
                coll.AddRange(hits);
                doc.CurrentSelection.CopyFrom(coll);
            }
            if (hits.Count > 0 && Bool(p, "zoom", true))
            {
                var vp = doc.CurrentViewpoint.CreateCopy();
                vp.ZoomBox(ItemsBox(hits));
                ApplyViewpoint(doc, vp);
            }
            var result = new JObject
            {
                ["found"] = found,
                ["notFound"] = notFound,
                ["itemCount"] = hits.Count,
                ["sourceFile"] = src,
                ["skippedInOtherModels"] = otherModels,
                ["mode"] = singlePass ? "singlePass" : "perIdSearch",
            };
            if (otherModels > 0)
                result["note"] = $"{otherModels} item(s) with matching IDs were ignored because they belong to other models than '{src}' (Revit IDs are only unique per Revit file).";
            return result;
        }

        /// <summary>Every item that carries an Element > Id property (one search, PruneBelowMatch).</summary>
        private static List<ModelItem> ItemsWithRevitId(Document doc)
        {
            var search = new Search();
            search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;
            search.PruneBelowMatch = true;
            search.SearchConditions.Add(SearchCondition.HasPropertyByDisplayName("Element", "Id"));
            return search.FindAll(doc, false).Cast<ModelItem>().ToList();
        }

        private static string ModelKey(string name) =>
            Regex.Replace(Path.GetFileName(name ?? "").Trim(), @"\.(nwc|nwd|nwf|rvt)$", "", RegexOptions.IgnoreCase).ToLowerInvariant();

        /// <summary>True when an NWC/RVT source file and a Revit document title name the same model
        /// (exact, or one is a prefix of the other — covers workshared locals like "Project_jane").</summary>
        private static bool SameModel(string sourceFile, string other)
        {
            var a = ModelKey(sourceFile);
            var b = ModelKey(other);
            return a != "" && b != "" && (a == b || a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal));
        }

        /// <summary>
        /// Reloads changed model files if this Navisworks version exposes a refresh call to plugins.
        /// Probed by reflection so the plugin compiles on every version.
        /// </summary>
        private static JObject RefreshModels()
        {
            var doc = ActiveDoc();
            string[] names = { "UpdateFiles", "RefreshFiles" };
            foreach (var target in new object[] { doc, doc.Models })
            {
                foreach (var n in names)
                {
                    var mi = target.GetType().GetMethod(n,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    if (mi == null) continue;
                    mi.Invoke(target, null);
                    NavisLogger.Log($"navis_refresh_models via {target.GetType().Name}.{n}");
                    return new JObject { ["refreshed"] = true, ["via"] = target.GetType().Name + "." + n };
                }
            }
            throw new Exception("This Navisworks version exposes no refresh call to plugins. Click Home > Refresh in " +
                                "Navisworks, then run navis_recheck_clash.");
        }
    }
}
