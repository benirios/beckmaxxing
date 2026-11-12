"""Workbench checks use synthetic report fixtures, never claimed market results."""
import http.client
import importlib.util
import json
from pathlib import Path
import threading
from unittest.mock import patch

import pytest

SPEC = importlib.util.spec_from_file_location("backtest_lab_server", Path(__file__).resolve().parents[1] / "backtest_lab/server.py")
labmod = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(labmod)


@pytest.fixture
def lab(tmp_path):
    (tmp_path / "04 - Planning").mkdir()
    return labmod.Lab(root=tmp_path, profile_dir=tmp_path / "private", install_dir=tmp_path / "installed")


def fixture_report(mode="ticks"):
    return {"main": {"cBotName": "Example", "symbol": "XAUUSD", "data": {"type": mode},
                     "netProfit": -10, "testingPeriod": {"startDate": 1577836800000, "endDate": 1577923200000},
                     "commissions": {"type": "usdPerMillionUsdVolume", "value": 30},
                     "spread": {"type": "fixed", "value": 1}, "startingCapital": 10000, "depositAsset": "USD"},
            "tradeStatistics": {"totalTrades": {"all": 3}, "profitFactor": {"all": .8}}}


def test_report_reader_extracts_actual_data_mode(tmp_path):
    p = tmp_path / "report.html"
    p.write_text('<script type="application/json" id="backtesting-report">' + json.dumps(fixture_report("m1BarsFromServer")) + '</script>')
    result = labmod.summary(labmod.read_report(p))
    assert result["data_mode"] == "m1BarsFromServer"
    assert result["net"] == -10
    assert result["trades"] == 3


def test_m1_and_wrong_dates_do_not_pass_tick_audit():
    issues = labmod.report_audit(fixture_report("m1BarsFromServer"), "Example", "2020-01-01", "2020-01-03")
    assert any("tick" in x for x in issues)
    assert any("endDate" in x for x in issues)


def test_wrong_symbol_or_robot_is_flagged():
    report = fixture_report()
    report["main"]["symbol"] = "EURUSD"
    issues = labmod.report_audit(report, "ExpectedRobot", "2020-01-01", "2020-01-02")
    assert len(issues) == 2


def test_native_report_costs_must_match_frozen_scenario():
    report = fixture_report()
    report["main"]["commissions"]["type"] = "usdPerOneLot"
    assert any("commission" in problem for problem in labmod.report_audit(report, "Example", "2020-01-01", "2020-01-02"))


def test_tick_report_with_no_spread_metadata_is_reviewable():
    report = fixture_report()
    report["main"]["spread"] = None
    assert any("spread" in problem for problem in labmod.report_audit(report, "Example", "2020-01-01", "2020-01-02"))


def test_native_invocation_failure_has_actionable_code():
    failure = labmod.execution_failure(1, "Unknown command line parameters found: data-dir")
    assert failure["code"] == "CLI_UNSUPPORTED_OPTIONS" and not failure["retryable"]


def test_missing_broker_history_is_not_retried_blindly():
    failure = labmod.execution_failure(86, "No historical data for the specified period")
    assert failure["code"] == "HISTORY_UNAVAILABLE" and not failure["retryable"]


def test_empty_report_is_not_a_market_result(tmp_path):
    p = tmp_path / "report.json"
    p.write_text('{}')
    with pytest.raises(ValueError, match="schema"):
        labmod.read_report(p)


def test_input_dependent_strategy_cannot_launch(lab):
    with pytest.raises(ValueError, match="external inputs"):
        lab.start_job({"strategy": "T100"})


def test_no_launch_before_authenticated_account(lab):
    with pytest.raises(ValueError, match="connection"):
        lab.start_job({"strategy": "T002"})


def test_account_discovery_does_not_pass_unsupported_account_flag(lab):
    with patch.object(lab, "verify_connection"):
        lab.save_connection({"ctid": "example", "account": "123", "password": "fixture-secret"})
    lab.cli = __file__
    class ImmediateThread:
        def __init__(self, target, **kwargs): self.target = target
        def start(self): self.target()
    with patch.object(labmod.threading, "Thread", ImmediateThread), patch.object(labmod.subprocess, "run") as run:
        run.return_value = labmod.subprocess.CompletedProcess([], 0, '[{"TraderLogin":123}]', '')
        lab.verify_connection()
        assert not any(a.startswith("--account=") for a in run.call_args.args[0])
        assert lab.connection["status"] == "connected"


