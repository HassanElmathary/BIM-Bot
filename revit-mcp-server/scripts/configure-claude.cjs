#!/usr/bin/env node
/**
 * configure-claude.cjs — register (or repair) the BIM-Bot MCP server in
 * Claude Desktop, Claude Code, ChatGPT Desktop, Cursor, Windsurf and VS Code.
 *
 * Unlike a naive "skip if BIM-Bot exists" check, this validates that the
 * configured node/server paths actually exist and repairs stale entries
 * (e.g. after the install folder or repo moved). Safe to run repeatedly.
 *
 * Usage:
 *   node configure-claude.cjs [--node <path-to-node.exe>] [--server <path-to-index.js>]
 *                             [--home <profile-dir>] [--all-users]
 *   node configure-claude.cjs --remove [--home <profile-dir>] [--all-users]
 *
 * --remove deletes the BIM-Bot entry from every supported MCP client config
 * instead of adding it. Used by the uninstaller so no dead entry pointing at
 * a deleted folder is left behind. Other servers in the same file are kept.
 *
 * With no args, paths are derived from this script's location:
 *   installed layout:  {app}\server\scripts\configure-claude.cjs
 *                      → {app}\server\build\index.js, {app}\nodejs\node.exe
 *   dev layout:        {repo}\revit-mcp-server\scripts\configure-claude.cjs
 *                      → {repo}\revit-mcp-server\build\index.js, process.execPath
 *
 * Profile targeting:
 *   By default the *current* process's profile (%USERPROFILE%/%APPDATA%) is
 *   configured. That is wrong when an elevated installer runs under a
 *   different admin account than the person installing — the entry would land
 *   in the admin's profile, or be skipped entirely because the admin has no
 *   MCP client. --all-users scans every real user profile on the machine and
 *   configures each one that already has a client installed; --home targets
 *   one specific profile directory.
 */
const fs = require("fs");
const path = require("path");

const SERVER_KEY = "BIM-Bot";

function parseArgs() {
    const args = process.argv.slice(2);
    const out = {};
    for (let i = 0; i < args.length; i++) {
        if (args[i] === "--node") out.node = args[++i];
        else if (args[i] === "--server") out.server = args[++i];
        else if (args[i] === "--home") out.home = args[++i];
        else if (args[i] === "--all-users") out.allUsers = true;
        else if (args[i] === "--remove") out.remove = true;
    }
    return out;
}

function resolvePaths() {
    const opts = parseArgs();
    // --remove never needs node/server paths — the uninstaller runs it when
    // the bundled runtime may already be gone.
    if (opts.remove) return { nodeExe: null, indexJs: null };
    const serverDir = path.dirname(__dirname); // scripts/ → server root

    let indexJs = opts.server || path.join(serverDir, "build", "index.js");
    if (!fs.existsSync(indexJs)) {
        console.error(`ERROR: MCP server not found at ${indexJs}`);
        process.exit(1);
    }

    let nodeExe = opts.node;
    if (!nodeExe) {
        // Installed layout: {app}\server → {app}\nodejs\node.exe
        const bundled = path.join(path.dirname(serverDir), "nodejs", "node.exe");
        nodeExe = fs.existsSync(bundled) ? bundled : process.execPath;
    }

    return { nodeExe, indexJs };
}

function isEntryValid(entry) {
    if (!entry || typeof entry !== "object") return false;
    const cmd = entry.command;
    if (!cmd || !path.isAbsolute(cmd) || !fs.existsSync(cmd)) return false;
    const script = Array.isArray(entry.args) ? entry.args[0] : null;
    if (!script || !fs.existsSync(script)) return false;
    return true;
}

/**
 * Ensure the BIM-Bot entry in one config file. Returns a status string.
 */
