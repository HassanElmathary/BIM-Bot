using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;
using BIMBotNavisPlugin;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Dispatches navis_* JSON-RPC methods to the Navisworks Manage .NET API.
    /// All calls arrive here already on the main thread via NavisMainThread.
    /// Every method degrades gracefully (Simulate → "requires Navisworks Manage",
    /// no open document → clear error). Never throws across the boundary with
    /// an unhandled exception type — ProcessRequest wraps, but messages stay clean.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        public static JObject Execute(string method, JObject pars)
        {
            switch (method)
            {
                case "navis_ping": return Ping();
                case "navis_get_model_info": return GetModelInfo();
                case "navis_search_items": return SearchItems(pars);
                case "navis_get_selected": return GetSelected(pars);
                case "navis_select_items": return SelectItems(pars);
                case "navis_viewpoints_list": return ViewpointsList();
                case "navis_viewpoint_apply": return ViewpointApply(pars);
                case "navis_viewpoint_save": return ViewpointSave(pars);
                case "navis_get_selection_sets": return SelectionSets();
                case "navis_list_clash_tests": return ClashList();
                case "navis_run_clash_test": return ClashRun(pars);
                case "navis_get_clash_results": return ClashResults(pars);
                case "navis_set_clash_status": return ClashSetStatus(pars);
                case "navis_export_clash_report": return ClashExport(pars);
                case "navis_append_file": return AppendFile(pars);
                case "navis_export_nwd": return ExportNwd(pars);
                case "navis_override_color": return OverrideColor(pars);
                case "navis_hide_items": return HideItems(pars);
                default:
                    return ExecuteExtended(method, pars)
                           ?? throw new Exception($"Unknown Navisworks command '{method}'.");
            }
        }

        // ── helpers ──

        private static Document ActiveDoc()
        {
            var doc = Application.ActiveDocument ?? Application.MainDocument;
            if (doc == null) throw new Exception("No open Navisworks document. Open a model first.");
            return doc;
        }

        private static string Str(JObject p, string name, string dflt = "")
        {
            var t = p[name];
            return t == null ? dflt : t.Value<string>() ?? dflt;
        }

        private static int Int(JObject p, string name, int dflt = 0)
        {
            var t = p[name];
            return t == null ? dflt : t.Value<int>();
        }

        private static double Num(JObject p, string name, double dflt = 0)
        {
            var t = p[name];
            return t == null ? dflt : t.Value<double>();
        }

        private static bool Bool(JObject p, string name, bool dflt = false)
        {
            var t = p[name];
            return t == null ? dflt : t.Value<bool>();
        }

        private static JObject ItemJson(ModelItem item)
        {
            var o = new JObject
            {
                ["displayId"] = ItemRef(ActiveDoc(), item),
                ["displayName"] = item.DisplayName ?? "",
                ["class"] = item.ClassDisplayName ?? "",
                ["hasGeometry"] = item.HasGeometry,
                ["isHidden"] = item.IsHidden,
            };
            if (item.InstanceGuid != Guid.Empty) o["instanceGuid"] = item.InstanceGuid.ToString();
            var rid = PropText(item, "Element", "Id");
            if (rid == "") rid = PropText(item, "Element ID", "Value");
            if (rid != "") o["revitElementId"] = rid;
            var rcat = PropText(item, "Element", "Category");
            if (rcat != "") o["revitCategory"] = rcat;
            return o;
        }

        /// <summary>Display string of tab > property, or "" when absent.</summary>
        private static string PropText(ModelItem item, string tab, string property)
        {
            try
            {
                var dp = item.PropertyCategories.FindPropertyByDisplayName(tab, property);
                return dp?.Value?.ToDisplayString() ?? "";
            }
            catch { return ""; }
        }
        /// <summary>
        /// Session-stable item reference "p:<modelIndex>/<childIndex>/...": the index of the
        /// item's model in doc.Models, then the child index at each level below the model root.
        /// Valid until models are removed or reloaded. "" if the item cannot be placed.
        /// </summary>
        private static string ItemRef(Document doc, ModelItem item)
        {
            var path = new List<int>();
            var cur = item;
            while (cur.Parent != null)
            {
                var parent = cur.Parent;
                int i = 0, at = -1;
                foreach (ModelItem child in parent.Children)
                {
                    if (child.Equals(cur)) { at = i; break; }
                    i++;
                }
                if (at < 0) return "";
                path.Add(at);
                cur = parent;
            }
            int m = 0, modelIndex = -1;
            foreach (Model model in doc.Models)
            {
                if (model.RootItem != null && model.RootItem.Equals(cur)) { modelIndex = m; break; }
                m++;
            }
            if (modelIndex < 0) return "";
            path.Add(modelIndex);
            path.Reverse();
            return "p:" + string.Join("/", path);
        }
        /// <summary>Normalized lookup key: "p:..." path, a non-empty GUID ("d" format), or null if invalid.</summary>
        private static string? RefKey(string id)
        {
            var s = id.Trim();
            if (s.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
                return "p:" + s.Substring(2).Trim();
            if (Guid.TryParse(s, out var g) && g != Guid.Empty) return g.ToString();
            return null;
        }
        private static ModelItem? ResolvePath(Document doc, string key)
        {
            var parts = key.Substring(2).Split('/');
            var idx = new int[parts.Length];
            for (int k = 0; k < parts.Length; k++)
                if (!int.TryParse(parts[k], out idx[k]) || idx[k] < 0) return null;
            var model = doc.Models.Cast<Model>().ElementAtOrDefault(idx[0]);
            ModelItem? cur = model?.RootItem;
            for (int k = 1; k < idx.Length && cur != null; k++)
                cur = cur.Children.Cast<ModelItem>().ElementAtOrDefault(idx[k]);
            return cur;
        }
        /// <summary>
        /// Resolve displayIds to ModelItems, in input order. Accepts "p:..." paths from
        /// ItemJson (O(depth) each) and, for compatibility, non-empty InstanceGuid strings
        /// (one model-tree walk for all of them).
        /// </summary>
        private static List<ModelItem> ResolveItems(Document doc, IEnumerable<string> ids)
        {
            var list = ids.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            var resolved = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);
            var guidWanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in list)
            {
                var key = RefKey(id);
                if (key == null || resolved.ContainsKey(key)) continue;
                if (key.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
                {
                    var item = ResolvePath(doc, key);
                    if (item != null) resolved[key] = item;
                }
                else guidWanted.Add(key);
            }
            if (guidWanted.Count > 0)
            {
                var found = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);
                foreach (Model model in doc.Models)
                    Walk(model.RootItem, guidWanted, found);
                foreach (var kv in found) resolved[kv.Key] = kv.Value;
            }
            var result = new List<ModelItem>();
            var missing = new List<string>();
            foreach (var id in list)
            {
                var key = RefKey(id);
                if (key != null && resolved.TryGetValue(key, out var item)) result.Add(item);
                else missing.Add(id);
            }
            if (missing.Count > 0)
                throw new Exception(
                    $"ModelItem(s) not found: {string.Join(", ", missing)}. Use the 'p:...' displayIds returned by " +
                    "navis_search_items or navis_get_selected in this session (they change if models are removed or " +
                    "reloaded). The all-zero GUID is never a valid id.");
            if (result.Count == 0) throw new Exception("No displayIds given.");
            return result;
        }
        private static void Walk(ModelItem item, HashSet<string> wanted, Dictionary<string, ModelItem> found)
        {
            if (item == null || found.Count >= wanted.Count) return;
            var key = item.InstanceGuid.ToString();
            if (wanted.Contains(key) && !found.ContainsKey(key))
                found[key] = item;
            if (found.Count >= wanted.Count) return;
            foreach (ModelItem child in item.Children)
            {
                Walk(child, wanted, found);
                if (found.Count >= wanted.Count) return;
            }
        }

        private static DocumentClashTests ClashData(Document doc)
        {
            try
            {
                return doc.GetClash().TestsData;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            {
                throw new Exception("Clash tools require Navisworks Manage (this looks like Simulate).", ex);
            }
        }

        private static List<ClashTest> AllTests(SavedItemCollection items)
        {
            var list = new List<ClashTest>();
            foreach (SavedItem item in items)
            {
                if (item is ClashTest t) list.Add(t);
                if (item is GroupItem g) list.AddRange(AllTests(g.Children));
            }
            return list;
        }

        private static ClashTest FindTest(DocumentClashTests data, string idOrName)
        {
            foreach (var t in AllTests(data.Tests))
            {
                if (string.Equals(t.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Guid.ToString(), idOrName, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            throw new Exception($"Clash test '{idOrName}' not found. Use navis_list_clash_tests.");
        }

        private static void CollectResults(SavedItemCollection items, List<ClashResult> outList)
        {
            foreach (SavedItem item in items)
            {
                if (item is ClashResult r) outList.Add(r);
                else if (item is GroupItem g) CollectResults(g.Children, outList);
            }
        }

        private static JObject ResultJson(ClashResult r)
        {
            return new JObject
            {
                ["id"] = r.Guid.ToString(),
                ["name"] = r.DisplayName ?? "",
                ["status"] = r.Status.ToString(),
                ["distance"] = r.Distance,
                ["description"] = r.Description ?? "",
            };
        }

        // ── reading / viewpoints ──

        private static JObject Ping()
        {
            var doc = Application.ActiveDocument ?? Application.MainDocument;
            return new JObject
            {
                ["ok"] = true,
                ["product"] = "navisworks",
                ["pluginVersion"] = NavisPluginApp.Version,
                ["port"] = NavisPluginApp.ActivePort,
                ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                ["hasDocument"] = doc != null && !doc.IsClear,
                ["fileName"] = doc?.FileName ?? "",
            };
        }

        private static JObject GetModelInfo()
        {
            var doc = ActiveDoc();
            int models = 0, viewpoints = 0, sets = 0;
            foreach (Model m in doc.Models) models++;
            try { viewpoints = doc.SavedViewpoints.ToSavedItemCollection().Count; } catch { }
            try { sets = doc.SelectionSets.ToSavedItemCollection().Count; } catch { }
            return new JObject
            {
                ["product"] = "navisworks",
                ["pluginVersion"] = NavisPluginApp.Version,
                ["title"] = doc.Title ?? "",
                ["fileName"] = doc.FileName ?? "",
                ["modelCount"] = models,
                ["savedViewpointCount"] = viewpoints,
                ["selectionSetCount"] = sets,
            };
        }

        private static JObject SearchItems(JObject p)
        {
            var doc = ActiveDoc();
            var text = Str(p, "searchText");
            if (string.IsNullOrWhiteSpace(text)) throw new Exception("searchText is required.");
            var prop = Str(p, "searchProperty", "Name").Trim();
            var limit = Math.Max(1, Int(p, "limit", 100));
            var withinSelection = Bool(p, "withinSelection");

            string tab, name;
            string? fbTab = null, fbName = null;
            switch (prop.ToLowerInvariant())
            {
                case "":
                case "name": tab = "Item"; name = "Name"; break;
                case "class":
                case "type": tab = "Item"; name = "Type"; break;
                case "material": tab = "Item"; name = "Material"; break;
                case "category": tab = "Element"; name = "Category"; fbTab = "Item"; fbName = "Type"; break;
                default:
                {
                    var sep = prop.IndexOf('>');
                    if (sep <= 0 || sep >= prop.Length - 1)
                        throw new Exception("searchProperty must be Name, Category, Class, Material, or 'Tab>Property' (e.g. 'Element>Mark').");
                    tab = prop.Substring(0, sep).Trim();
                    name = prop.Substring(sep + 1).Trim();
                    break;
                }
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var found = RunPropertySearch(doc, tab, name, text, withinSelection);
            var matchedOn = tab + ">" + name;
            if (found.Count == 0 && fbTab != null && fbName != null)
            {
                found = RunPropertySearch(doc, fbTab, fbName, text, withinSelection);
                matchedOn = fbTab + ">" + fbName;
            }
            sw.Stop();
            var arr = new JArray();
            foreach (ModelItem item in found)
            {
                if (arr.Count >= limit) break;
                arr.Add(ItemJson(item));
            }
            var result = new JObject
            {
                ["count"] = found.Count,
                ["returned"] = arr.Count,
                ["matchedOn"] = matchedOn,
                ["scope"] = withinSelection ? "selection" : "wholeModel",
                ["elapsedMs"] = sw.ElapsedMilliseconds,
                ["items"] = arr,
            };
            if (sw.ElapsedMilliseconds > 2000)
                result["note"] = $"Search took {sw.ElapsedMilliseconds} ms: Navisworks scans every item in scope on its UI thread " +
                                 "and is frozen meanwhile. Reuse the displayIds above instead of searching again, or select a " +
                                 "model/level first and pass withinSelection=true.";
            if (found.Count == 0 && fbTab != null)
                result["hint"] = $"No item has Element>Category or Item>Type containing '{text}'. Run navis_get_item_properties " +
                                 "on one item to see its property tabs, then search with searchProperty 'Tab>Property'.";
            return result;
        }
        private static ModelItemCollection RunPropertySearch(Document doc, string tab, string property, string text, bool withinSelection)
        {
            var search = new Search();
            if (withinSelection)
            {
                var sel = doc.CurrentSelection.SelectedItems;
                if (sel.Count == 0) throw new Exception("withinSelection is true but nothing is selected in Navisworks.");
                search.Selection.CopyFrom(sel);
            }
            else
            {
                search.Selection.SelectAll();
            }
            search.Locations = SearchLocations.DescendantsAndSelf;
            search.PruneBelowMatch = true;
            search.SearchConditions.Add(
                SearchCondition.HasPropertyByDisplayName(tab, property).DisplayStringContains(text).IgnoreStringValueCase());
            return search.FindAll(doc, false);
        }

        private static JObject GetSelected(JObject p)
        {
            var doc = ActiveDoc();
            var items = doc.CurrentSelection.SelectedItems;
            var limit = Math.Max(1, Int(p, "limit", 500));
            var arr = new JArray();
            foreach (ModelItem item in items)
            {
                if (arr.Count >= limit) break;
                arr.Add(ItemJson(item));
            }
            return new JObject { ["count"] = items.Count, ["returned"] = arr.Count, ["items"] = arr };
        }

        private static JObject SelectItems(JObject p)
        {
            var doc = ActiveDoc();
            var ids = p["displayIds"]?.Values<string>().Where(s => s != null).Select(s => s!).ToList()
                      ?? new List<string>();
            if (ids.Count == 0) throw new Exception("displayIds is required (non-empty).");
            var items = ResolveItems(doc, ids);
            doc.CurrentSelection.CopyFrom(items);
            return new JObject { ["selected"] = items.Count };
        }

        private static void CollectViewpoints(SavedItemCollection items, JArray arr, string folder = "")
        {
            foreach (SavedItem item in items)
            {
                if (item is SavedViewpoint sv)
                {
                    var o = new JObject { ["ref"] = $"vp:{arr.Count}", ["name"] = sv.DisplayName ?? "" };
                    if (folder != "") o["folder"] = folder;
                    if (sv.Guid != Guid.Empty) o["guid"] = sv.Guid.ToString();
                    arr.Add(o);
                }
                else if (item is GroupItem g)
                {
                    var name = g.DisplayName ?? "";
                    CollectViewpoints(g.Children, arr, folder == "" ? name : folder + " / " + name);
                }
            }
        }

        private static JObject ViewpointsList()
        {
            var doc = ActiveDoc();
            var arr = new JArray();
            CollectViewpoints(doc.SavedViewpoints.ToSavedItemCollection(), arr);
            var duplicates = arr.GroupBy(v => v.Value<string>("name") ?? "").Count(g => g.Count() > 1);
            return new JObject
            {
                ["count"] = arr.Count,
                ["duplicateNames"] = duplicates,
                ["note"] = "Use 'ref' (e.g. vp:12) to pick a viewpoint: names can repeat and many GUIDs are empty. Refs change if saved viewpoints are added, deleted or reordered.",
                ["viewpoints"] = arr,
            };
        }

        private static SavedViewpoint FindViewpoint(Document doc, string idOrName)
        {
            var found = new List<SavedViewpoint>();
            void Recurse(SavedItemCollection items)
            {
                foreach (SavedItem item in items)
                {
                    if (item is SavedViewpoint sv) found.Add(sv);
                    else if (item is GroupItem g) Recurse(g.Children);
                }
            }
            Recurse(doc.SavedViewpoints.ToSavedItemCollection());
            if (idOrName.StartsWith("vp:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(idOrName.Substring(3), out var idx))
            {
                if (idx >= 0 && idx < found.Count) return found[idx];
                throw new Exception($"Viewpoint ref '{idOrName}' is out of range (0–{found.Count - 1}). Use navis_viewpoints_list.");
            }
            foreach (var sv in found)
            {
                if (string.Equals(sv.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sv.Guid.ToString(), idOrName, StringComparison.OrdinalIgnoreCase))
                    return sv;
            }
            throw new Exception($"Viewpoint '{idOrName}' not found. Use navis_viewpoints_list.");
        }

        private static JObject ViewpointApply(JObject p)
        {
            var doc = ActiveDoc();
            var name = Str(p, "viewpoint");
            if (string.IsNullOrWhiteSpace(name)) throw new Exception("viewpoint (name or GUID) is required.");
            doc.SavedViewpoints.CurrentSavedViewpoint = FindViewpoint(doc, name);
            return new JObject { ["applied"] = name };
        }

        private static JObject ViewpointSave(JObject p)
        {
            var doc = ActiveDoc();
            var name = Str(p, "name");
            if (string.IsNullOrWhiteSpace(name)) throw new Exception("name is required.");
            if (doc.ActiveView == null) throw new Exception("No active view to save.");
            var svp = new SavedViewpoint(doc.ActiveView.CreateViewpointCopy()) { DisplayName = name };
            doc.SavedViewpoints.Value.Add(svp);
            return new JObject { ["saved"] = name, ["guid"] = svp.Guid.ToString() };
        }

        private static JObject SelectionSets()
        {
            var doc = ActiveDoc();
            var arr = new JArray();
            foreach (SavedItem item in doc.SelectionSets.ToSavedItemCollection())
            {
                if (item is SelectionSet s)
                {
                    int count = 0;
                    try
                    {
                        if (s.Search != null && !s.Search.IsClear)
                            count = s.Search.FindAll(doc, false).Count;
                    }
                    catch { }
                    arr.Add(new JObject { ["name"] = s.DisplayName ?? "", ["guid"] = s.Guid.ToString(), ["itemCount"] = count });
                }
            }
            return new JObject { ["count"] = arr.Count, ["sets"] = arr };
        }

        // ── clash ──

        private static JObject ClashList()
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var arr = new JArray();
            foreach (var t in AllTests(data.Tests))
            {
                int results = 0;
                try
                {
                    var tmp = new List<ClashResult>();
                    CollectResults(t.Children, tmp);
                    results = tmp.Count;
                }
                catch { }
                arr.Add(new JObject
                {
                    ["name"] = t.DisplayName ?? "",
                    ["guid"] = t.Guid.ToString(),
                    ["status"] = t.Status.ToString(),
                    ["testType"] = t.TestType.ToString(),
                    ["resultCount"] = results,
                });
            }
            return new JObject { ["count"] = arr.Count, ["tests"] = arr };
        }

        private static JObject ClashRun(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var type = Str(p, "testType");
            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse(type, true, out ClashTestType tt))
                test.TestType = tt;
            data.TestsRunTest(test);
            var results = new List<ClashResult>();
            CollectResults(test.Children, results);
            return new JObject { ["test"] = test.DisplayName ?? "", ["resultCount"] = results.Count };
        }

        private static JObject ClashResults(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var limit = Int(p, "limit", 100);
            var statusFilter = Str(p, "statusFilter");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            var arr = new JArray();
            int skipped = 0;
            foreach (var r in all)
            {
                if (!string.IsNullOrWhiteSpace(statusFilter)
                    && !r.Status.ToString().Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }
                if (arr.Count >= limit) break;
                arr.Add(ResultJson(r));
            }
            return new JObject
            {
                ["test"] = test.DisplayName ?? "",
                ["total"] = all.Count,
                ["returned"] = arr.Count,
                ["filteredOut"] = skipped,
                ["results"] = arr,
            };
        }

        private static JObject ClashSetStatus(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var statusStr = Str(p, "status");
            if (!Enum.TryParse(statusStr, true, out ClashResultStatus status))
                throw new Exception($"Invalid status '{statusStr}'. Use New/Active/Reviewed/Approved/Resolved.");
            var ids = new HashSet<string>(
                p["clashIds"]?.Values<string>().Where(s => s != null).Select(s => s!) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) throw new Exception("clashIds is required (non-empty).");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            int updated = 0;
            foreach (var r in all)
            {
                if (ids.Contains(r.Guid.ToString()))
                {
                    data.TestsEditResultStatus(r, status);
                    updated++;
                }
            }
            return new JObject { ["test"] = test.DisplayName ?? "", ["updated"] = updated, ["requested"] = ids.Count };
        }

        private static JObject ClashExport(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var testName = Str(p, "test");
            var test = FindTest(data, testName);
            var format = Str(p, "format", "HTML").ToUpperInvariant();
            var folder = Str(p, "folder");
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = !string.IsNullOrWhiteSpace(doc.FileName)
                    ? Path.GetDirectoryName(doc.FileName)!
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BIMBot");
            }
            Directory.CreateDirectory(folder);
            var safe = string.Concat((test.DisplayName ?? "clash").Split(Path.GetInvalidFileNameChars()));
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);

            string path;
            if (format == "XML")
            {
                path = Path.Combine(folder, safe + ".xml");
                new XDocument(
                    new XElement("ClashTest",
                        new XAttribute("name", test.DisplayName ?? ""),
                        new XAttribute("exported", DateTime.Now.ToString("o")),
                        all.Select(r => new XElement("Clash",
                            new XAttribute("id", r.Guid.ToString()),
                            new XAttribute("name", r.DisplayName ?? ""),
                            new XAttribute("status", r.Status.ToString()),
                            new XAttribute("distance", r.Distance),
                            new XAttribute("description", r.Description ?? ""))))).Save(path);
            }
            else
            {
                path = Path.Combine(folder, safe + ".html");
                var rows = string.Concat(all.Select(r =>
                    $"<tr><td>{r.Guid}</td><td>{System.Net.WebUtility.HtmlEncode(r.DisplayName)}</td>" +
                    $"<td>{r.Status}</td><td>{r.Distance:F3}</td>" +
                    $"<td>{System.Net.WebUtility.HtmlEncode(r.Description)}</td></tr>"));
                File.WriteAllText(path,
                    "<html><head><meta charset=\"utf-8\"><title>Clash report</title></head><body>" +
                    $"<h1>{System.Net.WebUtility.HtmlEncode(test.DisplayName)} — {all.Count} clashes</h1>" +
                    "<table border=\"1\"><tr><th>ID</th><th>Name</th><th>Status</th><th>Distance</th><th>Description</th></tr>" +
                    rows + "</table></body></html>");
            }
            return new JObject { ["test"] = test.DisplayName ?? "", ["format"] = format, ["path"] = path, ["resultCount"] = all.Count };
        }

        // ── federation / appearance ──

        private static JObject AppendFile(JObject p)
        {
            var doc = ActiveDoc();
            var path = Str(p, "filePath");
            if (string.IsNullOrWhiteSpace(path)) throw new Exception("filePath is required.");
            if (!File.Exists(path)) throw new Exception($"File not found: {path}");
            doc.AppendFile(path);
            return new JObject { ["appended"] = path };
        }

        private static JObject ExportNwd(JObject p)
        {
            var doc = ActiveDoc();
            var path = Str(p, "filePath");
            if (string.IsNullOrWhiteSpace(path))
            {
                if (string.IsNullOrWhiteSpace(doc.FileName))
                    throw new Exception("filePath is required (document has no file name yet).");
                path = Path.ChangeExtension(doc.FileName, ".nwd");
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            doc.SaveFile(path);
            return new JObject { ["saved"] = path };
        }

        private static JObject OverrideColor(JObject p)
        {
            var doc = ActiveDoc();
            var ids = p["displayIds"]?.Values<string>().Where(s => s != null).Select(s => s!).ToList()
                      ?? new List<string>();
            if (ids.Count == 0) throw new Exception("displayIds is required (non-empty).");
            var items = ResolveItems(doc, ids);
            doc.Models.OverridePermanentColor(items,
                new Autodesk.Navisworks.Api.Color(Num(p, "r"), Num(p, "g"), Num(p, "b")));
            var t = p["transparency"];
            if (t != null) doc.Models.OverridePermanentTransparency(items, t.Value<double>());
            return new JObject { ["overridden"] = items.Count };
        }

        private static JObject HideItems(JObject p)
        {
            var doc = ActiveDoc();
            var ids = p["displayIds"]?.Values<string>().Where(s => s != null).Select(s => s!).ToList()
                      ?? new List<string>();
            if (ids.Count == 0) throw new Exception("displayIds is required (non-empty).");
            var unhide = Bool(p, "unhide");
            var items = ResolveItems(doc, ids);
            doc.Models.SetHidden(items, !unhide);
            return new JObject { [unhide ? "unhidden" : "hidden"] = items.Count };
        }
    }
}
