// Unit tests for the Navisworks <-> Revit bridge decision logic (no Revit or Navisworks needed).
// Run inside revit-mcp-server:  npm run build && npm test
import { test } from "node:test";
import assert from "node:assert/strict";

const logic = await import(new URL("../build/utils/revitBridgeLogic.js", import.meta.url).href);
const { modelKey, sameModel, extractIds, decide, planActivation, csvCell } = logic;

const doc = (title, extra = {}) => ({ title, path: "", isActive: false, isLinked: false, isFamilyDocument: false, ...extra });
const item = (revitElementId, sourceFile, displayName = "elem") => ({ revitElementId, sourceFile, displayName });

test("modelKey strips folders and model extensions", () => {
    assert.equal(modelKey("C:\\models\\Arch.nwc"), "arch");
    assert.equal(modelKey("C:/models/MEP.RVT"), "mep");
    assert.equal(modelKey("Tower"), "tower");
    assert.equal(modelKey(""), "");
});

test("sameModel: exact, extension, prefix (workshared local), and non-matches", () => {
    assert.equal(sameModel("Arch.nwc", "Arch"), true);
    assert.equal(sameModel("Arch.rvt", "arch"), true);
    assert.equal(sameModel("Project.nwc", "Project_jane"), true);
    assert.equal(sameModel("MEP.nwc", "Arch"), false);
    assert.equal(sameModel("", "Arch"), false);
    assert.equal(sameModel("Arch.nwc", ""), false);
});

test("extractIds handles get_selected_elements shapes", () => {
    assert.deepEqual(extractIds({ totalCount: 3, count: 3, elements: [{ id: 1 }, { id: "2" }, { id: -3 }] }), [1, 2]);
    assert.deepEqual(extractIds([5, 6]), [5, 6]);
    assert.deepEqual(extractIds({ elementIds: [7] }), [7]);
    assert.deepEqual(extractIds(null), []);
    assert.deepEqual(extractIds({ totalCount: 0 }), []);
});

test("decide: active, linked, unknown, missing id, missing source", () => {
    const docs = [doc("Arch", { isActive: true }), doc("MEP", { isLinked: true })];
    const d = decide([
        item("10", "Arch.nwc", "wall"),
        item("20", "MEP.nwc", "duct"),
        item("30", "Struct.nwc", "beam"),
        item("", "Arch.nwc", "noid"),
        item("40", "", "nosrc"),
    ], docs);
    assert.deepEqual(d.map((x) => x.action), ["select", "select", "skip", "skip", "select"]);
    assert.equal(d[0].revitDocument, "Arch");
    assert.equal(d[1].revitDocument, "MEP");
    assert.match(d[1].reason, /linked model/);
    assert.match(d[2].reason, /not open in Revit/);
    assert.equal(d[3].revitElementId, null);
    assert.equal(d[4].revitDocument, "Arch");
});

test("decide: item from another open (not active, not linked) document -> activate", () => {
    const docs = [doc("Arch", { isActive: true }), doc("MEP")];
    const d = decide([item("20", "MEP.nwc")], docs);
    assert.equal(d[0].action, "activate");
    assert.equal(d[0].revitDocument, "MEP");
});

test("decide: family documents are never activation targets", () => {
    const docs = [doc("Arch", { isActive: true }), doc("Chair", { isFamilyDocument: true })];
    const d = decide([item("5", "Chair.rvt")], docs);
    assert.equal(d[0].action, "skip");
});

test("planActivation switches when every item belongs to one other document", () => {
    const docs = [doc("Arch", { isActive: true }), doc("MEP")];
    const plan = planActivation(decide([item("20", "MEP.nwc"), item("21", "MEP.nwc")], docs), true);
    assert.equal(plan.documentToActivate, "MEP");
    assert.deepEqual(plan.decisions.map((x) => x.action), ["select", "select"]);
});

test("planActivation does not switch when something is selectable in the active document", () => {
    const docs = [doc("Arch", { isActive: true }), doc("MEP")];
    const plan = planActivation(decide([item("10", "Arch.nwc"), item("20", "MEP.nwc")], docs), true);
    assert.equal(plan.documentToActivate, null);
    assert.deepEqual(plan.decisions.map((x) => x.action), ["select", "skip"]);
    assert.match(plan.decisions[1].reason, /not the active Revit document/);
});

test("planActivation does not switch when two different documents would be needed, or when disallowed", () => {
    const docs = [doc("Arch", { isActive: true }), doc("MEP"), doc("Struct")];
    const two = planActivation(decide([item("20", "MEP.nwc"), item("30", "Struct.nwc")], docs), true);
    assert.equal(two.documentToActivate, null);
    assert.deepEqual(two.decisions.map((x) => x.action), ["skip", "skip"]);
    const off = planActivation(decide([item("20", "MEP.nwc")], docs), false);
    assert.equal(off.documentToActivate, null);
    assert.equal(off.decisions[0].action, "skip");
});

test("csvCell quotes only when needed and doubles quotes", () => {
    assert.equal(csvCell("plain"), "plain");
    assert.equal(csvCell("a,b"), '"a,b"');
    assert.equal(csvCell('say "hi"'), '"say ""hi"""');
    assert.equal(csvCell("line1\nline2"), '"line1\nline2"');
    assert.equal(csvCell(undefined), "");
    assert.equal(csvCell(12.5), "12.5");
});

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
