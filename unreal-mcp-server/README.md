# BIM-Bot Unreal

An MCP (Model Context Protocol) server that bridges AI assistants (Claude, Gemini, etc.) with Unreal Engine's editor. Control your Unreal projects through natural language — spawn actors, manage Blueprints, manipulate materials, build lighting, and more.

## Architecture

```
┌─────────────────┐     MCP/stdio     ┌──────────────────┐     HTTP     ┌──────────────────────┐
│   AI Assistant   │ ◄──────────────► │  BIM-Bot Unreal  │ ◄──────────► │  Unreal Engine       │
│  (Claude, etc.)  │                  │  Server (Node)   │    :30010   │  Remote Control API  │
└─────────────────┘                  └──────────────────┘    :30011   │  MCP Bridge Plugin   │
                                                                       └──────────────────────┘
```

## Prerequisites

1. **Unreal Engine 5** (tested on 5.8.3)
2. Nothing else when you use `BIM-Bot-Unreal.exe`: it has Node built in and sets up the Unreal project for you (see [Standalone .exe](#standalone-exe-auto-connect))
3. *(Optional)* **UnrealMCPBridge plugin**: only adds real editor toast pop-ups. Without it every tool still works through Python, and `show_notification` writes to the Output Log instead

## Manual Setup Guide

Only needed if you don't use the exe's project setup.

### 1 — Enable the built-in Remote Control API plugin

Open your project in Unreal Engine 5.6, then:

```
Edit → Plugins → search "Remote Control API" → Enable → Restart Editor
Edit → Plugins → search "Python Editor Script Plugin" → Enable → Restart Editor
```

The Remote Control API listens on **port 30010** (default).

Then allow the Python calls (UE 5.1+ refuses them by default). In `Config/DefaultRemoteControl.ini`:

```ini
[/Script/RemoteControlCommon.RemoteControlSettings]
bAutoStartWebServer=True
RemoteControlHttpServerPort=30010
bEnableRemotePythonExecution=True
+CustomAllowedRemoteFunctionCalls=(ClassPath="/Script/PythonScriptPlugin.PythonScriptLibrary",bAllowChildClasses=False)
RemoteControlWebsocketServerBindAddress=127.0.0.1
```

The last line keeps the WebSocket server (which defaults to `0.0.0.0`) from letting other machines run Python. The HTTP server on port 30010 already listens on `127.0.0.1` only.

---

### 2 — *(Optional)* Build and enable the UnrealMCPBridge plugin

The plugin is already placed at:
```
Documents/Unreal Projects/MyProject/Plugins/UnrealMCPBridge/
```

**Build it:**
1. Close the Unreal Editor
2. Right-click `MyProject.uproject` → **Generate Visual Studio project files**
3. Open `MyProject.sln` in Visual Studio 2022
4. Set solution config to **Development Editor / Win64**
5. Build → the plugin compiles alongside the project
6. Reopen the editor → `Edit → Plugins → Project → UnrealMCPBridge → Enable`

The bridge starts automatically on **port 30011** when the editor loads.
Check the Output Log for: `[UnrealMCPBridge] Listening on http://localhost:30011`

> **Alternative (no VS required):** Right-click `MyProject.uproject` → **Switch Unreal Engine version** → select `5.6` → then double-click to open. UE will prompt to rebuild missing modules automatically.

---

### 3 — Connect Claude Desktop

The config is already written to:
```
C:\Users\hassa\AppData\Roaming\Claude\claude_desktop_config.json
```

Restart Claude Desktop — the **unreal** MCP server will appear in the tools panel.

**Test the connection** by asking Claude:
> "Ping Unreal Engine and tell me if it's connected"

---

### 4 — Verify everything is working

| Check | Expected result |
|---|---|
| `ping_unreal` tool | `connected: true` |
| `get_project_info` tool | Returns engine version + project name |
| `capture_viewport` tool | Returns a base64 PNG image |
| `execute_python` tool | Runs Python in the editor |

---

## Standalone .exe (auto-connect)

`dist/BIM-Bot-Unreal.exe` is a single file with Node bundled in — no `npm install` needed.

**Double-click it** (or run `BIM-Bot-Unreal.exe --install`). It:
1. Lists the Unreal Engine versions installed on this PC
2. Copies itself to `%LOCALAPPDATA%\Programs\BIM-Bot Unreal\BIM-Bot-Unreal.exe` (outside OneDrive)
3. Registers `BIM-Bot-Unreal` with **Claude Desktop**, **Claude Code** (`~/.claude.json`) and **OpenCode** (`~/.config/opencode/opencode.json`). Each config is backed up to `*.bak` first. Old `unreal-mcp` registrations are replaced
4. Writes the tool permissions below into Claude Code (`~/.claude/settings.json`) and OpenCode (`opencode.json` → `permission`). Your other rules are kept
5. Lists the projects in `Documents\Unreal Projects` and on the Desktop (up to two folders deep) and asks which to set up (Enter = all that need it). Setting up a project:
   - enables the **Remote Control API**, **Python Editor Script** and **Editor Scripting Utilities** plugins in the `.uproject`
   - writes the `Config/DefaultRemoteControl.ini` shown in the manual guide above

   Changed files are backed up to `*.bak` first. Open editors need a restart to pick it up.

Then restart the client. Other flags:

| Command | Does |
|---|---|
| `--setup-engines` | Turn the plugins and Remote Control config on by default in every installed engine, so **any** project connects with no setup of its own. Asks for admin if the engine folder needs it. The installer runs this; re-run it after an engine update or "Verify", which restore the stock files. Projects that explicitly disable one of the plugins keep it off |
| `--setup <project.uproject or folder> ...` | Set up those projects without asking. With no path: **every** project in `Documents\Unreal Projects` and on the Desktop |
| *drop a `.uproject` on the exe* | Install, then set up that project |
| `--status [paths]` | Show Unreal versions, registrations and which projects are ready |
| `--uninstall` | Remove the registrations and permissions (projects are left as they are) |
| `--serve` | Force MCP server mode |

**Sending it to someone:** `dist/BIM-Bot-Unreal.exe` is all they need. The exe is unsigned, so Windows SmartScreen may warn on first run ("More info" → "Run anyway").

When an MCP client launches the exe (with piped stdio), it runs the MCP server directly.

**Rebuild:** `npm run build:exe`. Run it from a folder that is stored locally: OneDrive cloud-only `node_modules` make esbuild time out.

## AI Permissions

What Claude and OpenCode may do with BIM-Bot Unreal. The single source is [`src/permissions.js`](src/permissions.js): edit it and run `--install` again to change the rules. The tests fail if a tool is missing from it.

| Level | Meaning |
|---|---|
| ✅ **Allow** | Runs without asking |
| ❓ **Ask** | You approve each call |
| ⛔ **Deny** | Blocked, the AI can't call it |

### ✅ Allowed: the AI can do these

| Group | Tools |
|---|---|
| Read the project | `ping_unreal`, `get_project_info`, `get_world_info`, `get_actors`, `find_actors_by_name`, `get_actor_properties`, `get_actor_bounds`, `get_selected_actors`, `search_assets`, `get_asset_info`, `get_blueprints`, `find_blueprint_nodes`, `get_remote_property`, `capture_viewport` |
| Editor UI | `select_actors_by_label`, `focus_viewport`, `show_notification` |
| Edit the level (undoable, in memory until saved) | `spawn_actor`, `spawn_blueprint_actor`, `duplicate_actor`, `rename_actor`, `set_actor_transform`, `set_actor_visibility`, `set_actor_property`, `set_actor_tag`, `set_static_mesh_material`, `undo` |
| Blueprints and UMG | `create_blueprint`, `compile_blueprint`, `add_component_to_blueprint`, `set_static_mesh_properties`, `set_component_property`, `set_physics_properties`, `set_blueprint_property`, `set_pawn_properties`, `add_blueprint_variable`, `add_blueprint_event_node`, `add_blueprint_input_action_node`, `add_blueprint_function_node`, `connect_blueprint_nodes`, `add_blueprint_get_self_component_reference`, `add_blueprint_self_reference`, `create_umg_widget_blueprint`, `add_text_block_to_widget`, `add_button_to_widget`, `bind_widget_event`, `add_widget_to_viewport`, `set_text_block_binding` |
| Play in editor | `play_in_editor`, `stop_play_in_editor` |

### ❓ Ask first: the AI needs your approval

| Tool | Why |
|---|---|
| `delete_actor` | Removes actors from the level |
| `save_level` | Writes the `.umap` to disk |
| `open_level` | Switches level, so unsaved changes can be lost |
| `import_asset` | Reads a file from your disk into `Content/` |
| `build_lighting` | Long-running, overwrites built lighting |
| `create_input_mapping` | Changes project-wide input settings |
| `call_remote_function` | Calls any UFunction on any object |
| `set_remote_property` | Sets any property on any object |

### ⛔ Denied: the AI can't do these

| Tool | Why |
|---|---|
| `execute_python` | Runs arbitrary Python with full access to your files, network and processes |
| `execute_console_command` | Runs arbitrary console commands (`quit`, `exec`, cvars) |

The other tools still use Python internally. Only the AI's direct, free-form calls are blocked. To allow one, move it to `ask` in `permissions.js`.

**Per client**
- **Claude Code**: rules go in `~/.claude/settings.json` as `mcp__BIM-Bot-Unreal__<tool>`
- **OpenCode**: rules go in `opencode.json` as `"permission": { "BIM-Bot-Unreal_<tool>": "allow" | "ask" | "deny" }`
- **Claude Desktop** has no config-file rules. It asks per tool in the app, so choose "Always allow" only for the ✅ tools above

## Quick Start

```bash
# Install dependencies
npm install

# Start the MCP server
npm start

# Run tests
npm test
```

## Configuration

| Environment Variable | Default | Description |
|---|---|---|
| `UNREAL_HOST` | `localhost` | Unreal Engine host |
| `UNREAL_PORT` | `30010` | Remote Control API port |
| `UNREAL_BRIDGE_PORT` | `30011` | MCP Bridge plugin port |
| `UNREAL_TIMEOUT` | `10000` | Request timeout (ms) |
| `LOG_LEVEL` | `info` | Logging level: `debug`, `info`, `warn`, `error` |

## Claude Desktop Setup

Copy `claude_desktop_config.json` to your Claude Desktop config directory, or merge the `mcpServers` block into your existing config:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "BIM-Bot-Unreal": {
      "command": "node",
      "args": ["C:/path/to/Unreal MCP/src/index.js"],
      "env": {
        "UNREAL_HOST": "localhost",
        "UNREAL_PORT": "30010"
      }
    }
  }
}
```

## Available Tools (58)

### Connection
| Tool | Description |
|---|---|
| `ping_unreal` | Check if Unreal Engine is reachable |

### Python Execution
| Tool | Description |
|---|---|
| `execute_python` | Execute a Python script inside Unreal Editor |

### Actor Management
| Tool | Description |
|---|---|
| `get_actors` | List all actors in the current level |
| `find_actors_by_name` | Find actors by name pattern (case-insensitive substring) |
| `spawn_actor` | Spawn an actor in the level by class path |
| `spawn_blueprint_actor` | Spawn a Blueprint actor by asset path |
| `delete_actor` | Delete an actor by label |
| `duplicate_actor` | Duplicate an actor with optional offset |
| `rename_actor` | Rename an actor's label |
| `set_actor_transform` | Set location, rotation, and/or scale |
| `set_actor_property` | Set any named property on an actor |
| `get_actor_properties` | Get properties of an actor |
| `get_actor_bounds` | Get bounding box origin and extent |
| `set_actor_visibility` | Show or hide an actor |
| `set_actor_tag` | Add or remove a tag on an actor |
| `get_selected_actors` | Get currently selected actors |
| `select_actors_by_label` | Select actors by their labels |
| `focus_viewport` | Focus the editor viewport on an actor or location |

### Level Management
| Tool | Description |
|---|---|
| `get_world_info` | Get current world/level info |
| `save_level` | Save the current level |
| `open_level` | Open a level by asset path |

### Asset Management
| Tool | Description |
|---|---|
| `search_assets` | Search the content browser |
| `get_asset_info` | Get detailed asset info |

### Blueprint Management
| Tool | Description |
|---|---|
| `get_blueprints` | List Blueprint assets |
| `create_blueprint` | Create a new Blueprint class asset |
| `compile_blueprint` | Compile a Blueprint |
| `add_component_to_blueprint` | Add a component to a Blueprint's component tree |
| `set_static_mesh_properties` | Assign mesh + material to a StaticMeshComponent in a Blueprint |
| `set_component_property` | Set any property on a named component inside a Blueprint |
| `set_physics_properties` | Configure physics on a PrimitiveComponent in a Blueprint |
| `set_blueprint_property` | Set a property on the Blueprint's Class Default Object |
| `set_pawn_properties` | Set Pawn-specific defaults (auto-possess, controller rotation, etc.) |

### Blueprint Node Graph
| Tool | Description |
|---|---|
| `add_blueprint_variable` | Add a member variable to a Blueprint |
| `add_blueprint_event_node` | Add an event node (BeginPlay, Tick, custom) to the event graph |
| `add_blueprint_input_action_node` | Add an InputAction event node |
| `add_blueprint_function_node` | Add a function call node to the event graph |
| `connect_blueprint_nodes` | Wire two nodes together by pin names |
| `add_blueprint_get_self_component_reference` | Add a component getter node |
| `add_blueprint_self_reference` | Add a "Get Self" reference node |
| `find_blueprint_nodes` | Search event graph for nodes by type or event name |

### UMG / Widget UI
| Tool | Description |
|---|---|
| `create_umg_widget_blueprint` | Create a new Widget Blueprint with a Canvas Panel root |
| `add_text_block_to_widget` | Add a TextBlock to a Widget Blueprint |
| `add_button_to_widget` | Add a Button (with label) to a Widget Blueprint |
| `bind_widget_event` | Bind a widget event (OnClicked, etc.) to a Blueprint function |
| `add_widget_to_viewport` | Resolve a Widget Blueprint class for viewport use |
| `set_text_block_binding` | Create a variable + getter for TextBlock data binding |

### Project / Input
| Tool | Description |
|---|---|
| `create_input_mapping` | Create an Action or Axis input mapping in Project Settings |

### Editor Control
| Tool | Description |
|---|---|
| `play_in_editor` | Start Play-in-Editor (PIE) |
| `stop_play_in_editor` | Stop PIE |
| `undo` | Undo the last operation |

### Materials
| Tool | Description |
|---|---|
| `set_static_mesh_material` | Set a material on a StaticMeshActor |

### Remote Control API (Low-level)
| Tool | Description |
|---|---|
| `call_remote_function` | Call any function on a UObject |
| `get_remote_property` | Get any UObject property |
| `set_remote_property` | Set any UObject property |

### Lighting
| Tool | Description |
|---|---|
| `build_lighting` | Build lighting in the current level |

### Editor Tools (UnrealMCPBridge if installed, otherwise Python)
| Tool | Description |
|---|---|
| `capture_viewport` | Screenshot the active viewport (Python: Unreal must run on the same PC) |
| `execute_console_command` | Execute a console command |
| `show_notification` | Editor toast with the bridge; Output Log line without it |
| `get_project_info` | Get engine version, project metadata |
| `import_asset` | Import a file into the content browser |

### How Python results come back

Tools run Python through `PythonScriptLibrary.ExecutePythonCommandEx`, which returns what the script `print`s. A script that raises returns an error with the traceback. (`ExecutePythonScript` only returns `true`/`false`, so no data came back before.)

## Error Handling & Reliability

- **Automatic retries** with exponential backoff (3 attempts, 500ms → 1s → 2s)
- **Connection health monitoring** with periodic heartbeat checks
- **Structured JSON logging** to stderr (stdout reserved for MCP transport)
- **Graceful shutdown** on SIGINT/SIGTERM
- **Configurable timeouts** per-request and globally

## Project Structure

```
Unreal MCP/   (repo folder)
├── src/
│   ├── index.js          # MCP server + tool definitions (58 tools)
│   ├── unreal-client.js  # HTTP client with retries, health checks, Python fallbacks
│   ├── launcher.js       # exe entry point: install, project setup, serve
│   ├── unreal-setup.js   # Unreal project setup (.uproject plugins + Remote Control ini)
│   ├── permissions.js    # AI tool permissions
│   └── logger.js         # Structured JSON logger
├── test/
│   ├── server.test.js    # Test suite (67 tests)
│   └── mock-unreal.js    # Mock Unreal Engine server
├── package.json
├── claude_desktop_config.json
└── README.md
```

## License

ISC
