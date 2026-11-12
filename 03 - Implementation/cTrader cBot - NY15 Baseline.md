---
tags: [xauusd, orb, ctrader, cbot, h2]
status: compiled-ready-for-backtest
experiment: EXP-001
---

# cTrader cBot — NY15 Baseline

## Correlated study

This cBot implements **H2 — New York 15-minute ORB** from [[../01 - Research/Research Hypotheses#H2 — New York 15-minute ORB|Research Hypotheses]]. It is the executable cTrader counterpart of [[Baseline Strategy Specification]].

Source:

- `ctrader/XauUsdNy15OrbBaseline.cs`
- `ctrader/XauUsdNy15OrbBaseline.csproj`

Experiment record: [[../04 - Planning/Experiment Register#EXP-001 — cTrader NY15 baseline|EXP-001]].

## Study-to-code mapping

| Research rule | cBot implementation |
|---|---|
| Instrument | Attach to the broker's XAUUSD symbol |
| Resolution | Requires an M1 chart |
| Session | `TimeZones.EasternStandardTime`, DST-aware New York time |
| Opening range | Exactly 15 M1 bars from 09:30 through 09:44 |
| Missing range bar | Session invalidated unless exactly 15 bars exist |
| Confirmation | First M1 close strictly above OR high or below OR low |
| Entry | Market order when the following M1 bar opens |
| Entry deadline | Final eligible confirmation is the 11:59 bar; entry at 12:00 |
| Maximum trades | First eligible signal consumes the session |
| Stop | Absolute opposite opening-range boundary |
| Target | Configurable R multiple; default 1R |
| Exit | Managed position closed at the first bar opening at or after 16:00 |
| Position sizing | Default 0.5% equity risk using broker symbol metadata |
| Safety | Real-time execution disabled by default |

## Important correlation difference

The Python baseline uses midpoint M1 bars plus an explicit cost model. The cBot uses the broker's executable bid/ask stream and actual market-order fill. Its 1R target is calculated from the **actual fill** to the opposite range boundary. Small trade-level differences are expected and are evidence about execution, not automatically a coding error.

Compare the same session dates on:

- direction and signal timestamp;
- OR high and low;
- confirmation close;
- next-bar entry/fill;
- exit reason;
- gross and net R after converting cTrader history to the research ledger.

## Safety behavior

`Allow Live Trading` defaults to `false`. A real-time instance stops immediately unless this is explicitly changed. This is a research backtest cBot; it is not evidence that the strategy is profitable.

## Build

The project references the official `cTrader.Automate` package.

```bash
dotnet build ctrader/XauUsdNy15OrbBaseline.csproj
```

Verified on 2026-10-25 against `cTrader.Automate` 1.0.21 targeting `net6.0`: build succeeded with zero warnings and zero errors. The build also generated an installable `.algo` artifact under `ctrader/bin/Release/net6.0/` (the build directory is intentionally ignored by Git).

Alternatively, create a cBot in cTrader Algo and replace its generated `.cs` content with `XauUsdNy15OrbBaseline.cs`.

## Required cTrader backtest settings

1. Symbol: the intended broker's spot-gold symbol, normally XAUUSD.
2. Chart timeframe: M1.
3. Data: **tick data from server** for the primary correlation run.
4. Spread: server/historical behavior where available; separately run fixed stress spreads.
5. Commission: the intended broker's real commission per million.
6. Date partitions: follow [[../02 - Methodology/Validation Protocol]].
7. Parameters: target 1R, 0.5% equity risk, live trading disabled.
8. Save the HTML report and parameter `.cbotset` with the experiment artifacts.

cTrader identifies server tick data as its most accurate backtest source. M1 server or CSV data can help debugging, but same-bar path results should not be treated as equivalent to tick replay.

## First correlation run

Use a short training-only interval first—approximately 20–30 sessions—and compare each generated `ORB_RANGE`, `ORB_ENTRY`, and `ORB_CLOSED` log event against the Python ledger. Do not expose the final-test period during implementation debugging.

## Verified six-year result

The supplied cTrader report confirms **-74.74% ROI** from May 6, 2020 through October 24, 2026, with **75.84% maximum balance drawdown**. Starting balance was $10,000; ending balance was $2,525.72. See the full result and yearly breakdown in [[../04 - Planning/Experiment Register#EXP-001 — cTrader NY15 baseline|EXP-001]].

The run used FTMO Platform XAUUSD, M1 bars from server (not tick replay), fixed spread setting 1, and commission set to $30 per million USD volume. It executed 1,653 trades: 711 wins and 942 losses, 43.01% win rate, 0.70 profit factor, and -$4.52 average trade. Gross trade P/L was -$5,598.60 before $1,701.76 commissions and $173.92 swaps, so the negative result persists before those two costs.

Two stop modifications failed with `InvalidStopLossTakeProfit` (July 4, 2023 and February 19, 2024); the cBot closed those positions immediately. This is a real execution-path defect to address, but two such events do not explain the overall performance. The report also exposes 2022–2026, so that period is no longer an untouched holdout for future tuning.

## Acceptance checks

- [x] cTrader build succeeds.
- [ ] Real-time instance stops when live trading is disabled.
- [ ] DST dates produce 09:30 New York ranges.
- [ ] Exactly 15 range bars are required.
- [ ] Wicks without a closing break produce no entry.
- [ ] Entry occurs on the bar after confirmation.
- [ ] Only one signal is consumed per session.
- [ ] Stop equals the opposite range boundary.
- [ ] Target is 1R from actual fill.
- [ ] Open positions close at 16:00.
- [ ] Training-period trades correlate with the Python ledger.

## Official API references

- https://help.ctrader.com/ctrader-algo/documentation/cbots/cbot-bar-events/
- https://help.ctrader.com/ctrader-algo/references/Utility/TimeZones/
- https://help.ctrader.com/ctrader-algo/references/MarketData/Symbols/Symbol/VolumeForFixedRisk/
- https://help.ctrader.com/ctrader-algo/cbot_testing/

