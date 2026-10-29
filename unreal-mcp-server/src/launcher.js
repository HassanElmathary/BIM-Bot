/**
 * Entry point for the standalone BIM-Bot-Unreal.exe
 *
 *   BIM-Bot-Unreal.exe              double-clicked → register with Claude + OpenCode,
 *                                   then offer to set up the Unreal projects found
 *                                   launched by an MCP client (piped stdio) → serve MCP
 *   BIM-Bot-Unreal.exe --install    register with Claude Desktop, Claude Code, OpenCode
 *                                   and write the tool permissions (see permissions.js)
 *   BIM-Bot-Unreal.exe --setup [project.uproject | folder ...]
 *                                   set up those Unreal projects (default: all found)
 *                                   without asking — see unreal-setup.js
 *   BIM-Bot-Unreal.exe --setup-engines
 *                                   turn the plugins on by default in every Unreal
 *                                   engine install, so any project connects (asks for
 *                                   admin once; re-run after an engine update)
 *   BIM-Bot-Unreal.exe --uninstall  remove those registrations and permissions
 *   BIM-Bot-Unreal.exe --status     show Unreal versions, projects and registrations
 *   BIM-Bot-Unreal.exe --serve      force MCP stdio server mode
 *
 * Dragging a .uproject onto the exe installs and sets up that project.
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const readline = require('readline');
const { POLICY, claudeRules, opencodeRules } = require('./permissions.js');
const { findProjects, setupProject, projectStatus, setupEngine, engineStatus } = require('./unreal-setup.js');

const SERVER_NAME = 'BIM-Bot-Unreal';
// Names this server was registered under before the rename
const LEGACY_NAMES = ['unreal-mcp'];
const ENV = {
  UNREAL_HOST: process.env.UNREAL_HOST || 'localhost',
  UNREAL_PORT: process.env.UNREAL_PORT || '30010',
  UNREAL_BRIDGE_PORT: process.env.UNREAL_BRIDGE_PORT || '30011',
};

const HOME = os.homedir();
const APPDATA = process.env.APPDATA || path.join(HOME, 'AppData', 'Roaming');
const LOCALAPPDATA = process.env.LOCALAPPDATA || path.join(HOME, 'AppData', 'Local');

// Installed copy lives outside OneDrive so clients never launch a cloud-only file
const INSTALL_EXE = path.join(LOCALAPPDATA, 'Programs', 'BIM-Bot Unreal', 'BIM-Bot-Unreal.exe');

// ─── JSON config helpers ──────────────────────────────────────────────────────

function readJson(file) {
  if (!fs.existsSync(file)) return {};
  const text = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
  return text.trim() ? JSON.parse(text) : {};
}

function writeJson(file, data) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  if (fs.existsSync(file)) fs.copyFileSync(file, file + '.bak');
  const tmp = file + '.tmp';
  fs.writeFileSync(tmp, JSON.stringify(data, null, 2) + '\n', 'utf8');
  fs.renameSync(tmp, file);
}

function isLegacyEntry(entry) {
  const cmd = [entry && entry.command, ...((entry && entry.args) || [])].flat().join(' ');
  return /Unreal MCP[\\/]+src[\\/]+index\.js|UnrealMCP[\\/]+unreal-mcp\.exe/i.test(cmd);
}

// ─── Permission writers ───────────────────────────────────────────────────────

const OUR_NAMES = [SERVER_NAME, ...LEGACY_NAMES];

// Claude Code: permissions.{allow,ask,deny} in ~/.claude/settings.json
function claudePermissions(remove) {
  const file = path.join(HOME, '.claude', 'settings.json');
  const ours = (r) => OUR_NAMES.some((n) => r.startsWith(`mcp__${n}__`));
  const cfg = readJson(file);
  const perms = cfg.permissions = cfg.permissions || {};
  const rules = claudeRules(SERVER_NAME);
  for (const level of ['allow', 'ask', 'deny']) {
    const kept = (perms[level] || []).filter((r) => !ours(r));
    perms[level] = remove ? kept : [...kept, ...rules[level]];
    if (!perms[level].length) delete perms[level];
  }
  writeJson(file, cfg);
  return file;
}

// OpenCode: "permission": { "<server>_<tool>": "allow" | "ask" | "deny" } in opencode.json
function opencodePermissions(file, remove) {
  const ours = (k) => OUR_NAMES.some((n) => k.startsWith(`${n}_`));
  const cfg = readJson(file);
  let perm = cfg.permission;
  if (typeof perm === 'string') perm = { '*': perm };
  perm = Object.fromEntries(Object.entries(perm || {}).filter(([k]) => !ours(k)));
  if (!remove) Object.assign(perm, opencodeRules(SERVER_NAME));
  if (Object.keys(perm).length) cfg.permission = perm; else delete cfg.permission;
  writeJson(file, cfg);
  return file;
}

// ─── Clients ──────────────────────────────────────────────────────────────────

function clients(exePath) {
  return [
    {
      name: 'Claude Desktop',
      file: path.join(APPDATA, 'Claude', 'claude_desktop_config.json'),
      // Only touch Claude Desktop if it is installed (its config folder exists)
      present: (file) => fs.existsSync(path.dirname(file)),
      key: 'mcpServers',
      entry: { command: exePath, args: [], env: ENV },
    },
    {
      name: 'Claude Code',
      file: path.join(HOME, '.claude.json'),
      present: (file) => fs.existsSync(file) || fs.existsSync(path.join(HOME, '.claude')),
      key: 'mcpServers',
      entry: { type: 'stdio', command: exePath, args: [], env: ENV },
      permissions: (remove) => claudePermissions(remove),
    },
    {
      name: 'OpenCode',
      file: path.join(process.env.XDG_CONFIG_HOME || path.join(HOME, '.config'), 'opencode', 'opencode.json'),
      present: (file) => fs.existsSync(path.dirname(file)),
      key: 'mcp',
      entry: { type: 'local', command: [exePath], enabled: true, environment: ENV },
      permissions(remove) { return opencodePermissions(this.file, remove); },
    },
  ];
}

function copyToInstallDir(exePath) {
  if (path.resolve(exePath).toLowerCase() === INSTALL_EXE.toLowerCase()) return INSTALL_EXE;
  fs.mkdirSync(path.dirname(INSTALL_EXE), { recursive: true });
  try {
    fs.copyFileSync(exePath, INSTALL_EXE);
  } catch (e) {
    // Busy when a client is running the server right now — keep the existing copy
    if (!fs.existsSync(INSTALL_EXE)) throw e;
    console.log(`  ! could not update ${INSTALL_EXE} (${e.code}); close Claude/OpenCode and run again to update it`);
  }
  console.log(`  > installed to ${INSTALL_EXE}`);
  return INSTALL_EXE;
}

function install(sourceExe) {
  const exePath = copyToInstallDir(sourceExe);
  for (const c of clients(exePath)) {
    if (!c.present(c.file)) {
      console.log(`  - ${c.name}: not installed, skipped`);
      continue;
    }
    try {
      const cfg = readJson(c.file);
      cfg[c.key] = cfg[c.key] || {};
      // Stale entries pointing at the old node script are replaced by the exe entry
      for (const [name, entry] of Object.entries(cfg[c.key])) {
        if (name !== SERVER_NAME && (LEGACY_NAMES.includes(name) || isLegacyEntry(entry))) {
          delete cfg[c.key][name];
        }
      }
      cfg[c.key][SERVER_NAME] = c.entry;
      writeJson(c.file, cfg);
      console.log(`  + ${c.name}: registered  (${c.file})`);
      if (c.permissions) {
        const f = c.permissions(false);
        console.log(`    permissions: ${POLICY.allow.length} allow, ${POLICY.ask.length} ask, ${POLICY.deny.length} deny  (${f})`);
      } else {
        console.log('    permissions: approve each tool in the app (no config-file rules)');
      }
    } catch (e) {
      console.log(`  ! ${c.name}: FAILED — ${e.message}`);
    }
  }
}

function uninstall(exePath) {
  for (const c of clients(exePath)) {
    try {
      const cfg = readJson(c.file);
      const names = OUR_NAMES.filter((n) => cfg[c.key] && cfg[c.key][n]);
      if (names.length) {
        for (const n of names) delete cfg[c.key][n];
        writeJson(c.file, cfg);
        console.log(`  - ${c.name}: removed`);
      } else {
        console.log(`  . ${c.name}: not registered`);
      }
      if (c.permissions && c.present(c.file)) c.permissions(true);
    } catch (e) {
      console.log(`  ! ${c.name}: FAILED — ${e.message}`);
    }
  }
}

function status() {
  for (const c of clients(INSTALL_EXE)) {
    let state = 'not registered';
    try {
      const entry = (readJson(c.file)[c.key] || {})[SERVER_NAME];
      if (entry) {
        const cmd = Array.isArray(entry.command) ? entry.command[0] : entry.command;
        state = `registered → ${cmd}`;
      }
    } catch (e) {
      state = `unreadable config (${e.message})`;
    }
    console.log(`  ${c.name.padEnd(15)} ${state}`);
  }
}

// ─── Unreal Engine detection ──────────────────────────────────────────────────

function detectUnreal() {
  const found = new Map();
  const add = (dir) => {
    const file = path.join(dir, 'Engine', 'Build', 'Build.version');
    try {
      const v = JSON.parse(fs.readFileSync(file, 'utf8'));
      found.set(path.resolve(dir).toLowerCase(), { dir, version: `${v.MajorVersion}.${v.MinorVersion}.${v.PatchVersion}` });
    } catch { /* not an engine folder */ }
  };

  const programData = process.env.ProgramData || 'C:\\ProgramData';
  try {
    const dat = readJson(path.join(programData, 'Epic', 'UnrealEngineLauncher', 'LauncherInstalled.dat'));
    for (const item of dat.InstallationList || []) {
      if (/^UE_\d/.test(item.AppName)) add(item.InstallLocation);
    }
  } catch { /* launcher not installed */ }

  const epicDir = path.join(process.env.ProgramFiles || 'C:\\Program Files', 'Epic Games');
  try {
    for (const d of fs.readdirSync(epicDir)) if (/^UE_\d/.test(d)) add(path.join(epicDir, d));
  } catch { /* none */ }

  return [...found.values()].sort((a, b) => b.version.localeCompare(a.version, undefined, { numeric: true }));
}

