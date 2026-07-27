# Navisworks Integration — Implementation Plan

**Goal:** Extend BIM-Bot so the same MCP server can drive Autodesk **Navisworks Manage** (clash, viewpoints, selection/search sets, model tree, TimeLiner 4D, Quantification 5D, appearance, data export) in addition to Revit — reusing the existing socket protocol and installer machinery.

**Status:** Plan for review. No code written yet.

---

## 1. How the current system works (the pattern we mirror)

Three tiers, cleanly separated:

```
Claude ──stdio──▶ MCP server (Node/TS) ──JSON-RPC/TCP:8080──▶ Revit plugin (C#) ──▶ Revit API
```

| Layer | Key file | Responsibility |
|---|---|---|
| Tool defs | `revit-mcp-server/src/tools/*.ts` | One `registerXxxTools(server)` per file; auto-discovered by `register.ts` |
| Transport (client) | `src/utils/SocketClient.ts` | JSON-RPC 2.0 over TCP, length-prefixed framing |
| Connection | `src/utils/ConnectionManager.ts` | Persistent singleton socket, retry/backoff. **Port 8080 hardcoded** |
| Transport (server) | `plugin/Core/SocketService.cs` | TcpListener on 8080, same framing, watchdog + auto-restart |
| Thread marshaling | `plugin/Core/ExternalEventManager.cs` | Queues commands onto Revit's single UI thread via `ExternalEvent` |
| Dispatch | `plugin/Core/CommandExecutor.cs` (+ partials) | `switch(command)` → API implementation |
| Host | `plugin/Core/Application.cs` | `IExternalApplication`, ribbon, auto-start on idle, health check |

**Key insight:** `register.ts` auto-discovers any `*.ts` file exporting a `register*` function, and `SocketClient`/framing are **transport-generic** (nothing Revit-specific). So most of the server layer is reusable as-is. The Navisworks work is mostly (a) a new C# add-in and (b) a routing tweak so tools can target port 8081.

---

## 2. Target architecture

```
                        ┌─▶ Revit plugin  (C#) ──JSON-RPC/TCP:8080──▶ Revit API
Claude ──▶ MCP server ──┤
                        └─▶ Navisworks add-in (C#) ──JSON-RPC/TCP:8081──▶ Navisworks API
```

- **One MCP server**, two backends distinguished by port.
- Revit tools keep port 8080 untouched (zero regression risk).
- Navisworks tools connect to 8081 via a second connection manager.
- Both apps can run simultaneously; each answers only its own tool calls.

---

## 3. Server-side changes (TypeScript)

### 3.1 Generalize the connection manager

`ConnectionManager.ts` currently hardcodes `localhost:8080` in three places. Two options:

- **Option A (minimal):** Add a parallel `NavisworksConnectionManager.ts` — a copy pointed at 8081 exporting `withNavisworksConnection()`. Simple, zero risk to Revit path, ~160 lines duplicated.
- **Option B (clean):** Refactor `ConnectionManager.ts` into a factory `createConnectionManager(port)` returning `{ withConnection }`, then instantiate two (8080, 8081). Less duplication; touches the Revit path so needs regression testing.

**Recommendation:** Option B — the duplication in A will rot, and the refactor is mechanical (the retry/backoff logic is identical). Revit path validated by existing smoke test after refactor.

`SocketClient.ts` needs **no changes** — it already takes `host`/`port` in its constructor.

### 3.2 New tool file

`revit-mcp-server/src/tools/navisworks_tools.ts` exporting `registerNavisworksTools(server)`. Auto-picked up by `register.ts` with no change there. Each tool follows the existing shape:

```ts
server.tool("nw_run_clash_tests", "Run all clash tests in the active Navisworks document...",
  { testName: z.string().optional() },
  async (args) => {
    try {
      const r = await withNavisworksConnection(c => c.sendCommand("nw_run_clash_tests", args));
      return { content: [{ type: "text", text: JSON.stringify(r, null, 2) }] };
    } catch (e) { return { content: [{ type: "text", text: `Failed: ${msg(e)}` }] }; }
  });
```

**Naming:** prefix every Navisworks tool `nw_` so Claude (and users) can tell them apart from Revit tools and to avoid collisions.

### 3.3 Tool-count / docs housekeeping

Update `website.md`, `setup.iss`, READMEs with the new category ("Navisworks") and tool count once the toolset is finalized.

---

## 4. Navisworks add-in (C#) — new project `navisworks-mcp-plugin/`

Mirror the plugin structure. **Reusable almost verbatim** (Newtonsoft, socket framing, watchdog):

- `Core/SocketService.cs` — copy from Revit plugin, change port to 8081, swap `ExternalEventManager` for `NavisworksDispatcher` (below). Framing/watchdog/port-kill logic identical.
- `Core/Logger.cs` — copy.

**New / Navisworks-specific:**