function ensureConfig(label, configPath, nodeExe, indexJs, createIfMissing, opts = {}) {
    const serversKey = opts.serversKey || "mcpServers";
    const stdioType = !!opts.stdioType;

    let config = {};
    let hadBom = false;
    let rebuiltFromBroken = false;
    if (fs.existsSync(configPath)) {
        try {
            // Strip UTF-8 BOM — some editors/tools write one and it breaks JSON.parse
            const raw = fs.readFileSync(configPath, "utf8");
            hadBom = raw.charCodeAt(0) === 0xfeff;
            config = JSON.parse(hadBom ? raw.slice(1) : raw);
        } catch (err) {
            // A config the client cannot parse is already broken for the user;
            // "left untouched, fix it yourself" is a dead end for a non-developer.
            // Quarantine it with a timestamped copy and rebuild a clean one.
            try {
                const stamp = new Date().toISOString().replace(/[:.]/g, "-");
                fs.copyFileSync(configPath, `${configPath}.broken-${stamp}`);
                config = {};
                rebuiltFromBroken = true;
            } catch (copyErr) {
                return `${label}: config is not valid JSON (${err.message}) and could not be backed up (${copyErr.message}) — left untouched: ${configPath}`;
            }
        }
    } else if (!createIfMissing) {
        return `${label}: not installed (no config file) — skipped`;
    }

    if (!config[serversKey] || typeof config[serversKey] !== "object") {
        config[serversKey] = {};
    }

    if (isEntryValid(config[serversKey][SERVER_KEY]) && !hadBom) {
        return `${label}: already configured correctly`;
    }

    const wasStale = !!config[serversKey][SERVER_KEY];
    const entry = {
        command: nodeExe,
        args: [indexJs],
        env: {},
    };
    // VS Code requires an explicit transport type on each server entry.
    if (stdioType) entry.type = "stdio";
    config[serversKey][SERVER_KEY] = entry;

    try {
        if (fs.existsSync(configPath)) {
            fs.copyFileSync(configPath, configPath + ".bimbot-backup");
        }
        fs.mkdirSync(path.dirname(configPath), { recursive: true });
        fs.writeFileSync(configPath, JSON.stringify(config, null, 2));
    } catch (err) {
        // One unwritable profile must not abort the rest of an --all-users run.
        return `${label}: failed to write config (${err.message}): ${configPath}`;
    }

    if (rebuiltFromBroken) return `${label}: config was corrupt (invalid JSON) — backed it up and rebuilt it`;
    if (hadBom) return `${label}: rewrote config without UTF-8 BOM (breaks some parsers)`;
    return `${label}: ${wasStale ? "repaired stale entry" : "added BIM-Bot entry"}`;
}

// ── ChatGPT Desktop (TOML) ───────────────────────────────────
//
// ChatGPT Desktop runs local stdio MCP servers from the Codex config it
// shares with the Codex CLI: %USERPROFILE%\.codex\config.toml (or
// $CODEX_HOME\config.toml), table [mcp_servers.<name>]. It is TOML, not JSON,
// and the file is full of settings the app owns (model, projects, plugins...),
// so it is edited line-wise: only the BIM-Bot table (and its sub-tables) is
// replaced; every other byte is preserved.

