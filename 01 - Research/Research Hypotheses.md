---
tags: [xauusd, orb, hypotheses]
status: active
---

# Research Hypotheses

Each hypothesis must receive its own versioned experiment. A filter is computed using information available before entry, with rolling statistics shifted by at least one complete session.

## H1 — Volatility-filtered 30-minute ORB

**Claim:** a 30-minute ORB performs better during unusually active sessions.

Compare the unfiltered baseline against filters based on:

- lagged ATR;
- pre-entry realized volatility;
- opening-range width percentile using past sessions only;
- overnight range or activity.

Primary question: does filtering improve net expectancy and stability after reducing the trade count?

### Preregistered first variant — EXP-005

- Baseline: the EXP-003 New York 30-minute, M1-close-confirmed, next-bar,
  opposite-boundary stop, 1R target, 16:00 exit configuration.
- Activity measure: the completed 09:30–10:00 New York opening-range width.
- History: the previous 60 valid sessions only. The current session is added to
  the rolling window after its filter decision.
- Threshold: nearest-rank 70th percentile of those 60 prior widths. Sort widths
  ascending and select rank `ceil(0.70 × 60)` using one-based ranks.
- Eligibility: trade only when the current width is greater than or equal to the
  threshold. The first 60 valid sessions are warmup and cannot trade.
- Missing sessions: invalid opening ranges are neither traded nor added to the
  rolling history.
- No parameter search: do not test alternate lookbacks or percentiles after
  inspecting EXP-005.
- Primary success rule: positive net expectancy and profit factor above 1.0,
  plus improvement over EXP-003 on the same post-warmup/eligible-session basis.
- Secondary metrics: trade count, drawdown, yearly stability, long/short results,
  and gross-versus-net performance.

This filter is information available at 10:00 before any eligible entry and is
computed exclusively from prior sessions. See
[[../03 - Implementation/cTrader cBot - NY30 Width Filter|implementation note]].

**Result:** EXP-005 rejected this first H1 variant. It returned -9.94% ROI,
0.86 profit factor, and -$2.18 average net P/L across 456 trades. The run passed
filter and protection integrity checks, but gross and net expectancy remained
negative and six of seven calendar-year slices lost money. The modest matched-
date improvement over EXP-003 (PF 0.82 to 0.86) did not satisfy the preregistered
profitability gates. Do not test nearby percentile or lookback values. See
[[../outputs/EXP-005-ny30-width-p70-2026-11-01/RUN-NOTE|EXP-005]].

## H2 — New York 15-minute ORB

**Claim:** a 15-minute New York opening range contains useful directional information.

Compare:

1. immediate stop entry;
2. candle-close confirmation followed by next-bar entry;
3. breakout followed by a retest.

The current implementation is the close-confirmation baseline; see [[../03 - Implementation/Baseline Strategy Specification]].

