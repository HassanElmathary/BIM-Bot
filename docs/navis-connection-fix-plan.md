# Implementation Plan — Navisworks MCP connection diagnostics & fast-fail

**Audience:** coding agent (OpenCode / Nemotron). Follow this file literally. Do not improvise outside the listed files.
**Repo root:** `C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP`
**Date:** 2026-10-15

---

## 1. Problem

Calling any `navis_*` MCP tool while Navisworks Manage is closed produces:

```
Failed: Could not connect to Navisworks at 127.0.0.1:8091 (from BIMBOT_NAVIS_HOST/BIMBOT_NAVIS_PORT)
after 5 attempts. Last error: TCP connection failed: connect ECONNREFUSED 127.0.0.1:8091.
Check that Navisworks Manage is open with a model loaded, and that the BIM-Bot ribbon button reads "BIM-Bot ON".
```

Verified facts (do NOT try to "fix" these, they are already correct):
- The plugin is installed at `C:\Program Files\Autodesk\Navisworks Manage 2024\Plugins\BIMBotNavisPlugin\BIMBotNavisPlugin.dll`.
- When Navisworks runs, the plugin auto-starts and listens on 8091 (see `%LOCALAPPDATA%\BIMBot\logs\navis-plugin.log`).
- The failure happened because `Roamer.exe` (Navisworks) was not running.

So the error is *expected*, but the code handles it badly:

| # | Defect | Where |
|---|---|---|
| D1 | `navis_connection_status` only echoes config (host/port/source). It does not check whether Navisworks is running, whether the plugin is installed, whether the handshake file exists, or whether the port answers. It is useless as a diagnostic. | `revit-mcp-server/src/tools/navis_reading_tools.ts` (tool `navis_connection_status`) |
| D2 | `connectWithRetry` retries 5 times with exponential backoff (1+2+4+8 s = **15 s+**) even on `ECONNREFUSED` when there is no live handshake file — i.e. when nothing will ever answer. Every failing tool call wastes ~15 s. | `revit-mcp-server/src/utils/navisConnectionManager.ts` |
| D3 | The error message tells the user to check that the ribbon button "reads BIM-Bot ON". The Navis ribbon button never shows that text (its DisplayName is always `BIM-Bot`). Message is misleading and generic — it doesn't say *which* thing is wrong. | `revit-mcp-server/src/utils/navisConnectionManager.ts` line ~63 |
| D4 | The plugin has no cheap `ping` command, so the server cannot confirm that the listener on 8091 is really BIM-Bot (vs. another program on that port). | `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs` |
| D5 | Clicking the ribbon button silently stops the service (toggle). The user gets no feedback and can unknowingly leave it OFF, which produces the same ECONNREFUSED. | `navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.cs` `Execute()` |

---

## 2. Hard rules (read before editing)

1. **Only edit/create these files:**
   - CREATE `revit-mcp-server/src/utils/navisDiagnostics.ts`
   - EDIT `revit-mcp-server/src/utils/navisConnectionManager.ts`
   - EDIT `revit-mcp-server/src/tools/navis_reading_tools.ts` (only the `navis_connection_status` tool block)
   - EDIT `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs` (only add the `navis_ping` case + method)
   - EDIT `navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.cs` (only `Execute()`)
2. **Never touch** Revit-side files: `src/utils/ConnectionManager.ts`, `src/utils/endpoint.ts`, `src/utils/SocketClient.ts`, anything under `revit-mcp-plugin/`, `installer/setup.iss`, `navisEndpoint.ts`, `NavisSocketService.cs`, `NavisServiceEndpoint.cs`, `BIMBotAutoStart.cs`, `NavisPluginApp.cs`, any `.csproj`.
3. The working tree already contains **uncommitted changes from a previous fix** (setup.iss, navisEndpoint.ts, BIMBotAutoStart.cs, etc.). **Do not revert, reformat, or "clean up" them.** Do not run `git checkout`, `git stash`, `git reset`, or `git clean`.
4. **No new npm or NuGet dependencies.** Use only Node built-ins (`fs`, `os`, `path`, `net`, `child_process`).
5. Keep the code style of the surrounding file (4-space indent, double quotes in TS, `.js` suffix on relative imports because the project is `"module": "NodeNext"`).
6. Do **not** bump version numbers. Do **not** commit. Leave changes in the working tree for review.
7. Windows-only behaviour is fine (the product is Windows-only), but every OS call must be wrapped in try/catch and must time out — a diagnostic must never hang or throw.

