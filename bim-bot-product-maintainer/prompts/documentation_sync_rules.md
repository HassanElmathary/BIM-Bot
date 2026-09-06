# Documentation Synchronization Rules

## Zero-Mismatch Policy
All documentation and branding numbers across BIM-Bot MUST remain 100% synchronized at all times.

### 1. Tool Count Synchronization
Whenever tool count changes (e.g. from 186 to 187):
- Update `README.md` header badge and overview text ("186 tools").
- Update `README.md` Tool Reference Table.
- Update `version.json` changelog summary ("Includes 186 MCP tools...").
- Update `website.md` Hero Stats (`186 MCP Tools | 19 Categories | 8 Revit Versions`).
- Update `website.md` Tools Section Header (`186 tools across 19 categories`).
- Update GitHub Release notes summary.

---

### 2. Category Count & Structure
- The official category count is **19**.
- Categories: Reading, Creating, Editing, Documentation, QA/QC, AI, Power Tools, Advanced, Drafting, Export, Extended, File Management, MEP, Power BI, Rendering, Settings, Sketch, Transactions, BIM Dashboard.
- When creating a new category, increment category count (19 → 20) across `README.md`, `website.md`, and release notes.

---

### 3. Version Badges & Metadata
- **npm Version Badge**: Matches `version` field in `revit-mcp-server/package.json`.
- **GitHub Release Badge**: Matches current tag (e.g. `v2.2.0`).
- **Installer Version**: Matches `#define MyAppVersion` in Inno Setup script and setup filename `BIMBot-Setup-X.Y.Z.exe`.
- **Revit Matrix**: Must strictly specify supported Revit range: `Revit 2020–2027` (8 Revit versions).

---

### 4. Verification Protocol
Before committing documentation changes:
1. Search codebase for previous tool count string (e.g. `186 tools`).
2. Search codebase for previous version string (e.g. `2.2.0`).
3. Confirm zero remaining outdated numbers.
