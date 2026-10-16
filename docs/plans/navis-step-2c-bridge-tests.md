IMPLEMENTATION PLAN — Step 2c: Unit tests for the Revit bridge decision logic
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge (steps 2 and 2b are committed there)

IMPORTANT: read THIS file from disk with your file-read tool. Copy code blocks character for
character — they contain TypeScript generics like Partial<RevitDoc>[] and template literals with
backticks that must be kept exactly.

======================================================================
0. BEFORE YOU START
======================================================================
1. git branch --show-current   -> must print feat/navis-2-revit-bridge (do NOT switch branches)
2. git status --short          -> may list only STEP2_BRIDGE_REPORT.md, STEP2B_BRIDGE_FIXES_REPORT.md
   and this plan file as untracked (??). If anything is MODIFIED (M), STOP and report.

======================================================================
1. GOAL
======================================================================
The bridge's core rules (which Revit document an Element ID belongs to, when to switch documents,
how ids are pulled out of Revit replies, CSV escaping) live inside the MCP tool file and cannot be
tested without Revit and Navisworks running. Move that pure logic (no I/O) into its own module and
cover it with Node's built-in test runner (node --test, no new packages). Tool behaviour must not change.

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit:
   - CREATE revit-mcp-server/src/utils/revitBridgeLogic.ts
   - EDIT   revit-mcp-server/src/tools/navis_revit_bridge_tools.ts   (Tasks 2a–2c only)
   - CREATE revit-mcp-server/test/revit-bridge-logic.test.mjs
   - EDIT   revit-mcp-server/package.json                            (add one "test" script)
