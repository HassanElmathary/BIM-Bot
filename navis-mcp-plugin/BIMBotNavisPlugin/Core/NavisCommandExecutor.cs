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
                case "navis_get_selected": return GetSelected();
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
            return new JObject
            {
                ["displayId"] = item.InstanceGuid.ToString(),
                ["displayName"] = item.DisplayName ?? "",
                ["class"] = item.ClassDisplayName ?? "",
                ["hasGeometry"] = item.HasGeometry,
                ["isHidden"] = item.IsHidden,
            };
        }

        /// <summary>Resolve InstanceGuid strings to ModelItems by walking the model tree.</summary>
        private static List<ModelItem> ResolveItems(Document doc, IEnumerable<string> ids)
        {
            var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            var found = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);
            foreach (Model model in doc.Models)
                Walk(model.RootItem, wanted, found);
            var missing = wanted.Where(w => !found.ContainsKey(w)).ToList();
            if (missing.Count > 0)
                throw new Exception($"ModelItem(s) not found: {string.Join(", ", missing)}. Use navis_search_items to get valid displayIds.");
            return wanted.Select(w => found[w]).ToList();
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
            var prop = Str(p, "searchProperty", "Name");
            var limit = Int(p, "limit", 100);

            var search = new Search();
            search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;
            bool filterByClass = prop.Equals("Class", StringComparison.OrdinalIgnoreCase);
            if (prop.Equals("Category", StringComparison.OrdinalIgnoreCase))
                search.SearchConditions.Add(SearchCondition.HasCategoryByDisplayName(text));
            else if (!filterByClass)
                search.SearchConditions.Add(
                    SearchCondition.HasPropertyByDisplayName("Item", "Name").DisplayStringContains(text));

            var found = search.FindAll(doc, false);
            var arr = new JArray();
            int matched = 0;
            foreach (ModelItem item in found)
            {
                if (filterByClass && (item.ClassDisplayName == null ||
                    item.ClassDisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                matched++;
                if (arr.Count >= limit) continue;
                arr.Add(ItemJson(item));
            }
            return new JObject { ["count"] = matched, ["returned"] = arr.Count, ["items"] = arr };
        }

        private static JObject GetSelected()
        {
            var doc = ActiveDoc();
            var items = doc.CurrentSelection.SelectedItems;
            var arr = new JArray();
            foreach (ModelItem item in items) arr.Add(ItemJson(item));
            return new JObject { ["count"] = arr.Count, ["items"] = arr };
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

        private static void CollectViewpoints(SavedItemCollection items, JArray arr)
        {
            foreach (SavedItem item in items)
            {
                if (item is SavedViewpoint sv)
                    arr.Add(new JObject { ["name"] = sv.DisplayName ?? "", ["guid"] = sv.Guid.ToString() });
                else if (item is GroupItem g)
                    CollectViewpoints(g.Children, arr);
            }
        }

        private static JObject ViewpointsList()
        {
            var doc = ActiveDoc();
            var arr = new JArray();
            CollectViewpoints(doc.SavedViewpoints.ToSavedItemCollection(), arr);
            return new JObject { ["count"] = arr.Count, ["viewpoints"] = arr };
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
