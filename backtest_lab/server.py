"""Loopback-only research app. Run: python3 -m backtest_lab.server"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import re
import secrets
import signal
import shutil
import subprocess
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs

ROOT = Path(__file__).resolve().parents[1]
STATIC = Path(__file__).resolve().parent
READY = {
    "T002": ("EXP-017", "Tokyo pressure fade", "EXP017_START", {"Mode": "Fade"}),
    "T006": ("EXP-018", "Equity shock → gold", "T006_START", {"ReferenceSymbol": "US500.cash"}),
    "T015": ("EXP-019", "Round-number rejection", "T015_START", {"Levels": "RoundHundreds"}),
    "DATA_TICKS": ("DATA-001", "Broker tick-history metadata", "HISTORY_PROBE_START", {}),
    "DATA_FEATURES": ("DATA-002", "Tick-derived research features", "FEATURE_EXPORT_START", {}),
}
UTILITIES = {"DATA_TICKS": "XauUsdTickHistoryAvailability.cs", "DATA_FEATURES": "XauUsdResearchFeatureExport.cs"}
PREVIOUS = {"T074": "EXP-001", "T075": "EXP-009", "T079": "EXP-002"}
LEGACY = {
    "XauUsdNy30OrbBaseline.cs": "EXP-003", "XauUsdNy5OrbBenchmark.cs": "EXP-004",
    "XauUsdNy30WidthFilteredOrb.cs": "EXP-005", "XauUsdNy15RetestOrb.cs": "EXP-006",
    "XauUsdNy15ImmediateStopOrb.cs": "EXP-007", "XauUsdGcCloseMomentum.cs": "EXP-008",
    "XauUsdGcDaytimeShort.cs": "EXP-011", "XauUsdGcDaytimeShortOnly.cs": "EXP-011",
    "XauUsdGldR5CloseMomentum.cs": "EXP-012", "XauUsdExtremeMoveReversal.cs": "EXP-013",
    "XauUsdLondonRsiOrb.cs": "EXP-014",
}
TERMINAL_STATES = {"needs_review", "failed", "interrupted"}


class LabError(ValueError):
    def __init__(self, code, message, status=400, retryable=False):
        super().__init__(message)
        self.code, self.status, self.retryable = code, status, retryable


def capabilities():
    return {
        "api_version": "1", "name": "Gold Research Lab", "engine": "cTrader CLI",
        "operations": {"health": "GET /api/v1/health", "strategies": "GET /api/v1/strategies", "coverage": "GET /api/v1/coverage",
                       "strategy": "GET /api/v1/strategies/{id}", "preflight": "POST /api/v1/preflight",
                       "verify_saved_login": "POST /api/v1/verify", "start": "POST /api/v1/jobs",
                       "jobs": "GET /api/v1/jobs", "job": "GET /api/v1/jobs/{id}",
                       "logs": "GET /api/v1/jobs/{id}/logs?cursor=0&limit=8192",
                       "result": "GET /api/v1/jobs/{id}/result",
                       "reinspect_artifacts": "POST /api/v1/jobs/{id}/reinspect"},
        "backtest_request_schema": {"type": "object", "additionalProperties": False,
            "required": ["strategy", "idempotency_key"], "properties": {
                "strategy": {"type": "string", "enum": sorted(set(READY) | set(json.loads((ROOT / "ctrader/catalog-coverage.json").read_text())))},
                "branch": {"type": "string", "enum": ["primary", "placebo"], "default": "primary"},
                "start": {"type": "string", "format": "date", "default": "2020-05-06"},
                "end": {"type": "string", "format": "date", "default": "2026-10-24"},
                "idempotency_key": {"type": "string", "pattern": "^[A-Za-z0-9_.:-]{1,128}$"},
                "expected_source_sha256": {"type": "string", "pattern": "^[a-f0-9]{64}$"},
                "preset": {"type": "string", "maxLength": 100}}},
        "states": {"active": ["running"], "terminal": sorted(TERMINAL_STATES)},
        "poll_after_seconds": 3, "max_parallel_jobs": 1,
        "invariants": ["backtests_only", "server_ticks", "no_parameter_sweeps", "no_automatic_strategy_promotion"],
        "engine_profile": {"invocation": "compatible_minimal", "history_cache": "CLI-managed",
                           "cost_validation": "Verify spread, commission type, commission value, currency and balance in the native report."},
        "result_semantics": "Terminal means execution ended. needs_review is not a validated strategy result.",
        "authentication": "X-Lab-Token; local agent-session.json descriptor has the URL and token. Never print the token.",
        "retry_policy": "Reads may be retried. Retry a launch only with the identical idempotency key and request.",
    }


def now():
    return datetime.now(timezone.utc).isoformat()


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read_report(path):
    """Read native cTrader JSON or the embedded JSON in an HTML report."""
    text = Path(path).read_text(encoding="utf-8-sig")
    if Path(path).suffix == ".html":
        match = re.search(r'<script\b[^>]*id=[\"\']backtesting-report[\"\'][^>]*>(.*?)</script>', text, re.S)
        if not match:
            raise ValueError("No embedded cTrader report found")
        text = match[1]
    report = json.loads(text)
    if not isinstance(report, dict) or not isinstance(report.get("main"), dict):
        raise ValueError("Unrecognised native report schema")
    return report


def metric(value):
    return value.get("all") if isinstance(value, dict) else value


def summary(report):
    main, stats, equity = report["main"], report.get("tradeStatistics", {}), report.get("equity", {})
    return {
        "robot": main.get("cBotName"), "symbol": main.get("symbol"),
        "period": main.get("testingPeriod", {}).get("formatted"),
        "data_mode": main.get("data", {}).get("type"), "net": main.get("netProfit"),
        "roi": main.get("roi"), "trades": metric(stats.get("totalTrades")),
        "profit_factor": metric(stats.get("profitFactor")),
        "drawdown": equity.get("maxEquityDrawdownPercent"),
    }


def catalog():
    mapping = json.loads((ROOT / "ctrader/catalog-coverage.json").read_text())
    index = (ROOT / "ctrader/CATALOG-INDEX.md").read_text()
    titles = dict(re.findall(r"\| (T\d{3}) — (.*?) \|", index))
    rows = []
    for thesis, filename in sorted(mapping.items()):
        source = ROOT / "ctrader" / filename
        text = source.read_text()
        empty = [name for args, name in re.findall(
            r"\[Parameter\((.*?)\)\]\s*public\s+\S+\s+(\w+)", text, re.S
        ) if re.search(r'DefaultValue\s*=\s*""', args)]
        header = re.search(r"/// <summary>(.*?)/// </summary>", text, re.S)
        contract = re.sub(r"\s+", " ", re.sub(r"///\s*", "", header[1])).strip() if header else ""
        rows.append({"id": thesis, "name": titles.get(thesis, source.stem), "source": filename,
                     "status": "ready" if thesis in READY else "recorded" if thesis in PREVIOUS else "needs_data",
                     "required_inputs": empty, "contract": contract, "sha256": digest(source),
                     "experiment": READY[thesis][0] if thesis in READY else PREVIOUS.get(thesis)})
    return rows


def report_audit(report, expected_robot, start, end):
    """A report is reviewable only if it matches this job and its tick-data contract."""
    problems = []
    main = report["main"]
    if main.get("cBotName") not in {expected_robot, "XAUUSD", "CLIProjects"}:
        problems.append("Report robot name differs from the verified package; inspect identity")
    if main.get("symbol") != "XAUUSD":
        problems.append("Report symbol is not XAUUSD")
    if main.get("data", {}).get("type") not in {"ticks", "ticksFromServer", "tickDataFromServer"}:
        problems.append("Historical server-tick data mode has not been verified")
    period = main.get("testingPeriod", {})
    for key, wanted in [("startDate", start), ("endDate", end)]:
        raw = period.get(key)
        try:
            actual = datetime.fromtimestamp(float(raw) / 1000, timezone.utc).date().isoformat()
        except (ValueError, TypeError, OverflowError):
            actual = None
        if actual != wanted:
            problems.append(f"Reported {key} does not match requested date")
    commission, spread = main.get("commissions") or {}, main.get("spread") or {}
    if commission.get("value") != 30 or str(commission.get("type", "")).lower() != "usdpermillionusdvolume":
        problems.append("Native commission does not confirm USD 30 per million USD volume")
    if spread.get("type") != "fixed" or spread.get("value") != 1:
        problems.append("Native report does not confirm the fixed 1-pip spread scenario")
    if main.get("startingCapital") != 10000 or main.get("depositAsset") != "USD":
        problems.append("Native report does not confirm USD 10,000 initial balance")
    return problems


def execution_failure(return_code, log):
    if "AlgoMetadataUnavailableException" in log:
        return {"code": "ALGO_NOT_INSTALLED", "message": "The CLI could not read installed robot metadata.", "retryable": False,
                "next_action": "Stage the verified package in cAlgo/Sources/Robots and preserve any previous installed file."}
    if "Unknown command line parameters" in log or "is not allowed" in log:
        return {"code": "CLI_UNSUPPORTED_OPTIONS", "message": "The installed CLI rejected invocation options.",
                "retryable": False, "next_action": "Correct the invocation for this installed CLI; preserve this attempt and use a new request key."}
    if return_code == 86 or "No historical data for the specified period" in log:
        return {"code": "HISTORY_UNAVAILABLE", "message": "cTrader did not supply historical data for the requested period.",
                "retryable": False, "next_action": "Check broker history availability with separately labelled diagnostic probes. Keep the full-period experiment incomplete; do not substitute a shorter run as its result."}
    if return_code == 80 or "Connection can't be established" in log:
        return {"code": "BROKER_UNAVAILABLE", "message": "The cTrader server connection failed.",
                "retryable": True, "next_action": "Verify connectivity; preserve this failed attempt. A deliberately new run needs a new request key."}
    if "target framework" in log.lower() or "not supported" in log.lower():
        return {"code": "ENGINE_COMPATIBILITY_ERROR", "message": "The native engine rejected the package or runtime.",
                "retryable": False, "next_action": "Inspect the native log and rebuild the unchanged source with a compatible official compiler."}
    return {"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.",
            "retryable": False, "next_action": "Inspect the native log and artifacts before planning another attempt."}


def native_report_finished(directory, robot):
    """Proof of native completion, not a timeout or mere report-file existence."""
    try:
        report = read_report(directory / "report.json")
        main = report["main"]
        if main.get("cBotName") != robot or main.get("symbol") != "XAUUSD" or main.get("titleKind") != "backtesting":
            return False
        if not {"tradeStatistics", "history", "positions"} <= report.keys(): return False
        with (directory / "console.log").open("rb") as stream:
            stream.seek(max(0, (directory / "console.log").stat().st_size - 65536))
            tail = stream.read().decode("utf-8", errors="replace")
        return f"CBot instance [{robot}, XAUUSD, m1] stopped." in tail
    except (OSError, ValueError, KeyError):
        return False


def live_progress(directory, job):
    path = directory / "console.log"
    text = ""
    if path.exists():
        with path.open("rb") as stream:
            stream.seek(max(0, path.stat().st_size - 262144))
            text = stream.read().decode("utf-8", errors="replace")
    percentages = re.findall(r"Progress\s*\|\s*Backtesting\s*\|\s*([\d.]+)\s*%", text)
    value = float(percentages[-1]) if percentages else None
    if value is not None and not 0 <= value <= 100: value = None
    metrics = None
    curve = []
    observer_file = directory / "equity-live.csv"
    if observer_file.is_file() and job.get("status") == "running":
        try:
            with observer_file.open(newline="") as stream:
                rows = list(csv.DictReader(stream))
            stride = max(1, math.ceil(len(rows) / 800))
            selected = rows[::stride]
            if rows and (not selected or selected[-1] is not rows[-1]): selected.append(rows[-1])
            for row in selected:
                try:
                    point = {"utc": row["utc"], "balance": float(row["balance"]), "equity": float(row["equity"]),
                             "net_profit": float(row["net_profit"]), "closed_positions": int(row["closed_positions"])}
                    if all(math.isfinite(point[k]) for k in ("balance", "equity", "net_profit")): curve.append(point)
                except (KeyError, TypeError, ValueError): continue
            if curve: metrics = curve[-1]
        except (OSError, csv.Error):
            curve = []
    pattern = r"LAB_LIVE_V1,utc=([^,\r\n]+),balance=([^,]+),equity=([^,]+),net_profit=([^,]+),closed_positions=(\d+)"
    for match in (() if metrics else re.finditer(pattern, text)):
        try:
            numbers = list(map(float, match.group(2, 3, 4)))
            if not all(map(math.isfinite, numbers)): continue
            metrics = dict(utc=match[1], balance=numbers[0], equity=numbers[1], net_profit=numbers[2], closed_positions=int(match[5]))
        except ValueError: continue
    return {"percent": value, "phase": job.get("phase", "replaying" if value is not None else "connecting"),
            "metrics": metrics, "metrics_status": "observed_snapshot" if metrics else "awaiting_native_snapshot",
            "equity_curve": curve,
            "updated_utc": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat() if path.exists() else None}


def experiment_rows(root):
    path = root / "04 - Planning/Experiment Register.md"
    rows = {}
    if path.exists():
        for line in path.read_text().splitlines():
            if not re.match(r"\| EXP-\d+ \|", line): continue
            cells = [part.strip() for part in line.split("|")[1:-1]]
            if len(cells) >= 8:
                rows[cells[0]] = {"id": cells[0], "title": cells[2], "result": cells[6], "note": cells[7]}
    return rows


class Lab:
    def __init__(self, root=ROOT, profile_dir=None, install_dir=None):
        self.root = Path(root)
        self.profile_dir = Path(profile_dir or Path.home() / ".config/xauusd-backtest-lab")
        self.profile_path = self.profile_dir / "profile.json"
        self.password_path = self.profile_dir / "credentials.pwd"
        self.install_dir = Path(install_dir or Path.home() / "cAlgo/Sources/Robots")
        self.output = self.root / "outputs/backtest-lab"
        self.output.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()
        self.jobs = []
        self.connection = {"status": "not_connected", "code": "AUTH_REQUIRED", "retryable": False,
                           "message": "Connect your cTrader ID to fetch FTMO history."}
        self.cli = shutil.which("ctrader-cli") or str(Path.home() / ".local/bin/ctrader-cli")
        self.secret = secrets.token_urlsafe(32)
        self._report_cache = {}

    def publish_agent_session(self, port):
        self.profile_dir.mkdir(parents=True, mode=0o700, exist_ok=True)
        self.profile_dir.chmod(0o700)
        path = self.profile_dir / "agent-session.json"
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as stream:
            os.fchmod(stream.fileno(), 0o600)
            json.dump({"api_version": "1", "url": f"http://127.0.0.1:{port}", "token": self.secret,
                       "pid": os.getpid(), "repository": str(self.root)}, stream)
        return path

    def profile(self):
        if self.profile_path.exists():
            return json.loads(self.profile_path.read_text())
        return {}

    def save_connection(self, data):
        ctid, account = str(data.get("ctid", "")).strip(), str(data.get("account", "")).strip()
        password = data.get("password", "")
        if not ctid or len(ctid) > 254 or not account.isdigit():
            raise ValueError("Enter your cTrader ID and numeric FTMO account number")
        if not isinstance(password, str) or not password or "\n" in password or "\r" in password:
            raise ValueError("Enter your cTrader ID password")
        if self.connection["status"] == "checking" or self.unfinished_jobs():
            raise ValueError("Wait for the current task before changing the connection")
        self.profile_dir.mkdir(parents=True, mode=0o700, exist_ok=True)
        self.profile_dir.chmod(0o700)
        broker = str(data.get("broker", "FTMO Platform")).strip()
        if not broker or len(broker) > 100 or "\n" in broker:
            raise ValueError("Enter the broker name shown by cTrader")
        for path, content in [(self.password_path, password), (self.profile_path, json.dumps({"ctid": ctid, "account": account, "broker": broker}))]:
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, "w") as stream:
                os.fchmod(stream.fileno(), 0o600)
                stream.write(content)
        self.verify_connection()

    def auth_args(self):
        profile = self.profile()
        if not profile.get("ctid") or not profile.get("account") or not self.password_path.is_file():
            raise ValueError("Connect your cTrader ID first")
        return [f"--ctid={profile['ctid']}", f"--pwd-file={self.password_path}", f"--account={profile['account']}",
                f"--broker={profile.get('broker', 'FTMO Platform')}"]

    def verify_connection(self):
        if not Path(self.cli).is_file():
            raise ValueError("cTrader CLI is not installed")
        # This installed batch `accounts` command rejects --account; list linked
        # accounts and then verify the user's selected number in its JSON.
        args = [arg for arg in self.auth_args() if not arg.startswith("--account=")]
        if self.connection["status"] == "checking":
            raise LabError("AUTH_CHECK_IN_PROGRESS", "An account check is already running", 409, True)
        self.connection = {"status": "checking", "code": "AUTH_CHECK_IN_PROGRESS", "retryable": True,
                           "message": "Checking the selected account with cTrader…"}

        def check():
            try:
                result = subprocess.run([self.cli, "accounts", *args], capture_output=True, text=True, timeout=120)
                output = result.stdout + result.stderr
                account = self.profile()["account"]
                # The CLI echoes its command. Only inspect the JSON payload after the echo.
                if result.returncode:
                    if "connection has been lost" in output.lower() or result.returncode == 80:
                        raise LabError("BROKER_UNAVAILABLE", "cTrader CLI lost its server connection (exit 80). Account verification and history download have not completed.", 503, True)
                    if "Parameter" in output and "not allowed" in output:
                        raise LabError("CLI_CONFIG_ERROR", "The installed CLI rejected connection options; inspect its local command reference.")
                    if "broker" in output.lower() and ("unknown" in output.lower() or "not found" in output.lower()):
                        raise ValueError("Broker name was not recognised. Enter the exact cTrader broker name.")
                    raise ValueError("cTrader could not authenticate or reach the broker. Check your cTrader ID login and broker name.")
                match = re.search(r"(?m)^[\[{]", output)
                if not match:
                    raise ValueError("cTrader did not return an account response. Check login and network access.")
                payload, _ = json.JSONDecoder().raw_decode(output[match.start():])
                def contains(value):
                    if isinstance(value, dict): return any(contains(v) for v in value.values())
                    if isinstance(value, list): return any(contains(v) for v in value)
                    return str(value) == account
                if result.returncode or not contains(payload):
                    raise ValueError("The selected account was not verified. Check your cTrader ID and account number.")
                self.connection = {"status": "connected", "code": "CONNECTED", "retryable": False,
                                   "message": "Account verified. Historical backtests are available."}
            except Exception as error:
                code = error.code if isinstance(error, LabError) else "BROKER_TIMEOUT" if isinstance(error, subprocess.TimeoutExpired) else "ACCOUNT_UNVERIFIED"
                self.connection = {"status": "error", "code": code,
                    "retryable": error.retryable if isinstance(error, LabError) else isinstance(error, subprocess.TimeoutExpired),
                    "message": str(error) if isinstance(error, ValueError) else "Connection failed or timed out. Check login and network access."}
        threading.Thread(target=check, daemon=True).start()

    def package(self, source):
        # Prefer native CLI-built packages when present; retain legacy packages for inspection.
        native = self.root / "ctrader/bin/Lab" / source.stem / (source.stem + ".algo")
        if native.is_file():
            identity = native.with_suffix(".identity.json")
            if identity.exists():
                proof = json.loads(identity.read_text())
                if proof.get("source_sha256") == digest(source) and proof.get("package_sha256") == digest(native):
                    generated = self.root / proof.get("generated_source", "")
                    if proof.get("generated_source_sha256") and (not generated.is_file() or digest(generated) != proof["generated_source_sha256"]):
                        raise LabError("PACKAGE_UNVERIFIED", "Observer source does not match its build identity", 409)
                    return native
        legacy = self.root / "ctrader/bin/Packaged" / source.stem / "Debug/net6.0/XAUUSD.algo"
        proof_path = self.root / "ctrader/packaging-results-20261107.json"
        if legacy.exists() and proof_path.exists():
            for proof in json.loads(proof_path.read_text())["records"]:
                if proof["source"] == source.name and proof["source_sha256"] == digest(source):
                    if proof.get("packages", {}).get(str(legacy.relative_to(self.root))) == digest(legacy):
                        return legacy
        raise LabError("PACKAGE_UNVERIFIED", "A matching verified package is required; build this source before running", 409)

    def frozen_preset(self, thesis, preset):
        if not isinstance(preset, str) or not re.fullmatch(r"[A-Za-z0-9_.-]{1,100}", preset):
            raise LabError("PRESET_REQUIRED", "Select an explicitly frozen research preset", 409)
        path = self.root / "configs/backtest-presets.json"
        entries = json.loads(path.read_text()).get("presets", {}) if path.exists() else {}
        item = entries.get(preset)
        if not item or item.get("strategy") != thesis:
            raise LabError("PRESET_NOT_FOUND", "No frozen preset for this strategy", 409)
        source = self.root / "ctrader" / item["source"]
        if source.parent != self.root / "ctrader" or digest(source) != item["source_sha256"]:
            raise LabError("SOURCE_CHANGED", "Frozen preset source changed", 409)
        for relative, fingerprint in item.get("inputs", {}).items():
            data = (self.root / relative).resolve()
            if not data.is_relative_to(self.root.resolve()) or not data.is_file() or digest(data) != fingerprint:
                raise LabError("INPUT_CHANGED", "Frozen input is missing or changed: " + relative, 409)
        parameters = item.get("parameters", {})
        required = [name for args, name in re.findall(r"\[Parameter\((.*?)\)\]\s*public\s+\S+\s+(\w+)", source.read_text(), re.S)
                    if re.search(r'DefaultValue\s*=\s*""', args)]
        if any(not str(parameters.get(name, "")).strip() for name in required):
            raise LabError("INPUTS_REQUIRED", "Preset does not supply all required inputs", 409)
        return item

    def preflight(self, thesis, preset=None):
        row = next((row for row in catalog() if row["id"] == thesis), None)
        if thesis in UTILITIES:
            source = self.root / "ctrader" / UTILITIES[thesis]
            row = {"id": thesis, "source": source.name, "sha256": digest(source), "required_inputs": []}
        if row is None: raise LabError("STRATEGY_NOT_FOUND", "Unknown strategy", 404)
        blockers = []
        frozen = None
        if preset:
            try: frozen = self.frozen_preset(thesis, preset)
            except (ValueError, OSError) as error:
                blockers.append({"code": getattr(error, "code", "PRESET_INVALID"), "message": str(error), "retryable": False})
        if thesis not in READY and not frozen:
            blockers.append({"code": "INPUTS_REQUIRED" if row["required_inputs"] else "NOT_SCHEDULED",
                             "message": "Supply audited inputs and a frozen specification before launching.", "retryable": False,
                             "required_inputs": row["required_inputs"]})
        if not Path(self.cli).is_file(): blockers.append({"code": "CLI_MISSING", "message": "Install the official cTrader CLI.", "retryable": False})
        if self.connection["status"] != "connected":
            blockers.append({"code": self.connection.get("code", "AUTH_REQUIRED"),
                             "message": self.connection["message"], "retryable": self.connection.get("retryable", False)})
        if self.unfinished_jobs():
            owned = any(j["status"] == "running" for j in self.jobs)
            blockers.append({"code": "RUN_BUSY" if owned else "PROCESS_INSPECTION_REQUIRED",
                             "message": "An unfinished job must be inspected or completed.", "retryable": owned})
        package = None
        if thesis in READY or frozen:
            try: package = self.package(self.root / "ctrader" / row["source"])
            except (ValueError, OSError) as error:
                blockers.append({"code": "PACKAGE_UNVERIFIED", "message": str(error), "retryable": False})
        return {"strategy": thesis, "can_launch": not blockers, "blockers": blockers,
                "source_sha256": row["sha256"], "package_sha256": digest(package) if package else None,
                "defaults": {"start": "2020-05-06", "end": "2026-10-24", "branch": "primary", "data_mode": "ticks"},
                "compatibility": "Native execution and coverage are unverified until an actual run is audited."}

    def unfinished_jobs(self):
        for path in self.output.glob("*/job.json"):
            if json.loads(path.read_text()).get("status") == "running":
                return True
        return False

    def start_job(self, data):
        thesis = data.get("strategy")
        preset_name = data.get("preset")
        frozen = self.frozen_preset(thesis, preset_name) if preset_name else None
        if not isinstance(thesis, str) or (thesis not in READY and not frozen):
            raise LabError("INPUTS_REQUIRED", "This strategy requires audited external inputs before it can run", 409)
        start, end = data.get("start", "2020-05-06"), data.get("end", "2026-10-24")
        try:
            begin = datetime.strptime(start, "%Y-%m-%d")
            finish = datetime.strptime(end, "%Y-%m-%d")
        except (ValueError, TypeError):
            raise ValueError("Choose valid start and end dates") from None
        if begin >= finish or finish.date() > datetime.now(timezone.utc).date():
            raise ValueError("Require start before end, with no future dates")
        if frozen and (start != frozen["start"] or end != frozen["end"]):
            raise LabError("PRESET_DATES_CHANGED", "Use the frozen preset dates", 409)
        branch = data.get("branch", "primary")
        if not isinstance(branch, str) or branch not in {"primary", "placebo"} or (branch == "placebo" and thesis != "T015"):
            raise ValueError("Invalid comparison branch")
        key = data.get("idempotency_key")
        if key is not None and (not isinstance(key, str) or not re.fullmatch(r"[A-Za-z0-9_.:-]{1,128}", key)):
            raise ValueError("Invalid idempotency key")
        expected = data.get("expected_source_sha256")
        if expected is not None and (not isinstance(expected, str) or not re.fullmatch(r"[a-f0-9]{64}", expected)):
            raise ValueError("Invalid source fingerprint")
        request = {"strategy": thesis, "start": start, "end": end, "branch": branch}
        if expected: request["expected_source_sha256"] = expected
        if frozen:
            request["preset"] = preset_name
            request["preset_sha256"] = hashlib.sha256(json.dumps(frozen, sort_keys=True).encode()).hexdigest()
        with self.lock:
            # Lookup precedes connection/busy checks: a lost response must never
            # cause a second execution, even after the account disconnects.
            if key:
                for path in self.output.glob("*/job.json"):
                    old = json.loads(path.read_text())
                    if old.get("idempotency_key") == key:
                        if old.get("request") != request:
                            raise LabError("IDEMPOTENCY_CONFLICT", "This idempotency key already identifies a different request", 409)
                        return old["id"]
            if self.connection["status"] != "connected":
                raise LabError("NOT_CONNECTED", "Verify your cTrader connection before running", 409)
            if self.unfinished_jobs():
                owned = any(j["status"] == "running" for j in self.jobs)
                raise LabError("RUN_BUSY" if owned else "PROCESS_INSPECTION_REQUIRED",
                               "An unfinished backtest exists; wait for its report or inspect its process before another launch", 409, owned)
            mapping = json.loads((self.root / "ctrader/catalog-coverage.json").read_text())
            mapping.update(UTILITIES)
            if frozen: mapping[thesis] = frozen["source"]
            source = self.root / "ctrader" / mapping[thesis]
            if expected and digest(source) != expected:
                raise LabError("SOURCE_CHANGED", "Source changed since preflight; inspect it and submit a new frozen request", 409)
            package = self.package(source)
            if frozen:
                experiment, title, marker, parameters = (frozen["experiment"], frozen["title"], frozen["marker"], dict(frozen["parameters"]))
            else:
                experiment, title, marker, parameters = READY[thesis]
                parameters = {**parameters, "FixedVolumeInUnits": 1, "AllowLiveTrading": False}
            if branch == "placebo": parameters["Levels"] = "NonRoundPlacebo"
            job_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "_" + secrets.token_hex(3)
            directory = self.output / job_id
            directory.mkdir()
            staged = directory / (source.stem + ".algo")
            shutil.copy2(package, staged)
            shutil.copy2(source, directory / source.name)
            identity_path = package.with_suffix(".identity.json")
            identity = json.loads(identity_path.read_text()) if identity_path.exists() else None
            if identity:
                generated = self.root / identity.get("generated_source", "")
                if generated.is_file():
                    archived = directory / (source.stem + ".observer.cs")
                    shutil.copy2(generated, archived)
                    identity["archived_generated_source"] = archived.name
                (directory / "build-identity.json").write_text(json.dumps(identity, indent=2) + "\n")
            if frozen:
                (directory / "frozen-preset.json").write_text(json.dumps(frozen, indent=2) + "\n")
            parameters["PositionLabel"] = "LAB_" + thesis + "_" + (preset_name or branch)
            declared = set(re.findall(r"\[Parameter\(.*?\)\]\s*public\s+\S+\s+(\w+)", source.read_text(), re.S))
            live_observer = bool(identity and identity.get("observer") == "LAB_LIVE_V2" and frozen)
            if live_observer: declared.add("LabObserverOutput")
            for optional in ("FixedVolumeInUnits", "AllowLiveTrading", "PositionLabel"):
                if optional not in declared: parameters.pop(optional, None)
            if set(parameters) - declared:
                raise LabError("PRESET_INVALID", "Preset names an undeclared cBot parameter", 409)
            serialized = {k: str(v) for k, v in parameters.items()}
            if live_observer: serialized["LabObserverOutput"] = str(directory / "equity-live.csv")
            if "Mode" in serialized: serialized["Mode"] = "0"
            if "Levels" in serialized: serialized["Levels"] = "1" if branch == "placebo" else "0"
            settings = {"Chart": {"Symbol": "XAUUSD", "Period": "m1"}, "Parameters": serialized}
            settings_file = directory / "parameters.cbotset"
            settings_file.write_text(json.dumps(settings, indent=2) + "\n")
            command = [self.cli, "backtest", str(staged), str(settings_file), *self.auth_args(),
                       "--symbol=XAUUSD", "--period=m1", f"--start={begin:%d/%m/%Y}", f"--end={finish:%d/%m/%Y}",
                       "--data-mode=ticks", "--balance=10000", "--commission=30",
                       "--spread=1",
                       f"--report={directory / 'report.html'}", f"--report-json={directory / 'report.json'}"]
            if thesis == "DATA_FEATURES" or live_observer: command.append("--full-access")
            job = {"id": job_id, "strategy": thesis, "title": title, "experiment": experiment, "branch": branch,
                   "status": "running", "started_utc": now(), "start": start, "end": end,
                   "directory": str(directory.relative_to(self.root)), "source_sha256": digest(source),
                   "package_sha256": digest(staged), "marker": marker, "robot": source.stem,
                   "parameters": parameters, "command": command, "message": "Starting historical tick backtest…"}
            job.update(idempotency_key=key, request=request)
            if frozen:
                job.update(preset=preset_name, preset_sha256=request["preset_sha256"], input_hashes=frozen.get("inputs", {}),
                           research_review_notes=frozen.get("review_notes", []))
            job.update(kind="data_probe" if thesis in UTILITIES or "history-probe" in (key or "") else frozen.get("kind", "strategy_test") if frozen else "strategy_test", build_identity=identity)
            self.jobs.insert(0, job)
            self.write_job(job)
            threading.Thread(target=self.execute, args=(job,), daemon=True).start()
            return job_id

    def write_job(self, job):
        path = self.root / job["directory"] / "job.json"
        temp = path.with_suffix(".tmp")
        temp.write_text(json.dumps(job, indent=2) + "\n")
        temp.replace(path)

    def execute(self, job):
        directory = self.root / job["directory"]
        try:
            self.install_package(job)
            with (directory / "console.log").open("w") as log:
                process = subprocess.Popen(job["command"], cwd=directory, stdout=log, stderr=subprocess.STDOUT)
                job["pid"] = process.pid
                self.write_job(job)
                completed_at = None
                while True:
                    try:
                        job["exit_code"] = process.wait(timeout=1)
                        break
                    except subprocess.TimeoutExpired:
                        with (directory / "console.log").open("rb") as diagnostic:
                            diagnostic.seek(max(0, (directory / "console.log").stat().st_size-16384))
                            fatal = "The error occurred while cbot creating" in diagnostic.read().decode("utf-8", errors="replace")
                        if fatal and not job.get("startup_failure_transport_closed"):
                            job["startup_failure_transport_closed"] = True
                            job["transport_closure_reason"] = "Explicit fatal native cBot-creation exception; no running strategy was created."
                            self.write_job(job)
                            process.send_signal(signal.SIGINT)
                        if native_report_finished(directory, job["robot"]):
                            if completed_at is None:
                                completed_at = time.monotonic()
                                job.update(phase="native_report_ready", message="Native cBot stopped and report is written; waiting for CLI exit.")
                                self.write_job(job)
                            elif time.monotonic() - completed_at >= 30 and not job.get("transport_closed_after_native_report"):
                                # Own Popen handle only. Never stop/restart a run merely
                                # because observation expired or a file appeared.
                                job["transport_closed_after_native_report"] = True
                                job["transport_closure_reason"] = "Matching native report plus explicit cBot stopped marker; CLI remained alive for 30 more seconds."
                                self.write_job(job)
                                process.send_signal(signal.SIGINT)
                            elif time.monotonic() - completed_at >= 45 and not job.get("transport_terminated_after_native_report"):
                                job["transport_terminated_after_native_report"] = True
                                self.write_job(job)
                                process.terminate()
            for name in ["log.txt", "events.json"]:
                candidates = list(directory.rglob(name))
                if candidates and not (directory / name).exists():
                    shutil.copy2(max(candidates, key=lambda p: p.stat().st_mtime), directory / name)
            self.audit_native_artifacts(job)
        except Exception as error:
            log_path = directory / "console.log"
            diagnostic = execution_failure(job.get("exit_code"), log_path.read_text() if log_path.exists() else "")
            job.update(status="failed", message="No verified market result: " + diagnostic["message"],
                       failure=diagnostic, detail=str(error))
        finally:
            job["finished_utc"] = now()
            job["files"] = {str(p.relative_to(directory)): {"sha256": digest(p), "bytes": p.stat().st_size}
                            for p in directory.rglob("*") if p.is_file() and p.name not in {"job.json", "job.tmp"} and p.suffix != ".algo"}
            self.write_job(job)
            self.write_note(job)

    def install_package(self, job):
        directory = self.root / job["directory"]
        staged = directory / (job["robot"] + ".algo")
        if digest(staged) != job["package_sha256"]:
            raise LabError("PACKAGE_UNVERIFIED", "Staged package changed before native launch", 409)
        destination = self.install_dir / staged.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        previous_hash = digest(destination) if destination.is_file() else None
        if previous_hash and previous_hash != job["package_sha256"]:
            backup = directory / "previous-installed-package.algo"
            shutil.copy2(destination, backup)
        if previous_hash != job["package_sha256"]: shutil.copy2(staged, destination)
        job["installation"] = {"path": str(destination), "previous_sha256": previous_hash,
                               "staged_sha256": digest(destination), "recorded_utc": now(),
                               "semantics": "The desktop application may repackage this file; confirm runtime startup/observer markers."}
        self.write_job(job)

    def audit_native_artifacts(self, job):
        directory = self.root / job["directory"]
        report_path = directory / "report.json"
        if not report_path.exists(): report_path = directory / "report.html"
        report = read_report(report_path)
        problems = report_audit(report, job["robot"], job["start"], job["end"])
        if job.get("transport_closed_after_native_report"):
            problems.append("CLI transport was interrupted only after native stopped/report completion evidence; inspect recorded closure metadata")
        elif job.get("exit_code"): problems.append("cTrader exited with an error")
        log_text = (directory / "console.log").read_text()
        if (directory / "log.txt").exists(): log_text += (directory / "log.txt").read_text()
        if job["marker"] not in log_text: problems.append("Expected strategy startup marker is missing")
        observer = (job.get("build_identity") or {}).get("observer")
        if observer and (f"LAB_OBSERVER_{observer[-2:]},original_source_sha256="+job["source_sha256"]) not in log_text:
            problems.append("Expected read-only observer/source stamp was not confirmed by the native runtime")
        if observer == "LAB_LIVE_V2" and not (directory / "equity-live.csv").is_file():
            problems.append("Live observer equity stream was not written; inspect cBot full-access and output-path logs")
        if "EXP017_TOKYO_PRESSURE_REVERSAL_V2" not in log_text and job["strategy"] == "T002":
            problems.append("Tokyo V2 build was not confirmed in the log")
        if metric(report.get("tradeStatistics", {}).get("totalTrades")) == 0:
            if job.get("kind") not in {"data_probe", "observation_test"}: problems.append("No trades: inspect eligibility and data coverage before interpreting performance")
        first = re.search(r"HISTORY_PROBE_FIRST,utc=([^,\r\n]+)", log_text)
        if first: job["history_metadata"] = {"reported_first_server_tick_utc": first[1], "semantics": "Raw API response; cross-check against the successful replay interval before inferring global history coverage."}
        problems.extend(job.get("research_review_notes", []))
        observation_count = len(set(re.findall(r"\b" + re.escape(job["strategy"]) + r"_OUTCOME,receipt=([^,\r\n]+)", log_text)))
        if job.get("kind") == "observation_test" and not observation_count:
            problems.append("No valid native observations; inspect quote coverage and eligibility")
        job.pop("failure", None)
        job.pop("detail", None)
        job.update(status="needs_review", phase="audit_complete", summary=summary(report), audit_issues=problems,
                   message="Native report saved. Review execution and coverage before accepting the result.")
        if job.get("kind") == "observation_test": job["summary"]["observations"] = observation_count

    def reinspect(self, job_id):
        job = self.get_job(job_id)
        if job["status"] not in {"failed", "needs_review"}:
            raise LabError("PROCESS_INSPECTION_REQUIRED", "Only a known terminal job can be reinspected; verify any outstanding process first", 409)
        directory = self.output / job_id
        original = json.loads((directory / "job.json").read_text())
        revised = dict(original)
        closure = directory / "native-completion-closure.json"
        if closure.exists() and native_report_finished(directory, revised["robot"]):
            record = json.loads(closure.read_text())
            if record.get("report_sha256") == digest(directory / "report.json"):
                revised["transport_closed_after_native_report"] = True
                revised["transport_closure_reason"] = record["action"]
        # Re-read immutable native evidence. Never launch/replay a strategy.
        self.audit_native_artifacts(revised)
        revision = directory / "audit-revisions" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "_" + secrets.token_hex(3) + ".json")
        revision.parent.mkdir(exist_ok=True)
        revision.write_text(json.dumps(original, indent=2) + "\n")
        revised["reinspected_utc"] = now()
        revised["previous_audit"] = str(revision.relative_to(directory))
        revised.setdefault("files", {})[str(revision.relative_to(directory))] = {"sha256": digest(revision), "bytes": revision.stat().st_size}
        self.write_job(revised)
        self.write_note(revised)
        return self.public_job(revised)

    def write_note(self, job):
        note = self.root / "04 - Planning" / f"Lab run {job['id']} - {job['strategy']}.md"
        fields = job.get("summary", {})
        text = f"---\ntags: [experiment, ctrader, backtest-lab]\nstatus: {job['status']}\n---\n\n# {job['experiment']} — {job['title']} ({job['branch']})\n\n"
        text += f"Status: **{job['status']}**. {job['message']}\n\n"
        text += f"Requested: {job['start']}–{job['end']}; exposed development history. Requested mode: historical server ticks. Costs: fixed spread 1 pip; USD 30 per million USD volume; USD 10,000 initial balance.\n\n"
        text += f"Source SHA-256: `{job['source_sha256']}`. Package SHA-256: `{job['package_sha256']}`.\n\n"
        if (job.get("build_identity") or {}).get("observer"):
            text += f"Observer build: {job['build_identity']['observer']}. Canonical strategy source is unchanged; a derived build adds account snapshot logging and an hourly timer only. The original and generated source hashes are preserved in build-identity.json.\n\n"
        if job.get("kind") == "data_probe": text += "**Data-availability diagnostic only; this is not a profitability test.**\n\n"
        if fields:
            text += "| Report field | Value |\n|---|---|\n"
            text += "".join(f"| {k} | {str(v).replace('|', '/')} |\n" for k, v in fields.items())
        text += "\n## Execution review\n\n"
        text += "\n".join("- " + p for p in job.get("audit_issues", [])) or "Manual review remains required."
        text += "\n\nNo strategy is promoted automatically. Audit native fills, holding times, protections, missing data and actual coverage before interpreting returns.\n\n"
        text += f"Artifacts and hashes: [[../{job['directory']}/job.json]].\n"
        note.write_text(text)
        job["note"] = str(note.relative_to(self.root))
        self.write_job(job)
        self.write_results_index()

    def coverage(self, rows=None, jobs=None):
        rows = rows if rows is not None else catalog()
        if jobs is None:
            jobs = [self.get_job(p.parent.name) for p in sorted(self.output.glob("*/job.json"), reverse=True)]
        prior = experiment_rows(self.root)
        items = []
        for row in rows:
            attempts = [j for j in jobs if j["strategy"] == row["id"]]
            latest = attempts[0] if attempts else None
            previous_id = PREVIOUS.get(row["id"]) if row.get("category", "catalog") == "catalog" else LEGACY.get(row["source"])
            previous = prior.get(previous_id)
            status = "needs_inputs" if row["status"] == "needs_data" else "pending"
            if previous: status = "prior_result"
            if latest:
                status = "running" if latest["status"] == "running" else "failed_attempt" if latest["status"] in {"failed", "interrupted"} else "diagnostic" if latest.get("kind") == "data_probe" or not (latest.get("summary", {}).get("trades") or 0) else "result_recorded"
            if latest and latest.get("kind") == "observation_test" and latest["status"] == "needs_review":
                status = "observation_recorded" if latest.get("summary", {}).get("observations") else "diagnostic"
            scope_path = self.root / "configs/research-scope.json"
            scope = json.loads(scope_path.read_text()) if scope_path.exists() else {}
            if row["id"] in scope.get("excluded", {}) and not latest and not previous: status = "excluded_by_user"
            items.append({"id": row["id"], "name": row["name"], "source": row["source"], "status": status,
                          "required_inputs": row["required_inputs"], "attempt_count": len(attempts),
                          "latest_job": latest["id"] if latest else None, "latest_result": latest.get("summary") if latest else None,
                          "evidence_status": latest.get("evidence_status") if latest else "prior_experiment" if previous else "not_run",
                          "prior_experiment": previous})
        return items

    def legacy_rows(self):
        rows = []
        mapping = set(json.loads((ROOT / "ctrader/catalog-coverage.json").read_text()).values())
        for source in sorted((ROOT / "ctrader").glob("XauUsd*.cs")):
            if source.name in mapping or source.name in set(UTILITIES.values()) | {"XauUsdTickExporter.cs"}: continue
            rows.append({"id": "legacy:" + source.stem, "name": source.stem.replace("XauUsd", ""), "source": source.name,
                         "status": "recorded" if source.name in LEGACY else "needs_data", "category": "legacy",
                         "contract": "Linked prior experiment; inspect its note for exact tested build identity." if source.name in LEGACY else "Observation variant requiring supplied point-in-time inputs.",
                         "required_inputs": [] if source.name in LEGACY else ["Observations"], "experiment": LEGACY.get(source.name)})
        return rows

    def write_results_index(self):
        rows = self.coverage() + self.coverage(self.legacy_rows())
        path = self.root / "04 - Planning/Backtest Lab Results.md"
        text = "---\ntags: [ctrader, experiments, results]\nstatus: active\n---\n\n# Backtest Lab — strategy run ledger\n\nGenerated from stored native jobs and the existing experiment register. A failed attempt, data probe or zero-trade warmup does not complete a strategy test. Prior experiments may use M1 data or have execution defects; inspect linked evidence.\n\n"
        text += "| Source / thesis | State | Attempts | Latest result / prior observation | Evidence |\n|---|---|---:|---|---|\n"
        for row in rows:
            result = row["latest_result"]
            observed = (f"Net {result.get('net')}; PF {result.get('profit_factor')}; trades {result.get('trades')}; data {result.get('data_mode')}" if result else row["prior_experiment"]["result"] if row["prior_experiment"] else "Missing inputs: " + ", ".join(row["required_inputs"]) if row["required_inputs"] else "No native strategy result")
            evidence = f"[[../outputs/backtest-lab/{row['latest_job']}/job.json]]" if row["latest_job"] else row["prior_experiment"]["note"] if row["prior_experiment"] else "—"
            text += f"| {row['id']} — {row['source']} | {row['status']} | {row['attempt_count']} | {observed.replace('|', '/')} | {evidence} |\n"
        path.write_text(text)

    def public_job(self, job):
        job = {k: v for k, v in job.items() if k not in {"command", "log"}}
        if job["status"] == "running" and not any(j["id"] == job["id"] for j in self.jobs):
            job.update(status="interrupted", message="Server restarted. Inspect the original process and artifacts; do not relaunch blindly.")
        job["terminal"] = job["status"] in TERMINAL_STATES
        job["poll_after_seconds"] = None if job["terminal"] else 3
        job["evidence_status"] = "requires_review" if job["status"] == "needs_review" else "no_verified_result"
        job["progress"] = live_progress(self.output / job["id"], job)
        return job

    def get_job(self, job_id):
        if not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", job_id):
            raise LabError("JOB_NOT_FOUND", "Unknown job", 404)
        path = self.output / job_id / "job.json"
        if not path.is_file(): raise LabError("JOB_NOT_FOUND", "Unknown job", 404)
        return self.public_job(json.loads(path.read_text()))

    def job_logs(self, job_id, cursor=0, limit=8192):
        job = self.get_job(job_id)
        if cursor < 0 or not 1 <= limit <= 65536: raise ValueError("Invalid log cursor or limit")
        path = self.output / job_id / "console.log"
        size = path.stat().st_size if path.exists() else 0
        reset = cursor > size
        if reset: cursor = 0
        data = b""
        if path.exists():
            with path.open("rb") as stream:
                stream.seek(cursor)
                data = stream.read(limit)
        return {"id": job_id, "cursor": cursor, "next_cursor": cursor + len(data),
                "data": data.decode("utf-8", errors="replace"), "reset_required": reset,
                "has_more": cursor + len(data) < size, "terminal": job["terminal"],
                "poll_after_seconds": None if job["terminal"] else 3}

    def result(self, job_id):
        job = self.get_job(job_id)
        if not job["terminal"]: raise LabError("RUN_RUNNING", "The job is still running; poll its status", 409, True)
        return {"id": job_id, "status": job["status"], "evidence_status": job["evidence_status"],
                "metrics": job.get("summary"), "audit_issues": job.get("audit_issues", []),
                "failure": (job.get("failure") or {"code": "EXECUTION_FAILED" if job["status"] == "failed" else "PROCESS_INSPECTION_REQUIRED",
                            "message": job["message"]}) if job["status"] != "needs_review" else None,
                "files": job.get("files", {}), "directory": job["directory"], "note": job.get("note"),
                "source_sha256": job.get("source_sha256"), "package_sha256": job.get("package_sha256"),
                "history_metadata": job.get("history_metadata"), "kind": job.get("kind", "strategy_test")}

    def state(self):
        runs = []
        for p in sorted((self.root / "outputs").glob("EXP-*/report.html")):
            try:
                signature = (p.stat().st_mtime_ns, p.stat().st_size)
                cached = self._report_cache.get(str(p))
                if not cached or cached[0] != signature:
                    cached = (signature, summary(read_report(p)))
                    self._report_cache[str(p)] = cached
                runs.append({"id": p.parent.name, **cached[1], "report": str(p.relative_to(self.root)), "origin": "archive",
                             "status": "invalid_attempt" if "invalid" in p.parent.name else "archived_observation"})
            except (OSError, ValueError, KeyError): pass
        jobs = []
        for p in sorted(self.output.glob("*/job.json"), reverse=True):
            try:
                job = self.public_job(json.loads(p.read_text()))
                log = p.parent / "console.log"
                if log.exists():
                    with log.open("rb") as stream:
                        stream.seek(max(0, log.stat().st_size - 12000))
                        job["log"] = stream.read().decode("utf-8", errors="replace")
                jobs.append(job)
                if job.get("summary"):
                    runs.insert(0, {"id": job["id"], **job["summary"], "origin": "platform", "strategy": job["strategy"],
                                    "kind": job.get("kind", "data_probe" if "history-probe" in job.get("idempotency_key", "") else "strategy_test"),
                                    "status": job["status"], "report": str((p.parent / "report.html").relative_to(self.root)), "note": job.get("note")})
            except (OSError, ValueError): pass
        rows, legacy = catalog(), self.legacy_rows()
        coverage = self.coverage(rows, jobs) + self.coverage(legacy, jobs)
        indexed = {row["id"]: row for row in coverage}
        for row in rows + legacy: row["execution"] = indexed[row["id"]]
        profile = self.profile()
        return {"catalog": rows, "legacy_sources": legacy, "coverage": coverage, "runs": runs, "jobs": jobs,
                "current_job": next((j for j in jobs if j["status"] == "running"), None), "connection": self.connection,
                "cli_installed": Path(self.cli).is_file(), "profile": profile,
                "saved_login": self.password_path.exists(), "repository": str(self.root)}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_): pass

    def valid_host(self):
        return self.headers.get("Host") == f"127.0.0.1:{self.server.server_port}"

    def send(self, status, content, content_type="application/json"):
        body = content if isinstance(content, bytes) else json.dumps(content, allow_nan=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Lab-API-Version", "1")
        if isinstance(content, dict) and content.get("retryable"):
            self.send_header("Retry-After", "3")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
        self.end_headers()
        self.wfile.write(body)

    @property
    def lab(self): return self.server.lab

    def error(self, error):
        custom = isinstance(error, LabError)
        retryable = error.retryable if custom else False
        return self.send(error.status if custom else 400 if isinstance(error, ValueError) else 500,
                         {"error": str(error), "code": error.code if custom else "INVALID_REQUEST" if isinstance(error, ValueError) else "FILESYSTEM_ERROR",
                          "retryable": retryable, "retry_after_seconds": 3 if retryable else None})

    def agent_get(self):
        path, query = urlsplit(self.path).path, parse_qs(urlsplit(self.path).query)
        if path == "/api/v1/health":
            return self.send(200, {"api_version": "1", "status": "serving", "connection": self.lab.connection,
                "cli_installed": Path(self.lab.cli).is_file(), "unfinished_job_exists": self.lab.unfinished_jobs(),
                "saved_login": self.lab.password_path.is_file(),
                "can_fetch_history": self.lab.connection["status"] == "connected"})
        if path == "/api/v1/strategies":
            rows = catalog()
            status = query.get("status", [None])[0]
            if status is not None and status not in {"ready", "needs_data", "recorded"}: raise ValueError("Unknown strategy status")
            if status: rows = [r for r in rows if r["status"] == status]
            offset, limit = int(query.get("offset", [0])[0]), int(query.get("limit", [25])[0])
            if offset < 0 or not 1 <= limit <= 100: raise ValueError("Invalid pagination")
            items = [{k: row[k] for k in ["id", "name", "status", "experiment", "required_inputs", "sha256"]} for row in rows[offset:offset+limit]]
            return self.send(200, {"items": items, "total": len(rows), "next_offset": offset+limit if offset+limit < len(rows) else None})
        if path == "/api/v1/coverage": return self.send(200, {"items": self.lab.coverage() + self.lab.coverage(self.lab.legacy_rows())})
        parts = path.strip("/").split("/")
        if len(parts) == 4 and parts[:3] == ["api", "v1", "strategies"]:
            row = next((r for r in catalog() if r["id"] == parts[3]), None)
            if row is None: raise LabError("STRATEGY_NOT_FOUND", "Unknown strategy", 404)
            return self.send(200, row)
        if path == "/api/v1/jobs":
            paths = sorted(self.lab.output.glob("*/job.json"), reverse=True)
            offset, limit = int(query.get("offset", [0])[0]), int(query.get("limit", [25])[0])
            if offset < 0 or not 1 <= limit <= 100: raise ValueError("Invalid pagination")
            fields = ["id", "strategy", "status", "terminal", "evidence_status", "started_utc", "finished_utc", "poll_after_seconds"]
            items = [{k: v for k, v in self.lab.get_job(p.parent.name).items() if k in fields} for p in paths[offset:offset+limit]]
            return self.send(200, {"items": items, "total": len(paths), "next_offset": offset+limit if offset+limit < len(paths) else None})
        if len(parts) in {4, 5} and parts[:3] == ["api", "v1", "jobs"]:
            if len(parts) == 4: return self.send(200, self.lab.get_job(parts[3]))
            if parts[4] == "logs":
                return self.send(200, self.lab.job_logs(parts[3], int(query.get("cursor", [0])[0]), int(query.get("limit", [8192])[0])))
            if parts[4] == "result": return self.send(200, self.lab.result(parts[3]))
        raise LabError("NOT_FOUND", "Unknown agent endpoint", 404)

    def do_GET(self):
        if not self.valid_host(): return self.error(LabError("UNAUTHORIZED", "Use the printed loopback URL", 403))
        if self.path == "/api/v1/capabilities": return self.send(200, capabilities())
        if self.path.startswith("/api/v1/"):
            if self.headers.get("X-Lab-Token") != self.lab.secret:
                return self.error(LabError("UNAUTHORIZED", "Session token required", 403))
            try: return self.agent_get()
            except (ValueError, OSError) as error: return self.error(error)
        if urlsplit(self.path).path == "/api/artifact":
            if self.headers.get("X-Lab-Token") != self.lab.secret: return self.send(403, {"error": "Session token required"})
            relative = parse_qs(urlsplit(self.path).query).get("path", [""])[0]
            path = (self.lab.root / relative).resolve()
            allowed = path.is_relative_to((self.lab.root / "outputs").resolve())
            if not allowed or path.suffix not in {".html", ".json", ".txt", ".md", ".cbotset", ".log"} or not path.is_file():
                return self.send(404, {"error": "Artifact not found"})
            return self.send(200, path.read_bytes(), "application/octet-stream")
        if self.path == "/api/state":
            if self.headers.get("X-Lab-Token") != self.lab.secret: return self.send(403, {"error": "Session token required"})
            return self.send(200, self.lab.state())
        if self.path == "/session.js":
            return self.send(200, ("window.LAB_TOKEN=" + json.dumps(self.lab.secret) + ";").encode(), "text/javascript")
        if self.path in {"/", "/app.js", "/styles.css"}:
            name = "index.html" if self.path == "/" else self.path[1:]
            types = {"index.html": "text/html; charset=utf-8", "app.js": "text/javascript", "styles.css": "text/css"}
            return self.send(200, (STATIC / name).read_bytes(), types[name])
        return self.send(404, {"error": "Not found"})

    def do_POST(self):
        origin = f"http://127.0.0.1:{self.server.server_port}"
        if not self.valid_host() or self.headers.get("Origin") != origin or self.headers.get("X-Lab-Token") != self.lab.secret:
            return self.error(LabError("UNAUTHORIZED", "Local session verification failed", 403))
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if size < 2 or size > 16384: raise ValueError("Invalid request size")
            if self.headers.get_content_type() != "application/json": raise ValueError("JSON required")
            data = json.loads(self.rfile.read(size))
            if not isinstance(data, dict): raise ValueError("JSON object required")
            if self.path == "/api/v1/preflight":
                if set(data) - {"strategy", "preset"} or not isinstance(data.get("strategy"), str): raise ValueError("Supply a strategy ID and optional frozen preset")
                return self.send(200, self.lab.preflight(data["strategy"], data.get("preset")))
            if self.path == "/api/v1/jobs":
                allowed = set(capabilities()["backtest_request_schema"]["properties"])
                if set(data) - allowed: raise ValueError("Unknown backtest request fields")
                if not data.get("idempotency_key"): raise ValueError("Agent launches require an idempotency_key")
                job_id = self.lab.start_job(data)
                return self.send(202, {"id": job_id, "status_url": f"/api/v1/jobs/{job_id}",
                                       "poll_after_seconds": 3})
            parts = self.path.strip("/").split("/")
            if len(parts) == 5 and parts[:3] == ["api", "v1", "jobs"] and parts[4] == "reinspect":
                if data: raise ValueError("Artifact reinspection takes an empty JSON object")
                return self.send(200, self.lab.reinspect(parts[3]))
            if self.path in {"/api/verify", "/api/v1/verify"}: self.lab.verify_connection()
            elif self.path == "/api/connect": self.lab.save_connection(data)
            elif self.path == "/api/backtest": return self.send(202, {"id": self.lab.start_job(data)})
            else: return self.send(404, {"error": "Not found"})
            return self.send(202, {"ok": True})
        except (ValueError, OSError) as error:
            return self.error(error)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    server.lab = Lab()
    server.lab.write_results_index()
    descriptor = server.lab.publish_agent_session(server.server_port)
    print(f"Gold Research Lab: http://127.0.0.1:{server.server_port}", flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally:
        server.server_close()
        if descriptor.exists() and json.loads(descriptor.read_text()).get("pid") == os.getpid():
            descriptor.unlink()


if __name__ == "__main__": main()
