import * as crypto from "crypto";
import * as fs from "fs";
import * as os from "os";
import * as path from "path";

/**
 * Per-user shared secret the Revit and Navisworks plugins require on every
 * request (top-level "auth" field). Without it any local process or web page
 * could drive Revit through the loopback socket.
 *
 * The plugins create %LOCALAPPDATA%\BIMBot\auth.token when their service
 * starts; we create it ourselves if we get there first, so start-up order does
 * not matter. Keep in sync with ServiceAuth.cs / NavisServiceAuth.cs.
 */
export const AUTH_FIELD = "auth";
export const UNAUTHORIZED_CODE = -32001;

function tokenPath(): string {
    const localAppData =
        process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "BIMBot", "auth.token");
}

function tryRead(file: string): string | undefined {
    try {
        const text = fs.readFileSync(file, "utf8").replace(/^﻿/, "").trim();
        return text.length >= 32 ? text : undefined;
    } catch {
        return undefined;
    }
}

/**
 * Read the token, creating it if no plugin has yet. Read on every call (the
 * file is tiny): a reinstall may replace it while this server keeps running.
 */
export function serviceToken(): string {
    const file = tokenPath();
    const existing = tryRead(file);
    if (existing) return existing;

    fs.mkdirSync(path.dirname(file), { recursive: true });
    const fresh = crypto.randomBytes(32).toString("hex");
    const temp = `${file}.${crypto.randomUUID()}.tmp`;
    fs.writeFileSync(temp, fresh, "utf8");
    try {
        // linkSync fails with EEXIST when a plugin won the race, and never
        // exposes a half-written file — both sides then agree on one token.
        fs.linkSync(temp, file);
        return fresh;
    } catch {
        const winner = tryRead(file);
        if (winner) return winner;
        fs.writeFileSync(file, fresh, "utf8");
        return fresh;
    } finally {
        try { fs.unlinkSync(temp); } catch { /* already gone */ }
    }
}
