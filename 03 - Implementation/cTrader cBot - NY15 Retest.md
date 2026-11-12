---
tags: [xauusd, orb, ctrader, cbot, h2, retest]
status: completed-rejected
experiment: EXP-006
---

# cTrader cBot — NY15 First-Touch Retest

EXP-006 tests the breakout-then-retest entry family in
[[../01 - Research/Research Hypotheses#Preregistered retest variant — EXP-006|H2]]
without changing the NY15 opening range, stop, target, risk, or session exit.

## Locked signal state machine

1. Build the 09:30–09:45 New York opening range from exactly 15 M1 bars.
2. From 09:45 through 11:59, the first M1 close strictly above the range high
   or below the range low arms that direction. The first break is final; the
   strategy cannot reverse direction that session.
3. The breakout bar cannot also be the retest bar.
4. After arming, wait for the first later M1 bar whose low reaches the broken
   high for a long, or whose high reaches the broken low for a short.
5. That first touch is the session's only retest attempt. It confirms only if
   it closes strictly back outside the broken boundary in the breakout
   direction. A close on the boundary or inside the range fails and ends the
   session without a trade.
6. Enter at market on the next M1 bar after a confirmed retest. The final
   eligible confirmation bar is 11:59–12:00, producing a 12:00 entry.
7. If no breakout or no retest occurs before the deadline, do not trade.

This definition intentionally avoids a tolerance band, repeated retest
attempts, or a selectable confirmation candle.

## Unchanged comparison rules

- Stop at the opposite NY15 opening-range boundary.
- Target at 1R from actual market entry.
- One possible trade per session.
- Close any open position at 16:00 New York.
- Default position risk is 0.5% of equity.
- Live trading is disabled by default.
- Initial stop and target are attached to the market order; missing or
  wrong-side protection triggers an immediate safety close and an explicit log.

## Artifacts

- Source: `ctrader/XauUsdNy15RetestOrb.cs`.
- Project: `ctrader/XauUsdNy15RetestOrb.csproj`.
- Build marker: `EXP006_NY15_FIRST_RETEST_1R_V1_20261101`.
- Source SHA-256: `e6039a59b57e22061cee407c127e01ac5848fa35f8148c34fb97d034ef1423cc`.
- Release `.algo` SHA-256: `b4726e911e29c1d0e41ce6fbfbec9ccac6813050291bc83661f6a3ffa60d1a41`.

Release build verified on 2026-11-01 against `cTrader.Automate` 1.0.21 with
zero warnings and zero compiler errors. The generated cBot is at
`ctrader/bin/Ny15RetestOrb/Release/net6.0/XAUUSD.algo`.
An identical copy was published to
`/Users/beni/cAlgo/Sources/Robots/XAUUSD ORB NY15 Retest.algo` for cTrader.

## Evaluation

Primary success requires positive net expectancy and profit factor above 1.0.
Secondary comparisons against EXP-001 include trade count, drawdown, yearly
stability, long/short results, and gross-versus-net performance. The full test
period is already exposed, so this is a comparative in-sample research result,
not untouched out-of-sample validation.

## Result received 2026-11-01

The completed run returned -45.90% ROI, 0.66 profit factor, -$6.83 average
trade, and 47.02% maximum balance drawdown across 672 trades. The state machine
reconciled exactly, every order had correctly sided protection, and no order or
protection failures occurred. Gross and net results were negative in every
calendar-year slice. Reject EXP-006; see
[[../outputs/EXP-006-ny15-retest-2026-11-01/RUN-NOTE|archived run note]].

