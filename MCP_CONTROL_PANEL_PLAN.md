# BIM-Bot Control Panel — Implementation Plan

**Goal:** turn `mcp-dashboard/` into **one portable `.exe`** (`BIMBot-Control.exe`) that a user can run from anywhere (Downloads, USB, desktop) **without an admin password**, and that is the single control panel for BIM-Bot:

1. Switch any MCP server on/off, per AI program.
2. Switch any tool, or any whole **feature group**, on/off, per server and program.
3. Control updates per app (Revit, Navisworks, Unreal, MCP server, the panel itself): check, install, skip, auto-update on/off.
4. Test every MCP connection — and the live connection to Revit / Navisworks / Unreal behind it.
5. Repair broken connections with one click.

When you finish, reply with the **Report back** section at the end filled in.

---

## 0. What already exists (build on it — do not rewrite)

| File | What it does today |
|---|---|
| `mcp-dashboard/server.py` | Stdlib HTTP API on 127.0.0.1 with a per-run token. Reads/writes MCP configs for Claude Desktop (+ Store), Claude Code, Cursor, Windsurf, Gemini CLI, Antigravity, OpenCode. On/off per program (`set_enabled`, stash in `~/.bimbot/mcp-dashboard/disabled.json`), live MCP probe (`probe_stdio` / `probe_http`), static diagnosis + fixes (`diagnose` / `apply_fix`), per-tool filter (`set_tools`), hide servers, backups of every config write. |
| `mcp-dashboard/proxy.py` | stdio proxy that hides switched-off tools from `tools/list`, blocks their `tools/call`, and sends `notifications/tools/list_changed` when `tool-filter.json` changes. |
| `mcp-dashboard/app.py` | pywebview window + pystray tray icon; `app.py proxy …` runs the proxy. Single-instance mutex. |
| `mcp-dashboard/index.html` | The UI (overview matrix, per-program cards, Test / Tools / Configure / Details / Hide, toggles). |
| `mcp-dashboard/mcp-control.spec` | PyInstaller **one-folder** build: `BIMBot-MCP-Control.exe` (windowed) + `bimbot-mcp-proxy.exe` (console). |
| `mcp-dashboard/install.ps1` | Builds and copies to `%LOCALAPPDATA%\Programs\BIMBot\mcp-control` + Start-menu shortcut. |
| `revit-mcp-plugin/BIMBotPlugin/Core/UpdateChecker.cs` | Revit updater: reads `version.json` (elmthary.space, then GitHub raw), SHA-256 check, skip file `%LOCALAPPDATA%\BIMBot\skip_version.txt`. |
| `version.json` | Release manifest (`version`, `downloadUrl`, `sha256`, `changelog`, `minVersion`). |
| `installer/setup.iss` | Inno Setup, `PrivilegesRequired=lowest`, per-user by default. Revit add-ins → `%APPDATA%\Autodesk\Revit\Addins\<year>`. **Navisworks plugin → `<Navis install>\Plugins` (Program Files) → needs one UAC prompt.** |
| `revit-mcp-server/src/tools/register.ts` | Loads one tool file per feature (`*_tools.ts`, `navis_*.ts`); `BIMBOT_SCOPE=all|navis`. |
| `%LOCALAPPDATA%\BIMBot\auth.token` | Per-user token every socket request to the Revit/Navis plugins must carry (commit 62a5a66). |

Run it from source: `python mcp-dashboard/server.py --no-browser --port 8765` (launch config `mcp-dashboard`).

---

## 1. Hard rules (no-admin + portable)

- **Never write** to `Program Files`, `ProgramData`, `HKLM`, or any Autodesk install folder. Allowed: `%USERPROFILE%`, `%APPDATA%`, `%LOCALAPPDATA%`, `HKCU`.
- Exe manifest = `asInvoker` (PyInstaller default — add `uac_admin=False` explicitly in the spec so nobody flips it).
- No Windows service, no scheduled task with highest privileges, no driver, no firewall rule. Loopback only (`127.0.0.1`), as today.
- Updates launched by the panel run the installer with `/CURRENTUSER` so Inno never asks for elevation. The **only** known exception is the Navisworks plugin copy — see §5.4.
- Keep every config write going through `write_json` (atomic + backup). Keep the token check on every API call.
- Stdlib + pywebview + pystray + pillow only (current deps). No new heavy deps.

---

## 2. Portable single exe

