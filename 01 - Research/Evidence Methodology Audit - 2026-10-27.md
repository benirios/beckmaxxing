---
tags: [xauusd, orb, literature, methodology-audit]
status: complete-for-accessible-sources
audited: 2026-10-27
---

# Evidence Methodology Audit — 2026-10-27

This audit checks what each cited source actually tested, what can be reproduced,
and which claims can inform the XAUUSD experiments. It does not treat a published
profit statistic as transferable evidence.

## Evidence classification

| Source | Class | Full method available? | Data available? | Direct relevance |
|---|---|---:|---:|---|
| RIP Backtest gold ORB | Independent backtest | Yes: specification, code, logs | No raw vendor data | High for hypothesis design; medium-low as independent evidence |
| Fetna futures grid | SSRN preprint | Abstract and partial full-text evidence retrieved; PDF access blocked in this session | Verification repository claimed, licensed market data not inspected | Medium for controls and failure modes; not spot XAUUSD evidence |
| Open Market Journal gold sessions | AI-assisted independent research | Partial; production rules explicitly private | No; aggregate data attributed to Cortiq | Low as strategy evidence; useful only as a hypothesis generator |
| Holmberg et al. | Peer-reviewed journal article / accessible working paper | Yes | No underlying CSI data | Low for this exact ORB; useful for regime and volatility reasoning |
| Zarattini et al. | Swiss Finance Institute working paper / SSRN preprint | Yes | No licensed IQFeed/CRSP data | Low for gold; useful for ex-ante activity-filter design |

## 1. RIP Backtest — gold 30-minute ORB

Sources:

- https://ripbacktest.com/posts/gold-opening-range-breakout/
- https://ripbacktest.com/posts/gold-opening-range-breakout/gold_orb_spec.md
- https://ripbacktest.com/posts/gold-opening-range-breakout/gold_orb_protocol.js
- https://ripbacktest.com/posts/gold-opening-range-breakout/clock_audit_compare.txt

### Verified method

- Instrument is broker spot XAUUSD from an MT4 `.hst` M1 file, not futures.
- Data source is identified as `ICMarketsSC-Demo06`, but the raw vendor history is not published.
- Sessions are 09:00 Tokyo, 08:00 London, and 09:30 New York with local DST rules.
- Opening range is 30 minutes. A day remains eligible with only 20 of 30 range bars, unlike this project's strict completeness rule.
- Entry is an immediate stop trigger one cent beyond the range, detected from an M1 high/low. It is not a completed-close confirmation and not a next-bar entry.
- Stop is one cent beyond the opposite range edge. The original versions have no target and exit seven hours after the session open.
- Cost is a fixed `$0.30/oz` round trip deducted from every trade; the data do not contain observed spread, ask, or slippage.
- RSI is Wilder RSI(14) on constructed M30 bars, using the last completed value.
- Six original variants were declared; six 1R-target variants were added after the first results and counted separately.
- Development is 2004–2012, validation 2013–2019, and 2020–2026 is explicitly marked as an already-used holdout.

### Reproducibility and defects

The strategy code, trade logs, gates, controls, and clock-audit outputs are public,
so arithmetic can be checked. Independent market-data reproduction cannot be done
without the unpublished `.hst` file.

The later clock audit found the broker timestamps one hour early in 366 of 1,197
weeks, concentrated in 2004–2010 and some DST-transition weeks. Correcting the
clock changed Asia-with-RSI from about `+21.0R` to `-100.4R`, and New-York-with-RSI
from about `-99.7R` to `+16.4R`; all variants still failed the source's overall gate.
This is strong evidence that session-clock validation must precede a time-of-day
strategy backtest.

### What it supports here

- Keep observed-cost and lower/higher-cost scenarios.
- Keep time-of-day/side-matched random controls, not merely unconstrained random entries.
- Audit timestamps against known scheduled events and provider charts.
- Do not import its immediate-stop entry results as evidence about this project's close-confirmed baseline.

## 2. Fetna — 225-cell futures study

Sources:

- https://papers.ssrn.com/sol3/papers.cfm?abstract_id=7428398
- https://doi.org/10.2139/ssrn.7428398

### Verified from accessible records

The September 2026 SSRN preprint reports a preregistered grid of 225 cells:
nine US futures markets, two session anchors, four range lengths, three exit
families, plus a volatility comparator, using M1 data from 2010–2026. It reports
zero cells meeting all positive criteria at `$25` round-trip costs and explicitly
distinguishes powered negative results from underpowered nulls.

The reported best residual effects concentrate in high-volatility regimes and a
random-anchor control weakens the case that the session open itself contains the
information. This supports adding a random-anchor control, not only a random-entry
control.

### Unresolved in this audit

The SSRN PDF endpoint was blocked during this session. Exact GC contract-roll rules,
bar source, fill sequencing, cost mapping by contract, and the public verification
repository were not independently inspected. Treat the abstract-level claims as a
preprint report, not a reproduced result.

