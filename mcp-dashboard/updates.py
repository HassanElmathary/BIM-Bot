"""Per-app update control for the BIM-Bot MCP Control panel.

Shows the installed version of every BIM-Bot part on this PC (MCP server,
Revit plugin per year, Navisworks plugin per year, Unreal, this panel), the
latest release from version.json, and runs the BIM-Bot installer for just the
parts the user picks.

Safety rules (same as the Revit plugin's UpdateChecker, commit 4d09a41):
* the installer is only run after its SHA-256 matches the manifest and, when
  GitHub publishes one, the release asset digest; no checksum = no install;
* it runs per-user (/CURRENTUSER), so Windows never asks for an admin password
  -- except the Navisworks plugin copy, which the installer elevates itself
  because Navisworks only loads plugins from its Program Files folder.

Skip / auto-check for the Revit plugin are written to the files the plugin
already reads (skip_version.txt, plugin-settings.json), so the panel and the
plugin's own prompt always agree. The other parts keep theirs in updates.json.
"""

import hashlib
import json
import os
import re
import subprocess
import threading
import time
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

HOME = Path.home()
APPDATA = Path(os.environ.get("APPDATA", HOME / "AppData" / "Roaming"))
LOCALAPPDATA = Path(os.environ.get("LOCALAPPDATA", HOME / "AppData" / "Local"))
PROGRAMDATA = Path(os.environ.get("PROGRAMDATA", r"C:\ProgramData"))
PROGRAM_FILES = Path(os.environ.get("ProgramW6432", os.environ.get("ProgramFiles", r"C:\Program Files")))

STATE_FILE = HOME / ".bimbot" / "mcp-dashboard" / "updates.json"   # skip / auto-check for non-Revit parts
BIMBOT_DIR = LOCALAPPDATA / "Programs" / "BIMBot"
DOWNLOAD_DIR = LOCALAPPDATA / "BIMBot" / "Updates"
REVIT_SKIP_FILE = LOCALAPPDATA / "BIMBot" / "skip_version.txt"
REVIT_SETTINGS = APPDATA / "Autodesk" / "Revit" / "Addins" / "BIMBot" / "plugin-settings.json"

GITHUB = "HassanElmathary/BIM-Bot"
MANIFEST_URLS = ("https://elmthary.space/version.json",
                 f"https://raw.githubusercontent.com/{GITHUB}/main/version.json")
YEARS = range(2020, 2028)
NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)
AUTO_CHECK_EVERY_S = 24 * 3600

APPS = {
    "server": "BIM-Bot MCP server",
    "revit": "Revit plugin",
    "navis": "Navisworks plugin",
    "unreal": "Unreal Engine",
    "control": "This control panel",
}

LOCK = threading.Lock()
MANIFEST = {"data": None, "source": None, "error": None, "at": None}
JOB = {"state": "idle"}   # idle | downloading | verifying | installing | done | failed


# ---------------------------------------------------------------- small helpers

def _read_json(path, default):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return default


def _write_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    os.replace(tmp, path)


def parse_version(v):
    """'v2.7.6', '2.7.6.0', '2.7.6+abc' -> (2, 7, 6). Unknown -> None."""
    m = re.match(r"v?(\d+)(?:\.(\d+))?(?:\.(\d+))?", str(v or "").strip())
    return tuple(int(x or 0) for x in m.groups()) if m else None


def _fmt(t):
    return ".".join(map(str, t)) if t else None


