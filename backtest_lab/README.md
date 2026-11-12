# Gold Research Lab

A local app for the existing XAUUSD research repository. Python standard library
backend, buildless HTML/CSS/JS interface, official Spotware CLI execution engine.
It binds only to `127.0.0.1`; do not expose it to the internet or a reverse proxy.

Start from the repository root:

```sh
python3 -m backtest_lab.server --port 8765
```

Open **http://127.0.0.1:8765**. Stop with Ctrl+C after all jobs have finished.

## Agent interface

Agents can use the JSON client without opening a browser:

```sh
python3 -m backtest_lab.agent capabilities
python3 -m backtest_lab.agent health
python3 -m backtest_lab.agent strategies --status ready
python3 -m backtest_lab.agent strategy T002
python3 -m backtest_lab.agent preflight T002
```

After preflight returns `can_launch: true`, submit a frozen test:

```sh
python3 -m backtest_lab.agent backtest T002 --idempotency-key exp017-fade-v2-ticks-001 --source-sha256 SHA256_FROM_PREFLIGHT
python3 -m backtest_lab.agent job JOB_ID
python3 -m backtest_lab.agent wait JOB_ID --timeout 30
python3 -m backtest_lab.agent logs JOB_ID --cursor 0 --limit 8192
python3 -m backtest_lab.agent result JOB_ID
python3 -m backtest_lab.agent reinspect JOB_ID
```

The client reads an owner-only session descriptor written by the server at
`~/.config/xauusd-backtest-lab/agent-session.json`; it does not require passwords
or tokens in command arguments. Run `verify` to check an already-saved login.
The descriptor and its token must never be printed or committed.

`GET /api/v1/capabilities` exposes the versioned endpoint contract, JSON request
schema, terminal states and retry rules. Other versioned endpoints require the
session token. Lists are paginated and omit large contracts/logs; retrieve one
strategy's contract or a bounded log segment when needed.

Launches require an idempotency key. Retrying the same key/request returns the
original job even after a restart or connection failure. A different request
with the same key returns `IDEMPOTENCY_CONFLICT`. Source fingerprints protect
against launching changed code after preflight. Use one server per repository.

Job state and evidence state are distinct: `needs_review` is terminal execution
with unreviewed evidence, not a validated strategy result. A polling timeout is
not a stopped job. After restart, an outstanding job requires process inspection;
the app never blindly launches a replacement. Structured errors include stable
codes, retryability and retry timing. See [agent operating instructions](AGENTS.md)
for the full protocol and exit codes.

## Use

1. Click **Connect cTrader**. Enter the cTrader ID username/email and **cTrader ID
   password**, plus the FTMO trading account number. The account password from
   another trading platform is not a substitute. Broker defaults to `FTMO Platform`;
   change it to the exact broker name if CLI reports that the broker is unknown.
2. The app saves an owner-only credentials file under
   `~/.config/xauusd-backtest-lab/`, outside this repository, and verifies the
   selected account with the CLI. The password is not returned through the API.
3. Choose a strategy marked **Broker data**, inspect its rules, and run a test.
   The three initial presets are T002/EXP-017 Tokyo Fade, T006/EXP-018 equity-shock
   gold response, and T015/EXP-019 round-number rejection. T015 also has its frozen
   non-round placebo branch. Native execution compatibility remains subject to the
   actual installed CLI; compiled source alone is not proof of a successful run.
4. Watch **Runs & evidence**. New jobs request historical server ticks, M1 chart,
   $10,000 starting balance, fixed spread 1 pip and $30 per million USD commission.
   Dates default to the already-exposed 2020-05-06–2026-10-24 development interval.
5. Inspect saved report, log, events, parameter file and hashes under
   `outputs/backtest-lab/<run-id>/`. Each attempt also writes a uniquely named
   `Lab run ...md` note directly into `04 - Planning/` in this Obsidian vault.

Only one new job runs at a time. There are no live-order endpoints, generic CLI
command endpoints or optimization sweeps. Sources requiring external inputs
cannot be launched with empty input rows. The app shows all 100 catalog entries,
their source contracts, and existing reports under `outputs/EXP-*/report.html`.
Previously invalid attempts remain visible by their original directory names.

The current-run panel also draws an **Observed account equity** curve from a per-run cTrader snapshot file: startup, closed-position events, and hourly timer readings. Observer builds flush each sample during replay, so the line moves before the native report finishes. The chart labels the sample count and latest simulation timestamp, connects observed samples only, and does not interpolate between them. An observer-only derived build writes only to that run’s artifact directory; canonical strategy source and trading rules remain unchanged. Zero-order observation studies correctly show a flat $10,000 curve.

