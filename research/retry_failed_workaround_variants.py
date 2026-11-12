"""One additional native attempt for frozen presets with no first report."""
from backtest_lab.agent import Client
from research.workaround_batch import OUT, ROOT, START, END, write_json
import json
import time

PRESETS = ["t041-primary", "t041-opposite", "t041-shuffled", "t091-primary", "t091-opposite", "t097-random"]
client = Client()
note = ROOT / "04 - Planning/Workaround backtest batch - 2026-11-11.md"
registry = json.loads((ROOT / "configs/backtest-presets.json").read_text())["presets"]
for name in PRESETS:
    old = json.loads((OUT / f"{name}-result.json").read_text())
    if old.get("status") != "failed":
        print(json.dumps({"skipped_not_failed": name}), flush=True)
        continue
    preflight = client.request("/api/v1/preflight", {"strategy": registry[name]["strategy"], "preset": name})
    if not preflight["can_launch"]:
        write_json(OUT / f"{name}-retry-v2-blocked.json", preflight)
        print(json.dumps({"blocked": name, "blockers": preflight["blockers"]}), flush=True)
        continue
    request = {"strategy": registry[name]["strategy"], "preset": name, "start": START, "end": END,
               "branch": "primary", "idempotency_key": "workaround-retry-v2-" + name,
               "expected_source_sha256": preflight["source_sha256"]}
    job_id = client.request("/api/v1/jobs", request)["id"]
    print(json.dumps({"launched": name, "id": job_id}), flush=True)
    with note.open("a") as stream:
        stream.write(f"\n- **Retry launched {name}**: `[[Lab run {job_id} - {registry[name]['strategy']}]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.\n")
    last = 0
    while True:
        job = client.request("/api/v1/jobs/" + job_id)
        if job["terminal"]: break
        if time.monotonic() - last >= 45:
            progress = job.get("progress") or {}
            print(json.dumps({"running": name, "percent": progress.get("percent"),
                              "equity_samples": len(progress.get("equity_curve") or [])}), flush=True)
            last = time.monotonic()
        time.sleep(5)
    result = client.request("/api/v1/jobs/" + job_id + "/result")
    write_json(OUT / f"{name}-retry-v2-result.json", result)
    with note.open("a") as stream:
        stream.write(f"\n- **Retry terminal {name} — {job['status']}**: [[Lab run {job_id} - {registry[name]['strategy']}]]. Summary `{json.dumps(job.get('summary', job.get('failure', {})))}`. Independent audit still required.\n")
    print(json.dumps({"finished": name, "id": job_id, "status": job["status"],
                      "summary": job.get("summary"), "failure": job.get("failure")}), flush=True)
