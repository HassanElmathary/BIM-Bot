import * as fs from "fs";
import * as os from "os";
import * as path from "path";

export interface RevitEndpoint {
    host: string;
    port: number;
    source: "handshake" | "env" | "default";
}

export const DEFAULT_HOST = "127.0.0.1";
export const DEFAULT_PORT = 8080;

/**
 * Handshake file written by the Revit plugin's SocketService once it knows
 * which port it actually bound to.
 *
 * Port 8080 is one of the most contested ports on a Windows workstation. When
 * something else owns it the plugin now walks forward to the next free port —
 * but only this file tells us where it landed. Dialling 8080 blindly was worse
 * than a timeout: on a machine where another service holds 8080, every tool
 * call connected to that unrelated process and hung.
 */
function handshakePath(): string {
    const localAppData =
        process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "BIMBot", "service.json");
}

/**
 * Is the Revit process that wrote the handshake still running?
 *
 * The plugin deletes this file when the service stops, but a Revit crash
 * leaves it behind — and a stale port is worse than no port, because by then
 * some unrelated process may own it and we would send BIM commands into it.
 * Signal 0 tests for existence without touching the process; EPERM means it
 * exists under another account, which still counts as alive.
 */
function writerIsAlive(pid: unknown): boolean {
    const id = Number(pid);
    if (!Number.isInteger(id) || id <= 0) return true; // older plugin — no pid to check
    try {
        process.kill(id, 0);
        return true;
    } catch (err) {
        return (err as NodeJS.ErrnoException)?.code === "EPERM";
    }
}

/**
 * Resolve where the Revit-side service is listening.
 *
 * Precedence: BIMBOT_PORT/BIMBOT_HOST env override → handshake file → 8080.
 * Re-read on every connection attempt: Revit may have restarted (and moved
 * port) since this process started.
 */
export function resolveEndpoint(): RevitEndpoint {
    const envPort = process.env.BIMBOT_PORT
        ? Number.parseInt(process.env.BIMBOT_PORT, 10)
        : NaN;
    if (Number.isInteger(envPort) && envPort > 0 && envPort < 65536) {
        return {
            host: process.env.BIMBOT_HOST || DEFAULT_HOST,
            port: envPort,
            source: "env",
        };
    }

    try {
        const raw = fs.readFileSync(handshakePath(), "utf8");
        const data = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
        const port = Number(data?.port);
        if (Number.isInteger(port) && port > 0 && port < 65536 && writerIsAlive(data?.pid)) {
            return {
                host: typeof data.host === "string" && data.host ? data.host : DEFAULT_HOST,
                port,
                source: "handshake",
            };
        }
    } catch {
        // No handshake yet (Revit not started, or an older plugin build) —
        // fall through to the historical default so nothing regresses.
    }

    return { host: DEFAULT_HOST, port: DEFAULT_PORT, source: "default" };
}
