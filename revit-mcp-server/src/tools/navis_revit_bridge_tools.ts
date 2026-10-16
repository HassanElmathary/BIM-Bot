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
