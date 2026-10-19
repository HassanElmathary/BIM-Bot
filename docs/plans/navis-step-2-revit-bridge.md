IMPLEMENTATION PLAN — Step 2: Navisworks <-> Revit clash bridge
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge

======================================================================
0. BEFORE YOU START
======================================================================
1. git switch feat/navis-2-revit-bridge
2. git branch --show-current   -> must print feat/navis-2-revit-bridge
3. git status --short          -> must be empty. If not, STOP and report.

======================================================================
1. GOAL
======================================================================
BIM-Bot's one MCP server talks to BOTH Revit (port 8080) and Navisworks (port 8091). Use that to
close the coordination loop in one conversation:

  navis_clash_to_revit       Navisworks clash -> Revit Element IDs of both items -> select (+zoom) them in Revit
  revit_selection_to_navis   Revit selection  -> find the same elements in Navisworks -> select + zoom
  navis_recheck_clash        re-run the clash test and report whether one clash is now Resolved
  navis_clash_handoff_table  all clashes of a test as a table of Revit element-ID pairs, grouped by source file

No Revit plugin change is needed: the Revit commands select_elements {elementIds:number[], isolate?:bool},
zoom_to_element {elementId:number}, get_selected_elements and list_open_documents already exist.

Background facts (verified):
- In NWC files exported from Revit, the Revit Element ID is the property Element > Id. On the
  geometry node that a clash points at it is often on the PARENT node, so walk up a few levels.
- ItemJson(item) already returns revitElementId / revitCategory when present.
- Revit select_elements already resolves IDs in linked models too.
- Revit get_selected_elements returns elements with an "id" field (the exact wrapper shape may be
  an array or an object holding an array — parse defensively, see Task 3).

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit:
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs
   - EDIT   navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
            (ONLY add three `case` lines to the switch in ExecuteExtended)
   - CREATE revit-mcp-server/src/tools/navis_revit_bridge_tools.ts
2. Do NOT touch anything under revit-mcp-plugin/, ConnectionManager.ts, SocketClient.ts,
   navisConnectionManager.ts, any .csproj, or any other file.
3. No new packages. Do not deploy. Do not commit or push. No git reset/stash/clean/checkout -- <file>.
4. Reuse existing helpers in the partial class (do not redefine): ActiveDoc, Str, Int, Bool,
   ClashData, FindTest, CollectResults, ResultJson, ItemJson, PropText, ItemRef, RunPropertySearch,
   ItemsBox, ApplyViewpoint.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create NavisCommandExecutor.Bridge.cs with exactly this content -----

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

Compile notes (change only what the note says):
- If Model.SourceFileName does not exist, use m.FileName only.
- If RunPropertySearch has a different signature, read it in NavisCommandExecutor.cs and call it
  with the equivalent arguments (tab "Element", property "Id", text = id, not within selection).
- If ModelItemCollection.AddRange does not accept List<ModelItem>, add items one by one with Add.

----- TASK 2 — Register the commands -----
In NavisCommandExecutor.Extended.cs, inside `switch (method)` of `ExecuteExtended`, add right after
the `case "navis_view_preset": ...` line:

                case "navis_get_clash_detail": return ClashDetail(p);
                case "navis_list_clash_pairs": return ClashPairs(p);
                case "navis_find_by_revit_ids": return FindByRevitIds(p);

----- TASK 3 — Create revit-mcp-server/src/tools/navis_revit_bridge_tools.ts with exactly this content -----

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Navisworks <-> Revit bridge. Orchestrates both plugin connections from the server:
 * Navisworks (8091) for clashes / item lookup, Revit (8080) for select / zoom.
 */

interface BridgeItem {
    displayId?: string;
    displayName?: string;
    revitElementId?: string;
    revitCategory?: string;
    sourceFile?: string;
}
interface ClashDetail {
    test: string;
    clash: { id: string; name: string; status: string; distance: number };
    item1: BridgeItem | null;
    item2: BridgeItem | null;
}

const text = (t: string) => ({ content: [{ type: "text" as const, text: t }] });
const fail = (e: unknown) => text(`Failed: ${e instanceof Error ? e.message : String(e)}`);

