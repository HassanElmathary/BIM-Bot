IMPLEMENTATION PLAN — Step 2d: Bridge works on federated NWD models (real source file per item)
Audience: coding agent (Nemotron in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge (steps 2, 2b, 2c are committed there)

IMPORTANT: read THIS file from disk with your file-read tool and copy code character for character
(it contains C# tuples, TypeScript arrow functions and template literals that must be kept exactly).

======================================================================
0. BEFORE YOU START
======================================================================
1. git branch --show-current   -> must print feat/navis-2-revit-bridge (do NOT switch branches)
2. git status --short          -> may list only the untracked STEP2*_REPORT.md files and this plan.
   If anything is MODIFIED (M), STOP and report.

======================================================================
1. WHAT THE LIVE TEST FOUND (Navisworks 2024, real federated model, 2026-10-18)
======================================================================
The model is ONE federated NWD that contains the discipline NWC files. Tree (verified live):

  p:0          "QC…-FED-…-00001.nwd"   Item>Internal Type = LcOaPartition   (the federated NWD)
  p:0/4        "QC…-ELE-…-10002.nwc"   Item>Internal Type = LcOaPartition   Item>Source File Name = "…\QC…-ELE-…-10002_m.ali4ZHSN.rvt"
  p:0/4/1      "Cable Trays"           Item>Internal Type = LcRevitCollection  (Revit category)
  p:0/9/32     "L27"                   Item>Internal Type = LcRevitLayer       (Revit LEVEL — has its own Element ID!)

Bugs:
B1. SourceFileOf() returns the ROOT model's file ("20260401_FEDERATED MODEL.nwd") for every item, so
    both items of every clash get the same sourceFile. Then navis_clash_to_revit can match no Revit
    document (all items skipped), revit_selection_to_navis filters everything out, and the hand-off
    table groups everything as "FEDERATED MODEL.nwd vs FEDERATED MODEL.nwd".
    Fix: the item's source is the NEAREST ancestor whose Item>Internal Type is "LcOaPartition"
    (the appended NWC). Also return the original Revit file from its Item>Source File Name.
B2. RevitIdUp() walks up to 4 parents looking for an Element Id. It can climb into a Level node
    (LcRevitLayer), a category node (LcRevitCollection) or a file node, which would return the
    LEVEL's Element ID instead of the element's. Fix: stop at those structural nodes.
B3. revitCategory is empty for some items (pipe fittings). Fix: fall back to the nearest
    LcRevitCollection ancestor's name (that is the Revit category).

======================================================================
2. HARD RULES
======================================================================
1. Only edit:
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs   (Tasks 1–4)
   - revit-mcp-server/src/utils/revitBridgeLogic.ts                         (Task 5)
   - revit-mcp-server/test/revit-bridge-logic.test.mjs                      (Task 6: append one test)
2. Touch nothing else. No new packages. Do not deploy. Do not commit or push.
   No git reset/stash/clean/checkout. Do not delete the STEP2* report files.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Bridge.cs: add structural-node helpers -----
Directly ABOVE the line `private static (string id, ModelItem carrier) RevitIdUp(ModelItem item)`
(and its doc comment), insert:

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

----- TASK 2 — Bridge.cs: replace RevitIdUp -----
Replace the WHOLE method RevitIdUp (signature to closing brace) with:

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

----- TASK 3 — Bridge.cs: replace BridgeItemJson -----
Replace the WHOLE method BridgeItemJson (signature to closing brace) with:

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

----- TASK 4 — Bridge.cs: FindByRevitIds uses the real source -----
In FindByRevitIds, replace the line

                if (src != "" && !SameModel(SourceFileOf(doc, m), src)) { otherModels++; continue; }

with

                if (src != "" && !MatchesSource(doc, m, src)) { otherModels++; continue; }

Change nothing else in Bridge.cs. Keep the existing SourceFileOf method (SourceInfoOf falls back to it).

Compile note: if `PropText` is not visible from Bridge.cs, it is a private static in the same partial
class — it is visible; do not redefine it. If the tuple deconstruction `var (file, rvt) = ...` fails,
use `var info = SourceInfoOf(doc, item); var file = info.file; var rvt = info.revitFile;`.

----- TASK 5 — revitBridgeLogic.ts: match on the original Revit file too -----
5a. In `export interface BridgeItem`, add this member after `sourceFile?: string;`:

    revitSourceFile?: string;

5b. In `export function decide(...)`, directly below the line
        const label = it.displayName || it.displayId || "?";
    add:

        const rsrc = it.revitSourceFile ?? "";
        const fits = (title: string) => sameModel(src, title) || (!!rsrc && sameModel(rsrc, title));

5c. In the same function, replace exactly these three expressions:
        sameModel(src, active.title)   ->   fits(active.title)
        sameModel(src, d.title)        ->   fits(d.title)        (it appears TWICE: in `linked` and in `other`)
    Change nothing else in the file.

----- TASK 6 — append one test to revit-mcp-server/test/revit-bridge-logic.test.mjs -----
Append at the END of the file:

test("decide: federated NWD items match by discipline NWC or original RVT name", () => {
    const docs = [doc("QC-ELE-10002_m.ali4ZHSN", { isActive: true }), doc("QC-FPR-10002", { isLinked: true })];
    const d = decide([
        { revitElementId: "9276566", sourceFile: "QC-ELE-10002.nwc", revitSourceFile: "QC-ELE-10002_m.ali4ZHSN.rvt", displayName: "tray" },
        { revitElementId: "2155309", sourceFile: "Other.nwc", revitSourceFile: "QC-FPR-10002_k.ibrahim33RDB.rvt", displayName: "fitting" },
        { revitElementId: "1", sourceFile: "20260401_FEDERATED MODEL.nwd", displayName: "rootonly" },
    ], docs);
    assert.deepEqual(d.map((x) => x.action), ["select", "select", "skip"]);
    assert.equal(d[0].revitDocument, "QC-ELE-10002_m.ali4ZHSN");
    assert.equal(d[1].revitDocument, "QC-FPR-10002");
});

======================================================================
4. BUILD + TEST (all must pass)
======================================================================
  cd revit-mcp-server && npm run build && npm test
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release

npm test must report 11 passing, 0 failing. If a test fails, report the FULL output. Never change
logic to make a test pass — report it instead.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> modified: NavisCommandExecutor.Bridge.cs, revitBridgeLogic.ts,
     revit-bridge-logic.test.mjs ; plus the untracked report/plan .md files. Nothing else.
  B. grep -n "SourceInfoOf\|IsStructuralNode\|MatchesSource" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs
     -> all three present
  C. grep -c "fits(" revit-mcp-server/src/utils/revitBridgeLogic.ts -> 3

======================================================================
6. REPORT BACK
======================================================================
Write STEP2D_FEDERATED_SOURCES_REPORT.md in the repo root with: 1. files changed, 2. last ~10 lines of
each build and the FULL npm test output, 3. self-checks A–C, 4. anything not done exactly as written.
