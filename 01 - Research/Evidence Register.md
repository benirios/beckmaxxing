---
tags: [xauusd, orb, literature, evidence]
status: living-document
---

# Evidence Register

Independent backtest articles are useful hypothesis generators, but they do not carry the same evidentiary weight as peer-reviewed research. Gold futures and spot XAUUSD are related, not interchangeable.

Detailed source-by-source verification: [[Evidence Methodology Audit - 2026-10-27]].

## 1. Gold Opening-Range Breakout: 22-Year Backtest

- Source: https://ripbacktest.com/posts/gold-opening-range-breakout/
- Published: October 2026.
- Market: spot XAUUSD.
- Period: 2004–2026, one-minute data.
- Design: Asian, London, and New York sessions; 30-minute opening ranges; 12 variants.
- Reported result: none of the 12 variants passed every robustness criterion.
- Reported net expectancy at `$0.30/oz` round-trip cost:

| Variant | Average net expectancy |
|---|---:|
| Asia ORB | -0.06R |
| Asia ORB + RSI | +0.01R |
| London ORB | +0.01R |
| London ORB + RSI | +0.03R |
| New York ORB | -0.02R |
| New York ORB + RSI | -0.02R |

At a lower reported cost of `$0.17/oz`, all six original variants were reportedly positive in aggregate, but none passed every validation criterion.

**Use here:** direct hypothesis input for session, cost, and RSI tests. Methodology and data provenance still require verification.

## 2. Opening Range Breakout Does Not Survive Trading Costs

- Source: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=7428398
- Author: Mulham Fetna.
- Published: September 2026, SSRN preprint.
- Market set: nine futures markets, including COMEX Gold.
- Period: 16 years of minute data.
- Design: 225 ORB configurations and multiple opening-range lengths.
- Reported result: zero configurations passed the predefined profitability and robustness rules at `$25` round-trip cost; five-minute ORB performed particularly poorly.

**Use here:** strong reason to cost-stress and treat 5-minute ORB only as a baseline. GC futures results do not directly establish XAUUSD spot behavior.

## 3. Gold Opening-Range Breakout: London vs New York

- Source: https://openmarketjournal.com/research/gold-opening-range-breakout
- Published: July–August 2026.
- Observations: 7,948 London and New York sessions over approximately 16 years.

| Price behavior | London | New York |
|---|---:|---:|
| Clean breakout | 16.8% | 22.7% |
| Re-enters range | 35.3% | 49.2% |
| Breaks both sides | 47.4% | 22.3% |

A separate London test reportedly used 143 sessions, achieved a 44% win rate at 1:1 R:R, and excluded costs.

**Use here:** motivates the London failed-breakout reversal hypothesis. These path categories are not trade win rates; exact definitions must be verified.

## 4. Assessing the Profitability of Intraday Opening Range Breakout Strategies

- DOI: https://doi.org/10.1016/j.frl.2012.09.001
- Authors: Holmberg, Lönnbark, and Lundström.
- Published: 2013, *Finance Research Letters*.
- Market: crude-oil futures, 1983–2011.
- Reported result: statistically significant aggregate profitability that was not robust across historical subperiods.

**Use here:** supports regime-aware evaluation, not a claim about gold.

## 5. A Profitable Day Trading Strategy for the U.S. Equity Market

- Source: https://ssrn.com/abstract=4729284
- Authors: Zarattini, Barbon, and Aziz.
- Working paper: 2024, revised 2025.
- Market: more than 7,000 US stocks, 2016–2023.
- Design: five-minute ORB filtered for unusually active stocks.
- Reported result: top-20 Stocks in Play portfolio above 1,600% cumulative net return and Sharpe ratio of 2.81.

**Use here:** motivates ex-ante activity and volatility filters. It does not imply transferability from equities to gold.

## 6. Hedging Demand and Market Intraday Momentum

- DOI: https://doi.org/10.1016/j.jfineco.2021.04.029
- Authors: Baltussen, Da, Lammers, and Martens.
- Published: 2021, *Journal of Financial Economics*.
- Markets: more than 60 futures across equities, bonds, commodities, and
  currencies, including COMEX Gold; December 1974–May 2020 overall.
- GC trading hours: 08:20–13:30 New York, selected from volume patterns.
- Design: use the sign of the return from the previous close through 13:00 to
  predict and trade the final 13:00–13:30 return.
- Gold result: the individual GC regression reports a positive, statistically
  significant rest-of-day coefficient and positive out-of-sample R². The
  table's coefficients and R² values are scaled by 100.
- Cost caveat: the paper's market-timing results do not include transaction
  costs, and the authors warn that frequent rebalancing may prevent practical
  exploitation.

