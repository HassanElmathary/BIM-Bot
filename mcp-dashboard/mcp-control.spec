# PyInstaller spec: the portable, windowed onefile control panel.
#
#   BIMBot-Control.exe   windowed control panel (tray + webview window)
#
# It bundles the console tool-filter proxy (built one-folder by proxy.spec
# into dist/bimbot-mcp-proxy/) as data under the "proxy/" prefix. On first
# start, server.ensure_proxy() extracts it to
# %LOCALAPPDATA%\BIMBot\control\proxy\ so program configs can point at a path
# that survives the portable exe being moved or deleted.
#
# Build (two passes, outputs to %TEMP% to avoid OneDrive file locks mid-build):
#   python -m PyInstaller proxy.spec --noconfirm --workpath %TEMP%\mcpctl-build\proxy --distpath %TEMP%\mcpctl-dist
#   set BIMBOT_PROXY_STAGING=%TEMP%\mcpctl-dist\bimbot-mcp-proxy
#   python -m PyInstaller mcp-control.spec --noconfirm --workpath %TEMP%\mcpctl-build\control --distpath %TEMP%\mcpctl-dist
#   copy %TEMP%\mcpctl-dist\BIMBot-Control.exe dist\
#
# See build.ps1.

import os

EXCLUDES = ["PySide6", "PyQt5", "PyQt6", "qtpy", "shiboken6", "gi", "numpy", "tkinter",
            "webview.platforms.qt", "webview.platforms.gtk", "webview.platforms.cef"]

PROXY_STAGING = os.environ.get("BIMBOT_PROXY_STAGING", "dist/bimbot-mcp-proxy")

a = Analysis(["app.py"], pathex=["."],
             datas=[("index.html", "."), (PROXY_STAGING, "proxy")],
             hiddenimports=["server", "pystray._win32", "webview.platforms.edgechromium"],
             excludes=EXCLUDES)
pyz = PYZ(a.pure)

window = EXE(pyz, a.scripts, a.binaries, a.datas, name="BIMBot-Control",
             console=False, icon="../installer/assets/bimbot.ico",
             version="version-info.txt", uac_admin=False)
