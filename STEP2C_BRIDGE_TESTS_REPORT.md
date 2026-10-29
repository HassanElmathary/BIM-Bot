# STEP 2c — Unit tests for the Revit bridge decision logic

**Status: implemented; build clean; the new suite is 10 passing / 0 failing.**
**One acceptance deviation, fully explained in §4: plain `npm test` (`node --test`) also discovers pre-existing `test-*` files in this repo that fail for unrelated reasons, so the overall `npm test` run is not 0-failing.**

Branch: `feat/navis-2-revit-bridge` (confirmed — not switched). Started from a clean tree (only the two untracked report .md files; nothing modified).

---

## 1. Files created/changed

| File | Action |
|---|---|
| `revit-mcp-server/src/utils/revitBridgeLogic.ts` | **created** — pure decision logic, verbatim per Task 1 (`modelKey` uses `path.win32.basename` so both `\` and `/` separators work; interfaces `BridgeItem`, `ClashInfo`, `ClashDetail`, `RevitDoc`, `Decision`, `ActivationPlan`; functions `modelKey`, `sameModel`, `extractIds`, `decide`, `planActivation`, `csvCell`) |
| `revit-mcp-server/src/tools/navis_revit_bridge_tools.ts` | **edited** — Tasks 2a–2c only: deleted the 5 interfaces + `modelKey`/`sameModel`/`extractIds`/`decide`/`csvCell`; added the two `revitBridgeLogic.js` import lines below the `ConnectionManager.js` import; replaced the inline decisions block with `planActivation(decide(items, docs), args.activateDocument !== false)` + `activate_document` call. Kept `text`, `fail`, `revitDocs`, `clashDetail`, `registerNavisRevitBridgeTools` (still 5 tools). |
| `revit-mcp-server/test/revit-bridge-logic.test.mjs` | **created** — 10 tests, verbatim per Task 3 |
| `revit-mcp-server/package.json` | **edited** — one entry added directly after `"start"`: `"test": "node --test"` (valid JSON, nothing else changed) |

Tool behaviour is unchanged: same decisions, same reasons, same `activate_document` command and payload; the logic now lives in `src/utils/revitBridgeLogic.ts`.

---

## 2. Build

### `npm run build` (last lines)

```
> bim-bot-server@2.7.2 prebuild
> npm run clean


> bim-bot-server@2.7.2 clean
> rimraf build


> bim-bot-server@2.7.2 build
> tsc
```

Clean, exit 0. Node used: **v24.13.1**.

---

## 3. Test runs

### 3a. `npm test` — FULL output (as required)

```
> bim-bot-server@2.7.2 test
> node --test

✔ build\tests\test-socket.d.ts (109.2949ms)

🧪 BIM-Bot Socket Integration Tests

  ✗ Can connect to Revit plugin on port 8080: 
  ✗ get_current_view_info returns data: 
  ✗ get_project_info returns data: 
  ✗ get_levels returns array: 
  ✗ get_views returns data: 
  ✗ get_warnings returns data: 
  ✗ get_model_statistics returns data: 
  ✓ Invalid command returns error (1ms)

──────────────────────────────────────────────────
Results: 1 passed, 7 failed, 8 total

Failed tests:
  ✗ Can connect to Revit plugin on port 8080: 
  ✗ get_current_view_info returns data: 
  ✗ get_project_info returns data: 
  ✗ get_levels returns array: 
  ✗ get_views returns data: 
  ✗ get_warnings returns data: 
  ✗ get_model_statistics returns data: 

✖ build\tests\test-socket.js (103.5109ms)
✔ build\tests\test-startup.d.ts (99.4762ms)

🧪 MCP Server Startup Test

  ✓ Server instance created
  ✓ Registered tool: advanced_tools
  ...
  ✓ Registered tool: navis_revit_bridge_tools
  ...
Total tools registered: 35
  ✓ Tool registration complete

  All checks passed ✓

✔ build\tests\test-startup.js (62303.5179ms)
✔ build\tests\test-transfer.d.ts (99.9462ms)

BIM-Bot Transfer Tools Test (v2.5.0)

  Registered transfer tools module (expects: list_open_documents, activate_document, export_annotations, import_annotations, transfer_annotations)
  All 5 transfer tools present

  All checks passed

✔ build\tests\test-transfer.js (5865.0565ms)

🧪 BIM-Bot Socket Integration Tests

  ✗ Can connect to Revit plugin on port 8080: 
  ✗ get_current_view_info returns data: 
  ✗ get_project_info returns data: 
  ✗ get_levels returns array: 
  ✗ get_views returns data: 
  ✗ get_warnings returns data: 
  ✗ get_model_statistics returns data: 
  ✓ Invalid command returns error (0ms)

──────────────────────────────────────────────────
Results: 1 passed, 7 failed, 8 total

Failed tests:
  ... (same 7 as above)

✖ src\tests\test-socket.ts (119.8735ms)
node:internal/modules/esm/resolve:275
    throw new ERR_MODULE_NOT_FOUND(
          ^

Error [ERR_MODULE_NOT_FOUND]: Cannot find module '...\revit-mcp-server\src\tools\register.js' imported from ...\src\tests\test-startup.ts

✖ src\tests\test-startup.ts (3771.173ms)
Error [ERR_MODULE_NOT_FOUND]: Cannot find module '...\revit-mcp-server\src\tools\transfer_tools.js' imported from ...\src\tests\test-transfer.ts

✖ src\tests\test-transfer.ts (3776.8765ms)
✔ modelKey strips folders and model extensions (1.2867ms)
✔ sameModel: exact, extension, prefix (workshared local), and non-matches (0.1388ms)
✔ extractIds handles get_selected_elements shapes (0.5326ms)
✔ decide: active, linked, unknown, missing id, missing source (0.3162ms)
✔ decide: item from another open (not active, not linked) document -> activate (0.1198ms)
✔ decide: family documents are never activation targets (0.08ms)
✔ planActivation switches when every item belongs to one other document (0.1765ms)
✔ planActivation does not switch when something is selectable in the active document (0.1328ms)
✔ planActivation does not switch when two different documents would be needed, or when disallowed (0.1265ms)
✔ csvCell quotes only when needed and doubles quotes (0.2131ms)
ℹ tests 19
ℹ suites 0
ℹ pass 15
ℹ fail 4
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 62364.2487

✖ failing tests:

test at build\tests\test-socket.js:1:1
✖ build\tests\test-socket.js (103.5109ms)
  'test failed'

test at src\tests\test-socket.ts:1:1
✖ src\tests\test-socket.ts (119.8735ms)
  'test failed'

test at src\tests\test-startup.ts:1:1
✖ src\tests\test-startup.ts (3771.173ms)
  'test failed'

test at src\tests\test-transfer.ts:1:1
✖ src\tests\test-transfer.ts (3776.8765ms)
  'test failed'
```

The new suite inside that run: **the 10 `revit-bridge-logic` tests all pass** (`✔ modelKey ...` through `✔ csvCell ...`). The 4 failing "tests" are the pre-existing files (see §4) — none of them is the new test file.

### 3b. New suite alone (evidence it is genuinely 10/0)

`node --test test/revit-bridge-logic.test.mjs` (file argument — NOT the forbidden folder form):

```
✔ modelKey strips folders and model extensions (1.4974ms)
✔ sameModel: exact, extension, prefix (workshared local), and non-matches (0.1451ms)
✔ extractIds handles get_selected_elements shapes (0.5873ms)
✔ decide: active, linked, unknown, missing id, missing source (0.4017ms)
✔ decide: item from another open (not active, not linked) document -> activate (0.1243ms)
✔ decide: family documents are never activation targets (0.0952ms)
✔ planActivation switches when every item belongs to one other document (0.208ms)
✔ planActivation does not switch when something is selectable in the active document (0.1513ms)
✔ planActivation does not switch when two different documents would be needed, or when disallowed (0.1418ms)
✔ csvCell quotes only when needed and doubles quotes (0.2446ms)
ℹ tests 10
ℹ suites 0
ℹ pass 10
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 76.8617
```

---

## 4. Self-checks

### A. `git status --short`

```
 M revit-mcp-server/package.json
 M revit-mcp-server/src/tools/navis_revit_bridge_tools.ts
?? STEP2_BRIDGE_REPORT.md
?? STEP2B_BRIDGE_FIXES_REPORT.md
?? revit-mcp-server/src/utils/revitBridgeLogic.ts
?? revit-mcp-server/test/
```

Exactly: new `src/utils/revitBridgeLogic.ts` + `test/`, modified tools file + package.json, plus the untracked report .md files. Nothing else.

### B. grep for the moved definitions in the tools file — NO output (they live only in `revitBridgeLogic.ts`)

```
(no matches)
```

### C. `server.tool(` count

```
5
```

---

## 5. Anything not done exactly as written, and why

1. **Task 4 / §4 acceptance — `npm test` is not 0-failing overall.** The script was added exactly as written (`"test": "node --test"`, directly after `"start"`, nothing else changed). But the plan's premise — *"Plain 'node --test' finds every `*.test.mjs` file (node_modules excluded)"* — is incomplete: `node --test` (Node v24.13.1 here) also discovers any file whose **name** matches `test-*.{js,ts}` etc., not only `*.test.mjs`. This repo already contains:
   - `src/tests/test-socket.ts`, `src/tests/test-startup.ts`, `src/tests/test-transfer.ts` (pre-existing, unmodified) — Node 24 runs `.ts` directly; they fail with `ERR_MODULE_NOT_FOUND` for `../tools/register.js` / `../tools/transfer_tools.js` (their `.js` imports only resolve inside `build/`), and `test-socket.*` additionally requires a live Revit on port 8080;
   - `build/tests/test-socket.js`, `build/tests/test-startup.js`, `build/tests/test-transfer.js` (build output of the above) — also discovered; `test-socket.js` fails because no Revit is running (this is pre-existing live-integration behaviour, unrelated to this step);
   - even `build/tests/*.d.ts` files are "run" (they pass as empty modules).

   Per the plan's own rule — *"You may fix a failing TEST only if the mistake is in the test file itself; never change `revitBridgeLogic.ts`… report it instead"* — I did **not** change any of those files or the package.json entry. The new suite itself is 10/10 (evidence in §3b).

   **Recommended one-string fix for the plan owner (verified working above):**
   `"test": "node --test test/revit-bridge-logic.test.mjs"` — a file argument (not the forbidden `test/` folder form), which runs only the new suite → exactly 10 passing, 0 failing.

2. `node --version` here is **v24.13.1** (plan said Node 20+; the engines field says >=18). Everything in Task 1/2/3 compiled and ran unmodified — no TS reconstruction was needed this time (unlike step 2b), the plan's code blocks were intact.

3. Nothing else deviated: no C# touched, no new packages, no deploy, no commit, no push, no git reset/stash/clean/checkout; `STEP2*_REPORT.md` files untouched.

---

## 6. Notes for review

- `navis_revit_bridge_tools.ts` behaviour is unchanged: `decide()` → `planActivation()` → optional `activate_document` → `select_elements`/`zoom_to_element`; the response payload keys are identical (`decisions`, `activatedDocument`, `selectedInRevit`, …).
- The pure logic is now covered without Revit/Navisworks: document matching (active / linked / other open / family / unknown / missing id), the activation plan (switch only when nothing is selectable in the active document, all items share one target, and switching is allowed), defensive `get_selected_elements` parsing, and CSV escaping.
