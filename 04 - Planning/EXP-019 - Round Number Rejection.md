---
tags: [experiment, xauusd, ctrader]
experiment_id: EXP-019
status: exploratory_under_review
date: 2026-11-11
---

# EXP-019 — Round-number rejection

Status: Both available-history tick branches recorded; negative primary; full-period evaluation and validation remain incomplete.

Frozen primary `RoundHundreds`, then separate `NonRoundPlacebo` control on the
same history, costs and volume. FTMO XAUUSD M1 chart, historical server ticks,
2020-05-06–2026-10-24 exposed development interval, USD 10,000 initial balance,
commission USD 30 per million USD volume, fixed spread 1 pip if available in
tick mode. Record actual settings and historical coverage. M1-generated prices
are insufficient for this next-quote strategy.

One unit fixed volume; label `XAU_T015_ROUND_REJECT`; Allow Live Trading false.
The source fixes the $100 grid and $37.50 placebo offset; neither may be tuned.

Rules: track previously untouched levels from midnight New York; fade the first
eligible crossing during 08:00–12:00 New York on a later quote within two seconds.
Protect with the range of the preceding 15 complete M1 bars. No target;
15-minute exit. At most one entry attempt per day. Day-start coverage, contiguous
bars, quote gap, valid broker stop geometry and minimum volume are required.

Audit day-start and quote coverage, touch versus entry timestamps, quote-side
pricing, stale-quote skips, warmup, stop geometry, actual volume, protection and
close failures, full time exits and final open positions. Repeated touches are
not independent opportunities. Preserve the primary and placebo separately.

Save report, events, log, parameters, source/package hashes and settings for each
branch. Report net expectancy, trade count, PF, drawdown, costs, yearly and
directional performance, and matched placebo interpretation. Negative expectancy
rejects the frozen strategy; execution defects invalidate the run. Positive
returns alone do not establish special round-number predictability.

Package staged at `/Users/beni/cAlgo/Sources/Robots/XauUsdRoundNumberRejection.algo`.
Package SHA-256: `56d605e09fd278b752d6e0280ccc8784f8c2ebe04caa4b9605f7cece3628739a`.

## Results

Primary pending. Placebo pending. Desktop-control timeouts have prevented a
verified launch as of this note.

## Available-history primary — 2026-11-11

Native run [[Lab run 20261111T182129_f60476 - T015]], RoundHundreds,
requested 2025-06-23–2026-10-24, selected from history availability before returns.
Native startup is 2025-06-23 22:00:02.050 UTC; first partial local day is excluded
by the day-start guard. This does not complete the original six-year request.

**73 trades, -$330.85 net, -3.31% ROI, PF 0.53**, maximum equity drawdown
3.67575%. Mean net trade -$4.532. Gross -$313.33, commission -$17.52,
zero swaps. 2025: 23 trades, -$48.59. 2026: 50 trades, -$282.26.
Long: 41 trades, -$216.52; short: 32 trades, -$114.33.
No final open positions or pending orders.

Console reconciles 73 touches, signals, entries and closes. There are 47
successful timed closes and 26 stop-loss closes, with no logged order or close
failures. All 73 next-quote lags are positive and within two seconds
(0.001–0.665 seconds). Report holding times are 0.761–900.992 seconds;
shorter holds include stops. There are 72 no-full-day-touch-history skip records.
The worst trade is -$116.16 (2026-01-29); do not equate attached stop distance
with a guaranteed execution price.

Negative exploratory replication; no promotion. Native report confirms ticks
and commissioned costs but has null spread metadata; fixed 1 pip is unverified.
Independent full-tick reconstruction of untouched-level history, stop geometry
and market gaps remains outstanding. Native artifacts and hashes are retained;
owned CLI transport closure occurred only after native stop and matching report.
The separately frozen NonRoundPlacebo branch is running on identical settings.

## Available-history placebo and comparison — 2026-11-11

[[Lab run 20261111T182319_43e199 - T015]] ran NonRoundPlacebo on identical
2025-06-23–2026-10-24 history, original volume and requested costs.
**86 trades, +$82.42 net, +0.82% ROI, PF 1.19**, maximum equity drawdown
1.29153%. Mean net trade $0.958. Gross $103.06, commissions -$20.64,
swaps zero. 2025: 25 trades, +$43.90; 2026: 61 trades, +$38.52.
Long: 48 trades, +$75.78; short: 38 trades, +$6.64.
No final open positions or pending orders.

All 86 touches/signals/entries/closes reconcile. There are 67 successful timed
closes and 19 stop-loss closes; no logged order/close failures. Next-quote lags
0.001–0.664 seconds satisfy the two-second limit. Holds span 1.005–900.602 seconds.
72 partial-day skips and 2 stale-touch skips are retained. The expected placebo
startup and source observer stamp are present. Native spread metadata remains
null, and independent full-tick eligibility reconstruction remains outstanding.

The round-number primary loses $330.85 while the non-round branch gains $82.42;
primary minus placebo is **-$413.27**. The branches share dates/rules/costs but
have different eligible touches and trade counts, so this is not a paired
same-event statistical estimate. It supplies no evidence of a special positive
round-number effect. Retain the frozen primary as a negative exploratory result;
do not optimize the placebo offset or promote its modest positive return.
Both branches completed native replay; both retain evidence-review limitations.