2. Touch nothing else (no C#, no other TS). No new packages. Do not deploy. Do not commit or push.
   No git reset/stash/clean/checkout. Do not delete the STEP2* report files.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create revit-mcp-server/src/utils/revitBridgeLogic.ts with exactly this content -----

import * as path from "path";

/**
 * Pure decision logic for the Navisworks <-> Revit bridge (no I/O), so it can be unit-tested.
 * Revit Element IDs are only unique inside ONE Revit file, so every ID is matched to the Revit
 * document it came from (Navisworks source file vs Revit document title) before it is used.
 */

export interface BridgeItem {
    displayId?: string;
    displayName?: string;
    revitElementId?: string;
    revitCategory?: string;
    sourceFile?: string;
}

export interface ClashInfo {
    id: string;
    name: string;
    status: string;
    distance: number;
}

export interface ClashDetail {
    test: string;
    clash: ClashInfo;
    item1: BridgeItem | null;
    item2: BridgeItem | null;
}

export interface RevitDoc {
    title: string;
    path: string;
    isActive: boolean;
    isLinked: boolean;
    isFamilyDocument: boolean;
}

export interface Decision {
    item: string;
    revitElementId: number | null;
    sourceFile: string;
    revitDocument: string;
    action: "select" | "skip" | "activate";
    reason: string;
}

export interface ActivationPlan {
    documentToActivate: string | null;
    decisions: Decision[];
}

/** File name without folder or .nwc/.nwd/.nwf/.rvt, lower-case. Handles both \ and / separators. */
export function modelKey(name: string): string {
    return path.win32.basename(name || "").trim().replace(/\.(nwc|nwd|nwf|rvt)$/i, "").toLowerCase();
}

/** Same rule as the plugin's SameModel: exact match, or one name is a prefix of the other. */
export function sameModel(sourceFile: string, docTitle: string): boolean {
    const a = modelKey(sourceFile);
    const b = modelKey(docTitle);
    return !!a && !!b && (a === b || a.startsWith(b) || b.startsWith(a));
}

/** Pulls numeric element ids out of whatever shape get_selected_elements returns. */
export function extractIds(resp: unknown): number[] {
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

/** Decide, per clash item, which Revit document its Element ID belongs to and whether to select it. */
export function decide(items: BridgeItem[], docs: RevitDoc[]): Decision[] {
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

/**
 * Turns "activate" decisions into a plan: if nothing can be selected in the active document and every
 * remaining item belongs to ONE other open document (and switching is allowed), switch to it and select
 * those items; otherwise skip them with a reason.
 */
export function planActivation(decisions: Decision[], allowActivate: boolean): ActivationPlan {
    const toActivate = [...new Set(decisions.filter((x) => x.action === "activate").map((x) => x.revitDocument))];
    const anySelect = decisions.some((x) => x.action === "select");
    if (!anySelect && toActivate.length === 1 && allowActivate) {
        return {
            documentToActivate: toActivate[0],
            decisions: decisions.map((x) => x.action === "activate"
                ? { ...x, action: "select" as const, reason: `Revit switched to '${x.revitDocument}' first.` }
                : x),
        };
    }
    return {
        documentToActivate: null,
        decisions: decisions.map((x) => x.action === "activate"
            ? { ...x, action: "skip" as const, reason: `Belongs to '${x.revitDocument}', which is not the active Revit document. Activate it and run again.` }
            : x),
    };
}

/** One CSV cell: quoted when it contains a comma, quote or line break; quotes doubled. */
export function csvCell(v: unknown): string {
    const s = String(v ?? "");
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

----- TASK 2 — Edit revit-mcp-server/src/tools/navis_revit_bridge_tools.ts -----

2a. DELETE these definitions from the file (each one completely, including its doc comment):
      interface BridgeItem, interface ClashInfo, interface ClashDetail, interface RevitDoc,
      interface Decision, function modelKey, function sameModel, function extractIds,
      function decide, function csvCell.
    KEEP: the imports, `const text`, `const fail`, `async function revitDocs`, `async function clashDetail`,
    and `export function registerNavisRevitBridgeTools`.

2b. Directly below the line
        import { withRevitConnection } from "../utils/ConnectionManager.js";
    add these two lines:

import type { BridgeItem, ClashDetail, ClashInfo, RevitDoc } from "../utils/revitBridgeLogic.js";
import { csvCell, decide, extractIds, planActivation } from "../utils/revitBridgeLogic.js";

2c. Inside the navis_clash_to_revit handler, replace this block (from `let decisions` down to the
    closing brace of the else branch):

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

    with:

                const plan = planActivation(decide(items, docs), args.activateDocument !== false);
                const decisions = plan.decisions;
                let activated: unknown = null;
                if (plan.documentToActivate) {
                    const documentTitle = plan.documentToActivate;
                    activated = await withRevitConnection((c) =>
                        c.sendCommand("activate_document", { documentTitle }));
                }

    (If your copy of the file formats the old block slightly differently — line breaks, spacing — replace
    the equivalent block. The logic is: decide -> pick one document to activate -> map activate to select/skip.)

    Change nothing else in the file. If after 2a the compiler says a type or function is missing,
    add it to the `import` lines from 2b (never re-define it in the tools file).

----- TASK 3 — Create revit-mcp-server/test/revit-bridge-logic.test.mjs with exactly this content -----

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

----- TASK 4 — revit-mcp-server/package.json -----
In "scripts", add this entry directly after the "start" entry:

    "test": "node --test",

(keep valid JSON — mind the commas). Change nothing else. Do NOT write "node --test test/": on
Node 22+ a folder argument is treated as a file name and the run fails. Plain "node --test" finds
every *.test.mjs file (node_modules excluded) on Node 20 and later.

======================================================================
4. BUILD + TEST (must pass)
======================================================================
  cd revit-mcp-server
  npm run build
  npm test

npm test must report 10 passing tests and 0 failing. If a test fails, report the FULL test output.
You may fix a failing TEST only if the mistake is in the test file itself; never change
revitBridgeLogic.ts logic to make a test pass — report it instead.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> new: src/utils/revitBridgeLogic.ts, test/revit-bridge-logic.test.mjs ;
     modified: src/tools/navis_revit_bridge_tools.ts, package.json ; plus the untracked report/plan .md files
  B. grep -n "function decide\|function sameModel\|function csvCell\|interface Decision" revit-mcp-server/src/tools/navis_revit_bridge_tools.ts
     -> NO output (they now live only in revitBridgeLogic.ts)
  C. grep -c "server.tool(" revit-mcp-server/src/tools/navis_revit_bridge_tools.ts -> 5

======================================================================
6. REPORT BACK
======================================================================
Write your report to STEP2C_BRIDGE_TESTS_REPORT.md in the repo root with:
1. Files created/changed. 2. Last ~10 lines of npm run build and the FULL npm test output.
3. Self-checks A–C. 4. Anything you could not do exactly as written, and why.
