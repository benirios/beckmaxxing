---
tags: [decisions, xauusd, orb]
status: active
---

# Decision Log

## 2026-10-25 — Repository and Obsidian vault share one root

**Decision:** use `/Users/beni/Dev/XAUUSD ORB RESEARCH` as both the Python repository and Obsidian vault.

**Reason:** research notes, configurations, source code, tests, and outputs remain linked and versionable in one place.

## 2026-10-25 — Dukascopy for discovery, cTrader for replication

**Decision:** use Dukascopy bid/ask ticks for the discovery dataset and require target-broker cTrader replication.

**Reason:** Dukascopy supports executable quote modeling, while broker replication addresses decentralized spot-feed dependence.

## 2026-10-25 — Start with a conservative close-confirmed baseline

**Decision:** use a 15-minute 09:30 New York range, confirm on an M1 close, and enter at the next M1 open.

**Reason:** this avoids pretending the already-observed confirmation close was available in advance.

## 2026-10-25 — Resolve unknown M1 paths stop-first

**Decision:** if stop and target are both touched within one M1 bar, record a stop unless tick ordering is available.

**Reason:** deterministic, conservative treatment prevents optimistic hidden assumptions.

## 2026-10-25 — Reject incomplete sessions

**Decision:** skip a session with any required M1 bar missing.

**Reason:** prices are never invented or forward-filled.

## 2026-10-25 — Lock the final test operationally

**Decision:** ordinary CLI runs exclude post-2021 data; an explicit flag releases the holdout.

**Reason:** a stated holdout is not meaningful if routine development continuously reveals it.

## 2026-10-27 — Require explicit XAUUSD scale evidence

**Decision:** the Dukascopy decoder has no default commodity scale. Every ingestion run must supply an explicit scale and an independently observed Dukascopy reference price; a mismatch fails the run.

**Reason:** Dukascopy's generic documentation states that commodity point values vary and does not provide a verified XAUUSD default. A plausible-looking but wrongly scaled dataset is more dangerous than a loud ingestion failure.

## 2026-10-27 — Do not bypass authenticated Dukascopy access

**Decision:** record the requester-pays S3 migration as an acquisition constraint and wait for authorized authenticated access rather than substituting an unverified mirror.

**Reason:** provenance is part of the research result. A third-party file cannot be silently labeled as Dukascopy evidence.

## 2026-11-02 — Close the spot-XAUUSD GC-close momentum family

**Decision:** reject the tested 13:00–13:30 GC-close momentum transfer and stop
creating nearby cTrader variants on the exposed XAUUSD dates.

**Reason:** EXP-009 reproduced EXP-008 exactly and showed that momentum was
worse than always-long, always-short, and fixed-seed random controls. Every arm
lost after spread and commission, and momentum remained negative after a
mechanical zero-spread adjustment. Further direction reversal, timing changes,
thresholds, stops, or random-seed selection would be post-result tuning.

**Next evidence required:** actual GC futures data for source-market
replication, plus bid/ask XAUUSD ticks for an execution audit.

## 2026-11-02 — Deprecate EXP-010 and preregister the daytime anomaly

**Decision:** cancel the Python GC-futures replication path before any market
data is run, retain its files only as audit history, and allocate EXP-011 to a
cTrader test of the 08:20–13:30 negative daytime-return anomaly.

**Reason:** the operational experiment loop is cTrader cBots and supplied
backtest logs. EXP-010 had only synthetic implementation tests, so cancelling
it creates no result suppression and exposes no market split. Peer-reviewed
evidence for negative daytime gold returns supplies a new, low-parameter thesis
that is distinct from the rejected final-half-hour momentum signal.

**Locked comparison:** run the short arm first, then an otherwise identical
always-long control. No timestamp, weekday, volatility, stop, target, or
exposure search is authorized after inspection.

## 2026-11-02 — Reject the GC daytime-return transfer as executable edge

**Decision:** close EXP-011 and do not tune the 08:20–13:30 daytime-return
family on the exposed XAUUSD sample.

**Reason:** the fixed-sell primary lost 44.04% with PF 0.82, while the matched
long control lost 67.03% with PF 0.75. The short direction is relatively better
and the raw average daytime return is negative, but the effect is weak,
year-unstable, and economically insufficient. Even a mechanical zero-spread
scenario remains -$136.39 after configured commission and swaps. Execution-gap
sensitivities do not change the conclusion.

**Next constraint:** the next experiment must be a materially different,
preregistered thesis. No session endpoint, year, weekday, holiday, volatility,
stop, target, or cost search may be derived from EXP-011's exposed outcomes.

## 2026-11-02 — Select GLD R5-to-close momentum for EXP-012

**Decision:** test whether the sign of the 11:30–12:00 New York XAUUSD return
predicts the 15:30–16:00 return in the same direction.