---

## 3. Tasks

### Task 1 — Create `revit-mcp-server/src/utils/navisDiagnostics.ts`

Export exactly this API:

```ts
export type NavisVerdict =
    | "ok"                      // TCP reachable AND navis_ping answered
    | "navisworks_not_running"  // no Roamer.exe process
    | "plugin_not_installed"    // Roamer.exe may run, but no BIMBotNavisPlugin.dll in any install
    | "service_off"             // Roamer.exe running, plugin installed, port refuses
    | "wrong_listener"          // port accepts TCP but navis_ping fails / is not BIM-Bot
    | "unknown";                // anything else

export interface NavisDiagnosis {
    verdict: NavisVerdict;
    hint: string;               // one or two plain-English sentences telling the user what to do
    endpoint: { host: string; port: number; source: "handshake" | "env" | "default" };
    handshake: { path: string; exists: boolean; pid?: number; pidAlive?: boolean; port?: number; updated?: string };
    navisworksRunning: boolean | null;   // null = could not determine
    installs: { year: string; dir: string; pluginInstalled: boolean }[];
    tcpReachable: boolean;
    ping?: unknown;             // navis_ping result when verdict === "ok"
    logTail: string[];          // last 5 lines of navis-plugin.log, [] if missing
    elapsedMs: number;
}

export async function diagnoseNavis(opts?: { ping?: boolean }): Promise<NavisDiagnosis>;
```

Implementation requirements:

1. **Endpoint:** call `resolveNavisEndpoint()` from `./navisEndpoint.js`.
2. **Handshake:** path = `%LOCALAPPDATA%\BIMBot\service-navis.json` (same formula as `navisHandshakePath()` in navisEndpoint.ts — copy it, don't export-modify navisEndpoint.ts). Strip a UTF-8 BOM before `JSON.parse`. `pidAlive` via `process.kill(pid, 0)` in try/catch (`EPERM` ⇒ alive).
3. **Navisworks running:** `child_process.execFile("tasklist", ["/FI", "IMAGENAME eq Roamer.exe", "/FO", "CSV", "/NH"], { timeout: 3000, windowsHide: true })`. Running ⇔ stdout contains `"Roamer.exe"` (case-insensitive). On error/timeout ⇒ `null`. Wrap in a Promise; never reject.
4. **Installs:** for each base in `[process.env["ProgramFiles"] ?? "C:\\Program Files"]` read `<base>\Autodesk`, keep dirs matching `/^Navisworks Manage (\d{4})$/` **that contain `Roamer.exe`**. For each, `pluginInstalled = fs.existsSync(<dir>\Plugins\BIMBotNavisPlugin\BIMBotNavisPlugin.dll)`. Any fs error ⇒ empty list.
5. **TCP probe:** `net.connect({ host, port })` with a **1500 ms** timeout; resolve `true` on `connect` (then `socket.destroy()`), `false` on `error`/`timeout`. Never reject.
6. **Ping** (only if `tcpReachable` and `opts.ping !== false`): create `new RevitSocketClient(host, port)` from `./SocketClient.js`, connect, `sendCommand("navis_ping", {})` raced against a **3000 ms** timeout, then `disconnect()` in `finally`. Do **not** use `withNavisConnection` here (it retries and would recurse into the diagnosis). If `SocketClient`'s connect API is awkward, copy the pattern of `attemptConnect` from navisConnectionManager.ts (listen for `connect`/`error` on `client.rawSocket`, then `client.connect()`).
7. **Log tail:** last 5 non-empty lines of `%LOCALAPPDATA%\BIMBot\logs\navis-plugin.log`; `[]` if missing.
8. **Verdict order** (first match wins):
   1. `tcpReachable && ping succeeded` → `"ok"`, hint: `"Navisworks <year if known> is connected on port <port>."`
   2. `tcpReachable && ping failed` → `"wrong_listener"`, hint: `"Something is listening on <host>:<port> but it is not the BIM-Bot Navisworks plugin. Close the other program or restart Navisworks so the plugin can pick a free port (8091–8101)."`
   3. `installs.length > 0 && installs.every(i => !i.pluginInstalled)` → `"plugin_not_installed"`, hint: `"The BIM-Bot Navisworks plugin is not installed. Re-run the BIM-Bot installer and tick your Navisworks Manage version."`
   4. `navisworksRunning === false` → `"navisworks_not_running"`, hint: `"Navisworks Manage is not running. Open Navisworks Manage (the BIM-Bot service starts automatically), then retry."`
   5. `navisworksRunning === true` → `"service_off"`, hint: `"Navisworks is running but the BIM-Bot service is not listening. Open the Add-ins tab and click BIM-Bot once to start it, then retry. If that does not help, restart Navisworks."`
   6. else → `"unknown"`, hint: `"Could not reach Navisworks at <host>:<port>. Make sure Navisworks Manage is open."`
9. `elapsedMs` = wall time of the whole function. With Navisworks closed the function must finish in **< 4 s**.
10. The function must **never throw**: wrap the body in try/catch and on unexpected error return `verdict: "unknown"` with the error message in `hint`.

### Task 2 — Fast-fail in `navisConnectionManager.ts`

1. In `attemptConnect`'s `onError`, preserve the Node error code. Replace the reject with:
   ```ts
   const wrapped = new Error(`TCP connection failed: ${err.message}`) as NodeJS.ErrnoException;
   wrapped.code = (err as NodeJS.ErrnoException).code;
   reject(wrapped);
   ```
2. In `connectWithRetry`, after catching an error on an attempt, **stop retrying immediately** when **both**:
   - `(lastError as NodeJS.ErrnoException).code === "ECONNREFUSED"`, and
   - `resolveNavisEndpoint().source !== "handshake"` (no live plugin handshake ⇒ nobody is coming).

   Use `break` out of the loop. Keep the existing retry/backoff behaviour for every other case (timeouts, live handshake = plugin restarting).
3. Replace the final `throw new Error(...)` block with:
   ```ts
   const diag = await diagnoseNavis({ ping: false });
   throw new Error(
       `Could not connect to Navisworks at ${diag.endpoint.host}:${diag.endpoint.port} ` +
       `(${diag.verdict}). ${diag.hint} ` +
       `Last error: ${lastError?.message ?? "unknown"}. ` +
       `Run navis_connection_status for full details.`
   );
   ```
   (`ping: false` because we already know TCP failed.) Import `diagnoseNavis` from `./navisDiagnostics.js`.
4. Remove the string `"BIM-Bot ON"` from this file. **Do not** edit the same string in `ConnectionManager.ts` (Revit — out of scope).
5. Delete the now-unused `where` variable / `ep` lookup if nothing else uses them.

### Task 3 — Make `navis_connection_status` a real health check

In `revit-mcp-server/src/tools/navis_reading_tools.ts`, replace only the `navis_connection_status` tool:

- New description: `"Diagnose the Navisworks connection: checks Navisworks is running, the BIM-Bot plugin is installed, the handshake file, the TCP port and a live ping. Returns a verdict and a fix hint. Safe to call when Navisworks is closed."`
- Body: `const diag = await (await import("../utils/navisDiagnostics.js")).diagnoseNavis();` and return `JSON.stringify({ target: "navisworks", ...diag }, null, 2)` as text.
- Keep the existing outer try/catch shape.

### Task 4 — Add `navis_ping` to the plugin

In `navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.cs`:

1. Add `case "navis_ping": return Ping();` as the **first** case in the `switch (method)` in `Execute`.
2. Add the method (inside the same partial class, near `GetModelInfo`):
   ```csharp
   private static JObject Ping()
   {
       var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
       return new JObject
       {
           ["ok"] = true,
           ["product"] = "navisworks",
           ["pluginVersion"] = NavisPluginApp.Version,
           ["port"] = NavisPluginApp.ActivePort,
           ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
           ["hasDocument"] = doc != null && !doc.IsClear,
           ["fileName"] = doc?.FileName ?? "",
       };
   }
   ```
   If `doc.IsClear` doesn't compile against the referenced Navisworks API, use `doc != null && doc.Models.Count > 0` instead. Check how `GetModelInfo()` in the same file reads the document and mirror that.
3. `NavisPluginApp` lives in namespace `BIMBotNavisPlugin`; add `using BIMBotNavisPlugin;` at the top only if it is not already resolvable.

### Task 5 — Ribbon toggle feedback

In `navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.cs`, `Execute()`:

- Keep the existing 10-second auto-start guard exactly as is.
- After `StopService()` show:
  `System.Windows.Forms.MessageBox.Show("BIM-Bot service stopped. Claude / ChatGPT can no longer reach Navisworks.\n\nClick BIM-Bot again to restart it.", "BIM-Bot", MessageBoxButtons.OK, MessageBoxIcon.Information);`
- After `StartService(manual: true)` show:
  `$"BIM-Bot service is running on port {NavisPluginApp.ActivePort}."` with the same title/buttons/icon.
- In the `catch`, also show a `MessageBoxIcon.Error` box: `$"BIM-Bot could not start: {ex.Message}"`.
- Add `using System.Windows.Forms;` if needed. The project already references WinForms (`NavisPluginApp.cs` has `using System.Windows.Forms;`).

---

## 4. Build

Run both; both must succeed with **zero new errors**:

```bash
cd revit-mcp-server && npm run build
```

```bash
dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
```

If the .NET build fails because Navisworks API reference DLLs are missing on the machine, **stop and report the exact error** — do not edit the `.csproj` to work around it.

Do **not** deploy/copy the DLL into `C:\Program Files\...` — the reviewer will do that.

---

## 5. Acceptance tests (run what you can, report results verbatim)

With Navisworks **closed**:

| Test | Expected |
|---|---|
| `node -e "import('./revit-mcp-server/build/utils/navisDiagnostics.js').then(m=>m.diagnoseNavis()).then(d=>console.log(JSON.stringify(d,null,2)))"` | `verdict: "navisworks_not_running"`, `installs` lists 2024 with `pluginInstalled: true`, `tcpReachable: false`, `elapsedMs < 4000` |
| Simulated tool failure: `node -e "import('./revit-mcp-server/build/utils/navisConnectionManager.js').then(m=>m.withNavisConnection(c=>c.sendCommand('navis_get_model_info',{}))).catch(e=>console.log(e.message))"` | Message contains `navisworks_not_running` and `Open Navisworks Manage`; returns in **< 5 s** (was ~15 s); does **not** contain `BIM-Bot ON` |
| `git diff --stat` | Only the 5 files in §2 rule 1 changed beyond the pre-existing uncommitted changes |

With Navisworks **open** (reviewer will run): `navis_connection_status` ⇒ `verdict: "ok"` with `ping.ok === true`; `navis_get_model_info` works; clicking the ribbon button shows the stop dialog, clicking again shows the start dialog, and a tool call in between returns `service_off`.

---

## 6. Report back

When done, reply with:
1. List of files changed/created.
2. Output of both builds (last ~20 lines each).
3. Output of the acceptance tests in §5.
4. Anything in this plan you could not do exactly as written, and why.