**Problem:** a PyInstaller `--onefile` exe unpacks to a new `%TEMP%\_MEIxxxx` folder each run. Program configs must point the tool-filter proxy at a path that **stays valid** after the panel closes or is moved — so the configs can't point at the onefile exe's temp dir, and if they point at the portable exe itself, deleting/moving it breaks every filtered server.

**Design:**

1. Build **one onefile, windowed** exe: `BIMBot-Control.exe`. Bundle a second onefile **console** exe `bimbot-mcp-proxy.exe` inside it as data.
2. On every start, the panel ensures a **stable runtime folder** `%LOCALAPPDATA%\BIMBot\control\` containing:
   - `bimbot-mcp-proxy.exe` (copied if missing or a different hash/version — copy to `.tmp` then `os.replace`; if the file is locked because a program is using it, leave the old one and retry next start).
   - `version.txt`.
3. `server.launcher()` returns that stable proxy path when frozen (today it returns a sibling of `sys.executable`). The existing `rewrap` fix then migrates old entries that point at `Programs\BIMBot\mcp-control\bimbot-mcp-proxy.exe`.
4. State stays in `~/.bimbot/mcp-dashboard/` (already user-only). Add `--portable-data` flag later only if asked; not needed now.
5. Optional "Pin to Start menu" button in Settings: creates the `.lnk` in the user Programs folder pointing at a copy of the exe in `%LOCALAPPDATA%\BIMBot\control\` (same code as `install.ps1`, but from Python via `WScript.Shell` COM through `ctypes`/`powershell -NoProfile`). Never required.
6. WebView2 missing (rare on Win 10): fall back to opening the dashboard in the default browser (`server.main()` path) instead of crashing.
7. Update `mcp-control.spec`: two `EXE(...)` onefile targets; the proxy built first and added to the window exe's `datas`. Add a real icon (`installer/assets/bimbot.ico`). Set version info resource.
8. Startup time: onefile unpack is ~1–2 s; show the tray icon first, window after the server is up.

**Done when:** copying only `BIMBot-Control.exe` to a fresh standard-user Windows account, double-clicking it, switching off one tool of BIM-Bot in Claude Desktop, closing the panel and **deleting the exe** → Claude Desktop still starts BIM-Bot with that tool hidden.

---

## 3. On/off for every MCP and every feature

### 3.1 MCP servers (exists — keep, polish)
- Overview matrix cells become clickable toggles (today they only jump).
- "All programs" toggle per server row (on/off in every program at once).
- Show the program's `restart` hint after every change, with a **"Restart <program>" button** where safe (OpenCode/Claude Code: just the hint; Claude Desktop: offer to close & reopen `Claude.exe` for the user — ask first, it ends their chats).

### 3.2 Tools (exists — keep)
Per-tool toggles via `proxy.py` + `tool-filter.json`. Add search box and "all on / all off".

### 3.3 Feature groups (new)
Users think in features ("Excel/Sheets export", "AI code execution", "Navis clash", "Unreal"), not 267 tool names.

- **Server side** (`revit-mcp-server/src/tools/register.ts`): when registering tools from a file, tag each tool with its group, derived from the file stem (e.g. `export_tools` → `export`, `navis_clash_tools` → `navis-clash`). Put it in the tool's `_meta`: `{"bimbot/group": "<stem>", "bimbot/groupLabel": "<human name>"}`. A small `groups.json` next to `register.ts` maps stems → labels + a `risky` flag (code execution: `send_code_to_revit`, `navis_execute_code`, `execute_python`, `preview_code`).
- **Panel side:** `probe_stdio` already stores `tool_list`; also store `_meta` group. The Tools dialog groups tools by feature with a toggle per group (group toggle = add/remove all its tools in `tool-filter.json`). Servers with no groups (third-party) show a flat list as today.
- **Presets** (one click, per program): "Everything", "Read-only" (hide every tool whose name starts with `create_|delete_|modify_|set_|move_|...` or is in a `writes` list in `groups.json`), "No code execution", "Navisworks only". Stored in `settings.json` as `{presets: {...}}`.
- **Safe default:** offer (don't force) "No code execution" the first time the panel sees BIM-Bot.

### 3.4 Product features (new, small)
A **Settings → BIM-Bot features** page for switches that live outside MCP. Store in one shared file `%LOCALAPPDATA%\BIMBot\settings.json` that the plugins read at start-up:

| Key | Effect | Who reads it |
|---|---|---|
| `revit.autoStart` | Start the Revit socket listener with Revit | Revit plugin |
| `navis.autoStart` | `BIMBotAutoStart.cs` | Navis plugin |
| `updates.<app>.autoCheck` | see §4 | each updater |
| `telemetry` / AI provider toggles | only if they already exist in the plugins — **check, don't invent** | — |

Plugin changes here are small: read the JSON (missing file/key = current behaviour), no UI changes inside Revit required.

---

## 4. Updates per app

New tab **Updates**. One row per component:

| Component | Installed version — how to detect |
|---|---|
| MCP server | `%LOCALAPPDATA%\Programs\BIMBot\server\package.json` `version` |
| Revit plugin, per year | file version of `BIMBotPlugin.dll` in the folder the `%APPDATA%\Autodesk\Revit\Addins\<year>\BIMBot.addin` points to |
| Navisworks plugin, per year | file version of `<Navis>\Plugins\BIMBotNavisPlugin\BIMBotNavisPlugin.dll` (read-only access is fine) |
| Unreal plugin | `.uplugin` `VersionName` in projects the user adds (Unreal has no fixed install place — let the user pick project folders) |
| Control panel | its own version |

Latest version: fetch `version.json` the same way `UpdateChecker.cs` does (elmthary.space first, GitHub raw second).

Per row: **Check now**, **Update**, **Skip this version**, **Auto-check on/off**, changelog.

- **Update** = download `downloadUrl` to `%LOCALAPPDATA%\BIMBot\updates\`, verify `sha256` (refuse if missing or wrong — same rule as commit 4d09a41), then run `BIMBot-Setup.exe /CURRENTUSER /SILENT /COMPONENTS=... /TASKS=...` with only the chosen components/years. Check `setup.iss` `[Components]`/`[Tasks]` names and pass exactly those. Warn first if Revit/Navisworks is running (the DLL is locked) and offer to wait.
- **Skip / auto-check**: move the skip state from `%LOCALAPPDATA%\BIMBot\skip_version.txt` into the shared `%LOCALAPPDATA%\BIMBot\settings.json` (`updates.<app>.skip`, `updates.<app>.autoCheck`). Update `UpdateChecker.cs` (and the Navis updater if it has one) to read these, **keeping `skip_version.txt` as a fallback** for old installs. This is what lets the user say "update BIM-Bot in Revit but not in Navisworks".
- **Panel self-update**: same manifest gets an optional `control` block `{version, downloadUrl, sha256}`; the panel downloads the new exe next to itself as `.new`, verifies, and swaps on next start (a running exe can be renamed but not overwritten).

---

## 5. Test and fix connections

### 5.1 Test (extend existing probe)
Three levels, shown as three dots per server:
1. **Config** — `diagnose()` (exists).
2. **MCP** — `probe_stdio` / `probe_http` (exists): starts, answers `initialize`, lists tools.
3. **Host app** (new, BIM-Bot servers only) — in the same probe session, call the server's own status tool (`navis_connection_status` / `navis_bridge_status` for Navis, the Revit equivalent — find it in `revit-mcp-server/src/tools`, else `get_project_info`; `ping_unreal` for Unreal) and show "Revit 2025 connected / Revit not running / token rejected (-32001) / port in use".

"Test all" keeps running in parallel threads as today. Add a **Logs** button that shows `stderr` (already captured, last 1500 chars) and the BIM-Bot log file.

### 5.2 Fix (extend `diagnose` / `apply_fix`)
Keep the pattern: every detected problem returns a fix id; the user clicks it; the change is backed up.

| Problem detected | Fix |
|---|---|
| Node / server path missing | exists (`resolve-node`, `bimbot-path`) |
| Proxy path moved | exists (`rewrap`) → point at `%LOCALAPPDATA%\BIMBot\control\` |
| OpenCode timeout < 60 s | exists (`raise-timeout`) |
| BIM-Bot not registered in an installed program | exists (`can_add`) — add "Register in all programs" |
| Config file is invalid JSON | show the parse error and line; offer **Restore last good backup** (backups already exist in `~/.bimbot/mcp-dashboard/backups/`; add a Backups dialog listing them per file) |
| Socket token rejected (-32001) / `auth.token` missing | recreate is **not** safe while a plugin holds the old one — tell the user to restart Revit/Navisworks; offer to fix the file ACL to user-only if it is wrong |
| Port 8080 / 8091 busy by another process | show which process (`netstat -ano` + `tasklist`); offer to set `BIMBOT_PORT` / `BIMBOT_NAVIS_PORT` env on the entry — only if the plugin side can be told the same port; otherwise just report |
| Stale `node.exe` BIM-Bot processes | list them (command line contains `BIMBot\server\build\index.js`); offer to end them (current user's processes only) |
| Duplicate BIM-Bot entries under different names | offer to switch the duplicates off |
| Revit add-in manifest missing for an installed Revit year | offer "Reinstall plugin for Revit <year>" → runs the installer for that task only |

### 5.3 Fix everything
One **"Fix all safe issues"** button at the top that applies only fixes marked `safe=True` (path fixes, rewrap, timeout, register). Never ends processes or restores backups without a click on that specific fix.

### 5.4 Navisworks + admin
The Navisworks plugin lives in `<Navisworks install>\Plugins`, which needs admin. The panel must **not** try to elevate. Two options — **investigate and tell me which works**:
- (a) Navisworks also loads plugins from a per-user folder (`%APPDATA%\Autodesk Navisworks Manage <year>\Plugins` — **verify this on a real Navisworks 2024/2025**). If yes, the installer and panel switch to it and the UAC prompt goes away entirely.
- (b) If not, the panel shows "Navisworks plugin needs a one-time admin install" and a button that runs the installer normally (user sees UAC). Everything else stays admin-free.

---

## 6. UI changes (`index.html`)

Tabs across the top: **Servers** (today's page) · **Features** · **Updates** · **Health** · **Settings**.
- Keep the current dark style and layout patterns. Mobile width not needed (desktop window ≥ 420 px wide, already set).
- Every destructive action (end process, restore backup, run installer, close Claude Desktop) gets a confirm dialog naming exactly what will happen.
- Toasts for "Saved — restart <program> to apply".

---

## 7. Work order (one commit each, conventional commits like the repo history)

1. `feat(control): portable onefile build with stable proxy location` — §2. *Ship this first; everything else depends on it.*
2. `feat(control): feature groups and presets` — §3.3 (server `_meta` tags + panel UI).
3. `feat(control): health checks against Revit, Navisworks and Unreal` — §5.1.
4. `feat(control): more one-click fixes and backup restore` — §5.2–5.3.
5. `feat(control): per-app update control` — §4 (panel side).
6. `feat(plugin): read update and feature switches from settings.json` — §3.4 + §4 plugin side, with fallback to old files.
7. `feat(control): self-update`.
8. Navisworks per-user plugin investigation — §5.4, report findings before changing the installer.

Do not touch `installer/setup.iss` / `build-installer.ps1` — they have uncommitted changes in progress. If a step needs installer changes, write them up in the report instead.

---

## 8. Tests / acceptance

Run all of these on a **standard (non-admin) Windows user**:

- [ ] Fresh account, only `BIMBot-Control.exe` copied: starts, no UAC prompt, no SmartScreen-only blocker other than "unknown publisher" (note it if unsigned).
- [ ] Toggle BIM-Bot off/on in each of the 7 programs; files diff cleanly; backups created.
- [ ] Switch off one tool and one feature group; restart the program; the tools are gone; `tools/call` on a hidden tool returns an error.
- [ ] Delete / move the portable exe → filtered servers still work.
- [ ] Break each thing in §5.2 by hand → the panel detects it and the fix repairs it.
- [ ] Updates tab shows correct installed versions for Revit years present on the PC; "Skip" in the panel stops the Revit plugin's own prompt; Update runs with no UAC (except §5.4 b).
- [ ] Bad `sha256` in a test manifest → update refused.
- [ ] Two panel instances → second shows "already running" (exists).
- [ ] API still rejects requests without the token and from non-loopback hosts.
- [ ] Existing Python tests (if any under `mcp-dashboard/`) and `revit-mcp-server` tests pass; plugin builds for all years still compile.

---

## Report back (fill this in and send it to me)

```
1. Done steps (commit hashes):
2. Not done / blocked, and why:
3. Navisworks per-user plugin folder (§5.4): works? tested on which year(s)?
4. Exe size and cold-start time:
5. Things in this plan that were wrong about the code:
6. Plugin/installer changes you need me to approve:
7. How to run and test it:
```