## 3. Open Market Journal — London versus New York gold

Sources:

- https://openmarketjournal.com/research/gold-opening-range-breakout
- https://openmarketjournal.com/about

### Verified method disclosures

- The long sample contains 7,948 London and New York sessions over 16 years.
- The page says it measures the first hourly close beyond an opening range through session end.
- The separate recent London trade test uses 143 sessions, M5 candles, first break, opposite-edge stop, and a one-range target; costs are not included.
- The site labels the research AI-assisted and says some production rules remain private.
- Aggregate data are attributed to Cortiq; no raw data, code, dated ledger, or exact outcome thresholds are published.

### Evidentiary limit

The 16-year categories (`clean`, `fakeout`, `both sides`) are behavior labels, not
trade returns. Exact extension thresholds and private stop/target geometry prevent
independent reproduction. The study can motivate H3, but it cannot validate H3 or
justify parameter changes after EXP-002.

## 4. Holmberg, Lönnbark, and Lundström — crude oil

Sources:

- https://doi.org/10.1016/j.frl.2012.09.001
- https://www.econ.umu.se/ueslpnr/ues845.pdf

### Verified method

- Market is a rollover-adjusted US crude-oil futures series from Commodity Systems Inc, 1983-03-30 through 2011-01-26.
- The test uses daily open, high, low, and close—not intraday bars and not an opening-window high/low.
- Breakout thresholds are tail levels from a normal-return model, and the threshold parameters are estimated from the sample. The authors describe the test as ex post.
- A high/low crossing assumes a perfect fill at the threshold, zero bid/ask spread, and zero commission in the main results; positions close at the daily close.
- The paper later suggests `0.08%` round-trip costs but does not incorporate observed execution into the main table.
- Full-sample profitability is driven largely by the high-volatility 2001–2011 subperiod and is not stable across the three subperiods.

### What it supports here

This is not evidence for a conventional first-N-minutes gold ORB. Its useful lesson
is narrower: breakout-like directional exposure behaves as long volatility, and
full-sample significance can be regime concentration. It supports volatility-regime
reporting and chronological evaluation, not a profitable-XAUUSD prior.

## 5. Zarattini, Barbon, and Aziz — US equities

Sources:

- https://ssrn.com/abstract=4729284
- https://www.alexandria.unisg.ch/entities/publication/ab50f49b-4d5c-4aa8-aaa1-af7a4c131008

### Verified method

- Universe covers more than 7,000 US stocks from CRSP, including delisted stocks; intraday bars come from IQFeed and are unadjusted for splits/dividends.
- Sample is 2016-01-01 through 2023-12-31 with no reported chronological holdout or walk-forward evaluation.
- Base eligibility uses opening price above `$5`, prior-14-day average volume of at least one million shares, and prior-14-day ATR above `$0.50`.
- Trade direction is the first opening-range candle's body direction. Entry is a stop at its high/low, stop loss is 10% of daily ATR, and remaining positions exit at 16:00 ET.
- Position risk is capped at 1% with 4x maximum leverage.
- Reported friction is `$0.0035/share` commission. The method does not model bid/ask spread, market impact, borrow availability/fees, or slippage.
- The high-relative-volume strategy requires first-N-minute volume at least 100% of its prior-14-day mean and selects the top 20 stocks each day.

### Selection concern

The same 2016–2023 sample is used to show the relationship between relative-volume
buckets and PnL, introduce the `>=100%`/top-20 rule, compare 5/15/30/60-minute
ranges, and report performance. The paper says the parameters were not heavily
optimized, but it does not provide an untouched later period. The 1,637% result is
therefore a hypothesis-generating in-sample finding, not external validation of H1.

### What it supports here

Use an ex-ante activity measure based only on completed pre-entry information. Freeze
the threshold family before validation, include realistic spread/slippage, and require
a later chronological test. Do not import the paper's 5-minute preference into gold;
the authors themselves say its superiority is unexplained.

## Protocol changes implied by this audit

1. Add a random-anchor control to separate session information from generic volatility continuation.
2. Require event/chart-based timestamp audits before any session strategy is run.
3. Treat observed costs as time-varying data; retain cost grids only as sensitivity analysis.
4. Report volatility-regime concentration and contribution from the best trades.
5. For H1/H5, define filters from lagged data and freeze thresholds before validation.
6. Never treat an in-sample filter-discovery paper as out-of-sample confirmation.

## Remaining retrieval gaps

- Inspect the full Fetna preprint and its claimed verification repository.
- Locate a stable archival copy of the final published Holmberg article if any differences from the working paper matter.
- Ask Open Market Journal for exact category thresholds, session endpoints, and an exportable dated ledger if independent reproduction is pursued.
- Obtain any executable appendix or code for the Zarattini paper; licensed CRSP/IQFeed inputs would still be required.