def test_future_dates_are_rejected(lab):
    with pytest.raises(ValueError, match="future"):
        lab.start_job({"strategy": "T002", "start": "2090-01-01", "end": "2091-01-01"})


def test_password_is_private_and_not_in_profile_or_state(lab):
    with patch.object(lab, "verify_connection"):
        lab.save_connection({"ctid": "example", "account": "123", "password": "fixture-secret"})
    assert lab.password_path.stat().st_mode & 0o777 == 0o600
    assert lab.profile_dir.stat().st_mode & 0o777 == 0o700
    assert "fixture-secret" not in lab.profile_path.read_text()
    assert "fixture-secret" not in json.dumps(lab.state())
    assert lab.auth_args()[1].startswith("--pwd-file=")


def test_credentials_refuse_symlink_target(lab, tmp_path):
    outside = tmp_path / "outside"
    outside.write_text("keep")
    lab.profile_dir.mkdir()
    lab.password_path.symlink_to(outside)
    with patch.object(lab, "verify_connection"), pytest.raises(OSError):
        lab.save_connection({"ctid": "example", "account": "123", "password": "fixture-secret"})
    assert outside.read_text() == "keep"


def test_restart_does_not_allow_duplicate_unfinished_backtest(lab):
    directory = lab.output / "old"
    directory.mkdir()
    (directory / "job.json").write_text(json.dumps({"status": "running"}))
    lab.connection["status"] = "connected"
    with pytest.raises(ValueError, match="unfinished"):
        lab.start_job({"strategy": "T002"})


def test_source_change_invalidates_native_package(lab):
    source = lab.root / "Example.cs"
    source.write_text("changed")
    package = lab.root / "ctrader/bin/Lab/Example/Example.algo"
    package.parent.mkdir(parents=True)
    package.write_bytes(b"fixture")
    package.with_suffix(".identity.json").write_text(json.dumps({"source_sha256": "old", "package_sha256": labmod.digest(package)}))
    with pytest.raises(ValueError, match="verified package"):
        lab.package(source)


def test_job_command_can_only_backtest_with_live_disabled(lab):
    source = lab.root / "ctrader/Example.cs"
    source.parent.mkdir()
    source.write_text('[Parameter("Mode")] public string Mode {get;set;} [Parameter("Live")] public bool AllowLiveTrading {get;set;} [Parameter("Volume")] public double FixedVolumeInUnits {get;set;} [Parameter("Label")] public string PositionLabel {get;set;}')
    (source.parent / "catalog-coverage.json").write_text(json.dumps({"T002": source.name}))
    package = lab.root / "fixture.algo"
    package.write_bytes(b"synthetic fixture")
    lab.connection["status"] = "connected"
    with patch.object(lab, "package", return_value=package), patch.object(lab, "auth_args", return_value=[]), patch.object(labmod.threading, "Thread"):
        job_id = lab.start_job({"strategy": "T002"})
    job = lab.jobs[0]
    assert job["id"] == job_id
    assert job["command"][1] == "backtest"
    assert "--data-mode=ticks" in job["command"]
    settings = json.loads((lab.root / job["directory"] / "parameters.cbotset").read_text())
    assert settings["Parameters"]["AllowLiveTrading"] == "False"
    assert settings["Parameters"]["Mode"] == "0"


def test_cli_failure_creates_failed_note_without_invented_metrics(lab):
    directory = lab.output / "fixture"
    directory.mkdir()
    job = {"id": "fixture", "strategy": "T002", "experiment": "EXP-017", "title": "Synthetic failure check",
           "branch": "primary", "directory": str(directory.relative_to(lab.root)), "command": ["fixture-cli", "backtest"],
           "source_sha256": "fixture", "package_sha256": "fixture", "robot": "Example", "marker": "EXAMPLE_START",
           "start": "2020-01-01", "end": "2020-01-02"}
    with patch.object(labmod.subprocess, "Popen") as spawn, patch.object(lab, "install_package"):
        spawn.return_value.pid = 1234
        spawn.return_value.wait.return_value = 80
        lab.execute(job)
    assert job["status"] == "failed"
    assert "summary" not in job
    note = (lab.root / job["note"]).read_text()
    assert "No verified market result" in note
    assert "Reported net" not in note
    assert job["failure"]["code"] == "BROKER_UNAVAILABLE"


