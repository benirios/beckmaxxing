---
tags: [xauusd, ctrader, cbot, h8, gld, intraday-momentum, exp-012]
status: completed-rejected
experiment: EXP-012
---

# cTrader cBot — GLD R5 Close Momentum

EXP-012 tests whether GLD's documented fifth-half-hour predictability of its
last half-hour transfers to executable spot XAUUSD. The source instrument is a
gold ETF during NYSE core hours; this is a cross-market transfer test, not a
claim that XAUUSD and GLD are interchangeable.

## Locked primary protocol

1. Attach the robot to XAUUSD M1.
2. Require exactly 30 M1 bars from 11:30 through 11:59 New York.
3. At exactly 12:00, calculate `11:59 close / 11:30 open - 1`.
4. Fix the direction for the day: buy for a positive return, sell for a
   negative return, and skip zero or incomplete predictors.
5. Enter by market order at exactly 15:30 New York.
6. Close at 16:00 New York. There is no stop or profit target.
7. Use 1.0x current-equity notional and at most one trade per New York date.

The timestamps map the source paper's fifth regular-session half hour
(`r5`, 11:30–12:00) to its final half hour (`r13`, 15:30–16:00). The class uses
`TimeZones.EasternStandardTime`, so cTrader handles New York DST. It refuses an
M1 predictor missing its exact first bar, final bar, or any of the 30 minutes.
It also refuses a shifted entry if the exact 15:30 bar is absent. Live trading
is disabled by default.

## No post-result search

Do not change the predictor interval, entry/exit timestamps, add a return
threshold, reverse the signal, select high-volatility days, add stops/targets,
or change exposure after inspecting EXP-012. The source reports stronger
predictability in some volatility/jump subsets, but those filters are not part
of this first transfer test and cannot be imported after the result.

## Artifacts

- Source: `ctrader/XauUsdGldR5CloseMomentum.cs`
- Project: `ctrader/XauUsdGldR5CloseMomentum.csproj`
- Build marker: `EXP012_GLD_R5_CLOSE_MOMENTUM_V1_20261102`
- Robot class: `XauUsdGldR5CloseMomentumV1`
- Source SHA-256: `4382abeed5d2e7f40a84638d1c58961449f781960b3c2e39440bee4dceb00cf4`
- Repository release `.algo` SHA-256: `6d3c55642689a61de0743fe8de994051a9ff5e15938dc744f28edf5aa7a73f2b`
- Published cTrader `.algo` SHA-256: `5c7008e3ed1b813cd3d82399edeacae4bd584baa0adfc3dbb77be0817efc83d2`
- Build package:
  `ctrader/bin/GldR5CloseMomentum/Release/net6.0/XAUUSD.algo`
- Published cTrader project:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD GLD R5 Close Momentum V1/`
- Published package:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD GLD R5 Close Momentum V1.algo`

Both repository and published-project release builds completed against
`cTrader.Automate` 1.0.21 with zero warnings and zero errors. Their `.algo`
hashes differ because the project/assembly names differ; their robot source is
byte-for-byte identical.

## Backtest protocol

- Symbol/timeframe: XAUUSD M1.
- Period: 2020-05-06 through 2026-10-24, already exposed.
- Starting balance: $10,000.
- Data: FTMO Platform M1 server bars.
- Costs: fixed spread 1 and commission $30 per million, matching prior runs.
- Leave `Sizing Mode = EquityNotionalLeverage` and
  `Equity Notional (x) = 1.0`.
- Leave `Allow Live Trading = false`.

Primary acceptance requires positive net expectancy and PF above 1.0 after
configured costs. Before promoting the signal, it must also outperform
always-long, always-short, and fixed-seed deterministic-random directions on
the same eligible dates. Those diagnostic controls will receive separate,
unambiguous builds after the primary is archived; do not substitute a control
for this primary robot.

## Evidence

- Xu, Bouri, Saeed, and Wen, “Intraday return predictability: Evidence from
  commodity ETFs and their related volatility indices,” *Resources Policy* 69
  (2020), 101830: https://doi.org/10.1016/j.resourpol.2020.101830
- NYSE core-session hours: https://www.nyse.com/trade/trading-information
- GLD objective and relationship to gold bullion:
  https://www.ssga.com/us/en/individual/etfs/spdr-gold-shares-gld

The paper reports that GLD `r5` positively predicts `r13`, with an in-sample
t-statistic of 3.03, in-sample R-squared of 0.49%, and positive out-of-sample
R-squared of 0.26%. The effect is small, source-market-specific, and not treated
as proof of executable XAUUSD profitability.

## Primary result received 2026-11-02

The correct primary run lost 52.32% with PF 0.27 across 1,616 trades and 52.36%
maximum equity drawdown. Both directions and every represented calendar year
were negative after the configured spread. Predictor/target raw-return
correlation was -0.015 and directional accuracy was 49.32%.

The equity-sized raw move before spread contributed only +$114.69. Removing the
entire fixed spread still leaves -$997.06 after commission and swaps. Exact-exit
and complete-path sensitivities remain severely negative. Reject EXP-012; the
absolute gates fail, so promotion controls are unnecessary. See
[[../outputs/EXP-012-gld-r5-primary-2026-11-02/RUN-NOTE|primary result]].