// Parse a TOML table header line into its key parts, or null if the line is
// not a header. Handles bare, "basic" and 'literal' key segments.
function tomlHeaderParts(line) {
    const m = /^\s*\[\[?(.+?)\]\]?\s*(#.*)?$/.exec(line);
    if (!m) return null;
    const src = m[1];
    const parts = [];
    let i = 0;
    while (i < src.length) {
        while (src[i] === " " || src[i] === "\t") i++;
        let part = "";
        if (src[i] === '"') {
            i++;
            while (i < src.length && src[i] !== '"') {
                if (src[i] === "\\" && i + 1 < src.length) { part += src[i + 1]; i += 2; }
                else part += src[i++];
            }
            i++;
        } else if (src[i] === "'") {
            i++;
            while (i < src.length && src[i] !== "'") part += src[i++];
            i++;
        } else {
            while (i < src.length && /[A-Za-z0-9_-]/.test(src[i])) part += src[i++];
            if (!part) return null; // not a key — e.g. an array value line
        }
        parts.push(part);
        while (src[i] === " " || src[i] === "\t") i++;
        if (i < src.length) {
            if (src[i] !== ".") return null;
            i++;
        }
    }
    return parts;
}

function isOwnTomlTable(parts) {
    return !!parts && parts.length >= 2 && parts[0] === "mcp_servers" && parts[1] === SERVER_KEY;
}

// Split a TOML document into the BIM-Bot block and everything else.
// Also drops inline forms (`BIM-Bot = {...}` under [mcp_servers], or
// `mcp_servers.BIM-Bot... =` dotted keys at the root) so the table we append
// can never collide with a duplicate key.
function splitTomlBlock(text) {
    const lines = text.split(/\r?\n/);
    const keep = [];
    const own = [];
    let table = [];          // key parts of the current table ([] = root)
    let inOwn = false;
    const inlineKey = new RegExp(`^\\s*(["']?)${SERVER_KEY}\\1\\s*=`);
    const rootDotted = new RegExp(`^\\s*mcp_servers\\s*\\.\\s*(["']?)${SERVER_KEY}\\1\\s*[.=]`);
    for (const line of lines) {
        const parts = tomlHeaderParts(line);
        if (parts) {
            table = parts;
            inOwn = isOwnTomlTable(parts);
        }
        if (inOwn) { own.push(line); continue; }
        if (!parts && table.length === 1 && table[0] === "mcp_servers" && inlineKey.test(line)) { own.push(line); continue; }
        if (!parts && table.length === 0 && rootDotted.test(line)) { own.push(line); continue; }
        keep.push(line);
    }
    return { keep, own };
}

function tomlString(value) {
    // Literal strings need no escaping — ideal for Windows paths.
    if (!/['\r\n]/.test(value)) return `'${value}'`;
    return JSON.stringify(value); // valid TOML basic string for plain text
}

function readTomlString(src) {
    if (!src) return null;
    if (src[0] === "'") return src.slice(1, -1);
    try { return JSON.parse(src); } catch { return null; }
}

function isTomlBlockValid(ownLines) {
    const text = ownLines.join("\n");
    const cmd = /^\s*command\s*=\s*("(?:[^"\\]|\\.)*"|'[^']*')/m.exec(text);
    const arg = /^\s*args\s*=\s*\[\s*("(?:[^"\\]|\\.)*"|'[^']*')/m.exec(text);
    return isEntryValid({
        command: readTomlString(cmd && cmd[1]),
        args: [readTomlString(arg && arg[1])],
    });
}

function buildTomlBlock(nodeExe, indexJs) {
    return [
        `[mcp_servers.${SERVER_KEY}]`,
        `command = ${tomlString(nodeExe)}`,
        `args = [${tomlString(indexJs)}]`,
        // First launch of node from Program Files can exceed the 10 s default
        // while antivirus scans it; long Revit operations exceed the 60 s one.
        "startup_timeout_sec = 30",
        "tool_timeout_sec = 300",
    ];
}

function codexHome(home, isCurrentProfile) {
    if (isCurrentProfile && process.env.CODEX_HOME) return process.env.CODEX_HOME;
    return path.join(home, ".codex");
}

// ChatGPT Desktop ships as an MSIX package (OpenAI.Codex_* / OpenAI.ChatGPT*);
// the Codex CLI leaves ~/.codex. Either means someone will read config.toml.
function chatGptSeen(home, codexDir) {
    if (fs.existsSync(codexDir)) return true;
    try {
        const packages = path.join(home, "AppData", "Local", "Packages");
        return fs.readdirSync(packages).some((n) => /^OpenAI\.(Codex|ChatGPT)/i.test(n));
    } catch {
        return false;
    }
}

function ensureTomlConfig(label, configPath, nodeExe, indexJs, createIfMissing) {
    let raw = "";
    let hadBom = false;
    if (fs.existsSync(configPath)) {
        try {
            raw = fs.readFileSync(configPath, "utf8");
        } catch (err) {
            return `${label}: failed to read config (${err.message}): ${configPath}`;
        }
        hadBom = raw.charCodeAt(0) === 0xfeff;
        if (hadBom) raw = raw.slice(1);
    } else if (!createIfMissing) {
        return `${label}: not installed (no config file) — skipped`;
    }

    const eol = raw.includes("\r\n") ? "\r\n" : "\n";
    const { keep, own } = splitTomlBlock(raw);
    if (own.length && isTomlBlockValid(own) && !hadBom) {
        return `${label}: already configured correctly`;
    }

    while (keep.length && keep[keep.length - 1].trim() === "") keep.pop();
    const out = (keep.length ? keep.join(eol) + eol + eol : "") +
        buildTomlBlock(nodeExe, indexJs).join(eol) + eol;

    try {
        if (fs.existsSync(configPath)) {
            fs.copyFileSync(configPath, configPath + ".bimbot-backup");
        }
        fs.mkdirSync(path.dirname(configPath), { recursive: true });
        fs.writeFileSync(configPath, out);
    } catch (err) {
        return `${label}: failed to write config (${err.message}): ${configPath}`;
    }
    if (hadBom) return `${label}: rewrote config without UTF-8 BOM (breaks some parsers)`;
    return `${label}: ${own.length ? "repaired stale entry" : "added BIM-Bot entry"}`;
}

function removeTomlEntry(label, configPath) {
    if (!fs.existsSync(configPath)) {
        return `${label}: no config file — skipped`;
    }
    let raw;
    try {
        raw = fs.readFileSync(configPath, "utf8");
    } catch (err) {
        return `${label}: failed to read config (${err.message}): ${configPath}`;
    }
    if (raw.charCodeAt(0) === 0xfeff) raw = raw.slice(1);
    const eol = raw.includes("\r\n") ? "\r\n" : "\n";
    const { keep, own } = splitTomlBlock(raw);
    if (!own.length) return `${label}: no BIM-Bot entry — skipped`;
    while (keep.length && keep[keep.length - 1].trim() === "") keep.pop();
    try {
        fs.copyFileSync(configPath, configPath + ".bimbot-backup");
        fs.writeFileSync(configPath, keep.length ? keep.join(eol) + eol : "");
    } catch (err) {
        return `${label}: failed to write config (${err.message}): ${configPath}`;
    }
    return `${label}: removed BIM-Bot entry`;
}

function isCurrentHome(home) {
    const current = process.env.USERPROFILE || require("os").homedir();
    return home.toLowerCase() === current.toLowerCase();
}

/**
 * Configure every supported MCP client inside one user profile.
 * Returns the number of clients that ended up configured.
 */
function configureProfile(home, nodeExe, indexJs, { scanningAllProfiles = false } = {}) {
    const appData = path.join(home, "AppData", "Roaming");
    const localAppData = path.join(home, "AppData", "Local");
    let configured = 0;

    const report = (line) => {
        console.log(`  ${line}`);
        if (!/: (not installed|config is not valid JSON|failed to write config)/.test(line)) configured++;
    };

    // Claude Desktop. For the profile we were actually asked about, write the
    // config even when the app is not visible yet: %APPDATA%\Claude only appears
    // after Claude Desktop has run once, so gating on it silently skipped
    // everyone who installs BIM-Bot before Claude (the normal order on a fresh
    // laptop) and left them hand-editing JSON. Creating it early is harmless —
    // Claude reads it on first launch.
    //
    // When sweeping every profile on the machine, only touch profiles that
    // already show a Claude footprint; an installer has no business seeding
    // config folders in unrelated users' profiles. Those users are covered
    // anyway: the Revit plugin self-heals on startup, as themselves.
    const desktopConfig = path.join(appData, "Claude", "claude_desktop_config.json");
    const claudeSeen =
        fs.existsSync(path.join(appData, "Claude")) ||
        fs.existsSync(path.join(localAppData, "AnthropicClaude"));
    report(ensureConfig("Claude Desktop", desktopConfig, nodeExe, indexJs,
        scanningAllProfiles ? claudeSeen : true));

    // Claude Code — only modify ~/.claude.json if it already exists.
    report(ensureConfig("Claude Code", path.join(home, ".claude.json"),
        nodeExe, indexJs, false));

    // Cursor — same "mcpServers" schema as Claude. Create when ~/.cursor exists.
    const cursorDir = path.join(home, ".cursor");
    report(ensureConfig("Cursor", path.join(cursorDir, "mcp.json"),
        nodeExe, indexJs, fs.existsSync(cursorDir)));

    // Windsurf (Codeium) — also "mcpServers" schema.
    const codeiumDir = path.join(home, ".codeium");
    report(ensureConfig("Windsurf", path.join(codeiumDir, "windsurf", "mcp_config.json"),
        nodeExe, indexJs, fs.existsSync(codeiumDir)));

    // VS Code / Insiders — top-level "servers" key, each entry needs type "stdio".
    for (const [label, dirName] of [["VS Code", "Code"], ["VS Code Insiders", "Code - Insiders"]]) {
        const codeRoot = path.join(appData, dirName);
        report(ensureConfig(label, path.join(codeRoot, "User", "mcp.json"),
            nodeExe, indexJs, fs.existsSync(codeRoot), { serversKey: "servers", stdioType: true }));
    }

    // ChatGPT Desktop (and Codex CLI, which shares the file) — TOML config.
    // Created once ChatGPT/Codex shows up in the profile.
    const codexDir = codexHome(home, isCurrentHome(home));
    report(ensureTomlConfig("ChatGPT Desktop", path.join(codexDir, "config.toml"),
        nodeExe, indexJs, chatGptSeen(home, codexDir)));

    return configured;
}

/**
 * Remove the BIM-Bot entry from one config file. Returns a status string.
 * Never deletes the file itself and never touches other servers.
 */
function removeEntry(label, configPath, opts = {}) {
    const serversKey = opts.serversKey || "mcpServers";
    if (!fs.existsSync(configPath)) {
        return `${label}: no config file — skipped`;
    }
    let config;
    try {
        const raw = fs.readFileSync(configPath, "utf8");
        config = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
    } catch (err) {
        return `${label}: config is not valid JSON (${err.message}) — left untouched: ${configPath}`;
    }
    const servers = config[serversKey];
    if (!servers || typeof servers !== "object" || !(SERVER_KEY in servers)) {
        return `${label}: no BIM-Bot entry — skipped`;
    }
    delete servers[SERVER_KEY];
    try {
        fs.copyFileSync(configPath, configPath + ".bimbot-backup");
        fs.writeFileSync(configPath, JSON.stringify(config, null, 2));
    } catch (err) {
        return `${label}: failed to write config (${err.message}): ${configPath}`;
    }
    return `${label}: removed BIM-Bot entry`;
}

/**
 * Remove the BIM-Bot entry from every supported MCP client in one profile.
 * Returns the number of files changed.
 */
function removeFromProfile(home) {
    const appData = path.join(home, "AppData", "Roaming");
    const localAppData = path.join(home, "AppData", "Local");
    let removed = 0;

    const report = (line) => {
        console.log(`  ${line}`);
        if (/: removed BIM-Bot entry/.test(line)) removed++;
    };

    report(removeEntry("Claude Desktop",
        path.join(appData, "Claude", "claude_desktop_config.json")));
    // MS-Store Claude keeps its config under Packages\...\LocalCache\Roaming.
    try {
        const packages = path.join(localAppData, "..", "Local", "Packages");
        if (fs.existsSync(packages)) {
            for (const name of fs.readdirSync(packages)) {
                if (/^(Claude_|AnthropicClaude)/i.test(name)) {
                    report(removeEntry("Claude Desktop (MS Store)",
                        path.join(packages, name, "LocalCache", "Roaming",
                            "Claude", "claude_desktop_config.json")));
                }
            }
        }
    } catch { /* non-fatal */ }
    report(removeEntry("Claude Code", path.join(home, ".claude.json")));
    report(removeEntry("Cursor", path.join(home, ".cursor", "mcp.json")));
    report(removeEntry("Windsurf",
        path.join(home, ".codeium", "windsurf", "mcp_config.json")));
    // Gemini CLI uses the same schema.
    report(removeEntry("Gemini CLI", path.join(home, ".gemini", "settings.json")));
    for (const [label, dirName] of [["VS Code", "Code"], ["VS Code Insiders", "Code - Insiders"]]) {
        report(removeEntry(label,
            path.join(appData, dirName, "User", "mcp.json"),
            { serversKey: "servers" }));
    }
    report(removeTomlEntry("ChatGPT Desktop",
        path.join(codexHome(home, isCurrentHome(home)), "config.toml")));
    return removed;
}

/**
 * Every real user profile on the machine. Built-in/service profiles have no
 * AppData\Roaming (or are explicitly excluded), so they are filtered out.
 */
function enumerateUserProfiles() {
    const currentHome = process.env.USERPROFILE || require("os").homedir();
    const usersRoot = path.dirname(currentHome);
    const skip = new Set(["public", "default", "default user", "all users",
        "defaultapppool", "administrator"]);

    let entries = [];
    try {
        entries = fs.readdirSync(usersRoot, { withFileTypes: true });
    } catch {
        return [currentHome];
    }

    const profiles = entries
        .filter((e) => e.isDirectory() && !skip.has(e.name.toLowerCase()))
        .map((e) => path.join(usersRoot, e.name))
        .filter((p) => fs.existsSync(path.join(p, "AppData", "Roaming")));

    // Always include the current profile, even if it lives elsewhere.
    if (!profiles.some((p) => p.toLowerCase() === currentHome.toLowerCase())) {
        profiles.push(currentHome);
    }
    return profiles;
}

function main() {
    const opts = parseArgs();
    if (opts.remove) {
        // Uninstall path: no node/server resolution needed.
        const profiles = opts.home
            ? [opts.home]
            : opts.allUsers
                ? enumerateUserProfiles()
                : [process.env.USERPROFILE || require("os").homedir()];

        let totalRemoved = 0;
        for (const home of profiles) {
            console.log(`Profile: ${home}`);
            totalRemoved += removeFromProfile(home);
        }
        console.log(totalRemoved === 0
            ? "\nNo BIM-Bot entries found — nothing to remove."
            : `\nDone — removed ${totalRemoved} BIM-Bot entr${totalRemoved === 1 ? "y" : "ies"}.`);
        return;
    }
    const { nodeExe, indexJs } = resolvePaths();
    console.log(`BIM-Bot MCP setup\n  node:   ${nodeExe}\n  server: ${indexJs}\n`);

    // --all-users exists because an elevated installer runs under whichever
    // account answered the UAC prompt. Configuring only that profile silently
    // misses the person who is actually installing BIM-Bot.
    const profiles = opts.home
        ? [opts.home]
        : opts.allUsers
            ? enumerateUserProfiles()
            : [process.env.USERPROFILE || require("os").homedir()];

    let totalConfigured = 0;
    for (const home of profiles) {
        console.log(`Profile: ${home}`);
        totalConfigured += configureProfile(home, nodeExe, indexJs, {
            scanningAllProfiles: !!opts.allUsers && !opts.home,
        });
    }

    if (totalConfigured === 0) {
        console.log("\nNo MCP client found to configure. Install Claude Desktop, then open Revit " +
            "and click \"Connect Claude\" on the BIM-Bot ribbon (or re-run this script).");
    } else {
        console.log(`\nDone — ${totalConfigured} client config(s) in place. ` +
            "Fully quit and reopen your MCP client (Claude, ChatGPT, Cursor, Windsurf, VS Code) for changes to take effect.");
    }
}

main();