@pytest.fixture
def web(lab):
    server = labmod.ThreadingHTTPServer(("127.0.0.1", 0), labmod.Handler)
    server.lab = lab
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield server
    server.shutdown()
    server.server_close()


def request(web, method, path, data=None, headers=None):
    conn = http.client.HTTPConnection("127.0.0.1", web.server_port)
    conn.request(method, path, json.dumps(data) if data is not None else None, headers=headers or {})
    response = conn.getresponse()
    body = response.read()
    conn.close()
    return response.status, body


def test_private_api_requires_session_token(web):
    assert request(web, "GET", "/api/state")[0] == 403
    code, body = request(web, "GET", "/api/state", headers={"X-Lab-Token": web.lab.secret})
    assert code == 200
    assert len(json.loads(body)["catalog"]) == 100


def test_cross_origin_request_cannot_save_credentials(web):
    code, _ = request(web, "POST", "/api/connect", {"ctid": "example", "account": "123", "password": "fixture-secret"},
                      {"X-Lab-Token": web.lab.secret, "Origin": "https://example.com", "Content-Type": "application/json"})
    assert code == 403
    assert not web.lab.password_path.exists()


def test_host_rebinding_is_rejected(web):
    assert request(web, "GET", "/", headers={"Host": "example.com"})[0] == 403


def test_artifact_endpoint_cannot_read_outside_outputs(web):
    web.lab.profile_dir.mkdir()
    web.lab.password_path.write_text("fixture-secret")
    code, _ = request(web, "GET", "/api/artifact?path=private/credentials.pwd", headers={"X-Lab-Token": web.lab.secret})
    assert code == 404


def test_static_app_serves_without_external_assets(web):
    code, body = request(web, "GET", "/")
    assert code == 200 and b"Gold Research Lab" in body
    assert b"https://fonts" not in body


@pytest.fixture
def ready_lab(lab):
    source = lab.root / "ctrader/Example.cs"
    source.parent.mkdir()
    source.write_text('[Parameter("Mode")] public string Mode {get;set;} [Parameter("Live")] public bool AllowLiveTrading {get;set;} [Parameter("Volume")] public double FixedVolumeInUnits {get;set;} [Parameter("Label")] public string PositionLabel {get;set;}')
    (source.parent / "catalog-coverage.json").write_text(json.dumps({"T002": source.name}))
    package = lab.root / "fixture.algo"
    package.write_bytes(b"synthetic fixture")
    lab.connection["status"] = "connected"
    with patch.object(lab, "package", return_value=package), patch.object(lab, "auth_args", return_value=[]), patch.object(lab, "execute") as worker:
        yield lab, worker


def test_retry_is_idempotent_after_disconnect_and_server_restart(ready_lab):
    lab, worker = ready_lab
    data = {"strategy": "T002", "idempotency_key": "experiment-001"}
    first = lab.start_job(data)
    lab.jobs.clear()
    lab.connection["status"] = "error"
    assert lab.start_job(data) == first
    assert len(list(lab.output.glob("*/job.json"))) == 1


def test_conflicting_retry_never_starts_another_job(ready_lab):
    lab, worker = ready_lab
    lab.start_job({"strategy": "T002", "idempotency_key": "frozen"})
    with pytest.raises(labmod.LabError) as error:
        lab.start_job({"strategy": "T002", "idempotency_key": "frozen", "end": "2020-12-31"})
    assert error.value.code == "IDEMPOTENCY_CONFLICT"
    assert len(list(lab.output.glob("*/job.json"))) == 1


def test_concurrent_retries_start_exactly_one_job(ready_lab):
    from concurrent.futures import ThreadPoolExecutor
    lab, worker = ready_lab
    with ThreadPoolExecutor(max_workers=4) as pool:
        ids = list(pool.map(lambda _: lab.start_job({"strategy": "T002", "idempotency_key": "concurrent"}), range(4)))
    assert len(set(ids)) == 1
    assert len(list(lab.output.glob("*/job.json"))) == 1