function printUnreal() {
  const engines = detectUnreal();
  if (!engines.length) {
    console.log('  (no Unreal Engine installs found)');
    return;
  }
  for (const e of engines) {
    const missing = engineStatus(e.dir, { port: ENV.UNREAL_PORT });
    console.log(`  UE ${e.version.padEnd(8)} ${missing.length ? 'needs setup' : 'ready      '}  ${e.dir}`);
  }
}

/** Relaunch this program elevated (one UAC prompt) with the given flags and wait for it */
function runElevated(flags) {
  const { execFileSync } = require('child_process');
  let isSea = false;
  try { isSea = require('node:sea').isSea(); } catch { /* older Node */ }
  // As a single-file exe the program is execPath itself; under node it is the script
  const argList = [...(isSea ? [] : [process.argv[1]]), ...flags];
  const quote = (s) => `'${String(s).replace(/'/g, "''")}'`;
  const ps = `$p = Start-Process -FilePath ${quote(process.execPath)} -ArgumentList ${argList.map((a) => quote(`"${a}"`)).join(',')} ` +
    '-Verb RunAs -Wait -PassThru -WindowStyle Hidden; exit $p.ExitCode';
  try {
    execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', ps], { stdio: 'ignore' });
    return true;
  } catch {
    return false; // UAC declined, or the elevated run failed
  }
}

