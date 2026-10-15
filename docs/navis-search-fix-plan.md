IMPLEMENTATION PLAN — navis_search_items: usable item IDs + correct Category search
Audience: coding agent. Follow this literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP

======================================================================
1. PROBLEM (measured live on 2026-10-15)
======================================================================
File: navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs

Bug 1 — every result has displayId "00000000-0000-0000-0000-000000000000".
  ItemJson() writes ModelItem.InstanceGuid. Navisworks leaves InstanceGuid empty
  (Guid.Empty) for most items. ResolveItems()/Walk() look items up by the same
  InstanceGuid string. So the AI can never refer back to a search result in
  navis_select_items, navis_zoom_to_items, navis_hide_items, navis_override_color,
  navis_measure, navis_create_selection_set, navis_get_item_properties, clash-test
  selections or TimeLiner attachments. ALL of those go through ONE helper:
  ResolveItems(Document, IEnumerable<string>) in NavisCommandExecutor.cs
  (callers: SelectItems, OverrideColor, HideItems in NavisCommandExecutor.cs;
  ItemsOrSelection, ApplyTaskFields, FillClashSelection, Measure in
  NavisCommandExecutor.Extended.cs). Fixing ItemJson + ResolveItems fixes them all.

Bug 2 — searchProperty "Category" finds nothing for Revit models.
  SearchItems() calls SearchCondition.HasCategoryByDisplayName(text). That matches
  items that have a property TAB named <text>. In Revit NWC/RVT models the category
  is the "Category" PROPERTY inside the "Element" tab (e.g. Element > Category =
  "Walls"). Searching "Walls" returned 0 items and blocked the Navisworks UI thread
  for 8.7 s (the search scans every item of the model).
  Also: "Material" is advertised by the MCP tool but falls through to a Name search,
  "Class" enumerates the WHOLE model with no condition and filters in C#, and the
  Name search is case-sensitive.

======================================================================
2. THE FIX (design)
======================================================================
A. Item reference = model-tree index path, string form "p:<model>/<child>/<child>..."
   - <model> = index of the item's model in doc.Models; each following number is the
     index of the next item in its parent's Children. "p:0" is the root of model 0.
   - Resolving it is O(depth): no whole-model walk. Stable for the whole session as
     long as models are not removed or reloaded (appending a file adds models at the
     end, so existing paths stay valid).
   - ResolveItems accepts "p:..." paths AND (for backward compatibility) real,
     non-empty InstanceGuid strings. The all-zero GUID is rejected with a clear hint.
   - ResolveItems keeps the INPUT ORDER (Measure relies on items[0]/items[1]).
   - ItemJson keeps its signature `ItemJson(ModelItem item)` (Extended.cs uses it as a
     method group), outputs displayId = the "p:" path, and adds optional extras:
     "instanceGuid" (only when not empty), "revitElementId" and "revitCategory"
     (only when the item has the Revit "Element" tab).
B. Search semantics (all case-insensitive "contains", native Navisworks Search):
     Name     → Item > Name                     (default)
     Category → Element > Category; if 0 hits, retry Item > Type
     Class    → Item > Type   (Item > Type is the class display name)
     Material → Item > Material
     "Tab>Property" → any property, e.g. "Element>Mark", "Element ID>Value"
   - PruneBelowMatch = true: one hit per element, not its geometry children too.
   - New optional param withinSelection (bool): search only under the current
     selection — the way to make searches fast on big federated models.
   - Response adds matchedOn, scope, elapsedMs, and a "note" when the search took
     > 2 s explaining the cost. A Category search with 0 hits adds a "hint".
   - The search must stay on the UI thread (Navisworks API rule) — the cost cannot be
     moved off-thread, only reduced (prune, selection scope) and reported.
C. navis_get_selected gets an optional "limit" (default 500), because ItemJson now
   does a little more work per item.
D. MCP tool text for navis_search_items is updated so the AI knows the ID format,
   the property options and the cost.

======================================================================
3. HARD RULES
======================================================================
1. Edit ONLY these two files:
     navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs
     revit-mcp-server/src/tools/navis_reading_tools.ts
