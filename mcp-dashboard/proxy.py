"""Tool-filtering stdio proxy.

A program's config entry is rewritten to start this proxy instead of the real
MCP server:

    <launcher> proxy --program opencode --server BIM-Bot -- node index.js

The proxy starts the real server, passes every message through, and

* removes switched-off tools from tools/list results,
* answers tools/call for a switched-off tool with an error without forwarding it,
* tells the program the tool list changed when tool-filter.json is edited, so
  programs that support it pick up changes without a restart.

The disabled list lives in ~/.bimbot/mcp-dashboard/tool-filter.json as
{program: {server: [tool, ...]}} and is re-read whenever the file changes.
"""

import json
import os
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

FILTER_FILE = Path.home() / ".bimbot" / "mcp-dashboard" / "tool-filter.json"


def load_disabled(program, server):
    try:
        with open(FILTER_FILE, encoding="utf-8") as f:
            return set((json.load(f).get(program) or {}).get(server) or [])
    except (OSError, ValueError):
        return set()


def run(program, server, cmd):
    exe = cmd[0] if os.path.exists(cmd[0]) else (shutil.which(cmd[0]) or cmd[0])
    child = subprocess.Popen([exe, *cmd[1:]], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                             stderr=sys.stderr, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    out = sys.stdout.buffer
    out_lock = threading.Lock()
    state = dict(disabled=load_disabled(program, server), mtime=_mtime(), list_ids=set(), init_ids=set(),
                 ready=False)

    def to_client(msg):
        data = (json.dumps(msg, ensure_ascii=False) + "\n").encode("utf-8")
        with out_lock:
            out.write(data)
            out.flush()

    def client_to_server():
        for line in sys.stdin.buffer:
            try:
                msg = json.loads(line)
            except ValueError:
                child.stdin.write(line)
                child.stdin.flush()
                continue
            method = msg.get("method") if isinstance(msg, dict) else None
            if method == "tools/list":
                state["list_ids"].add(msg.get("id"))
            elif method == "initialize":
                state["init_ids"].add(msg.get("id"))
            elif method == "notifications/initialized":
                state["ready"] = True
            elif method == "tools/call" and (msg.get("params") or {}).get("name") in state["disabled"]:
                to_client(dict(jsonrpc="2.0", id=msg.get("id"), result=dict(isError=True, content=[dict(
                    type="text", text=f"The tool '{msg['params']['name']}' is switched off for this program "
                                      f"in the BIM-Bot MCP Control app.")])))
                continue
            child.stdin.write(line if line.endswith(b"\n") else line + b"\n")
            child.stdin.flush()
        try:
            child.stdin.close()
        except OSError:
            pass

    def server_to_client():
        for line in child.stdout:
            try:
                msg = json.loads(line)
            except ValueError:
                with out_lock:
                    out.write(line)
                    out.flush()
                continue
            if isinstance(msg, dict) and "result" in msg:
                mid = msg.get("id")
                if mid in state["list_ids"]:
                    state["list_ids"].discard(mid)
                    tools = msg["result"].get("tools")
                    if isinstance(tools, list):
                        msg["result"]["tools"] = [t for t in tools if t.get("name") not in state["disabled"]]
                elif mid in state["init_ids"]:
                    # Advertise list-change notifications so the program refreshes when the filter changes.
                    state["init_ids"].discard(mid)
                    caps = msg["result"].setdefault("capabilities", {})
                    if isinstance(caps.get("tools"), dict):
                        caps["tools"]["listChanged"] = True
            to_client(msg)

    def watch_filter():
        while child.poll() is None:
            time.sleep(2)
            m = _mtime()
            if m != state["mtime"]:
                state["mtime"] = m
                new = load_disabled(program, server)
                if new != state["disabled"]:
                    state["disabled"] = new
                    if state["ready"]:
                        to_client(dict(jsonrpc="2.0", method="notifications/tools/list_changed"))

    threading.Thread(target=client_to_server, daemon=True).start()
    threading.Thread(target=watch_filter, daemon=True).start()
    server_to_client()
    return child.wait()


def _mtime():
    try:
        return FILTER_FILE.stat().st_mtime
    except OSError:
        return None


def main(argv):
    """argv: --program P --server S -- command args..."""
    if "--" not in argv:
        sys.exit("usage: proxy --program P --server S -- command [args...]")
    split = argv.index("--")
    opts, cmd = argv[:split], argv[split + 1:]
    program = opts[opts.index("--program") + 1]
    server = opts[opts.index("--server") + 1]
    sys.exit(run(program, server, cmd))


if __name__ == "__main__":
    main(sys.argv[1:])
