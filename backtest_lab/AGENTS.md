# Operating Gold Research Lab

Use the JSON client, not browser automation:

```sh
python3 -m backtest_lab.agent capabilities
python3 -m backtest_lab.agent health
python3 -m backtest_lab.agent strategies
python3 -m backtest_lab.agent preflight T002
```

The server runs with `python3 -m backtest_lab.server`. It writes an owner-only
`~/.config/xauusd-backtest-lab/agent-session.json` descriptor. The client reads
the URL and session token automatically. Never print the descriptor or token.
Credentials remain outside the repository; use `verify` to check a saved login.
Do not ask for passwords in chat or put passwords in command arguments.

Read the selected strategy contract with `strategy T002`. Require preflight
`can_launch: true`; resolve its explicit blocker codes before launching. Do not
replace missing external data, change frozen rules, or tune rejected strategies.

Launch one authorized backtest with a stable, unique request key:

```sh
python3 -m backtest_lab.agent backtest T002 --idempotency-key exp017-fade-ticks-v2-001 --source-sha256 SOURCE_SHA256_FROM_PREFLIGHT
```

If the launch response is lost, retry the **same request and key**. The server
returns the original job even after restart or account disconnection. Reusing
the key for different settings returns `IDEMPOTENCY_CONFLICT`. A deliberately
new experiment needs a new key and a frozen specification.

Poll `job JOB_ID`, or use `wait JOB_ID --timeout 30`. A wait timeout does not
mean execution stopped. Read `terminal`, not a guessed duration. For logs, call
`logs JOB_ID --cursor 0 --limit 8192` and retain `next_cursor` for the next call.
No need to reread the full UI state, all contracts, or complete log repeatedly.

Terminal states are `needs_review`, `failed`, and `interrupted`. For terminal
jobs read `result JOB_ID`. `needs_review` means native report artifacts exist;
it is not a passed strategy test. Inspect audit issues, source/package hashes,
actual data mode, fills, timing, protections and coverage before interpreting
metrics. A failed/interrupted run has no verified market result. Do not restart
an interrupted run until its original process and artifacts have been inspected.

If a parser/audit repair is needed after a known terminal run, use
`reinspect JOB_ID` to re-read its native artifacts. This does not rerun the
strategy; it preserves the previous audit as a revision. Missing report spread
metadata remains an explicit review issue. A reported cBot stop plus a matching
complete native report can permit cleanup of a lingering owned CLI transport;
a timeout or report file alone never permits it. Closure metadata is retained.

The platform writes an Obsidian note per attempt and artifacts under
`outputs/backtest-lab/`. `files` gives relative names, SHA-256 hashes and sizes.
Never claim all strategies are finished based on source coverage, compilation,
unit checks, zero-trade aborts, or terminal process state.

Exit codes: 0 = request succeeded (read the result's evidence status),
2 = invalid request/API error, 3 = blocked preflight or conflict,
4 = session/server unavailable, 5 = failed/interrupted job. Errors contain
`code`, `error`, `retryable`, and optionally `retry_after_seconds`.
