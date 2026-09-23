# Power BI Integration — Finalized (offline)

Two features, both directions:

| Direction | Tool | What it does |
|---|---|---|
| Revit → Power BI | `export_to_powerbi` | Exports 3D geometry + parameters to a ready-to-open `.pbit` dashboard with the BIM-Bot 3D Viewer custom visual embedded |
| Power BI → Revit | `show_powerbi_report` | Opens an interactive Power BI report inside Revit (WebView2), via Publish-to-Web URL or authenticated Azure AD embedding |

## 0. Offline guarantee

The exported dashboard needs **no internet at any point**:

- The 3D Viewer visual (including Three.js) is compiled **into** the `.pbit` — verified: the package contains zero network calls (only a three.js console-message string).
- Data comes from the local `data/*.csv` files next to the `.pbit`; all other visuals are built-in.
- The report opens directly on the **3D Dashboard** page (single page, `activeSectionIndex: 0`).
- Every generated `.pbit` is self-verified (`PbitGenerator.VerifyPackage`): package skeleton + embedded 3D visual must be present or generation fails instead of producing a file that opens without 3D.
- The in-Revit viewer (`show_powerbi_report`) is the only part that needs internet (Power BI Service embeds); the exported file does not.

Portable single-file flow: double-click the `.pbit` once (loads the local CSVs) → File → Save As `.pbix`. The `.pbix` embeds data + visual and can be mailed / archived / opened fully offline.

## Export contents checklist

Every `export_to_powerbi` (pbit format) produces and verifies:

- [ ] **3D Dashboard page** — opens on this page; BIM-Bot 3D Viewer visual pre-wired (ElementId, ChunkIndex, Category, MeshJSON, RGB)
- [ ] **3D view is controllable** — left-drag rotate, right/middle-drag pan, wheel zoom, hover tooltip (Category — ID)
- [ ] **Category slicer** → 3D cross-filters (dim or hide, see below)
- [ ] **Level slicer, Family slicer, Parameter + Parameter Value slicers** → 3D cross-filters
- [ ] **KPI cards** — element count, category count
- [ ] **Elements-by-Category bar chart** — clicking a bar filters the 3D view; clicking a 3D element filters the chart (Ctrl+click = multi)
- [ ] **Tables** — Elements, Parameters, Geometry (chunked MeshJSON), CategoryColors, ModelInfo, all related bidirectionally
- [ ] **3D Viewer custom visual embedded** — no separate `.pbiviz` import needed

### Dim vs Hide (your choice)

Selecting info (e.g. the Columns category) cross-filters the 3D view. Two behaviors, switchable per report:

- **Dim (default)** — non-selected elements turn translucent, selection stays in context.
- **Hide** — non-selected elements disappear completely (isolated view).

Switch: click the 3D visual → Format → Rendering → **"Hide filtered-out elements"** on/off. Hidden elements also stop intercepting clicks and hover tooltips.

## 1. Export: Revit → Power BI dashboard

```
MCP: export_to_powerbi(exportScope="currentView", format="pbit")
 → <Project>_PowerBI/
     data/Elements.csv        ElementId, Category, FamilyName, TypeName, LevelName, Mark
     data/Parameters.csv      ElementId, ParamName, ParamValue (max 200/element, values >500 chars skipped)
     data/Geometry.csv        ElementId, ChunkIndex, MeshJSON (30k-char chunks — PBI truncates text at 32,766)
     data/CategoryColors.csv  Category, R, G, B, ColorHex
     data/ModelInfo.csv       export metadata
     <Project> 3D Dashboard.pbit   double-click to open
```

- Geometry is tessellated mesh (`{"v":[...],"f":[...]}` in meters) extracted via `CustomExporter` from the active 3D view (`currentView`) or the default `{3D}` view (`allModel`).
- The `.pbit` bakes the absolute data-folder path into its M queries, so it loads with zero prompts. If you move the folder, refresh the `File.Contents` paths in Power Query.
- The 3D Viewer visual (`guid bimBot3DViewer1A2B3C4D`) is embedded in the `.pbit` — no separate `.pbiviz` import needed.
- All model relationships are bidirectional, so Category/Level/Family/Parameter slicers and the bar chart cross-filter the 3D view, and clicking a 3D element filters the other visuals (Ctrl+click = multi-select).
- Empty exports (hidden view, wrong category filter) fail fast with a clear message instead of producing an empty dashboard.
- Legacy `format="sqlite"` path is unchanged (requires an ODBC driver; prefer `pbit`).

