---
tags: [xauusd, orb, ctrader, cbot, h3]
status: backtested-partial-run
experiment: EXP-002
---

# cTrader cBot — London Failed Breakout

This is the EXP-002 implementation of [[../01 - Research/Research Hypotheses#H3 — London failed-breakout reversal|H3]]. It is a single preregistered variant; building the cBot does not mean the strategy has been backtested or shown to be profitable.

## Source and build

- Source: `ctrader/XauUsdLondonFailedBreakout.cs`
- Project: `ctrader/XauUsdLondonFailedBreakout.csproj`
- Build: `dotnet build ctrader/XauUsdLondonFailedBreakout.csproj`
- Built artifact: `ctrader/bin/LondonFailedBreakout/Debug/net6.0/XAUUSD ORB RESEARCH.algo`
- cTrader installation: `/Users/beni/cAlgo/Sources/Robots/XAUUSD ORB London Failed Breakout.algo`
- cTrader source project: `/Users/beni/cAlgo/Sources/Robots/XAUUSD ORB London Failed Breakout/XAUUSD ORB London Failed Breakout/`

Build verified on 2026-10-25 with cTrader.Automate 1.0.21, zero warnings and zero errors.

The first backtest attempt was invalid: 205 immediate stop-modification failures caused forced closes, and the run depleted equity before its requested end date. A subsequent corrected-build attempt had zero protection failures but still depleted the account on 2024-02-20, before the requested 2026-10-24 end date. It recorded 828 closed trades, -100.01% ROI, and PF 0.48. Its first logged time-exit loss exceeded the planned stop distance materially, so stop/risk execution remains unresolved. The saved position label remained `XAU_NY15_ORB_V1`; London `LFB_*` events confirm the strategy code path. See [[../04 - Planning/Experiment Register#EXP-002 — London failed-breakout reversal|EXP-002]] and [[../outputs/EXP-002-partial-2026-10-27/RUN-NOTE|partial-run artifact note]].

## Locked behavior

| Rule | cBot behavior |
|---|---|
| Chart | XAUUSD M1 |
| Clock | `GMTStandardTime` (London, including DST) |
| Opening range | 08:00–08:29; requires exactly 30 bars |
| Breakout | First M1 close outside the range, from 08:30 up to but not including 11:00 |
| Failure | First subsequent M1 close strictly inside both range boundaries |
| Entry | Market order at the next bar, faded toward the range interior |
| Stop | Extreme beyond the broken range edge from breakout through failure bar |
| Target | Opposite opening-range boundary |
| Attempts | One breakout/failure attempt per session; no re-arming |
| Deadline | Failure close must occur by 11:00 London; position time-exits at 12:00 |
| Sizing | Default 0.5% equity risk; can select fixed units for controlled comparison |
| Live trading | Disabled by default |

The broker's executable bid/ask quote is used for market entry. cTrader's configured backtest spread, commission, and data mode determine the test costs; record those settings with the HTML report and `.cbotset`. Initial stop/target distances are expressed relative to the order fill; slippage can shift the resulting absolute levels from the intended excursion/range prices, so compare and log both.

## Backtest procedure

In cTrader, select the distinct bot **XAUUSD ORB London Failed Breakout** (not `XAUUSD ORB`). Confirm its log starts with `LFB_START` and does not contain `LFB_PROTECTION_FAILED`. Run an M1 XAUUSD backtest with tick data from server where available. Keep `Allow Live Trading=false`, set `PositionLabel=XAU_LDN_FAIL_V1`, use the intended account's commission/spread settings, and save the report, log, and parameter set to a distinct EXP-002 artifact folder. Compare the cBot dates/signals with a separate research ledger before interpreting performance. The supplied report folder does not contain the underlying market bars needed for Python replication.

EXP-001 already exposed 2020–2026. Any use of those dates for this strategy is exploratory; do not describe it as untouched out-of-sample performance or optimize this variant against those results.
