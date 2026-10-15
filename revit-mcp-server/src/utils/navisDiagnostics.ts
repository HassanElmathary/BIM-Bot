import * as fs from "fs";
import * as os from "os";
import * as path from "path";
import * as net from "net";
import * as child_process from "child_process";
import { resolveNavisEndpoint } from "./navisEndpoint.js";
import { RevitSocketClient } from "./SocketClient.js";

export type NavisVerdict =
    | "ok"
    | "navisworks_not_running"
    | "plugin_not_installed"
    | "service_off"
    | "wrong_listener"
    | "plugin_outdated"
    | "unknown";

export interface NavisDiagnosis {
    verdict: NavisVerdict;
    hint: string;
    endpoint: { host: string; port: number; source: "handshake" | "env" | "default" };
    handshake: { path: string; exists: boolean; pid?: number; pidAlive?: boolean; port?: number; updated?: string };
    navisworksRunning: boolean | null;
    installs: { year: string; dir: string; pluginInstalled: boolean }[];
    tcpReachable: boolean;
    ping?: unknown;
    logTail: string[];
    elapsedMs: number;
}

async function diagnoseNavis(opts?: { ping?: boolean }): Promise<NavisDiagnosis> {
    const startMs = Date.now();
    try {
        const endpoint = resolveNavisEndpoint();

        // Handshake file
        const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
        const handshakePath = path.join(localAppData, "BIMBot", "service-navis.json");
        let handshake = {
            path: handshakePath,
            exists: false,
            pid: undefined as number | undefined,
            pidAlive: undefined as boolean | undefined,
            port: undefined as number | undefined,
            updated: undefined as string | undefined,
        };
        try {
            const raw = fs.readFileSync(handshakePath, "utf8");
            handshake.exists = true;
            const data = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
            handshake.pid = data?.pid;
            handshake.port = data?.port;
            handshake.updated = data?.updated;
            if (typeof handshake.pid === "number") {
                try {
                    process.kill(handshake.pid, 0);
                    handshake.pidAlive = true;
                } catch (err) {
                    handshake.pidAlive = (err as NodeJS.ErrnoException)?.code === "EPERM";
                }
            }
        } catch {
            // Handshake file doesn't exist or is unreadable
        }

        // Navisworks running check
        let navisworksRunning: boolean | null = null;
        try {
            const result = await new Promise<{ stdout: string; stderr: string }>((resolve) => {
                child_process.execFile(
                    "tasklist",
                    ["/FI", "IMAGENAME eq Roamer.exe", "/FO", "CSV", "/NH"],
                    { timeout: 3000, windowsHide: true },
                    (err, stdout, stderr) => resolve({ stdout: stdout ?? "", stderr: stderr ?? "" })
                );
            });
            navisworksRunning = /Roamer\.exe/i.test(result.stdout);
        } catch {
            navisworksRunning = null;
        }

        // Installs check
        let installs: { year: string; dir: string; pluginInstalled: boolean }[] = [];
        try {
            const programFiles = process.env["ProgramFiles"] ?? "C:\\Program Files";
            const autodeskDir = path.join(programFiles, "Autodesk");
            if (fs.existsSync(autodeskDir)) {
                const entries = fs.readdirSync(autodeskDir, { withFileTypes: true });
                for (const entry of entries) {
                    if (!entry.isDirectory()) continue;
                    const match = entry.name.match(/^Navisworks Manage (\d{4})$/);
                    if (!match) continue;
                    const year = match[1];
                    const dir = path.join(autodeskDir, entry.name);
                    const roamerPath = path.join(dir, "Roamer.exe");
                    if (!fs.existsSync(roamerPath)) continue;
                    const pluginPath = path.join(dir, "Plugins", "BIMBotNavisPlugin", "BIMBotNavisPlugin.dll");
                    const pluginInstalled = fs.existsSync(pluginPath);
                    installs.push({ year, dir, pluginInstalled });
                }
            }
        } catch {
            installs = [];
        }

        // TCP probe
        let tcpReachable = false;
        try {
            await new Promise<void>((resolve, reject) => {
                const socket = net.connect({ host: endpoint.host, port: endpoint.port });
                const timer = setTimeout(() => {
                    socket.destroy();
                    reject(new Error("timeout"));
                }, 1500);
                socket.once("connect", () => {
                    clearTimeout(timer);
                    socket.destroy();
                    resolve();
                });
                socket.once("error", (err) => {
                    clearTimeout(timer);
                    reject(err);
                });
            });
            tcpReachable = true;
        } catch {
            tcpReachable = false;
        }

        // Ping if TCP reachable
        let ping: unknown = undefined;
        if (tcpReachable && opts?.ping !== false) {
            const client = new RevitSocketClient(endpoint.host, endpoint.port);
            try {
                await new Promise<void>((resolve, reject) => {
                    let settled = false;
                    const onConnect = () => {
                        if (settled) return;
                        settled = true;
                        client.rawSocket.removeListener("error", onError);
                        clearTimeout(timer);
                        resolve();
                    };
                    const onError = (err: Error) => {
                        if (settled) return;
                        settled = true;
                        client.rawSocket.removeListener("connect", onConnect);
                        clearTimeout(timer);
                        reject(err);
                    };
                    const timer = setTimeout(() => {
                        if (settled) return;
                        settled = true;
                        client.rawSocket.removeListener("connect", onConnect);
                        client.rawSocket.removeListener("error", onError);
                        client.disconnect();
                        reject(new Error("Connection timeout"));
                    }, 3000);
                    client.rawSocket.once("connect", onConnect);
                    client.rawSocket.once("error", onError);
                    client.connect();
                });
                ping = await Promise.race([
                    client.sendCommand("navis_ping", {}),
                    new Promise((_, rej) => setTimeout(() => rej(new Error("navis_ping timed out (3s)")), 3000)),
                ]);
            } catch (err) {
                ping = { ok: false, error: err instanceof Error ? err.message : String(err) };
            } finally {
                try { client.disconnect(); } catch { }
            }
        }

        // Log tail
        let logTail: string[] = [];
        try {
            const logPath = path.join(localAppData, "BIMBot", "logs", "navis-plugin.log");
            if (fs.existsSync(logPath)) {
                const content = fs.readFileSync(logPath, "utf8");
                const lines = content.split(/\r?\n/).filter(l => l.trim().length > 0);
                logTail = lines.slice(-5);
            }
        } catch {
            logTail = [];
        }

        // Determine verdict
        let verdict: NavisVerdict = "unknown";
        let hint = "";

        const pingSucceeded = ping && typeof ping === "object" && ping !== null && "ok" in ping && (ping as any).ok === true;

        if (tcpReachable && pingSucceeded) {
            verdict = "ok";
            const year = installs.find(i => i.pluginInstalled)?.year ?? "unknown";
            hint = `Navisworks ${year} is connected on port ${endpoint.port}.`;
        } else if (tcpReachable && ping && (ping as any).ok === false && /Unknown Navisworks command/i.test(String((ping as any).error))) {
            verdict = "plugin_outdated";
            hint = "BIM-Bot is running in Navisworks but the plugin is an older build without navis_ping. Re-run the BIM-Bot installer (or redeploy BIMBotNavisPlugin.dll) and restart Navisworks.";
        } else if (tcpReachable && opts?.ping !== false) {
            verdict = "wrong_listener";
            hint = `Something is listening on ${endpoint.host}:${endpoint.port} but it is not the BIM-Bot Navisworks plugin. Close the other program or restart Navisworks so the plugin can pick a free port (8091–8101).`;
        } else if (tcpReachable && opts?.ping === false) {
            verdict = "unknown";
            hint = "Navisworks port is now answering — retry the command.";
        } else if (installs.length > 0 && installs.every(i => !i.pluginInstalled)) {
            verdict = "plugin_not_installed";
            hint = "The BIM-Bot Navisworks plugin is not installed. Re-run the BIM-Bot installer and tick your Navisworks Manage version.";
        } else if (navisworksRunning === false) {
            verdict = "navisworks_not_running";
            hint = "Navisworks Manage is not running. Open Navisworks Manage (the BIM-Bot service starts automatically), then retry.";
        } else if (navisworksRunning === true) {
            verdict = "service_off";
            hint = "Navisworks is running but the BIM-Bot service is not listening. Open the Add-ins tab and click BIM-Bot once to start it, then retry. If that does not help, restart Navisworks.";
        } else {
            verdict = "unknown";
            hint = `Could not reach Navisworks at ${endpoint.host}:${endpoint.port}. Make sure Navisworks Manage is open.`;
        }

        return {
            verdict,
            hint,
            endpoint,
            handshake,
            navisworksRunning,
            installs,
            tcpReachable,
            ping,
            logTail,
            elapsedMs: Date.now() - startMs,
        };
    } catch (err) {
        return {
            verdict: "unknown",
            hint: `Diagnostic error: ${err instanceof Error ? err.message : String(err)}`,
            endpoint: resolveNavisEndpoint(),
            handshake: { path: "", exists: false },
            navisworksRunning: null,
            installs: [],
            tcpReachable: false,
            logTail: [],
            elapsedMs: Date.now() - startMs,
        };
    }
}

export { diagnoseNavis };