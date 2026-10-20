import * as fs from "fs";
import * as os from "os";
import * as path from "path";

/**
 * Manual bridge session shared by the Navisworks master button, the Revit "Join Bridge" button and
 * this server: %LOCALAPPDATA%\BIMBot\bridge-session.json.
 */

export interface BridgeModel {
    name: string;
    revitFile?: string;
}

export interface BridgeSlave {
    slot: string;
    pid: number;
    port: number;
    documentTitle: string;
    documentPath?: string;
    model: string;
    joinedUtc?: string;
}

export interface BridgeSession {
    version: number;
    sessionId: string;
    startedUtc: string;
    master: { pid: number; port: number; file: string; models: BridgeModel[] };
    slaves: BridgeSlave[];
}

export function bridgeSessionPath(): string {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "BIMBot", "bridge-session.json");
}

function pidAlive(pid: unknown): boolean {
    const id = Number(pid);
    if (!Number.isInteger(id) || id <= 0) return false;
    try {
        process.kill(id, 0);
        return true;
    } catch (err) {
        return (err as NodeJS.ErrnoException)?.code === "EPERM";
    }
}

/**
 * The live bridge session, or null when none is running (no file, unreadable, or the Navisworks
 * master has exited). Slaves whose Revit process has exited are dropped.
 */
export function readBridgeSession(): BridgeSession | null {
    try {
        const raw = fs.readFileSync(bridgeSessionPath(), "utf8");
        const s = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw) as BridgeSession;
        if (!s?.master || !pidAlive(s.master.pid)) return null;
        s.master.models = Array.isArray(s.master.models) ? s.master.models : [];
        s.slaves = (Array.isArray(s.slaves) ? s.slaves : [])
            .filter((x) => pidAlive(x.pid) && Number.isInteger(x.port) && x.port > 0);
        return s;
    } catch {
        return null;
    }
}
