IMPLEMENTATION PLAN — Step 2b: Revit bridge fixes (ID collisions, recheck, speed, refresh, CSV)
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge (step 2 is already committed there as 9380d7a)

======================================================================
0. BEFORE YOU START
======================================================================
1. git switch feat/navis-2-revit-bridge
2. git branch --show-current   -> must print feat/navis-2-revit-bridge
3. git status --short          -> may list STEP2_BRIDGE_REPORT.md (??) and nothing else.
   If anything else is listed, STOP and report.

======================================================================
1. WHAT STEP 2 MISSED
======================================================================
G1. WRONG ELEMENT SELECTED IN REVIT (correctness bug). Revit Element IDs are only unique inside one
    Revit file. A clash between Arch.nwc and MEP.nwc has two IDs from two different files, but
    navis_clash_to_revit sends both to select_elements in the ACTIVE Revit document. Revit's
    select_elements prefers a host element with that ID, so the MEP ID can select an unrelated Arch
    element that happens to have the same number. Same problem in revit_selection_to_navis: it
    searches EVERY Navisworks model, so an ID can match elements in several NWC files.
    Fix: map every item to the Revit document it came from (source file vs open document titles),
    only send IDs that belong to the active document (or a linked model, with a warning), offer to
    activate the right document, and filter the Navisworks search to the active Revit model.
G2. FALSE "RESOLVED". navis_recheck_clash treats "clash not found" as resolved, so a mistyped clashId
    reports success. Fix: look the clash up BEFORE re-running; fail if it isn't there; report
    status before -> after.
G3. SLOW LOOKUP. navis_find_by_revit_ids runs one whole-model search PER id (up to 50 searches, all
    freezing the Navisworks UI). Fix: for more than 3 ids, one single search for "has Element > Id",
    then match in memory. Raise the cap from 50 to 500.
G4. MANUAL REFRESH. Rechecking needs Home > Refresh by hand. Fix: navis_refresh_models tries the
    plugin-accessible refresh call (by reflection, so it compiles on every version) and otherwise
    says clearly that the user must click Refresh.
G5. HAND-OFF ONLY AS TEXT. navis_clash_handoff_table can't be sent to a discipline as a file.
    Fix: optional csvPath writes the same rows as CSV.
G6. NO PER-ITEM EXPLANATION. Fix: navis_clash_to_revit returns a decision table (per item: id,
    source file, target Revit document, selected/skipped, and why).

======================================================================
2. HARD RULES
======================================================================
1. Only edit:
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs   (Tasks 1–3)
   - navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs (Task 4: ONE case line)
   - revit-mcp-server/src/tools/navis_revit_bridge_tools.ts                   (Task 5: replace whole file)
2. Do NOT touch revit-mcp-plugin/, ConnectionManager.ts, SocketClient.ts, navisConnectionManager.ts,
   any .csproj, any other file. No new packages. Do not deploy. Do not commit or push.
   No git reset/stash/clean/checkout -- <file>. Do not delete STEP2_BRIDGE_REPORT.md.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Bridge.cs: add a using -----
At the top of NavisCommandExecutor.Bridge.cs, add after `using System.Linq;`:

using System.Text.RegularExpressions;

----- TASK 2 — Bridge.cs: replace FindByRevitIds -----
Replace the WHOLE method `private static JObject FindByRevitIds(JObject p) { ... }` (from its
signature line to its closing brace) with exactly these four methods:

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
                if (src != "" && !SameModel(SourceFileOf(doc, m), src)) { otherModels++; continue; }
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

----- TASK 3 — Bridge.cs: add RefreshModels -----
Add this method inside the same partial class, after SameModel:

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

Compile notes (change only what the note says):
- If `search.FindAll(doc, false)` returns a type without .Cast<ModelItem>(), loop over it with
  `foreach (ModelItem m in ...)` and add to a List<ModelItem> instead.
- If `doc.Models` cannot be put in `new object[] { ... }`, use `(object)doc.Models`.

