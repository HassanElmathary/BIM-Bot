#!/usr/bin/env node
/**
 * configure-claude.cjs — register (or repair) the BIM-Bot MCP server in
 * Claude Desktop and Claude Code configs.
 *
 * Unlike a naive "skip if BIM-Bot exists" check, this validates that the
 * configured node/server paths actually exist and repairs stale entries
 * (e.g. after the install folder or repo moved). Safe to run repeatedly.
 *
 * Usage:
 *   node configure-claude.cjs [--node <path-to-node.exe>] [--server <path-to-index.js>]
 *                             [--home <profile-dir>] [--all-users]
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
    }
    return out;
}

function resolvePaths() {
    const opts = parseArgs();
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
    if (fs.existsSync(configPath)) {
        try {
            // Strip UTF-8 BOM — some editors/tools write one and it breaks JSON.parse
            const raw = fs.readFileSync(configPath, "utf8");
            hadBom = raw.charCodeAt(0) === 0xfeff;
            config = JSON.parse(hadBom ? raw.slice(1) : raw);
        } catch (err) {
            return `${label}: config is not valid JSON (${err.message}) — left untouched: ${configPath}`;
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

    if (hadBom) return `${label}: rewrote config without UTF-8 BOM (breaks some parsers)`;
    return `${label}: ${wasStale ? "repaired stale entry" : "added BIM-Bot entry"}`;
}

/**
 * Configure every supported MCP client inside one user profile.
 * Returns the number of clients that ended up configured.
 */
function configureProfile(home, nodeExe, indexJs) {
    const appData = path.join(home, "AppData", "Roaming");
    const localAppData = path.join(home, "AppData", "Local");
    let configured = 0;

    const report = (line) => {
        console.log(`  ${line}`);
        if (!/: (not installed|config is not valid JSON|failed to write config)/.test(line)) configured++;
    };

    // Claude Desktop — create config if the app appears installed (or the
    // config dir already exists); otherwise skip quietly.
    const desktopConfig = path.join(appData, "Claude", "claude_desktop_config.json");
    const desktopInstalled =
        fs.existsSync(path.join(appData, "Claude")) ||
        fs.existsSync(path.join(localAppData, "AnthropicClaude"));
    report(ensureConfig("Claude Desktop", desktopConfig, nodeExe, indexJs, desktopInstalled));

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

    return configured;
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
        totalConfigured += configureProfile(home, nodeExe, indexJs);
    }

    if (totalConfigured === 0) {
        console.log("\nNo MCP client found to configure. Install Claude Desktop, then open Revit " +
            "and click \"Connect Claude\" on the BIM-Bot ribbon (or re-run this script).");
    } else {
        console.log(`\nDone — ${totalConfigured} client config(s) in place. ` +
            "Fully quit and reopen your MCP client (Claude, Cursor, Windsurf, VS Code) for changes to take effect.");
    }
}

main();
