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
 *
 * With no args, paths are derived from this script's location:
 *   installed layout:  {app}\server\scripts\configure-claude.cjs
 *                      → {app}\server\build\index.js, {app}\nodejs\node.exe
 *   dev layout:        {repo}\revit-mcp-server\scripts\configure-claude.cjs
 *                      → {repo}\revit-mcp-server\build\index.js, process.execPath
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
function ensureConfig(label, configPath, nodeExe, indexJs, createIfMissing) {
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

    if (!config.mcpServers || typeof config.mcpServers !== "object") {
        config.mcpServers = {};
    }

    if (isEntryValid(config.mcpServers[SERVER_KEY]) && !hadBom) {
        return `${label}: already configured correctly`;
    }

    const wasStale = !!config.mcpServers[SERVER_KEY];
    config.mcpServers[SERVER_KEY] = {
        command: nodeExe,
        args: [indexJs],
        env: {},
    };

    if (fs.existsSync(configPath)) {
        fs.copyFileSync(configPath, configPath + ".bimbot-backup");
    }
    fs.mkdirSync(path.dirname(configPath), { recursive: true });
    fs.writeFileSync(configPath, JSON.stringify(config, null, 2));

    if (hadBom) return `${label}: rewrote config without UTF-8 BOM (breaks some parsers)`;
    return `${label}: ${wasStale ? "repaired stale entry" : "added BIM-Bot entry"}`;
}

function main() {
    const { nodeExe, indexJs } = resolvePaths();
    console.log(`BIM-Bot MCP setup\n  node:   ${nodeExe}\n  server: ${indexJs}\n`);

    const appData = process.env.APPDATA || path.join(process.env.USERPROFILE, "AppData", "Roaming");
    const home = process.env.USERPROFILE || require("os").homedir();

    // Claude Desktop — create config if the app appears installed (or the
    // config dir already exists); otherwise skip quietly.
    const desktopConfig = path.join(appData, "Claude", "claude_desktop_config.json");
    const desktopInstalled =
        fs.existsSync(path.join(appData, "Claude")) ||
        fs.existsSync(path.join(process.env.LOCALAPPDATA || "", "AnthropicClaude"));
    console.log(ensureConfig("Claude Desktop", desktopConfig, nodeExe, indexJs, desktopInstalled));

    // Claude Code — only modify ~/.claude.json if it already exists.
    const claudeCodeConfig = path.join(home, ".claude.json");
    console.log(ensureConfig("Claude Code", claudeCodeConfig, nodeExe, indexJs, false));

    console.log("\nDone. Restart Claude for changes to take effect.");
}

main();
