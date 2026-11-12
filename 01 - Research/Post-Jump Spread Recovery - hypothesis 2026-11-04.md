---
tags: [xauusd, intraday, hypothesis, execution, liquidity]
status: untested-research-design
catalog: T017
---

# Post-jump spread recovery — T017

## Evidence and limit

A [gold futures and GLD jump study](https://www.efmaefm.org/0EFMAMEETINGS/EFMA%20ANNUAL%20MEETINGS/2024-Lisbon/papers/EFMA_COMPLETEMANU_NSOBTI.pdf) reports elevated effective spreads around five-minute price jumps, with spread normalization several minutes later; its event study also finds other liquidity measures can take longer to recover. This is an exchange-market observation. It does not establish the same pattern on a retail XAUUSD broker, a reliable jump forecast, or a profitable direction. T017 asks whether **a measured broker spread recovery after an observed jump** improves execution of a separately frozen signal. Related: [[Thesis Catalog#T017 — Post-jump spread recovery before XAUUSD entry]].

## First-test definition to freeze before inspection

1. Work from time-stamped bid/ask ticks on the intended XAUUSD broker. Build non-overlapping five-minute midpoint bars in `America/New_York`. A jump event is a completed five-minute absolute midpoint return above the 99th percentile of same-clock-bin absolute returns from the preceding 60 complete trading days. If fewer than 60 valid days exist, warm up without events. The threshold is shifted and the current bar never enters its own reference distribution.
2. Exclude scheduled CPI, Employment Situation, Federal Funds Rate/Statement, and FOMC Minutes windows from five minutes before through 15 minutes after actual release, plus broker reopen and rollover intervals. Keep an exclusion ledger. This isolates routine liquidity shocks and avoids implying that a news trade is permitted.
3. At the first quote after a jump bar closes, label spread as wide if ask minus bid exceeds the median of each prior day's **same-local-minute median spread** over the preceding 60 valid trading days. This gives each day one observation and uses only information known at that quote. A frozen external signal must already have requested an entry at that time; T017 supplies **no direction**.
4. Baseline enters at the first executable quote after the signal. Gate waits until the first quote whose spread is at or below that prior-history median, capped at ten minutes. If none appears, it skips. For paired comparison, both branches share the signal direction, risk budget, original exit clock, and protective stop logic. The gated branch's stop distance and position size are recalculated at its actual entry, then charged its actual spread, commission, and adverse slippage. Do not extend its exit to make up time.
5. Primary diagnostic: paired difference in net P/L per original signal opportunity, counting gate skips as zero and baseline outcomes at the original time. Secondary: entry spread, time waited, price movement during wait, missed favorable moves, adverse excursion, and number of valid opportunities. Compare with a fixed five-minute delay placebo and a same-clock random-event control. Report annual and spread-regime slices without selecting the best ex post.

**Reject as a useful gate** unless the paired net difference is positive after all friction, enough opportunities remain, and the full frozen strategy has positive net expectancy and survives the intended firm's intraday equity limits. A lower spread alone is insufficient. This is a risk/execution hypothesis, not a directional trading signal. The available independent M5 bid/ask bars cannot reconstruct quote-by-quote spread recovery or stop order; no result is claimed here. See [[../02 - Methodology/Validation Protocol]] and [[../04 - Planning/Experiment Register]].
