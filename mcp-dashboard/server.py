"""BIM-Bot MCP Dashboard.

A local web dashboard that shows which MCP servers are working in each AI
program (Claude Desktop, Claude Code, Cursor, Windsurf, Gemini, Antigravity,
OpenCode), lets you switch each one on or off per program, and repairs the
configuration problems it knows how to fix.

Standard library only. Run:  python mcp-dashboard/server.py  [--port 8765]
"""

import argparse
import glob
import socket
import json
import os
import queue
import re
import secrets
import shutil
import subprocess
import sys
import threading
import time
import webbrowser
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import updates

HOME = Path.home()
APPDATA = Path(os.environ.get("APPDATA", HOME / "AppData" / "Roaming"))
LOCALAPPDATA = Path(os.environ.get("LOCALAPPDATA", HOME / "AppData" / "Local"))

STATE_DIR = HOME / ".bimbot" / "mcp-dashboard"
STASH_FILE = STATE_DIR / "disabled.json"     # entries switched off in programs with no native "enabled" flag
PROBES_FILE = STATE_DIR / "probes.json"      # last test result per server
FILTER_FILE = STATE_DIR / "tool-filter.json"  # {program: {server: [switched-off tools]}}, read by proxy.py
SETTINGS_FILE = STATE_DIR / "settings.json"   # {"hidden": [servers not shown in the app]}
BACKUP_DIR = STATE_DIR / "backups"
LAST_SAVED_DIR = STATE_DIR / "last-saved"    # copy of what this app last wrote to each program config

# Stable runtime folder: the tool-filter proxy (one-folder) is extracted here
# from the bundled onefile exe so program configs can point at a path that
# survives the portable exe being moved or deleted.
CONTROL_DIR = LOCALAPPDATA / "BIMBot" / "control"
PROXY_DIR = CONTROL_DIR / "proxy"
PROXY_EXE = PROXY_DIR / "bimbot-mcp-proxy.exe"
VERSION_FILE = CONTROL_DIR / "version.txt"
PANEL_VERSION = "1.2.1"

BIMBOT_DIR = LOCALAPPDATA / "Programs" / "BIMBot"
BIMBOT_NODE = BIMBOT_DIR / "nodejs" / "node.exe"
BIMBOT_SERVER = BIMBOT_DIR / "server" / "build" / "index.js"
BIMBOT_DATA = LOCALAPPDATA / "BIMBot"
AUTH_TOKEN_FILE = BIMBOT_DATA / "auth.token"
NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)

HERE = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
TOKEN = secrets.token_urlsafe(24)
FILE_LOCK = threading.Lock()

PROBE_TIMEOUT_S = 60
SECRET_HINT = re.compile(r"key|token|secret|password|pass|auth", re.I)


# ---------------------------------------------------------------- programs

def _store_claude_configs():
    pattern = str(LOCALAPPDATA / "Packages" / "Claude_*" / "LocalCache" / "Roaming" / "Claude" / "claude_desktop_config.json")
    return glob.glob(pattern)


def program_defs():
    """Every AI program BIM-Bot knows how to register MCP servers with."""
    progs = [
        dict(id="claude-desktop", name="Claude Desktop", path=APPDATA / "Claude" / "claude_desktop_config.json",
             key="mcpServers", fmt="std", restart="Quit Claude Desktop from the tray and reopen it."),
        dict(id="claude-code", name="Claude Code", path=HOME / ".claude.json",
             key="mcpServers", fmt="std", restart="Start a new Claude Code session."),
        dict(id="cursor", name="Cursor", path=HOME / ".cursor" / "mcp.json",
             key="mcpServers", fmt="std", restart="Reload the Cursor window."),
        dict(id="windsurf", name="Windsurf", path=HOME / ".codeium" / "windsurf" / "mcp_config.json",
             key="mcpServers", fmt="std", restart="Refresh MCP servers in Windsurf."),
        dict(id="gemini", name="Gemini CLI", path=HOME / ".gemini" / "settings.json",
             key="mcpServers", fmt="std", restart="Restart the gemini CLI."),
        dict(id="antigravity", name="Antigravity", path=HOME / ".gemini" / "antigravity" / "mcp_config.json",
             key="mcpServers", fmt="std", restart="Refresh MCP servers in Antigravity."),
        dict(id="opencode", name="OpenCode", path=HOME / ".config" / "opencode" / "opencode.json",
             key="mcp", fmt="opencode", restart="Restart OpenCode."),
    ]
    for i, p in enumerate(_store_claude_configs()):
        progs.insert(1, dict(id=f"claude-desktop-store-{i}", name="Claude Desktop (Store)", path=Path(p),
                             key="mcpServers", fmt="std", restart="Quit Claude Desktop from the tray and reopen it."))
    for p in progs:
        p["installed"] = p["path"].exists() or p["path"].parent.exists()
    return progs


def get_program(pid):
    for p in program_defs():
        if p["id"] == pid:
            return p
    raise ValueError(f"Unknown program: {pid}")


# ---------------------------------------------------------------- json io

def read_json(path, default=None):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except FileNotFoundError:
        return {} if default is None else default


def read_config(prog):
    """(config, error): error says what is wrong when the file can't be read or isn't valid JSON."""
    try:
        return read_json(prog["path"]), None
    except ValueError as e:
        return {}, f"{prog['path'].name} is not valid JSON ({e})."
    except OSError as e:
        return {}, f"Could not read {prog['path']}: {e}"


def _backup_tag(path):
    path = Path(path)
    return re.sub(r"[^\w.-]", "_", str(path.relative_to(path.anchor)))


def write_json(path, data, backup=True):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if backup and path.exists():
        BACKUP_DIR.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
        shutil.copy2(path, BACKUP_DIR / f"{stamp}__{_backup_tag(path)}")
    tmp = path.with_suffix(path.suffix + ".tmp")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    os.replace(tmp, path)
    if backup:
        LAST_SAVED_DIR.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, LAST_SAVED_DIR / _backup_tag(path))


# ---------------------------------------------------------------- tool filter

def _bundled_proxy_dir():
    """Path to the proxy folder bundled inside the onefile window exe (frozen only)."""
    if not getattr(sys, "frozen", False):
        return None
    base = getattr(sys, "_MEIPASS", None)
    if not base:
        return None
    p = Path(base) / "proxy"
    return p if p.is_dir() else None


