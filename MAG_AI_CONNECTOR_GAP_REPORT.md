# MAG AI Connector 0.3.3 vs BIM-Bot 2.7.6: gap report

_Date: 2026-10-25. Studied file: `MAGAIConnector-Setup-0.3.3.exe` (inside the .rar). Its SHA-256 `50efc141…0643` matches the checksum published for release v0.3.3._

## 1. How this was studied

- The .exe is an **Inno Setup 6.7** installer. innoextract 1.9 cannot unpack that version, and the installer was **not run**,
  so its compiled code was not decompiled.
- Its strings show the publisher **MAGtelligent** and the product **MAG AI Connector**. Its public repo
  (`github.com/MAGtelligent/MAGAIConnector`) holds only a README and release notes for 0.1.0 to 0.3.3, all released
  18 to 21 Oct 2026. Sections 2 and 3 come from those, plus the exact upstream MCP servers it pins.
- BIM-Bot was checked by searching the files tracked in this repo.

## 2. What MAG AI Connector is

MAG is **not its own MCP server**. It is a **setup and management desktop app** that installs and connects four
open-source MCP servers written by other people. It pins each one to a fixed commit:

| Design app | Bundled server | Size of its toolset |
|---|---|---|
| Revit 2024–2027 | `mcp-server-for-revit-python` (runs through **pyRevit**) | ~20 tools: model info, levels, families, place family, color splash, view PNG, open/save/close, sync with central, **launch Revit**, run IronPython |
| Navisworks 2025–2027 | `Aitology/Navisworks_MCP` | ~25: clash tests/groups/results, selection & search sets, viewpoints, properties, hide, transparency |
| **Civil 3D 2025–2027** | `Sacred-G/Civil3D-mcp` | **Very large**: alignments, profiles, corridors, surfaces, grading, pipe & pressure networks, hydrology/detention, parcels, COGO points, plan production (sheets/PDF), QC checks, quantity takeoff, data shortcuts, long-running jobs |
| **AutoCAD 2018+** | `daobataotie/CAD-MCP` (COM) | ~35: draw line/arc/circle/polyline/hatch/text, dimensions, layers, blocks, move/rotate/mirror/array/offset, screenshot, send command |

AI clients it sets up: Claude Desktop, Claude Code, OpenAI Codex, Cursor, OpenCode.

**Conclusion:** BIM-Bot's own tools are far deeper for Revit (267 tools vs ~20) and Navisworks. MAG's advantages are
**which apps it covers (Civil 3D, AutoCAD)**, **safety controls**, and **how polished setup and updates are**.

## 3. What BIM-Bot is missing, by priority

### P1: Security and trust (MAG markets these heavily)

| # | MAG feature | BIM-Bot today | Suggested work |
|---|---|---|---|
| 1 | **The plugin answers only its own connector.** Since 0.3.0 the Navisworks add-in rejects requests that don't come from MAG ("a web page cannot send it commands") | `SocketService.cs` / `NavisSocketService.cs` listen with a raw `TcpListener` on loopback and **check no token**. Any local process can send commands, including `send_code_to_revit` | Write a random token to the handshake file in `%LOCALAPPDATA%` (user-only access) and require it on every message. Reject payloads that start with `GET `/`POST ` (stops browser-originated HTTP) |
| 2 | **Approvals in Revit:** asks before changes and **before running code** (the user reviews the code in Revit) | `CodeExecutor.cs` only blocks a list of disallowed patterns; nothing asks the user. Modify, delete and purge tools run without prompting | Add a setting with Off / Ask before code / Ask before any change. Show a TaskDialog with the code or diff summary, with Allow, Deny and "Allow for this session" |
| 3 | **Read-only mode** (Navisworks, AutoCAD): the AI is given **only the tools that look**, including tools added later | No read-only mode. No MCP tool annotations (`readOnlyHint` / `destructiveHint` appear 0 times in `revit-mcp-server/src`) | Mark every `server.tool(...)` as read-only or destructive. Add an env flag `BIMBOT_READONLY=1` that skips registering destructive tools. Default to read-only for unknown or new tools |
| 4 | **Signed updates:** installs only updates signed by MAGtelligent, and checks the checksum | `UpdateChecker.DownloadUpdateAsync` downloads any `.exe/.zip/.msi` asset from the latest release **without checking a hash or signature** | Publish a `.sha256` (or an ed25519 `.sig`) beside each release asset and check it before running the file. Ideally Authenticode-sign the installer too (it also reduces SmartScreen warnings) |

### P2: More design apps (MAG's biggest product difference)

| # | Gap | Notes |
|---|---|---|
| 5 | **Civil 3D connector** | No Civil 3D support at all (the only "civil"/"acad" matches are DWG export). This market is underserved; even 15–20 core tools would compete: surfaces, alignments, profiles, pipe networks, COGO points, quantities |
| 6 | **AutoCAD connector** | No support. A COM-based or .NET plugin for draw, layers, blocks, dimensions and screenshots is cheap to build and reuses BIM-Bot's socket/bridge pattern |
| 7 | Long-running **job** tools (start / status / cancel) | Civil3D-mcp has these. BIM-Bot's heavy tools (exports, purges, clash runs) block until done. A job pattern avoids MCP client timeouts |

