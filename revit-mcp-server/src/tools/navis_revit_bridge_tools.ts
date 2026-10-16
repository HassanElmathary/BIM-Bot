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
interface ClashInfo {
    id: string;
    name: string;
    status: string;
    distance: number;
}
interface ClashDetail {
    test: string;
    clash: ClashInfo;
    item1: BridgeItem | null;
    item2: BridgeItem | null;
}
interface RevitDoc {
    title: string;
    path: string;
    isActive: boolean;
    isLinked: boolean;
    isFamilyDocument: boolean;
}
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
    return (await withNavisConnection((c) => c.sendCommand("navis_get_clash_detail", { test, clashId }))) as ClashDetail;
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
            csvPath: z.string().optional().describe("Absolute path of a .csv file to write (its folder must exist), e.g. C:\\Temp\\clashes.csv"),
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
