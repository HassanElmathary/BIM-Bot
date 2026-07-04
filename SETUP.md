# BIM-Bot — Setup & GitHub Guide

## Quick Setup

### 1. Set API Key (Optional)
```bash
cd revit-mcp-server
copy .env.example .env
# Edit .env and add your key if you wish to use server-side AI features
```

### 2. Run Local Test
```bash
cd revit-mcp-server
npm run build
node build/tests/test-startup.js
```

### 3. Install Plugin to Revit
Download **`BIMBot-Setup-2.1.0.exe`** from [GitHub Releases](https://github.com/HassanElmathary/MIB-Mot/releases) and run as Administrator.

Or build from source: compile `installer/setup.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php).

### 4. Connect an AI Client

Claude setup is automatic, in three layers:

1. **Installer** — registers BIM-Bot in Claude Desktop and Claude Code (real JSON merge via `configure-claude.cjs`, repairs stale entries from previous installs).
2. **Revit plugin self-heal** — every time Revit starts, the plugin silently validates the Claude configs and repairs any entry whose paths no longer exist (e.g. the install folder moved). A `.bimbot-backup` copy is written before any change.
3. **"Connect Claude" ribbon button** — runs the same validate/repair on demand and reports per-client status.

You can also run the repair manually at any time:
```bash
node "C:\Program Files\BIMBot\server\scripts\configure-claude.cjs"
# or from the repo (dev):
node revit-mcp-server/scripts/configure-claude.cjs
```

After any config change, fully restart Claude (File → Exit, then reopen). For other clients, use the configurations below.

#### Claude Desktop
**Config file**: `%APPDATA%\Claude\claude_desktop_config.json`
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

#### Cursor IDE
**Config file**: `~/.cursor/mcp.json` or **Settings → Features → MCP → Add New MCP Server**
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

#### Windsurf
**Config file**: `%USERPROFILE%\.codeium\windsurf\mcp_config.json`
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

#### VS Code (Copilot / MCP Extension)
**Config file**: `.vscode/mcp.json` (workspace) or Command Palette → "MCP: Open User Configuration"
```json
{
  "servers": {
    "BIM-Bot": {
      "type": "stdio",
      "command": "C:\\Program Files\\BIMBot\\nodejs\\node.exe",
      "args": ["C:\\Program Files\\BIMBot\\server\\build\\index.js"]
    }
  }
}
```

#### Gemini CLI
**Config file**: `~/.gemini/settings.json`
```json
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "C:\\Program Files\\BIMBot\\nodejs\\node.exe",
      "args": ["C:\\Program Files\\BIMBot\\server\\build\\index.js"]
    }
  }
}
```

#### Developer (from source)
```json
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "node",
      "args": ["<repo-path>/revit-mcp-server/build/index.js"],
      "env": {}
    }
  }
}
```

---

## GitHub Repository Setup

Run these commands to create and push the repo:

```bash
# 1. Login to GitHub (one time)
gh auth login --web --git-protocol https

# 2. Push to existing repo
cd "C:\Users\hassa\OneDrive\01-me\Revit MCP"
git push origin main

# 3. Create a new release
gh release create v2.1.0 "installer/output/BIMBot-Setup-2.1.0.exe" --title "BIM-Bot v2.1.0" --notes "179 MCP tools, Revit 2024–2026 support, built-in Gemini AI, one-click installer"
```

### After GitHub is set up:
Update `UpdateChecker.cs` with your GitHub username:
```csharp
private const string GITHUB_OWNER = "HassanElmathary";
private const string GITHUB_REPO = "MIB-Mot";
```

---

## Test Commands

```bash
# Smoke test (no Revit needed)
node build/tests/test-startup.js

# Socket integration test (Revit must be open with MCP started)
node build/tests/test-socket.js
```