### P3: Setup and maintenance experience

| # | MAG feature | BIM-Bot today | Suggested work |
|---|---|---|---|
| 8 | A **stand-alone manager app** (dashboard with live status per connection, connect/disconnect per AI app, light/dark mode) | The wizard runs only during install. After that: the ribbon **Connect Claude** button plus `scripts/bimbot-doctor.ps1` (a script, not UI) | Turn `RevitMCPInstaller` into a small tray/dashboard app, or add a "Connections" window in the plugin |
| 9 | **One-click "Test connection"**: checks each design app, starts each MCP server, confirms every AI app config matches the current settings, and explains fixes in plain words | `bimbot-doctor.ps1` covers part of this, but only from a terminal | Expose the doctor checks in the UI with plain-language fixes. Also flag an AI-app config that is **stale** (old path or version) |
| 10 | **In-app update**: banner with "What's new" / "Update now", installs in about a minute, then **reconnects the AI apps automatically**; "check at start-up" switch | An update checker exists (GitHub + elmthary.space). Confirm whether it re-runs the client-config step after the update | Re-write the AI-client configs after an update, show release notes, add an opt-out switch |
| 11 | **Launch the app from the AI** (`launch_revit`, `list_revit_installations`) | Missing. The AI can't start Revit or open a model when Revit is closed | Add an MCP tool that runs on the server side and needs no plugin: find `Revit.exe` per year, start it, wait for the handshake port |
| 12 | Greys out apps that aren't installed or are unsupported versions, with a note naming the supported versions | The installer detects 2020–2027, but check whether it explains *why* a version is skipped | Small UI change |
| 13 | Detects apps **outside `C:\Program Files`** (e.g. Navisworks on D:) | Check: detection may rely on default paths | Read install paths from the registry (`HKLM\SOFTWARE\Autodesk\…`) |
| 14 | Works behind **company HTTPS inspection** (uses the Windows certificate store) | Node uses its own CA bundle, so npm/HTTPS calls fail behind such proxies | Set `NODE_OPTIONS=--use-system-ca` (Node 22.15+/23.8+) or `NODE_EXTRA_CA_CERTS` in the generated MCP config |
| 15 | **Only one copy runs**; old config backups are tidied up automatically | `.bimbot-backup` files are created but never cleaned up | Keep only the last N backups |
| 16 | Opt-in, **anonymous setup telemetry** (which steps fail and how long they take; no paths or models) | None. Install failures are invisible unless users report them | Optional. If added, make it opt-in or clearly disclosed, like MAG |

## 4. Where BIM-Bot is already ahead (keep these in your marketing)

- **267 tools** vs ~20 for Revit in MAG: modeling, MEP, sheets, schedules, audits, ISO 19650/BEP/IDS validation, links,
  worksharing, checkpoints/rollback, Excel/Sheets/Notion/Power BI/SQLite exports.
- **Revit ↔ Navisworks bridge** (clash-to-Revit, selection sync, handoff tables). MAG's servers are separate and don't
  talk to each other.
- **Revit 2020–2027** (MAG: 2024–2027), Navisworks 2020–2027 (MAG: 2025–2027).
- No pyRevit dependency (MAG's Revit side **needs pyRevit**, and 2 of its 6 releases fix pyRevit pairing problems).
- More AI clients: Windsurf, VS Code, Gemini CLI, Antigravity, plus built-in Gemini/Ollama chat inside Revit.
- An Unreal connector, Power BI visual, and AI analysis tools.
- `preview_code` + pattern blocking for code execution (MAG only gives the user a review prompt).

## 5. Suggested order

1. Socket token auth (#1) and update hash check (#4): small changes that close real security holes.
2. Tool annotations + read-only mode (#3), then the approval dialog (#2).
3. `launch_revit` (#11) and system CA (#14): quick wins.
4. A manager app with Test connection (#8, #9, #10).
5. AutoCAD connector (#6), then Civil 3D (#5): new markets, the most work.

## Sources

- [MAGtelligent/MAGAIConnector README](https://github.com/MAGtelligent/MAGAIConnector) and release notes v0.1.0 to v0.3.3
- [mcp-server-for-revit-python @6ca46b3](https://github.com/mcp-servers-for-revit/mcp-server-for-revit-python/tree/6ca46b3ea96a8f7cce9894cf865d8f19ca2c549d) (`mcp-tool-manifest.json`)
- [Aitology/Navisworks_MCP @97c3b63](https://github.com/Aitology/Navisworks_MCP/tree/97c3b63d798f3ce9e6cec320fb13560a3165be2d)
- [Sacred-G/Civil3D-mcp @9e437a3](https://github.com/Sacred-G/Civil3D-mcp/tree/9e437a3bab6df4ba13fabc2e909e138e8ac15896) (`docs/tools.generated.md`)
- [daobataotie/CAD-MCP @bfcf9bb](https://github.com/daobataotie/CAD-MCP/tree/bfcf9bb1da8f828b0787af12194717364a7c4a50)
