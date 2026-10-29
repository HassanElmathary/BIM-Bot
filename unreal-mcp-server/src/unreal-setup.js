/**
 * Prepares Unreal projects for BIM-Bot Unreal, so no manual plugin setup is needed:
 *
 *   - enables the stock Remote Control API, Python Editor Script and Editor
 *     Scripting Utilities plugins in the .uproject
 *   - writes Config/DefaultRemoteControl.ini so the Remote Control web server
 *     starts with the editor and accepts the server's Python calls
 *
 * The same can be done once per engine install (setupEngine), which turns the plugins
 * on by default so every project of that engine connects without its own setup.
 *
 * Every file is backed up to *.bak before it is changed.
 */

const fs = require('fs');
const os = require('os');
const path = require('path');

const PLUGINS = ['RemoteControl', 'PythonScriptPlugin', 'EditorScriptingUtilities'];

const RC_SECTION = '/Script/RemoteControlCommon.RemoteControlSettings';
const PYTHON_ALLOW_RULE = '(ClassPath="/Script/PythonScriptPlugin.PythonScriptLibrary",bAllowChildClasses=False)';

function rcKeys(port) {
  return {
    bAutoStartWebServer: 'True',
    RemoteControlHttpServerPort: String(port),
    // Both are off by default: without them /remote/object/call on PythonScriptLibrary is refused
    bEnableRemotePythonExecution: 'True',
    '+CustomAllowedRemoteFunctionCalls': PYTHON_ALLOW_RULE,
    // The HTTP server already listens on 127.0.0.1 only; the WebSocket server defaults to
    // 0.0.0.0, which would let other machines run Python once it is allowed
    RemoteControlWebsocketServerBindAddress: '127.0.0.1',
  };
}

// ─── File helpers ─────────────────────────────────────────────────────────────

function backupAndWrite(file, text) {
  if (fs.existsSync(file)) {
    if (fs.readFileSync(file, 'utf8') === text) return false;
    fs.copyFileSync(file, file + '.bak');
  }
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, 'utf8');
  return true;
}

/**
 * Set keys inside one [section] of an .ini text, adding the section if missing.
 * A key starting with "+" is an array entry: the line is added unless it is already there.
 */
