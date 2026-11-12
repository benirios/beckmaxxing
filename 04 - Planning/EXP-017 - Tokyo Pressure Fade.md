# EXP-017 — Tokyo-open price-pressure fade

Status: preregistered 2026-11-10; full-period tick evaluation remains incomplete.
V2 M1 exploratory results and CLI data-availability attempts are recorded below.
Canonical thesis: T002.

## Frozen primary run

- Robot: `XauUsdTokyoPressureReversal`; build `EXP017_TOKYO_PRESSURE_REVERSAL_V1`.
- Package: `ctrader/bin/Packaged/XauUsdTokyoPressureReversal/Debug/net6.0/XAUUSD.algo`.
- Broker/symbol: FTMO Platform XAUUSD, matching the earlier broker-feed experiments.
- Period: 2020-05-06 through 2026-10-24. This is exposed development history, not an untouched final test.
- Data: historical broker ticks; chart timeframe M1. Record actual tick availability and any shorter actual period. An M1-generated-price run is insufficient for this quote-based signal.
- Starting balance: USD 10,000. Fixed spread setting 1; commission USD 30 per million, for comparability with earlier experiments. Record the platform's spread units and actual settings. This fixed-spread scenario is not evidence of variable historical spreads; include actual spread/latency sensitivity before promotion.
- Test Mode: `Fade` only for the first run.
- Position Label: `XAU_EXP017_TOKYO_V1`.
- Fixed Volume (Units): 1; this is units, not lots. If broker minimum prevents orders, retain the skip logs and resolve the sizing contract before rerunning.
- Allow Live Trading: false.

## Implemented rules

Tokyo local time (Japan, UTC+9): accumulate the 09:00–09:30 window. After 60 valid prior-session warmup observations, qualify if the absolute opening midpoint return is at or above the prior-60-session nearest-rank 90th percentile. Fade the move at the first eligible quote from 09:30. One opportunity per session; zero return gives no trade. Protective stop distance is the median high-low opening range over the prior 20 valid sessions, derived from broker bars. No take-profit; time exit at/after 10:00 via the M1 bar event. Missing/incomplete 30-bar windows are skipped; no interpolation.

The implementation samples the opening midpoint at the first available opening-window tick and the endpoint at the first available quote after 09:30; audit these timestamps from the run. This is an exploratory translation, not a published profitable rule. Do not tune the window, percentile, lookbacks or stop after seeing results.

## Frozen artifacts

- Source SHA-256: `73c474cb81ef2a7cf2bba408a3a8431f43d708033a8a322db3651043213477b5`.
- Package SHA-256: `45450e4e580077909ff05cbd9a75d86c0d0f3274ca1d33377e7840821096b490`.

## Return and review

Return `report.html`, `log.txt`, `events.json` (or exported full trade/event history), and `parameters.cbotset`. Preserve failed attempts separately. Check the log begins with `EXP017_START`, correct build and `mode=Fade`.

Audit timing, warmup, thresholds, volume, protections, order failures, actual covered dates and completed time exits first. Then report net expectancy, profit factor, drawdown, trade count, annual and directional performance, and costs. Negative net expectancy rejects this frozen implementation; execution faults invalidate the run instead. A positive result alone does not pass the vault validation gate.

Continuation, UnconditionalFade and RandomDirection are implemented comparison modes, to run separately on identical settings and dates as needed for the comparison audit. Do not select the best mode as a new optimized strategy. No result has yet been viewed.

## V1 attempt — invalid implementation run

User supplied the V1 startup and session logs through 2026-10-24. Nearly every ordinary opening window reports 29 bars at 09:30; no trades were reported. V1 finalized the window in OnTick while the final closed bar was counted in OnBar, making eligibility dependent on callback order. No strategy-profitability conclusion follows from this attempt. Some dates also show genuine shorter windows; those remain excluded.

V2 reads the completed 09:00–09:29 bars directly from Bars before eligibility in either callback, checks exact consecutive minute timestamps, and avoids double counting when both callbacks fire. It retains the original 30-bar requirement, threshold, stop, volume and clocks. Build: `EXP017_TOKYO_PRESSURE_REVERSAL_V2`. Rerun the same frozen settings with the updated source/package; verify V2 in EXP017_START. Original V1 hashes above identify the invalid attempt and are not the current artifact hashes.

V2 source SHA-256: `e5f256e4f41059f58ad4b52b5963364dba24656d029cde8c6cfab6b2c1fc86a6`.

## CLI tick-data availability — 2026-11-11

The unchanged V2 package now executes through the official headless CLI. The
frozen full-period request returned native exit 86, `No historical data for the
specified period`; this is not a trading result. A separately preregistered
2026-10-19–2026-10-24 diagnostic confirms recent broker tick replay but had only
five warmup sessions and zero trades. It cannot substitute for the original
experiment or establish profitability. Native spread metadata is null.
See [[cTrader tick history availability probe - 2026-11-11]] and its linked native
artifacts, hashes, closure evidence and audit revision.

V2 package SHA-256: `f9c4588cadb3bf88a881efa1ff2878efdb7a97fb872d29d14271dec41d0c9a5f`.

Verification: actual API package build passed with zero warnings/errors; 428 synthetic harness checks passed, including 14 Tokyo boundary checks. This is implementation evidence only.

## V2 returned result

[[../outputs/EXP-017-tokyo-fade-v2-m1-2026-11-10/RUN-NOTE|Archived V2 audit]]: 201 trades, -$307.74, PF 0.51; all years negative. Report explicitly uses M1 server bars, so this is a negative exploratory run and the frozen historical tick-data test is still pending. Rerun unchanged Fade settings with historical broker ticks.

## Available-history tick replication — 2026-11-11

Native run [[Lab run 20261111T181053_25439c - T002]] requested
2025-06-23–2026-10-24, selected from broker history metadata before returns.
Native startup is 2025-06-23 22:00:02.050 UTC (2025-06-24 07:00 Japan), so
continuous coverage from the requested midnight is not established. This shorter
exposed-history replication does not complete the original six-year request.

Report: 41 trades; net **+$25.18**, ROI +0.25%, PF 1.10; maximum equity drawdown
1.85094%. Mean net trade $0.614. Gross $35.02, commissions -$9.84, swaps zero.
2025: 11 trades, +$81.53. 2026: 30 trades, -$56.35. Long: 18 trades,
+$35.18; short: 23 trades, -$10.00. No open positions or pending orders at end.
All report entries are at 09:30 Japan; holding times 4.197–1800.530 seconds
include stop exits. Logged warmup progresses from 0 to 59 over 60 valid sessions.
There are no logged order failures, stop-geometry rejections or failed time closes.
These log-based counts are limited by the gap below.

**Evidence remains under review; no promotion.** The report confirms server ticks,
USD 10,000 and USD 30/million commission but `spread` is null. Observed bid/ask
spreads vary, so the requested fixed 1-pip scenario is not proven. Console has
39 signals/closed events versus 41 report trades; the 2025-09-25 and 2025-09-29
entries lack corresponding signal records. Preserve that discrepancy; do not
invent event logs or certify all protections from report comments alone.
The native engine stopped with a complete matching report, then the platform
closed its lingering owned CLI transport using its completion guard.

The small positive aggregate, negative 2026 result and incomplete execution
reconciliation do not pass validation. Frozen comparison modes, cost sensitivity
and independent confirmation remain outstanding. Exact source, generated observer,
package and evidence hashes are in the native job record.
