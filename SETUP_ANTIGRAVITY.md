# Antigravity + BIM-Bot — Setup Guide

> **Control Revit from your IDE chat. No API keys. No quota. Unlimited.**

## Prerequisites

- [VS Code](https://code.visualstudio.com/) with the **Gemini Code Assist** (Antigravity) extension
- Revit 2020–2027 with the MCP plugin installed (see [SETUP.md](SETUP.md))
- PowerShell 7+ (`pwsh`) — included with Windows 11 or install from [aka.ms/powershell](https://aka.ms/powershell)

---

## 1. Install Antigravity in VS Code

1. Open VS Code → Extensions (`Ctrl+Shift+X`)
2. Search **"Gemini Code Assist"** and install it
3. Sign in with your Google account when prompted

## 2. Open the Project

Open the **BIM-Bot** folder in VS Code:

```
File → Open Folder → select "BIM-Bot"
```

Antigravity will automatically discover the `/revit` workflow from `.agents/workflows/revit.md`.

## 3. Start Revit

1. Open your Revit project
2. In the Revit ribbon, click **"Start MCP Service"**
3. You should see a confirmation that the TCP server is listening on port 8080

## 4. Start Chatting

In the Antigravity chat panel, type:

```
/revit show me all levels
```

Antigravity will:
1. Read the workflow instructions
2. Run the PowerShell script to send `get_levels` to Revit
3. Display the results in a formatted table

### More Examples

| You say | What happens |
|---------|-------------|
| `/revit list all walls` | Queries all wall elements |
| `/revit create a 20ft wall on Level 1` | Creates a wall via `create_wall` |
| `/revit audit the model` | Runs `audit_model` for quality check |
| `/revit export sheets to PDF` | Triggers `print_sheets` |
| `/revit find duplicate rooms` | Runs `find_duplicates` |
| `/revit how many elements in the model?` | Calls `get_model_statistics` |

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| "Connection timed out" | Make sure Revit is open and **MCP Service is started** |
| `pwsh` not found | Install PowerShell 7: [aka.ms/powershell](https://aka.ms/powershell) |
| Workflow not found | Ensure you opened the root `BIM-Bot` folder in VS Code |

---

## Architecture

```
You (IDE chat)
    ↓  natural language
Antigravity reads /revit workflow
    ↓  picks the right tool
pwsh scripts/revit-cmd.ps1
    ↓  TCP JSON-RPC to localhost:8080
BIM-Bot Plugin
    ↓  executes in Revit API
Results returned to chat
```