def file_version(path):
    """File version from a Windows PE resource, e.g. '2.7.6'. None if missing or unreadable."""
    if os.name != "nt" or not Path(path).exists():
        return None
    import ctypes
    from ctypes import wintypes
    ver = ctypes.windll.version
    size = ver.GetFileVersionInfoSizeW(str(path), None)
    if not size:
        return None
    buf = ctypes.create_string_buffer(size)
    if not ver.GetFileVersionInfoW(str(path), 0, size, buf):
        return None
    ptr, n = ctypes.c_void_p(), wintypes.UINT()
    if not ver.VerQueryValueW(buf, "\\", ctypes.byref(ptr), ctypes.byref(n)) or not ptr.value:
        return None
    ffi = ctypes.cast(ptr, ctypes.POINTER(ctypes.c_uint32 * 4)).contents   # signature, struc, MS, LS
    ms, ls = ffi[2], ffi[3]
    return f"{ms >> 16}.{ms & 0xFFFF}.{ls >> 16}"


def _sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _norm_sha(v):
    v = str(v or "").strip().lower()
    v = v[7:] if v.startswith("sha256:") else v
    return v if re.fullmatch(r"[0-9a-f]{64}", v) else ""


def _get(url, timeout=15):
    if not url.lower().startswith("https://"):
        raise ValueError(f"Refusing non-HTTPS URL: {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "BIMBot-MCP-Control",
                                               "Accept": "application/vnd.github+json"})
    return urllib.request.urlopen(req, timeout=timeout)


# ---------------------------------------------------------------- installed parts

def _addin_assembly(year):
    """Path of BIMBotPlugin.dll that Revit <year> loads, from its BIMBot.addin (per-user first)."""
    for root in (APPDATA, PROGRAMDATA):
        addin = root / "Autodesk" / "Revit" / "Addins" / str(year) / "BIMBot.addin"
        if addin.exists():
            try:
                node = ET.parse(addin).getroot().find(".//Assembly")
                if node is not None and node.text:
                    return addin, Path(node.text.strip())
            except (ET.ParseError, OSError):
                return addin, None
    return None, None


def _navis_dir(year):
    d = PROGRAM_FILES / "Autodesk" / f"Navisworks Manage {year}"
    return d if (d / "Roamer.exe").exists() else None


def _unreal_projects():
    return [Path(p) for p in _read_json(STATE_FILE, {}).get("unrealProjects") or []]


def _uplugin_version(project):
    for up in sorted(Path(project, "Plugins").glob("*/*.uplugin")):
        if re.search(r"bim.?bot|mcp", up.stem, re.I):
            data = _read_json(up, {})
            return up.stem, data.get("VersionName") or str(data.get("Version") or "")
    return None, None


def installed_parts():
    """Every BIM-Bot part this PC has, or could have (a Revit / Navisworks year without the plugin)."""
    parts = []
    pkg = _read_json(BIMBOT_DIR / "server" / "package.json", {})
    parts.append(dict(id="server", app="server", label="MCP server (Node.js)",
                      installed=(BIMBOT_DIR / "server" / "build" / "index.js").exists(),
                      version=pkg.get("version"), path=str(BIMBOT_DIR / "server")))

    for y in YEARS:
        addin, dll = _addin_assembly(y)
        rdir = PROGRAM_FILES / "Autodesk" / f"Revit {y}"
        host = rdir.is_dir()   # same test the installer uses to offer the revit<year> task
        if not (addin or host):
            continue
        note = None
        if addin and (dll is None or not dll.exists()):
            note = "BIMBot.addin points at a missing DLL — update to repair it."
        elif not host:
            note = f"Revit {y} is not installed (left-over add-in); the installer will skip this year."
        elif not (rdir / "Revit.exe").exists():
            note = f"The Revit {y} folder has no Revit.exe — Revit {y} may have been uninstalled."
        parts.append(dict(id=f"revit{y}", app="revit", label=f"Revit {y}", task=f"revit{y}",
                          installed=bool(dll and dll.exists()), host=host,
                          version=file_version(dll) if dll else None,
                          path=str(dll or addin or ""), note=note))

    for y in YEARS:
        d = _navis_dir(y)
        if not d:
            continue
        dll = d / "Plugins" / "BIMBotNavisPlugin" / "BIMBotNavisPlugin.dll"
        parts.append(dict(id=f"navis{y}", app="navis", label=f"Navisworks Manage {y}", task=f"navis{y}",
                          installed=dll.exists(), host=True, version=file_version(dll), path=str(dll),
                          admin=True))

    uexe = BIMBOT_DIR / "unreal" / "BIM-Bot-Unreal.exe"
    # The exe's own version resource is the bundled Node runtime's, so use the
    # release it shipped with (the same installer as the MCP server).
    parts.append(dict(id="unreal", app="unreal", label="Unreal MCP server", component="unreal",
                      installed=uexe.exists(), version=pkg.get("version") if uexe.exists() else None,
                      path=str(uexe)))
    for proj in _unreal_projects():
        name, v = _uplugin_version(proj)
        parts.append(dict(id="uproj:" + str(proj), app="unreal", label=f"Project {proj.name}", project=str(proj),
                          installed=bool(name), version=v, path=str(proj), readonly=True,
                          note=None if name else "No BIM-Bot plugin found in this project's Plugins folder."))
    return parts