**Use here:** motivates EXP-008 as a materially different intraday-momentum
replication. GC futures and spot XAUUSD are not interchangeable, so this is a
hypothesis source rather than validation of the cTrader strategy.

## 7. Overnight Gold Returns

- DOI: https://doi.org/10.1080/13504851.2014.922661
- Authors: Laurence E. Blose and Vijay Gondhalekar.
- Published: 2014, *Applied Economics Letters*.
- Market and period: front-month COMEX Gold futures, 1985–2012.
- Reported result: average overnight returns are significantly positive while
  average daytime returns are significantly negative. The asymmetry weakened
  over time but remained present in the authors' sample.

**Use here:** establishes the directional and session-level premise for
EXP-011. It does not establish profitability on spot XAUUSD, at this broker, or
after the present cost model.

## 8. Overnight versus Day Returns in Gold and Gold-Related Assets

- DOI: https://doi.org/10.1007/s12197-017-9403-0
- Authors: Laurence E. Blose, Vijay Gondhalekar, and Matthew Kort.
- Published: 2018, *Journal of Economics and Finance*.
- Markets: COMEX front futures, London spot fixes, gold equities, and gold ETFs.
- Reported result: positive overnight and negative day returns appear across
  the examined gold-related assets and in both rising and falling gold markets;
  the authors also assess economic importance with transaction costs.

**Use here:** strengthens the case for one locked, low-parameter XAUUSD transfer
test and for a matched long control. Cross-asset persistence remains motivation,
not proof of executable XAUUSD edge.

## 9. Intraday Return Predictability in Commodity ETFs

- DOI: https://doi.org/10.1016/j.resourpol.2020.101830
- Authors: Yahua Xu, Elie Bouri, Tareq Saeed, and Zhuzhu Wen.
- Published: 2020, *Resources Policy* 69, 101830.
- Data: one-minute high-frequency GLD data from Thomson Reuters DataScope
  Select, November 2004–May 2019; the source dataset includes bid/ask, trades,
  and volume.
- Session: 13 half-hour intervals over the 09:30–16:00 US equity session.
- Gold result: GLD's fifth half-hour (`r5`, 11:30–12:00) positively predicts
  the last half hour (`r13`, 15:30–16:00), with predictor t-statistic 3.03 and
  in-sample R-squared 0.49%.
- Out-of-sample result: positive GLD `r5` forecast R-squared of 0.26%.
- Mechanisms proposed: infrequent rebalancing and late-informed investors.

**Use here:** motivates EXP-012's exact predictor and holding intervals. The
reported effect is small and belongs to a listed ETF with closing flows; it is
not validation for decentralized spot XAUUSD. The first transfer test therefore
uses no volatility/jump filter and evaluates the configured broker costs
directly.

## 10. Commodity Futures Overreaction During COVID-19

- DOI: https://doi.org/10.1016/j.resourpol.2020.101922
- Authors: Q. F. Akram and O. E. C. (as indexed by publisher metadata).
- Published: 2020, *Resources Policy* 69, article 101922.
- Data: 20 commodity futures, Nov 2019–Jun 2020; intraday frequencies from
  one-minute to hourly.
- Finding: large intraday moves are sometimes followed by proportional price
  reversals; the event count and amplitude increased during the COVID period.
- Limitation: precious metals show materially fewer overreactions than energy;
  the sample is short and exceptional.

**Use here:** motivates the single fixed EXP-013 extreme-move fade. Our 2x
prior-ATR trigger is an operationalized test rule, not claimed to be the paper's
original detector, and must not be tuned after observing results.

## Evidence gaps to close

- [ ] Obtain every full methodology and confirm session definitions. Two full papers and the executable RIP Backtest method have been inspected; the Fetna PDF and Open Market Journal private rules remain unavailable.
- [ ] Verify raw-data provenance and treatment of missing bars. RIP Backtest identifies an unpublished broker `.hst`; Open Market Journal provides no raw ledger; the two academic papers use licensed data.
- [ ] Confirm whether prices were bid, ask, midpoint, or trade prices. RIP Backtest's MT4 file contains one OHLC stream and no executable bid/ask pair; other sources remain provider-specific.
- [ ] Reconstruct all cost assumptions on a per-ounce basis. RIP Backtest is mapped directly; futures and equities require contract/share-specific comparison rather than false per-ounce equivalence.
- [x] Confirm whether parameter selection used future information. See the audit: Holmberg estimates thresholds from the full sample; Zarattini develops and reports the relative-volume filter on the same sample; RIP Backtest preregisters six originals and labels six later variants.
- [x] Separate peer-reviewed evidence, preprints, and independent research in final reporting.

## Related notes

- [[Research Brief]]
- [[Research Hypotheses]]
- [[Evidence Methodology Audit - 2026-10-27]]
- [[../02 - Methodology/Validation Protocol|Validation protocol]]

