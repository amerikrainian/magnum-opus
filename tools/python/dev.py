"""The dev server from the command line (standard library only).

    uv run python tools/python/dev.py launch          start the game, wait for the dev server
    uv run python tools/python/dev.py kill            close the game (WM_CLOSE, then force)
    uv run python tools/python/dev.py key Down Enter  real key presses, posted to the game window
    uv run python tools/python/dev.py key ctrl+shift+m
    uv run python tools/python/dev.py click [x y]     a left click in client coordinates (default 300,300)
    uv run python tools/python/dev.py get /nav        any GET route (/screen /gui /log?grep=x /speech ...)
    uv run python tools/python/dev.py post /input ui.down
    uv run python tools/python/dev.py eval "N.Name(N.Top)"
    uv run python tools/python/dev.py speech [--since N]
    uv run python tools/python/dev.py screenshot

Keys go through the dev server (POST /input "dev.key:<chord>"): the module injects the press into
its own keyboard snapshot for one frame. Nothing here ever brings the game window to the
foreground (the user works on the same machine), and launch shows the window without activating it.
A key the GAME itself must see goes through the mod as well (GameKeys.Forced), never the OS.
"""
import ctypes
import subprocess
import sys
import time
import urllib.request
from ctypes import wintypes

PORT = 8776
BASE = f"http://127.0.0.1:{PORT}"
GAME_DIR = r"C:\Program Files (x86)\Steam\steamapps\common\Opus Magnum"

user32 = ctypes.WinDLL("user32", use_last_error=True)
WM_CLOSE = 0x10
WM_LBUTTONDOWN, WM_LBUTTONUP = 0x201, 0x202


def game_window():
    hwnds = []
    proto = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    def cb(hwnd, _):
        buf = ctypes.create_unicode_buffer(256)
        user32.GetWindowTextW(hwnd, buf, 256)
        if buf.value == "Opus Magnum" and user32.IsWindowVisible(hwnd):
            hwnds.append(hwnd)
        return True

    user32.EnumWindows(proto(cb), 0)
    return hwnds[0] if hwnds else None


def normalize(route):
    # Git Bash rewrites a leading "/nav" into "C:/Program Files/Git/nav": undo that, and accept
    # routes written without the slash.
    marker = "/Git/"
    if ":" in route[:3] and marker in route:
        route = route[route.index(marker) + len(marker) - 1:]
    return route if route.startswith("/") else "/" + route


def http(method, route, body=None, timeout=60):
    route = normalize(route)
    data = body.encode("utf-8") if body is not None else None
    req = urllib.request.Request(BASE + route, data=data, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def wait_health(seconds=90):
    end = time.time() + seconds
    while time.time() < end:
        try:
            http("GET", "/health", timeout=2)
            return True
        except Exception:
            time.sleep(0.5)
    return False


def press(chord):
    print(http("POST", "/input", "dev.key:" + chord).strip())
    time.sleep(0.12)  # a few frames, so consecutive presses land on separate frames


def launch():
    # SW_SHOWNOACTIVATE through STARTUPINFO: Windows applies it to the process's first ShowWindow,
    # so the game appears without taking the foreground.
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 4
    subprocess.Popen([GAME_DIR + r"\Lightning.exe"], cwd=GAME_DIR, startupinfo=si)


def main(argv):
    if not argv:
        print(__doc__)
        return 1
    cmd, rest = argv[0], argv[1:]
    if cmd == "launch":
        launch()
        print("dev server up" if wait_health() else "dev server did not come up")
    elif cmd == "kill":
        hwnd = game_window()
        if hwnd:
            user32.PostMessageW(hwnd, WM_CLOSE, 0, 0)
            time.sleep(4)
        if game_window():
            subprocess.run(["taskkill", "/F", "/IM", "Lightning.exe"], capture_output=True)
            print("killed")
        else:
            print("closed")
    elif cmd == "key":
        for chord in rest:
            press(chord)
    elif cmd == "click":
        x, y = (int(rest[0]), int(rest[1])) if len(rest) >= 2 else (300, 300)
        hwnd = game_window()
        lp = (y << 16) | x
        user32.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
        user32.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)
    elif cmd == "get":
        sys.stdout.write(http("GET", rest[0]))
    elif cmd == "post":
        sys.stdout.write(http("POST", rest[0], rest[1] if len(rest) > 1 else ""))
    elif cmd == "eval":
        sys.stdout.write(http("POST", "/eval", rest[0]))
    elif cmd == "speech":
        since = rest[rest.index("--since") + 1] if "--since" in rest else "0"
        sys.stdout.write(http("GET", "/speech?since=" + since))
    elif cmd == "screenshot":
        sys.stdout.write(http("GET", "/screenshot"))
    else:
        print(__doc__)
        return 1
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main(sys.argv[1:]))
