# IMPLEMENTATION PLAN — Step 2: Navisworks <-> Revit clash bridge

**Status: COMPLETE — both builds pass with 0 errors.**

Branch: `feat/navis-2-revit-bridge` (confirmed via `git branch --show-current`, started from a clean tree)

---

## 1. Files created/changed

| File | Action |
|---|---|
| `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs` | **created** (verbatim per plan) |
| `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs` | **edited** (3 `case` lines only) |
| `revit-mcp-server/src/tools/navis_revit_bridge_tools.ts` | **created** (verbatim per plan) |

Commands exposed by the plugin (bridge file):

- `navis_get_clash_detail` — one clash + both items with Revit Element IDs, source file, clashed displayId
- `navis_list_clash_pairs` — all clashes of a test as item pairs (limit + statusFilter)
- `navis_find_by_revit_ids` — find Navisworks items by Revit Element ID (max 50), select + zoom

MCP tools exposed to the AI (TS file):

- `navis_clash_to_revit` — Navisworks clash -> Revit Element IDs -> select (+zoom, +optional isolate) in Revit
- `revit_selection_to_navis` — Revit selection -> same elements found/selected/zoomed in Navisworks
- `navis_recheck_clash` — re-run clash test, report whether the clash is now Resolved
- `navis_clash_handoff_table` — clashes as a Revit element-ID pair table grouped by source-file pair

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

Time Elapsed 00:00:03.67
```

---

## 3. Self-checks

### A. `git status --short` — exactly 2 new files + 1 modified

```
 M navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
?? navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Bridge.cs
?? revit-mcp-server/src/tools/navis_revit_bridge_tools.ts
```

`git diff --stat`: `1 file changed, 3 insertions(+)`. Nothing else touched.

### B. `git diff` of Extended.cs — exactly 3 added lines, nothing removed

```diff
@@ -36,6 +36,9 @@ namespace BIMBotNavisPlugin.Core
                 case "navis_set_camera": return SetCamera(p);
                 case "navis_zoom_to_items": return ZoomToItems(p);
                 case "navis_view_preset": return ViewPreset(p);
+                case "navis_get_clash_detail": return ClashDetail(p);
+                case "navis_list_clash_pairs": return ClashPairs(p);
+                case "navis_find_by_revit_ids": return FindByRevitIds(p);
                 case "navis_capture_view": return CaptureView(p);
                 case "navis_capture_clash": return CaptureClash(p);
                 // TimeLiner
```

### C. `server.tool(` count in `navis_revit_bridge_tools.ts`

```
4
```

---

## 4. Deviations / notes

- **None.** `Model.SourceFileName`, `RunPropertySearch(doc, tab, prop, text, withinSelection)` and `ModelItemCollection.AddRange(List<ModelItem>)` all compiled as written — none of the compile-note workarounds were needed.
- `rg` (ripgrep) is unavailable on this machine; check C was run with the PowerShell `Select-String` equivalent (same result: 4).
- No `register.ts` edit needed: it auto-discovers `registerNavisRevitBridgeTools` in the new tools file at startup.
- Per instructions: no deploy, no commit, no push.

## 5. How to verify live

1. Open the Navisworks model (with clash tests) and the source Revit model (or one linking it), both plugins running (ports 8091 / 8080).
2. `navis_list_clash_tests` -> `navis_get_clash_results` to get a clashId.
3. `navis_clash_to_revit {test, clashId}` -> the two elements get selected/zoomed in Revit.
4. Fix in Revit, re-export NWC, refresh in Navisworks, then `navis_recheck_clash {test, clashId}`.
5. `navis_clash_handoff_table {test}` for the grouped ID-pair table.
