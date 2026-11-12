---
tags: [xauusd, intraday, hypothesis, preregistered]
status: development-rejected
---

# Asia rebound after a negative U.S. session — locked screen

**Question:** does spot gold rebound during Asian hours after a negative U.S. session strongly enough to cover executable spread, commission and slippage? [World Gold Council's H1 2026 session analysis](https://www.gold.org/goldhub/research/gold-mid-year-outlook-2026) reports U.S.-hour pullbacks and Asian rebounds in that particular half-year. The conditional trade below is **our inference**, not its reported strategy. The fixed 08:20–13:30 U.S. short tested in EXP-011 already failed and does not establish this hypothesis.

This is a **screen on an independent data feed**, not an FTMO execution result or an authorised live trade. It is logged as EXP-015 so a failure cannot disappear. No parameter search follows the first result.

## Frozen rule

- Data: EV Trading Labs yearly XAUUSD M5 bid and ask OHLC, attributed by that provider to aggregated Dukascopy ticks. Use only 2018–2025 closed calendar years. Each bar is timestamped at open in UTC. No volume filter.
- Convert all timestamps using `America/New_York`; do not use a fixed UTC offset.
- For New York date `d`, require all 92 consecutive M5 bars from 08:20 through 15:55 and all 72 bars from 20:00 through the next date's 01:55. Skip incomplete sessions. Do not fill bars.
- Signal: U.S. midpoint return from 08:20 **open** to 15:55 **close** is strictly negative. Otherwise no trade.
- Entry: buy one ounce at the 20:00 **ask open**, only on signal dates. This is after the daily 17:00–18:00 New York metals maintenance break.
- Stop distance: median of the completed 20:00–01:55 **bid high minus bid low** ranges from the previous 20 complete eligible Asian windows, whether or not they carried a signal. Current night is excluded. Require 20-window warmup. Stop = entry ask minus this distance. Skip if the stop is at or above the entry-time bid open.
- Exit: first M5 bid low at or below stop from 20:00 through 01:55 exits at stop, or at that bar's bid open if it gapped below stop. Otherwise exit at next-day 02:00 bid open. Stop first within a bar. One trade maximum per New York date.
- Net P/L per ounce = bid exit minus ask entry minus `30 / 1,000,000` times entry and exit notional (commission charged on each side) minus `$0.02` adverse slippage per side. No rollover financing is included because the position opens after the 17:00 New York rollover; this assumption must be checked on the actual account.
- Report 2018–2023 and 2024–2025 separately, plus each calendar year. The later split is a reserved confirmation screen for this exact rule; dates have been seen in other FTMO experiments, so do not call it a pristine project-wide holdout.
- Controls: unconditional long on every otherwise eligible Asian window with the same prior-20-window stop, and same-date short on the signal dates with a symmetric stop. Charge both controls the same executable costs. Compare the conditional and unconditional long on the same **date universe**, counting conditional no-trade dates as zero P/L. They are separate attempted variants; no selecting a winner.

## Decision gate

Reject if the conditioned long has nonpositive net expectancy or PF ≤1 in either 2018–2023 or 2024–2025, if its mean P/L per eligible date underperforms the unconditional long on the same date universe, or if the result is carried by one year. A surviving screen still needs FTMO XAUUSD tick/M1 replication, a quote-quality audit, mark-to-market intraday equity simulation against the exact prop account's drawdown rules, and forward paper trading. No positive screen is labelled a real edge before those checks.

**Result (2026-11-03):** the frozen 2018–2023 development screen failed with
-$0.6204 mean net P/L per ounce and PF 0.7152 across 588 conditional trades.
The 2024–2025 confirmation years remain uninspected for EXP-015. See
[[../outputs/EXP-015-asia-rebound-screen/RUN-NOTE|the run note]].