function setIniKeys(text, section, keys) {
  const eol = text.includes('\r\n') ? '\r\n' : '\n';
  const lines = text ? text.split(/\r?\n/) : [];
  const header = `[${section}]`;
  let start = lines.findIndex((l) => l.trim() === header);
  if (start === -1) {
    if (lines.length && lines[lines.length - 1].trim() !== '') lines.push('');
    lines.push(header);
    start = lines.length - 1;
  }
  let end = lines.findIndex((l, i) => i > start && /^\s*\[/.test(l));
  if (end === -1) end = lines.length;

  for (const [key, value] of Object.entries(keys)) {
    const isArray = key.startsWith('+');
    const re = isArray
      ? new RegExp(`^\\s*${escapeRe(`${key}=${value}`)}\\s*$`, 'i')
      : new RegExp(`^\\s*[+\\-.!]?${key}\\s*=`, 'i');
    const idx = lines.slice(start + 1, end).findIndex((l) => re.test(l));
    if (isArray && idx !== -1) continue;
    if (idx === -1) {
      // Insert after the last non-blank line of the section
      let at = end;
      while (at > start + 1 && lines[at - 1].trim() === '') at--;
      lines.splice(at, 0, `${key}=${value}`);
      end++;
    } else {
      lines[start + 1 + idx] = `${key}=${value}`;
    }
  }
  let out = lines.join(eol);
  if (!out.endsWith(eol)) out += eol;
  return out;
}

function escapeRe(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function readIniValue(text, section, key) {
  const lines = text.split(/\r?\n/);
  const start = lines.findIndex((l) => l.trim() === `[${section}]`);
  if (start === -1) return undefined;
  for (let i = start + 1; i < lines.length && !/^\s*\[/.test(lines[i]); i++) {
    const m = lines[i].match(new RegExp(`^\\s*${key}\\s*=\\s*(.*)$`, 'i'));
    if (m) return m[1].trim();
  }
  return undefined;
}

// ─── Project discovery ────────────────────────────────────────────────────────

/** <home>/<name> plus the OneDrive copy of it, which Windows uses when folder backup is on */
function userDirs(name) {
  const dirs = [path.join(os.homedir(), name)];
  for (const env of ['OneDrive', 'OneDriveConsumer', 'OneDriveCommercial']) {
    if (process.env[env]) dirs.push(path.join(process.env[env], name));
  }
  return [...new Set(dirs.map((d) => path.resolve(d)))];
}

/** Where projects are looked for when no path is given */
function defaultRoots() {
  return [
    ...userDirs('Documents').map((d) => path.join(d, 'Unreal Projects')),
    // Projects are often kept on the Desktop, e.g. Desktop/Unreal Engine Projects/MyProject
    ...userDirs('Desktop'),
  ];
}

/**
 * .uproject files (2 levels deep) under the given files/folders, or under
 * <Documents>/Unreal Projects and the Desktop when none are given
 */
function findProjects(paths = []) {
  const roots = paths.length ? paths : defaultRoots();
  const found = new Map();
  const scan = (dir, depth) => {
    let entries;
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
    for (const e of entries) {
      const full = path.join(dir, e.name);
      if (e.isFile() && e.name.toLowerCase().endsWith('.uproject')) {
        found.set(path.resolve(full).toLowerCase(), full);
      } else if (e.isDirectory() && depth > 0 && !/^(Binaries|Intermediate|Saved|DerivedDataCache|Content|Plugins|\..*)$/i.test(e.name)) {
        scan(full, depth - 1);
      }
    }
  };
  for (const r of roots) {
    if (r.toLowerCase().endsWith('.uproject')) {
      if (fs.existsSync(r)) found.set(path.resolve(r).toLowerCase(), r);
    } else {
      scan(r, 2);
    }
  }
  return [...found.values()].sort();
}

// ─── Setup / status ───────────────────────────────────────────────────────────

function readUproject(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
}

/** Enable the plugins and write the Remote Control config. Returns what changed. */
function setupProject(uprojectFile, { port = 30010 } = {}) {
  const changes = [];

  const proj = readUproject(uprojectFile);
  proj.Plugins = proj.Plugins || [];
  for (const name of PLUGINS) {
    const entry = proj.Plugins.find((p) => p.Name === name);
    if (!entry) {
      proj.Plugins.push({ Name: name, Enabled: true });
      changes.push(`enabled plugin ${name}`);
    } else if (!entry.Enabled) {
      entry.Enabled = true;
      changes.push(`enabled plugin ${name}`);
    }
  }
  const eol = fs.readFileSync(uprojectFile, 'utf8').includes('\r\n') ? '\r\n' : '\n';
  backupAndWrite(uprojectFile, JSON.stringify(proj, null, '\t').replace(/\n/g, eol) + eol);

  const ini = path.join(path.dirname(uprojectFile), 'Config', 'DefaultRemoteControl.ini');
  const before = fs.existsSync(ini) ? fs.readFileSync(ini, 'utf8') : '';
  if (backupAndWrite(ini, setIniKeys(before, RC_SECTION, rcKeys(port)))) {
    changes.push('Remote Control: web server auto-start, Python calls allowed from this PC only');
  }

  return changes;
}

/** Whether a project is ready: list of missing pieces (empty = ready) */
function projectStatus(uprojectFile) {
  const missing = [];
  let proj;
  try { proj = readUproject(uprojectFile); } catch (e) { return [`unreadable .uproject (${e.message})`]; }
  for (const name of PLUGINS) {
    const entry = (proj.Plugins || []).find((p) => p.Name === name);
    if (!entry || !entry.Enabled) missing.push(`plugin ${name}`);
  }
  const ini = path.join(path.dirname(uprojectFile), 'Config', 'DefaultRemoteControl.ini');
  const text = fs.existsSync(ini) ? fs.readFileSync(ini, 'utf8') : '';
  if (setIniKeys(text, RC_SECTION, rcKeys(readIniValue(text, RC_SECTION, 'RemoteControlHttpServerPort') || 30010)) !== text) {
    missing.push('Remote Control config');
  }
  return missing;
}

// ─── Engine-wide setup ────────────────────────────────────────────────────────
// Turns the plugins on by default for every project of an engine install, so any
// project connects without its own setup. Projects that list a plugin as disabled
// keep it off. Engine installs live in Program Files (admin), and an engine update
// or "Verify" restores the stock files, so this has to be re-run after one.

/** <Engine>/Plugins/.../<name>.uplugin, searched a few folders deep */
function findEnginePlugin(engineDir, name) {
  const want = `${name}.uplugin`.toLowerCase();
  const scan = (dir, depth) => {
    let entries;
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return null; }
    for (const e of entries) {
      if (e.isFile() && e.name.toLowerCase() === want) return path.join(dir, e.name);
    }
    if (depth === 0) return null;
    for (const e of entries) {
      if (!e.isDirectory() || /^(Binaries|Intermediate|Source|Content|Resources|Config|Shaders)$/i.test(e.name)) continue;
      const hit = scan(path.join(dir, e.name), depth - 1);
      if (hit) return hit;
    }
    return null;
  };
  return scan(path.join(engineDir, 'Engine', 'Plugins'), 3);
}

/** The .uplugin text with "EnabledByDefault": true, keeping the rest of its formatting */
function enableByDefault(text) {
  if (/"EnabledByDefault"\s*:\s*true/i.test(text)) return text;
  if (/"EnabledByDefault"\s*:/i.test(text)) return text.replace(/("EnabledByDefault"\s*:\s*)false/i, '$1true');
  const eol = text.includes('\r\n') ? '\r\n' : '\n';
  return text.replace(/^(﻿?\s*\{)/, `$1${eol}\t"EnabledByDefault": true,`);
}

/** Per-plugin files an engine needs changed: [{ name, file, text }] (text = wanted content) */
function enginePlan(engineDir, port) {
  const plan = [];
  for (const name of PLUGINS) {
    const file = findEnginePlugin(engineDir, name);
    if (!file) throw new Error(`plugin ${name} not found in ${engineDir}`);
    plan.push({ name, file, text: enableByDefault(fs.readFileSync(file, 'utf8')) });
    if (name === 'RemoteControl') {
      // The plugin's own Default ini is a config layer every project reads
      const ini = path.join(path.dirname(file), 'Config', 'DefaultRemoteControl.ini');
      const before = fs.existsSync(ini) ? fs.readFileSync(ini, 'utf8') : '';
      plan.push({ name: 'Remote Control config', file: ini, text: setIniKeys(before, RC_SECTION, rcKeys(port)) });
    }
  }
  return plan;
}

/** Enable the plugins for every project of this engine. Returns what changed. */
function setupEngine(engineDir, { port = 30010 } = {}) {
  return enginePlan(engineDir, port)
    .filter((step) => backupAndWrite(step.file, step.text))
    .map((step) => (step.file.endsWith('.ini') ? 'Remote Control: web server auto-start, Python calls allowed from this PC only' : `plugin ${step.name} on by default`));
}

/** Whether an engine is set up: list of missing pieces (empty = ready) */
function engineStatus(engineDir, { port = 30010 } = {}) {
  try {
    return enginePlan(engineDir, port)
      .filter((step) => (fs.existsSync(step.file) ? fs.readFileSync(step.file, 'utf8') : '') !== step.text)
      .map((step) => (step.file.endsWith('.ini') ? step.name : `plugin ${step.name}`));
  } catch (e) {
    return [e.message];
  }
}

module.exports = {
  findProjects, setupProject, projectStatus, setupEngine, engineStatus, enableByDefault,
  setIniKeys, PLUGINS, RC_SECTION,
};
