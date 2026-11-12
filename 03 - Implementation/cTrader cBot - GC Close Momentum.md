---
tags: [xauusd, ctrader, cbot, h6, intraday-momentum, comex-close]
status: completed-rejected
experiment: EXP-008
---

# cTrader cBot — GC Close Momentum

EXP-008 is the first post-ORB hypothesis. It attempts to replicate the COMEX
gold component of Baltussen, Da, Lammers, and Martens (2021), which reports that
the return before the final half-hour predicts the 13:00–13:30 New York return.
The source studies GC futures; this experiment tests whether the direction
transfers to spot XAUUSD after executable cTrader costs.

## Locked signal

1. Use the source paper's GC volume-based trading hours: 08:20–13:30 New York.
2. Retain the previous valid session's 13:30 close, represented by the close of
   the 13:29 M1 bar.
3. Require all 280 M1 bars from 08:20 through 12:59 in the current session.
4. At exactly 13:00, calculate `12:59 close / prior 13:30 close - 1`.
5. Enter long when the return is positive or short when it is negative. Skip a
   zero return or any session lacking the exact timestamps or required bars.
6. Exit at 13:30. There is no price stop or profit target because the tested
   mechanism is the paper's unprotected final-half-hour timing position.

## Exposure and execution

- Default notional exposure is 1.0× current account equity.
- XAUUSD volume in units is calculated as `equity / executable entry price` and
  rounded down to the broker's valid volume step.
- Buy at ask and sell at bid by market order at 13:00.
- One signal and at most one position per session.
- Close at the first available quote at or after 13:30; log data gaps.
- Live trading is disabled by default.

The source paper does not include transaction costs in its timing results and
explicitly warns that frequent rebalancing may make the effect unexploitable.
EXP-008 therefore retains the same fixed spread and commission settings as the
earlier cTrader experiments.

## Artifacts

- Source: `ctrader/XauUsdGcCloseMomentum.cs`.
- Project: `ctrader/XauUsdGcCloseMomentum.csproj`.
- Build marker: `EXP008_GC_CLOSE_MOMENTUM_1X_V2_20261101`.
- Robot class: `XauUsdGcCloseMomentumV2`.
- Source SHA-256: `9d2cb12cd4b0eb00cc6cfa390c2b338aa7f956605dac47a8d9fcea9e2192b21a`.
- Repository release `.algo` SHA-256: `e2b1a22ce6466fcac12c893d890b3fb460d42dc33713b35aea409adf3b3cb6d1`.

Release build verified on 2026-11-01 against `cTrader.Automate` 1.0.21 with
zero warnings and zero compiler errors. The generated cBot is at
`ctrader/bin/GcCloseMomentum/Release/net6.0/XAUUSD.algo`.
This build was published as a uniquely named source project at
`/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Close Momentum V2/` and compiled to
`/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Close Momentum V2.algo`. Select the
`XauUsdGcCloseMomentumV2` robot rather than the old `XAUUSD ORB` instance.

## Evaluation

Primary success requires positive net expectancy and profit factor above 1.0
after the configured spread and commission. Secondary metrics are gross versus
net performance, yearly stability, long/short results, drawdown, and the count
of skipped or delayed sessions.

Do not tune the 08:20/13:00/13:30 timestamps, add a predictor threshold, alter
direction, change exposure, or add stops/targets after viewing EXP-008. All test
dates are already exposed, and GC futures evidence does not establish an edge
in spot XAUUSD.

## Invalid V1 run and correction

The first backtest received on 2026-11-01 is invalid as the final hypothesis
test. V1 treated Sunday-evening reopen dates as missing GC sessions, erased
Friday's valid close, and systematically skipped the following Monday signal.
Its 1,302-trade subset was severely negative (PF 0.28), but it did not evaluate
the preregistered valid-session set.

V2 adds a session-activation guard: only a date with at least one bar inside the
08:20–13:00 predictor window can invalidate a missing 13:30 close. No research
rule changed. See
[[../outputs/EXP-008-invalid-weekend-reset-2026-11-01/RUN-NOTE|invalid-run note]].

A second supplied run used the correct 2026-10-24 endpoint but still launched
the cached V1 `XAUUSD ORB` instance. It is archived at
[[../outputs/EXP-008-invalid-v1-rerun-2026-11-01/RUN-NOTE|second invalid V1 run]].
The V2 robot was subsequently given a distinct class and cTrader source-project
identity to prevent reuse of that cached instance.

## Valid V2 result received 2026-11-02

The corrected run returned -55.72% ROI, 0.27 profit factor, -$3.43 average
trade, and 55.78% maximum balance drawdown across 1,626 trades. Session-state
integrity passed: Mondays retained Friday closes, every entered signal had 280
predictor bars, and all positions closed. Gross P/L was -$4,577.07, both
directions lost, and every annual slice was negative. Reject EXP-008; see
[[../outputs/EXP-008-gc-close-momentum-v2-2026-11-02/RUN-NOTE|valid V2 run]].

## Primary source

- Baltussen, Da, Lammers, and Martens, “Hedging demand and market intraday
  momentum,” *Journal of Financial Economics* 142 (2021), 377–403.
  https://doi.org/10.1016/j.jfineco.2021.04.029

