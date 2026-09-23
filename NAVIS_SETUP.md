# BIM-Bot Navisworks — Setup (ADDITIVE, Revit untouched)

Revit connection is unchanged (port 8080 + `service.json`).
Navisworks Manage runs in parallel (port **8091** + `service-navis.json`).

## 1. Install Navisworks plugin

1. Build on a machine with Navisworks Manage installed:
   `dotnet build navis-mcp-plugin/BIMBotNavisPlugin -c Release`
   (Single `net48` build loads in Manage 2020–2025+. If `Autodesk.Navisworks.Api.dll`
   is not in `C:\Program Files\Autodesk\Navisworks Manage 20xx`, pass
   `-p:NavisworksSDK="C:\path\to\api\folder"`.)
2. Copy output to `C:\Program Files\Autodesk\Navisworks Manage 20xx\Plugins\BIMBot\`
   (or per-user `*.bundle` — Revit installer section unchanged).
3. Open Navisworks Manage → BIM-Bot tab → **Start BIM-Bot** (should read BIM-Bot ON).

## 2. Claude MCP (Claude Desktop)

File: `%APPDATA%\Claude\claude_desktop_config.json` — copy from
`revit-mcp-server/mcp-config.claude.json`. Restart Claude Desktop.
`BIM-Bot` = Revit tools, `BIM-Bot-Navis` = `navis_*` tools.

## 3. ChatGPT MCP (ChatGPT Desktop ≥ 2025 MCP support)

Settings → Apps → Add MCP Server → paste contents of
`revit-mcp-server/mcp-config.chatgpt.json`. Same `build/index.js` binary
serves both targets; `navis_*` tools appear automatically.

## 4. Integrated chat + API key (in-Navisworks)

Same providers as Revit: `gemini / openai / deepseek / perplexity /
openrouter / groq / cerebras / ollama` (`navis-mcp-plugin/.../AI/NavisAISettings.cs`).
Paste the SAME key as Revit into BIM-Bot → Settings in Navisworks.
Stored separately at `%AppData%\Autodesk\Navisworks\Addins\BIMBot\navis-settings.json`
so Revit settings are never touched.

Server-side AI tools (same `GEMINI_API_KEY`): `navis_ai_chat`,
`navis_ai_analyze_clashes`, `navis_ai_generate_plugin_code`.

## 5. Verify

- `navis_connection_status` → `{ target: navisworks, host, port, source }`
- `navis_get_model_info` with Manage open → model info
- Without Manage running → clear "service is probably not running" error (Revit tools unaffected)

## 6. Typical federated flow (Claude / ChatGPT)

`export_to_nwc` (Revit) → `navis_append_file` → `navis_run_clash_test` →
`navis_get_clash_results` → `navis_ai_analyze_clashes`
