# Civil 3D, AutoCAD and Blender MCP servers in OpenCode: plan

_Date: 2026-10-26. OpenCode 1.19.0, config `%USERPROFILE%\.config\opencode\opencode.json`._

## 0. What this machine has today

| Needed for | Status |
|---|---|
| AutoCAD 2026 (25.1, update 2026.1.2) | Installed |
| Civil 3D | **Not installed** |
| Blender | **Not installed** |
| Python 3.12 + 3.11, `uv`/`uvx` 0.11.13 | Installed (`uv` came from pip; the Blender docs prefer the standalone installer, but pip's works) |
| Node (`C:\Program Files\nodejs`), git | Installed |
| .NET SDK 9.0 and 10.0 | Installed (both can build a `net8.0` project) |

Ports already in use by BIM-Bot: Revit **8080**, Navisworks **8091–8101**, Unreal **30010/30011**.

## 1. The servers

| App | Server | Why this one | Licence | Tools |
|---|---|---|---|---|
| Civil 3D | [`Sacred-G/Civil3D-mcp`](https://github.com/Sacred-G/Civil3D-mcp) | The only Civil 3D server with real depth; MAG pins it too | MIT | 34 default, about 218 in the full catalogue |
| AutoCAD | [`daobataotie/CAD-MCP`](https://github.com/daobataotie/CAD-MCP) | COM through pywin32, no plugin to build; MAG pins it too | MIT | 35 |
| Blender | [`ahujasid/blender-mcp`](https://github.com/ahujasid/blender-mcp) (PyPI `mcp-for-blender`) | The standard one (about 32k stars), runs from `uvx` | MIT | 9 |

All three are third-party code that drives your desktop apps. Clone each one at a reviewed commit and keep that hash, the way MAG pins them, rather than tracking `main`.

## 2. Rules for all three in OpenCode

1. **Keep them out of every session by default.** Turn their tools off at the top level (`"tools": { "Civil3D_*": false, ... }`) and give each app its own primary agent that turns them back on, the same way the `BIM-Bot` agent works now. Then a normal OpenCode session doesn't load 300 more tool schemas.
2. **Use per-tool `permission` entries**, as you already do for `BIM-Bot-Unreal_*`: `allow` for read tools, `ask` for anything that changes or saves a file, `deny` for raw code or command execution.
3. **Use full paths in `command`** (`node.exe`, `python.exe`, `uvx.exe`). OpenCode doesn't always get your shell `PATH`.
4. **Set `"timeout": 30000`.** A cold start under antivirus can take longer than the default 5 s.
5. **Check each one** with `opencode mcp list`, then start a session in that app's agent and call a read-only tool first.

## 3. AutoCAD (done, tested live on 2026-10-26)

**Status:** working end to end against AutoCAD 2026 (25.1), both directly over MCP stdio and through `opencode run --agent AutoCAD`.

**How it works:** a Python stdio server attaches to the running AutoCAD over COM. Nothing gets loaded into AutoCAD.

**What's installed**
- CAD-MCP at `C:\Users\hassa\mcp-servers\CAD-MCP`, commit **`628a928`**, venv with Python 3.12 (mcp 1.30, pywin32 312, pydantic 2.14, Pillow 13). Its log is `src\cad_mcp.log`.
- `src\config.json`: `cad.type` = `AUTOCAD`, **`cad.progid` = `AutoCAD.Application.25.1`** (see below), `transport` = `stdio`, `output.directory` = `C:\Users\hassa\mcp-servers\CAD-MCP\output`.

**Live test results**

| Check | Result |
|---|---|
| `list_layers`, `create_layer`, `set_current_layer` | Pass |
| `draw_line` on a test layer, `list_entities`, `get_entity_properties` (start, end and length right) | Pass |
| `screenshot` | Pass: a PNG of the whole AutoCAD window |
| `undo` | Pass in 3 of 4 runs. The miss was the very first run, right after AutoCAD opened, when attaching had just failed twice. `undo` still reports success when it undoes nothing, so check with `list_entities` |
| OpenCode, allowed tools (draw circle, list, undo, screenshot) | Pass |
| OpenCode, `ask` on `erase_entity` and `save_drawing` | Pass: TUI prompts; `opencode run` auto-rejects ("permission requested … auto-rejecting"), nothing changed |
| OpenCode, `deny` on `send_command` / `process_command` | Pass, but **only** as agent-level rules (see below) |

**Problems found and fixed**
1. **AutoCAD 2027 is also on this machine** (`C:\Program Files\Autodesk\AutoCAD 2027\acad.exe`, registered as `AutoCAD.Application.26`). CAD-MCP tries the newest ProgID first, so it went to 2027 before 2026, and with 2026 closed it would have *launched 2027*. Fixed by pinning `cad.progid` to `AutoCAD.Application.25.1`; it now attaches to 2026 on the first try. It still falls back to `.26` if 2026 isn't running but 2027 is.
2. **Top-level `permission` rules don't apply to MCP tools in OpenCode 1.19.** With `"AutoCAD_send_command": "deny"` at the top level, the agent called it and it ran (a harmless `REGEN`). The rules now live in `agent.AutoCAD.permission`, where they work: `"AutoCAD_*": "allow"` first, then `ask` for erase/new/open/close/save and `deny` for send_command/process_command (last match wins; a deny hides the tool from the agent). The 35 top-level `AutoCAD_*` entries were removed. **The same problem probably affects the top-level Blender, Civil 3D and BIM-Bot-Unreal rules.**
3. **Tool replies are in Chinese** (CAD-MCP's own messages), and the free model answered in Chinese too. The agent prompt now says to answer in the user's language and translate them, and that refused ask-first calls mean stop and explain.

**CAD-MCP quirks (upstream, not patched)**
- `create_layer` says "created and set current" even when the layer already exists.
- `draw_*` with a `layer` creates the layer if it's missing.
- The first calls right after AutoCAD opens can fail on `Documents`/`Count` while it's still busy; a retry a few seconds later works.

**Setup steps (as done)**
1. Clone it to `%USERPROFILE%\mcp-servers\CAD-MCP` and note the commit.
2. Create a venv there: `py -3.12 -m venv .venv`, then `.venv\Scripts\pip install -r requirements.txt` (pulls in pywin32, mcp, pydantic, Pillow).
3. In `src\config.json`, set `cad.type` to AutoCAD, set `cad.progid` to the installed year's versioned ProgID, keep `transport` as `stdio`, and point `output.directory` at a scratch folder, not a project folder.
4. Start AutoCAD 2026 and open a test drawing.
5. Add this to `opencode.json`:

```json
"mcp": {
  "AutoCAD": {
    "type": "local",
    "command": [
      "C:\\Users\\hassa\\mcp-servers\\CAD-MCP\\.venv\\Scripts\\python.exe",
      "C:\\Users\\hassa\\mcp-servers\\CAD-MCP\\src\\server.py"
    ],
    "enabled": true,
    "timeout": 30000
  }
}
```

6. **Agent and permissions:** add an `AutoCAD` primary agent with `"tools": { "AutoCAD_*": true }`, a short prompt (test drawings, read before write, never save over the open file without asking, answer in the user's language), and the rules **inside the agent**:

```json
"permission": {
  "AutoCAD_*": "allow",
  "AutoCAD_erase_entity": "ask",
  "AutoCAD_new_drawing": "ask",
  "AutoCAD_open_drawing": "ask",
  "AutoCAD_close_drawing": "ask",
  "AutoCAD_save_drawing": "ask",
  "AutoCAD_send_command": "deny",
  "AutoCAD_process_command": "deny"
}
```

7. **Check:** list the layers, draw one line on a scratch layer, take a screenshot, then undo, and confirm with `list_entities` that the line is gone.

**Risks**
- With AutoCAD 2026 and 2027 both running, the pinned ProgID picks 2026; with only 2027 running, it attaches to 2027.
- If AutoCAD isn't running, the first tool call starts it (2026, because of the pin) and waits 20 s.
- `opencode run` auto-rejects every `ask`, so scripted runs can't erase or save; use the TUI for those.

**Fallback:** [`C48H74/autocad-mcp`](https://glama.ai/mcp/servers/C48H74/autocad-mcp) is also COM-based, keeps AutoLISP off by default, and adds tools for block attributes and Excel round trips.

## 4. Blender (install Blender first)

**How it works:** a Blender add-on opens a socket on **9876**, and `uvx mcp-for-blender` is the stdio MCP server that talks to it.

**Steps**
1. Install Blender: `winget install BlenderFoundation.Blender`.
2. Install the add-on: `uvx mcp-for-blender install-addon`. Then in Blender, enable it under Edit → Preferences → Add-ons, press `N` in the viewport, open the **MCP for Blender** tab and start the server.
3. Add this to `opencode.json`:

```json
"Blender": {
  "type": "local",
  "command": [
    "C:\\Users\\hassa\\AppData\\Local\\Programs\\Python\\Python312\\Scripts\\uvx.exe",
    "mcp-for-blender"
  ],
  "environment": {
    "BLENDER_HOST": "localhost",
    "BLENDER_PORT": "9876",
    "BLENDER_MCP_SAFE_MODE": "1",
    "DISABLE_TELEMETRY": "true"
  },
  "enabled": true,
  "timeout": 60000
}
```

   Pin a version (`mcp-for-blender==<version>`) once one is checked. The first `uvx` run downloads packages, which is why the timeout is 60 s.
4. **Permissions:** `allow` `get_scene_info`, `look`, `get_addon_status` and `search_assets`. `ask` for `import_asset` and `generate_3d` (generate_3d calls paid outside services). **`ask` for `execute_blender_code`**, with safe mode on. Blender can do very little without it, so `deny` would make the server close to useless.
5. **Agent:** add a `Blender` agent with `"tools": { "Blender_*": true }` and the instruction to save the .blend file before running any code.
6. **Check:** get the scene info, then call `look` to get a viewport image.

**Risks**
- The add-on's socket has **no authentication**. Any local process can run Python in Blender while it's on. Start it only when you need it, and keep it on localhost.
- Telemetry is on by default. The `DISABLE_TELEMETRY` setting above turns it off.

## 5. Civil 3D (built for 2027; only a live test is left)

_Updated 2026-10-26._

**What's installed:** the earlier check missed it, but **Civil 3D 2027** (13.9, update 2027.1) is installed, at `C:\Program Files\Autodesk\AutoCAD 2027\C3D`. The project only supports and tests **2026**, so 2027 runs on a small local patch (below).

**How it works:** a Node MCP server talks to a .NET plugin (`Civil3DMcpPlugin.dll`) that you `NETLOAD` into Civil 3D. They talk JSON-RPC over TCP on loopback. Loading the plugin starts its listener right away (`C3DMCPSTOP` / `C3DMCPSTART` / `C3DMCPSTATUS` control it).

### Done

| Step | Result |
|---|---|
| Clone | `C:\Users\hassa\mcp-servers\Civil3D-mcp`, pinned at **`9e437a3`** (merge of PR #5, 2026-08-16), v1.2.1, MIT |
| Code review | No outbound network calls (Autodesk help links are only built as text, never fetched). The plugin listens on **loopback only**, with **no token**. The HTTP bridge always starts on loopback, with no token unless `MCP_HTTP_TOKEN` is set. If its port is taken, it logs an error and the server keeps running |
| Node server | `npm ci` (242 packages) and `npm run build`: OK. **422/422 unit tests pass.** A `tools/list` smoke test returns **34 tools** in about 0.4 s, without Civil 3D running |
| Plugin | Builds against the **2027** DLLs with 0 warnings and 0 errors. Output: `Civil3D-MCP-Plugin\bin\Release\net10.0-windows\Civil3DMcpPlugin.dll` |
| OpenCode | `Civil3D` server entry (**`"enabled": false`**), `"Civil3D_*": false` at the top level, a `Civil3D` agent, and permissions. Backup: `opencode.json.before-civil3d` |

### Local patch (2 files, saved as `C:\Users\hassa\mcp-servers\Civil3D-mcp-local-2027-port8095.patch`)

1. **Port.** Upstream hard-codes `public const int Port = 8080;` in `PluginRuntime.cs`, with no setting to change it. That's the same port as BIM-Bot Revit. The patch reads **`CIVIL3D_PORT`** from the Civil 3D process environment and falls back to **8095**:
   ```csharp
   public static readonly int Port =
     int.TryParse(Environment.GetEnvironmentVariable("CIVIL3D_PORT"), out var port) && port is > 0 and < 65536
       ? port
       : 8095;
   ```
   You start Civil 3D yourself, so it won't see OpenCode's env vars. That's why the default (8095) does the work, and no Windows env var is needed.
2. **Framework.** Civil 3D 2027 runs on **.NET 10** (its `acdbmgd.runtimeconfig.json` says `net10.0`), so `TargetFramework` goes from `net8.0-windows` to **`net10.0-windows`**.

**The port setting on both sides**

| Side | Setting | Value |
|---|---|---|
| Plugin (inside Civil 3D) | Patched default in `PluginRuntime.Port`; a `CIVIL3D_PORT` env var on the Civil 3D process overrides it | **8095** |
| Node server | `CIVIL3D_PORT` in the OpenCode entry (`ConnectionManager.ts` reads it, default 8080) | **8095** |
| HTTP bridge | `MCP_HTTP_PORT` in the OpenCode entry (default 3000, a common dev-server port) | **3017** |

To update: in the clone, run `git checkout -- Civil3D-MCP-Plugin` to drop the patch, then `git pull` (review the new commits first), then `git apply ..\Civil3D-mcp-local-2027-port8095.patch` to put it back. Then run `npm ci`, `npm run build`, and rebuild the plugin:

```bash
dotnet build -c Release -p:Civil3DReferencesPath=C:\Users\hassa\mcp-servers\Civil3D-refs-2027
```

`Civil3D-refs-2027` holds copies of the six reference DLLs. In 2027 they're spread over `AutoCAD 2027\`, `AutoCAD 2027\ACA` and `AutoCAD 2027\C3D`, but the project wants one folder. They're compile-time references only and aren't shipped.

### OpenCode entry (in place, disabled)

```json
"Civil3D": {
  "type": "local",
  "command": [
    "C:\\Program Files\\nodejs\\node.exe",
    "C:\\Users\\hassa\\mcp-servers\\Civil3D-mcp\\build\\index.js"
  ],
  "environment": {
    "CIVIL3D_HOST": "localhost",
    "CIVIL3D_PORT": "8095",
    "CIVIL3D_HELP_VERSION": "2027",
    "MCP_HTTP_PORT": "3017"
  },
  "enabled": false,
  "timeout": 30000
}
```

**Permissions.** The 34 tools are grouped by domain, and each mixes reads and writes behind an `action` argument. For example, `civil3d_surface` both lists and creates surfaces, so a per-tool `ask` would prompt on every read. The real gate is the server's approval flow. Any delete, import, export, or change that isn't safe to retry needs a token from `civil3d_request_approval`. **The AI can call that tool itself, and the server never asks you.** So the rules, under **`agent.Civil3D.permission`**, are:

- `"Civil3D_*": "allow"`
- `"Civil3D_civil3d_request_approval": "ask"`. This makes each approval a real yes or no from you.
- Never set `CIVIL3D_APPROVAL_MODE=disabled`.

The rules have to sit inside the agent. The AutoCAD track found that OpenCode 1.19 **ignores top-level `permission` rules for MCP tools**, so the first top-level version of these rules was moved. `opencode agent list` now shows the Civil3D agent's rules in this order: `Civil3D_*=deny` (from the global `tools` off switch), `Civil3D_*=allow`, `Civil3D_civil3d_request_approval=ask`. The last match wins, so approvals prompt. This is not yet tested with a live call, since the server is disabled. In `opencode run`, an `ask` is rejected automatically, so do the approval test in the TUI.

### Left to do (needs Civil 3D 2027 open)

1. Open Civil 3D 2027 with a **test** drawing, and `NETLOAD` `Civil3D-MCP-Plugin\bin\Release\net10.0-windows\Civil3DMcpPlugin.dll`. The command line should say *listener started on port 8095*. `C3DMCPSTATUS` confirms it.
2. Smoke-test from the clone: in `bash`, run `CIVIL3D_PORT=8095 npm run test:live-plugin`.
3. Set `"enabled": true` on the `Civil3D` entry and run `opencode mcp list`.
4. In the `Civil3D` agent: `civil3d_health`, `civil3d_drawing` (drawing info), then list surfaces and alignments. Then try one small change, to check that the approval prompt comes up.
5. If 2027 works, set up autoload using the registry key in `docs\DEPLOYMENT.md`, so you don't have to `NETLOAD` every time.

**Risks**
- 2027 isn't validated upstream. The build is clean, but API behaviour changes would only show up at runtime.
- The plugin socket has no token: while it's loaded, any local process can send it commands. That's the gap BIM-Bot closed in `62a5a66`. A follow-up patch could reuse that idea (a token in a user-only handshake file).

## 6. Order

1. **AutoCAD:** everything it needs is installed. About an hour.
2. **Blender:** install Blender, then about 30 minutes.
3. **Civil 3D:** blocked until Civil 3D 2026 is installed. Then it's the most work: Node build, .NET build, port change.

## 7. Later, for BIM-Bot itself

If these work well, BIM-Bot's installer could set them up the same way MAG does (gap report items #5 and #6). It would pin each repo, write the OpenCode, Claude and Codex entries, and add the agents and permissions above. A built-in AutoCAD connector, reusing the Revit socket pattern and the per-user token, would also avoid both the COM limits and Blender's unauthenticated socket.

## 8. Side note on the existing OpenCode config

`opencode.json` stores the Z.AI API key in plain text under `provider.provider-auth-zai.options.apiKey`. Move it to `opencode auth login`, as the README says, and rotate it if the file was ever shared or synced.
