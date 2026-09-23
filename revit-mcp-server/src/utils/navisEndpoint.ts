import * as fs from "fs";
import * as os from "os";
import * as path from "path";

// ── ADDITIVE-ONLY: Navisworks endpoint resolution ──
// Does NOT touch revit endpoint.ts. Revit stays on 8080 + service.json.
// Navisworks Manage listens on 8091 (+scan) and publishes service-navis.json.

export interface NavisEndpoint {
    host: string;
    port: number;
    source: "handshake" | "env" | "default";
}

export const NAVIS_DEFAULT_HOST = "127.0.0.1";
export const NAVIS_DEFAULT_PORT = 8091;

function navisHandshakePath(): string {
    const localAppData =
        process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "BIMBot", "service-navis.json");
}

function writerIsAlive(pid: unknown): boolean {
    const id = Number(pid);
    if (!Number.isInteger(id) || id <= 0) return true;
    try {
        process.kill(id, 0);
        return true;
    } catch (err) {
        return (err as NodeJS.ErrnoException)?.code === "EPERM";
    }
}

/**
 * Resolve where the Navisworks-side service is listening.
 * Precedence: BIMBOT_NAVIS_PORT/BIMBOT_NAVIS_HOST env → service-navis.json → 8091.
 */
export function resolveNavisEndpoint(): NavisEndpoint {
    const envPort = process.env.BIMBOT_NAVIS_PORT
        ? Number.parseInt(process.env.BIMBOT_NAVIS_PORT, 10)
        : NaN;
    if (Number.isInteger(envPort) && envPort > 0 && envPort < 65536) {
        return {
            host: process.env.BIMBOT_NAVIS_HOST || NAVIS_DEFAULT_HOST,
            port: envPort,
            source: "env",
        };
    }

    try {
        const raw = fs.readFileSync(navisHandshakePath(), "utf8");
        const data = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
        const port = Number(data?.port);
        if (Number.isInteger(port) && port > 0 && port < 65536 && writerIsAlive(data?.pid)) {
            return {
                host: typeof data.host === "string" && data.host ? data.host : NAVIS_DEFAULT_HOST,
                port,
                source: "handshake",
            };
        }
    } catch {
        // No handshake yet — Navisworks service not started.
    }

    return { host: NAVIS_DEFAULT_HOST, port: NAVIS_DEFAULT_PORT, source: "default" };
}