### Limits

- Very large models (>~8k elements) render but orbit gets heavy — the HUD shows a warning; use slicers to narrow down. Geometry above Power BI's 30k-row `window` reduction streams in via `fetchMoreData`.
- Parameters are capped at 200 per element to keep refresh fast.

## 2. Custom visual: `revit-mcp-powerbi-visual/`

Three.js viewer with manual orbit (left-drag rotate, right/middle-drag pan, wheel zoom), hover tooltips, HUD stats, and bi-directional cross-filtering.

Key implementation notes:

- **Inbound filtering** uses a row-subset heuristic: table mappings carry no highlight payload, so a page that is a strict subset of all loaded ElementIds means "filtered" → ghost the rest. Segment streaming never ghosts; disjoint pages reset the baseline (new model).
- **Outbound selection** goes through `SelectionManager`; external selections resolve back to ElementIds via the current page's selection pairs.
- **Formatting pane** (`capabilities.json → objects`): Rendering (background color, dimmed opacity, hide-filtered-out toggle, wireframe) and Interaction (click-to-filter, orbit, idle auto-rotate + speed). Parsed via `VisualSettings.parse` in `update()`.
- Click-vs-drag is measured from the mousedown position; listeners and the render loop are torn down in `destroy()`.

### Rebuild + re-sync (required after any `src/` change)

```powershell
cd revit-mcp-powerbi-visual
./node_modules/.bin/pbiviz package
Copy-Item dist\bimBot3DViewer1A2B3C4D.1.0.0.0.pbiviz `
  ..\revit-mcp-plugin\BIMBotPlugin\PowerBI\Assets\bimBot3DViewer1A2B3C4D.pbiviz -Force
```

`PbitGenerator` embeds `PowerBI/Assets/*.pbiviz` into every generated `.pbit`, so the Assets copy must stay in sync with `dist`. (`npm run lint` = `tsc --noEmit`.)

## 3. Viewer: Power BI report inside Revit

- **Public URL mode** (no setup): paste a Publish-to-Web URL (`https://app.powerbi.com/view?r=...`). Enable in Integrations Settings → Power BI → paste URL.
- **Authenticated mode**: set `POWERBI_CLIENT_ID` (+ optional `POWERBI_TENANT_ID`, default `common`) in `revit-mcp-server/.env`, sign in via Integrations Settings (OAuth2 + PKCE, `localhost:3848` callback; needs Azure AD app with `Report.Read.All`, `Workspace.Read.All`). Then pick workspace/report in the viewer toolbar.
- Non-http(s) URLs are rejected; unknown hosts get a warning (sovereign clouds still work). WebView2 Runtime missing → install prompt.

## 4. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| "No exportable 3D geometry found" | Active view isn't 3D, everything hidden, or category filter matches nothing — open a 3D view / use `allModel` |
| `.pbit` asks for data source on open | Data folder moved — update `File.Contents` paths in Power Query |
| 3D visual shows hint, no model | Drag ElementId + ChunkIndex + MeshJSON (+ Category, colors) into the visual's fields |
| Slicers don't dim the model | Update the visual to the build in `dist/` (ghosting needs the current version) |
| Viewer shows blank / error | Public URL needs Publish-to-Web enabled; authenticated embed needs Power BI Pro/PPU license |
| Sign-in loops | Refresh token expired — sign out/in via Integrations Settings |

## 5. Acceptance checklist

- [ ] `export_to_powerbi` on a small model → `.pbit` double-click opens with 3D model, slicers, cards, bar chart, no prompts
- [ ] Disconnect from the internet → dashboard still opens and orbits (offline proof)
- [ ] Click bar-chart category → 3D ghosts everything else; clear → restores
- [ ] Select "Columns" in the Category slicer → only columns solid in 3D
- [ ] Format → Rendering → "Hide filtered-out elements" ON → non-selected categories disappear; OFF → they dim
- [ ] Click 3D element → other visuals filter; Ctrl+click → multi; empty-space click → clear
- [ ] Formatting pane shows Rendering + Interaction sections and they take effect
- [ ] Empty view export → clear error, no empty `.pbit`
- [ ] `show_powerbi_report` with public URL loads; authenticated mode lists workspaces/reports
