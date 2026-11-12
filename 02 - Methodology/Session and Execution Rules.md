---
tags: [xauusd, sessions, execution]
status: baseline-locked
---

# Session and Execution Rules

## Gold-specific session problem

Spot XAUUSD trades nearly continuously during the trading week and has no universal opening bell. The session anchor is therefore a research variable, not a background implementation detail.

Candidate anchors:

- Asian session;
- London session;
- 08:20 `America/New_York`, associated with COMEX gold conventions;
- 09:30 `America/New_York`, the US equity open.

Each experiment states its IANA timezone and local anchor. A fixed UTC offset is prohibited because London and New York change daylight-saving time on different dates.

## Locked initial baseline

- Timezone: `America/New_York`.
- Opening range: 09:30 through 09:44 M1 bars.
- Range complete: 09:45.
- Confirmation: first M1 close strictly outside the range.
- Entry: next M1 bar open.
- Entry deadline: 12:00.
- Maximum trades: one per session.
- Stop: opposite opening-range boundary.
- Target: 1R measured from raw midpoint entry to stop.
- Time exit: close of the 15:59 bar at 16:00.

## Why next-bar entry

A confirmation bar's close is known only after that bar completes. Filling at that same close without tick-level evidence is optimistic. The next M1 open is deterministic and avoids using an already-observed closing price as if it were executable in advance.

## Intrabar ambiguity

When an M1 bar reaches both stop and target and tick order is unavailable:

- assume the stop occurred first;
- label the exit `ambiguous_stop_first`;
- rerun with tick replay when ticks exist.

## Gap fills

- Stop-market gap: fill at the next bar open when it is worse than the stop.
- Profit-target gap: credit only the target price, not a favorable improvement.

## Missing bars

The baseline requires every M1 bar from 09:30 through 15:59. A missing bar skips and records the session. No forward fill, interpolation, or synthetic candle is permitted.

## Transaction-cost model

The baseline uses midpoint OHLC and applies adverse friction to both fills:

```text
net PnL/oz = gross directional move
             - round-trip spread
             - 2 × per-side slippage
             - round-trip commission
```

Initial scenario:

- round-trip spread: `$0.30/oz`;
- slippage: `$0.02/oz` per side;
- commission: `$0.00/oz` pending broker-specific evidence.

These values are transparent scenarios, not claims about actual fills.

## Required sensitivity tests

- observed bid/ask execution;
- spread and slippage by session and volatility regime;
- zero, baseline, stressed, and severe-cost cases;
- tick replay for all ambiguous trades;
- long and short results separately.

