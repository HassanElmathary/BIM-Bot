# revit-mcp-server

[![npm version](https://img.shields.io/npm/v/revit-mcp-server.svg)](https://www.npmjs.com/package/revit-mcp-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **MCP Server for Autodesk Revit** — 179 AI-powered tools for BIM automation.  
> Works with Claude Desktop, Cursor, Windsurf, and any MCP client. Supports **Revit 2020–2026**.

## ⚡ Quick Start

### Run with npx (no install needed)

```bash
npx -y revit-mcp-server
```

### Or install globally

```bash
npm install -g revit-mcp-server
```

## Claude Desktop Configuration

Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "revit-mcp": {
      "command": "npx",
      "args": ["-y", "revit-mcp-server"]
    }
  }
}
```

Restart Claude Desktop after editing the config.

## Full Installation (Plugin + Server)

For the complete experience (MCP server + Revit plugin), use the one-liner installer:

```powershell
irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/install.ps1 | iex
```

This will:
- ✅ Download & install everything
- ✅ Auto-detect Revit 2020–2026
- ✅ Deploy the correct plugin for each version
- ✅ Auto-configure Claude Desktop

## 179 MCP Tools

| Category | Count | Key Tools |
|----------|-------|-----------|
| 🔍 Reading | 19 | `get_elements`, `get_parameters`, `get_rooms`, `get_views` |
| 🏗️ Creating | 15 | `create_wall`, `create_floor`, `create_roof`, `create_room` |
| ✏️ Editing | 12 | `modify_parameter`, `move_element`, `rotate_element`, `batch_modify` |
| 📄 Documentation | 8 | `create_sheet`, `place_viewport`, `export_dwg` |
| ✅ QA/QC | 8 | `audit_model`, `check_naming`, `find_duplicates`, `purge_unused` |
| 🤖 AI | 8 | `ai_chat`, `ai_generate_code`, `ai_analyze_model` |
| ⚡ Power Tools | 29 | Batch operations, bulk processing, advanced queries |
| 📦 Export | 16 | DWG, PDF, IFC, NWC, images, CSV/Excel |
| 🔌 Extended | 16 | Additional element manipulation |
| 📁 File Management | 10 | Worksharing, links, worksets |
| 🔥 MEP | 8 | Ducts, pipes, cable trays, fittings |
| ⚙️ Settings | 9 | Project info, units, shared parameters |
| 🔄 Transactions | 6 | Undo/redo, transaction groups |
| 🔧 Advanced | 5 | Complex automation, code execution |
| 📐 Drafting | 3 | Detail lines, components, filled regions |
| 🎨 Rendering | 3 | Materials, visual styles |
| ✍️ Sketch | 3 | Model lines, reference planes |
| 📊 Power BI | 1 | 3D geometry export |

## Architecture

```
AI Client (Claude/Cursor) ←→ MCP Server (this package) ←→ Revit Plugin (TCP:8080)
```

The MCP server communicates with the Revit plugin over TCP on port 8080. The plugin must be running inside Revit — click **"Start MCP Service"** in the Revit ribbon.

## Supported Revit Versions

| Version | Framework |
|---------|-----------|
| 2020–2024 | .NET Framework 4.8 |
| 2025–2026 | .NET 8.0 |

## Uninstall

```powershell
irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/uninstall.ps1 | iex
```

## Links

- 📖 [Documentation & Source](https://github.com/HassanElmathary/Revit-MCP)
- 🐛 [Report Issues](https://github.com/HassanElmathary/Revit-MCP/issues)
- 👤 [Hassan Ahmed Elmathary](https://www.linkedin.com/in/hassan-elmathary/)

## License

MIT