2. Do NOT edit NavisCommandExecutor.Extended.cs or any other file, any .csproj,
   package.json, tsconfig.json, or anything else. ItemJson(ModelItem) and
   ResolveItems(Document, IEnumerable<string>) MUST keep these exact signatures so
   Extended.cs compiles unchanged.
3. Do not run git checkout / stash / reset / clean. Do not commit. Do not revert or
   reformat anything you were not told to change.
4. No new NuGet or npm packages. No new `using` lines in the C# file (Stopwatch is
   written fully qualified on purpose).
5. Do NOT deploy: do not copy the DLL anywhere, do not run deploy.ps1 or any
   installer script, do not run `npm run build` (it rewrites revit-mcp-server/build,
   which the live MCP server runs from). Type-check TypeScript with --noEmit only.
   Navisworks may be open while you work; that is fine because you only build into bin\.

======================================================================
4. TASK — NavisCommandExecutor.cs
======================================================================
Make exactly the five edits below. Everything else in the file stays as it is.

----------------------------------------------------------------------
4.1 Dispatcher: pass params to GetSelected
----------------------------------------------------------------------
FIND:
                case "navis_get_selected": return GetSelected();
REPLACE WITH:
                case "navis_get_selected": return GetSelected(pars);

----------------------------------------------------------------------
4.2 Replace ItemJson, ResolveItems and Walk
----------------------------------------------------------------------
FIND the block that starts with
        private static JObject ItemJson(ModelItem item)
and ends with the closing brace of
        private static void Walk(ModelItem item, HashSet<string> wanted, Dictionary<string, ModelItem> found)
(i.e. the three methods ItemJson, ResolveItems, Walk, plus the /// summary line above
ResolveItems). REPLACE that whole block with exactly:

----- BEGIN BLOCK -----
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
        /// Session-stable item reference "p:&lt;modelIndex&gt;/&lt;childIndex&gt;/...": the index of the
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
----- END BLOCK -----

----------------------------------------------------------------------
4.3 Replace SearchItems
----------------------------------------------------------------------
FIND the whole method
        private static JObject SearchItems(JObject p)
        { ... }
(from that line to its closing brace, just before `private static JObject GetSelected()`).
REPLACE it with exactly:

----- BEGIN BLOCK -----
        /// <summary>
        /// Case-insensitive "contains" search on one property, using the native Navisworks Search.
        /// searchProperty: Name (Item>Name) | Category (Revit Element>Category, falls back to Item>Type)
        /// | Class (Item>Type) | Material (Item>Material) | "Tab>Property".
        /// Runs on the UI thread and scans every item in scope, so it is reported with elapsedMs;
        /// withinSelection limits the scope to the current selection.
        /// </summary>
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
----- END BLOCK -----

----------------------------------------------------------------------
4.4 Replace GetSelected
----------------------------------------------------------------------
FIND:
        private static JObject GetSelected()
        {
            var doc = ActiveDoc();
            var items = doc.CurrentSelection.SelectedItems;
            var arr = new JArray();
            foreach (ModelItem item in items) arr.Add(ItemJson(item));
            return new JObject { ["count"] = arr.Count, ["items"] = arr };
        }
REPLACE WITH:
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

----------------------------------------------------------------------
4.5 Nothing else
----------------------------------------------------------------------
SelectItems, OverrideColor, HideItems keep calling ResolveItems unchanged.

Notes for compiling (apply ONLY if that exact error appears; change nothing else):
- CS1061 "'Search' does not contain a definition for 'PruneBelowMatch'": delete that
  one line and say so in your report.
- CS1061 on `.IgnoreStringValueCase()`: it is already used the same way in
  NavisCommandExecutor.Extended.cs (CreateSearchSet), so this should not happen; if it
  does, remove `.IgnoreStringValueCase()` from RunPropertySearch and report it.
- Nullable warnings (CS86xx) are acceptable; errors are not.
- If `pars` could be null at 4.1, Int() would throw — it is not null today (the same
  `pars` is already passed to SelectItems), so do not add null handling.

======================================================================
5. TASK — revit-mcp-server/src/tools/navis_reading_tools.ts
======================================================================
Replace ONLY the `server.tool("navis_search_items", ...)` registration (the block that
currently starts at `server.tool(` right above `"navis_search_items",` and ends with the
matching `);`) with exactly:

