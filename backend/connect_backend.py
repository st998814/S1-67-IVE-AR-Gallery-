#!/usr/bin/env python3
"""Start and verify backend connectivity for Unity local testing."""

from __future__ import annotations

import argparse
import http.client
import subprocess
import sys
import time
from pathlib import Path

DEFAULT_HOST = "localhost"
DEFAULT_PORT = 5050
CHECK_PATH = "/api/upload"


def check_backend(host: str, port: int, timeout: float = 1.0) -> tuple[bool, int | None]:
    """Return (is_up, status_code)."""
    conn = None
    try:
        conn = http.client.HTTPConnection(host, port, timeout=timeout)
        conn.request("GET", CHECK_PATH)
        resp = conn.getresponse()
        status = resp.status
        # /api/upload only accepts POST, so 405 means service is healthy.
        return status in (200, 400, 404, 405), status
    except OSError:
        return False, None
    finally:
        if conn is not None:
            conn.close()


def wait_ready(host: str, port: int, wait_seconds: float) -> tuple[bool, int | None]:
    deadline = time.time() + wait_seconds
    last_status = None
    while time.time() < deadline:
        ok, status = check_backend(host, port)
        if ok:
            return True, status
        last_status = status
        time.sleep(0.5)
    return False, last_status


def start_backend_process(app_py: Path) -> subprocess.Popen[bytes]:
    return subprocess.Popen([sys.executable, str(app_py)], cwd=str(app_py.parent))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Connect Unity to local backend by starting and checking Flask service."
    )
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Backend host, default: {DEFAULT_HOST}")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help=f"Backend port, default: {DEFAULT_PORT}")
    parser.add_argument(
        "--wait-seconds",
        type=float,
        default=12.0,
        help="Max seconds to wait for backend readiness.",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="Only check whether backend is reachable; do not start app.py.",
    )
    args = parser.parse_args()

    ok, status = check_backend(args.host, args.port)
    if ok:
        print(f"[OK] Backend already reachable: http://{args.host}:{args.port}{CHECK_PATH} (status={status})")
        return 0

    if args.check_only:
        print(f"[FAIL] Backend not reachable at http://{args.host}:{args.port}")
        return 1

    app_py = Path(__file__).resolve().with_name("app.py")
    if not app_py.exists():
        print(f"[FAIL] Cannot find backend entry: {app_py}")
        return 2

    print(f"[INFO] Starting backend: {app_py}")
    proc = start_backend_process(app_py)

    ready, ready_status = wait_ready(args.host, args.port, args.wait_seconds)
    if not ready:
        print(f"[FAIL] Backend did not become ready in {args.wait_seconds:.1f}s")
        proc.terminate()
        try:
            proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            proc.kill()
        return 3

    print(
        f"[OK] Backend connected for Unity: http://{args.host}:{args.port} "
        f"(check endpoint status={ready_status})"
    )
    print("[INFO] Keep this process running while using Unity. Press Ctrl+C to stop backend.")

    try:
        return proc.wait()
    except KeyboardInterrupt:
        print("\n[INFO] Stopping backend...")
        proc.terminate()
        try:
            proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            proc.kill()
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
