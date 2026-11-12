---
tags: [xauusd, intraday, thesis-scan, strategy-ideas]
status: hypotheses-and-follow-up-results
---

# Intraday XAUUSD thesis scan — 2026-11-03

## Scope and finding

Public primary research supports two distinct **mechanisms** worth testing: scheduled macroeconomic news changes gold's intraday volatility and price discovery, and COMEX gold futures often incorporate information before London spot gold. Neither finding establishes an executable edge on this broker's XAUUSD feed. This scan records candidate tests and later results; it contains no live signal.

The repository has already rejected the tested NY5/NY15/NY30 ORBs, a NY30 width filter, a London failed-breakout reversal, a fixed COMEX daytime short, a COMEX-close momentum transfer, a GLD close-momentum transfer, and a fixed extreme-move fade. Do not repackage those exposed rules as new strategies. See [[Research Hypotheses]] and [[../04 - Planning/Experiment Register]].

## Source extraction

| Primary source | Direct finding | What it can justify | What it cannot justify |
|---|---|---|---|
| [Cai, Cheung & Wong, *Journal of Futures Markets* (2001)](https://onlinelibrary.wiley.com/doi/10.1002/1096-9934%28200103%2921%3A3%3C257%3A%3AAID-FUT4%3E3.0.CO%3B2-W) | COMEX gold futures show strong intraday volatility patterns; employment, GDP, CPI and personal-income releases had the largest effects among 23 U.S. announcements studied. | Study event windows separately from ordinary sessions. | A directional gold trade or a post-release drift at current costs. |
| [Sobti, Sehgal & Ilango, *International Review of Financial Analysis* (2021)](https://www.sciencedirect.com/science/article/pii/S1057521921002209) | One-minute 2013–2018 New York, London and Shanghai data show U.S. futures leading global gold price discovery; large macro surprises shift price discovery. | Test a synchronized futures-to-spot signal and event conditioning. | A profitable lead-lag trade after latency and spread. |
| [Hauptfleisch, Putniņš & Lucey, *Journal of Futures Markets* manuscript (2015/2016)](https://www.research.ed.ac.uk/files/82358957/GoldILS_JFutMkt_Forthcoming.pdf) | Over 1997–2014, New York futures generally contributed more to gold price discovery than London spot. The paper also distinguishes fast information incorporation from quote noise; employment news raised futures quote noise. | Require quote-quality and latency controls in any lead-lag test. | A stable or exploitable spot delay today. |
| [Baltussen et al., *Journal of Financial Economics* (2021)](https://academicweb.nd.edu/~zda/intramom.pdf) | Across futures markets, the return from the regular-session open through the penultimate half-hour predicts the final half-hour; the sample includes COMEX GC at 08:20–13:30 Eastern. The paper does not account for transaction costs. | Motivate a direct after-cost transfer test on XAUUSD. | A profitable retail CFD trade: the corresponding COMEX-close momentum transfer was already tested and rejected here as EXP-008. |
| [Blose, Gondhalekar & Kort, *Journal of Economics and Finance* (2018)](https://doi.org/10.1007/s12197-017-9403-0) | Historical gold futures, spot-fix and related assets had stronger overnight than daytime returns; a [related earlier paper](https://doi.org/10.1080/13504851.2014.922661) said the asymmetry had weakened. | Preregister a broker-hour overnight transfer test, EXP-016. | A current prop-firm edge: the 2018–2023 independent-feed test lost after the specified stop and costs. |
| [World Gold Council, mid-year outlook (2026)](https://www.gold.org/goldhub/research/gold-mid-year-outlook-2026) | Its attribution framework identifies risk, opportunity cost, FX and momentum as gold drivers. Its H1 2026 session chart reports Asian rebounds and U.S.-hour pullbacks. | Treat macro and session regime as candidate conditioning variables. | Extrapolation of a half-year pattern into a standing intraday short; this repo's fixed daytime short already failed. |
| [BLS dissemination policy](https://www.bls.gov/about-bls/dissemination.htm) and [Federal Reserve timing notice](https://www.federalreserve.gov/newsevents/pressreleases/monetary20130313a.htm) | Major BLS releases are scheduled for 08:30 Eastern; regular FOMC statements have been scheduled for 14:00 Eastern since 2013, with press conferences around 14:30. | Build event calendars using actual publication timestamps and `America/New_York`. | Treat scheduled timestamps as proof that an announcement was released on time. |

## Candidate A — post-release gold continuation, event conditioned

**Thesis:** a subset of CPI and Employment Situation releases may produce a persistent repricing after the initial spread/volatility burst. This is an inference from announcement effects, not a reported trading result. For FTMO, first obtain an account-specific ruling that this is not prohibited news gap trading; the published rule prohibits gap-trading practices around scheduled major news. The proposed 08:35 entry is outside the standard funded account's two-minute restricted execution window, but that fact alone does not establish permission under the broader forbidden-practices rule.

**Proposed first test, to freeze before running:**

1. Use only actual CPI and Employment Situation release dates and timestamps. Skip delayed or off-schedule releases, market closures, and days with another overlapping major release.
2. On XAUUSD bid/ask ticks, observe the 08:30:00–08:34:59 Eastern midpoint return relative to the 08:29:59 midpoint. A zero return gives no trade.
3. At the first executable quote at or after 08:35:00, trade in the sign of that return only when the absolute move exceeds the **previous 60 eligible release days'** median absolute five-minute move. The first 60 events are warmup. The current event cannot enter its own threshold.
4. Exit at the first executable quote at or after 09:00:00; apply a protective stop at the 08:30–08:34:59 opposite extreme. Skip if the stop is already crossed before entry. One trade per event.
5. Benchmark against same-date opposite direction and deterministic random direction, both entered and exited at identical times; also report the unfiltered event-direction variant as a diagnostic, counting it as another tried variant.

**Failure gate:** reject unless net expectancy and profit factor exceed zero and one respectively after observed spread, commission and adverse slippage; require the gain to survive the matched controls and later untouched event dates. Report separately for CPI and employment; do not select the better group after looking at results.

**Data needed:** historical release timestamps including revisions/delays, synchronized XAUUSD bid/ask ticks, actual broker fees and order latency. The official 2018–2023 [BLS scheduled-event CSV](../data/reference/bls_cpi_empsit_2018_2023.csv) now lists 144 CPI/Employment Situation calendar rows, with [[../data/reference/BLS event calendar provenance|provenance and availability checks]]. It is a schedule, not an audit of actual publication or historical executable quotes. The repo currently has no audited full tick history.

## Candidate B — GC-to-XAUUSD lead-lag convergence

**Thesis:** because COMEX gold futures contribute heavily to price discovery, a futures quote move may occasionally precede a spot quote. The historical paper does not demonstrate a delay long enough to trade. **Do not pursue this as an FTMO strategy:** FTMO explicitly forbids strategies exploiting delayed price updates or an external/slow data feed. Retain it only as a cross-market research question for a venue that expressly permits it.

**Proposed first test, to freeze before running:**

1. Acquire timestamped GC best bid/ask and broker XAUUSD best bid/ask with clock-synchronization error measured and logged. Use explicit GC contract IDs and roll dates; exclude roll transitions, halts and stale quotes.
2. At each second, compare the latest midpoints' **five-second changes**, not their absolute levels (which differ by carry and basis). A GC change that exceeds its previous 20 complete trading days' 95th percentile is a candidate event. All rolling values are shifted at least one complete session.
3. Enter XAUUSD in the GC move direction only if the spot five-second change is less than half the GC move, the current XAUUSD spread is below its previous-20-day same-minute median, and the expected catch-up distance exceeds twice estimated all-in round-trip cost. Use the next available executable quote after the decision timestamp.
4. Exit after five minutes, at measured convergence, or at a protective loss equal to the initial expected catch-up distance, whichever happens first. One open position at a time; no signal re-arm for ten minutes.
5. Compare with a time-matched sign-randomized GC trigger and a delayed-quote placebo. Include an explicit latency sweep; if plausible latency erases the edge, reject.

**Failure gate:** positive net expectancy after actual bid/ask, commissions and latency-adjusted slippage, with a stable out-of-sample result. Also require enough post-cost opportunity to cover broker execution variance. This is a higher-data-cost, lower-priority candidate.

**Data needed:** licensed historical GC quote data, broker XAUUSD ticks, synchronized clocks, GC roll schedule, quote quality and measured execution latency. One-minute bars cannot validate a seconds-scale lead-lag claim.

## Candidate C — news-window risk gate for existing strategies

**Thesis:** if event-window spreads and volatility make existing entries less executable, a **calendar-only exclusion** might improve their net result. This is a risk-control hypothesis, not a new directional edge. Employment news also increased quote noise in the price-discovery paper.

**Proposed first test:** compare each existing strategy's original, frozen rules with the same rules skipping any entry from 08:25 through 08:45 Eastern on actual CPI/Employment Situation release dates. Count skipped trades and compare results on the same eligible dates, including costs. Do not adjust the window after seeing results. A filter that merely reduces loss by reducing exposure without positive net expectancy fails the project's promotion gate.

## Priority and validation

1. **EXP-014 completed and rejected:** the locked London RSI ORB lost $1,464.89 (-14.65%) across 1,185 FTMO Platform XAUUSD trades, with PF 0.77. Every represented year lost net; the cTrader log and report passed the execution audit. See [[../outputs/EXP-014-london-rsi-orb-2026-11-03/RUN-NOTE|run note]].
2. **Candidate A** is conditional on both audited event-date ticks and an account-specific ruling that the proposed post-news entry is permitted under FTMO's broader gap-trading policy. Until then, it is research only and is not a prop-firm trading plan.
3. **Candidate C** is a low-cost diagnostic once exact event timestamps and the old trade ledger are available; it cannot rescue a rejected strategy by itself.
4. **Candidate B** is excluded from the FTMO prop-firm path by its published forbidden-practices rule. It would require a different venue, synchronized cross-market quotes and a latency audit. Do not approximate it with M1 OHLC or claim the academic price-discovery result is a retail trading edge.

## Prop-firm rule check (FTMO, checked 2026-11-03)

- [FTMO 2-Step objectives](https://ftmo.com/en/2-step-challenge/) list a 10% first-phase target, 5% verification target, 5% maximum daily loss and 10% maximum loss. Intraday **equity**, including floating P/L, matters under the [detailed objectives](https://ftmo.com/en/trading-objectives/); a closed-trade-only backtest cannot establish rule survival.
- The [FTMO news FAQ](https://ftmo.com/en/faq/can-i-trade-news/) says standard funded accounts restrict XAUUSD order execution from two minutes before to two minutes after selected USD releases, including stop and target executions. Challenge and Verification accounts and Swing accounts are excepted from that specific restriction. Rules must be checked again for the actual account type before trading.
- The [forbidden-practices policy](https://ftmo.com/en/forbidden-trading-practices/) separately bars exploitation of delayed price updates or external/slow feeds and news gap trading. These restrictions override attractive backtest statistics.

All 2020–2026 FTMO dates previously inspected in this repository are **exposed**. Further tests on that interval are comparative development, not untouched validation. Freeze definitions and retain future dates for the final test. Follow [[../02 - Methodology/Validation Protocol]]; record every attempted variant in [[../04 - Planning/Experiment Register]] before running it.

**Current conclusion:** this scan has not found a verified, prop-firm-compatible XAUUSD edge. Promotion requires a positive after-cost broker-feed test, untouched later dates, and an intraday equity-path simulation of the exact firm's daily and total loss limits. Closed-trade summary ROI alone cannot establish prop-firm survival.

**Subsequent screen:** the distinct Asia-rebound candidate [[Asia Rebound Screen - preregistered 2026-11-03|EXP-015]] was preregistered and tested on independent M5 bid/ask history. Its 2018–2023 development result was negative after observed spread even before modeled commission/slippage, so it was rejected without evaluating 2024–2025. See [[../outputs/EXP-015-asia-rebound-screen/RUN-NOTE|run note]]. These two rejections leave no verified edge or trading signal.

**Overnight follow-up:** the older published overnight-positive gold finding
motivated a separately locked 18:05–08:20 New York weekday long
[[Overnight Gold Drift - preregistered 2026-11-03|EXP-016]]. It lost $289.5016
per ounce over 1,235 2018–2023 development trades after observed spread and
modeled costs (PF 0.9296). The 2024–2025 confirmation years remain uninspected
for this rule. See [[../outputs/EXP-016-overnight-gold-screen/RUN-NOTE|run note]].