# ---------------------------------------------------------------- per-app settings

def app_settings():
    st = _read_json(STATE_FILE, {})
    out = {}
    for app in APPS:
        s = (st.get("apps") or {}).get(app) or {}
        out[app] = dict(autoCheck=s.get("autoCheck", True), skip=s.get("skip") or "")
    # The Revit plugin keeps its own; read those so the panel shows what Revit will do.
    out["revit"]["autoCheck"] = _read_json(REVIT_SETTINGS, {}).get("AutoCheckUpdates", True) is not False
    try:
        out["revit"]["skip"] = REVIT_SKIP_FILE.read_text(encoding="utf-8").strip().lstrip("v")
    except OSError:
        out["revit"]["skip"] = ""
    return out


def set_app_setting(app, auto_check=None, skip=None):
    if app not in APPS:
        raise ValueError(f"Unknown app: {app}")
    with LOCK:
        if app == "revit":
            if auto_check is not None:
                s = _read_json(REVIT_SETTINGS, {})
                s["AutoCheckUpdates"] = bool(auto_check)
                _write_json(REVIT_SETTINGS, s)
            if skip is not None:
                if skip:
                    REVIT_SKIP_FILE.parent.mkdir(parents=True, exist_ok=True)
                    REVIT_SKIP_FILE.write_text("v" + skip.lstrip("v"), encoding="utf-8")   # the plugin compares "v2.7.6"
                else:
                    REVIT_SKIP_FILE.unlink(missing_ok=True)
            return
        st = _read_json(STATE_FILE, {})
        s = st.setdefault("apps", {}).setdefault(app, {})
        if auto_check is not None:
            s["autoCheck"] = bool(auto_check)
        if skip is not None:
            s["skip"] = skip.lstrip("v")
        _write_json(STATE_FILE, st)


def set_unreal_projects(paths):
    clean = []
    for p in paths:
        p = Path(str(p).strip().strip('"'))
        if not p.is_dir() or not list(p.glob("*.uproject")):
            raise ValueError(f"Not an Unreal project folder (no .uproject): {p}")
        clean.append(str(p))
    with LOCK:
        st = _read_json(STATE_FILE, {})
        st["unrealProjects"] = sorted(set(clean))
        _write_json(STATE_FILE, st)


# ---------------------------------------------------------------- manifest

def check_manifest():
    """Fetch version.json (website first, then GitHub raw). Keeps the last good copy on failure."""
    errors = []
    for url in MANIFEST_URLS:
        try:
            with _get(url) as r:
                data = json.loads(r.read().decode("utf-8-sig"))
            if not parse_version(data.get("version")):
                raise ValueError("no version in manifest")
            MANIFEST.update(data=data, source=url, error=None, at=datetime.now().isoformat(timespec="seconds"))
            with LOCK:
                st = _read_json(STATE_FILE, {})
                st["lastCheck"] = time.time()
                st["manifest"] = data
                _write_json(STATE_FILE, st)
            return MANIFEST
        except Exception as e:  # noqa: BLE001 - any failure moves on to the next source
            errors.append(f"{url}: {e}")
    MANIFEST["error"] = "Could not reach the update server. " + " | ".join(errors)
    return MANIFEST


