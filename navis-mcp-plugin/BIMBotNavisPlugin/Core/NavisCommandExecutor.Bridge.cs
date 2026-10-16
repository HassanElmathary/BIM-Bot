using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>Walks up from item until a node carries Element > Id. Returns (id, carrierItem) or ("", item).</summary>
        private static (string id, ModelItem carrier) RevitIdUp(ModelItem item)
        {
            var cur = item;
            for (int i = 0; i <= RevitIdSearchDepth && cur != null; i++)
            {
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
            o["sourceFile"] = SourceFileOf(doc, item);
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
            if (ids.Count > 50) throw new Exception("At most 50 elementIds per call (each one is a model search).");

            var found = new JObject();
            var notFound = new JArray();
            var hits = new List<ModelItem>();
            foreach (var id in ids)
            {
                var exact = RunPropertySearch(doc, "Element", "Id", id, false)
                    .Cast<ModelItem>()
                    .Where(m => PropText(m, "Element", "Id") == id)
                    .ToList();
                if (exact.Count == 0) { notFound.Add(id); continue; }
                found[id] = new JArray(exact.Select(m => ItemRef(doc, m)));
                hits.AddRange(exact);
            }

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
            return new JObject { ["found"] = found, ["notFound"] = notFound, ["itemCount"] = hits.Count };
        }
    }
}