- `NavisworksAddin.cs` — the entry point. Navisworks uses `AddInPlugin` (a "tool" plugin triggered from the ribbon) plus optionally a `DockPanePlugin` for a panel. Decorated with `[Plugin("BIMBotNW", "BMBT", DisplayName="BIM-Bot")]` and `[AddInPlugin(AddInLocation.AddIn)]`. This starts the socket service. Unlike Revit there's no `IExternalApplication` auto-load-on-startup event; see §4.1 for auto-start options.
- `Core/NavisworksDispatcher.cs` — the equivalent of `ExternalEventManager` + `CommandExecutor`. **Critical difference:** Navisworks has no `ExternalEvent`/`Idling` mechanism. The socket runs on a background thread but the Navisworks API must be touched on the main UI thread. We marshal via a WinForms `SynchronizationContext`/`Control.Invoke` captured at add-in load (a hidden control created on the UI thread), or a `DockPanePlugin`'s control. Every command is `Invoke`d onto that control before touching `Application.ActiveDocument`.
- `Core/NavisworksCommandExecutor.cs` (+ partials by domain, mirroring the Revit split):
  - `NavisworksCommandExecutor.Clash.cs`
  - `NavisworksCommandExecutor.Viewpoints.cs`
  - `NavisworksCommandExecutor.Selection.cs`
  - `NavisworksCommandExecutor.ModelTree.cs`
  - `NavisworksCommandExecutor.TimeLiner.cs`
  - `NavisworksCommandExecutor.Quantities.cs`
  - `NavisworksCommandExecutor.Appearance.cs`
  - `NavisworksCommandExecutor.Files.cs`

### 4.1 Auto-start considerations

Revit auto-starts the socket on the `Idling` event. Navisworks add-ins are load-on-demand (clicked from ribbon) and there is no equivalent global idle hook in the managed API. Options:

- **EventWatcherPlugin** (`AddInLocation` variants) — Navisworks supports a plugin type that loads at startup and can subscribe to document events. Use this to auto-start the socket when Navisworks opens, matching Revit's behavior.
- **Manual start** — a ribbon button ("Start BIM-Bot") that boots the socket. Simplest, but user must click each session.

