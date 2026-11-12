"""JSON-only agent client. Example: python3 -m backtest_lab.agent preflight T002."""
from __future__ import annotations

import argparse
import http.client
import json
import os
from pathlib import Path
import stat
import sys
import time
from urllib.parse import urlencode, urlsplit, quote


class ClientError(Exception):
    def __init__(self, body, exit_code=2):
        self.body, self.exit_code = body, exit_code


class Client:
    def __init__(self, session_file=None):
        path = Path(session_file or Path.home() / ".config/xauusd-backtest-lab/agent-session.json")
        try:
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
            with os.fdopen(fd) as stream:
                info = os.fstat(stream.fileno())
                if not stat.S_ISREG(info.st_mode) or info.st_mode & 0o077 or info.st_uid != os.getuid():
                    raise ValueError("Agent session file must be an owner-only regular file")
                session = json.load(stream)
            if not isinstance(session, dict) or not isinstance(session.get("url"), str):
                raise ValueError("Malformed agent session descriptor")
            parsed = urlsplit(session["url"])
            if parsed.scheme != "http" or parsed.hostname != "127.0.0.1" or not parsed.port or parsed.path or parsed.query or parsed.fragment or parsed.username or parsed.password:
                raise ValueError("Agent session must point to the local loopback server")
            self.port, self.token = parsed.port, session["token"]
            if not isinstance(self.token, str) or not 20 <= len(self.token) <= 128 or not all(c.isascii() and (c.isalnum() or c in "-_") for c in self.token):
                raise ValueError("Invalid session token")
        except (OSError, ValueError, KeyError) as error:
            raise ClientError({"code": "SESSION_UNAVAILABLE", "error": str(error), "retryable": False,
                               "next_action": "Start python3 -m backtest_lab.server in the repository."}, 4) from None

    def request(self, path, data=None):
        conn = http.client.HTTPConnection("127.0.0.1", self.port, timeout=15)
        headers = {"X-Lab-Token": self.token, "Origin": f"http://127.0.0.1:{self.port}"}
        body = None
        if data is not None:
            headers["Content-Type"] = "application/json"
            body = json.dumps(data)
        try:
            conn.request("POST" if data is not None else "GET", path, body=body, headers=headers)
            response = conn.getresponse()
            result = json.loads(response.read())
            if not isinstance(result, dict): raise ValueError("Malformed API response")
            if response.status >= 300:
                raise ClientError(result, 3 if response.status == 409 else 2)
            return result
        except (OSError, ValueError, http.client.HTTPException) as error:
            raise ClientError({"code": "SERVER_UNAVAILABLE", "error": str(error), "retryable": True,
                               "next_action": "Check that the local server is running; refresh the private session descriptor after a restart."}, 4) from None
        finally:
            conn.close()


class JsonArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        # Do not echo accidentally supplied secrets in unknown argument values.
        print(json.dumps({"code": "INVALID_ARGUMENTS", "error": "Invalid command arguments; run --help.", "retryable": False}))
        raise SystemExit(2)


def parser():
    p = JsonArgumentParser(description=__doc__)
    p.add_argument("--session-file", type=Path)
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("capabilities")
    sub.add_parser("health")
    sub.add_parser("coverage")
    sub.add_parser("verify", help="Verify the saved cTrader login; no passwords in agent arguments")
    strategies = sub.add_parser("strategies")
    strategies.add_argument("--status", choices=["ready", "needs_data", "recorded", "all"], default="ready")
    strategies.add_argument("--offset", type=int, default=0)
    strategies.add_argument("--limit", type=int, default=25)
    for name in ["strategy", "preflight"]:
        cmd = sub.add_parser(name)
        cmd.add_argument("strategy")
        if name == "preflight": cmd.add_argument("--preset")
    start = sub.add_parser("backtest")
    start.add_argument("strategy")
    start.add_argument("--idempotency-key", required=True)
    start.add_argument("--branch", choices=["primary", "placebo"], default="primary")
    start.add_argument("--start", default="2020-05-06")
    start.add_argument("--end", default="2026-10-24")
    start.add_argument("--source-sha256", dest="expected_source_sha256")
    start.add_argument("--preset")
    jobs = sub.add_parser("jobs")
    jobs.add_argument("--offset", type=int, default=0)
    jobs.add_argument("--limit", type=int, default=25)
    for name in ["job", "result", "logs", "wait", "reinspect"]:
        cmd = sub.add_parser(name)
        cmd.add_argument("id")
        if name == "logs":
            cmd.add_argument("--cursor", type=int, default=0)
            cmd.add_argument("--limit", type=int, default=8192)
        if name == "wait": cmd.add_argument("--timeout", type=float, default=30)
    return p


def dispatch(client, args):
    command = args.command
    if command in {"capabilities", "health", "coverage"}:
        return client.request("/api/v1/" + command)
    if command == "verify": return client.request("/api/v1/verify", {})
    if command == "strategies":
        query = {"offset": args.offset, "limit": args.limit}
        if args.status != "all": query["status"] = args.status
        return client.request("/api/v1/strategies?" + urlencode(query))
    if command == "strategy": return client.request("/api/v1/strategies/" + quote(args.strategy, safe=""))
    if command == "preflight": return client.request("/api/v1/preflight", {"strategy": args.strategy, **({"preset": args.preset} if args.preset else {})})
    if command == "backtest":
        data = {key: getattr(args, key) for key in ["strategy", "idempotency_key", "branch", "start", "end"]}
        if args.expected_source_sha256: data["expected_source_sha256"] = args.expected_source_sha256
        if args.preset: data["preset"] = args.preset
        return client.request("/api/v1/jobs", data)
    if command == "jobs": return client.request("/api/v1/jobs?" + urlencode({"offset": args.offset, "limit": args.limit}))
    path = "/api/v1/jobs/" + quote(args.id, safe="")
    if command == "logs": return client.request(path + "/logs?" + urlencode({"cursor": args.cursor, "limit": args.limit}))
    if command == "result": return client.request(path + "/result")
    if command == "reinspect": return client.request(path + "/reinspect", {})
    if command == "job": return client.request(path)
    if not 0 <= args.timeout <= 60:
        raise ClientError({"code": "INVALID_REQUEST", "error": "Wait timeout must be between 0 and 60 seconds", "retryable": False})
    deadline = time.monotonic() + args.timeout
    while True:
        job = client.request(path)
        if job["terminal"]: return job
        remaining = deadline - time.monotonic()
        if remaining <= 0: return {**job, "wait_timed_out": True}
        time.sleep(min(job["poll_after_seconds"], remaining))


def main(argv=None):
    args = parser().parse_args(argv)
    try:
        result = dispatch(Client(args.session_file), args)
        code = 3 if result.get("can_launch") is False else 5 if result.get("status") in {"failed", "interrupted"} else 0
    except ClientError as error:
        result, code = error.body, error.exit_code
    print(json.dumps(result, ensure_ascii=False, allow_nan=False, separators=(",", ":")))
    return code


if __name__ == "__main__": sys.exit(main())
