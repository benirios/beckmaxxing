---
tags: [xauusd, intraday, hypothesis, london, gold-auction]
status: untested-preregistered-design
---

# London PM gold auction opening — intraday hypothesis

## Evidence and inference

The [ICE benchmark page](https://www.ice.com/iba/lbma-precious-metals) says the LBMA Gold Price PM auction is expected to start at **15:00 Europe/London** on eligible London business days; its final price has no fixed publication time. [Caminschi and Heaney (2014), *Journal of Futures Markets*](https://research-repository.uwa.edu.au/files/4725264/A0205_use_this.pdf) found elevated GC and GLD volume and volatility immediately after the old PM fixing began, with price movement concentrated in the first four minutes. Their directional return calculation conditions on the *eventual fixing direction*, which a trader does not know at entry. The paper does not establish that the first observable minute predicts later minutes, or that a retail XAUUSD order can capture them.

The benchmark moved to an [independently administered electronic auction on 20 March 2015](https://www.lbma.org.uk/articles/the-new-lbma-gold-price-successfully-launched-on-20th-march-2015). The old fixing's information leakage may have disappeared or changed. This is the main reason for a low prior, even though the auction clock remains relevant.

**Hypothesis:** on current auction days, an unusually large XAUUSD midpoint move in the first auction minute sometimes continues over the next three minutes because auction-related price discovery is still underway. This is a new inference, not a source finding. It uses only the same broker's XAUUSD quotes, not an external faster feed or the unpublished benchmark result. It is distinct from the rejected NY ORBs, fixed session short, and 30-minute extreme-move fade in [[../04 - Planning/Experiment Register]].

## Locked first-test design (no results inspected)

1. Eligible dates: ICE PM auction days only, with the expected start at 15:00 `Europe/London`. Exclude actual cancellations, late starts if verifiable, broker closures, and days with a scheduled high-impact USD release from 14:55 to 15:10 London time. Record every exclusion before examining returns. Use `Europe/London` and `America/New_York` timezone databases, never a fixed UTC offset.
2. Require continuous, time-stamped **broker bid and ask ticks** from 14:59:59 to 15:05:00 London time and a non-stale quote at each decision point. Define the first-minute move as midpoint at the last quote at or before 15:00:00 to midpoint at the last quote at or before 15:01:00. If either quote is older than two seconds, skip. A zero move is no trade.
3. Require its absolute size to exceed the 75th percentile of absolute first-minute moves from the **previous 60 eligible auction days**, with no current or future day in the threshold. Require the decision-time spread to be no greater than the median 15:01 spread over those same 60 days. First 60 days are warmup.
4. At the first executable quote **after** 15:01:00, enter in the first-minute move's direction at the broker ask for a long or bid for a short. Exit at the first executable quote at or after 15:04:00, using the opposite side. No same-bar fills. One attempt per day. A catastrophic protective stop equal to twice the first-minute absolute move is required in a live implementation; in replay, simulate its first tick touch and adverse slippage. If that distance violates broker minimum-stop rules, skip.
5. Primary estimand: mean net dollars per ounce over **all eligible signal days**, with skipped days reported separately. Charge observed spread, actual commission, and a predeclared adverse slippage allowance; show results at zero, one, and two additional ticks per side. Compare with same-time opposite-direction and deterministic random-direction controls on exactly the same dates. Also report unconditional first-minute continuation as a separate, counted variant; do not choose it after seeing results.
6. Development window: 2018–2023 independent broker quotes, if obtainable. Freeze code, cost assumptions, exclusions, and controls before looking at 2024–2025 on that feed. Prior FTMO 2020–2026 strategy results are exposed and cannot be called a new holdout. Any final claim requires untouched later broker-feed dates and intraday equity-path prop-firm rule simulation.

**Reject** if net expectancy is not positive after the specified friction, if it does not beat both matched controls, if a plausible two-tick-per-side slippage erases the effect, or if later untouched dates fail. Report year, long/short, spread, trade count, and bootstrap uncertainty by day; do not optimize the percentile or exit minute after inspection. Register each executed variation in [[../04 - Planning/Experiment Register]].

**Data gap:** the available independent XAUUSD files are M5 bid/ask bars. They cannot identify the 15:01 entry, auction-minute spread, or stop order sequence. This hypothesis has **no backtest result and no trading signal** until suitable ticks and costs are available. Historical auction start exceptions and broker clock quality also need verification. The benchmark's final price must not be used as a feature unless its actual publication timestamp and permitted availability are established.

Related: [[Intraday Thesis Scan - 2026-11-03]] · [[Intraday Session Microstructure Hypotheses]] · [[../02 - Methodology/Validation Protocol]]
