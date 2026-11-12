---
tags: [xauusd, ctrader, cbot, h9, mean-reversion, exp-013]
status: completed-rejected
experiment: EXP-013
---

# cTrader cBot — Extreme Move Reversal

EXP-013 is a low-confidence XAUUSD transfer test inspired by commodity-futures
research that defines overreaction as a large price change followed by a
proportional reversal. The published sample is short and pandemic-focused; it
reports precious metals as less prone to overreactions than energy. Our rule is
an explicit preregistered operationalization, not a replication of the paper's
non-parametric detector.

## Locked rule

- At 10:00 New York, require exactly 30 M1 bars from 09:30 through 09:59.
- Calculate the 09:30 open to 09:59 close move.
- Calculate 20-bar average true range from bars completed before 09:30.
- If the absolute 30-minute move is at least 2.0x that ATR, enter against it
  immediately at 10:00.
- Exit at exactly 10:30. No stop or target.
- One trade maximum per New York date, 1.0x equity notional by default.
- No tuning of threshold, lookback, timestamps, exit, or weekdays after result.

## Artifacts

- Source: `ctrader/XauUsdExtremeMoveReversal.cs`
- Project: `ctrader/XauUsdExtremeMoveReversal.csproj`
- Build marker: `EXP013_EXTREME_MOVE_REVERSAL_V2_20261103`
- Robot class: `XauUsdExtremeMoveReversalV2`
- Source SHA-256: `5e6f60d7e051ab0fc04cf3791be506f9fff078fdd6c7579ae68e1260274959d0`

The repository source compiles with zero warnings and errors using
`cTrader.Automate` 1.0.21. Import the V2 `.cs` source into cTrader and build it
there before the manual backtest.

### Pre-run implementation correction — 2026-11-03

The never-run V1 source was retired during a pre-backtest audit. It counted the
09:59 bar after evaluating the signal, measured only the 09:59 candle rather
than the full 09:30-open to 09:59-close move, and calculated ATR from bars
inside the signal window. V2 fixes all three defects and explicitly requires:

- the exact 30 bars from 09:30 through 09:59;
- the move from the 09:30 open to the 09:59 close; and
- true range from the exact 20 completed bars from 09:10 through 09:29.

No market result was exposed under V1, so this is an implementation correction
to the preregistered rule, not post-result tuning. Do not run the previously
published V1 package.

### Invalid wrong-robot attempt — 2026-11-03

The first supplied manual-run bundle was an EXP-005 rerun from the existing
`XAUUSD ORB` project. Its log contains the
`EXP005_NY30_WIDTH_P70_L60_1R_V1_20261030` marker and no `EXP013_*` events.
It is archived at
`outputs/EXP-013-invalid-wrong-bot-2026-11-03/` and provides no EXP-013
evidence. The next run must use `XauUsdExtremeMoveReversalV2` and show the V2
build marker in the startup line.

## Backtest

Use XAUUSD M1, 2020-05-06–2026-10-24, $10,000 initial balance, fixed spread 1,
$30/million commission, and equity notional 1.0x. The dates are already
exposed. Primary gates: positive net expectancy and PF above 1.0 after costs.

### Completed V2 result — 2026-11-03

Rejected: -50.52% ROI, PF 0.45, 1,117 trades, and 50.69% maximum equity
drawdown. Gross P/L after the fixed spread and before commission was already
-$4,316.04. Both directions and every represented year were negative. All
positions held exactly 30 minutes, and there were no order failures. See
[[../outputs/EXP-013-extreme-move-reversal-2026-11-03/RUN-NOTE|the full run note]].

## Source

Akram, Q. F. and O. E. C. (2020), “Price overreactions in the commodity futures
market: An intraday analysis of the Covid-19 pandemic impact,” *Resources
Policy* 69, 101922. https://doi.org/10.1016/j.resourpol.2020.101922
