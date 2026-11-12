---
tags: [xauusd, intraday, hypothesis, fomc, macro]
status: untested-research-design
catalog: T016
---

# FOMC easing-surprise continuation — T016

## Mechanism and source limit

[Awartani, Hussain and Virk (2024)](https://ideas.repec.org/a/eee/finana/v95y2024ipbs1057521924004186.html) report that gold futures' short-horizon return and volatility responses were stronger after unexpectedly looser than unexpectedly tighter FOMC announcements; adjustment extended beyond five minutes. This is a futures response finding, **not** a net XAUUSD strategy result. A 14:05 long may already miss the response, and policy statements can convey information beyond the target-rate decision.

The [Federal Reserve's timing notice](https://www.federalreserve.gov/newsevents/pressreleases/monetary20130313a.htm) sets regularly scheduled statements at 14:00 `America/New_York` and press conferences near 14:30. Historical event-specific releases must still be checked. [FTMO's current news FAQ](https://ftmo.com/en/faq/can-i-trade-news/) includes XAUUSD in the Federal Funds Rate & Statement restriction and bars executions within two minutes either side on affected funded accounts, with stated exceptions. Its [forbidden-practices policy](https://ftmo.com/en/forbidden-trading-practices/) separately addresses news-gap trading. The proposed timing alone does not establish that the trade is permitted; obtain an account-specific ruling before any FTMO use.

## Falsifiable first test to freeze before results

1. Use scheduled FOMC statement dates with confirmed **actual** release time. Exclude unscheduled decisions, statement delays, missing quote intervals, and events with another simultaneous major release. Group press-conference days separately; never pool an overlapping 14:30 window into the primary 14:05–14:10 test.
2. Define a policy easing surprise before viewing XAUUSD outcomes. The preferred measure is the change in a suitably matched Fed funds futures-implied policy rate from the last quote before 14:00 to the first quote at or after 14:01:00, with contract, expiry, and month-averaging rules documented. An easing surprise is a **negative** implied-rate change exceeding a predeclared quote-noise floor. If this feed is unavailable, do not substitute the XAUUSD move and retain the study as data-blocked.
3. Enter long at the first executable broker XAUUSD ask after 14:05:00 New York time. Exit at the first executable bid at or after 14:10:00. Use actual bid/ask ticks; reject stale quotes older than two seconds. One trade per event. Apply a catastrophic stop fixed before entry from the previous 20 complete non-event trading days' median five-minute absolute move, with an explicit minimum-stop check and stop-first replay.
4. Primary outcome: net dollars per ounce across **all qualifying easing-surprise events**, including observed spread, commission and adverse slippage. Compare to: (a) same-time short, (b) deterministic random direction, and (c) a predeclared symmetric tightening-surprise short on its own event set. The tightening comparison is an asymmetry test, not a same-date control.
5. Reject if the long fails positive net expectancy and PF above 1, fails the same-date controls, or loses at a plausible two-tick-per-side added slippage. Report event count, spread, each event's P/L, both directions, confidence intervals by event, and the effect of news-rule exclusions. Do not tune the 14:05 or 14:10 clock after seeing results.

## Data and validation status

No audited intraday policy-futures expectation series or full broker XAUUSD tick history is present in this vault. The independent M5 XAUUSD bars can show rough outcomes but cannot establish the first executable 14:05 fill, stop sequence, or contemporaneous policy surprise. The older FTMO 2020–2026 period has been exposed by other strategy tests; it cannot be called an untouched final test. Freeze this design and use genuinely later untouched dates for any promotion claim. See [[Thesis Catalog#T016 — Asymmetric FOMC easing-surprise continuation]], [[Intraday Thesis Scan - 2026-11-03]], and [[../02 - Methodology/Validation Protocol]].

**Current conclusion:** an untested research hypothesis, with no trade signal and no permission determination for an FTMO account.
