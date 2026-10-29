# PyInstaller spec: the console tool-filter proxy, built ONE-FOLDER.
#
#   dist/bimbot-mcp-proxy/   bimbot-mcp-proxy.exe + _internal/ (python312.dll etc.)
#
# One-folder so an MCP start never unpacks Python into %TEMP% and never leaves
# _MEI* folders behind when an AI program force-kills the server.
#
# This folder is bundled into BIMBot-Control.exe by mcp-control.spec (datas)
# and extracted to %LOCALAPPDATA%\BIMBot\control\proxy\ at runtime.
#
# Build:  python -m PyInstaller proxy.spec --noconfirm

EXCLUDES = ["PySide6", "PyQt5", "PyQt6", "qtpy", "shiboken6", "gi", "numpy", "tkinter",
            "webview", "webview.platforms.qt", "webview.platforms.gtk", "webview.platforms.cef",
            "pystray", "PIL", "server"]

a = Analysis(["app.py"], pathex=["."],
             hiddenimports=["proxy"],
             excludes=EXCLUDES)
pyz = PYZ(a.pure)
# One-folder: EXE carries only the script + bootloader; COLLECT adds binaries/datas.
exe = EXE(pyz, a.scripts, [], name="bimbot-mcp-proxy",
          console=True, icon=None, version="version-info.txt", uac_admin=False)
COLLECT(exe, a.binaries, a.datas, name="bimbot-mcp-proxy")