**Recommendation:** EventWatcher/startup plugin for parity, with a ribbon toggle button as manual fallback (mirrors Revit's toggle).

### 4.2 Build targeting (`navisworks-mcp-plugin/BIMBotNW.csproj`)

Mirror the Revit multi-target approach:

- Navisworks 2020–2024 → **.NET Framework 4.x** (`net48`).
- Navisworks 2025–2027 → **.NET 8** (`net8.0-windows`), matching Autodesk's move to .NET 8.
- Reference `Autodesk.Navisworks.Api.dll` (and `Autodesk.Navisworks.ComApi` / `Interop` if COM bridging is needed for TimeLiner/Quantification, which are partly COM-only). Unlike Revit there is **no maintained NuGet** for the Navisworks API — DLLs must be referenced from an installed Navisworks (`C:\Program Files\Autodesk\Navisworks Manage 20xx\`) with `ExcludeAssets=runtime`/`Private=false`. This means the build machine needs Navisworks installed (or the DLLs vendored). **Open question — see §8.**

---

## 5. Proposed tool catalog (~30 tools)

Grouped by the API surface Navisworks actually exposes. `nw_` prefix on all.

### Clash (Clash Detective — `DocumentClash` / `Autodesk.Navisworks.Api.Clash`)
- `nw_get_clash_tests` — list tests, status, counts by severity
- `nw_run_clash_tests` — run all or a named test
- `nw_get_clash_results` — results for a test (name, status, distance, elements, grid location)
- `nw_create_clash_test` — new test from two selection/search sets
- `nw_update_clash_status` — set a result to Approved/Reviewed/Resolved
- `nw_export_clash_report` — HTML/XML report to a path (feeds the existing Revit-side Clash Report Viewer!)

### Viewpoints (`DocumentSavedViewpoints`)
- `nw_get_viewpoints` — list saved viewpoints/folders
- `nw_restore_viewpoint` — activate a saved viewpoint by name/path
- `nw_save_viewpoint` — save current camera as a named viewpoint
- `nw_export_viewpoint_image` — render current view to PNG/JPG

### Selection & Search sets (`DocumentSelectionSets`, `Search`)
- `nw_get_selection_sets` — list saved selection & search sets
- `nw_run_search` — run a property-based search (category/property/value), return matched items
- `nw_get_current_selection` — items currently selected in the UI
- `nw_set_selection` — select items by search result / GUIDs

### Model tree & properties (`ModelItem`, `PropertyCategory`)
- `nw_get_model_tree` — hierarchical tree (paged; large models)
- `nw_get_item_properties` — all property categories/tabs for an item
- `nw_get_model_stats` — model count, item count, source files, units, bounding box **(smoke-test command)**
- `nw_find_items_by_property` — query items by property predicate

### TimeLiner 4D (`DocumentTimeliner` — partly COM)
- `nw_get_timeliner_tasks` — task list, dates, status, attached items
- `nw_attach_items_to_task` — link current selection/search set to a task
- `nw_simulate_timeliner` — export simulation frames / summary
- `nw_export_timeliner` — CSV/XML of the schedule

### Quantification 5D (`DocumentQuantification` — partly COM)
- `nw_get_quantification_catalog` — WBS / item catalog
- `nw_get_takeoff_items` — takeoff quantities
- `nw_export_quantities` — export takeoff to CSV/XLSX

### Appearance / visibility (`DocumentInactiveState`, overrides)
- `nw_override_appearance` — color/transparency on a selection
- `nw_set_visibility` — hide/unhide/isolate a selection
- `nw_reset_appearance` — clear overrides

### Files & data
- `nw_append_model` — append/merge an NWC/NWD/RVT/IFC into the current document
- `nw_save_document` — save NWF / export NWD
- `nw_export_properties` — dump item properties → reuse existing **Excel / SQLite / PowerBI / Notion** exporters on the server side

> Exact counts firm up during build; some 4D/5D calls may be COM-only and land in a later phase.

---

## 6. Deployment & installer

Extend `installer/build-installer.ps1` + `setup.iss`:

- **Detect Navisworks** 2020–2027 (registry `HKLM\SOFTWARE\Autodesk\Navisworks Manage\<ver>` / install-path probing), analogous to the Revit-version detection already present.
- **Deploy the add-in.** Navisworks loads managed plugins from a `Plugins\<PluginName>\` folder under the install dir, or via an **`.bundle`** in `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (the modern, per-version-agnostic path). Recommend the `.bundle` approach — single deploy location, `PackageContents.xml` declares supported release ranges.
- **Claude config unchanged** — the MCP server entry in `claude_desktop_config.json` is the same; it now simply exposes `nw_*` tools too.
- Ship the correct framework build (net48 vs net8) per detected Navisworks version, same logic as Revit.

---

## 7. Phasing (build order)

1. **Phase 0 — Plumbing.** New `navisworks-mcp-plugin` project; copy SocketService/Logger; `NavisworksDispatcher` with UI-thread marshaling; one command `nw_get_model_stats`. Server: connection-manager refactor (§3.1 Option B) + `navisworks_tools.ts` with just `nw_get_model_stats`. **Exit criteria:** Claude calls `nw_get_model_stats` and gets live data from an open Navisworks doc.
2. **Phase 1 — Clash** (highest value): full Clash group + `nw_export_clash_report` wired into the existing Revit Clash Report Viewer round-trip.
3. **Phase 2 — Read/navigate:** viewpoints, selection/search sets, model tree, properties.
4. **Phase 3 — Appearance & files:** overrides, visibility, append/save.
5. **Phase 4 — 4D/5D:** TimeLiner + Quantification (COM interop as needed).
6. **Phase 5 — Installer + docs:** Navisworks detection, `.bundle` deploy, update counts/website.

Each phase is independently shippable and leaves the Revit path untouched.

---

## 8. Risks & open questions

- **API reference / build machine.** No official Navisworks NuGet. The build needs `Autodesk.Navisworks.Api.dll` from an installed Navisworks (or vendored DLLs committed to the repo). *Do you have Navisworks Manage installed on the build machine?*
- **Manage vs Freedom/Simulate.** The .NET API requires **Navisworks Manage** (or Simulate for a subset). Freedom (free viewer) has **no** API. Confirm target edition.
- **Thread marshaling.** No `ExternalEvent` equivalent — must marshal socket-thread calls to the UI thread via a captured `SynchronizationContext`. Slightly more manual than Revit; validated in Phase 0.
- **COM-only surfaces.** TimeLiner and Quantification are partially exposed only through the older COM API (`Autodesk.Navisworks.Api.Interop.ComApi`), which is clunkier and version-sensitive. May constrain Phase 4 scope.
- **Version span.** 2020–2027 crosses the .NET Framework→.NET 8 boundary (Navisworks moved at 2025), same split we handle for Revit — manageable but doubles the build matrix.
- **Auto-start.** Confirm the EventWatcher/startup-plugin approach loads reliably across all target versions, else fall back to manual ribbon start.

---

## 9. Effort estimate (rough)

| Phase | Scope | Est. |
|---|---|---|
| 0 | Plumbing + smoke test | 1–2 days |
| 1 | Clash | 2–3 days |
| 2 | Read/navigate | 2–3 days |
| 3 | Appearance/files | 1–2 days |
| 4 | 4D/5D (COM) | 3–4 days |
| 5 | Installer + docs | 1–2 days |

Phases 0–2 (a genuinely useful clash+navigation integration) ≈ **1 week**; full parity ≈ **2–2.5 weeks**.