/**
 * Turn the plugins on for every project of each engine install. Engines live in
 * Program Files, so when that is refused it asks for admin once, unless this run
 * already is the elevated one.
 */
function setupEngines(elevated) {
  const engines = detectUnreal();
  if (!engines.length) {
    console.log('  (no Unreal Engine installs found)');
    return true;
  }
  const denied = [];
  for (const e of engines) {
    try {
      const changes = setupEngine(e.dir, { port: ENV.UNREAL_PORT });
      console.log(`  + UE ${e.version}: ${changes.length ? changes.join(', ') : 'already set up'}`);
    } catch (err) {
      if (['EPERM', 'EACCES'].includes(err.code) && !elevated) denied.push(e);
      else console.log(`  ! UE ${e.version}: FAILED — ${err.message}`);
    }
  }
  if (denied.length) {
    console.log('  Asking for administrator rights to change the engine install...');
    runElevated(['--setup-engines', '--elevated']);
    for (const e of denied) {
      const missing = engineStatus(e.dir, { port: ENV.UNREAL_PORT });
      console.log(missing.length
        ? `  ! UE ${e.version}: not set up (admin rights declined?) — missing ${missing.join(', ')}`
        : `  + UE ${e.version}: plugins on by default for every project`);
    }
  }
  return engines.every((e) => !engineStatus(e.dir, { port: ENV.UNREAL_PORT }).length);
}

// ─── Unreal projects ──────────────────────────────────────────────────────────