def _sha256(path):
    import hashlib
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _dir_copy(src, dst):
    """Copy folder src onto dst, staging to a sibling temp dir then renaming.

    Returns True on success, False if dst is locked (in use) — in that case
    the previous copy is left in place.
    """
    import tempfile
    stage = dst.parent / (dst.name + ".tmp-" + str(os.getpid()))
    if stage.exists():
        shutil.rmtree(stage, ignore_errors=True)
    try:
        shutil.copytree(src, stage)
    except OSError:
        shutil.rmtree(stage, ignore_errors=True)
        return False
    aside = dst.parent / (dst.name + ".old")
    if aside.exists():
        shutil.rmtree(aside, ignore_errors=True)
    if dst.exists():
        try:
            os.replace(dst, aside)
        except OSError:
            shutil.rmtree(stage, ignore_errors=True)
            return False   # locked — keep the previous copy
    try:
        os.replace(stage, dst)
    except OSError:
        try:
            os.replace(aside, dst)
        except OSError:
            pass
        return False
    shutil.rmtree(aside, ignore_errors=True)
    return True


def ensure_proxy():
    r"""Make sure a usable proxy folder lives in the stable control folder.

    The proxy is built one-folder (exe + _internal) so an MCP start never
    unpacks Python into %TEMP% and never leaves _MEI* folders behind when an
    AI program force-kills the server. Extracted to
    %LOCALAPPDATA%\BIMBot\control\proxy\ on first start; re-extracted only
    when the bundled exe hash differs. Left in place (and retried next start)
    if a running program has it locked.
    """
    if not getattr(sys, "frozen", False):
        return
    src = _bundled_proxy_dir()
    if src is None:
        raise RuntimeError(
            "BIMBot-Control.exe was built without the tool-filter proxy; "
            "reinstall the panel.")
    try:
        CONTROL_DIR.mkdir(parents=True, exist_ok=True)
        exe_src = src / "bimbot-mcp-proxy.exe"
        exe_installed = PROXY_DIR / "bimbot-mcp-proxy.exe"
        need_copy = True
        if exe_installed.exists() and exe_src.exists():
            try:
                need_copy = _sha256(exe_src) != _sha256(exe_installed)
            except OSError:
                need_copy = True
        if need_copy:
            _dir_copy(src, PROXY_DIR)
        VERSION_FILE.write_text(PANEL_VERSION, encoding="utf-8")
    except (OSError, PermissionError):
        # Locked, read-only, or otherwise unusable — keep the previous copy.
        pass


def _launcher_list():
    """Command that starts this app; with a `proxy` argument it runs proxy.py."""
    if getattr(sys, "frozen", False) or PROXY_EXE.exists():
        # Once the exe has installed the stable proxy, a run from source keeps
        # using it too, so program configs never end up pointing at a script
        # in a source checkout.
        return [str(PROXY_EXE)]
    py = Path(sys.executable)
    if py.name.lower() == "pythonw.exe":
        py = py.with_name("python.exe")   # MCP needs real stdio
    return [str(py), str(Path(__file__).resolve().parent / "app.py")]


def launcher():
    """Command that starts this app; with a `proxy` argument it runs proxy.py."""
    if getattr(sys, "frozen", False):
        src = _bundled_proxy_dir()
        if src is None:
            raise RuntimeError(
                "BIMBot-Control.exe was built without the tool-filter proxy; "
                "reinstall the panel.")
        ensure_proxy()
        if not PROXY_EXE.exists():
            raise RuntimeError(
                "The tool-filter proxy could not be installed to %s — it may be "
                "locked by another program. Close any AI program using BIM-Bot "
                "and try again." % PROXY_DIR)
        return [str(PROXY_EXE)]
    return _launcher_list()


def disabled_tools(pid, name):
    return list((read_json(FILTER_FILE, {}).get(pid) or {}).get(name) or [])


def unwrap(entry):
    """If the entry runs through the tool-filter proxy, show the real server command instead."""
    cmd = [entry["command"], *entry["args"]]
    if "proxy" in cmd and "--program" in cmd and "--" in cmd:
        inner = cmd[cmd.index("--") + 1:]
        entry.update(wrapper=cmd[:cmd.index("proxy")], command=inner[0] if inner else "", args=inner[1:])
    return entry


def wrap(entry, pid, name):
    """Route the entry through the proxy while any of its tools is switched off."""
    if entry["type"] != "stdio" or not disabled_tools(pid, name):
        return entry["command"], entry["args"]
    cmd = [*launcher(), "proxy", "--program", pid, "--server", name, "--", entry["command"], *entry["args"]]
    return cmd[0], cmd[1:]


# ---------------------------------------------------------------- entries

def normalize(raw, fmt):
    """Convert a program's native entry to {type, command, args, env, url, enabled, timeout}."""
    if fmt == "opencode":
        cmd = raw.get("command") or []
        if isinstance(cmd, str):
            cmd = [cmd]
        return unwrap(dict(
            type="http" if raw.get("type") == "remote" else "stdio",
            command=cmd[0] if cmd else "", args=list(cmd[1:]),
            env=dict(raw.get("environment") or {}), url=raw.get("url", ""),
            enabled=raw.get("enabled", True) is not False, timeout=raw.get("timeout"),
        ))
    is_http = raw.get("type") in ("http", "sse", "streamable-http") or ("url" in raw and "command" not in raw)
    return unwrap(dict(
        type="http" if is_http else "stdio",
        command=raw.get("command", ""), args=list(raw.get("args") or []),
        env=dict(raw.get("env") or {}), url=raw.get("url") or raw.get("serverUrl", ""),
        enabled=raw.get("disabled") is not True, timeout=raw.get("timeout"),
    ))


def denormalize(entry, fmt, pid, name, original=None):
    """Write a normalized entry back, keeping keys the dashboard doesn't manage."""
    entry = dict(entry)
    entry["command"], entry["args"] = wrap(entry, pid, name)
    out = dict(original or {})
    if fmt == "opencode":
        if entry["type"] == "http":
            out.update(type="remote", url=entry["url"])
            out.pop("command", None)
        else:
            out.update(type="local", command=[entry["command"], *entry["args"]])
            out.pop("url", None)
        out["environment"] = entry["env"]
        if entry.get("timeout"):
            out["timeout"] = int(entry["timeout"])
        out["enabled"] = entry.get("enabled", True)
        return out
    if entry["type"] == "http":
        out["url"] = entry["url"]
        for k in ("command", "args"):
            out.pop(k, None)
    else:
        out["command"] = entry["command"]
        out["args"] = entry["args"]
        out.pop("url", None)
    out["env"] = entry["env"]
    return out


