#!/usr/bin/env node
/**
 * probe-revit.cjs — strict Link-2 diagnostic.
 *
 * Reads the handshake file the Revit plugin publishes
 * (%LOCALAPPDATA%\BIMBot\service.json), checks the writer PID is alive,
 * proves something is listening, and performs a full JSON-RPC round-trip
 * ("ping" works with NO document open; any other command needs a project).
 *
 * Exit codes: 0 = full round-trip OK, 1 = config/handshake problem,
 * 2 = TCP/timeout problem.
 *
 * Usage: node probe-revit.cjs [--port N] [--host H]
 */
const fs = require("fs");
const net = require("net");
const os = require("os");
const path = require("path");

function handshakePath() {
    const lad = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(lad, "BIMBot", "service.json");
}

function parseArgs() {
    const a = process.argv.slice(2);
    const o = {};
    for (let i = 0; i < a.length; i++) {
        if (a[i] === "--port") o.port = Number(a[++i]);
        else if (a[i] === "--host") o.host = a[++i];
    }
    return o;
}

function writerAlive(pid) {
    const id = Number(pid);
    if (!Number.isInteger(id) || id <= 0) return null; // older plugin, unknown
    try { process.kill(id, 0); return true; }
    catch (e) { return e && e.code === "EPERM" ? true : false; }
}

function main() {
    const opts = parseArgs();
    let host = opts.host || process.env.BIMBOT_HOST || "127.0.0.1";
    let port = opts.port || (process.env.BIMBOT_PORT ? Number(process.env.BIMBOT_PORT) : NaN);
    let source = "flag/env";
    if (!Number.isInteger(port)) {
        try {
            const raw = fs.readFileSync(handshakePath(), "utf8");
            const data = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
            port = Number(data.port);
            if (data.host) host = data.host;
            source = "handshake";
            console.log(`handshake: ${host}:${port} (pid ${data.pid}, plugin ${data.pluginVersion || "?"}, ${data.startedAt || "?"})`);
            const alive = writerAlive(data.pid);
            if (alive === false) {
                console.error(`FAIL: Revit PID ${data.pid} is dead — stale handshake. Reopen Revit (service republishes on start).`);
                process.exit(1);
            }
        } catch (e) {
            console.error(`FAIL: no handshake file (${handshakePath()}) and no --port. Is Revit open with BIM-Bot ON? (${e.message})`);
            process.exit(1);
        }
    }
    if (!Number.isInteger(port) || port <= 0 || port > 65535) {
        console.error(`FAIL: invalid port ${port}`);
        process.exit(1);
    }
    console.log(`probing ${host}:${port} (source: ${source}) ...`);

    const sock = new net.Socket();
    let buf = Buffer.alloc(0);
    let done = false;
    const finish = (code, msg) => { if (!done) { done = true; console.log(msg); sock.destroy(); process.exit(code); } };

    sock.setTimeout(5000);
    sock.on("timeout", () => finish(2, "FAIL: TCP timeout (5s) — service not listening or firewall blocking loopback."));
    sock.on("error", (e) => finish(2, `FAIL: TCP error: ${e.message}`));
    sock.on("data", (d) => {
        buf = Buffer.concat([buf, d]);
        const s = buf.toString("utf8");
        // Any well-formed JSON-RPC reply (result OR error) proves round-trip.
        if (s.includes('"result"') || s.includes('"error"') || s.includes('"id"')) {
            finish(0, `OK: Revit answered (${s.slice(0, 300)})`);
        }
    });
    sock.connect(port, host, () => {
        const body = Buffer.from(JSON.stringify({ jsonrpc: "2.0", id: "probe", method: "ping", params: {} }), "utf8");
        sock.write(`Content-Length: ${body.length}\n`);
        sock.write(body);
    });
}

main();