----- TASK 4 — Extended.cs: register the refresh command -----
Inside `switch (method)` of ExecuteExtended, directly after the line
`case "navis_find_by_revit_ids": return FindByRevitIds(p);` add:

                case "navis_refresh_models": return RefreshModels();

----- TASK 5 — Replace revit-mcp-server/src/tools/navis_revit_bridge_tools.ts with exactly this content -----

import * as fs from "fs";
import * as path from "path";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Navisworks <-> Revit bridge. Orchestrates both plugin connections from the server:
 * Navisworks (8091) for clashes / item lookup, Revit (8080) for select / zoom.
 * Revit Element IDs are only unique inside ONE Revit file, so every ID is matched to the Revit
 * document it came from (Navisworks source file vs Revit document title) before it is used.
 */

interface BridgeItem {
    displayId?: string;
    displayName?: string;
    revitElementId?: string;
    revitCategory?: string;
    sourceFile?: string;
}
interface ClashInfo { id: string; name: string; status: string; distance: number }
interface ClashDetail {
    test: string;
    clash: ClashInfo;
    item1: BridgeItem | null;
    item2: BridgeItem | null;
}
interface RevitDoc { title: string; path: string; isActive: boolean; isLinked: boolean; isFamilyDocument: boolean }
interface Decision {
    item: string;
    revitElementId: number | null;
    sourceFile: string;
    revitDocument: string;
    action: "select" | "skip" | "activate";
    reason: string;
}

const text = (t: string) => ({ content: [{ type: "text" as const, text: t }] });
const fail = (e: unknown) => text(`Failed: ${e instanceof Error ? e.message : String(e)}`);

function modelKey(name: string): string {
    return path.basename(name || "").trim().replace(/\.(nwc|nwd|nwf|rvt)$/i, "").toLowerCase();
}

/** Same rule as the plugin's SameModel: exact match, or one name is a prefix of the other. */
function sameModel(sourceFile: string, docTitle: string): boolean {
    const a = modelKey(sourceFile);
    const b = modelKey(docTitle);
    return !!a && !!b && (a === b || a.startsWith(b) || b.startsWith(a));
}

/** Pulls numeric element ids out of whatever shape get_selected_elements returns. */
function extractIds(resp: unknown): number[] {
    const pick = (v: unknown): unknown[] | null => (Array.isArray(v) ? v : null);
    const r = resp as Record<string, unknown> | unknown[] | null;
    const arr =
        pick(r) ??
        (r && typeof r === "object"
            ? pick((r as Record<string, unknown>).elements) ??
              pick((r as Record<string, unknown>).selected) ??
              pick((r as Record<string, unknown>).items) ??
              pick((r as Record<string, unknown>).elementIds)
            : null) ??
        [];
    return arr
        .map((e) => (typeof e === "number" ? e : Number((e as Record<string, unknown>)?.id)))
        .filter((n) => Number.isInteger(n) && n > 0);
}

async function revitDocs(): Promise<RevitDoc[]> {
    try {
        const r = (await withRevitConnection((c) => c.sendCommand("list_open_documents", {}))) as {
            documents?: Partial<RevitDoc>[];
        };
        return (r?.documents ?? [])
            .map((d) => ({
                title: String(d.title ?? ""),
                path: String(d.path ?? ""),
                isActive: !!d.isActive,
                isLinked: !!d.isLinked,
                isFamilyDocument: !!d.isFamilyDocument,
            }))
            .filter((d) => d.title);
    } catch {
        return [];
    }
}

async function clashDetail(test: string, clashId: string): Promise<ClashDetail> {
    return (await withNavisConnection((c) =>
        c.sendCommand("navis_get_clash_detail", { test, clashId }))) as ClashDetail;
}