def resolve_command(cmd):
    if not cmd:
        return None
    if os.path.isabs(cmd) or os.sep in cmd or "/" in cmd:
        return cmd if os.path.exists(cmd) else None
    return shutil.which(cmd)


def probe_key(entry):
    return json.dumps([entry["type"], entry["command"], entry["args"], entry["env"], entry["url"]], sort_keys=True)


def find_node():
    for c in (str(BIMBOT_NODE), shutil.which("node"), r"C:\Program Files\nodejs\node.exe"):
        if c and os.path.exists(c):
            return c
    return None


def looks_like_bimbot(name, entry):
    blob = (name + " " + " ".join(entry["args"])).lower()
    return "bim-bot" in blob or "bimbot" in blob or "revit-mcp" in blob


def diagnose(prog, name, entry):
    """Static checks. Returns (issues, fixes); each fix is something the dashboard can apply."""
    issues, fixes = [], []
    if entry["type"] == "http":
        if not entry["url"]:
            issues.append("No URL configured.")
        return issues, fixes

    if entry.get("wrapper"):
        wrapper = list(entry["wrapper"])
        missing = [p for p in wrapper if os.path.isabs(p) and not os.path.exists(p)]
        if missing:
            issues.append("The tool-filter launcher has moved: " + " ".join(wrapper))
            fixes.append(dict(id="rewrap", label="Point at this copy of MCP Control", safe=True))
        elif wrapper != _launcher_list():
            # Still works, so not part of "Fix all safe issues".
            issues.append("The tool-filter launcher is outdated: " + " ".join(wrapper))
            fixes.append(dict(id="rewrap", label="Re-wrap tool-filter proxy at this copy of MCP Control"))
    if not entry["command"]:
        issues.append("No command configured.")
    elif not resolve_command(entry["command"]):
        issues.append(f"Command not found: {entry['command']}")
        if os.path.basename(entry["command"]).lower() in ("node", "node.exe") and find_node():
            fixes.append(dict(id="resolve-node", label=f"Use Node at {find_node()}", safe=True))

    for a in entry["args"]:
        if re.search(r"\.(js|mjs|cjs|py|exe|dll)$", a, re.I) and (os.path.isabs(a) or ":" in a) and not os.path.exists(a):
            issues.append(f"File not found: {a}")
            if looks_like_bimbot(name, entry) and BIMBOT_SERVER.exists():
                fixes.append(dict(id="bimbot-path", label="Point at the installed BIM-Bot server", safe=True))
            break

    for k, v in entry["env"].items():
        if v in ("", None) or (isinstance(v, str) and re.fullmatch(r"<.*>|YOUR_.*|xxx+", v, re.I)):
            issues.append(f"Environment variable {k} is empty or a placeholder.")

    if prog["fmt"] == "opencode" and entry.get("timeout") and entry["timeout"] < 60000 and looks_like_bimbot(name, entry):
        fixes.append(dict(id="raise-timeout", label="Raise OpenCode start-up timeout to 60 s", safe=True))
    return issues, fixes


def find_duplicates(entries):
    """{name: first name} for switched-on entries that start exactly the same server as an earlier one."""
    seen, dups = {}, {}
    for name, entry, _ in entries:
        if not entry["enabled"]:
            continue
        key = probe_key(entry)
        if key in seen:
            dups[name] = seen[key]
        else:
            seen[key] = name
    return dups


def apply_fix(prog, name, fix_id):
    if fix_id == "disable-duplicate":
        set_enabled(prog, name, False)
        return

    def mutate(entry):
        if fix_id == "resolve-node":
            entry["command"] = find_node()
        elif fix_id == "bimbot-path":
            entry["command"] = str(BIMBOT_NODE) if BIMBOT_NODE.exists() else (find_node() or "node")
            entry["args"] = [str(BIMBOT_SERVER)]
        elif fix_id == "raise-timeout":
            entry["timeout"] = 60000
        elif fix_id == "rewrap":
            pass   # rewriting the entry re-wraps it with the current launcher
        else:
            raise ValueError(f"Unknown fix: {fix_id}")
        return entry
    update_entry(prog, name, mutate)


def fix_all_safe():
    """Apply every fix marked safe (path fixes, re-wrap, timeout) in every program. Returns how many."""
    done, failed = 0, []
    for prog in program_defs():
        if not prog["path"].exists() and not stash().get(prog["id"]):
            continue
        cfg, err = read_config(prog)
        if err:
            continue
        for name, entry, _ in list_program(prog):
            for fix in diagnose(prog, name, entry)[1]:
                if fix.get("safe"):
                    try:
                        apply_fix(prog, name, fix["id"])
                        done += 1
                    except (OSError, ValueError) as e:
                        failed.append(f"{prog['name']} / {name}: {e}")
                    break   # the entry changed; its other fixes are re-checked next time
    return dict(fixed=done, failed=failed)


# ---------------------------------------------------------------- backups

LAST_SAVED = "last-saved"


def _backup_file(prog, fname):
    return LAST_SAVED_DIR / _backup_tag(prog["path"]) if fname == LAST_SAVED else BACKUP_DIR / fname


def _valid_json(f):
    try:
        json.loads(f.read_text(encoding="utf-8-sig"))
        return True
    except (OSError, ValueError):
        return False


def list_backups(prog, limit=40):
    """Backups of one program's config file: what this app last saved first, then newest-first copies
    taken before each change."""
    out = []
    last = _backup_file(prog, LAST_SAVED)
    if last.exists():
        at = datetime.fromtimestamp(last.stat().st_mtime).isoformat(sep=" ", timespec="seconds")
        out.append(dict(file=LAST_SAVED, at=at + " (last saved by this app)", size=last.stat().st_size,
                        valid=_valid_json(last)))
    if not BACKUP_DIR.is_dir():
        return out
    tag = _backup_tag(prog["path"])
    names = sorted((f for f in os.listdir(BACKUP_DIR) if f.partition("__")[2] == tag), reverse=True)[:limit]
    for fname in names:
        f = BACKUP_DIR / fname
        try:
            at = datetime.strptime(fname.partition("__")[0], "%Y%m%d-%H%M%S-%f").isoformat(sep=" ", timespec="seconds")
        except ValueError:
            at = fname.partition("__")[0]
        out.append(dict(file=fname, at=at, size=f.stat().st_size, valid=_valid_json(f)))
    return out