def test_source_fingerprint_freezes_launch(ready_lab):
    lab, worker = ready_lab
    with pytest.raises(labmod.LabError) as error:
        lab.start_job({"strategy": "T002", "expected_source_sha256": "0" * 64})
    assert error.value.code == "SOURCE_CHANGED"
    worker.assert_not_called()


def write_fixture_job(lab, status="failed"):
    directory = lab.output / "fixture"
    directory.mkdir(exist_ok=True)
    job = {"id": "fixture", "status": status, "message": "Synthetic check", "directory": "outputs/backtest-lab/fixture"}
    (directory / "job.json").write_text(json.dumps(job))
    return directory


def test_log_cursor_is_incremental_and_bounded(lab):
    directory = write_fixture_job(lab)
    (directory / "console.log").write_bytes(b"abcdefghij")
    first = lab.job_logs("fixture", 0, 4)
    second = lab.job_logs("fixture", first["next_cursor"], 4)
    assert first["data"] == "abcd" and second["data"] == "efgh"
    assert second["next_cursor"] == 8 and second["has_more"]
    assert first["terminal"]
    with pytest.raises(ValueError): lab.job_logs("fixture", 0, 65537)


def test_truncated_log_explicitly_resets_cursor(lab):
    directory = write_fixture_job(lab)
    (directory / "console.log").write_bytes(b"abc")
    result = lab.job_logs("fixture", 999, 4)
    assert result["reset_required"] and result["data"] == "abc" and result["next_cursor"] == 3


def test_failed_result_has_no_performance_metrics(lab):
    write_fixture_job(lab)
    result = lab.result("fixture")
    assert result["metrics"] is None
    assert result["failure"]["code"] == "EXECUTION_FAILED"
    assert result["evidence_status"] == "no_verified_result"


def test_server_restart_requires_inspection_not_automatic_relaunch(lab):
    write_fixture_job(lab, "running")
    result = lab.result("fixture")
    assert result["status"] == "interrupted"
    assert result["failure"]["code"] == "PROCESS_INSPECTION_REQUIRED"
    assert lab.unfinished_jobs()
    blocker = next(b for b in lab.preflight("T100")["blockers"] if b["code"] == "PROCESS_INSPECTION_REQUIRED")
    assert blocker["retryable"] is False


def test_terminal_report_is_not_automatically_validated(lab):
    write_fixture_job(lab, "needs_review")
    job = lab.get_job("fixture")
    assert job["terminal"] and job["evidence_status"] == "requires_review"


def test_preflight_exposes_missing_inputs_without_launching(lab):
    with patch.object(labmod.subprocess, "run") as run:
        result = lab.preflight("T100")
    assert not result["can_launch"]
    assert result["blockers"][0]["code"] == "INPUTS_REQUIRED"
    assert result["blockers"][0]["required_inputs"]
    run.assert_not_called()


def test_v1_capabilities_are_public_and_contain_no_token(web):
    code, body = request(web, "GET", "/api/v1/capabilities")
    result = json.loads(body)
    assert code == 200 and result["api_version"] == "1"
    assert web.lab.secret.encode() not in body
    assert result["backtest_request_schema"]["additionalProperties"] is False


def test_v1_errors_have_stable_codes_and_no_retry_loop(web):
    code, body = request(web, "GET", "/api/v1/jobs/unknown", headers={"X-Lab-Token": web.lab.secret})
    result = json.loads(body)
    assert code == 404 and result["code"] == "JOB_NOT_FOUND" and result["retryable"] is False
    code, body = request(web, "GET", "/api/v1/health")
    assert code == 403 and json.loads(body)["code"] == "UNAUTHORIZED"


def test_agent_launch_requires_key_and_rejects_unknown_fields(web):
    headers = {"X-Lab-Token": web.lab.secret, "Origin": f"http://127.0.0.1:{web.server_port}", "Content-Type": "application/json"}
    for data in [{"strategy": "T002"}, {"strategy": "T002", "idempotency_key": "x", "AllowLiveTrading": True}]:
        code, body = request(web, "POST", "/api/v1/jobs", data, headers)
        assert code == 400 and json.loads(body)["code"] == "INVALID_REQUEST"
    assert not list(web.lab.output.glob("*/job.json"))