## Dashboard

The dark interface opens on **Runs & evidence**. Select a native report to inspect
profit factor, return, maximum equity drawdown, winning/losing/total trades,
win rate, average trades per elapsed calendar day, costs, streaks and direction
breakdown. Missing native fields remain unavailable. The chart plots native
balance samples with the sampled minimum/maximum equity range.

Reports sort by return divided by maximum equity drawdown, highest first.
Diagnostics, invalid attempts and incomplete metrics are unranked. Different
periods and sample sizes still require review before comparing results.
Each run includes a short strategy explanation; exact tested settings remain in
the run artifacts. The **What’s next?** tab lists proposed future improvements
and does not launch tests or change configuration.

## Result interpretation

An exit code of zero is insufficient. Native report identity, XAUUSD symbol,
reported data mode, requested dates, startup marker, Tokyo V2 marker and nonzero
trade count are checked. Reports are always saved as **needs review**, never
automatically accepted or promoted. Every check issue is included in the note.
Manually audit actual covered dates, warmup, fill timing, costs, protections,
final open positions, and held-out-data boundaries before interpreting results.
The report's requested end date does not prove uninterrupted market coverage.

The app parses native cTrader reports, not synthetic performance. Existing
M1 results stay labelled M1. A malformed/missing report makes a failed attempt;
it never becomes a zero-return backtest. If the app restarts during a job, it
blocks new jobs until the outstanding process and artifacts have been inspected.

## Engine setup and compatibility

Official install: <https://help.ctrader.com/ctrader-cli/setup/>.
Historical backtest commands: <https://help.ctrader.com/ctrader-cli/cbots/>.

This Mac's official Spotware CLI 5.9.0.3 was installed under
`~/.local/share/ctrader-cli/5.9.0/`, linked at `~/.local/bin/ctrader-cli`.
The vendor archive's SHA-256 was checked against the official Homebrew formula.
The CLI's `--version`, `--help` and metadata command responded successfully.
The installed `accounts` command rejects `--account`; the workbench lists linked
accounts without that flag and then checks the selected account in the response.
Homebrew itself rejected installation because of outdated Apple Command Line
Tools, so the already-downloaded, verified official binary archive was extracted
without changing the system toolchain.

Native compatibility was subsequently verified: the existing net6.0 Tokyo V2
package executed on real server ticks in the 2026-10-19–2026-10-24 diagnostic.
Its startup log confirms the unchanged V2 build. Rebuilding it for .NET 8 is
unnecessary for this installed CLI. The exploratory net8.0 build attempt failed
with CT0004 in the pinned packager; no strategy source was changed. The official
documentation and installed command set differ in several places, so actual
native output and version-specific invocation checks remain authoritative.

During setup, a saved-login account check was attempted. After correcting the
unsupported `accounts --account` flag, both broker-filtered and unfiltered checks
exited with code 80: repeated reconnects followed by "Connection can't be
established". The supplied credentials have not been verified; this response
does not prove that they are incorrect. No history download or new market
backtest completed. The app reports this as connectivity failure and preserves
the original run queue. The browser app and agent protocol now have 47 passing
focused implementation checks, including concurrent retries, source drift,
restart recovery, log cursors, structured errors and session-file handling.

Subsequently the saved account was verified through the agent API. The first
native launch was preserved as a failed invocation because this installed CLI
also rejects `--commission-type` and `--data-dir`. New jobs use the supported
minimal arguments and the CLI-managed history cache. Their native reports must
confirm the requested commission unit/value, spread, currency and balance;
the app does not infer those facts from a successful process exit.

The recent-week diagnostic then produced a real tick-data report and the five
expected Tokyo warmup events, with zero trades. This establishes recent data
availability, not profitability or full-period coverage. Native `spread: null`
is preserved as a review issue. The legacy CLI remained alive after native
cBot stop/report completion; its owned transport was gracefully interrupted
only after both completion signals were verified. Closure evidence is saved.
The parser fix was applied by artifact reinspection without rerunning the market
data, and the prior failed audit is preserved as a revision. Future workers use
the same completion guard before cleaning up lingering CLI transports.

Browser visual QA could not run because the browser access host is unavailable;
HTTP/API and behavior checks are separate implementation evidence.

Run focused checks:

```sh
.venv/bin/python -m pytest tests/test_backtest_lab.py -q
.venv/bin/python -m pytest tests/test_backtest_lab_agent.py -q
node --check backtest_lab/app.js
npm ci
npm test
```