function stripExt(f: string): string {
    return f.replace(/\.(nwc|nwd|nwf|rvt)$/i, "").toLowerCase();
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

async function revitDocTitles(): Promise<string[]> {
    try {
        const r = (await withRevitConnection((c) => c.sendCommand("list_open_documents", {}))) as {
            documents?: { title?: string }[];
        };
        return (r?.documents ?? []).map((d) => String(d.title ?? "")).filter(Boolean);
    } catch {
        return [];
    }
}

export function registerNavisRevitBridgeTools(server: McpServer) {

    server.tool(
        "navis_clash_to_revit",
        "Take one Navisworks clash, read the Revit Element IDs of both clashing items, then select them in " +
        "Revit (and zoom to the first). Needs Navisworks AND Revit open, with the Revit model that the NWC " +
        "came from (or a model linking it) active. Get clashIds from navis_get_clash_results.",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID or name"),
            zoom: z.boolean().optional().describe("Zoom Revit to the first element (default true)"),
            isolate: z.boolean().optional().describe("Isolate the two elements in the Revit view (default false)"),
        },
        async (args) => {
            try {
                const d = (await withNavisConnection((c) =>
                    c.sendCommand("navis_get_clash_detail", { test: args.test, clashId: args.clashId }))) as ClashDetail;
                const items = [d.item1, d.item2].filter((i): i is BridgeItem => !!i);
                const ids = items.map((i) => Number(i.revitElementId)).filter((n) => Number.isInteger(n) && n > 0);
                if (ids.length === 0) {
                    return text(
                        `Clash ${d.clash.name} has no Revit Element IDs on its items (not from a Revit NWC?).\n` +
                        JSON.stringify(d, null, 2));
                }
                const titles = await revitDocTitles();
                const sources = [...new Set(items.map((i) => i.sourceFile ?? "").filter(Boolean))];
                const matchedDocs = sources.filter((s) =>
                    titles.some((t) => stripExt(t).startsWith(stripExt(s)) || stripExt(s).startsWith(stripExt(t))));
                const sel = await withRevitConnection((c) =>
                    c.sendCommand("select_elements", { elementIds: ids, isolate: args.isolate ?? false }));
                let zoomed: unknown = null;
                if (args.zoom !== false) {
                    try {
                        zoomed = await withRevitConnection((c) => c.sendCommand("zoom_to_element", { elementId: ids[0] }));
                    } catch (e) {
                        zoomed = `zoom failed: ${e instanceof Error ? e.message : String(e)}`;
                    }
                }
                return text(JSON.stringify({
                    clash: d.clash,
                    revitElementIds: ids,
                    sourceFiles: sources,
                    openRevitDocuments: titles,
                    warning: sources.length > 0 && matchedDocs.length === 0
                        ? "None of the open Revit documents look like the clash's source file(s); the IDs may resolve in a different model."
                        : undefined,
                    revitSelect: sel,
                    revitZoom: zoomed,
                }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "revit_selection_to_navis",
        "Take the elements currently selected in Revit and find, select and zoom to the same elements in " +
        "Navisworks (matched by Revit Element ID = Navisworks Element > Id). Max 50 elements.",
        {
            zoom: z.boolean().optional().describe("Zoom Navisworks to the found items (default true)"),
        },
        async (args) => {
            try {
                const selResp = await withRevitConnection((c) => c.sendCommand("get_selected_elements", {}));
                const ids = extractIds(selResp).slice(0, 50);
                if (ids.length === 0) return text("Nothing is selected in Revit (or no element ids were returned).");
                const found = await withNavisConnection((c) =>
                    c.sendCommand("navis_find_by_revit_ids", { elementIds: ids, select: true, zoom: args.zoom !== false }));
                return text(JSON.stringify({ revitElementIds: ids, navisworks: found }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "navis_recheck_clash",
        "Re-run a clash test and report whether one clash is now Resolved. Use it after fixing the elements " +
        "in Revit, re-exporting the NWC, and refreshing the model in Navisworks (Home > Refresh).",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID or name to check"),
        },
        async (args) => {
            try {
                const run = await withNavisConnection((c) => c.sendCommand("navis_run_clash_test", { test: args.test }));
                let detail: ClashDetail | null = null;
                try {
                    detail = (await withNavisConnection((c) =>
                        c.sendCommand("navis_get_clash_detail", { test: args.test, clashId: args.clashId }))) as ClashDetail;
                } catch {
                    detail = null;
                }
                const status = detail?.clash.status ?? "NotFound";
                const resolved = status === "Resolved" || status === "NotFound";
                return text(JSON.stringify({
                    run,
                    clashId: args.clashId,
                    status,
                    resolved,
                    note: status === "NotFound"
                        ? "The clash no longer exists in the test results, so it is resolved."
                        : resolved ? "Navisworks marked this clash Resolved." : "Still clashing.",
                }, null, 2));
            } catch (e) { return fail(e); }
        }
    );

    server.tool(
        "navis_clash_handoff_table",
        "List clashes of a test as Revit element-ID pairs grouped by source-file pair, ready to hand to " +
        "each discipline (e.g. 'Arch.nwc vs MEP.nwc: 42 clashes').",
        {
            test: z.string().describe("Clash test name or GUID"),
            statusFilter: z.string().optional().describe("Only this status: New | Active | Reviewed | Approved | Resolved"),
            limit: z.number().optional().describe("Max clashes (default 200, max 1000)"),
        },
        async (args) => {
            try {
                const r = (await withNavisConnection((c) => c.sendCommand("navis_list_clash_pairs", args))) as {
                    test: string; total: number; matched: number; returned: number;
                    pairs: { clash: ClashDetail["clash"]; item1: BridgeItem | null; item2: BridgeItem | null }[];
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
  A. git status --short   -> exactly 2 new files (??) + M NavisCommandExecutor.Extended.cs
  B. git diff navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
     -> exactly 3 added lines, nothing removed
  C. grep -c "server.tool(" revit-mcp-server/src/tools/navis_revit_bridge_tools.ts   -> 4

(The reviewer will test live with both Revit and Navisworks open.)

======================================================================
6. REPORT BACK
======================================================================
1. Files created/changed. 2. Last ~15 lines of each build. 3. Self-checks A, B, C.
4. Anything you could not do exactly as written, and why.