def restore_backup(prog, fname=None):
    """Put a backup back in place (the current file is backed up first). fname=None: newest valid one."""
    backups = list_backups(prog, limit=500)
    if fname is None:
        pick = next((b for b in backups if b["valid"]), None)
        if not pick:
            raise ValueError(f"No valid backup of {prog['path'].name} to restore")
        fname = pick["file"]
    elif fname not in {b["file"] for b in backups}:
        raise ValueError("Unknown backup")
    data = read_json(_backup_file(prog, fname))
    with FILE_LOCK:
        write_json(prog["path"], data)
    return fname


# ---------------------------------------------------------------- tool groups

RISKY_TOOLS = {"send_code_to_revit", "navis_execute_code", "execute_python", "execute_console_command",
               "preview_code", "post_command"}
WRITE_VERBS = re.compile(
    r"^(create|delete|modify|set|move|rotate|mirror|copy|edit|change|apply|batch|align|auto|connect|convert|"
    r"purge|deep_purge|heal|resolve|rollback|undo|save|sync|relinquish|detach|load|unload|import|transfer|"
    r"group|snap|tag|wall_floor|room_to|reassign|category_to|find_replace|parameter|bulk|add|place|override|"
    r"color|hide|send|execute|post|spawn|duplicate|rename|compile|build|play|stop|open|close|activate|reset|"
    r"bind|recheck|run|refresh|append|update|timeliner_(add|delete|update)|enrich|configure|crop|manage|"
    r"select_actors|ai_summarize_and_push|clear)(_|$)")
TOOL_GROUPS = [
    ("code", "Code execution", re.compile("^(" + "|".join(sorted(RISKY_TOOLS)) + ")$")),
    ("navis", "Navisworks", re.compile(r"^navis_|_to_navis$")),
    ("ai", "AI assistants", re.compile(r"^(ai_|ollama_)")),
    ("data", "Export, import & cloud data", re.compile(r"export|import|google|notion|powerbi|sqlite|snapshot|csv|excel|auth_status")),
    ("qaqc", "QA/QC & model health", re.compile(r"audit|check_|validate|warning|duplicate|heal|purge|remediation|compliance|naming|health|ids_|clash")),
    ("docs", "Views, sheets & annotation", re.compile(r"sheet|view|schedule|legend|tag|dimension|text_note|print|annotation|revision|spot_elevation|filled_region|detail_")),
    ("model", "Modelling & editing", WRITE_VERBS),
    ("read", "Reading the model", re.compile(r".")),
]
UNREAL_GROUP = ("unreal", "Unreal Engine")


def tool_meta(server, tool):
    """Feature group and safety flags for one tool, used by the Tools dialog and its presets."""
    bare = tool[len("navis_"):] if tool.startswith("navis_") else tool
    risky = tool in RISKY_TOOLS
    writes = risky or bool(WRITE_VERBS.match(bare))
    if not risky and "unreal" in server.lower():
        gid = UNREAL_GROUP[0]
    else:
        gid = next(g for g, _, rx in TOOL_GROUPS if rx.search(tool))
    return dict(group=gid, risky=risky, writes=writes)


# ---------------------------------------------------------------- host apps / processes

def _ps_json(script, timeout=25):
    try:
        out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
                             capture_output=True, timeout=timeout, creationflags=NO_WINDOW).stdout
        return json.loads(out.decode("utf-8", "replace") or "null")
    except (OSError, subprocess.SubprocessError, ValueError):
        return None


def _as_list(v):
    return [] if v is None else v if isinstance(v, list) else [v]


def system_snapshot():
    """Running processes {pid: info} and listening TCP ports {port: pid}, from one PowerShell call."""
    data = _ps_json(
        "[Console]::OutputEncoding=[Text.Encoding]::UTF8;"
        "$p=Get-CimInstance Win32_Process | Select-Object @{n='pid';e={$_.ProcessId}},@{n='ppid';e={$_.ParentProcessId}},"
        "@{n='name';e={$_.Name}},@{n='cmd';e={if($_.Name -match '^(node|bimbot-mcp-proxy)\\.exe$'){$_.CommandLine}}},"
        "@{n='started';e={if($_.CreationDate){$_.CreationDate.ToString('s')}}};"
        "$l=Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Select-Object @{n='port';e={$_.LocalPort}},@{n='pid';e={$_.OwningProcess}};"
        "@{procs=@($p);listen=@($l)} | ConvertTo-Json -Depth 3 -Compress") or {}
    procs = {int(p["pid"]): p for p in _as_list(data.get("procs")) if isinstance(p, dict) and p.get("pid") is not None}
    listen = {}
    for l in _as_list(data.get("listen")):
        if isinstance(l, dict) and l.get("port") is not None:
            listen.setdefault(int(l["port"]), int(l.get("pid") or 0))
    return procs, listen


def _running(procs, *exe):
    names = {e.lower() for e in exe}
    return [p for p in procs.values() if (p.get("name") or "").lower() in names]


def _port_open(port, host="127.0.0.1", timeout=0.4):
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except OSError:
        return False


