IMPLEMENTATION PLAN — Step 2e: Manual bridge session — Navisworks MASTER button + server support
Audience: coding agent (Nemotron in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge

IMPORTANT: read THIS file from disk with your file-read tool. Every code block is between a
"----- BEGIN <name> -----" and "----- END <name> -----" line; copy what is between them character
for character (C# tuples, TypeScript generics like Promise<T> and template literals with backticks).

======================================================================
0. BEFORE YOU START
======================================================================
1. git branch --show-current   -> must print feat/navis-2-revit-bridge (do NOT switch branches)
2. git status --short          -> nothing may be MODIFIED (M). Untracked report .md files are fine.
3. Step 2d must already be in the code. Check:
     grep -c "revitSourceFile" revit-mcp-server/src/utils/revitBridgeLogic.ts   -> must be 2 or more
     grep -c "SourceInfoOf" navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs -> 1 or more
   If either is 0, STOP and report "step 2d is missing".

======================================================================
1. WHAT WE ARE BUILDING
======================================================================
A manual pairing between ONE Navisworks (the MASTER, normally a federated NWD) and one or more Revit
documents (SLAVES 01, 02, …), possibly in several Revit windows. They share one file:

  %LOCALAPPDATA%\BIMBot\bridge-session.json
  {
    "version": 1,
    "sessionId": "a1b2c3d4e5f6",
    "startedUtc": "2026-10-19T08:00:00.0000000Z",
    "master": { "pid": 49032, "port": 8091, "file": "QC…-FED-…-00001.nwd",
                "models": [ { "name": "QC…-ELE-…-10002.nwc", "revitFile": "QC…-ELE-…-10002_m.ali4ZHSN.rvt" }, … ] },
    "slaves": [ { "slot": "01", "pid": 1234, "port": 8080, "documentTitle": "QC…-ELE-…-10002_hassan",
                  "documentPath": "C:\\…\\QC…-ELE-…-10002_hassan.rvt", "model": "QC…-ELE-…-10002.nwc",
                  "joinedUtc": "…" } ]
  }

- THIS step: the Navisworks "BIM-Bot Bridge" button (Add-ins tab) creates/ends the session and lists the
  models; the MCP server reads the session and routes each clash element to the slave that owns its model.
- Step 2f (separate plan): the Revit "Join Bridge" button adds/removes slaves.
- When no session is running, every tool keeps today's behaviour (name matching).

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit:
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisBridgeSession.cs
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/BIMBotBridgeMaster.cs
   - EDIT   navis-mcp-plugin/BIMBotNavisPlugin/BIMBotAutoStart.cs            (Task 3: one line)
   - CREATE revit-mcp-server/src/utils/bridgeSession.ts
   - EDIT   revit-mcp-server/src/utils/revitBridgeLogic.ts                   (Task 5: append)
   - EDIT   revit-mcp-server/src/tools/navis_revit_bridge_tools.ts           (Task 6)
   - EDIT   revit-mcp-server/test/revit-bridge-logic.test.mjs                (Task 7: append)
   - EDIT   revit-mcp-server/package.json                                    (Task 8: test script)
2. Touch nothing else (nothing under revit-mcp-plugin/ — that is step 2f). No new packages.
   Do not deploy. Do not commit or push. No git reset/stash/clean/checkout.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisBridgeSession.cs -----

----- BEGIN NavisBridgeSession.cs -----
using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Manual bridge session: this Navisworks is the MASTER, Revit documents join as SLAVES.
    /// Shared through %LocalAppData%\BIMBot\bridge-session.json. Navisworks creates it from the
    /// "BIM-Bot Bridge" button and lists the models of the open (federated) file; Revit's
    /// "Join Bridge" button adds slaves; the MCP server reads it to route clash elements.
    /// </summary>
    public static class NavisBridgeSession
    {
        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "bridge-session.json");

        private static int Pid => Process.GetCurrentProcess().Id;

        public static JObject? Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JObject.Parse(File.ReadAllText(FilePath));
            }
            catch { return null; }
        }

        public static bool IsActiveForThisProcess() => Read()?["master"]?["pid"]?.Value<int>() == Pid;

        /// <summary>Starts a new session with this Navisworks as master (replaces any previous session).</summary>
        public static JObject Start(Document doc)
        {
            var models = ListModels(doc);
            var session = new JObject
            {
                ["version"] = 1,
                ["sessionId"] = Guid.NewGuid().ToString("N").Substring(0, 12),
                ["startedUtc"] = DateTime.UtcNow.ToString("o"),
                ["master"] = new JObject
                {
                    ["pid"] = Pid,
                    ["port"] = NavisPluginApp.ActivePort,
                    ["file"] = Path.GetFileName(doc.FileName ?? ""),
                    ["models"] = models,
                },
                ["slaves"] = new JArray(),
            };
            Write(session);
            NavisLogger.Log($"Bridge session {session["sessionId"]} started as master ({models.Count} models)");
            return session;
        }

        /// <summary>Ends the session if this Navisworks owns it.</summary>
        public static void Stop()
        {
            try
            {
                if (!IsActiveForThisProcess()) return;
                File.Delete(FilePath);
                NavisLogger.Log("Bridge session stopped");
            }
            catch { }
        }

        /// <summary>
        /// Discipline models: the file nodes (Item > Internal Type = LcOaPartition) directly under each
        /// model root — the NWCs inside a federated NWD — or the model root itself when it has none.
        /// </summary>
        public static JArray ListModels(Document doc)
        {
            var arr = new JArray();
            foreach (Model m in doc.Models)
            {
                var root = m.RootItem;
                if (root == null) continue;
                bool any = false;
                foreach (ModelItem child in root.Children)
                {
                    if (Prop(child, "Item", "Internal Type") != "LcOaPartition") continue;
                    arr.Add(ModelJson(child));
                    any = true;
                }
                if (!any) arr.Add(ModelJson(root));
            }
            return arr;
        }

        private static JObject ModelJson(ModelItem file)
        {
            var src = Prop(file, "Item", "Source File Name");
            var name = file.DisplayName ?? "";
            if (name == "" && src != "") name = Path.GetFileName(src);
            return new JObject
            {
                ["name"] = name,
                ["revitFile"] = src.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(src) : "",
            };
        }

        private static string Prop(ModelItem item, string tab, string property)
        {
            try
            {
                return item.PropertyCategories.FindPropertyByDisplayName(tab, property)?.Value?.ToDisplayString() ?? "";
            }
            catch { return ""; }
        }

        private static void Write(JObject session)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, session.ToString());
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
    }
}
----- END NavisBridgeSession.cs -----

