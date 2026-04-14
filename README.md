# BIM-Bot — AI-Powered Revit Automation

[![npm version](https://img.shields.io/npm/v/revit-mcp-server.svg)](https://www.npmjs.com/package/revit-mcp-server)
[![GitHub release](https://img.shields.io/github/v/release/HassanElmathary/Revit-MCP)](https://github.com/HassanElmathary/Revit-MCP/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Revit](https://img.shields.io/badge/Revit-2020--2027-blue.svg)](#)

> BIM-Bot: Model Context Protocol (MCP) server + Revit plugin for AI-driven BIM automation with **179 tools**, built-in Gemini AI chat, and Power BI 3D export.

---

## ⚡ Quick Install

### One-Line Install (Recommended)

Open PowerShell and paste:

```powershell
irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/install.ps1 | iex
```

**That's it!** The installer will:
- ✅ Download & install BIM-Bot
- ✅ Auto-detect your Revit versions (2020–2027)
- ✅ Deploy the correct plugin for each version
- ✅ Auto-configure Claude Desktop
- ✅ Ready to use in 60 seconds

### Alternative Install Methods

<details>
<summary>📦 <strong>NPX — Run MCP Server Directly</strong> (if you have Node.js 18+)</summary>

```powershell
npx -y revit-mcp-server
```

Or install globally:
```powershell
npm install -g revit-mcp-server
```

Then add to your Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "npx",
      "args": ["-y", "revit-mcp-server"]
    }
  }
}
```

> **Note:** This only installs the MCP server. You still need the Revit plugin — download from [GitHub Releases](https://github.com/HassanElmathary/Revit-MCP/releases).

</details>

<details>
<summary>📥 <strong>Manual Download</strong> (offline install)</summary>

1. Download `BIMBot-v2.1.0.zip` from [GitHub Releases](https://github.com/HassanElmathary/Revit-MCP/releases)
2. Extract the ZIP to `C:\Program Files\BIMBot` (or any folder)
3. Run `Install.bat` as Administrator
4. Open Revit — look for **"BIM-Bot"** in the Add-ins tab

</details>

<details>
<summary>🗑️ <strong>Uninstall</strong></summary>

See the [Uninstall](#-uninstall) section below.

</details>

---

## Claude Desktop Configuration

The installer auto-configures Claude Desktop. If you need to do it manually, add this to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "C:\\Program Files\\BIMBot\\nodejs\\node.exe",
      "args": ["C:\\Program Files\\BIMBot\\server\\build\\index.js"],
      "env": {}
    }
  }
}
```

> **Restart Claude Desktop** after editing the config to load the new MCP server.

---

## 🗑️ Uninstall

### One-Line Uninstall (Recommended)

Open PowerShell and paste:

```powershell
irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/install.ps1 | iex; Install-BIMBot -Uninstall
```

The uninstaller will:
- 🧹 Remove the Revit plugin (`.addin` files + DLLs) from **all** Revit versions (2020–2027)
- 🧹 Remove the `BIM-Bot` entry from Claude Desktop config (+ legacy `revit-mcp` / `bim-bot` keys)
- 🧹 Delete the installation directory (`C:\Program Files\BIMBot` or `%LOCALAPPDATA%\BIMBot`)
- 🔄 Prompt to restart Revit and Claude Desktop

> **Note:** Restart Revit after uninstalling to fully unload the plugin.

### Manual Uninstall

If you prefer to remove everything by hand:

<details>
<summary>📋 <strong>Click to expand manual steps</strong></summary>

#### 1. Remove the Revit plugin

Delete the following files/folders for each Revit version you have (replace `20XX` with your version):

```
%ProgramData%\Autodesk\Revit\Addins\20XX\BIMBot.addin
%ProgramData%\Autodesk\Revit\Addins\20XX\BIMBot\        (entire folder)
```

Also check the user-level addins folder:
```
%APPDATA%\Autodesk\Revit\Addins\20XX\BIMBot.addin
%APPDATA%\Autodesk\Revit\Addins\20XX\BIMBot\
```

#### 2. Remove Claude Desktop config entry

Open `%APPDATA%\Claude\claude_desktop_config.json` in a text editor and delete the `"BIM-Bot"` block from `"mcpServers"`:

```diff
  {
    "mcpServers": {
-     "BIM-Bot": {
-       "command": "C:\\Program Files\\BIMBot\\nodejs\\node.exe",
-       "args": ["C:\\Program Files\\BIMBot\\server\\build\\index.js"],
-       "env": {}
-     }
    }
  }
```

#### 3. Delete the installation folder

```powershell
# If installed as Admin:
Remove-Item "C:\Program Files\BIMBot" -Recurse -Force

# If installed as regular user:
Remove-Item "$env:LOCALAPPDATA\BIMBot" -Recurse -Force
```

#### 4. Restart applications

- **Restart Revit** to fully unload the plugin
- **Restart Claude Desktop** to reload the MCP config

</details>

### NPX / npm Global Uninstall

If you installed the MCP server via npm:

```powershell
npm uninstall -g revit-mcp-server
```

> This only removes the MCP server. The Revit plugin must be removed separately using the steps above.

---

## Features

- **179 MCP Tools** across 18 categories:
  - 🔍 **Reading** (19): Views, elements, parameters, rooms, levels, sheets, families, schedules, linked models
  - 🏗️ **Creating** (15): Walls, floors, ceilings, roofs, levels, grids, rooms, views, sheets, tags
  - ✏️ **Editing** (12): Modify, move, rotate, copy, delete, mirror, align, group, batch modify
  - 📄 **Documentation** (8): Sheets, viewports, exports, legends, revisions, tags
  - ✅ **QA/QC** (8): Warnings, audits, compliance, naming, duplicates, purge, validation
  - 🤖 **AI** (8): Gemini chat, code generation, model analysis, Google OAuth
  - ⚡ **Power Tools** (29): Batch operations, bulk element processing, advanced queries
  - 🔧 **Advanced** (5): Complex model operations and automation
  - 📐 **Drafting** (3): Detail lines, detail components, filled regions
  - 📦 **Export** (16): DWG, PDF, IFC, NWC, images, schedules to CSV/Excel
  - 🔌 **Extended** (16): Additional element manipulation and property management
  - 📁 **File Management** (10): Worksharing, links, worksets, file operations
  - 🔥 **MEP** (8): Ducts, pipes, cable trays, fittings, systems
  - 📊 **Power BI** (1): 3D geometry export for Power BI visualization
  - 🎨 **Rendering** (3): Materials, render settings, visual styles
  - ⚙️ **Settings** (9): Project info, units, shared parameters, global settings
  - ✍️ **Sketch** (3): Model lines, reference planes, sketch-based geometry
  - 🔄 **Transactions** (6): Undo/redo, transaction groups, sub-transactions

- **Antigravity AI Chat** — Built-in Gemini-powered natural language interface inside Revit
- **Google Gemini 2.5** integration for natural language BIM interaction
- **Power BI 3D Visual** — Custom Three.js visual for Revit geometry in Power BI dashboards
- **Integrations** — Google Sheets, Excel, Notion, SQLite connectors
- **Local AI Support** — Ollama integration for offline AI capabilities
- **Revit 2020–2027** support (multi-target plugin)
- **Auto-updater** via GitHub Releases
- **One-click installer** (.exe) with portable Node.js

## Supported Revit Versions

| Revit Version | Framework | Status |
|---------------|-----------|--------|
| Revit 2020 | .NET Framework 4.8 | ✅ Supported |
| Revit 2021 | .NET Framework 4.8 | ✅ Supported |
| Revit 2022 | .NET Framework 4.8 | ✅ Supported |
| Revit 2023 | .NET Framework 4.8 | ✅ Supported |
| Revit 2024 | .NET Framework 4.8 | ✅ Supported |
| Revit 2025 | .NET 8.0 | ✅ Supported |
| Revit 2026 | .NET 8.0 | ✅ Supported |
| Revit 2027 | .NET 10.0 | ✅ Supported |

## Architecture

```
┌─────────────────┐     MCP/stdio      ┌─────────────────┐     TCP/JSON-RPC     ┌─────────────────┐
│   AI Client     │◄──────────────────► │   MCP Server    │◄────────────────────►│  Revit Plugin   │
│ (Gemini/Claude) │                     │  (Node.js/TS)   │     port 8080        │   (C# Add-in)   │
└─────────────────┘                     └─────────────────┘                      └─────────────────┘
                                              │                                        │
                                              ▼                                        ▼
                                        ┌───────────┐                           ┌──────────────┐
                                        │Integrations│                           │ Antigravity  │
                                        │Sheets/Excel│                           │  AI Chat UI  │
                                        │Notion/SQLite│                          │ (Gemini CLI) │
                                        └───────────┘                           └──────────────┘
```

## Developer Setup

### 1. Install Dependencies
```bash
cd revit-mcp-server
npm install
npm run build
```

### 2. Configure API Key (Optional — for AI features)
```bash
cp .env.example .env
# Edit .env with your Gemini API key
```

### 3. Configure MCP Client

Add to your MCP client config (e.g. Claude Desktop, Cursor, Windsurf, etc.):
```json
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "node",
      "args": ["<path-to>/revit-mcp-server/build/index.js"],
      "env": {}
    }
  }
}
```

### 4. Start in Revit
1. Load the Revit plugin (via installer or manual .addin)
2. Click **"Start MCP Service"** in the Revit ribbon
3. The AI client can now interact with your Revit model

## Project Structure

```
BIM-Bot/
├── revit-mcp-server/              # MCP Server (TypeScript/Node.js)
│   └── src/
│       ├── index.ts               # Server entry point
│       ├── ai/
│       │   ├── gemini-service.ts
│       │   └── ollama-service.ts   # Local AI via Ollama
│       ├── auth/
│       │   └── google-oauth.ts
│       ├── integrations/           # External service connectors
│       │   ├── sheets-client.ts
│       │   ├── excel-client.ts
│       │   ├── notion-client.ts
│       │   └── sqlite-client.ts
│       ├── tools/                  # 179 MCP tools (18 files)
│       │   ├── reading_tools.ts
│       │   ├── creating_tools.ts
│       │   ├── editing_tools.ts
│       │   ├── documentation_tools.ts
│       │   ├── qaqc_tools.ts
│       │   ├── ai_tools.ts
│       │   ├── advanced_tools.ts
│       │   ├── power_tools.ts
│       │   ├── drafting_tools.ts
│       │   ├── export_tools.ts
│       │   ├── extended_tools.ts
│       │   ├── file_management_tools.ts
│       │   ├── mep_tools.ts
│       │   ├── powerbi_tools.ts
│       │   ├── rendering_tools.ts
│       │   ├── settings_tools.ts
│       │   ├── sketch_tools.ts
│       │   └── transaction_tools.ts
│       └── utils/
│           ├── SocketClient.ts
│           └── ConnectionManager.ts
│
├── revit-mcp-plugin/              # Revit Plugin (C# Add-in)
│   └── BIMBotPlugin/
│       ├── Core/                  # Application core
│       │   ├── Application.cs
│       │   ├── CommandExecutor.cs
│       │   ├── SocketService.cs
│       │   ├── CodeExecutor.cs
│       │   ├── ToolRegistry.cs
│       │   ├── ExcelService.cs
│       │   ├── ProjectDataService.cs
│       │   └── ProjectFilesService.cs
│       ├── AI/                    # AI integration
│       │   ├── GeminiClient.cs
│       │   ├── ChatOrchestrator.cs
│       │   ├── GeminiCliClient.cs
│       │   └── GeminiCliOrchestrator.cs
│       ├── Antigravity/           # Built-in AI chat UI
│       │   ├── AntigravityWindow.cs
│       │   ├── AntigravityBridge.cs
│       │   └── AntigravityCommand.cs
│       ├── PowerBI/               # Power BI 3D export
│       │   ├── PowerBIExportContext.cs
│       │   └── PowerBISqliteWriter.cs
│       ├── UI/                    # WPF user interface
│       │   ├── ChatWindow.cs
│       │   ├── SettingsWindow.cs
│       │   ├── Tools/             # Tool launcher windows
│       │   └── Themes/            # Dark theme & icons
│       └── Commands/
│
├── revit-mcp-powerbi-visual/      # Power BI Custom Visual
│   └── src/
│       ├── visual.ts              # Three.js 3D renderer
│       ├── meshParser.ts          # Revit geometry parser
│       └── settings.ts
│
├── installer/                     # Inno Setup Installer
│   └── setup.iss
│
└── scripts/                       # Utility scripts
    ├── antigravity-watcher.ps1
    └── revit-cmd.ps1
```

## Tool Reference

| Category | Count | Key Tools |
|----------|-------|-----------|
| Reading | 19 | `get_elements`, `get_current_view_info`, `get_parameters`, `get_rooms`, `get_linked_models` |
| Creating | 15 | `create_wall`, `create_floor`, `create_roof`, `create_room`, `create_sheet` |
| Editing | 12 | `modify_parameter`, `move_element`, `rotate_element`, `copy_element`, `batch_modify` |
| Documentation | 8 | `create_sheet`, `place_viewport`, `export_dwg`, `add_revision` |
| QA/QC | 8 | `audit_model`, `check_naming`, `find_duplicates`, `purge_unused` |
| AI | 8 | `ai_chat`, `ai_generate_code`, `ai_analyze_model`, `google_oauth` |
| Power Tools | 29 | Batch operations, bulk processing, advanced element queries |
| Export | 16 | DWG, PDF, IFC, NWC, images, schedule CSV/Excel |
| Extended | 16 | Additional element manipulation and property management |
| File Mgmt | 10 | Worksharing, links, worksets, file operations |
| MEP | 8 | Ducts, pipes, cable trays, fittings, systems |
| Settings | 9 | Project info, units, shared parameters |
| Transactions | 6 | Undo/redo, transaction groups |
| Advanced | 5 | Complex automation |
| Drafting | 3 | Detail lines, components, filled regions |
| Rendering | 3 | Materials, visual styles |
| Sketch | 3 | Model lines, reference planes |
| Power BI | 1 | 3D geometry export |

## Building the Installer

1. Compile the C# plugin in Visual Studio
2. Download [portable Node.js](https://nodejs.org/en/download/) to `installer/nodejs/`
3. Install [Inno Setup](https://jrsoftware.org/isinfo.php)
4. Compile `installer/setup.iss`

## License

MIT