**Initial result:** the cTrader implementation of the close-confirmed, 1R version lost 74.74% over May 2020–October 2026 (maximum balance drawdown 75.84%, PF 0.70). This rejects that tested configuration on the tested FTMO M1-server-bar setup. See [[../04 - Planning/Experiment Register#EXP-001 — cTrader NY15 baseline|EXP-001]]. This result does not validate alternate H2 entry variants, and the 2022–2026 period is now exposed.

### Preregistered retest variant — EXP-006

- Opening range: 09:30–09:45 New York, requiring exactly 15 complete M1 bars.
- Breakout: the first M1 close strictly outside either range boundary from
  09:45 through 11:59. This permanently fixes the session direction.
- Retest: beginning with the next M1 bar, the first bar whose range reaches the
  broken boundary is the only retest attempt. For a long, its low must be at or
  below the range high and its close strictly above it. For a short, its high
  must be at or above the range low and its close strictly below it.
- Failed retest: if that first touching bar closes on the boundary or does not
  close outside in the breakout direction, consume the session without entry.
- Entry: market on the next M1 bar after a confirmed retest. A retest confirmed
  by the 11:59–12:00 bar may enter at 12:00; no later entry is allowed.
- Risk and exit: opposite opening-range boundary stop, 1R target, 0.5% equity
  risk, one trade maximum, and 16:00 New York time exit.
- No parameter search: do not add a retest tolerance, allow multiple touches,
  change the deadline, or change the target after inspecting EXP-006.
- Primary success rule: positive net expectancy and profit factor above 1.0.
  Secondary metrics are drawdown, trade count, yearly stability, long/short
  results, and improvement over EXP-001.

All tested dates are already exposed. EXP-006 is therefore a comparative test,
not untouched out-of-sample validation. See
[[../03 - Implementation/cTrader cBot - NY15 Retest|implementation note]].

**Result:** EXP-006 rejected the locked first-touch retest variant. It returned
-45.90% ROI, 0.66 profit factor, and -$6.83 average net P/L across 672 trades.
Every annual slice and both directions were negative, while the state machine
and order protection passed their integrity checks. Relative to EXP-001, the
smaller cumulative loss came from fewer trades; profit factor and average trade
were worse. Do not tune retest tolerance, touch count, deadline, or target. See
[[../outputs/EXP-006-ny15-retest-2026-11-01/RUN-NOTE|EXP-006]].

### Preregistered immediate stop-entry benchmark — EXP-007

- Opening range: 09:30–09:45 New York, requiring exactly 15 complete M1 bars.
- Trigger window: 09:45 through 11:59:59 New York.
- Trigger: the first bid strictly above the range high buys immediately at the
  available ask; the first bid strictly below the range low sells immediately
  at the available bid.
- OCO rule: the first boundary trigger consumes the session before sizing or
  order submission. No re-arming or opposite-direction trade is allowed.
- Gap rule: if the first available quote is already beyond a boundary, enter at
  that quote and calculate position size from executable entry to stop.
- Risk and exit: opposite opening-range boundary stop, 1R target, 0.5% equity
  risk, one trade maximum, and 16:00 New York time exit.
- Implementation: quote-triggered market entry with stop and target attached to
  the initial order. This is the protected equivalent of an OCO stop-entry for
  this cTrader Robot API.
- No parameter search: do not add an entry buffer, change quote side, move the
  deadline, re-arm a failed order, or change the target after viewing EXP-007.
- Primary success rule: positive net expectancy and profit factor above 1.0,
  plus improved conditional trade quality over EXP-001.

The M1 server-bar engine may synthesize intraminute quote paths, so EXP-007 is a
comparative bar-model benchmark rather than a tick-level execution audit. All
dates are already exposed. See
[[../03 - Implementation/cTrader cBot - NY15 Immediate Stop Entry|implementation note]].

**Result:** EXP-007 rejected the immediate stop-entry benchmark. It returned
-75.83% ROI, 0.69 profit factor, and -$4.59 average net P/L across 1,652 trades.
Every annual slice and both directions were negative; trigger and protection
integrity passed. Its profile was effectively the same as the rejected EXP-001
close-confirmed baseline. Combined with EXP-006, all three planned H2 entry
families are rejected on this setup. See
[[../outputs/EXP-007-ny15-immediate-stop-2026-11-01/RUN-NOTE|EXP-007]].

## H3 — London failed-breakout reversal

**Claim:** fading a failed London breakout produces better expectancy than following the first breakout.

**Preregistered first variant (EXP-002):**

- Session timezone: `Europe/London` (IANA/DST-aware).
- Opening range: 08:00–08:29 London time (30 complete M1 bars).
- Breakout: first M1 close strictly outside the range from 08:30 through 11:00.
- Failure: after that close, the first M1 close back inside the range; enter at the next M1 open in the direction of the range interior.
- Stop: farthest excursion beyond the broken boundary from breakout through failure confirmation.
- Target: opposite opening-range boundary. Skip if that target is not beyond the actual entry in the trade direction.
- Limits: one attempt per session; exit at 12:00 London if neither stop nor target has hit. No re-arming.
- Execution: use bid/ask data where available; otherwise mark the result exploratory and apply adverse spread/slippage assumptions. Resolve same-bar stop/target ambiguity stop-first.

This is a single hypothesis test, not a parameter search. The cited London path statistics motivate the test but do not establish that this entry/exit rule is profitable. The first cTrader run was invalid because of repeated stop-protection modification failures. A subsequent corrected-build run had no protection failures but still depleted the account by 2024-02-20, before the requested end date. Treat it as a severe negative partial-period result, not a completed six-year evaluation. See [[../03 - Implementation/cTrader cBot - London Failed Breakout]] and [[../04 - Planning/Experiment Register#EXP-002 — London failed-breakout reversal|EXP-002]].

## H4 — Five-minute ORB benchmark

**Claim:** none. This is a required benchmark used to test the common assumption that the shortest opening range is best.

The result must be reported even if decisively negative.

**Result:** EXP-004 rejected the tested New York five-minute, M1-close-confirmed,
next-bar, 1R configuration on the FTMO M1-server-bar setup. It returned -92.22%
ROI with a 0.55 profit factor across 1,663 trades, materially worse than the
already-negative NY15 and NY30 baselines. Protection integrity passed. See
[[../outputs/EXP-004-ny5-2026-10-30/RUN-NOTE|EXP-004]].

## H5 — RSI filter

**Claim:** an RSI condition improves net expectancy, consistency, or drawdown relative to the otherwise identical strategy.

The filter should be rejected if it merely lowers the number of trades while producing no stable out-of-sample improvement.

### Preregistered first variant — EXP-014

- Session: London, using historical local DST rules.
- Opening range: exact M1 bars from 08:00 through 08:29; require all 30 bars.
- Minimum opening-range width: $0.60.
- Entry window: 08:30 through 11:59:59.
- Trigger: bid reaches one tick above the range high or one tick below the
  range low.
- RSI: Wilder RSI(14) on M30 closes, using the latest completed value at the
  breakout. Long requires RSI > 50; short requires RSI < 50.
- Each side's first breakout is evaluated once. An RSI disagreement disables
  that side for the date while leaving the other side available.
- Entry: immediate market order, paying the executable ask/bid spread.
- Stop: one tick beyond the opposite opening-range boundary. No target.
- Exit: stop or 15:00 London time, whichever occurs first.
- Exposure: fixed one XAUUSD unit; one trade maximum per date.
- No parameter search after inspection.
- Primary success rule: positive net expectancy and PF above 1.0 after costs.
- Secondary metrics: gross versus net P/L, annual and directional stability,
  stop/time-exit mix, RSI rejection counts, missing sessions, protection
  integrity, and zero-spread sensitivity.

The public source's London+RSI variant was slightly positive in aggregate but
failed validation and robustness gates. EXP-014 is a locked broker-feed
replication, not a claim that the source established a robust edge. Requiring
all 30 range bars is this project's conservative missing-data adaptation. See
[[../03 - Implementation/cTrader cBot - London RSI ORB|the implementation]].

**Result (2026-11-03):** EXP-014 failed its locked promotion gates on FTMO
Platform XAUUSD M1 server bars: -$1,464.89 net, -14.65% ROI, PF 0.77, and
1,185 trades. All represented years lost net. The run log and report passed
the execution audit. The positive short-side aggregate is a post-result
subgroup and is not a validated strategy. See
[[../outputs/EXP-014-london-rsi-orb-2026-11-03/RUN-NOTE|the run note]].

## H6 — COMEX-close intraday momentum

**Claim:** the sign of XAUUSD's return from the prior COMEX-volume-session close
through 13:00 New York predicts the direction of the final 13:00–13:30 half
hour.

### Preregistered first variant — EXP-008

- Session mapping: use the primary source's COMEX Gold hours, 08:20–13:30 New
  York, on the XAUUSD M1 server-bar series.
- Predictor: `current 12:59 M1 close / previous valid session 13:29 M1 close -
  1`. Require exactly 280 current-session bars from 08:20 through 12:59.
- Entry: market at exactly 13:00; long for a positive predictor, short for a
  negative predictor, and no trade for zero or incomplete data.
- Exit: market at 13:30. No stop or target.
- Exposure: 1.0× current equity notional, rounded down to valid XAUUSD units.
- Costs: use the cTrader run's configured executable spread and commission.
- No parameter search: do not change timestamps, require a return threshold,
  reverse direction, change exposure, or add protection after inspecting the
  result.
- Primary success rule: positive net expectancy and profit factor above 1.0.
- Secondary metrics: gross-versus-net result, yearly stability, long/short
  results, drawdown, and session-integrity counts.

The source evidence is for COMEX futures and its timing returns exclude costs.
EXP-008 is a cross-market replication on already exposed XAUUSD dates, not an
untouched validation. See
[[../03 - Implementation/cTrader cBot - GC Close Momentum|implementation note]].

**Implementation status:** the first V1 run is invalid because Sunday-evening
reopen dates erased Friday's stored close and systematically skipped Monday
signals. Its traded subset was severely negative, but it is not the final H6
test. Corrected V2 changes only calendar/session-state handling and requires a
full rerun. See
[[../outputs/EXP-008-invalid-weekend-reset-2026-11-01/RUN-NOTE|invalid V1 run]].

**Result:** corrected V2 rejects H6 on the tested spot-XAUUSD mapping. It
returned -55.72% ROI, 0.27 profit factor, and -$3.43 average net P/L across
1,626 trades. Gross P/L was -$4,577.07; both directions and every annual slice
were negative. Session and execution integrity passed. Do not reverse or tune
the fully exposed signal after this result. See
[[../outputs/EXP-008-gc-close-momentum-v2-2026-11-02/RUN-NOTE|EXP-008 V2]].

**Matched-control result — EXP-009:** the momentum mode reproduced EXP-008's
economic ledger exactly and underperformed always-long, always-short, and the
preregistered fixed-seed random direction on the same 1,626 dates. ROI/PF were
-55.72%/0.27 for momentum, -55.54%/0.29 long, -51.17%/0.32 short, and
-50.48%/0.33 random. All modes were negative in every year after the configured
fixed spread. Even mechanically removing the spread leaves momentum at about
-$1,438.88 after actual commission and swaps. The controls strengthen rejection
of H6 on this XAUUSD mapping; they do not promote any control to a strategy.
See [[../outputs/EXP-009-exp008-momentum-2026-11-02/RUN-NOTE|EXP-009 final]].

**Deprecated source-market replication — EXP-010:** the provider-neutral Python
engine was cancelled before data acquisition because this project executes
experiments through cTrader cBots. It produced no market result and exposed no
market split. The code is retained only as an audit artifact. See
[[../03 - Implementation/GC Futures Replication Engine|EXP-010 implementation]].

## H7 — Negative COMEX daytime return

**Claim:** the historically negative average return during the COMEX Gold
08:20–13:30 New York daytime session transfers to executable spot XAUUSD.

### Preregistered first variant — EXP-011

- Entry: sell XAUUSD at exactly 08:20 New York.
- Exit: close at exactly 13:30 New York. No stop or profit target.
- Exposure: 1.0x current-equity notional, rounded down to valid XAUUSD units.
- Frequency: at most one position per New York date.
- Primary run: `ShortDay`. Matched control: a separate `LongControl` run with
  otherwise identical dates, exposure, costs, and holding window.
- Data integrity: attach to M1; log the count of the expected 310 bars from
  08:20 through 13:29 and whether the exit occurred exactly at 13:30.
- No parameter search: do not change the session endpoints, add weekdays or
  volatility filters, introduce stops/targets, or change exposure after seeing
  EXP-011.
- Primary success rule: positive net expectancy and profit factor above 1.0 for
  `ShortDay` after configured costs, with an economically better result than
  `LongControl` on matched dates.
- Secondary metrics: gross versus net P/L, drawdown, annual stability, missing
  sessions, swaps, and exact/delayed exits.

The source evidence concerns COMEX futures and related gold assets, not this
broker's XAUUSD instrument. EXP-011 is therefore a cross-market transfer test
on already exposed dates, not untouched validation. See
[[../03 - Implementation/cTrader cBot - GC Daytime Short|implementation note]].

**Result:** reject H7 as an executable strategy. `ShortDay` returned -44.04%
ROI, PF 0.82, and -$2.64 average trade across 1,670 positions; six of seven
annual slices lost. It was better than `LongControl` (-67.03%, PF 0.75), and
the raw session return averaged -0.01547%, but the small and unstable effect
could not cover costs. Mechanically removing the entire fixed spread still
leaves the short arm at -$136.39 after commission and swaps. Do not tune the
session, filter years or holidays, or add protection. See
[[../outputs/EXP-011-short-day-2026-11-02/RUN-NOTE|EXP-011 final]].

## H8 — GLD fifth-half-hour to closing-half-hour momentum

**Claim:** the sign of XAUUSD's 11:30–12:00 New York return predicts its
15:30–16:00 return in the same direction, transferring the source paper's GLD
`r5` to `r13` relationship.

### Preregistered first variant — EXP-012

- Predictor: `11:59 M1 close / 11:30 M1 open - 1`, requiring exactly 30 bars.
- Evaluation: fix the direction at exactly 12:00 New York.
- Entry: market at exactly 15:30; long for positive predictor, short for
  negative predictor, skip zero or incomplete data.
- Exit: market at 16:00. No stop or target.
- Exposure: 1.0x current-equity notional, rounded down to valid units.
- Frequency: one trade maximum per New York date.
- No parameter search: do not alter either half-hour, add a threshold or
  volatility filter, reverse direction, change exposure, or add protection.
- Primary success rule: positive net expectancy and PF above 1.0.
- Promotion rule: also outperform matched always-long, always-short, and
  fixed-seed deterministic-random controls on identical eligible dates.
- Secondary metrics: gross versus net P/L, yearly stability, direction balance,
  drawdown, missing predictor bars, and exact/delayed exits.

The peer-reviewed source studies GLD during its 09:30–16:00 core equity session
and reports positive in-sample and out-of-sample predictability for `r5`. It
does not establish the same behavior for spot XAUUSD or this broker's costs.
EXP-012 is a cross-market transfer test on already exposed dates. See
[[../03 - Implementation/cTrader cBot - GLD R5 Close Momentum|implementation note]].

**Result:** reject H8 on this XAUUSD mapping. The primary returned -52.32% ROI,
PF 0.27, and -$3.24 average trade across 1,616 positions. Every annual slice
and both signal directions lost. More fundamentally, predictor/target raw
correlation was -0.015, directional accuracy was 49.32%, and pre-spread raw P/L
was only +$114.69. A mechanical zero-spread ledger remains -$997.06 after
commission and swaps. Do not tune intervals, thresholds, direction, volatility
subsets, or protection. See
[[../outputs/EXP-012-gld-r5-primary-2026-11-02/RUN-NOTE|EXP-012 final]].

## H9 — Extreme-move intraday reversal

**Claim:** an unusually large completed 30-minute XAUUSD move is followed by a
partial reversal in the next 30 minutes.

### Preregistered first variant — EXP-013

- At 10:00 New York, require all M1 bars from 09:30 through 09:59.
- Measure the 09:30 open to 09:59 close move.
- Compare its absolute size with 2.0 times the mean true range of the previous
  20 completed M1 bars before 09:30.
- If the threshold is met, enter against the move at 10:00 and exit at 10:30.
- Use one trade maximum per date, 1.0x current-equity notional, no stop/target.
- No threshold, lookback, timing, weekday, or exit search after results.
- Primary gates: positive net expectancy and PF above 1.0 after costs.

This operationalization is inspired by commodity-futures overreaction research
focused on the COVID-19 period. The paper reports fewer events for precious
metals than energy, so XAUUSD transfer confidence is low. See
[[../03 - Implementation/cTrader cBot - Extreme Move Reversal|EXP-013 implementation]].

**Result — rejected 2026-11-03.** Corrected V2 produced -50.52% ROI, PF 0.45,
and 1,117 trades. Gross P/L after the fixed spread but before commission was
-$4,316.04; every represented year and both directions were negative. All
positions held exactly 30 minutes and no order failed. See
[[../outputs/EXP-013-extreme-move-reversal-2026-11-03/RUN-NOTE|the run note]].

## Hypothesis-control rules

- Define primary and secondary metrics before running the final test.
- Count all tried variants in the experiment register.
- Prefer broad parameter plateaus to isolated optima.
- Preserve rejected and null results.
- Do not add a new filter solely to repair a disappointing validation result.

## Related notes

- [[Evidence Register]]
- [[../04 - Planning/Experiment Register|Experiment register]]
- [[../02 - Methodology/Validation Protocol|Validation protocol]]

