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
Double-click the installer: `installer\output\BIMBot-Setup-1.0.0.exe`

### 4. Connect an AI Client
Add to your MCP client config:
```json
{
  "mcpServers": {
    "bim-bot": {
      "command": "node",
      "args": ["C:/path/to/BIM-Bot/revit-mcp-server/build/index.js"]
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

# 2. Create the repository
cd "d:\OneDrive\01-me\Revit MCP"
gh repo create revit-mcp --public --source . --push --description "AI-Powered Revit MCP Server + Plugin"

# 3. Create first release (enables auto-update)
gh release create v1.0.0 "installer/output/BIMBot-Setup-1.0.0.exe" --title "BIM-Bot v1.0.0" --notes "Initial release with 63 MCP tools and Gemini AI integration"
```

### After GitHub is set up:
Update `UpdateChecker.cs` with your GitHub username:
```csharp
private const string GITHUB_OWNER = "YOUR_GITHUB_USERNAME";
private const string GITHUB_REPO = "revit-mcp";
```

---

## Test Commands

```bash
# Smoke test (no Revit needed)
node build/tests/test-startup.js

# Socket integration test (Revit must be open with MCP started)
node build/tests/test-socket.js
```