def _handshake_host(hid, label, exes, handshake, default_port, procs, listen):
    app = _running(procs, *exes)
    hs_file = BIMBOT_DATA / handshake
    try:
        hs = read_json(hs_file, None) if hs_file.exists() else None
    except ValueError:
        hs = {}
    hs_pid = int((hs or {}).get("pid") or 0)
    port = int((hs or {}).get("port") or default_port)
    stale = hs is not None and hs_pid > 0 and hs_pid not in procs
    owner_pid = listen.get(port)
    owner = (procs.get(owner_pid) or {}).get("name") if owner_pid else None
    details, fixes = [], []
    if hs is not None:
        details.append(f"Handshake {hs_file.name}: port {port}, pid {hs_pid or '?'}" + (" (process ended)" if stale else ""))
    if owner_pid:
        details.append(f"Port {port} is listening — owned by {owner or '?'} (pid {owner_pid})")
    if stale:
        fixes.append(dict(id=f"clean-handshake-{hid}", label=f"Remove stale {hs_file.name}", safe=True))

    if not app:
        state = "off"
        summary = f"{label} is not running."
        if owner_pid and owner and owner.lower() not in {e.lower() for e in exes}:
            state = "warn"
            summary += f" Port {port} is taken by {owner} (pid {owner_pid}); the add-in will move to the next free port."
    elif hs is not None and not stale and _port_open(port):
        state, summary = "ok", f"{label} is running and the BIM-Bot add-in is listening on port {port}."
    elif hs is None or stale:
        state = "warn"
        summary = (f"{label} is open but the BIM-Bot add-in has not started its listener. "
                   f"Open a model, check the BIM-Bot ribbon tab loaded, or restart {label}.")
    else:
        state = "bad"
        summary = f"{label} wrote port {port} but nothing answers there. Restart {label}."
    if app:
        details.insert(0, "Running: " + ", ".join(f"{p['name']} (pid {p['pid']})" for p in app))
    return dict(id=hid, name=label, state=state, summary=summary, details=details, fixes=fixes)


def _unreal_host(procs, listen):
    app = _running(procs, "UnrealEditor.exe", "UE4Editor.exe")
    rc, bridge = _port_open(30010), _port_open(30011)
    details = []
    if app:
        details.append("Running: " + ", ".join(f"{p['name']} (pid {p['pid']})" for p in app))
    details.append(f"Remote Control API (port 30010): {'answering' if rc else 'not answering'}")
    details.append(f"BIM-Bot bridge plugin (port 30011): {'answering' if bridge else 'not answering'}")
    if not app:
        state, summary = "off", "Unreal Editor is not running."
    elif rc or bridge:
        state = "ok"
        summary = "Unreal Editor is running and reachable" + ("" if bridge else " (bridge plugin off — Remote Control only)") + "."
    else:
        state = "warn"
        summary = ("Unreal Editor is open but neither port answers. Enable the Remote Control API plugin "
                   "(and the BIM-Bot bridge plugin) in the project, then restart the editor.")
    return dict(id="unreal", name="Unreal Engine", state=state, summary=summary, details=details, fixes=[])


AI_PROGRAM_EXES = {"claude.exe": "Claude Desktop", "cursor.exe": "Cursor", "windsurf.exe": "Windsurf",
                   "opencode.exe": "OpenCode", "antigravity.exe": "Antigravity", "gemini.exe": "Gemini CLI"}


def bimbot_processes(procs):
    """BIM-Bot MCP server (node) and tool-filter proxy processes, with whether the program that started them is alive."""
    out = []
    for p in procs.values():
        name = (p.get("name") or "").lower()
        cmd = (p.get("cmd") or "")
        low = cmd.lower().replace("/", "\\")
        if name == "bimbot-mcp-proxy.exe":
            kind = "Tool-filter proxy"
        elif name == "node.exe" and ("bimbot\\server\\build\\index.js" in low or "revit-mcp-server" in low):
            kind = "BIM-Bot MCP server"
        elif name == "node.exe" and "unreal-mcp" in low:
            kind = "Unreal MCP server"
        else:
            continue
        parent = procs.get(int(p.get("ppid") or 0))
        out.append(dict(pid=int(p["pid"]), kind=kind, started=p.get("started"), cmd=cmd[-300:],
                        parent=(parent or {}).get("name"), parent_pid=int(p.get("ppid") or 0),
                        orphan=parent is None, navis="BIMBOT_SCOPE" in cmd or "navis" in low))
    return sorted(out, key=lambda x: (not x["orphan"], x["kind"], x["pid"]))


def health():
    procs, listen = system_snapshot()
    hosts = [
        _handshake_host("revit", "Revit", ("Revit.exe",), "service.json", 8080, procs, listen),
        _handshake_host("navis", "Navisworks", ("Roamer.exe",), "service-navis.json", 8091, procs, listen),
        _unreal_host(procs, listen),
    ]
    version = None
    pkg = BIMBOT_DIR / "server" / "package.json"
    try:
        version = read_json(pkg, {}).get("version")
    except ValueError:
        pass
    token = AUTH_TOKEN_FILE.exists() and AUTH_TOKEN_FILE.stat().st_size > 0
    server = dict(installed=BIMBOT_SERVER.exists(), path=str(BIMBOT_SERVER), version=version,
                  node=find_node(), token=token, token_path=str(AUTH_TOKEN_FILE))
    ai = [dict(name=label, processes=len(found)) for exe, label in AI_PROGRAM_EXES.items()
          if (found := _running(procs, exe))]
    return dict(hosts=hosts, server=server, processes=bimbot_processes(procs), ai_programs=ai,
                checked=datetime.now().isoformat(timespec="seconds"), panel_version=PANEL_VERSION,
                system_ok=bool(procs))


def end_process(pid):
    """End one BIM-Bot server/proxy process (and its children). Refuses any other process."""
    procs, _ = system_snapshot()
    if pid not in {p["pid"] for p in bimbot_processes(procs)}:
        raise ValueError(f"Process {pid} is not a BIM-Bot MCP process")
    r = subprocess.run(["taskkill", "/T", "/F", "/PID", str(pid)], capture_output=True, text=True, creationflags=NO_WINDOW)
    if r.returncode != 0:
        raise ValueError((r.stderr or r.stdout).strip() or f"Could not end process {pid}")


def end_orphans():
    procs, _ = system_snapshot()
    n = 0
    for p in bimbot_processes(procs):
        if p["orphan"]:
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(p["pid"])], capture_output=True, creationflags=NO_WINDOW)
            n += 1
    return n


def health_fix(fix_id):
    m = re.fullmatch(r"clean-handshake-(revit|navis)", fix_id or "")
    if not m:
        raise ValueError(f"Unknown fix: {fix_id}")
    f = BIMBOT_DATA / ("service.json" if m.group(1) == "revit" else "service-navis.json")
    procs, _ = system_snapshot()
    try:
        pid = int((read_json(f, {}) or {}).get("pid") or 0)
    except ValueError:
        pid = 0
    if not procs:
        raise ValueError("Could not list running processes; leaving the handshake file alone")
    if pid in procs:
        raise ValueError(f"{f.name} belongs to a running process ({pid}); leaving it")
    f.unlink(missing_ok=True)


# ---------------------------------------------------------------- read / write per program

def stash():
    return read_json(STASH_FILE, {})


