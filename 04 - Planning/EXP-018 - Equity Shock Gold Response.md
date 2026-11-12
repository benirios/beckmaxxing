---
tags: [experiment, xauusd, ctrader]
experiment_id: EXP-018
status: exploratory_under_review
date: 2026-11-11
---

# EXP-018 — Equity-shock gold response

Status: Exploratory available-history tick result recorded; full-period evaluation and validation remain incomplete.

Canonical hypothesis: T006. Source:
[[../ctrader/XauUsdEquityDrawdownFlightToGold.cs]].

Frozen primary: FTMO XAUUSD M1 chart, historical server ticks,
2020-05-06–2026-10-24 exposed development interval, USD 10,000 initial balance,
commission USD 30 per million USD volume, fixed spread 1 pip if the platform
offers that setting in tick mode. Record actual available settings; never
substitute M1-generated prices and call them ticks.

Parameters: reference `US500.cash` (the symbol observed on this broker),
one unit fixed volume, label `XAU_T006_FLIGHT_GOLD_V1`, Allow Live Trading false.
If the actual broker symbol differs or reference history is unavailable, record
the configuration/data failure before attempting a corrected run.

Rules: completed regular-session S&P 500 M5 return below or equal to the
nearest-rank first percentile of the preceding 4,680 retained regular-session
returns, and negative, generates a gold long at the next eligible quote.
No overlapping managed position; 30-minute holding time; no stop or target.
The source retains a fixed bar count, not necessarily 60 complete calendar
sessions. Do not rewrite that distinction after seeing performance.

Audit synchronized reference history, warmup availability, reference bar close
versus signal/entry time, stale startup signals, actual holding times, skipped
and failed orders, account depletion, and positions still open at end. Historical
signals processed at startup must not be mistaken for newly available signals.
Native costs and fills remain the platform's responsibility.

Save report, events, log, parameters, source/package hashes and settings before
running another bot. Report trade count, net expectancy, PF, drawdown, costs,
annual and directional results. Negative net expectancy rejects this frozen
translation; temporal or execution defects invalidate it. A positive primary
requires separately frozen matched controls and robustness before promotion.

Package staged at `/Users/beni/cAlgo/Sources/Robots/XauUsdEquityDrawdownFlightToGold.algo`.
Package SHA-256: `c52f379d375aae4accde5461d8890768f04675daa887170097c4dc58b66f953b`.

## Result

Pending. UI-control timeouts have prevented a verified launch as of this note.

## Available-history tick replication — 2026-11-11

Supersedes the pending-launch checkpoint above. Native run
[[Lab run 20261111T181528_6e8c25 - T006]] requested 2025-06-23–2026-10-24,
frozen from history availability before returns. Source/observer/package hashes
are preserved in the job. US500.cash reference history loaded successfully;
startup identifies the expected V1 build and 4,680 prior-return warmup contract.
Actual startup is 2025-06-23 22:00:02.050 UTC, after requested midnight.
Original six-year request remains incomplete.

Report: **195 long trades, +$322.77 net, +3.23% ROI, PF 1.27**;
maximum equity drawdown 2.88385%. Mean net trade $1.655. Gross $369.57;
commissions -$46.80; swaps zero. 2025: 60 trades, +$157.24.
2026: 135 trades, +$165.53. No open positions or pending orders at end.

Console reconciliation: 248 signals = 195 entries + 53 overlapping-position
skips; 195 successful time exits; no logged order failures or failed exits.
Entry lags relative to the logged completed reference-bar timestamp range
0.155–61.611 seconds. Report holding times range 1,800.000–1,861.920 seconds.
The OnBar/OnTick implementation therefore produces delayed entries and exits;
do not describe the result as exact reference-close execution or exact 30-minute
fills. All logged entries occur after their reference close, and the first entry
is September 2025, after the retained-history warmup rather than at startup.
Independent reconstruction of synchronized reference bars and percentile
eligibility is still outstanding; successful symbol loading does not prove it.

**Exploratory positive result, requiring review; no promotion.** Native report
confirms historical server ticks, initial USD 10,000 and USD 30/million commission,
but spread metadata is null. Logged bid/ask spreads vary. This is not a verified
fixed 1-pip scenario. The platform closed its lingering CLI transport only after
native stop and complete matching report evidence. Matched controls, realistic
cost sensitivity and independent confirmation are still required by validation.