----- BEGIN BLOCK -----
    server.tool(
        "navis_search_items",
        "Search the Navisworks model for items whose property value contains searchText (case-insensitive). " +
        "Returns displayIds like 'p:0/3/12' that navis_select_items, navis_zoom_to_items, navis_hide_items, " +
        "navis_override_color, navis_get_item_properties and the other displayIds tools accept " +
        "(valid until models are removed or reloaded). Each search scans every item on the Navisworks UI thread: " +
        "on large federated models Navisworks freezes for several seconds, so reuse returned displayIds instead of " +
        "re-searching, and use withinSelection to search only under the current selection.",
        {
            searchText: z.string().describe("Text the property value must contain, e.g. 'Walls', 'Level 1', 'Pipe'"),
            searchProperty: z.string().optional().describe(
                "Name (Item>Name, default) | Category (Revit Element>Category, falls back to Item>Type) | " +
                "Class (Item>Type) | Material (Item>Material) | 'Tab>Property', e.g. 'Element>Mark'. " +
                "Use navis_get_item_properties on one item to see its tabs."),
            limit: z.number().optional().describe("Max items to return (default 100); 'count' reports all matches"),
            withinSelection: z.boolean().optional().describe("Search only the current Navisworks selection and its children (much faster on big models)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_search_items", {
                        searchText: args.searchText,
                        searchProperty: args.searchProperty || "Name",
                        limit: args.limit || 100,
                        withinSelection: args.withinSelection ?? false,
                    }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
----- END BLOCK -----

Also in the same file, in the `navis_select_items` registration, change ONLY the
description string
    "Select ModelItems in Navisworks by display IDs."
to
    "Select ModelItems in Navisworks by displayIds ('p:...' values from navis_search_items or navis_get_selected)."
Nothing else in this file changes.

======================================================================
6. BUILD / TYPE-CHECK
======================================================================
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
Must succeed with 0 errors.

  cd revit-mcp-server
  npx tsc --noEmit
Must finish with no errors. (Do NOT run `npm run build`.)

If either fails for any reason other than the notes in 4.5, STOP and report the exact
compiler output — do not change other files, the .csproj, or tsconfig.

======================================================================
7. SELF-CHECKS (no Navisworks needed)
======================================================================
Run and report output:
  A. git status --short
     Expect: exactly the two files from rule 1 modified (plus nothing new except
     build output under bin\/obj\, which is ignored).
  B. grep -n "HasCategoryByDisplayName" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs
     Expect: no output.
  C. grep -n "InstanceGuid" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs
     Expect: exactly 2 lines — the `if (item.InstanceGuid != Guid.Empty) ...` line in
     ItemJson and the `var key = item.InstanceGuid.ToString();` line in Walk.
     displayId must NOT be built from InstanceGuid.
  D. grep -n "ItemRef\|ResolvePath\|RefKey\|RunPropertySearch\|PruneBelowMatch\|withinSelection" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs
     Expect: all six names present.
  E. git diff --stat -- navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
     Expect: no output (Extended.cs untouched).

======================================================================
8. LIVE TEST (done afterwards by the reviewer — NOT by you)
======================================================================
For reference only; do not attempt. The reviewer deploys the DLL and, with a Revit
model open in Navisworks, checks:
  1. navis_search_items {searchText:"Walls", searchProperty:"Category"} → count > 0,
     matchedOn "Element>Category", displayIds start with "p:", revitCategory "Walls".
  2. navis_select_items / navis_zoom_to_items / navis_hide_items / navis_override_color
     with those displayIds act on the right elements; navis_measure with two of them
     returns itemA/itemB in the given order.
  3. A bogus id and the all-zero GUID give the new "not found" message.
  4. elapsedMs is reported; the same search with withinSelection=true on one selected
     level/model is clearly faster; "note" appears when > 2 s.
  5. searchProperty "Class" and "Material" return sensible hits (verifies the Item>Type
     assumption), and "Element>Mark" style works.

======================================================================
9. REPORT BACK
======================================================================
1. Files changed.
2. Last ~20 lines of the dotnet build output, and the tsc --noEmit output.
3. Output of self-checks A–E.
4. Anything you could not do exactly as written (including any 4.5 note you applied), and why.
