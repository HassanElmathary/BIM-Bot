IMPLEMENTATION PLAN — Step 1b: capture fixes found in live testing
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-1-view-capture   (already contains step 1; HEAD is at or after 3a3285f)

======================================================================
0. BEFORE YOU START
======================================================================
1. git switch feat/navis-1-view-capture
2. git branch --show-current   -> must print feat/navis-1-view-capture
3. git status --short          -> must be empty. If not, STOP and report.

======================================================================
1. PROBLEMS (found live on a 359-viewpoint federated model)
======================================================================
P1. navis_capture_view with displayIds zooms to the items, but the image shows only walls:
    other geometry blocks them, and the Navisworks selection highlight is NOT drawn by
    View.GenerateImage, so select=true does nothing for the picture.
    navis_capture_clash already solves this (temporary colour + "isolate" the items) — reuse it.
P2. Saved viewpoints cannot be targeted reliably: most have an all-zero GUID and names repeat
    (e.g. "NAVISWORK" exists more than 10 times). FindViewpoint by name returns the first match only.
    Fix: give every viewpoint a stable index reference "vp:<n>" in navis_viewpoints_list and accept
    it everywhere a viewpoint is taken.

======================================================================
2. HARD RULES
======================================================================
1. Only edit these files:
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Capture.cs
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs   (ONLY CollectViewpoints, ViewpointsList, FindViewpoint)
   - revit-mcp-server/src/tools/navis_capture_tools.ts
   - revit-mcp-server/src/tools/navis_reading_tools.ts                 (ONLY descriptions of navis_viewpoints_list and navis_viewpoint_apply)
2. Touch nothing else. No new packages. Do not deploy. Do not commit or push.
   No git reset / stash / clean / checkout -- <file>.
3. Reuse existing helpers (do not redefine): ActiveDoc, Str, Int, Bool, Ids (Extended.cs),
   ResolveItems, ItemsBox, ApplyViewpoint, ItemJson, OthersAlongPaths, Paint, Snapshot.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — NavisCommandExecutor.Capture.cs: highlight + isolate for displayIds -----

1a. Directly after the line `    public static partial class NavisCommandExecutor` and its `{`, add:

        private static readonly Autodesk.Navisworks.Api.Color Red = new Autodesk.Navisworks.Api.Color(1, 0, 0);
        private static readonly Autodesk.Navisworks.Api.Color Green = new Autodesk.Navisworks.Api.Color(0, 0.75, 0);
        private static readonly Autodesk.Navisworks.Api.Color Orange = new Autodesk.Navisworks.Api.Color(1, 0.5, 0);

1b. In CaptureView, replace this block:

                if (p["displayIds"] is JArray ids && ids.Count > 0)
                {
                    ZoomToItems(new JObject { ["displayIds"] = ids, ["select"] = Bool(p, "select", true) });
                    moved = true;
                }
                var shot = Snapshot(doc, p);

with:

                var ids = Ids(p);
                var items = ids.Count > 0 ? ResolveItems(doc, ids) : new List<ModelItem>();
                if (items.Count > 0)
                {
                    if (Bool(p, "select", true))
                    {
                        var coll = new ModelItemCollection();
                        coll.AddRange(items);
                        doc.CurrentSelection.CopyFrom(coll);
                    }
                    var vp = doc.CurrentViewpoint.CreateCopy();
                    vp.ZoomBox(ItemsBox(items));
                    ApplyViewpoint(doc, vp);
                    moved = true;
                }
                var shot = items.Count > 0
                    ? Highlighted(doc, items.Select(i => (i, Orange)).ToList(), p)
                    : Snapshot(doc, p);
                if (items.Count > 0) shot["items"] = new JArray(items.Select(i => (JToken)ItemJson(i)));

1c. In CaptureClash, replace:

            var shot = Highlighted(doc, r, p);

with:

            var painted = new List<(ModelItem item, Autodesk.Navisworks.Api.Color color)>();
            if (r.Item1 != null) painted.Add((r.Item1, Red));
            if (r.Item2 != null) painted.Add((r.Item2, Green));
            var shot = Highlighted(doc, painted, p);