def test_agent_catalog_is_paginated_and_compact(web):
    code, body = request(web, "GET", "/api/v1/strategies?limit=2", headers={"X-Lab-Token": web.lab.secret})
    result = json.loads(body)
    assert code == 200 and result["total"] == 100 and len(result["items"]) == 2
    assert result["next_offset"] == 2 and "contract" not in result["items"][0]


def test_agent_descriptor_is_private_and_not_returned_in_state(web):
    path = web.lab.publish_agent_session(web.server_port)
    assert path.stat().st_mode & 0o777 == 0o600
    assert web.lab.secret not in json.dumps(web.lab.state())


def test_live_progress_uses_only_emitted_account_snapshots(tmp_path):
    log = tmp_path / "console.log"
    log.write_text("Progress | Backtesting | 12.50 % |\n")
    result = labmod.live_progress(tmp_path, {})
    assert result["percent"] == 12.5 and result["metrics"] is None
    stamp = result["updated_utc"]
    assert labmod.live_progress(tmp_path, {})["updated_utc"] == stamp
    log.write_text(log.read_text()+"LAB_LIVE_V1,utc=2026-10-19T09:00:00Z,balance=10012.5,equity=9998,net_profit=12.5,closed_positions=2\n")
    result = labmod.live_progress(tmp_path, {})
    assert result["metrics"]["equity"] == 9998 and result["metrics"]["net_profit"] == 12.5
    assert result["metrics"]["closed_positions"] == 2


def test_nonfinite_telemetry_is_not_displayed(tmp_path):
    (tmp_path / "console.log").write_text("LAB_LIVE_V1,utc=2026-01-01,balance=NaN,equity=10000,net_profit=0,closed_positions=0")
    assert labmod.live_progress(tmp_path, {})["metrics"] is None


def test_coverage_does_not_count_warmup_probe_as_strategy_result(lab):
    jobs = [{"id": "fixture", "strategy": "T002", "status": "needs_review", "kind": "data_probe", "summary": {"trades": 0, "net": 0}}]
    row = next(r for r in lab.coverage(jobs=jobs) if r["id"] == "T002")
    assert row["status"] == "diagnostic" and row["attempt_count"] == 1


def test_completed_platform_report_is_visible_in_results(lab):
    directory = write_fixture_job(lab, "needs_review")
    job = json.loads((directory / "job.json").read_text())
    job.update(strategy="T015", kind="strategy_test", summary={"net": -10,"trades": 3}, title="Fixture")
    (directory / "job.json").write_text(json.dumps(job))
    result = lab.state()
    assert any(r["id"] == "fixture" and r["net"] == -10 for r in result["runs"])
    assert next(r for r in result["coverage"] if r["id"] == "T015")["status"] == "result_recorded"


def test_legacy_source_coverage_is_explicit(lab):
    rows = lab.legacy_rows()
    assert len(rows) == 13
    assert any(r["source"] == "XauUsdNy30OrbBaseline.cs" and r["experiment"] == "EXP-003" for r in rows)
    assert not any("Exporter" in r["source"] for r in rows)


def test_completion_requires_native_stop_and_matching_report(tmp_path):
    report = fixture_report()
    report["main"].update(cBotName="Example", titleKind="backtesting")
    report.update(history={}, positions={})
    (tmp_path / "report.json").write_text(json.dumps(report))
    (tmp_path / "console.log").write_text("Progress | Backtesting | 50 %")
    assert not labmod.native_report_finished(tmp_path, "Example")
    (tmp_path / "console.log").write_text("Info | CBot instance [Example, XAUUSD, m1] stopped.")
    assert labmod.native_report_finished(tmp_path, "Example")
    assert not labmod.native_report_finished(tmp_path, "DifferentRobot")


def test_reinspection_reuses_evidence_and_preserves_previous_audit(lab):
    directory = write_fixture_job(lab)
    job = json.loads((directory / "job.json").read_text())
    job.update(robot="Example", start="2020-01-01", end="2020-01-02", marker="EXAMPLE_START", strategy="T006",
               source_sha256="fixture", package_sha256="fixture", experiment="EXP-018", title="Synthetic check", branch="primary", exit_code=0)
    (directory / "job.json").write_text(json.dumps(job))
    (directory / "report.json").write_text(json.dumps(fixture_report()))
    (directory / "console.log").write_text("EXAMPLE_START")
    with patch.object(labmod.subprocess, "Popen") as spawn:
        result = lab.reinspect("fixture")
    spawn.assert_not_called()
    assert result["status"] == "needs_review"
    assert json.loads((directory / result["previous_audit"]).read_text())["status"] == "failed"
    assert result["evidence_status"] == "requires_review"