def maybe_auto_check():
    """Background check on start-up when any part has auto-check on and the last check is a day old."""
    st = _read_json(STATE_FILE, {})
    if MANIFEST["data"] is None and st.get("manifest"):
        MANIFEST.update(data=st["manifest"], source="cache",
                        at=datetime.fromtimestamp(st.get("lastCheck") or 0).isoformat(timespec="seconds"))
    if any(s["autoCheck"] for s in app_settings().values()) and time.time() - (st.get("lastCheck") or 0) > AUTO_CHECK_EVERY_S:
        threading.Thread(target=check_manifest, daemon=True).start()


def status(panel_version):
    data = MANIFEST["data"] or {}
    latest = parse_version(data.get("version"))
    settings = app_settings()
    parts = installed_parts()
    control = data.get("control") or {}
    parts.append(dict(id="control", app="control", label="BIM-Bot MCP Control", installed=True,
                      version=panel_version, readonly=True))
    for p in parts:
        mine = parse_version(p.get("version"))
        target = parse_version(control.get("version")) if p["app"] == "control" else latest
        skip = parse_version(settings[p["app"]]["skip"])
        if not target:
            p["state"] = "unknown"
        elif not p["installed"]:
            p["state"] = "not-installed"
        elif mine and mine >= target:
            p["state"] = "current"
        elif skip and skip == target:
            p["state"] = "skipped"
        else:
            p["state"] = "available"
        p["latest"] = _fmt(target)
    return dict(parts=parts, apps=[dict(id=a, name=n, **settings[a]) for a, n in APPS.items()],
                latest=_fmt(latest), changelog=data.get("changelog"), releaseUrl=data.get("releaseUrl"),
                controlUrl=control.get("downloadUrl"), checked=MANIFEST["at"], source=MANIFEST["source"],
                error=MANIFEST["error"], job=dict(JOB), unrealProjects=[str(p) for p in _unreal_projects()])


# ---------------------------------------------------------------- download, verify, install

def _github_digest(version, asset):
    try:
        with _get(f"https://api.github.com/repos/{GITHUB}/releases/tags/v{version}") as r:
            rel = json.loads(r.read().decode("utf-8"))
        for a in rel.get("assets") or []:
            if (a.get("name") or "").lower() == asset.lower():
                return _norm_sha(a.get("digest"))
    except Exception:  # noqa: BLE001 - offline / rate limit: the manifest checksum still applies
        pass
    return ""


def precheck(targets, procs):
    """What must happen before the installer can run. procs: {pid: {name, ...}} from server.system_snapshot()."""
    names = {(p.get("name") or "").lower() for p in procs.values()}
    blockers = []
    if any(t.startswith("revit") for t in targets) and "revit.exe" in names:
        blockers.append("Revit is open. Save your work and close Revit — its plugin DLL is locked while it runs.")
    if any(t.startswith("navis") for t in targets) and "roamer.exe" in names:
        blockers.append("Navisworks is open. Save your work and close Navisworks first.")
    if "unreal" in targets and "bim-bot-unreal.exe" in names:
        blockers.append("The Unreal MCP server is running. Close the AI program that uses BIM-Bot-Unreal first.")
    return blockers


def _download(url, dest):
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    with _get(url, timeout=60) as r, open(tmp, "wb") as f:
        total = int(r.headers.get("Content-Length") or 0)
        done = 0
        while True:
            chunk = r.read(1 << 18)
            if not chunk:
                break
            f.write(chunk)
            done += len(chunk)
            JOB.update(progress=done, total=total)
    os.replace(tmp, dest)