1d. Replace the whole Highlighted method (from its `/// <summary>` comment to its closing brace) with:

        /// <summary>
        /// Captures with each item painted its colour (highlight, default true). context decides what
        /// happens to the rest of the model so walls in front don't hide the items:
        ///   "isolate" (default) — temporarily hide everything except the items (fast);
        ///   "fade" — make everything else see-through (shows surroundings, slow on big models);
        ///   "none" — leave the rest as is.
        /// Colours are temporary overrides and hidden items are unhidden afterwards, so the user's
        /// own colours and hide state are untouched.
        /// </summary>
        private static JObject Highlighted(Document doc, List<(ModelItem item, Autodesk.Navisworks.Api.Color color)> painted, JObject p)
        {
            bool highlight = Bool(p, "highlight", true);
            var context = Str(p, "context", "isolate").Trim().ToLowerInvariant();
            if (!highlight || painted.Count == 0) return Snapshot(doc, p);

            var keep = painted.Select(x => x.item).ToList();
            var touched = new List<ModelItem>();
            var hidden = new List<ModelItem>();
            try
            {
                if (context == "fade")
                {
                    var roots = doc.Models.RootItems.ToList();
                    doc.Models.OverrideTemporaryTransparency(roots, 0.85);
                    touched.AddRange(roots);
                }
                else if (context == "isolate")
                {
                    hidden = OthersAlongPaths(doc, keep);
                    if (hidden.Count > 0) doc.Models.SetHidden(hidden, true);
                }
                foreach (var (item, color) in painted) Paint(doc, item, color, touched);
                var shot = Snapshot(doc, p);
                shot["context"] = context;
                return shot;
            }
            finally
            {
                if (hidden.Count > 0) doc.Models.SetHidden(hidden, false);
                if (touched.Count > 0) doc.Models.ResetTemporaryMaterials(touched);
            }
        }

Compile note: if the tuple syntax `(i, Orange)` or `foreach (var (item, color) in ...)` does not compile,
use `painted` entries via `.item` / `.color` in a normal foreach. Change nothing else.

----- TASK 2 — NavisCommandExecutor.cs: viewpoint refs "vp:<n>" -----

2a. Replace the whole CollectViewpoints method with:

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

2b. In ViewpointsList, replace

            return new JObject { ["count"] = arr.Count, ["viewpoints"] = arr };

with:

            var duplicates = arr.GroupBy(v => v.Value<string>("name") ?? "").Count(g => g.Count() > 1);
            return new JObject
            {
                ["count"] = arr.Count,
                ["duplicateNames"] = duplicates,
                ["note"] = "Use 'ref' (e.g. vp:12) to pick a viewpoint: names can repeat and many GUIDs are empty. Refs change if saved viewpoints are added, deleted or reordered.",
                ["viewpoints"] = arr,
            };

2c. In FindViewpoint, directly AFTER the line

            Recurse(doc.SavedViewpoints.ToSavedItemCollection());

insert:

            if (idOrName.StartsWith("vp:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(idOrName.Substring(3), out var idx))
            {
                if (idx >= 0 && idx < found.Count) return found[idx];
                throw new Exception($"Viewpoint ref '{idOrName}' is out of range (0–{found.Count - 1}). Use navis_viewpoints_list.");
            }

(Both CollectViewpoints and FindViewpoint walk the tree depth-first in the same order, so
vp:<n> from the list is the n-th viewpoint found here.)

----- TASK 3 — revit-mcp-server/src/tools/navis_capture_tools.ts -----

3a. In navis_capture_view's schema, change the `viewpoint` description to:
    "Saved viewpoint to apply first: its ref from navis_viewpoints_list (e.g. 'vp:12', preferred — names can repeat), or its name or GUID"
3b. Change the `displayIds` description to:
    "Zoom to these items first; they are painted orange and, by default, everything else is hidden for the capture (see context)"
3c. Change the `select` description to:
    "Also select the items in Navisworks (default true). Selection is not drawn in the image — the orange colour is."
3d. Add these two entries to navis_capture_view's schema, right after `restore`:

            highlight: z.boolean().optional().describe("Paint the displayIds items orange in the image (default true; reset after capture)"),
            context: z.enum(["isolate", "fade", "none"]).optional().describe(
                "With displayIds: isolate (default, fast) = show only those items; fade = everything else see-through " +
                "(slow on big models); none = leave as is. Restored after capture."),

Change nothing else in that file.

----- TASK 4 — revit-mcp-server/src/tools/navis_reading_tools.ts (descriptions only) -----

4a. navis_viewpoints_list description -> "List saved viewpoints. Each has a 'ref' like 'vp:12' — use it to pick a viewpoint, because names can repeat and many GUIDs are empty."
4b. navis_viewpoint_apply description -> "Apply a saved viewpoint by ref (e.g. 'vp:12' from navis_viewpoints_list, preferred), name or GUID."
    and its `viewpoint` parameter description -> "Viewpoint ref (vp:N), name or GUID"

======================================================================
4. BUILD (both must pass with 0 errors)
======================================================================
  cd revit-mcp-server && npm run build
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
If a build fails for a reason not covered by the compile note, STOP and report the exact error.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> exactly 4 modified files (the ones in rule 1), nothing new
  B. grep -n "Highlighted(doc, r, p)" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Capture.cs -> no match
  C. grep -n "vp:" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs -> at least 2 matches
  D. grep -n "ZoomToItems" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Capture.cs -> no match

(The reviewer will deploy and test live: displayIds capture shows the items in orange with nothing
blocking them; navis_viewpoints_list shows refs; capture with viewpoint "vp:<n>" picks that exact one.)

======================================================================
6. REPORT BACK
======================================================================
1. Files changed. 2. Last ~15 lines of each build. 3. Self-checks A–D.
4. Anything you could not do exactly as written, and why.
