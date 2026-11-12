---
tags: [xauusd, orb, ctrader, cbot, h1, volatility-filter]
status: completed-rejected
experiment: EXP-005
---

# cTrader cBot — NY30 Width Filter

EXP-005 tests [[../01 - Research/Research Hypotheses#Preregistered first variant — EXP-005|H1]]
without changing the underlying EXP-003 trade rules.

## Locked filter

At 10:00 New York, after exactly 30 valid opening-range M1 bars:

1. Calculate the current range width as `high - low`.
2. Require 60 prior valid session widths; otherwise log warmup and do not trade.
3. Sort the 60 prior widths ascending.
4. Select the nearest-rank 70th percentile: rank `ceil(0.70 × 60) = 42`.
5. Trade only when the current width is greater than or equal to that threshold.
6. Add the current width to history only after the decision, then remove the
   oldest width to retain 60 observations.

Invalid ranges are excluded from both trading and history. There is no alternate
lookback, percentile, or volatility measure in EXP-005.

## Unchanged execution

- 09:30–10:00 New York opening range.
- First later M1 close strictly outside the range.
- Market entry on the next M1 bar.
- Stop at the opposite range boundary and target at 1R.
- One consumed signal per eligible session.
- Exit at 16:00 New York.
- Default risk is 0.5% of equity; live trading disabled.
- Actual attached stop and target must be present and on the correct side.

## Artifacts

- Source: `ctrader/XauUsdNy30WidthFilteredOrb.cs`.
- Project: `ctrader/XauUsdNy30WidthFilteredOrb.csproj`.
- Build marker: `EXP005_NY30_WIDTH_P70_L60_1R_V1_20261030`.
- Source SHA-256: `aeb636489a80c043d3e38e9763726abe8b4575b3bb7572a5e0c0a9853236eaa0`.
- Release `.algo` SHA-256: `5312df48559928871fb0298b8afd7b911894848e652e7c969544ebdad3dd948f`.

Release build verified on 2026-10-30 against `cTrader.Automate` 1.0.21:
zero compiler errors. NuGet vulnerability-feed checks were unavailable, but the
already-installed package restored and the `.algo` artifact was generated at
`ctrader/bin/Ny30WidthFilteredOrb/Release/net6.0/XAUUSD.algo`.

The local cTrader MCP server 2.0.0 was connected and switched the application to
Automate mode, but its advertised catalog contains no cBot import, compile, or
backtest method. It exposes market/chart/trading/workspace methods only, with no
backtest resource or installed plugin bridge. The historical run therefore still
requires cTrader's Automate UI unless the MCP server adds a backtesting tool.

## Evaluation

The result must be compared with EXP-003 over the same post-warmup period and,
where possible, on the exact eligible session dates. A less-negative result is
not success: the filtered strategy must have positive net expectancy and profit
factor above 1.0 before robustness work is justified.

## Result received 2026-11-01

The completed run returned -9.94% ROI, 0.86 profit factor, -$2.18 average
trade, and 11.09% maximum balance drawdown across 456 trades. Filter and
protection integrity passed, but the primary profitability gates failed. Six of
seven represented calendar years were net negative, and gross P/L before costs
was also negative. Reject EXP-005 and do not test neighboring lookbacks or
percentile thresholds. See
[[../outputs/EXP-005-ny30-width-p70-2026-11-01/RUN-NOTE|archived run note]].
