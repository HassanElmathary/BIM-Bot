"""Tests for the control panel's config repair and update rules.

Run:  python -m unittest discover -s mcp-dashboard/tests

Every path the panel touches is derived from the home / AppData folders, so
this module points them at a throwaway folder before importing anything; the
real program configs on the PC are never read or written.
"""

import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HOME = Path(tempfile.mkdtemp(prefix="mcpctl-test-"))
os.environ.update(USERPROFILE=str(HOME), HOME=str(HOME), APPDATA=str(HOME / "Roaming"),
                  LOCALAPPDATA=str(HOME / "Local"))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import server  # noqa: E402
import updates  # noqa: E402

NODE = server.find_node() or "node"


def entry(script="C:\\missing\\BIMBot\\index.js"):
    return {"command": NODE, "args": [script]}


class ConfigRepair(unittest.TestCase):
    def setUp(self):
        self.claude = Path(os.environ["APPDATA"]) / "Claude" / "claude_desktop_config.json"
        self.gemini = HOME / ".gemini" / "settings.json"
        self.opencode = HOME / ".config" / "opencode" / "opencode.json"
        for f in (self.claude, self.gemini, self.opencode):
            f.unlink(missing_ok=True)
        server.write_json(self.claude, {"mcpServers": {"BIM-Bot": entry(), "BIM-Bot copy": entry()}})
        server.write_json(self.gemini, {"mcpServers": {"BIM-Bot": entry()}})
        self.prog = server.get_program("claude-desktop")

    def servers(self, path):
        return json.loads(path.read_text())["mcpServers"]

    def test_duplicate_is_flagged_and_switched_off(self):
        cd = next(p for p in server.snapshot()["programs"] if p["id"] == "claude-desktop")
        copy = next(s for s in cd["servers"] if s["name"] == "BIM-Bot copy")
        self.assertIn("disable-duplicate", [f["id"] for f in copy["fixes"]])
        server.apply_fix(self.prog, "BIM-Bot copy", "disable-duplicate")
        self.assertNotIn("BIM-Bot copy", self.servers(self.claude))

    def test_toggle_everywhere(self):
        self.assertEqual(set(server.toggle_everywhere("BIM-Bot", False)), {"Claude Desktop", "Gemini CLI"})
        self.assertNotIn("BIM-Bot", self.servers(self.gemini))
        self.assertEqual(set(server.toggle_everywhere("BIM-Bot", True)), {"Claude Desktop", "Gemini CLI"})
        self.assertIn("BIM-Bot", self.servers(self.gemini))

    def test_broken_config_is_reported_refused_and_restored(self):
        self.claude.write_text('{"mcpServers": {"BIM-Bot": {,}}')
        cd = next(p for p in server.snapshot()["programs"] if p["id"] == "claude-desktop")
        self.assertIn("not valid JSON", cd["error"])
        self.assertTrue(cd["can_restore"])
        with self.assertRaises(ValueError):
            server.set_enabled(self.prog, "BIM-Bot", False)
        self.assertEqual(server.restore_backup(self.prog), server.LAST_SAVED)
        self.assertIn("BIM-Bot copy", self.servers(self.claude))   # the last state this app saved
        self.assertTrue(any(not b["valid"] for b in server.list_backups(self.prog)),
                        "the broken file must be backed up before it is replaced")

    def test_restore_rejects_unknown_file(self):
        with self.assertRaises(ValueError):
            server.restore_backup(self.prog, "..\\..\\evil.json")

    def test_fix_all_raises_opencode_timeout(self):
        oc = self.opencode
        server.write_json(oc, {"mcp": {"BIM-Bot": {"type": "local", "command": [NODE, "x.js"], "timeout": 10000}}})
        self.assertGreaterEqual(server.fix_all_safe()["fixed"], 1)
        self.assertEqual(json.loads(oc.read_text())["mcp"]["BIM-Bot"]["timeout"], 60000)


class ToolGroups(unittest.TestCase):
    def test_groups_and_flags(self):
        self.assertEqual(server.tool_meta("BIM-Bot", "send_code_to_revit"), dict(group="code", risky=True, writes=True))
        self.assertEqual(server.tool_meta("BIM-Bot", "get_elements")["group"], "read")
        self.assertTrue(server.tool_meta("BIM-Bot", "create_wall")["writes"])
        self.assertFalse(server.tool_meta("BIM-Bot", "navis_get_clash_results")["writes"])
        self.assertEqual(server.tool_meta("BIM-Bot-Unreal", "spawn_actor")["group"], "unreal")


class UpdateRules(unittest.TestCase):
    PAYLOAD = b"not really an installer"

    def setUp(self):
        self.ran = []
        self._run = updates.subprocess.run
        updates.subprocess.run = lambda args, **kw: self.ran.append(args) or subprocess.CompletedProcess(args, 0)
        self._digest, updates._github_digest = updates._github_digest, (lambda v, a: "")
        self._download = updates._download

        def fake_download(url, dest):
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(self.PAYLOAD)
        updates._download = fake_download
        updates.JOB.clear()
        updates.JOB.update(state="idle")

    def tearDown(self):
        updates.subprocess.run, updates._github_digest, updates._download = self._run, self._digest, self._download

    def manifest(self, sha):
        updates.MANIFEST["data"] = {"version": "9.9.9", "assetFileName": "BIM-Bot.exe",
                                    "downloadUrl": "https://example.invalid/BIM-Bot.exe", "sha256": sha}

    def test_wrong_checksum_is_refused_and_deleted(self):
        self.manifest("0" * 64)
        updates._run_update(["revit2025"], [])
        self.assertEqual(updates.JOB["state"], "failed")
        self.assertEqual(self.ran, [])
        self.assertFalse((updates.DOWNLOAD_DIR / "BIM-Bot.exe").exists())

    def test_missing_checksum_is_refused(self):
        self.manifest("")
        updates._run_update(["revit2025"], [])
        self.assertEqual(updates.JOB["state"], "failed")
        self.assertEqual(self.ran, [])

    def test_matching_checksum_runs_per_user_installer_for_picked_parts(self):
        self.manifest(hashlib.sha256(self.PAYLOAD).hexdigest())
        updates._run_update(["revit2025", "navis2024", "unreal"], [])
        self.assertEqual(updates.JOB["state"], "done", updates.JOB.get("message"))
        args = self.ran[-1]
        self.assertIn("/CURRENTUSER", args)
        self.assertIn("/COMPONENTS=server,nodejs,plugin,unreal", args)
        self.assertIn("/TASKS=revit2025,navis2024", args)

    def test_precheck(self):
        self.assertTrue(updates.precheck(["revit2025"], {}), "no process list must block")
        self.assertTrue(updates.precheck(["revit2025"], {1: {"name": "Revit.exe"}}))
        self.assertEqual(updates.precheck(["navis2024"], {1: {"name": "Revit.exe"}}), [])

    def test_only_one_update_at_a_time(self):
        updates.JOB.update(state="downloading")
        with self.assertRaises(ValueError):
            updates.start_update(["server"], {1: {"name": "x"}}, [], True)

    def test_revit_skip_and_auto_check_use_the_plugins_files(self):
        updates.set_app_setting("revit", auto_check=False, skip="2.8.0")
        self.assertEqual(updates.REVIT_SKIP_FILE.read_text(), "v2.8.0")   # plugin compares "v<version>"
        self.assertEqual(json.loads(updates.REVIT_SETTINGS.read_text()), {"AutoCheckUpdates": False})
        updates.set_app_setting("revit", skip="")
        self.assertFalse(updates.REVIT_SKIP_FILE.exists())


if __name__ == "__main__":
    unittest.main()