def _run_update(targets, end_pids):
    data = MANIFEST["data"] or {}
    version = data.get("version", "").lstrip("v")
    asset = os.path.basename(data.get("assetFileName") or data.get("downloadUrl", "").split("/")[-1])
    url = data.get("downloadUrl") or ""
    try:
        if not url or not asset.lower().endswith(".exe"):
            raise ValueError("The release manifest has no installer download.")
        expected = [("version manifest", _norm_sha(data.get("sha256")))]
        expected.append(("GitHub release", _github_digest(version, asset)))
        expected = [(k, v) for k, v in expected if v]
        if not expected:
            raise ValueError("No SHA-256 checksum is published for this update, so it was not installed. "
                             "Download it from the GitHub release page instead.")

        dest = DOWNLOAD_DIR / asset
        JOB.update(state="downloading", message=f"Downloading {asset}…", progress=0, total=0)
        _download(url, dest)

        JOB.update(state="verifying", message="Checking the SHA-256 checksum…")
        actual = _sha256(dest)
        for source, sha in expected:
            if actual != sha:
                dest.unlink(missing_ok=True)
                raise ValueError(f"The download does not match the {source} checksum, so it was deleted "
                                 f"and not installed. Try again later.")
        try:
            os.remove(str(dest) + ":Zone.Identifier")
        except OSError:
            pass

        for pid in end_pids:   # BIM-Bot node.exe holds nodejs\node.exe open; the installer must replace it
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(pid)], capture_output=True, creationflags=NO_WINDOW)

        tasks = [t for t in targets if re.fullmatch(r"(revit|navis)\d{4}", t)]
        components = ["server", "nodejs"] + (["plugin"] if tasks else []) + (["unreal"] if "unreal" in targets else [])
        args = [str(dest), "/CURRENTUSER", "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/NOCANCEL",
                "/COMPONENTS=" + ",".join(components), "/TASKS=" + ",".join(tasks),
                "/LOG=" + str(DOWNLOAD_DIR / "install.log")]
        JOB.update(state="installing", message="Running the BIM-Bot installer"
                   + (" — Windows will ask once for admin rights to copy the Navisworks plugin" if any(t.startswith("navis") for t in tasks) else "")
                   + "…", command=" ".join(args[1:]))
        code = subprocess.run(args).returncode
        if code != 0:
            raise ValueError(f"The installer stopped with exit code {code}. See {DOWNLOAD_DIR / 'install.log'}.")
        JOB.update(state="done", message=f"BIM-Bot {version} installed for: " + ", ".join(targets or ["MCP server"])
                   + ". Restart Revit / Navisworks and your AI programs to use it.")
    except Exception as e:  # noqa: BLE001 - every failure is reported in the Updates tab
        JOB.update(state="failed", message=str(e))
    finally:
        JOB["finished"] = datetime.now().isoformat(timespec="seconds")


def start_update(targets, procs, bimbot_pids, end_processes=False):
    if JOB.get("state") in ("downloading", "verifying", "installing"):
        raise ValueError("An update is already running")
    if not MANIFEST["data"]:
        raise ValueError("Check for updates first")
    known = {p.get("task") or p["id"] for p in installed_parts()} | {"server"}
    bad = [t for t in targets if t not in known]
    if bad:
        raise ValueError("Unknown update target: " + ", ".join(bad))
    blockers = precheck(targets, procs)
    if blockers:
        raise ValueError(" ".join(blockers))
    if bimbot_pids and not end_processes:
        raise ValueError(f"{len(bimbot_pids)} BIM-Bot server process(es) are running and lock node.exe")
    JOB.clear()
    JOB.update(state="downloading", message="Starting…", started=datetime.now().isoformat(timespec="seconds"),
               targets=list(targets))
    threading.Thread(target=_run_update, args=([t for t in targets if t != "server"], bimbot_pids),
                     daemon=True).start()
