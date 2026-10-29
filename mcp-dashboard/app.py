"""BIM-Bot MCP Control: desktop window + tray icon around the dashboard.

    app.py                 open the control window (one instance per user)
    app.py proxy ...       run the tool-filter proxy (used inside program configs)
"""

import os
import sys


def _single_instance():
    """True if this is the only running window instance."""
    if sys.platform != "win32":
        return True
    import ctypes
    ctypes.windll.kernel32.CreateMutexW(None, False, "Local\\BIMBot-MCP-Control")
    return ctypes.windll.kernel32.GetLastError() != 183   # ERROR_ALREADY_EXISTS


def _tray_image():
    from PIL import Image, ImageDraw
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((2, 2, 62, 62), radius=14, fill=(47, 111, 222))
    for x, y in ((20, 20), (44, 20), (20, 44), (44, 44)):
        d.ellipse((x - 7, y - 7, x + 7, y + 7), fill="white")
    d.line((20, 20, 44, 44), fill="white", width=4)
    d.line((44, 20, 20, 44), fill="white", width=4)
    return img


def _has_webview2():
    """True if the Edge WebView2 runtime is installed.

    Without it pywebview silently falls back to the old IE engine, which can't
    run the dashboard, instead of raising.
    """
    if "BIMBOT_FORCE_BROWSER" in os.environ:   # test hook for the browser fallback
        return False
    try:
        from webview.platforms.winforms import _is_chromium
        return _is_chromium()
    except Exception:  # noqa: BLE001 - no pythonnet / no .NET: same as no WebView2
        return False


def run_window():
    if not _single_instance():
        import ctypes
        ctypes.windll.user32.MessageBoxW(None, "BIM-Bot MCP Control is already running.\n"
                                             "Use its icon in the system tray.", "BIM-Bot MCP Control", 0x40)
        return
    import server
    server.ensure_proxy()
    import pystray

    import threading
    import webbrowser

    url = server.start_server(0)
    state = {"quitting": False, "window": None}   # window stays None in browser mode
    done = threading.Event()

    def show(*_):
        if state["window"]:
            state["window"].show()
            state["window"].restore()
        else:
            webbrowser.open(url)

    def quit_app(*_):
        state["quitting"] = True
        icon.stop()
        done.set()
        if state["window"]:
            state["window"].destroy()

    def on_closing():
        if state["quitting"]:
            return True
        state["window"].hide()   # closing the window keeps the app in the tray
        return False

    icon = pystray.Icon("bimbot-mcp-control", _tray_image(), "BIM-Bot MCP Control", menu=pystray.Menu(
        pystray.MenuItem("Open", show, default=True),
        pystray.MenuItem("Quit", quit_app)))
    icon.run_detached()

    if _has_webview2():
        try:
            import webview
            state["window"] = webview.create_window("BIM-Bot MCP Control", url, width=1180, height=820,
                                                    min_size=(420, 500))
            state["window"].events.closing += on_closing
            webview.start()
            if not state["quitting"]:
                icon.stop()
            return
        except Exception:  # noqa: BLE001 - any start-up failure falls back to the browser
            state["window"] = None
    # No usable WebView2: show the dashboard in the default browser and keep
    # the tray icon (already running detached) until the user picks Quit.
    webbrowser.open(url)
    done.wait()


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "proxy":
        import proxy
        proxy.main(sys.argv[2:])
    else:
        run_window()


if __name__ == "__main__":
    main()