function printProjects(projects) {
  if (!projects.length) {
    console.log('  (no projects found in Documents\\Unreal Projects or on the Desktop)');
    return;
  }
  projects.forEach((p, i) => {
    const missing = projectStatus(p);
    console.log(`  ${String(i + 1).padStart(2)}. ${missing.length ? 'needs setup' : 'ready      '}  ${p}`);
  });
}

function setupProjects(projects) {
  for (const p of projects) {
    try {
      const changes = setupProject(p, { port: ENV.UNREAL_PORT });
      console.log(`  + ${path.basename(p)}: ${changes.length ? changes.join(', ') : 'already set up'}`);
    } catch (e) {
      console.log(`  ! ${path.basename(p)}: FAILED — ${e.message}`);
    }
  }
}

function ask(question) {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve) => rl.question(question, (a) => { rl.close(); resolve(a.trim()); }));
}

/** Double-click flow: list projects, let the user pick which to set up */
async function chooseAndSetup(projects) {
  printProjects(projects);
  const pending = projects.filter((p) => projectStatus(p).length);
  if (!pending.length || !process.stdin.isTTY) return;
  const answer = (await ask(
    `\nSet up ${pending.length === 1 ? 'this project' : `these ${pending.length} projects`}? ` +
    '[Enter = all, numbers like 1,3, or n = none] '
  )).toLowerCase();
  if (answer === 'n' || answer === 'no') return;
  const picked = answer
    ? answer.split(/[\s,]+/).map((n) => projects[parseInt(n, 10) - 1]).filter(Boolean)
    : pending;
  setupProjects(picked);
  console.log('  Restart the Unreal Editor for open projects to pick up the change.');
}

// ─── Main ─────────────────────────────────────────────────────────────────────

function waitForEnter() {
  if (!process.stdin.isTTY) return Promise.resolve();
  process.stdout.write('\nPress Enter to close...');
  return new Promise((resolve) => {
    process.stdin.resume();
    process.stdin.once('data', () => resolve());
  });
}

async function run() {
  const exePath = process.execPath;
  const args = process.argv.slice(2);
  const flag = args.find((a) => a.startsWith('--')) || '';
  // Projects or folders passed on the command line (or a .uproject dropped on the exe)
  const paths = args.filter((a) => !a.startsWith('--'));

  // An MCP client launches us with piped stdio and no flags → serve
  const serve = flag === '--serve' || (!flag && !process.stdin.isTTY);
  if (serve) {
    const { main } = require('./index.js');
    return main();
  }

  // The elevated copy runElevated starts: no console to talk to, nobody to press Enter
  if (flag === '--setup-engines' && args.includes('--elevated')) {
    process.exit(setupEngines(true) ? 0 : 1);
  }

  console.log('BIM-Bot Unreal\n');
  console.log('Unreal Engine installs:');
  printUnreal();
  console.log('');

  if (flag === '--setup-engines') {
    console.log('Setting up Unreal engine installs:');
    setupEngines(false);
    console.log('  Restart the Unreal Editor for open projects to pick up the change.');
  } else if (flag === '--uninstall') {
    console.log('Removing MCP registrations:');
    uninstall(exePath);
  } else if (flag === '--status') {
    console.log('MCP registrations:');
    status();
    console.log('\nUnreal projects:');
    printProjects(findProjects(paths));
  } else if (flag === '--setup') {
    console.log('Setting up Unreal projects:');
    const projects = findProjects(paths);
    if (projects.length) setupProjects(projects); else console.log('  (no projects found)');
  } else {
    console.log('Registering MCP server:');
    install(exePath);
    if (paths.length) {
      console.log('\nUnreal projects:');
      setupProjects(findProjects(paths));
    } else if (flag === '--install') {
      // Run by the installer, which sets up the engines itself (--setup-engines, elevated)
      console.log('\nUnreal projects:');
      await chooseAndSetup(findProjects());
    } else {
      // Double-clicked: once the engines are set up every project connects
      console.log('\nUnreal engine installs (plugins on for every project):');
      if (!setupEngines(false)) {
        console.log('\nUnreal projects:');
        await chooseAndSetup(findProjects());
      }
    }
    console.log('\nRestart Claude Desktop / Claude Code / OpenCode to pick up the server.');
    console.log('Other projects: run "BIM-Bot-Unreal.exe --setup <project.uproject>" or drop the .uproject on the exe.');
  }

  await waitForEnter();
  process.exit(0);
}

run().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
