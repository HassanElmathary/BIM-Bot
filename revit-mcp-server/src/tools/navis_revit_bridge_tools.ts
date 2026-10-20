import * as fs from "fs";
import * as path from "path";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import type { BridgeItem, ClashDetail, ClashInfo, RevitDoc } from "../utils/revitBridgeLogic.js";
import type { SlaveGroup } from "../utils/revitBridgeLogic.js";
import { csvCell, decide, decideWithSlaves, extractIds, modelKey, planActivation } from "../utils/revitBridgeLogic.js";
import { readBridgeSession } from "../utils/bridgeSession.js";
import { RevitSocketClient } from "../utils/SocketClient.js";

/**
 * Navisworks <-> Revit bridge. Orchestrates both plugin connections from the server:
 * Navisworks (8091) for clashes / item lookup, Revit (8080) for select / zoom.
 * Revit Element IDs are only unique inside ONE Revit file, so every ID is matched to the Revit
 * document it came from (Navisworks source file vs Revit document title) before it is used.
 */
const text = (t: string) => ({ content: [{ type: "text" as const, text: t }] });
const fail = (e: unknown) => text(`Failed: ${e instanceof Error ? e.message : String(e)}`);

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

/** Runs fn against the BIM-Bot listener of ONE Revit process (a bridge slave), then disconnects. */
async function withRevitAt<T>(port: number, fn: (c: RevitSocketClient) => Promise<T>): Promise<T> {
    const client = new RevitSocketClient("127.0.0.1", port);
    await new Promise<void>((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error(`Revit slave on port ${port} did not answer within 5 s.`)), 5000);
        client.rawSocket.once("connect", () => { clearTimeout(timer); resolve(); });
        client.rawSocket.once("error", (e) => { clearTimeout(timer); reject(e); });
        client.connect();
    });
    try {
        return await fn(client);
    } finally {
        try { client.disconnect(); } catch { /* already closed */ }
    }
}

/** Activates each slave's document in its own Revit window, then selects (and zooms to) its elements. */
async function selectInSlaves(groups: SlaveGroup[], isolate: boolean, zoom: boolean): Promise<unknown[]> {
    const out: unknown[] = [];
    for (const g of groups) {
        const r: Record<string, unknown> = { slot: g.slave.slot, documentTitle: g.slave.documentTitle, port: g.slave.port, elementIds: g.ids };
        try {
            await withRevitAt(g.slave.port, async (c) => {
                try {
                    r.activate = await c.sendCommand("activate_document", { documentTitle: g.slave.documentTitle });
                } catch (e) {
                    r.activate = `activate failed: ${e instanceof Error ? e.message : String(e)}`;
                }
                r.select = await c.sendCommand("select_elements", { elementIds: g.ids, isolate });
                if (zoom) {
                    try {
                        r.zoom = await c.sendCommand("zoom_to_element", { elementId: g.ids[0] });
                    } catch (e) {
                        r.zoom = `zoom failed: ${e instanceof Error ? e.message : String(e)}`;
                    }
                }
            });
        } catch (e) {
            r.error = e instanceof Error ? e.message : String(e);
        }
        out.push(r);
    }
    return out;
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
                const session = readBridgeSession();
                if (session && session.slaves.length > 0) {
                    const routed = decideWithSlaves(items, session.slaves);
                    const slaves = await selectInSlaves(routed.groups, args.isolate ?? false, args.zoom !== false);
                    return text(JSON.stringify({
                        mode: "bridge-session",
                        clash: d.clash,
                        decisions: routed.decisions,
                        slaves,
                        note: routed.groups.length === 0
                            ? "Nothing was selected: no joined Revit slave owns these items. See decisions."
                            : undefined,
                    }, null, 2));
                }
                const docs = await revitDocs();
                if (docs.length === 0) return text("Revit is not reachable or has no open document.");
                const plan = planActivation(decide(items, docs), args.activateDocument !== false);
                const decisions = plan.decisions;
                let activated: unknown = null;
                if (plan.documentToActivate) {
                    const documentTitle = plan.documentToActivate;
                    activated = await withRevitConnection((c) =>
                        c.sendCommand("activate_document", { documentTitle }));
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
        "(Revit IDs are only unique per file). Max 50 elements (the first 50 are used).",
        {
            zoom: z.boolean().optional().describe("Zoom Navisworks to the found items (default true)"),
            slot: z.string().optional().describe("Bridge slave slot to read the selection from, e.g. '02' (needed when several Revit slaves joined)"),
            sourceFile: z.string().optional().describe(
                "Override which Navisworks model to search (NWC/RVT file name). Default: the active Revit document's title. " +
                "Pass '*' to search every model."),
        },
        async (args) => {
            try {
                const session = readBridgeSession();
                if (session && session.slaves.length > 0) {
                    const slave = args.slot
                        ? session.slaves.find((s) => s.slot === args.slot)
                        : session.slaves.length === 1 ? session.slaves[0] : undefined;
                    if (!slave) {
                        return text(`The bridge has ${session.slaves.length} Revit slaves — pass slot: ` +
                            session.slaves.map((s) => `${s.slot} = '${s.documentTitle}'`).join(", "));
                    }
                    const selResp = await withRevitAt(slave.port, async (c) => {
                        try { await c.sendCommand("activate_document", { documentTitle: slave.documentTitle }); } catch { /* already active */ }
                        return c.sendCommand("get_selected_elements", {});
                    });
                    const slaveIds = extractIds(selResp).slice(0, 50);
                    if (slaveIds.length === 0) return text(`Nothing is selected in slave ${slave.slot} ('${slave.documentTitle}').`);
                    const slaveFound = await withNavisConnection((c) =>
                        c.sendCommand("navis_find_by_revit_ids", { elementIds: slaveIds, select: true, zoom: args.zoom !== false, sourceFile: slave.model }));
                    return text(JSON.stringify({
                        mode: "bridge-session", slot: slave.slot, revitDocument: slave.documentTitle,
                        model: slave.model, revitElementIds: slaveIds, navisworks: slaveFound,
                    }, null, 2));
                }
                const selResp = await withRevitConnection((c) => c.sendCommand("get_selected_elements", {}));
                const ids = extractIds(selResp).slice(0, 50);
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
        "navis_bridge_status",
        "Show the manual BIM-Bot bridge session: the Navisworks master (federated file and its models) and the " +
        "Revit slaves that joined (slot, document, model). Lists models with no slave yet, so you can tell the " +
        "user which Revit files to open and join.",
        {},
        async () => {
            try {
                const s = readBridgeSession();
                if (!s) {
                    return text("No bridge session is running. In Navisworks open the federated model and click " +
                        "Add-ins > BIM-Bot Bridge (that Navisworks becomes the master), then in each Revit model click BIM-Bot > Join Bridge.");
                }
                const joined = new Set(s.slaves.map((x) => modelKey(x.model)));
                return text(JSON.stringify({
                    sessionId: s.sessionId,
                    startedUtc: s.startedUtc,
                    master: { file: s.master.file, port: s.master.port, modelCount: s.master.models.length },
                    slaves: s.slaves.map((x) => ({ slot: x.slot, documentTitle: x.documentTitle, model: x.model, port: x.port })),
                    modelsWithoutSlave: s.master.models.map((m) => m.name).filter((n) => !joined.has(modelKey(n))),
                }, null, 2));
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