@pytest.mark.parametrize("native_finished", [False, True])
def test_transport_cleanup_requires_native_completion_evidence(lab, native_finished):
    directory = write_fixture_job(lab, "running")
    job = json.loads((directory / "job.json").read_text())
    job.update(robot="Example", start="2020-01-01", end="2020-01-02", marker="EXAMPLE_START", strategy="T006",
               source_sha256="fixture", package_sha256="fixture", experiment="EXP-018", title="Synthetic check", branch="primary", command=["fixture-cli", "backtest"])
    (directory / "report.json").write_text(json.dumps(fixture_report()))
    with patch.object(labmod.subprocess, "Popen") as spawn, patch.object(lab, "install_package"), patch.object(labmod, "native_report_finished", return_value=native_finished), patch.object(labmod.time, "monotonic", side_effect=[0, 31]):
        process = spawn.return_value
        process.pid = 1234
        process.wait.side_effect = [labmod.subprocess.TimeoutExpired("fixture", 1), labmod.subprocess.TimeoutExpired("fixture", 1), 0]
        lab.execute(job)
        assert bool(process.send_signal.call_count) is native_finished
        assert bool(job.get("transport_closed_after_native_report")) is native_finished


def test_native_installation_preserves_previous_package(lab):
    directory = write_fixture_job(lab)
    staged = directory / "Example.algo"
    staged.write_bytes(b'verified-fixture')
    lab.install_dir.mkdir()
    destination = lab.install_dir / staged.name
    destination.write_bytes(b'previous-fixture')
    job = {'id':'fixture','directory':str(directory.relative_to(lab.root)),'robot':'Example','package_sha256':labmod.digest(staged)}
    lab.install_package(job)
    assert destination.read_bytes() == staged.read_bytes()
    assert (directory / 'previous-installed-package.algo').read_bytes() == b'previous-fixture'
    assert job['installation']['staged_sha256'] == labmod.digest(staged)


def test_frozen_preset_rejects_changed_input_and_empty_features(lab):
    import hashlib
    src = lab.root / 'ctrader/Test.cs'
    src.parent.mkdir()
    src.write_text('[Parameter("Rows", DefaultValue="")] public string FeatureRows {get;set;}')
    data = lab.root / 'data.csv'
    data.write_text('audited')
    configs = lab.root / 'configs'
    configs.mkdir()
    item = {'strategy': 'T037', 'source': 'Test.cs',
            'source_sha256': labmod.digest(src), 'parameters': {'FeatureRows': 'frozen'},
            'inputs': {'data.csv': labmod.digest(data)}}
    path = configs / 'backtest-presets.json'
    path.write_text(json.dumps({'presets': {'test': item}}))
    assert lab.frozen_preset('T037', 'test') == item
    data.write_text('changed')
    with pytest.raises(labmod.LabError, match='missing or changed'):
        lab.frozen_preset('T037', 'test')
    item['inputs'] = {}
    item['parameters'] = {}
    path.write_text(json.dumps({'presets': {'test': item}}))
    with pytest.raises(labmod.LabError, match='required inputs'):
        lab.frozen_preset('T037', 'test')


def test_frozen_preset_is_bound_to_strategy_source_and_dates(lab):
    src = lab.root / 'ctrader/Test.cs'
    src.parent.mkdir()
    src.write_text('class Test {}')
    configs = lab.root / 'configs'
    configs.mkdir()
    item = {'strategy': 'T037', 'source': 'Test.cs', 'source_sha256': labmod.digest(src),
            'parameters': {}, 'start': '2025-06-23', 'end': '2026-10-24'}
    (configs / 'backtest-presets.json').write_text(json.dumps({'presets': {'test': item}}))
    with pytest.raises(labmod.LabError, match='No frozen preset'):
        lab.frozen_preset('T038', 'test')
    with pytest.raises(labmod.LabError, match='frozen preset dates'):
        lab.start_job({'strategy': 'T037', 'preset': 'test'})
    src.write_text('changed source')
    with pytest.raises(labmod.LabError, match='source changed'):
        lab.frozen_preset('T037', 'test')