def list_program(prog):
    cfg = read_json(prog["path"])
    servers = cfg.get(prog["key"]) or {}
    out = []
    for name, raw in servers.items():
        if isinstance(raw, dict):
            out.append((name, normalize(raw, prog["fmt"]), False))
    for name, raw in (stash().get(prog["id"]) or {}).items():
        if name not in servers:
            e = normalize(raw, prog["fmt"])
            e["enabled"] = False
            out.append((name, e, True))
    return out


def update_entry(prog, name, mutate):
    with FILE_LOCK:
        st = stash()
        stashed = (st.get(prog["id"]) or {}).get(name)
        if stashed is not None:
            entry = mutate(normalize(stashed, prog["fmt"]))
            st[prog["id"]][name] = denormalize(entry, prog["fmt"], prog["id"], name, stashed)
            write_json(STASH_FILE, st, backup=False)
            return
        cfg = read_json(prog["path"])
        servers = cfg.setdefault(prog["key"], {})
        if name not in servers:
            raise ValueError(f"{name} is not configured in {prog['name']}")
        original = servers[name]
        entry = mutate(normalize(original, prog["fmt"]))
        servers[name] = denormalize(entry, prog["fmt"], prog["id"], name, original)
        write_json(prog["path"], cfg)


def set_enabled(prog, name, enabled):
    with FILE_LOCK:
        cfg = read_json(prog["path"])
        servers = cfg.setdefault(prog["key"], {})
        if prog["fmt"] == "opencode":
            # OpenCode has a native switch, so the entry stays in place.
            if name not in servers:
                raise ValueError(f"{name} is not configured in OpenCode")
            servers[name]["enabled"] = bool(enabled)
            write_json(prog["path"], cfg)
            return
        st = stash()
        bucket = st.setdefault(prog["id"], {})
        if enabled:
            if name not in bucket:
                return
            servers[name] = bucket.pop(name)
            write_json(prog["path"], cfg)
        else:
            if name not in servers:
                return
            bucket[name] = servers.pop(name)
            write_json(prog["path"], cfg)
        if not bucket:
            st.pop(prog["id"], None)
        write_json(STASH_FILE, st, backup=False)


def save_entry(prog, name, entry, new=False):
    if new:
        with FILE_LOCK:
            cfg = read_json(prog["path"])
            servers = cfg.setdefault(prog["key"], {})
            if name in servers or name in (stash().get(prog["id"]) or {}):
                raise ValueError(f"{name} already exists in {prog['name']}")
            entry.setdefault("enabled", True)
            servers[name] = denormalize(entry, prog["fmt"], prog["id"], name)
            write_json(prog["path"], cfg)
        return
    def mutate(e):
        e.update({k: entry[k] for k in ("type", "command", "args", "env", "url", "timeout") if k in entry})
        return e
    update_entry(prog, name, mutate)


def set_tools(prog, name, disabled):
    """Switch individual tools off for one server in one program."""
    if raw_entry(prog, name)["type"] != "stdio":
        raise ValueError("Per-tool control works only for local (stdio) servers")
    with FILE_LOCK:
        flt = read_json(FILTER_FILE, {})
        bucket = flt.setdefault(prog["id"], {})
        if disabled:
            bucket[name] = sorted(set(disabled))
        else:
            bucket.pop(name, None)
        if not bucket:
            flt.pop(prog["id"], None)
        write_json(FILTER_FILE, flt, backup=False)
    update_entry(prog, name, lambda e: e)   # wraps or unwraps the entry to match


def bimbot_templates():
    node = str(BIMBOT_NODE) if BIMBOT_NODE.exists() else (find_node() or "node")
    base = dict(type="stdio", command=node, args=[str(BIMBOT_SERVER)], url="", timeout=60000)
    return {
        "BIM-Bot": dict(base, env={}),
        "BIM-Bot-Navis": dict(base, env={"BIMBOT_NAVIS_PORT": "8091", "BIMBOT_SCOPE": "navis"}),
    }


# ---------------------------------------------------------------- live probe

def _kill_tree(proc):
    if proc.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(proc.pid)], capture_output=True)
    else:
        proc.kill()


