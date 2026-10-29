# STEP 2b — Navisworks <-> Revit clash bridge fixes (G1–G6)

**Status: COMPLETE — both builds pass with 0 errors.**

Branch: `feat/navis-2-revit-bridge` (confirmed; only `?? STEP2_BRIDGE_REPORT.md` in the working tree before starting)

---

## 1. Files changed

| File | Change |
|---|---|
| `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs` | Task 1: added `using System.Text.RegularExpressions;`. Task 2: replaced `FindByRevitIds` with 4 methods (`FindByRevitIds`, `ItemsWithRevitId`, `ModelKey`, `SameModel`). Task 3: added `RefreshModels` after `SameModel`. |
| `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs` | Task 4: **1 added line** — `case "navis_refresh_models": return RefreshModels();` |
| `revit-mcp-server/src/tools/navis_revit_bridge_tools.ts` | Task 5: whole file replaced — 5 tools with document matching, decision table, refresh, before/after recheck, CSV export. |

### Fixes mapped to the gaps

- **G1 — wrong element selected:** the TS side now matches every clash item's `sourceFile` to the open Revit documents (`sameModel`: exact or prefix match, workshared locals covered). IDs only go to `select_elements` when they belong to the active document or a link (with warning); items in another open document get `action: "activate"` and the tool can switch Revit to that document first (`activate_document`); otherwise the item is skipped with a reason. `revit_selection_to_navis` passes the active Revit document title as `sourceFile`, so the Navisworks search only hits the matching NWC model (`"*"` override to search all). `navis_find_by_revit_ids` filters candidates by `SameModel(SourceFileOf(...), src)` and reports `skippedInOtherModels`.
- **G2 — false "Resolved":** `navis_recheck_clash` looks the clash up **before** re-running; a mistyped clashId returns a `Failed:` text instead of success. Result now reports `statusBefore` → `statusAfter`, and "NotFound" is only treated as resolved when the clash existed before the re-run.
- **G3 — slow lookup:** `navis_find_by_revit_ids` uses one targeted search per id for ≤ 3 ids, and **one** `ItemsWithRevitId` search (`Element > Id` exists, `PruneBelowMatch`) for more; IDs matched in memory. Cap raised 50 → 500. Returns `mode: "singlePass" | "perIdSearch"`.
- **G4 — manual refresh:** new `navis_refresh_models` tool + `RefreshModels()` plugin method, probing `Document.UpdateFiles` / `Document.RefreshFiles` / `ModelCollection.UpdateFiles` / `ModelCollection.RefreshFiles` by reflection (compiles on every Navisworks version); if nothing is found it tells the user to click Home > Refresh.
- **G5 — hand-off only as text:** `navis_clash_handoff_table` accepts optional `csvPath`; writes UTF-8 BOM CSV (Excel-safe) with the same rows; requires an absolute path.
- **G6 — no per-item explanation:** `navis_clash_to_revit` returns a `decisions` table: per item — label, `revitElementId`, `sourceFile`, target `revitDocument`, `action` (select/skip/activate), and `reason`.

---

## 2. Builds

### `npm run build` (revit-mcp-server)

```
> bim-bot-server@2.7.2 prebuild
> npm run clean

> bim-bot-server@2.7.2 clean
> rimraf build

> bim-bot-server@2.7.2 build
> tsc
```

No diagnostics, exit 0.

### `dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release`

```
  Determining projects to restore...
  All projects are up-to-date for restore.
  BIMBotNavisPlugin -> C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP\navis-mcp-plugin\BIMBotNavisPlugin\bin\Release\net48\BIMBotNavisPlugin.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.28
```

---

## 3. Self-checks

### A. `git status --short` — exactly the required set

```
 M navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs
 M navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
 M revit-mcp-server/src/tools/navis_revit_bridge_tools.ts
?? STEP2_BRIDGE_REPORT.md
```

`STEP2_BRIDGE_REPORT.md` untouched (not deleted, not modified).

### B. `git diff` of Extended.cs — exactly 1 added line

```diff
@@ -39,6 +39,7 @@ namespace BIMBotNavisPlugin.Core
                 case "navis_get_clash_detail": return ClashDetail(p);
                 case "navis_list_clash_pairs": return ClashPairs(p);
                 case "navis_find_by_revit_ids": return FindByRevitIds(p);
+                case "navis_refresh_models": return RefreshModels();
                 case "navis_capture_view": return CaptureView(p);
                 case "navis_capture_clash": return CaptureClash(p);
                 // TimeLiner
```

### C. `server.tool(` count in `navis_revit_bridge_tools.ts`

```
5
```

### D. `ItemsWithRevitId | SameModel | RefreshModels` in Bridge.cs — all present

```
NavisCommandExecutor.Bridge.cs:138: candidates = ItemsWithRevitId(doc);
NavisCommandExecutor.Bridge.cs:147: if (src != "" && !SameModel(SourceFileOf(doc, m), src)) { otherModels++; continue; }
NavisCommandExecutor.Bridge.cs:181: private static List<ModelItem> ItemsWithRevitId(Document doc)
NavisCommandExecutor.Bridge.cs:196: private static bool SameModel(string sourceFile, string other)
NavisCommandExecutor.Bridge.cs:207: private static JObject RefreshModels()
```

(`rg` is unavailable on this machine; checks C/D used the PowerShell `Select-String` equivalent.)

---

## 4. Anything not done exactly as written

- **Task 5 text reconstruction:** the plan's TS content arrived transport-mangled (missing backticks, `Partial[]` without a type argument, `Promise` without a type argument, several lines mashed together). Reconstructed to intent, logic/names/strings/types otherwise verbatim:
  - `const fail = (e) => text(\`Failed: ${...}\`)` uses a template literal.
  - `revitDocs()` casts to `{ documents?: Partial<RevitDoc>[] }`.
  - `clashDetail(...)` is typed `Promise<ClashDetail>`.
  - The `decide()` "not open in Revit" reason and `csvCell()` use template literals.
  - Imports split onto one per line; interfaces formatted multi-line (same members and types).
  - `csvPath` describe written as source `C:\\Temp\\clashes.csv` so it displays as `C:\Temp\clashes.csv`.
- **No C# compile notes were needed**: `search.FindAll(doc, false).Cast<ModelItem>()` works, `new object[] { doc, doc.Models }` compiles directly, and `NavisLogger.Log(string)` was verified to exist.
- No deploy, no commit, no push. Nothing outside the three allowed files was touched.

---

## 5. Suggested live review (as stated in the plan)

1. Clash between two different source files → `navis_clash_to_revit` selects **only** the element(s) from the active Revit model, explains the other in `decisions` (or offers `activate_document`).
2. Mistyped clashId → `navis_recheck_clash` returns `Failed: ... not found ... BEFORE re-running` instead of reporting "resolved".
3. >3 IDs → response shows `mode: "singlePass"`; the Navisworks UI freezes once, not once per ID.
4. After an NWC re-export → `navis_refresh_models` either refreshes (reports `via`) or tells the user to click Home > Refresh.
5. `navis_clash_handoff_table` with `csvPath` → CSV opens in Excel with correct rows and a BOM.