/** Decide, per clash item, which Revit document its Element ID belongs to and whether to select it. */
function decide(items: BridgeItem[], docs: RevitDoc[]): Decision[] {
    const active = docs.find((d) => d.isActive);
    return items.map((it): Decision => {
        const id = Number(it.revitElementId);
        const src = it.sourceFile ?? "";
        const label = it.displayName || it.displayId || "?";
        if (!Number.isInteger(id) || id <= 0) {
            return { item: label, revitElementId: null, sourceFile: src, revitDocument: "", action: "skip", reason: "No Revit Element ID on this item (not from a Revit NWC?)." };
        }
        if (!src) {
            return { item: label, revitElementId: id, sourceFile: "", revitDocument: active?.title ?? "", action: "select", reason: "Source file unknown — assumed to be the active Revit document." };
        }
        if (active && sameModel(src, active.title)) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: active.title, action: "select", reason: "From the active Revit document." };
        }
        const linked = docs.find((d) => d.isLinked && sameModel(src, d.title));
        if (linked) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: linked.title, action: "select", reason: "From a linked model. If the active model has an element with the same ID, Revit selects that one instead — check the result." };
        }
        const other = docs.find((d) => !d.isLinked && !d.isActive && !d.isFamilyDocument && sameModel(src, d.title));
        if (other) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: other.title, action: "activate", reason: "From an open Revit document that is not active." };
        }
        return { item: label, revitElementId: id, sourceFile: src, revitDocument: "", action: "skip", reason: `Its source file '${src}' is not open in Revit (as the active model, a link, or another open document).` };
    });
}

