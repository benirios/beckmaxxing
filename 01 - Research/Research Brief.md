---
tags: [xauusd, orb, research-brief]
status: active
---

# Research Brief

## Research question

Does an Opening Range Breakout strategy on spot XAUUSD possess a persistent edge after realistic spread, commission, and slippage assumptions?

The goal is not to maximize an attractive historical equity curve. The goal is to determine which, if any, narrowly specified ORB behaviors survive costs, nearby parameter changes, different market regimes, and untouched future data.

## Scope

- Instrument: spot XAUUSD.
- Initial resolution: one-minute OHLC; bid/ask ticks preferred.
- Initial session anchor: 09:30 `America/New_York`.
- Additional anchors: 08:20 New York, London, and Asian session opens.
- Opening ranges: 5, 15, 30, and 60 minutes.
- Entry families: immediate breakout, close confirmation, and breakout/retest.
- Exit families: fixed R, opposite boundary stop, and time-based exit.
- Future deployment target: cTrader cBot in C#, only if research evidence supports it.

## Provisional conclusions

1. There is not yet sufficient evidence that an unfiltered XAUUSD ORB is consistently profitable after costs.
2. Session definitions materially change the observed breakout behavior.
3. Five-minute ORB should be a benchmark, not a presumed optimum.
4. Spread and slippage can erase small gross advantages.
5. Activity, volatility, and range-width filters deserve controlled testing.
6. Gold needs explicit session definitions rather than copied equity-market rules.
7. Historical profitability without out-of-sample persistence is insufficient.

## Success criteria

A candidate is interesting only if it demonstrates:

- positive net expectancy after defensible costs;
- sufficient trade count and stability across years;
- tolerable maximum drawdown and loss streaks;
- performance across nearby parameters rather than at one isolated optimum;
- positive walk-forward and untouched final-test evidence;
- acceptable results under higher-cost stress scenarios;
- evidence beyond a time-of-day-matched random-entry baseline;
- replication on the intended broker's data feed.

## Failure criteria

Reject or downgrade a candidate if profitability depends on:

- one short subperiod or a small number of outlier trades;
- optimistic same-bar fill assumptions;
- fixed UTC offsets across DST changes;
- missing-data interpolation;
- selecting filters with knowledge of the final test;
- transaction costs below observed executable conditions;
- one exact parameter combination surrounded by losses.

## Related notes

- [[Evidence Register]]
- [[Research Hypotheses]]
- [[../02 - Methodology/Validation Protocol|Validation protocol]]
- [[../00 - XAUUSD ORB Research Hub|Project hub]]

