"""Agent transport checks use local synthetic fixtures; no broker calls."""
import importlib.util
import json
from pathlib import Path
from unittest.mock import patch

import pytest

SPEC = importlib.util.spec_from_file_location("lab_agent", Path(__file__).resolve().parents[1] / "backtest_lab/agent.py")
agent = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(agent)


def descriptor(tmp_path, url="http://127.0.0.1:8765"):
    path = tmp_path / "session.json"
    path.write_text(json.dumps({"url": url, "token": "fixture-token-0123456789012345"}))
    path.chmod(0o600)
    return path


def test_client_rejects_nonlocal_session_destination(tmp_path):
    with patch.object(agent.http.client, "HTTPConnection") as network, pytest.raises(agent.ClientError):
        agent.Client(descriptor(tmp_path, "https://example.com"))
    network.assert_not_called()


def test_client_rejects_world_readable_descriptor(tmp_path):
    path = descriptor(tmp_path)
    path.chmod(0o644)
    with pytest.raises(agent.ClientError) as error: agent.Client(path)
    assert error.value.body["code"] == "SESSION_UNAVAILABLE"


def test_invalid_cli_arguments_return_json_without_echoing_secret(capsys):
    with pytest.raises(SystemExit): agent.main(["health", "--password=fixture-secret"])
    output = capsys.readouterr().out
    assert json.loads(output)["code"] == "INVALID_ARGUMENTS"
    assert "fixture-secret" not in output


def test_wait_timeout_does_not_claim_the_job_stopped():
    args = agent.parser().parse_args(["wait", "fixture", "--timeout", "0"])
    class Client:
        def request(self, path): return {"terminal": False, "status": "running", "poll_after_seconds": 3}
    result = agent.dispatch(Client(), args)
    assert result["wait_timed_out"] and result["status"] == "running" and not result["terminal"]


def test_backtest_client_preserves_idempotency_key_and_fingerprint():
    args = agent.parser().parse_args(["backtest", "T002", "--idempotency-key", "frozen", "--source-sha256", "0" * 64])
    with patch.object(agent, "Client") as client:
        agent.dispatch(client, args)
    path, request = client.request.call_args.args
    assert path == "/api/v1/jobs"
    assert request["idempotency_key"] == "frozen" and request["expected_source_sha256"] == "0" * 64
