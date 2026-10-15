FOLLOW-UP FIX — only edit revit-mcp-server/src/utils/navisDiagnostics.ts
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Context: this follows docs/navis-connection-fix-plan.md, which you already implemented. A reviewer found 2 bugs + 1 edge case.

1. Wrap the ping call in a 3000 ms timeout (sendCommand's own timeout is 300 s, so the diagnostic can hang 5 minutes). Replace
     ping = await client.sendCommand("navis_ping", {});
   with
     ping = await Promise.race([
         client.sendCommand("navis_ping", {}),
         new Promise((_, rej) => setTimeout(() => rej(new Error("navis_ping timed out (3s)")), 3000)),
     ]);

2. Keep the real error, and treat a JSON-RPC error reply as "BIM-Bot is there but outdated":
   - In the ping catch:  ping = { ok: false, error: err instanceof Error ? err.message : String(err) };
   - Add verdict "plugin_outdated" to the NavisVerdict type.
   - Before the "wrong_listener" branch add:
       if (tcpReachable && ping && (ping as any).ok === false && /Unknown Navisworks command/i.test(String((ping as any).error)))
         -> verdict "plugin_outdated", hint "BIM-Bot is running in Navisworks but the plugin is an older build without navis_ping. Re-run the BIM-Bot installer (or redeploy BIMBotNavisPlugin.dll) and restart Navisworks."

3. When opts.ping === false and tcpReachable is true, do NOT return "wrong_listener":
   return verdict "unknown" with hint "Navisworks port is now answering — retry the command."

Rules:
- Do not edit any other file. Do not commit. Do not run git checkout/stash/reset/clean.
- No new dependencies. Keep the existing code style.

Then:
- cd revit-mcp-server && npm run build
- Rerun from the repo root, with Navisworks closed:
  Test A: node -e "import('./revit-mcp-server/build/utils/navisDiagnostics.js').then(m=>m.diagnoseNavis()).then(d=>console.log(JSON.stringify(d,null,2)))"
  Test B: node -e "import('./revit-mcp-server/build/utils/navisConnectionManager.js').then(m=>m.withNavisConnection(c=>c.sendCommand('navis_get_model_info',{}))).catch(e=>console.log(e.message))"
- Report: the diff of navisDiagnostics.ts, build output, and Test A / Test B output verbatim.