def probe_stdio(entry, timeout=PROBE_TIMEOUT_S):
    exe = resolve_command(entry["command"])
    if not exe:
        return dict(ok=False, error=f"Command not found: {entry['command']}")
    env = dict(os.environ, **{k: str(v) for k, v in entry["env"].items()})
    t0 = time.time()
    try:
        proc = subprocess.Popen([exe, *entry["args"]], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, env=env, cwd=str(HOME),
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    except OSError as e:
        return dict(ok=False, error=f"Could not start: {e}")

    lines, errs = queue.Queue(), []
    threading.Thread(target=lambda: [lines.put(l) for l in iter(proc.stdout.readline, b"")] + [lines.put(None)],
                     daemon=True).start()
    threading.Thread(target=lambda: [errs.append(l.decode("utf-8", "replace")) for l in iter(proc.stderr.readline, b"")],
                     daemon=True).start()

    def send(msg):
        proc.stdin.write((json.dumps(msg) + "\n").encode())
        proc.stdin.flush()

    def wait_for(msg_id):
        while True:
            left = timeout - (time.time() - t0)
            if left <= 0:
                raise TimeoutError
            line = lines.get(timeout=left)
            if line is None:
                raise EOFError
            try:
                msg = json.loads(line)
            except ValueError:
                continue   # banner text on stdout
            if msg.get("id") == msg_id:
                return msg

    result = dict(ok=False)
    try:
        send(dict(jsonrpc="2.0", id=1, method="initialize", params=dict(
            protocolVersion="2025-06-18", capabilities={},
            clientInfo=dict(name="bimbot-mcp-dashboard", version="1.0"))))
        init = wait_for(1)
        if "error" in init:
            raise RuntimeError(init["error"].get("message", "initialize failed"))
        info = init.get("result", {}).get("serverInfo", {})
        send(dict(jsonrpc="2.0", method="notifications/initialized"))
        send(dict(jsonrpc="2.0", id=2, method="tools/list", params={}))
        tools = wait_for(2).get("result", {}).get("tools", [])
        result = dict(ok=True, tools=len(tools),
                      tool_list=[dict(name=t.get("name"), description=(t.get("description") or "").strip()[:200])
                                 for t in tools][:1000],
                      server=f"{info.get('name', '?')} {info.get('version', '')}".strip())
    except (TimeoutError, queue.Empty):
        result["error"] = f"No response within {timeout} s"
    except EOFError:
        result["error"] = f"Process exited (code {proc.poll()}) before answering"
    except (OSError, RuntimeError) as e:
        result["error"] = str(e)
    finally:
        _kill_tree(proc)
    result["ms"] = int((time.time() - t0) * 1000)
    result["stderr"] = "".join(errs)[-1500:]
    return result


def probe_http(entry, timeout=20):
    import urllib.request
    t0 = time.time()
    body = json.dumps(dict(jsonrpc="2.0", id=1, method="initialize", params=dict(
        protocolVersion="2025-06-18", capabilities={}, clientInfo=dict(name="bimbot-mcp-dashboard", version="1.0"))))
    req = urllib.request.Request(entry["url"], data=body.encode(), method="POST", headers={
        "Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return dict(ok=True, server=f"HTTP {r.status}", tools=None, ms=int((time.time() - t0) * 1000))
    except Exception as e:  # noqa: BLE001 - any failure is a failed probe
        code = getattr(e, "code", None)
        if code in (401, 403):
            return dict(ok=False, error=f"HTTP {code}: needs sign-in / API key", ms=int((time.time() - t0) * 1000))
        return dict(ok=False, error=str(e), ms=int((time.time() - t0) * 1000))


PROBES = read_json(PROBES_FILE, {})
PROBES_RUNNING = set()


def run_probe(entry):
    key = probe_key(entry)
    if key in PROBES_RUNNING:
        return
    PROBES_RUNNING.add(key)
    try:
        res = probe_http(entry) if entry["type"] == "http" else probe_stdio(entry)
        res["at"] = datetime.now().isoformat(timespec="seconds")
        PROBES[key] = res
        with FILE_LOCK:
            write_json(PROBES_FILE, PROBES, backup=False)
    finally:
        PROBES_RUNNING.discard(key)


# ---------------------------------------------------------------- snapshot

def mask_env(env):
    return {k: ("••••••" if SECRET_HINT.search(k) and v else v) for k, v in env.items()}


def hidden_servers():
    return set(read_json(SETTINGS_FILE, {}).get("hidden") or [])


def set_hidden(name, hidden):
    """Hide a server that isn't part of BIM-Bot from the app; its program configs are left alone."""
    with FILE_LOCK:
        s = read_json(SETTINGS_FILE, {})
        names = set(s.get("hidden") or [])
        names.add(name) if hidden else names.discard(name)
        s["hidden"] = sorted(names)
        write_json(SETTINGS_FILE, s, backup=False)


def _with_tool_meta(name, probe):
    if not probe or not probe.get("tool_list"):
        return probe
    return dict(probe, tool_list=[dict(t, **tool_meta(name, t.get("name") or "")) for t in probe["tool_list"]])


def toggle_everywhere(name, enabled):
    """Switch one server on or off in every program that has it. Returns the programs changed."""
    changed = []
    for prog in program_defs():
        if read_config(prog)[1]:
            continue
        for n, entry, _ in list_program(prog):
            if n == name and entry["enabled"] != enabled:
                set_enabled(prog, name, enabled)
                changed.append(prog["name"])
    return changed


def register_everywhere(name):
    """Add a BIM-Bot template to every installed program that doesn't have it yet."""
    tpl = bimbot_templates().get(name)
    if not tpl:
        raise ValueError(f"No template for {name}")
    if not BIMBOT_SERVER.exists():
        raise ValueError("BIM-Bot is not installed on this PC")
    added = []
    for prog in program_defs():
        if not prog["installed"] or read_config(prog)[1]:
            continue
        if name not in {n for n, _, _ in list_program(prog)}:
            save_entry(prog, name, dict(tpl), new=True)
            added.append(prog["name"])
    return added


def snapshot(show_hidden=False):
    programs = []
    hidden = hidden_servers()
    for prog in program_defs():
        servers = []
        _, error = read_config(prog)
        if error:
            backups = list_backups(prog)
            programs.append(dict(id=prog["id"], name=prog["name"], path=str(prog["path"]), exists=True,
                                 installed=True, restart=prog["restart"], servers=[], can_add=[], error=error,
                                 can_restore=any(b["valid"] for b in backups)))
            continue
        if prog["path"].exists() or stash().get(prog["id"]):
            entries = list_program(prog)
            dups = find_duplicates(entries)
            for name, entry, stashed in entries:
                if name in hidden and not show_hidden:
                    continue
                issues, fixes = diagnose(prog, name, entry)
                if name in dups:
                    issues.append(f"Starts exactly the same server as {dups[name]} — the program loads it twice.")
                    fixes.append(dict(id="disable-duplicate", label=f"Switch off (duplicate of {dups[name]})"))
                key = probe_key(entry)
                probe = _with_tool_meta(name, PROBES.get(key))
                if not entry["enabled"]:
                    status = "off"
                elif key in PROBES_RUNNING:
                    status = "testing"
                elif issues:
                    status = "misconfigured"
                elif probe:
                    status = "working" if probe.get("ok") else "failed"
                else:
                    status = "untested"
                servers.append(dict(
                    name=name, status=status, enabled=entry["enabled"], stashed=stashed,
                    type=entry["type"], command=entry["command"], args=entry["args"], url=entry["url"],
                    env=mask_env(entry["env"]), timeout=entry.get("timeout"),
                    issues=issues, fixes=fixes, probe=probe, key=key,
                    tools_off=disabled_tools(prog["id"], name), filtered=bool(entry.get("wrapper")),
                    hidden=name in hidden))
        present = {s["name"] for s in servers}
        missing = [n for n in bimbot_templates() if n not in present] if prog["installed"] else []
        programs.append(dict(id=prog["id"], name=prog["name"], path=str(prog["path"]),
                             exists=prog["path"].exists(), installed=prog["installed"],
                             restart=prog["restart"], servers=servers, can_add=missing))
    groups = [dict(id=g, label=label, risky=g == "code") for g, label, _ in TOOL_GROUPS]
    groups.insert(-1, dict(id=UNREAL_GROUP[0], label=UNREAL_GROUP[1], risky=False))
    return dict(programs=programs, bimbot_installed=BIMBOT_SERVER.exists(), probe_timeout=PROBE_TIMEOUT_S,
                hidden=sorted(hidden), show_hidden=show_hidden, groups=groups, panel_version=PANEL_VERSION)


def raw_entry(prog, name):
    for n, e, _ in list_program(prog):
        if n == name:
            return e
    raise ValueError(f"{name} not found in {prog['name']}")


# ---------------------------------------------------------------- http

class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def _send(self, code, body, ctype="application/json"):
        data = body if isinstance(body, bytes) else json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def _authorized(self):
        host = (self.headers.get("Host") or "").split(":")[0]
        return host in ("127.0.0.1", "localhost") and secrets.compare_digest(
            self.headers.get("X-Dashboard-Token", ""), TOKEN)

    def do_GET(self):
        if (self.headers.get("Host") or "").split(":")[0] not in ("127.0.0.1", "localhost"):
            return self._send(403, dict(error="forbidden"))
        if self.path in ("/", "/index.html"):
            html = (HERE / "index.html").read_text(encoding="utf-8").replace("__TOKEN__", TOKEN)
            return self._send(200, html.encode(), "text/html; charset=utf-8")
        if not self._authorized():
            return self._send(403, dict(error="forbidden"))
        if self.path.split("?")[0] == "/api/state":
            return self._send(200, snapshot(show_hidden="all=1" in self.path))
        self._send(404, dict(error="not found"))

    def do_POST(self):
        if not self._authorized():
            return self._send(403, dict(error="forbidden"))
        try:
            body = json.loads(self.rfile.read(int(self.headers.get("Content-Length") or 0)) or b"{}")
            self._send(200, self.route(body) or dict(ok=True))
        except ValueError as e:
            self._send(400, dict(error=str(e)))
        except Exception as e:  # noqa: BLE001
            self._send(500, dict(error=f"{type(e).__name__}: {e}"))

    def route(self, b):
        p = self.path
        if p == "/api/hide":
            set_hidden(str(b.get("name", "")), bool(b.get("hidden")))
            return None
        if p == "/api/health":
            return health()
        if p == "/api/health-fix":
            health_fix(b.get("fix"))
            return None
        if p == "/api/kill":
            end_process(int(b.get("pid") or 0))
            return None
        if p == "/api/kill-orphans":
            return dict(ended=end_orphans())
        if p == "/api/fix-all":
            return fix_all_safe()
        if p == "/api/updates":
            return updates.status(PANEL_VERSION)
        if p == "/api/updates-check":
            updates.check_manifest()
            return updates.status(PANEL_VERSION)
        if p == "/api/updates-setting":
            updates.set_app_setting(str(b.get("app", "")),
                                    auto_check=b.get("autoCheck") if "autoCheck" in b else None,
                                    skip=str(b["skip"]) if "skip" in b else None)
            return None
        if p == "/api/updates-projects":
            updates.set_unreal_projects([str(x) for x in b.get("paths") or []])
            return None
        if p in ("/api/update-precheck", "/api/update"):
            targets = [str(t) for t in b.get("targets") or []]
            procs, _ = system_snapshot()
            running = bimbot_processes(procs)
            if p == "/api/update-precheck":
                return dict(blockers=updates.precheck(targets, procs), processes=len(running))
            updates.start_update(targets, procs, [x["pid"] for x in running], bool(b.get("endProcesses")))
            return None
        if p == "/api/toggle-all":
            return dict(changed=toggle_everywhere(str(b.get("name", "")), bool(b.get("enabled"))))
        if p == "/api/add-bimbot-all":
            return dict(added=register_everywhere(str(b.get("name", ""))))
        if p == "/api/backups":
            return dict(programs=[dict(id=pr["id"], name=pr["name"], path=str(pr["path"]),
                                       error=read_config(pr)[1], backups=list_backups(pr))
                                  for pr in program_defs() if pr["path"].exists() or list_backups(pr, 1)])
        if p == "/api/probe-all":
            seen, hidden = {}, hidden_servers()
            for prog in program_defs():
                if prog["path"].exists() and not read_config(prog)[1]:
                    for name, e, _ in list_program(prog):
                        if e["enabled"] and name not in hidden:
                            seen.setdefault(probe_key(e), e)
            for e in seen.values():
                threading.Thread(target=run_probe, args=(e,), daemon=True).start()
            return dict(started=len(seen))

        prog = get_program(b.get("program"))
        name = b.get("name", "")
        if p == "/api/restore":
            return dict(restored=restore_backup(prog, b.get("file") or None))
        if p == "/api/probe":
            threading.Thread(target=run_probe, args=(raw_entry(prog, name),), daemon=True).start()
        elif p == "/api/toggle":
            set_enabled(prog, name, bool(b.get("enabled")))
        elif p == "/api/tools":
            set_tools(prog, name, [str(t) for t in b.get("disabled") or []])
        elif p == "/api/fix":
            apply_fix(prog, name, b.get("fix"))
        elif p == "/api/entry":
            return dict(entry=raw_entry(prog, name))
        elif p == "/api/save":
            e = b.get("entry") or {}
            e["args"] = [str(a) for a in e.get("args", [])]
            e["env"] = {str(k): str(v) for k, v in (e.get("env") or {}).items()}
            if not name.strip():
                raise ValueError("Name is required")
            save_entry(prog, name.strip(), e, new=bool(b.get("new")))
        elif p == "/api/add-bimbot":
            tpl = bimbot_templates().get(name)
            if not tpl:
                raise ValueError(f"No template for {name}")
            if not BIMBOT_SERVER.exists():
                raise ValueError("BIM-Bot is not installed on this PC")
            save_entry(prog, name, dict(tpl), new=True)
        else:
            raise ValueError(f"Unknown endpoint {p}")
        return None


def start_server(port=0):
    """Start the dashboard API in a background thread; returns its URL (port 0 = any free port)."""
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    updates.maybe_auto_check()
    srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return f"http://127.0.0.1:{srv.server_address[1]}/"


def main():
    ap = argparse.ArgumentParser(description="BIM-Bot MCP dashboard (browser mode)")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--no-browser", action="store_true")
    args = ap.parse_args()
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    updates.maybe_auto_check()
    srv = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    url = f"http://127.0.0.1:{args.port}/"
    print(f"BIM-Bot MCP dashboard on {url}  (Ctrl+C to stop)")
    if not args.no_browser:
        threading.Timer(0.6, webbrowser.open, args=(url,)).start()
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
