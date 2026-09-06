# BIM-Bot Feature Lifecycle Rules

## Core Principles
Maintaining BIM-Bot as a production product requires synchronized execution across all 8 architectural tiers:
1. **MCP Server** (`revit-mcp-server/src/tools/`) — Node.js / TypeScript JSON-RPC 2.0 interface.
2. **Revit Plugin** (`revit-mcp-plugin/`) — C# / Revit API handler executing on `localhost:8080`.
3. **AI Layer** — System prompts, tool schemas, and agent context definitions (Claude Desktop, Cursor, Gemini).
4. **Installer** (`installer/installer.iss`) — Inno Setup `.exe` builder with embedded Node.js and auto-addin placement.
5. **README & Documentation** (`README.md`, `docs/`) — Tool reference table across 19 categories.
6. **GitHub Releases** (`version.json`, Git Tags) — Tagged releases with attached `.exe` setup binary.
7. **npm Package** (`revit-mcp-server`) — NPM registry distribution for NPX and MCP server runners.
8. **Website** (`website.md`, landing pages) — Product marketing and feature list at `elmthary.space`.

---

## 1. Feature Addition Rules
- **No Duplicate Tool Names**: Every tool must have a unique identifier.
- **Strict Category Mapping**: Every tool must belong to exactly **1 of 19 categories**:
  - Reading, Creating, Editing, Documentation, QA/QC, AI, Power Tools, Advanced, Drafting, Export, Extended, File Management, MEP, Power BI, Rendering, Settings, Sketch, Transactions, BIM Dashboard.
- **Tool Counter Increment**: When adding a tool, update the global tool count (e.g. `186 tools` → `187 tools`) everywhere in docs, code, and website.
- **Dual Layer Implementation**: Every tool MUST have both a TypeScript MCP wrapper and a C# Revit API command handler.

---

## 2. Feature Editing Rules
- **Schema Preservation**: Avoid breaking parameter changes unless performing a major version bump.
- **Multi-Version Compatibility**: Ensure modified C# handlers support Revit 2020 through 2027 without breaking API calls.
- **AI Description Clarity**: When updating tool parameters, update the tool description provided to the AI agent so LLMs use the new parameters accurately.

---

## 3. Feature Removal Rules
- **Deprecation First**: Prefer deprecating a tool with warning output before total removal.
- **Clean References**: Upon removal, remove the tool from MCP server, Revit plugin router, README reference table, website listing, and decrement total tool count.

---

## 4. Branding & Icon Rules
- **Ribbon Icons**: Revit ribbon icons must be provided in 16x16 and 32x32 PNG formats.
- **Installer Icon**: `app.ico` must be bundled into Inno Setup script.
- **Client Configuration Key**: The official configuration key for MCP clients MUST always be `"BIM-Bot"` (capital B, I, M, hyphen, capital B).