**Reason:** peer-reviewed GLD evidence identifies this exact fifth-half-hour
predictor, reports both in-sample and out-of-sample predictability, and provides
a market-structure mechanism tied to late information and closing flows. Its
predictor, target interval, and NYSE-close mechanism differ from the rejected
COMEX 13:00–13:30 signal.

**Locked limits:** one unfiltered sign test, exact timestamps, 30 complete
predictor bars, 1.0x equity notional, no stop/target. Volatility/jump filters,
thresholds, reversal, and interval changes are prohibited after inspection.

## 2026-11-02 — Reject GLD R5 momentum transfer

**Decision:** close EXP-012 after the primary run and do not build its promotion
controls or tune another half-hour momentum variant.

**Reason:** the primary lost 52.32% with PF 0.27, and both directions and every
annual slice were negative. The source relationship did not transfer even
before costs: predictor/target correlation was -0.015, directional accuracy was
49.32%, and the raw equity-sized gain was only $114.69. Zero spread still leaves
-$997.06 after configured commission and swaps. Delayed-exit sensitivities do
not change the conclusion.

**Next constraint:** EXP-013 must use a materially different mechanism, not an
opening range, unconditional session drift, or same-day half-hour momentum
variant derived from the exposed results.

## 2026-11-03 — Reject extreme-move reversal transfer

**Decision:** close EXP-013 and do not tune the 2.0x ATR extreme-move reversal
family on the exposed XAUUSD sample.

**Reason:** corrected V2 lost 50.52% with PF 0.45 across 1,117 trades. Gross
P/L after the fixed spread but before commission was -$4,316.04; every
represented year and both directions were negative. A mechanical zero-spread
sensitivity remained -$1,985.12. All trades held the intended 30 minutes and
there were no order failures, so execution defects do not explain the result.

**Next constraint:** EXP-014 must be a materially different, preregistered
mechanism. Do not select a nearby threshold, ATR lookback, time window,
weekday, direction, or exit using EXP-013's exposed outcome.

## 2026-11-03 — Select London RSI ORB for EXP-014

**Decision:** allocate EXP-014 to the preregistered H5 family and test the
public LD-B London 30-minute ORB with Wilder RSI(14) agreement.

**Reason:** this hypothesis was recorded before EXP-013, uses a different
session, entry mechanism, holding horizon, and external evidence source, and
does not repair EXP-013 using its outcome. The source study reports a small
positive aggregate London result but failed robustness gates, making a single
locked broker-feed replication informative without creating a parameter
search.

**Locked adaptations:** require all 30 opening-range bars; trigger on cTrader
bid quotes and execute at ask/bid; attach the opposite-boundary stop to the
initial order; retain the source's $0.60 minimum range, RSI(14)/50 rule,
08:30–12:00 entry window, and 15:00 exit. No nearby settings are authorized
after inspection.

## 2026-11-03 — Reject London RSI ORB broker-feed replication

**Decision:** close EXP-014 and do not tune the London RSI ORB family on the
exposed FTMO XAUUSD sample.

**Reason:** the locked full-period run lost 14.65% with PF 0.77 across 1,185
trades. Every represented year lost net, gross after the fixed spread was
-$1,180.49, and the mechanical zero-spread sensitivity still lost $279.89.
The short subgroup was positive but was discovered through this run and does
not justify switching to a short-only variant on the same sample. Execution
integrity passed.

**Next constraint:** present only a distinct, independently preregistered
hypothesis as the next cTrader cBot; do not choose settings from EXP-014's
outcome.

## 2026-11-07 — Complete the 100-hypothesis research collection

**Decision:** close the research-only collection task at exactly 100 distinct
canonical hypotheses. See [[../01 - Research/Thesis Index - 100 hypotheses|index and completion audit]]
and [[../01 - Research/Thesis Catalog|full sourced catalog]].

**Counting corrections:** retire T012 into T006; correct T023 to H8/EXP-012's
existing rejection; consolidate ORB settings into T074 and unconditional
day/overnight asymmetry into T076. T075/T077/T078/T079 explicitly map other
legacy records once. 101 IDs have been issued and exactly 100 are counted.

**Interpretation:** directional/conditional predictions, risk forecasts,
execution hypotheses and negative results are distinguished. Daily/other-
asset/theoretical transfers are labeled as inferences, and inaccessible full
texts are not represented as audited replications. The collection creates no
validated edge. Only research Markdown and navigation were changed; no new
market test, implementation or live order was performed.

## Open decisions

- Exact full-history start date after provider coverage audit.
- Whether primary economic reporting uses per-ounce PnL, broker contract units, or both.
- Walk-forward window length and recalibration frequency.
- Formal multiple-testing correction for the final hypothesis family.
- Macro-announcement inclusion versus preregistered exclusion study.

