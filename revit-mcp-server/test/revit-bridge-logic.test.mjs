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