function csvCell(v: unknown): string {
    const s = String(v ?? "");
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export function registerNavisRevitBridgeTools(server: McpServer) {

    server.tool(
        "navis_clash_to_revit",
        "Take one Navisworks clash, read the Revit Element IDs of both clashing items, work out which Revit " +
        "document each ID belongs to (IDs are only unique per Revit file), then select the right elements in " +
        "Revit and zoom to the first. Returns a per-item decision table. Needs Navisworks AND Revit open. " +
        "Get clashIds from navis_get_clash_results.",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID or name"),
            zoom: z.boolean().optional().describe("Zoom Revit to the first selected element (default true)"),
            isolate: z.boolean().optional().describe("Isolate the selected elements in the Revit view (default false)"),
            activateDocument: z.boolean().optional().describe(
                "If no item belongs to the active Revit document but they all belong to one other open document, " +
                "switch Revit to it first (default true)"),
        },
        async (args) => {
            try {
                const d = await clashDetail(args.test, args.clashId);
                const items = [d.item1, d.item2].filter((i): i is BridgeItem => !!i);
                const docs = await revitDocs();
                if (docs.length === 0) return text("Revit is not reachable or has no open document.");
                let decisions = decide(items, docs);

                let activated: unknown = null;
                const toActivate = [...new Set(decisions.filter((x) => x.action === "activate").map((x) => x.revitDocument))];
                const anySelect = decisions.some((x) => x.action === "select");
                if (!anySelect && toActivate.length === 1 && args.activateDocument !== false) {
                    activated = await withRevitConnection((c) =>
                        c.sendCommand("activate_document", { documentTitle: toActivate[0] }));
                    decisions = decisions.map((x) => x.action === "activate"
                        ? { ...x, action: "select" as const, reason: `Revit switched to '${x.revitDocument}' first.` }
                        : x);
                } else {
                    decisions = decisions.map((x) => x.action === "activate"
                        ? { ...x, action: "skip" as const, reason: `Belongs to '${x.revitDocument}', which is not the active Revit document. Activate it and run again.` }
                        : x);
                }

                const ids = decisions.filter((x) => x.action === "select" && x.revitElementId !== null).map((x) => x.revitElementId as number);
                let sel: unknown = null;
                let zoomed: unknown = null;
                if (ids.length > 0) {
                    sel = await withRevitConnection((c) =>
                        c.sendCommand("select_elements", { elementIds: ids, isolate: args.isolate ?? false }));
                    if (args.zoom !== false) {
                        try {
                            zoomed = await withRevitConnection((c) => c.sendCommand("zoom_to_element", { elementId: ids[0] }));
                        } catch (e) {
                            zoomed = `zoom failed: ${e instanceof Error ? e.message : String(e)}`;
                        }
                    }
                }
                return text(JSON.stringify({
                    clash: d.clash,
                    selectedInRevit: ids,
                    decisions,
                    activatedDocument: activated,
                    revitSelect: sel,
                    revitZoom: zoomed,
                    note: ids.length === 0 ? "Nothing was selected in Revit — see decisions for why." : undefined,
                }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "revit_selection_to_navis",
        "Take the elements currently selected in Revit and find, select and zoom to the same elements in " +
        "Navisworks. Only Navisworks models whose source file matches the active Revit document are searched " +
        "(Revit IDs are only unique per file). Max 500 elements.",
        {
            zoom: z.boolean().optional().describe("Zoom Navisworks to the found items (default true)"),
            sourceFile: z.string().optional().describe(
                "Override which Navisworks model to search (NWC/RVT file name). Default: the active Revit document's title. " +
                "Pass '*' to search every model."),
        },
        async (args) => {
            try {
                const selResp = await withRevitConnection((c) => c.sendCommand("get_selected_elements", {}));
                const ids = extractIds(selResp).slice(0, 500);
                if (ids.length === 0) return text("Nothing is selected in Revit (or no element ids were returned).");
                const active = (await revitDocs()).find((d) => d.isActive);
                const src = args.sourceFile === "*" ? "" : (args.sourceFile ?? active?.title ?? "");
                const found = await withNavisConnection((c) =>
                    c.sendCommand("navis_find_by_revit_ids", { elementIds: ids, select: true, zoom: args.zoom !== false, sourceFile: src }));
                return text(JSON.stringify({ revitDocument: active?.title ?? "", revitElementIds: ids, navisworks: found }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "navis_refresh_models",
        "Reload changed model files in Navisworks (like Home > Refresh), e.g. after re-exporting an NWC from " +
        "Revit. If this Navisworks version does not allow it from a plugin, says so — then click Refresh by hand.",
        {},
        async () => {
            try { return text(JSON.stringify(await withNavisConnection((c) => c.sendCommand("navis_refresh_models", {})), null, 2)); }
            catch (e) { return fail(e); }
        }
    );

    server.tool(
        "navis_recheck_clash",
        "Re-run a clash test and report whether one clash is now resolved (status before -> after). Use it " +
        "after fixing the elements in Revit, re-exporting the NWC and refreshing (navis_refresh_models or Home > Refresh).",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID or name to check"),
        },
        async (args) => {
            try {
                let before: ClashDetail;
                try {
                    before = await clashDetail(args.test, args.clashId);
                } catch (e) {
                    return text(`Failed: clash '${args.clashId}' was not found in test '${args.test}' BEFORE re-running, so nothing ` +
                        `can be checked. Get a valid clashId from navis_get_clash_results. (${e instanceof Error ? e.message : String(e)})`);
                }
                const run = await withNavisConnection((c) => c.sendCommand("navis_run_clash_test", { test: args.test }));
                let after: ClashDetail | null = null;
                try { after = await clashDetail(args.test, args.clashId); } catch { after = null; }
                const status = after?.clash.status ?? "NotFound";
                const resolved = status === "Resolved" || status === "NotFound";
                return text(JSON.stringify({
                    run,
                    clashId: args.clashId,
                    statusBefore: before.clash.status,
                    statusAfter: status,
                    resolved,
                    note: status === "NotFound"
                        ? "The clash existed before the re-run and is gone now, so it is resolved."
                        : resolved ? "Navisworks marked this clash Resolved." : "Still clashing.",
                }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "navis_clash_handoff_table",
        "List clashes of a test as Revit element-ID pairs grouped by source-file pair, ready to hand to each " +
        "discipline (e.g. 'Arch.nwc vs MEP.nwc: 42 clashes'). Optionally also writes a CSV file.",
        {
            test: z.string().describe("Clash test name or GUID"),
            statusFilter: z.string().optional().describe("Only this status: New | Active | Reviewed | Approved | Resolved"),
            limit: z.number().optional().describe("Max clashes (default 200, max 1000)"),
            csvPath: z.string().optional().describe("Absolute path of a .csv file to write (its folder must exist), e.g. C:\\\\Temp\\\\clashes.csv"),
        },
        async (args) => {
            try {
                const r = (await withNavisConnection((c) => c.sendCommand("navis_list_clash_pairs", {
                    test: args.test, statusFilter: args.statusFilter ?? "", limit: args.limit ?? 200,
                }))) as {
                    test: string; total: number; matched: number; returned: number;
                    pairs: { clash: ClashInfo; item1: BridgeItem | null; item2: BridgeItem | null }[];
                };
                const groups = new Map<string, string[]>();
                for (const p of r.pairs) {
                    const key = `${p.item1?.sourceFile || "?"} vs ${p.item2?.sourceFile || "?"}`;
                    const row = `${p.clash.name} [${p.clash.status}]  ${p.item1?.revitElementId || "-"} (${p.item1?.revitCategory || p.item1?.displayName || "?"})` +
                        `  <->  ${p.item2?.revitElementId || "-"} (${p.item2?.revitCategory || p.item2?.displayName || "?"})`;
                    if (!groups.has(key)) groups.set(key, []);
                    groups.get(key)!.push(row);
                }
                let out = `Test: ${r.test} — ${r.matched} matching of ${r.total} total, showing ${r.returned}\n`;
                for (const [key, rows] of groups) {
                    out += `\n## ${key}: ${rows.length} clash(es)\n` + rows.join("\n") + "\n";
                }

                if (args.csvPath) {
                    if (!path.isAbsolute(args.csvPath)) return text(`Failed: csvPath must be an absolute path.\n\n${out}`);
                    const header = ["test", "clash", "status", "distance", "file1", "revitId1", "category1", "name1",
                        "file2", "revitId2", "category2", "name2"];
                    const lines = [header.join(",")];
                    for (const p of r.pairs) {
                        lines.push([
                            r.test, p.clash.name, p.clash.status, p.clash.distance,
                            p.item1?.sourceFile, p.item1?.revitElementId, p.item1?.revitCategory, p.item1?.displayName,
                            p.item2?.sourceFile, p.item2?.revitElementId, p.item2?.revitCategory, p.item2?.displayName,
                        ].map(csvCell).join(","));
                    }
                    fs.writeFileSync(args.csvPath, "\uFEFF" + lines.join("\r\n") + "\r\n", "utf8");
                    out += `\nCSV written: ${args.csvPath} (${r.pairs.length} rows)\n`;
                }
                return text(out);
            } catch (e) { return fail(e); }
        }
    );
}

======================================================================
4. BUILD (both must pass with 0 errors)
======================================================================
  cd revit-mcp-server && npm run build
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
If a build fails for a reason not covered by the compile notes, STOP and report the exact error.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> exactly: M NavisCommandExecutor.Bridge.cs, M NavisCommandExecutor.Extended.cs,
     M navis_revit_bridge_tools.ts, and ?? STEP2_BRIDGE_REPORT.md (unchanged)
  B. git diff navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs -> exactly 1 added line
  C. grep -c "server.tool(" revit-mcp-server/src/tools/navis_revit_bridge_tools.ts -> 5
  D. grep -n "ItemsWithRevitId\|SameModel\|RefreshModels" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs
     -> all three names present

(The reviewer will test live with Revit and Navisworks open: a clash between two different source
files selects only the element(s) from the active Revit model and explains the other; a mistyped
clashId in navis_recheck_clash fails instead of reporting "resolved".)

======================================================================
6. REPORT BACK
======================================================================
1. Files changed. 2. Last ~15 lines of each build. 3. Self-checks A–D.
4. Anything you could not do exactly as written (including which compile notes you used), and why.