----- TASK 2 — Create navis-mcp-plugin/BIMBotNavisPlugin/BIMBotBridgeMaster.cs -----

----- BEGIN BIMBotBridgeMaster.cs -----
using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// "BIM-Bot Bridge" button (Add-ins tab). Click once: this Navisworks becomes the MASTER of a manual
    /// bridge session and lists the models of the open (federated) file, so Revit documents can join as
    /// slaves (Revit: BIM-Bot > Join Bridge). Click again: ends the session.
    /// </summary>
    [Plugin("BIMBotBridge", "BIMB",
        DisplayName = "BIM-Bot Bridge",
        ToolTip = "Start (or stop) a manual Navisworks-Revit bridge with this Navisworks as master. Then click Join Bridge in each Revit model.")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class BIMBotBridgeMaster : AddInPlugin
    {
        public override int Execute(string[] parameters)
        {
            try
            {
                if (NavisBridgeSession.IsActiveForThisProcess())
                {
                    var stop = MessageBox.Show(
                        "The BIM-Bot bridge is running with this Navisworks as MASTER.\n\nStop it? Revit slaves will be disconnected.",
                        "BIM-Bot Bridge", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (stop == DialogResult.Yes)
                    {
                        NavisBridgeSession.Stop();
                        MessageBox.Show("Bridge stopped.", "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    MessageBox.Show("Open the federated model first, then click BIM-Bot Bridge.",
                        "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                if (!NavisPluginApp.IsServiceRunning)
                {
                    NavisMainThread.Init();
                    NavisPluginApp.StartService(manual: true);
                }

                var session = NavisBridgeSession.Start(doc);
                var names = ((JArray)session["master"]!["models"]!).Select(m => "  • " + (string?)m["name"]).ToList();
                var shown = string.Join("\n", names.Take(15)) + (names.Count > 15 ? $"\n  … and {names.Count - 15} more" : "");
                MessageBox.Show(
                    $"Bridge started — this Navisworks is the MASTER.\n\n{names.Count} model(s):\n{shown}\n\n" +
                    "Now open each Revit model you want to work in and click BIM-Bot > Join Bridge.",
                    "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Bridge master button failed", ex);
                MessageBox.Show($"BIM-Bot Bridge failed: {ex.Message}", "BIM-Bot Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
----- END BIMBotBridgeMaster.cs -----

----- TASK 3 — BIMBotAutoStart.cs: end the session when Navisworks closes -----
In OnUnloading(), change

            try { NavisPluginApp.StopService(); }

to

            try { NavisBridgeSession.Stop(); NavisPluginApp.StopService(); }

Change nothing else.

----- TASK 4 — Create revit-mcp-server/src/utils/bridgeSession.ts -----

----- BEGIN bridgeSession.ts -----
import * as fs from "fs";
import * as os from "os";
import * as path from "path";

/**
 * Manual bridge session shared by the Navisworks master button, the Revit "Join Bridge" button and
 * this server: %LOCALAPPDATA%\BIMBot\bridge-session.json.
 */

export interface BridgeModel {
    name: string;
    revitFile?: string;
}

export interface BridgeSlave {
    slot: string;
    pid: number;
    port: number;
    documentTitle: string;
    documentPath?: string;
    model: string;
    joinedUtc?: string;
}

export interface BridgeSession {
    version: number;
    sessionId: string;
    startedUtc: string;
    master: { pid: number; port: number; file: string; models: BridgeModel[] };
    slaves: BridgeSlave[];
}

export function bridgeSessionPath(): string {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "BIMBot", "bridge-session.json");
}

function pidAlive(pid: unknown): boolean {
    const id = Number(pid);
    if (!Number.isInteger(id) || id <= 0) return false;
    try {
        process.kill(id, 0);
        return true;
    } catch (err) {
        return (err as NodeJS.ErrnoException)?.code === "EPERM";
    }
}

/**
 * The live bridge session, or null when none is running (no file, unreadable, or the Navisworks
 * master has exited). Slaves whose Revit process has exited are dropped.
 */
export function readBridgeSession(): BridgeSession | null {
    try {
        const raw = fs.readFileSync(bridgeSessionPath(), "utf8");
        const s = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw) as BridgeSession;
        if (!s?.master || !pidAlive(s.master.pid)) return null;
        s.master.models = Array.isArray(s.master.models) ? s.master.models : [];
        s.slaves = (Array.isArray(s.slaves) ? s.slaves : [])
            .filter((x) => pidAlive(x.pid) && Number.isInteger(x.port) && x.port > 0);
        return s;
    } catch {
        return null;
    }
}
----- END bridgeSession.ts -----

----- TASK 5 — revitBridgeLogic.ts: append slave routing -----
Append at the END of revit-mcp-server/src/utils/revitBridgeLogic.ts:

----- BEGIN revitBridgeLogic-append -----

/** A joined Revit document (bridge slave) as far as routing needs it. */
export interface SlaveTarget {
    slot: string;
    port: number;
    documentTitle: string;
    model: string;
}

export interface SlaveDecision {
    item: string;
    revitElementId: number | null;
    sourceFile: string;
    slot: string | null;
    action: "select" | "skip";
    reason: string;
}

export interface SlaveGroup {
    slave: SlaveTarget;
    ids: number[];
}

/**
 * Which joined slave owns an item: exact model name first, then the name rule, then the item's
 * original RVT vs the slave's document title. Null when no slave owns it.
 */
export function findSlave(item: BridgeItem, slaves: SlaveTarget[]): SlaveTarget | null {
    const src = item.sourceFile ?? "";
    const rsrc = item.revitSourceFile ?? "";
    const key = modelKey(src);
    return slaves.find((s) => key !== "" && modelKey(s.model) === key)
        ?? slaves.find((s) => sameModel(src, s.model))
        ?? (rsrc ? slaves.find((s) => sameModel(rsrc, s.documentTitle)) : undefined)
        ?? null;
}

/** Routes every clash item to the slave that owns it; ids are grouped per slave (no duplicates). */
export function decideWithSlaves(items: BridgeItem[], slaves: SlaveTarget[]): { decisions: SlaveDecision[]; groups: SlaveGroup[] } {
    const decisions: SlaveDecision[] = [];
    const groups = new Map<string, SlaveGroup>();
    for (const it of items) {
        const id = Number(it.revitElementId);
        const src = it.sourceFile ?? "";
        const label = it.displayName || it.displayId || "?";
        if (!Number.isInteger(id) || id <= 0) {
            decisions.push({ item: label, revitElementId: null, sourceFile: src, slot: null, action: "skip", reason: "No Revit Element ID on this item (not from a Revit NWC?)." });
            continue;
        }
        const slave = findSlave(it, slaves);
        if (!slave) {
            decisions.push({ item: label, revitElementId: id, sourceFile: src, slot: null, action: "skip", reason: `No Revit slave has joined the bridge for '${src || "?"}'. Open that model in Revit and click BIM-Bot > Join Bridge.` });
            continue;
        }
        decisions.push({ item: label, revitElementId: id, sourceFile: src, slot: slave.slot, action: "select", reason: `Slave ${slave.slot}: '${slave.documentTitle}'.` });
        const group = groups.get(slave.slot) ?? { slave, ids: [] };
        if (!group.ids.includes(id)) group.ids.push(id);
        groups.set(slave.slot, group);
    }
    return { decisions, groups: [...groups.values()] };
}
----- END revitBridgeLogic-append -----

----- TASK 6 — navis_revit_bridge_tools.ts -----

6a. Replace the line
        import { csvCell, decide, extractIds, planActivation } from "../utils/revitBridgeLogic.js";
    with these three lines:

----- BEGIN imports -----
import type { SlaveGroup } from "../utils/revitBridgeLogic.js";
import { csvCell, decide, decideWithSlaves, extractIds, modelKey, planActivation } from "../utils/revitBridgeLogic.js";
import { readBridgeSession } from "../utils/bridgeSession.js";
import { RevitSocketClient } from "../utils/SocketClient.js";
----- END imports -----

6b. Directly ABOVE the line `export function registerNavisRevitBridgeTools(server: McpServer) {` insert:

----- BEGIN helpers -----
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
----- END helpers -----

6c. In the navis_clash_to_revit handler, directly BELOW the line
        const items = [d.item1, d.item2].filter((i): i is BridgeItem => !!i);
    insert:

----- BEGIN clash-session -----
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
----- END clash-session -----

6d. In the revit_selection_to_navis tool's schema object, add this member directly after the
    `zoom: ...` member:

----- BEGIN selection-slot-arg -----
            slot: z.string().optional().describe("Bridge slave slot to read the selection from, e.g. '02' (needed when several Revit slaves joined)"),
----- END selection-slot-arg -----

6e. In the revit_selection_to_navis handler, directly BELOW its `try {` line insert:

----- BEGIN selection-session -----
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
                    const slaveIds = extractIds(selResp).slice(0, 500);
                    if (slaveIds.length === 0) return text(`Nothing is selected in slave ${slave.slot} ('${slave.documentTitle}').`);
                    const slaveFound = await withNavisConnection((c) =>
                        c.sendCommand("navis_find_by_revit_ids", { elementIds: slaveIds, select: true, zoom: args.zoom !== false, sourceFile: slave.model }));
                    return text(JSON.stringify({
                        mode: "bridge-session", slot: slave.slot, revitDocument: slave.documentTitle,
                        model: slave.model, revitElementIds: slaveIds, navisworks: slaveFound,
                    }, null, 2));
                }
----- END selection-session -----

6f. Directly ABOVE the line `    server.tool(` that starts the "navis_refresh_models" tool, insert this tool:

----- BEGIN status-tool -----
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

----- END status-tool -----

Change nothing else in the tools file.

----- TASK 7 — append tests to revit-mcp-server/test/revit-bridge-logic.test.mjs -----
Append at the END of the file:

----- BEGIN tests-append -----

test("decideWithSlaves routes each clash element to the slave that owns its model", () => {
    const { decideWithSlaves } = logic;
    const slaves = [
        { slot: "01", port: 8080, documentTitle: "QC-ELE-10002_hassan", model: "QC-ELE-10002.nwc" },
        { slot: "02", port: 8081, documentTitle: "QC-FPR-10002_hassan", model: "QC-FPR-10002.nwc" },
    ];
    const r = decideWithSlaves([
        { revitElementId: "9276566", sourceFile: "QC-ELE-10002.nwc", displayName: "tray" },
        { revitElementId: "2155309", sourceFile: "QC-FPR-10002.nwc", displayName: "fitting" },
        { revitElementId: "777", sourceFile: "QC-STR-10002.nwc", displayName: "beam" },
        { revitElementId: "", sourceFile: "QC-ELE-10002.nwc", displayName: "noid" },
    ], slaves);
    assert.deepEqual(r.decisions.map((x) => x.action), ["select", "select", "skip", "skip"]);
    assert.deepEqual(r.decisions.map((x) => x.slot), ["01", "02", null, null]);
    assert.match(r.decisions[2].reason, /Join Bridge/);
    assert.deepEqual(r.groups.map((g) => [g.slave.slot, g.ids]), [["01", [9276566]], ["02", [2155309]]]);
});

test("decideWithSlaves falls back to the original RVT vs the slave document title, and de-duplicates ids", () => {
    const { decideWithSlaves } = logic;
    const slaves = [{ slot: "01", port: 8080, documentTitle: "QC-ELE-10002_m.ali4ZHSN", model: "Renamed.nwc" }];
    const r = decideWithSlaves([
        { revitElementId: "5", sourceFile: "Other.nwc", revitSourceFile: "QC-ELE-10002_m.ali4ZHSN.rvt" },
        { revitElementId: "5", sourceFile: "Other.nwc", revitSourceFile: "QC-ELE-10002_m.ali4ZHSN.rvt" },
    ], slaves);
    assert.deepEqual(r.decisions.map((x) => x.slot), ["01", "01"]);
    assert.deepEqual(r.groups[0].ids, [5]);
});

test("readBridgeSession: null without a live master, drops dead slaves", async () => {
    const fs = await import("node:fs");
    const os = await import("node:os");
    const path = await import("node:path");
    const { readBridgeSession } = await import(new URL("../build/utils/bridgeSession.js", import.meta.url).href);
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "bimbot-bridge-"));
    const old = process.env.LOCALAPPDATA;
    process.env.LOCALAPPDATA = tmp;
    try {
        assert.equal(readBridgeSession(), null);
        fs.mkdirSync(path.join(tmp, "BIMBot"));
        const file = path.join(tmp, "BIMBot", "bridge-session.json");
        const base = { version: 1, sessionId: "x", startedUtc: "", master: { pid: process.pid, port: 8091, file: "F.nwd", models: [{ name: "A.nwc" }] } };
        fs.writeFileSync(file, JSON.stringify({ ...base, slaves: [
            { slot: "01", pid: process.pid, port: 8080, documentTitle: "A", model: "A.nwc" },
            { slot: "02", pid: 999999, port: 8081, documentTitle: "B", model: "B.nwc" },
        ] }));
        const s = readBridgeSession();
        assert.equal(s.slaves.length, 1);
        assert.equal(s.slaves[0].slot, "01");
        fs.writeFileSync(file, JSON.stringify({ ...base, master: { ...base.master, pid: 999999 }, slaves: [] }));
        assert.equal(readBridgeSession(), null);
    } finally {
        if (old === undefined) delete process.env.LOCALAPPDATA; else process.env.LOCALAPPDATA = old;
        fs.rmSync(tmp, { recursive: true, force: true });
    }
});
----- END tests-append -----

----- TASK 8 — revit-mcp-server/package.json -----
No change is needed if "test" is "node --test test/revit-bridge-logic.test.mjs" — the new tests are in
that same file. Leave package.json untouched.

======================================================================
4. BUILD + TEST (all must pass)
======================================================================
  cd revit-mcp-server && npm run build && npm test
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release

npm test must report 14 passing, 0 failing (11 from steps 2c/2d + 3 new). Report the FULL output if not.
Compile notes (change only what the note says):
- C#: if `Application` is ambiguous in BIMBotBridgeMaster.cs, it is already fully qualified as
  Autodesk.Navisworks.Api.Application — keep that; do not add `using Autodesk.Navisworks.Api;` there.
- C#: if File.Replace fails to compile on this framework, use: File.Copy(tmp, FilePath, true); File.Delete(tmp);

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> new: NavisBridgeSession.cs, BIMBotBridgeMaster.cs, bridgeSession.ts ;
     modified: BIMBotAutoStart.cs, revitBridgeLogic.ts, navis_revit_bridge_tools.ts, revit-bridge-logic.test.mjs
  B. grep -c "server.tool(" revit-mcp-server/src/tools/navis_revit_bridge_tools.ts -> 6
  C. grep -n "NavisBridgeSession.Stop" navis-mcp-plugin/BIMBotNavisPlugin/BIMBotAutoStart.cs -> 1 match

======================================================================
6. REPORT BACK
======================================================================
Write STEP2E_BRIDGE_MASTER_REPORT.md in the repo root: 1. files, 2. last ~10 lines of each build and
the FULL npm test output, 3. self-checks A–C, 4. anything not done exactly as written, and why.
