# Website Update Brief — elmthary.space
**Date:** April 11, 2026  
**From:** Hassan Elmathary  
**To:** Web Developer  
**Scope:** Update the BIM-Bot website with new sections, installation methods, content fixes, and improvements

---

## Changes Required

### 1. 📊 UPDATE: Hero Stats

**Current:**
`179 MCP Tools | 17 Categories | 7 Revit Versions`

**Change to:**
`179 MCP Tools | 18 Categories | 7 Revit Versions`

The correct category count is 18: Reading, Creating, Editing, Documentation, QA/QC, AI, Power Tools, Advanced, Drafting, Export, Extended, File Management, MEP, Power BI, Rendering, Settings, Sketch, Transactions.

---

### 2. 📋 UPDATE: Tools Section Header

**Current:**
`179 tools across 17 categories`

**Change to:**
`179 tools across 18 categories`

---

### 3. 🔗 UPDATE: Navigation Bar

**Current links:** Features | Tools | Architecture | Install | Compatibility

**Change to:** Features | Tools | Architecture | Install | Community

- **Add** "Community" linking to the new community section
- **Remove** "Compatibility" from nav (keep the section on the page, just remove from top nav)

---

### 4. 📥 UPDATE: Install Section — .exe Installer

**Replace the entire Install section** with a single download button for the `.exe` installer.

#### Download BIM-Bot Installer

Large CTA button:
> **Download BIM-Bot v2.1.0** — `BIMBot-Setup-2.1.0.exe` (Windows)

Link to: `https://github.com/HassanElmathary/Revit-MCP/releases/latest`

**What it does (show as animated checklist):**
- ✅ Installs BIM-Bot with a guided setup wizard
- ✅ Auto-detects your Revit versions (2020–2027)
- ✅ Deploys the correct plugin for each version
- ✅ Auto-configures Claude Desktop
- ✅ Bundles Node.js — no dependencies needed
- ✅ Ready to use in 60 seconds

**System requirements (small text below button):**
- Windows 10/11
- Autodesk Revit 2020–2027
- Claude Desktop (for AI features)

---

### 5. 🔧 NEW SECTION: Claude Desktop Configuration

Add a section (after Install) showing manual Claude config:

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

With note: *"The one-line installer configures this automatically. Use this only if you need to set it up manually."*

---

### 6. 🗑️ NEW SECTION: Uninstall

Add an expandable/accordion section with:

**One-Line Uninstall:**
```powershell
irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/install.ps1 | iex; Install-BIMBot -Uninstall
```

**What it removes:**
- 🧹 Revit plugin (`.addin` files + DLLs) from all Revit versions
- 🧹 `BIM-Bot` entry from Claude Desktop config (+ legacy `revit-mcp` / `bim-bot` keys)
- 🧹 Installation directory (`C:\Program Files\BIMBot` or `%LOCALAPPDATA%\BIMBot`)

**Manual uninstall steps** (collapsible):
1. Delete `%ProgramData%\Autodesk\Revit\Addins\20XX\BIMBot.addin` for each Revit version
2. Remove `"BIM-Bot"` block from `%APPDATA%\Claude\claude_desktop_config.json`
3. Delete `C:\Program Files\BIMBot` folder
4. Restart Revit and Claude Desktop

---

### 7. 🌍 NEW SECTION: "Community" (Before Footer)

Add a community section with:
- **Email/Contact** link: `hassan.elmathary@gmail.com`

Design as a banner CTA:
> "Join the Community — Connect with BIM professionals using AI to automate Revit"

Icon button (Email) below the text.

---

### 8. 🏷️ NEW: Social Proof Badges (Under Hero)

Add a small row under the hero stats showing:
- npm downloads badge: `https://img.shields.io/npm/dm/revit-mcp-server`
- MIT License badge
- "Works with" logos: Claude, Gemini, Cursor, VS Code

This builds trust and shows the tool is actively used.

---

### 9. 📝 UPDATE: Footer

**Current:**
`© 2024 BIM-Bot. All rights reserved.`

**Change to:**
`© 2025 BIM-Bot — Built by Hassan Elmathary`

**Add footer links:**
- Email/Contact: `hassan.elmathary@gmail.com`

---

### 10. 📱 NEW: Multi-Language Support (Low Priority)

Add a language toggle (EN | 中文) in the navbar. For now, just add the toggle UI — the Chinese translation will be provided later.

---

## File Checklist

| File | Changes |
|---|---|
| `index.html` | Update stats (17→18), replace install section with .exe download CTA, add uninstall section, add community section, update footer, update nav links, add social proof badges |
| `style.css` | Add styles for download button, community section, social proof badges |
| `script.js` | No major changes needed |

---

## Design Notes

- **Keep the existing dark theme and glassmorphism aesthetic** — don't change the look and feel
- **Install section** should have one prominent download button (large, glowing CTA) for the `.exe` installer
- **Community section** should be warm and inviting
- **All new sections** should use the same animation patterns (fade-in on scroll) as existing sections
- **Mobile responsive** — all new sections must work on mobile
- **Config key is `"BIM-Bot"`** (capital B, I, M, hyphen, capital B) — use this consistently everywhere

---

## Priority Order

1. 🔴 **Fix stats** (17 → 18 categories)
2. 🔴 **Install section** (.exe download button)
3. 🔴 **Add Uninstall section**
4. 🟡 **Community section**
5. 🟡 **Footer update** (year + attribution)
6. 🟢 **Social proof badges**
7. 🟢 **Language toggle placeholder**

---

## Questions to Answer Before Starting

- [x] What's the Discord server link? *(Not needed)*
- [x] What's the new GitHub repo URL? *(Not adding at all)*
- [x] Want to add a contact email to the footer? *(Yes: hassan.elmathary@gmail.com)*
- [x] What's the ZIP filename for download? *(BIMBot-v2.1.0.zip)*
- [x] What's the Claude config key? *(`"BIM-Bot"` — capital casing)*
- [x] What install methods to document? *(PowerShell one-liner, NPX, ZIP manual)*
