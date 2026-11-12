---
tags: [xauusd, intraday, hypothesis, preregistered]
status: development-rejected
experiment: EXP-016
---

# Overnight gold drift after the daily reopening — locked screen

**Question:** does a regular weekday XAUUSD long held from shortly after the
New York daily reopening to the COMEX regular-session open retain positive
expectancy after executable costs? [Blose, Gondhalekar and Kort (2018)](https://doi.org/10.1007/s12197-017-9403-0)
reported positive overnight and negative daytime gold returns in older futures
and spot data. Their related [2014 study](https://doi.org/10.1080/13504851.2014.922661)
reported that the asymmetry weakened over time. This broker-hour adaptation is
our inference, not their tested trading rule. EXP-015's conditional 20:00–02:00
Asian long already failed, making this hypothesis lower prior, but the fixed
window and unconditional exposure differ materially.

This is an independent-feed research screen, not an FTMO execution result or
authorization to trade live. Register it before inspecting any EXP-016 P/L.

## Frozen rule

- Data: EV Trading Labs XAUUSD M5 bid/ask OHLC, attributed by the provider to
  aggregated Dukascopy ticks. Use 2018–2025 closed years; timestamps are UTC
  bar opens. Convert to `America/New_York` with historical DST.
- For each Monday–Thursday New York date, require every five-minute bar from
  18:05 through next-day 08:15 inclusive (171 bars) and the 08:20 exit bar.
  Friday, Saturday and Sunday entries are excluded to avoid weekend exposure.
  Skip incomplete windows; never interpolate bars.
- Buy one XAUUSD ounce at the 18:05 **ask open**. Set a protective stop at 99%
  of that ask entry. If the initial bid is already at/below the stop, skip.
  During the 171 held bars, exit at the first bid low at/below stop, filling at
  the worse of stop and that bar's bid open. Otherwise sell at the next-day
  08:20 **bid open**. No target, trailing stop, or same-day re-entry.
- Charge observed entry/exit spread through ask and bid fills, $30 per million
  USD of entry and exit notional per side, and $0.02 adverse slippage per side.
  Do not charge swap: entry is after the New York 17:00 rollover and exit is
  before the next one. Verify actual broker financing before promotion.
- Report raw midpoint movement, gross P/L after spread, and net P/L. Run a
  symmetric same-date short as an explicit direction control with a 1% price
  stop and the same costs. The controls are attempted variants and are not
  eligible for promotion merely because the primary long fails.
- Development: 2018–2023 only. Confirmation: 2024–2025, evaluated once only
  if development clears every gate below. These years were exposed for other
  FTMO experiments and are not a pristine project-wide holdout.

## Decision gate

Reject the primary long in development if net mean P/L is nonpositive, profit
factor is at/below 1, fewer than four of six years have positive net P/L, or
one year contributes over half of the positive aggregate net P/L. A surviving
screen still needs confirmation on 2024–2025, FTMO bid/ask tick replication,
spread/slippage stress, intraday equity-path simulation against the exact prop
account rules, and forward paper trading. Do not label it a real edge until
all those checks pass. Do not shift the clock times or stop after inspection.

**Result (2026-11-03):** the first and only frozen development run failed:
-$289.5016 net per ounce across 1,235 trades, PF 0.9296, and four of six years
negative. The 2024–2025 confirmation years remain uninspected for EXP-016.
See [[../outputs/EXP-016-overnight-gold-screen/RUN-NOTE|the run note]].
