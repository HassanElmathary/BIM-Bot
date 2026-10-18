IMPLEMENTATION PLAN — Clash Snapshots b: frame the clash point, see-through green
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Work folder: C:\bbsnap   (git worktree, branch feat/navis-clash-snapshots, HEAD at or after aa794fb)

======================================================================
0. BEFORE YOU START
======================================================================
1. cd C:\bbsnap        <- do ALL work here. Never run git switch/checkout in the main repo folder.
2. git branch --show-current   -> must print feat/navis-clash-snapshots
3. git status --short          -> must be empty. If not, STOP and report.

======================================================================
1. PROBLEM (seen live)
======================================================================
Clash images zoom to the box around BOTH whole items. When a short red pipe crosses a long green duct,
the duct fills the frame and the red item is a tiny sliver or invisible (e.g. Clash6183, Clash6190).
When the red item sits inside the green one, the opaque green hides it.

Fix:
A. Zoom to the clash itself: ClashResult.ViewBounds (the box Clash Detective uses), falling back to
   ClashResult.BoundingBox, then to the old ItemsBox(items). Both properties exist in the 2024 API
   (verified by reflection).
B. Draw the GREEN (second) item 50% see-through in clash captures, so a red item inside it shows.
   Controlled by a new option seeThroughSecond (default true for clashes; not used for displayIds).

======================================================================
2. HARD RULES
======================================================================
1. Only edit:
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Capture.cs
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Snapshots.cs
   - revit-mcp-server/src/tools/navis_capture_tools.ts   (one new optional parameter)
2. Touch nothing else. No new packages. Do not deploy. Do not commit or push.
   No git reset / stash / clean / checkout -- <file>.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Capture.cs: add the zoom helper -----
Add this method directly ABOVE the line `        private static void Paint(Document doc, ModelItem item, ...`:

        /// <summary>
        /// Box to zoom to for a clash: Clash Detective's own ViewBounds, else the clash's
        /// BoundingBox, else the box around both items (old behaviour).
        /// </summary>
        private static BoundingBox3D ClashZoomBox(ClashResult r, List<ModelItem> items)
        {
            try
            {
                var vb = r.ViewBounds;
                if (vb != null && !vb.IsEmpty) return vb;
            }
            catch { }
            try
            {
                var bb = r.BoundingBox;
                if (bb != null && !bb.IsEmpty) return bb;
            }
            catch { }
            return ItemsBox(items);
        }

----- TASK 2 — Capture.cs: use it in CaptureClash -----
In CaptureClash ONLY (the occurrence around line 89, inside CaptureClash — NOT the one in CaptureView
around line 48), replace

            vp.ZoomBox(ItemsBox(items));

with

            vp.ZoomBox(ClashZoomBox(r, items));

Then, in CaptureClash, directly ABOVE the line

            var shot = Highlighted(doc, painted, p);

insert:

            if (p["seeThroughSecond"] == null) p["seeThroughSecond"] = true;

----- TASK 3 — Capture.cs: see-through second item in Highlighted -----
In Highlighted, replace the line

                foreach (var (item, color) in painted) Paint(doc, item, color, touched);

with

                foreach (var (item, color) in painted) Paint(doc, item, color, touched);
                if (painted.Count == 2 && Bool(p, "seeThroughSecond", false))
                    doc.Models.OverrideTemporaryTransparency(new[] { painted[1].item }, 0.5);

(The item is already in `touched`, so the existing finally block resets its transparency.)

----- TASK 4 — Snapshots.cs: same zoom + see-through -----
4a. Replace

                        vp.ZoomBox(ItemsBox(items));

    with

                        vp.ZoomBox(ClashZoomBox(r, items));

4b. Replace

            var opts = new JObject { ["width"] = width, ["height"] = height, ["format"] = "jpeg" };

    with

            var opts = new JObject { ["width"] = width, ["height"] = height, ["format"] = "jpeg", ["seeThroughSecond"] = true };

----- TASK 5 — navis_capture_tools.ts: expose the option on navis_capture_clash -----
In the navis_capture_clash schema, directly after the `context:` entry, add:

            seeThroughSecond: z.boolean().optional().describe("Draw the green item 50% see-through so a red item inside it is visible (default true)"),

Change nothing else.

======================================================================
4. BUILD (in C:\bbsnap, both must pass with 0 errors)
======================================================================
  cd C:\bbsnap\revit-mcp-server && npm run build
  cd C:\bbsnap && dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
If a build fails, STOP and report the exact error.

======================================================================
5. SELF-CHECKS (in C:\bbsnap)
======================================================================
  A. git status --short -> exactly 3 modified files, nothing new
  B. findstr /n /c:"ClashZoomBox" navis-mcp-plugin\BIMBotNavisPlugin\Core\NavisCommandExecutor.Capture.cs navis-mcp-plugin\BIMBotNavisPlugin\Core\NavisCommandExecutor.Snapshots.cs
     -> 3 matches (definition + CaptureClash + Snapshots)
  C. findstr /n /c:"ZoomBox(ItemsBox(items))" navis-mcp-plugin\BIMBotNavisPlugin\Core\NavisCommandExecutor.Capture.cs
     -> exactly 1 match (the CaptureView one stays)

======================================================================
6. REPORT BACK
======================================================================
Write C:\bbsnap\clash-snapshots-b-report.md with: files changed, last ~15 lines of each build,
self-checks A–C, and anything you could not do exactly as written.
