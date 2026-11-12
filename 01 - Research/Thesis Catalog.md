---
tags: [xauusd, thesis-catalog, intraday, research]
status: collection-complete
updated: 2026-11-07
goal: 100 distinct research hypotheses; none is a signal until validated
---

# XAUUSD intraday thesis catalog

Target: 100 distinct, documented hypotheses. A thesis is counted only when it
states a market mechanism and a falsifiable intraday prediction. Directional
ideas, execution/risk hypotheses, and rejected hypotheses are distinguished.
Repeated parameter variants do not count as distinct theses. No backtests or
implementations are authorized under the current research-only scope.

## Counted so far

The vault contains nine numbered ORB/momentum/reversal hypotheses (H1–H9), five
scan candidates (A–E), and two separately preregistered session hypotheses
(EXP-015/016). These overlap in places, so they are not blindly added to the
100 count. The numbered entries below are the canonical catalog; legacy notes
remain linked for history. Current canonical count: **100 / 100** (101 issued
IDs, T012 retired as a duplicate of T006). The count includes directional
predictions, volatility/risk forecasts, execution hypotheses and negative
results; it is not a count of profitable or validated strategies.
[[Thesis Index - 100 hypotheses|The compact index and completion audit]] list
all counted IDs and the legacy mappings. T023 records H8/EXP-012; T074–T079
consolidate other legacy families. Legacy labels and parameter variants never
add a second count.

## Shared interpretation and first-test requirements

These are research hypotheses, not complete implementation specifications.
Source findings and proposed XAUUSD predictions are separate: an inference
may deliberately test a null or challenge negative source evidence. A linked
publisher abstract supports only the finding it states; it does not mean the
full paper, its data or its reported performance was independently audited.
Many newer entries are substantial transfers from daily horizons, other
assets or theoretical models and have very low confidence.

Before any future experiment, freeze exact thresholds, clocks, sample,
missing-data rules, predictor receipt time and primary metric under
[[../02 - Methodology/Validation Protocol]]. Directional candidates must show
incremental forward prediction and positive net expectancy after executable
bid/ask, commissions, financing where applicable, slippage and plausible
latency, against time/exposure-matched controls. Risk forecasts must improve
forward loss or tail coverage against a nested simpler model. Execution/risk
policies must include all original opportunities, missed trades, nonfills and
equity paths, with exposure-matched controls. Statistical accuracy or a lower
loss alone does not validate a trading strategy.

Use actual broker contract terms, permitted data access and the intended
account's current news, order, maintenance, daily-reset and equity-loss rules.
Do not use delayed-price or external-slow-feed exploitation for the FTMO
research path. Availability of a source-market feature must precede order
submission. Missing licensed/proprietary data keeps a hypothesis untested;
it does not justify inventing a public proxy. All old exposed periods remain
development data; collection added no market runs or implementations.

### T001 — Post-release continuation after major U.S. data

**Type:** directional, event-conditioned; inference. **Confidence:** low.

After the initial CPI or Employment Situation repricing, the sign of the first
five-minute XAUUSD midpoint move may predict continuation over the next 25
minutes, but only when the move exceeds a trailing release-day threshold.
Gold's announcement-sensitive volatility is documented by
[Cai, Cheung & Wong](https://onlinelibrary.wiley.com/doi/10.1002/1096-9934%28200103%2921%3A3%3C257%3A%3AAID-FUT4%3E3.0.CO%3B2-W); they do not report this trading rule. **Constraint:** for FTMO, news-gap and account-specific rules must be reviewed before any trading use. Historical broker ticks and actual publication timestamps are missing.

### T002 — Tokyo-open price-pressure fade

**Type:** directional, session-conditioned; inference. **Confidence:** very low.

An unusually large first 30-minute Tokyo-session move may partially reverse
over the following 30 minutes if the move reflects short-lived uninformed
flow. [Iwatsubo, Watkins & Xu](https://www.rieti.go.jp/en/publications/summary/17110007.html)
find Tokyo daytime gold futures trading more uninformed in a 128-day 2014–15
sample; they do not establish reversal. Use one XAUUSD feed. Session calendar,
modern bid/ask ticks, and broker costs are required.

### T003 — Session-conditioned continuation strength

**Type:** directional, session-conditioned; inference. **Confidence:** low.

The predictive value of a same-direction intraday move may differ between
Tokyo, London, and New York sessions, with stronger follow-through during
New York's more informed trading hours. The mechanism is suggested by the same
[RIETI study](https://www.rieti.go.jp/jp/publications/dp/17e120.pdf), but it
does not show a return-sign forecast. Do not treat higher New York volatility
as proof of positive expectancy.

### T004 — Volatility-conditioned return feedback

**Type:** conditional signal; inference. **Confidence:** low.

The falsifiable prediction is that the slope of the next completed 15-minute
XAUUSD return on the preceding 15-minute return differs between high and low
prior-volatility states, within the same session. Define states with a
past-only 60-session median of same-clock variance and test one two-sided
interaction; do not choose a profitable direction after inspection.
[Batten et al.](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0174232)
report bilateral Granger causality between returns and volatility for most
subsamples. Granger causality is not an executable direction rule; the sign,
stability, and costs remain unknown.

### T005 — Calendar-only macro execution exclusion

**Type:** risk/execution hypothesis, not directional alpha. **Confidence:**
moderate mechanism, unproven benefit.

On scheduled CPI and Employment Situation days, spreads and volatility around
the release may reduce the quality of otherwise valid strategy entries. A
fixed event-window skip may improve same-date net expectancy by avoiding poor
fills. [Cai, Cheung & Wong](https://onlinelibrary.wiley.com/doi/10.1002/1096-9934%28200103%2921%3A3%3C257%3A%3AAID-FUT4%3E3.0.CO%3B2-W)
support event-sensitive volatility, not the profitability of a skip. Must
compare on the same eligible dates; fewer trades alone is not an edge.

### T006 — Gold reaction to extreme equity drawdowns

**Type:** cross-asset, event-conditioned; direct literature finding with
unproven retail translation. **Confidence:** low.

Extreme negative five-minute S&P 500 returns may precede a positive gold
reaction, consistent with flight-to-safety demand. The study
[The timing of the flight to gold](https://www.sciencedirect.com/science/article/abs/pii/S1544612319301448)
reports this relation using five-minute spot and futures data. A candidate
would require a threshold based only on prior S&P returns and entry in XAUUSD
at the next executable quote. Validate across crisis and ordinary regimes;
include spread, latency, and a matched no-signal control.

The [same study](https://www.sciencedirect.com/science/article/pii/S1544612319301448)
also reports that gold continued to rise **after U.S. stock trading hours** on
days with extreme equity declines. This is a secondary, equity-conditioned
extension of T006, not a new numbered thesis. A separate predeclared check
would mark the S&P 500 extreme using the completed regular-session close,
enter XAUUSD only at the next executable quote after that information exists,
and measure fixed post-close intervals on the broker feed. Compare with
same-time non-extreme and extreme-positive-equity days, excluding any interval
crossing the broker's maintenance break or financing rollover. Do not merge
this with [[Asia Rebound Screen - preregistered 2026-11-03|EXP-015]]: that
rejected screen conditioned on a **negative gold** U.S. session, not an
extreme equity decline. Neither the paper's after-hours observation nor
EXP-015 establishes an executable after-cost edge for this extension.

### T007 — Market-open volatility forecast for stop distance

**Type:** risk sizing/stop hypothesis; not directional alpha. **Confidence:**
moderate volatility mechanism, low strategy value.

Because gold volatility has time-of-day structure and is often elevated around
major market opens, a trailing same-time-of-day volatility estimate may set
more realistic protective distances than one fixed stop. [Batten et al.](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0174232)
document intraday periodicity. The hypothesis predicts improved drawdown or
execution robustness conditional on an existing positive signal; it cannot
rescue negative expectancy by itself.

### T008 — European-overlap execution-cost advantage

**Type:** execution hypothesis; no directional alpha. **Confidence:** low for
current CFD.

Gold bid-ask spreads may be lower during European market hours than during
less active hours, increasing the fraction of a small forecast captured by a
trade. [Batten et al.](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0174232)
report lower historical wholesale spreads when European
markets are open, while other samples show relatively stable gold spreads.
Measure the target broker's realized spread by time-of-day before using this
as a filter. It is an execution-cost thesis, not a return predictor.

### T009 — Dollar-fix pressure and post-fix gold reversal

**Type:** cross-asset, clock-time-conditioned; inference. **Confidence:** very
low.

The U.S. dollar has been documented to appreciate before major currency fixes
and reverse afterward. If that order-flow pressure passes through to
dollar-denominated gold, XAUUSD could weaken before a London or Tokyo fix and
rebound after the fixing window. [Krohn et al. (2024)](https://onlinelibrary.wiley.com/doi/abs/10.1111/jofi.13306)
study FX returns, not gold. This is a transmission hypothesis; test both fix
windows separately only if the original calendar windows are fixed in
advance. Need synchronized FX and broker XAUUSD quotes, and controls for the
London gold auction and simultaneous macro releases. This is not a delay-feed
strategy.

### T010 — Treasury-auction yield reversal and gold response

**Type:** scheduled-event, cross-asset; inference. **Confidence:** very low.

Treasury auction order flow may push yields higher before an auction and reverse
afterward; the gold hypothesis is that XAUUSD responds inversely to the
auction-driven real-rate component, with a post-auction rebound if yields
reverse. The [high-frequency Treasury auction study](https://fedinprint.org/item/fednsr/102915)
documents intraday price pressure and reversal in Treasuries, not a gold
response. It needs auction timestamps, intraday yield data, and executable
XAUUSD quotes; nominal-yield moves cannot be treated as real-yield moves
without an inflation compensation measure. No directional claim follows from
the Treasury study alone.

### T011 — U.S. equity-shock flight-to-gold decay

**Type:** cross-asset event response; direct short-run relation, unproven
trade. **Confidence:** low.

After an extreme negative S&P 500 five-minute return, gold may rise quickly,
then give back part of that response as immediate hedging demand fades. The
[flight-to-gold study](https://www.sciencedirect.com/science/article/pii/S1544612319301448)
reports a fast gold reaction to extreme negative equity returns; the proposed
decay/reversal is an inference. Specify the equity shock threshold from
trailing data, then measure XAUUSD returns in non-overlapping 5-, 15-, and
30-minute windows. A separate matched positive-equity-shock control is
required. Avoid treating this as a crisis-only anecdote; test ordinary periods
as well.

#### T012 — Retired duplicate: U.S. equity-shock continuation in gold

**Counting audit, 2026-11-07:** merged into T006. Persistence of the same
equity-conditioned safe-haven response is already T006's prediction; changing
the measurement window does not earn another count. T011 remains the competing
decay mechanism. The original discussion follows for provenance.

**Type:** cross-asset event response; alternate inference to T011.
**Confidence:** low.

Instead of fading the initial safe-haven response, a sufficiently severe
equity shock may produce persistent portfolio de-risking and continued gold
demand for a short interval. Use the same event definition as T011, but freeze
this as a separate directional hypothesis before any results are seen. The
source establishes a fast reaction, not continuation. T011 and T012 are
competing hypotheses, not two tunable variants; compare them with the same
event set and same executable timing, and correct inference for testing both.

### T013 — COMEX volatility state and Asian gold response

**Type:** session-conditioned, cross-market state; inference. **Confidence:**
low.

The return behavior of Asian gold futures may change after a high-volatility
COMEX session. The concrete transfer prediction is that the slope of the
09:00–09:30 Asia/Shanghai XAUUSD return on the last completed COMEX daytime
return differs between high/low prior-COMEX-variance states, after controlling
for the current Asian pre-open return and variance. Define the state using
a prior-60-session median and test the interaction two-sided; neither a
continuation nor a reversal is selected after results. Informed information
flow and temporary liquidity pressure are competing explanations. A recent
[Chinese gold and silver futures study](https://www.sciencedirect.com/science/article/pii/S1044028325000110)
reports that both intraday reversal and momentum patterns vary with realized
volatility and absolute returns around SHFE night sessions. It is venue- and
sample-specific, and does not establish an XAUUSD rule. The candidate must
define the prior COMEX state without looking ahead, use one executable XAUUSD
feed for orders, and compare conditional returns with unconditional Asian
returns. Do not use cross-broker price catch-up.

### T014 — PM gold auction opening continuation

**Type:** auction-conditioned directional; inference. **Confidence:** very low.

If the first minute after the 15:00 London PM gold auction start contains an
unusually large XAUUSD move, auction-related price discovery might continue
for another three minutes. [Caminschi and Heaney](https://research-repository.uwa.edu.au/files/4725264/A0205_use_this.pdf)
found GC/GLD activity concentrated immediately after the **old** fixing began;
their return advantage used the eventual fixing direction, unavailable at
entry. [ICE](https://www.ice.com/iba/lbma-precious-metals) confirms the current
auction start clock, while [LBMA](https://www.lbma.org.uk/articles/the-new-lbma-gold-price-successfully-launched-on-20th-march-2015)
documents the 2015 redesign. The exact first-minute signal, rolling threshold,
cost stress, matched controls, and failure gate are in
[[London PM Gold Auction Opening - hypothesis 2026-11-04]]. Requires broker
bid/ask ticks; available M5 bars cannot test it. Use one broker feed and never
use a final benchmark value before its actual publication. **Status:** untested.

### T015 — Gold round-number approach rejection

**Type:** price-level-conditioned directional; inference. **Confidence:** very low.

If round dollar levels attract clustered orders, the first approach to a
previously untouched **$10 multiple** during a liquid intraday session may
reject more often than a matched approach to a non-round level. [O'Connor and
Lucey (2016)](https://ray.yorksj.ac.uk/id/eprint/1438/1/The%20Gap%20Psychological%20Barriers%20in%20Gold%20and%20Silver%20Prices.pdf)
use London AM/PM fixes from 1975–2015. Their price-density tests find fewer
fixes near $100 and $10 multiples, but their **conditional-return** test
supports $10 barriers and does not reject its no-barrier null at $100. None of
these tests demonstrates a minute-scale bounce or a tradable CFD edge.
The falsifiable XAUUSD prediction is a higher probability of a move away from
the $10 level over the next 15 minutes after first approach, conditional on
the same prior 15-minute volatility, time of day, approach speed, and distance
traveled. Freeze touch and rejection definitions before testing; compare with
direction-matched placebo grids shifted $3 and $7 from each $10 multiple.
Subtract executable spread, commission, and slippage. Broker bid/ask ticks are
needed to determine touch order and fill quality. Use the target broker's own
quotes; avoid stop orders
that violate its minimum distance. **Status:** untested, with the old
fix-to-modern-intraday transfer the primary uncertainty.

Report post-cross acceleration as a prespecified diagnostic because the paper's
barrier framework allows both rejection on approach and faster movement after
a breach; do not choose the winning direction after inspecting outcomes.

### T016 — Asymmetric FOMC easing-surprise continuation

**Type:** scheduled-event directional; inference from measured response.
**Confidence:** low for market response, very low for an executable edge.

[Awartani, Hussain and Virk (2024)](https://ideas.repec.org/a/eee/finana/v95y2024ipbs1057521924004186.html)
report that gold futures react more strongly to unexpectedly looser than to
unexpectedly tighter FOMC policy, with return and volatility adjustment beyond
five minutes. The falsifiable XAUUSD prediction is that, **only after an
independently timestamped easing surprise**, a long entered after the first
five complete minutes has positive net return over the next five minutes and
outperforms both a same-time random-direction control and the symmetric
tightening-surprise short. The source establishes response asymmetry, not an
after-cost broker trade. [[FOMC Easing Surprise Continuation - hypothesis 2026-11-04|The detailed first-test design]] freezes the surprise measure and threshold using
information published by the entry time; require actual FOMC release times,
real-time policy-expectation data, broker bid/ask ticks, and slippage stress.
Exclude press-conference and other overlapping information windows by a
predeclared rule. For any FTMO use, recheck account-specific news restrictions
and the broader news-gap prohibition before trading; the hypothesis remains
research-only. This is distinct from T001's CPI/Employment first-move signal.
**Status:** untested.

### T017 — Post-jump spread recovery before XAUUSD entry

**Type:** event-conditioned execution gate; no directional alpha.
**Confidence:** low for retail CFD transfer.

The [gold futures/GLD jump study presented at EFMA
2024](https://www.efmaefm.org/0EFMAMEETINGS/EFMA%20ANNUAL%20MEETINGS/2024-Lisbon/papers/EFMA_COMPLETEMANU_NSOBTI.pdf)
reports sharply wider effective spreads around five-minute gold jumps and
normalization several minutes afterward, while other liquidity measures can
recover more slowly. The falsifiable broker-feed prediction is that, after a
large **completed** five-minute XAUUSD midpoint return defined against prior
same-time volatility, waiting for the observed bid/ask spread to return below
its trailing same-minute median reduces the round-trip execution cost of an
otherwise frozen signal. Compare signal trades that occur during the wide
spread state with the same signal's trades after the recovery condition;
report missed opportunities and market movement during the wait. A better fill
alone does not establish positive strategy expectancy. Require timestamped
broker bid/ask quotes and order fills; available M5 OHLC cannot measure the
spread-recovery trigger reliably. Do not use another venue's jump timestamp as
a slow-feed arbitrage signal. [[Post-Jump Spread Recovery - hypothesis 2026-11-04|The detailed first-test design]] fixes the event, gate, and paired controls. **Status:** untested.

### T018 — Downside semivariance and near-term stop risk

**Type:** return-sign-conditioned volatility and risk hypothesis; no
directional alpha. **Confidence:** moderate historical mechanism, low broker
transfer confidence.

Using 13.5 years of intraday gold futures, [Todorova (2017)](https://www.sciencedirect.com/science/article/pii/S0165176516304876)
finds that recent negative semivariance plays a persistent short-horizon role
in gold realized-volatility dynamics, while positive semivariance is more
important at longer horizons and during rising-gold periods. The falsifiable
XAUUSD prediction is that, for two otherwise matched intraday states with the
same total prior 30-minute realized variance, the state with a larger share
from negative five-minute returns has a higher probability of a large adverse
excursion in the next 30 minutes. Define the semivariance ratio using completed
broker midpoint bars only, then compare next-window excursion and stop-hit
rates with time-of-day and news-calendar matching. Compare a frozen protective
stop rule with a rule scaled by the predicted excursion; both must retain the
same initial dollar risk. A stop improvement counts only if the underlying
strategy has positive net expectancy after executable spread, commission, and
slippage and survives the firm's intraday equity limits. This differs from
T007's clock-time volatility estimate and T004's generic return/volatility
feedback: it tests the **sign decomposition** of recent realized variance.
Requires broker bid/ask ticks for actual stop sequencing and fill costs; M5
bars can screen predictive volatility but cannot validate stop execution.
**Status:** untested.

### T019 — Forecast disagreement and release-window risk

**Type:** pre-event, survey-conditioned volatility and execution hypothesis;
no directional alpha. **Confidence:** moderate historical futures mechanism,
low current broker transfer confidence.

[Smales and Yang (2015)](https://espace.curtin.edu.au/bitstream/handle/20.500.11937/40677/227313.pdf?sequence=4)
find that COMEX gold futures' immediate volume and volatility responses to
major U.S. releases were sharper when the **pre-release dispersion of analyst
forecasts** was high. Their study says much of the response dissipated within
roughly 90 seconds; it does not show a 5–30 minute return edge or broker spread
effect. The falsifiable XAUUSD prediction is that a high, prior-history-ranked
forecast-dispersion state has a higher probability of a broker midpoint jump
and wider executable spread in the first two minutes after a release than a
low-dispersion state for the **same release type**. Freeze a release-specific
top-quintile threshold using only earlier surveys; use forecast vintages
available before 08:30 New York time, actual publication timestamps, and
broker bid/ask ticks. Match or stratify on realized surprise size when testing
whether dispersion adds information beyond the announcement outcome; never
use the realized surprise in a pre-release trading decision. A candidate
execution rule would close an existing vulnerable position before the event
when high dispersion is observed, then compare its full net P/L and missed
upside with the unfiltered frozen strategy on the same opportunity set.
The source's 2006–2012 COMEX result needs modern replication. FTMO's XAUUSD
news execution restriction and broader gap-trading policy must be checked for
the exact account; a first-two-minute news entry is not proposed. **Status:**
untested. This differs from T005's calendar-only skip because the conditioning
variable is the **pre-release distribution of forecasts**, not the event date.

A separate [one-minute, three-market price-discovery
study](https://www.sciencedirect.com/science/article/pii/S1057521921002209)
finds larger news surprises and greater analyst-forecast dispersion associated
with stronger COMEX futures price-discovery leadership. That supports the
information-flow mechanism, but leadership share is **not** XAUUSD jump
probability, spread, or a directional forecast. Keep realized surprise as an
evaluation control only; it is unavailable when a pre-release risk gate must
act.

### T020 — Negative gold-news sentiment and downside jump hazard

**Type:** timestamped information, directional jump-risk hypothesis;
inference for XAUUSD execution. **Confidence:** low.

[Sobti (2025)](https://ideas.repec.org/a/eee/finana/v105y2025ics1057521925004673.html)
reports that negative news sentiment predicts negative intraday jumps in gold
futures and GLD, using proprietary high-frequency news and market-psych data.
That classification result does not establish that a broker XAUUSD order can
be filled **before** the jump or profit after costs. The falsifiable transfer
prediction is that, after a gold-specific negative news sentiment update has
actually reached the researcher, the next **non-overlapping five-minute**
XAUUSD midpoint bar has a higher downside-jump probability than matched
same-clock bars without such an update, after controlling for the prior gold
return, spread, scheduled-news calendar, and volatility state. Define the
sentiment score, jump threshold, and delivery-lag allowance before results;
never timestamp the signal at the article's creation if the tradable feed
delivered it later. A proposed short may enter only at the first executable
broker quote after that timestamp and must beat matched random-direction and
no-news controls after spread, commission, slippage, and latency stress.
Licensed point-in-time news, broker bid/ask ticks, and a delivery-time audit
are required. Exclude scheduled major-news windows and verify the target
account's rules, including FTMO's prohibition of news-gap trading; do not use
another broker's price as a catch-up signal. **Status:** untested.

### T021 — Weekly boundary quote-quality gate

**Type:** calendar-and-quote-conditioned execution hypothesis; no directional
alpha. **Confidence:** low, based on a few recent spot episodes.

The [World Gold Council's April 2026 spot-gold liquidity
analysis](https://www.gold.org/goldhub/gold-focus/2026/04/you-asked-we-answered-has-golds-performance-structurally-changed)
reports that its four largest recent intraday spread spikes occurred around
Sunday night/Monday morning or late Thursday night/Friday as gold entered the
Asian trading day. It also says the widening was short lived. This is a
**descriptive spot quote observation**, not proof of a standing broker-hour
anomaly or a profitable filter. The falsifiable XAUUSD prediction is that,
conditional on the same completed-bar volatility and a frozen strategy signal,
the intended broker's median executable spread and adverse slippage are
higher during its first 30 tradable minutes after the weekly reopen and the
corresponding late-week Asian open than during matched Tuesday–Thursday
windows. Use the broker's actual historical trading calendar and IANA local
time, with holidays, early closes, and DST handled explicitly; never infer
the CFD's reopen from the CME contract clock. A predeclared gate would require
the spread to fall below its prior-60-valid-session same-minute median before
entry, with a fixed 30-minute expiry and no extension of the original exit.
Compare paired full net P/L per original opportunity, counting skips as zero,
against the ungated frozen strategy and same-weekday placebo windows. Require
broker bid/ask ticks and actual fills; M5 bars cannot establish quote age,
spread spikes, or missed price movement. A lower median cost alone cannot
promote an unprofitable strategy. Check firm rollover and weekend execution
rules for the intended account. **Status:** untested. This differs from T008's
Europe-versus-other-session liquidity thesis: it tests **weekly boundary
hours and broker reopen behavior**.

### T022 — Shanghai night-information transfer to the next day open

**Type:** cross-session directional transfer; inference for continuously
traded XAUUSD. **Confidence:** very low.

A [study of Chinese metal futures](https://acfr.aut.ac.nz/__data/assets/pdf_file/0008/686816/4a-Z-Ivy-Zhou.pdf)
reports a positive relation between the first SHFE gold night-session return
and its next daytime first-half-hour return (reported t-statistic 3.0,
in-sample R² 0.56%). Its definitions matter: the night predictor runs from
the **previous 15:00 China close to 21:30**, and the daytime outcome from the
**02:30 night close to 09:30**. Both include closed-exchange gaps. A retail
XAUUSD position entered at 09:00 cannot capture the 02:30–09:00 portion of
that measured outcome. The falsifiable *tradable* transfer prediction is that
the sign of the XAUUSD midpoint change from 15:00 to 21:30 `Asia/Shanghai`
predicts the **09:00–09:30** XAUUSD return on the next valid SHFE gold day.
Use the published SHFE calendar and its actual historical session times,
exclude holidays and partial sessions, and measure the predictor on one
broker feed before the 09:00 entry. Compare a 09:00 sign-following entry with
same-window opposite-direction, always-long, and deterministic-random
controls; charge actual bid/ask, commission, and slippage. Separately report
the 02:30–09:00 pre-entry component to show how much of the paper's relation
was already gone by entry. The available M5 bid/ask bars can screen the
09:00–09:30 midpoint relation, but executable entry and stop claims need
broker ticks. This is not a cross-feed price-delay strategy. It is distinct
from T013's prior-COMEX-volatility state and EXP-016's fixed overnight long.
Reject unless the post-entry return is positive after costs, beats the matched
controls, and persists on genuinely later untouched dates. **Status:**
untested on XAUUSD; no source-market result is a broker edge.

### T023 — U.S. cash-session midday move and late gold return

**Type:** within-session directional transfer; inference for XAUUSD.
**Confidence:** very low.

[Bouri et al. (2020)](https://pmc.ncbi.nlm.nih.gov/articles/PMC7480318/)
find that GLD's fifth regular-session half-hour return (11:30–12:00
`America/New_York`) positively predicts its final half-hour return
(15:30–16:00). Their in-sample slope is 0.0436 (t = 3.03; R² = 0.49%),
and the selected predictor has a positive out-of-sample R² of 0.26% in
their 2004–2019 GLD sample. They suggest infrequent portfolio rebalancing
and late-informed investors as possible explanations, **not established
causes**. The narrow transfer prediction is that the sign of XAUUSD's own
11:30–12:00 midpoint return predicts its 15:30–16:00 midpoint return on
NYSE/GLD trading days, with an entry at the first executable broker quote
at or after 15:30 and a time exit by 16:00 New York time. Use an IANA
timezone and the NYSE calendar; exclude early closes and stale/missing
quotes. Predeclare a separate GLD-sign comparison only if point-in-time GLD
quotes are available by 15:30; never treat delayed ETF data as live.
Compare net bid/ask results with always-long, opposite-sign, and same-time
random controls, and report the midpoint relation separately. Split results
by pre-known volatility and scheduled U.S. news days, but do not choose
thresholds on the evaluation period. The paper's GLD slope was 0.0591
(t = 2.82) in its high-volatility subgroup and 0.0084 (t = 0.44) in its
low-volatility subgroup. It formed that split using realized volatility
from the **first** half-hour, available before the 11:30–12:00 predictor;
the same timing discipline is required for XAUUSD. This is a subgroup
diagnostic of T023, not another counted strategy. GLD's tiny forecast R²
and source-market execution results do not imply CFD profitability. Broker
bid/ask ticks, actual spread/slippage, NYSE sessions, and the intended account's
intraday news and execution rules are required. This is distinct from
T003's broad session-strength question: it tests a **specific U.S. ETF
cash-close horizon** with a multi-hour gap after the predictor. **Status:**
**Rejected on the tested XAUUSD mapping:** this is the canonical record of
H8 / EXP-012, not a new independent idea. EXP-012 returned -52.32% ROI,
PF 0.27 over 1,616 trades; raw predictor/target correlation was -0.015.
Every annual slice and both directions lost. See [[Research Hypotheses#H8 — GLD fifth-half-hour to closing-half-hour momentum]]
and [[../outputs/EXP-012-gld-r5-primary-2026-11-02/RUN-NOTE]]. Its volatility
subgroup is a source diagnostic only; it must not be used to repair the
exposed result. Correction recorded 2026-11-07.

### T024 — Futures order imbalance and next-interval gold jump risk

**Type:** cross-venue microstructure, directional jump-risk hypothesis;
inference for broker XAUUSD. **Confidence:** very low.

[Sobti (2025)](https://www.sciencedirect.com/science/article/pii/S1057521925004673)
reports that order imbalance and trading activity predict intraday jumps in
CME gold futures and GLD; its event study associates buy-side order flow
with positive jumps and sell-side flow with negative jumps. It does not prove
that an XAUUSD order placed **after** observing a completed futures interval
can profit before the move is priced into the broker quote. Aggressive
same-side futures trading could deplete available liquidity, creating a
short-lived directional jump hazard. The falsifiable transfer prediction is
that the signed CME gold-futures taker-volume imbalance measured over a
fully completed five-minute interval predicts a higher probability of a
**same-sign jump in the next non-overlapping five-minute XAUUSD midpoint
interval** than matched same-clock intervals with neutral imbalance. Freeze
the imbalance and jump thresholds using only preceding dates; timestamp the
signal when the complete exchange interval and data feed are actually
available. Compare positive and negative sides separately and stratify by
pre-signal spread, volatility, depth, and scheduled U.S. news. A proposed
entry is at the first executable XAUUSD quote after feed receipt, never at
the futures bar close retroactively. Require that any signed-return benefit
survive spread, commission, slippage, latency stress, and same-time
random-sign and opposite-sign controls. Licensed time-stamped CME trades,
contract rolls, broker bid/ask ticks, and feed-delivery logs are needed;
the available M5 CFD bars cannot reconstruct aggressive futures flow or
fills. Exclude major scheduled-news windows for the first screen and check
the intended prop account's news and execution restrictions before any
use. This is distinct from T020's **text-sentiment** information channel
and T017's **post-jump spread** gate. **Status:** untested on XAUUSD.

### T025 — Leveraged gold-fund rebalancing pressure

**Type:** directional flow hypothesis; inference. **Confidence:** very low.

[Todorov's ETF study](https://www.bis.org/publications/working-paper-952-passive-funds-affect-prices-evidence-most-etf-dominated-asset-classes)
finds price pressure from ETF demand in volatility and commodity markets,
with leverage rebalancing especially influential. It does not establish an
XAUUSD strategy. Fixed leverage can force funds to increase exposure after
gains and sell after losses. **Prediction:** a gold-specific signed estimate
of remaining daily rebalancing demand, constructed at 15:00 New York from
previously published AUM, mandate leverage and returns observed so far,
predicts same-sign XAUUSD returns from 15:00 to 16:00, after controlling for
the day's gold return. Compare high-demand dates with return-, volatility-
and clock-matched low-demand dates and shuffled-demand controls. This tests
mechanical fund demand, distinct from T023's lagged half-hour return.
Require historical fund mandates, disclosure availability times, actual
rebalance clocks, synchronized futures and broker bid/ask ticks. Do not
substitute daily final AUM or closing returns at entry. Reject without
incremental later-date prediction and positive executable net P/L. **Account
constraint:** intraday equity limits and permitted order sizing; no
cross-feed catch-up assumption. **Status:** untested.

### T026 — Announced gold-margin increase and intraday liquidation

**Type:** funding-pressure directional hypothesis; inference. **Confidence:** very low.

[BIS, March 2026, Box C](https://www.bis.org/publications/qr-202603/markets-recalibrate-amid-shifting-currents)
discusses margin-triggered deleveraging during the gold/silver sell-off;
the episode is not a causal identification of a trade. Higher collateral
requirements can force constrained longs to sell independently of fresh
fundamental news. **Prediction:** after a publicly announced increase in
GC initial margin, the first 60 liquid minutes following its actual
publication have more negative XAUUSD returns when the latest already
published speculative positioning is heavily long than on matched
non-increase dates. Freeze positioning percentiles from prior observations;
use the release time, not its future effective date, as the initial event.
Control for prior gold losses, volatility, scheduled news and contemporaneous
margin changes elsewhere. Require timestamped exchange notices, vintage
positioning reports, and broker ticks. Compare with unchanged-margin
high-volatility dates; reactive margin policy is a major confounder.
Distinct from T025's recurring daily leverage reset. **Account constraint:**
review event execution rules and intraday loss limits. Reject if the effect
exists only before announcement or disappears after costs. **Status:** untested.

### T027 — FOMC uncertainty resolution and subsequent intraday range

**Type:** volatility/risk forecast; no directional alpha; inference.
**Confidence:** low for broker transfer.

[Gu, Kurov and Stan (2023)](https://www.sciencedirect.com/science/article/pii/S1544612323002799)
find declining gold implied volatility around FOMC statements, with stronger
resolution on SEP dates and a gold adjustment taking about ten minutes.
GVZ represents option-implied 30-day uncertainty, not the next CFD range.
**Prediction:** the observed GVZ decline from five minutes before to ten
minutes after a statement predicts lower XAUUSD realized variance over
minutes +10 to +25, conditional on the initial gold jump and prior realized
variance. Use only received GVZ quotes; end before the usual press conference
and use actual historical schedules. Compare a gold-only volatility model
with one adding the GVZ change on identical events; SEP is a predeclared
interaction, not another thesis. Require licensed intraday GVZ, event
timestamps and broker ticks. Distinct from T016's directional easing
surprise and T017's spread normalization. Reject without later-event
forecast-loss improvement and well-calibrated range coverage. A better
forecast is not positive strategy expectancy. **Account constraint:** any
subsequent use must satisfy the intended account's news rules. **Status:** untested.

### T028 — Segmented-session variance forecasts the Shanghai close

**Type:** intraday volatility forecast; inference. **Confidence:** low.

[Intraday volatility predictability in China gold futures (2023)](https://www.sciencedirect.com/science/article/pii/S154461232300394X)
reports that separately measured session-block realized variances improve
SHFE gold's last-half-hour forecasts, including information from night and
U.S. trading. **Prediction:** at 14:30 Asia/Shanghai on valid SHFE days,
separate XAUUSD variances from completed SHFE day/night blocks forecast
14:30–15:00 broker midpoint variance more accurately than the same model
using their pooled total. Information arrival and heterogeneous trader
groups may give block location value beyond total variance. Freeze block
definitions from historical exchange calendars and estimate coefficients
only on earlier sessions. Benchmark against clock-only, pooled-variance and
simple recent-variance forecasts using forward loss and range coverage.
Require broker intraday quotes, SHFE calendars and exact missing-block
rules; futures additions need receipt timestamps and roll handling.
Distinct from T007's clock profile and T018's variance-sign split. Reject
without incremental untouched-date accuracy; no direction follows from RV.
**Account constraint:** any risk-sizing application must preserve initial
dollar risk and satisfy equity loss limits. **Status:** untested.

### T029 — Central-bank information shocks versus pure policy shocks

**Type:** cross-asset event classification; inference. **Confidence:** very low.

[Jarociński and Karadi (2020)](https://www.aeaweb.org/articles?id=10.1257/mac.20180090)
separate monetary-policy and central-bank-information shocks using
high-frequency rate/equity co-movement. Their result is not a gold forecast.
An increase in rates paired with falling equities may transmit a different
opportunity-cost/safe-haven mix to gold than one paired with rising equities.
**Prediction:** conditional on equal-size rate increases over a completed
FOMC five-minute window, subsequent XAUUSD returns over minutes +5 to +20
differ between the negative- and positive-equity-response groups. The
primary claim is a signed-return difference, not an assumed profitable
long or short. Freeze the classifier using available rate futures and
equity futures; do not label events from revised full-sample shock estimates.
Require synchronized feeds, delivery times, actual statements and broker
bid/ask ticks. Compare against rate-only and gold-first-move-only models;
control for initial gold returns and exclude overlapping releases. Distinct
from T016's easing/tightening asymmetry: this separates the information
content of similarly signed rate moves. **Account constraint:** news-policy
compatibility before any trading use. Reject without stable forward
incremental prediction; all trade translations require costs. **Status:** untested.

### T030 — Quote withdrawal versus traded-volume imbalance

**Type:** microstructure risk forecast; inference across asset classes.
**Confidence:** very low.

[Cont, Kukanov and Stoikov](https://arxiv.org/abs/1011.6402)
find short-interval equity price impact relates to best-quote order-flow
imbalance and inversely to depth; quote additions and cancellations matter,
not just trades. This is contemporaneous stock evidence. **Prediction:**
in GC intervals matched on aggressive traded-volume imbalance and gold
volatility, unusually large net cancellation of bid-side liquidity predicts
higher downside excursion in the next non-overlapping five-minute XAUUSD
interval. Liquidity withdrawal could leave less capacity to absorb selling.
Freeze cancellation intensity relative to preceding same-clock sessions;
identify true cancellations separately from executions, contract rolls and
feed resets. Require licensed message-level futures data, sequence recovery,
clock/receipt audits and broker ticks. Benchmark against trade-flow-only
and depth-only forecasts; measure incremental tail-risk calibration, not
only contemporaneous correlation. Distinct from T024's signed taker volume.
Reject if the relation vanishes after signal receipt or only reflects
already completed price changes. **Account constraint:** no exploitation
of a delayed broker feed; a risk gate must include missed opportunities
and preserve the original exit. **Status:** untested.

### T031 — Option-implied uncertainty beyond realized volatility

**Type:** intraday range forecast; inference from a longer forecast horizon.
**Confidence:** low.

[Luo, Qin and Ye (2016)](https://doi.org/10.1016/j.frl.2016.06.012)
report that GVZ improves next-day SHFE gold volatility forecasts and
outperforms historical jump information. Their next-day finding does not
establish a 30-minute forecast. Forward-looking option demand may contain
risk information absent from completed spot returns. **Prediction:** at
10:00 New York, the latest received GVZ level improves forecasts of
10:00–10:30 XAUUSD variance after controlling for prior broker variance,
clock seasonality and the scheduled calendar. Compare identical expanding
gold-only and gold-plus-GVZ forecasts using forward QLIKE and coverage;
estimate the GVZ coefficient rather than equating annualized 30-day IV
with half-hour variance. Require intraday GVZ publication/delivery logs and
broker quotes. Distinct from T027's FOMC-specific IV decline: this concerns
option-implied risk levels on ordinary sessions. Reject without incremental
later-date accuracy. **Account constraint:** any sizing translation must
retain fixed dollar risk and respect intraday equity limits. No directional
trade follows from a successful variance forecast. **Status:** untested.

### T032 — Discontinuous jump share and subsequent tail risk

**Type:** volatility-state decomposition; inference. **Confidence:** low.

The [same 2016 gold study](https://doi.org/10.1016/j.frl.2016.06.012)
finds forecasting information in historical jumps. A discontinuous shock may
reflect unresolved information or liquidity distress differently from smooth
diffusive activity of equal total variance. **Prediction:** among completed
60-minute XAUUSD windows with the same total realized variance, those with
more statistically identified jump variation have different next-30-minute
extreme-excursion probabilities. Freeze one jump-robust continuous-variance
estimator and its test threshold on earlier dates; use sufficiently dense
midquotes and seasonality adjustment. The primary is a two-sided conditional
hazard difference; do not choose continuation or fade after inspection.
Compare with total-variance-only and signed-semivariance models. Require
broker ticks, quote-error filtering and event timestamps. Distinct from
T018's positive/negative decomposition: this tests discontinuity after
controlling for return sign; distinct from T017's execution spread gate.
Reject unless tail calibration improves on untouched dates. **Account
constraint:** risk controls must include intraday equity excursions and
real stop fills; a statistical jump is not a guaranteed fillable price.
**Status:** untested.

### T033 — Previously published policy uncertainty and intraday jump hazard

**Type:** slow-state-conditioned intraday risk forecast; inference.
**Confidence:** very low.

[Zhang et al. (2021)](https://www.sciencedirect.com/science/article/pii/S0301420721000933)
link daily economic policy uncertainty to time-varying gold realized
volatility and jumps, with less stable return effects. Their intraday data
construct daily measures; they do not prove a minute-level trade.
Uncertain policy may leave investors more sensitive to fresh information.
**Prediction:** the latest already published daily EPU vintage improves
ordinary-session next-30-minute XAUUSD jump-probability forecasts beyond
gold's own recent variance, jumps and scheduled-news indicators. Use an
explicit release-time lag and never assign the day's retrospectively
computed newspaper index to its morning. Compare gold-only models with
one prespecified EPU interaction; match clock time and prior risk. Require
vintage EPU release records and broker ticks; absent those, keep this
unexecutable. Distinct from T020's newly delivered article sentiment and
T019's release-specific survey disagreement. Reject without incremental
forward hazard calibration across multiple regimes. **Account constraint:**
any risk gate must count skipped opportunities and observe the intended
account's intraday equity limits. **Status:** untested.

### T034 — Predictable index rolls and broker execution liquidity

**Type:** calendar-conditioned execution hypothesis; inference. **Confidence:** very low.

[Lauter, Prokopczuk and Trück (2026)](https://link.springer.com/article/10.1007/s11147-026-09235-w)
find that commodity index roll activity did not harm aggregate intraday
market quality; some liquidity measures improved across regimes. Their
design is not a direct roll-day-versus-neighbor-day CFD comparison.
Predictable uninformed demand may attract offsetting liquidity supply.
**Prediction:** during actual gold index-roll execution windows, matched
XAUUSD orders have lower spread plus adverse slippage than same-clock
non-roll orders, conditional on volatility and order size. Establish
the historical gold-specific index contract schedules first; do not assume
gold rolls every month just because an index has monthly roll days.
Compare identical frozen signals and report cost, completion and net P/L
per original opportunity. Require vintage index methodologies, GC
contract identifiers, broker ticks and fills. Distinct from T008's daily
European overlap and T025's directional leverage reset. Reject if lower
futures impact does not transfer to actual broker costs. **Account
constraint:** obey order-size and execution restrictions; no futures-to-CFD
stale-price arbitrage. **Status:** untested.

### T035 — Chinese physical premium and London-morning gold demand

**Type:** regional physical-demand directional hypothesis; inference.
**Confidence:** very low.

[CME's February 2022 commodity note](https://www.cmegroup.com/newsletters/commodity-futures-and-options/commodity-futures-and-options-february-2022.html)
describes Shanghai's gold premium and its relation to Chinese imports and
jewellery demand; it supplies no intraday forecast. Import demand could
support offshore bullion purchases as London liquidity arrives.
**Prediction:** a high Shanghai spot premium observed before 08:00 London
predicts a more positive 08:00–09:00 XAUUSD return than matched neutral-premium
dates, conditional on prior gold and CNY moves. Use synchronized SGE spot,
offshore gold and FX quotes with correct units, purity and taxes; never
compare a Shanghai live price with a later London PM fix. Freeze a trailing
premium percentile and control for quotas, Chinese holidays and London
news. Require point-in-time quotes and quota-policy history, plus broker
bid/ask ticks. Distinct from T022's prior-night return: this measures a
regional physical-price wedge. Compare premium-shuffled and unconditional
long controls; reject without later-date incremental prediction and net
profit. **Account constraint:** trade only the broker's executable quote;
no claim of executing physical arbitrage through a CFD. **Status:** untested.

### T036 — Dealer gamma and ordinary-session return feedback

**Type:** option-hedging directional-state hypothesis; inference.
**Confidence:** very low.

[Baltussen et al. (2021)](https://www.sciencedirect.com/science/article/pii/S0304405X21001598)
link intraday momentum to hedging demand, including direct S&P option
evidence. Their gold-close transfer already failed here as H6; this
entry does not reopen that exposed experiment. Short-gamma dealers may
amplify moves while long-gamma dealers absorb them. **Prediction:** the
slope of 10:00–10:30 XAUUSD returns on completed 09:30–10:00 returns is
more positive when a gold-options dealer-gamma estimate frozen at 09:25
is negative than when positive. Require GC option quotes, dated open
interest, expiry calendars and independently defensible dealer-position
signs; call/put OI alone does not identify dealers' net exposure. Publish
sign-assumption sensitivity and compare with gold-return/volatility-only
controls on later untouched dates. Missing credible dealer signs blocks
an empirical classification. Distinct from T025's fund leverage mandates
and T023's lagged-close relation. Reject without incremental prediction
and executable after-cost benefit. **Account constraint:** no external
slow-feed catch-up and no equity-limit breach. **Status:** untested;
data-identification risk is severe.

### T037 — Slow moving-average trend persistence

**Type:** technical directional hypothesis; inference. **Confidence:** very low.

[Does intraday technical trading have predictive power in precious metal markets?](https://doi.org/10.1016/j.intfin.2017.06.005)
finds no predictability from traditional MA settings, with some longer
settings predictive after a large search in historical five-minute gold.
The search is evidence of selection risk, not a ready-made parameter.
Slow information diffusion or persistent order splitting could create trends
outside opening-range events. **Prediction:** a single slow MA rule locked
from the source's economic timescale before broker-data inspection produces
positive signed next-hour XAUUSD returns and net P/L, relative to same-clock
random and opposite-sign controls. Freeze one MA definition, lookbacks,
signal clock, intraday flattening time and turnover cap in a later
preregistration; no parameter sweep is authorized. Require broker bid/ask
bars or ticks and session history. Distinct from ORB boundary crossing and
T023's specific lagged half-hour. Reject unless genuinely later data show
after-cost prediction robust to modest preregistered perturbations.
**Account constraint:** fixed initial risk, valid protective orders and
intraday equity limits. **Status:** untested; source-selected settings do
not constitute independent validation.

### T038 — VWAP-aligned pullback continuation

**Type:** technical directional hypothesis; unreviewed preprint transfer.
**Confidence:** very low.

[Bhatti (2026)](https://ssrn.com/abstract=6650958)
proposes M15 gold pullbacks to EMA50, conditional on EMA200 and session
VWAP, and reports a simulated 247-trade 2024 result. The reported
performance has not been audited here. Inventory accumulated near the
volume-weighted reference might support a trend-aligned pullback.
**Prediction:** completed EMA50 rejection bars in the direction jointly
specified by EMA200 and VWAP have positive next-hour signed XAUUSD returns
and outperform identical EMA pullbacks without VWAP alignment. Freeze the
source's exact rejection, session anchor and exit rules after full-text
audit and before any test. Identify whether volume is real exchange volume
or broker tick counts; these cannot be substituted silently. Require
auditable data, signal timestamps and broker fills. Distinct from T037's
unconditional slow crossover and from ORB. Reject without later-period
net profitability and incremental VWAP value on matched opportunities.
**Account constraint:** enforce initial protection and account equity
limits; the paper's simulation is not evidence of prop-firm survival.
**Status:** untested; full rule/data audit required before preregistration.

### T039 — Candle-close exit confirmation versus transient wicks

**Type:** exit/execution hypothesis; inference from the same
[Bhatti preprint](https://ssrn.com/abstract=6650958). **Confidence:** very low.

The proposed EMA trailing exit waits for candle-close confirmation.
Transient quote excursions could trigger an intrabar exit before the
underlying trend fails, but waiting can also magnify losses.
**Prediction:** for identical frozen entries, an M15-close-confirmed EMA50
profit-trailing exit improves net return per initial risk relative to the
same EMA threshold monitored intrabar, while both retain the identical hard
catastrophic stop from entry. Do not remove initial protection or claim a
closed-candle stop limits every loss. Hold entries and initial risk fixed;
report extra adverse excursion, tail loss, exit delays and equity-limit
breaches, not just win rate. Require broker ticks to reconstruct wicks,
executable stops and close-time availability. Distinct from T038's entry
prediction: this tests the observation/fill rule conditional on an unchanged
signal. Reject if gains arise only by accepting unacceptable larger loss
tails or fail costs and untouched dates. **Account constraint:** daily
equity limits apply before the confirming candle closes. **Status:** untested.

### T040 — Realized skewness and conditional intraday return tails

**Type:** distribution forecast; horizon-transfer inference. **Confidence:** very low.

[Bonato et al.](https://www.up.ac.za/media/shared/61/WP/wp_2016_45.zp91042.pdf)
find gold realized moments improve some daily/multi-day conditional return
quantiles. Intraday observations construct those predictors; the finding
is not an intraday trading result. Demand for skewed payoffs may make
distribution shape informative beyond variance.
**Prediction:** the standardized third moment of completed 09:00–11:00
New York five-minute XAUUSD returns improves the 11:00–12:00 return's
lower-tail quantile forecast beyond recent return, variance, jump share and
semivariance. Estimate only on preceding sessions, with a frozen sample-size
and outlier policy; no sign is selected after outcomes. Compare matched
quantile forecasts by forward pinball loss and coverage. Require dense
broker midpoint data and bid/ask ticks for any stop application. Distinct
from T018's squared negative returns and T032's discontinuity: the
incremental variable is third-moment asymmetry. Reject without stable
untouched-date improvement. **Account constraint:** a lower-tail forecast
must be translated into executable protection and intraday equity risk,
not assumed to guarantee a maximum loss. **Status:** untested.

### T041 — Low permutation entropy and subsequent directional predictability

**Type:** information-state hypothesis; inference. **Confidence:** very low.

[Pricing efficiency and asymmetric multifractality before and during COVID-19](https://pmc.ncbi.nlm.nih.gov/articles/PMC9293880/)
reports time variation in intraday pricing-efficiency proxies across assets
including gold. Entropy measures do not establish exploitable return signs.
Repeated ordinal patterns may reflect constrained or repetitive flow.
**Prediction:** a prespecified one-lag sign predictor has greater
out-of-sample next-five-minute XAUUSD predictive skill during low-entropy
states than high-entropy states, after controlling for volatility, clock,
spread and jump frequency. Compute one frozen permutation-entropy measure
using preceding sessions only; classify the next session before its open.
Benchmark against shuffled series with the same return distribution and
the unconditional predictor. Require broker quotes and enough independent
sessions; many bars do not create many independent crisis episodes.
Distinct from T004's volatility states: test incremental ordinal structure.
Reject if apparent predictability is microstructure noise or fails net
costs and later regimes. **Account constraint:** no stale-quote execution;
any proposed trade must fit the account's risk limits. **Status:** untested.

### T042 — Broad-dollar shock and residual gold response

**Type:** cross-asset directional hypothesis; inference. **Confidence:** very low.

[Is gold a hedge or safe haven against oil and currency movements?](https://pmc.ncbi.nlm.nih.gov/articles/PMC8566682/)
studies scale-dependent intraday dependence, not a lagged CFD strategy.
A broad dollar repricing may change the USD value of gold separately from
gold-specific demand. **Prediction:** a completed five-minute rise in a
prespecified USD currency basket predicts a negative XAUUSD return in the
next five minutes, beyond gold's contemporaneous return and prior variance.
Use synchronized live-available FX components and fixed historical weights;
do not reconstruct the basket using future fixings. Compare with
contemporaneous-only, gold-only and time-shuffled-dollar controls. Require
receipt-timestamped FX and broker bid/ask ticks; measure latency sensitivity
and exclude scheduled release windows in the first screen. Distinct from
T009's specific fixing pressure and T029's FOMC classification. Reject
if all dependence is simultaneous or net benefit disappears after realistic
receipt/fill delays. **Account constraint:** do not exploit a delayed
broker feed; only a persistent predictive relation is eligible for further
research. **Status:** untested.

### T043 — Transaction acceleration and future intraday variance

**Type:** activity-clock risk forecast; inference. **Confidence:** very low.

[Russell and Engle](https://doi.org/10.1198/073500104000000541)
model transaction durations jointly with price changes in an NYSE stock,
finding high per-second volatility at short durations. More frequent
information-bearing arrivals may raise calendar-time risk. **Prediction:**
unusually short GC intertrade durations in a completed five-minute interval
improve next-five-minute XAUUSD variance forecasts beyond gold returns,
variance, traded volume and clock. This separates transaction arrival speed
from T024's buy/sell imbalance. Require exchange trade/receipt timestamps,
roll IDs and broker ticks; batch reports and quote ticks are not trades.
Fit thresholds only on prior sessions. Compare identical gold-only and
duration-augmented forward forecasts; reject without incremental calibration.
**Account constraint:** any risk gate must count missed trades and avoid
stale-feed execution. **Status:** untested; no return direction is implied.

### T044 — Standing bid/ask depth imbalance and next gold move

**Type:** microstructure directional prediction; cross-asset inference.
**Confidence:** very low.

[Gould and Bonart](https://arxiv.org/abs/1512.03492)
find queue imbalance predicts the next midprice move in Nasdaq stocks,
especially large-tick stocks. A thin ask relative to bid may be exhausted
first. **Prediction:** the GC standing best-quote depth ratio, averaged
over a completed minute, predicts the next one-minute XAUUSD return after
controlling for taker flow, cancellation intensity and current gold return.
Distinct from T024's executed flow and T030's quote withdrawal. Require
licensed depth messages, sequence recovery, feed receipt times and broker
ticks. Test incremental forward log loss and executable signed returns;
stress delay until any advantage vanishes. Reject if only the next futures
tick is predicted with no broker after-cost horizon. **Account constraint:**
exclude delayed-price exploitation. **Status:** untested.

### T045 — Split execution versus one market order

**Type:** order-scheduling execution hypothesis; theoretical transfer.
**Confidence:** very low for retail size.

[Almgren and Chriss](https://www.smallake.kr/wp-content/uploads/2016/03/optliq.pdf)
model the trade-off between market impact and exposure while executing.
**Prediction:** at a fixed sufficiently large broker order size, splitting
an otherwise frozen XAUUSD entry into five equal marketable orders over
30 seconds reduces total implementation shortfall versus one immediate
order after including price drift, commissions and incomplete fills.
Same original signal, target exposure and exit; no child order may
exceed the original risk budget. Require broker order/fill logs and ticks;
OHLC cannot identify size impact. Compare on matched liquidity states and
include every intended order, not only completed baskets. Distinct from
spread-state entry gates: the treatment changes execution rate. Reject if
benefit exists only at irrelevant sizes or vanishes after delay risk.
**Account constraint:** child-order frequency/size rules and initial
protection still apply. **Status:** untested; no order submission authorized.

### T046 — Passive entry and adverse-selection cost

**Type:** order-type execution hypothesis; theory-based inference.
**Confidence:** very low.

[Glosten and Milgrom (1985)](https://business.columbia.edu/faculty/research/bid-ask-and-transaction-prices-specialist-market-heterogeneously-informed-traders)
show how informed flow can generate a spread. A passive order may save
spread while disproportionately filling before adverse moves.
**Prediction:** for fixed XAUUSD signals, a limit order at the current
same-side quote with a 30-second expiry has worse post-fill adverse
selection but may lower total cost per original opportunity compared with
immediate market entry. Freeze cancellation and fallback rules; measure
both predictions separately and include unfilled signals as opportunities.
Require actual broker limit-order semantics, order acknowledgements,
bid/ask ticks and fills; touching an OHLC low is not a fill guarantee.
Distinct from T045's market-order schedule. Reject the proposed passive
policy unless net opportunity P/L improves on later dates after missed
trends and fees. **Account constraint:** protective stop placement and
pending-order rules apply. **Status:** untested.

### T047 — Dealer inventory horizon and pre-break execution deterioration

**Type:** execution-liquidity hypothesis; cross-market inference.
**Confidence:** very low.

[Adrian et al. (2020)](https://doi.org/10.1016/j.finmar.2020.100564)
model overnight inventory costs and document rising Treasury trading
costs near the close. Dealers reducing overnight inventory capacity could
also affect gold quotes before a broker's daily maintenance break.
**Prediction:** conditional on volatility, spread at decision and order
size, XAUUSD slippage and rejection rates increase over the last 15
tradable minutes before the daily break. Compare identical frozen orders
with same-clock controls on historically different break schedules and
earlier matched intervals. Require broker historical hours and fill logs;
do not copy a Treasury or GC closing clock. Distinct from T021's weekly
reopen: this tests daily inventory horizon before closure. Reject if no
incremental effect or a skip fails net opportunity P/L. **Account
constraint:** obey maintenance and financing rules. **Status:** untested.

### T048 — Equity-market liquidity distress and gold execution cost

**Type:** cross-market liquidity risk forecast; inference. **Confidence:** very low.

[Does gold liquidity learn from the greenback or the equity?](https://www.sciencedirect.com/science/article/pii/S0275531916301763)
investigates cross-market liquidity and funding links. It does not establish
a broker gate. Shared intermediaries may withdraw capacity across markets
under funding stress. **Prediction:** a completed five-minute deterioration
in S&P futures depth predicts higher XAUUSD execution shortfall in the
next five minutes beyond equity returns and gold's own liquidity state.
Require synchronized depth and broker order/fill data; use actual feed
receipt times. Compare gold-only cost forecasts with equity-depth additions,
matching news and volatility. Distinct from T006's equity-return safe haven
and T030's withdrawal in gold's own book. Reject without incremental
forward cost prediction and improvement for a frozen entry policy including
skips. **Account constraint:** delayed broker updates are ineligible;
stress equity-based loss limits. **Status:** untested.

### T049 — Gold option-implied skewness and intraday direction

**Type:** option-demand directional hypothesis; horizon-transfer inference.
**Confidence:** very low.

[Commodity return predictability (2022)](https://doi.org/10.1016/j.intfin.2022.101569)
finds a positive implied-skewness/return relation across commodities using
daily and longer horizons, including gold. This is not proof for gold
alone or intraday. Asymmetric producer/consumer option demand may carry
information. **Prediction:** gold option-implied skewness available at
10:00 New York predicts the 10:00–12:00 XAUUSD return positively after
controlling for prior returns, IV level and realized skewness. Fit on
earlier dates only; compare with an otherwise identical model without
implied skewness. Require historical GC option surfaces, valid maturities,
receipt times and broker ticks. Distinct from T031's variance forecast and
T040's realized third moment. Reject without later-date incremental
directional skill and executable net benefit. **Account constraint:**
respect news exclusions and intraday equity limits. **Status:** untested.

### T050 — Oil–gold co-volatility and gold excursion risk

**Type:** cross-asset covariance risk forecast; horizon-transfer inference.
**Confidence:** very low.

[Asai, Gupta and McAleer (2019)](https://repository.up.ac.za/bitstreams/3fd33420-1e0d-46ec-9b7d-e18a4412a765/download)
study jumps and leverage in daily oil–gold co-volatility forecasts built
from intraday data. Common macro shocks may carry risk information beyond
gold-only variation. **Prediction:** oil–gold covariance from a completed
60-minute window improves next-30-minute XAUUSD extreme-excursion
forecasts after conditioning on each asset's variance, return and jumps.
Use synchronized WTI contract data with roll/negative-price handling and
broker gold ticks. Compare identical gold-only and covariance-augmented
models; do not infer an oil-to-gold direction from contemporaneous covariance.
Distinct from T032's own-market discontinuity and T048's liquidity channel.
Reject without improved forward tail coverage across ordinary and crisis
periods. **Account constraint:** any sizing application must retain fixed
initial risk and equity-limit compliance. **Status:** untested.

### T051 — Pre-existing sentiment and event-time liquidity withdrawal

**Type:** sentiment-conditioned execution risk; inference. **Confidence:** low.

[Smales and Lucey (2019)](https://doi.org/10.1016/j.intfin.2018.12.003)
find precious-metal liquidity withdrawal around monetary-policy releases
depends on sentiment and surprise size. **Prediction:** a low-sentiment
state measured before the release predicts larger XAUUSD spread/depth
deterioration during its first ten minutes, beyond the realized surprise
and prior gold variance. Sentiment may change safe-haven demand and dealer
adverse-selection exposure. Use only previously published sentiment
vintages; compare identical release dates and gold-only risk forecasts.
Require vintage indicators, event times, broker ticks and fills. Distinct
from T019's disagreement among macro forecasts and T020's fresh article
sentiment. Reject without incremental forward cost forecasting; any gate
must improve net P/L per original opportunity. **Account constraint:**
event execution rules apply even to stop exits. **Status:** untested.

### T052 — Cash-demand stress overrides equity flight to gold

**Type:** competing cross-asset directional mechanism; inference.
**Confidence:** very low.

[BIS on the March 2020 dash for cash](https://www.bis.org/speeches/20220210-learning-dash-cash-findings-and-next-steps-margining-practices)
describes selling of liquid safe assets, including gold. **Prediction:**
after equally large negative five-minute equity returns, subsequent
15-minute XAUUSD returns are lower when the dollar rises and Treasury
liquidity simultaneously deteriorates than when neither occurs.
Collateral demand may dominate safe-haven buying. This tests a funding
mechanism competing with T006, not a larger equity-shock threshold.
Freeze a joint observable stress definition from prior sessions; compare
matched equity shocks with controls for initial gold return and volatility.
Require synchronized equity/FX/Treasury depth and broker quotes. Reject
if the incremental effect is absent outside one crisis or untradeable
after receipt and costs. **Account constraint:** risk-limit simulation
must include liquidity-gap fills; avoid stale-feed exploitation.
**Status:** untested.

### T053 — Real-yield opportunity cost at an intraday horizon

**Type:** cross-asset directional transfer; inference. **Confidence:** very low.

[Chicago Fed, What Drives Gold Prices? (2021)](https://www.chicagofed.org/publications/chicago-fed-letter/2021/464)
examines expected inflation, real rates and bad-times demand, including
daily TIPS-based evidence. It does not establish intraday predictability.
**Prediction:** an observable rise in a fixed-maturity TIPS real yield
over 09:30–10:00 New York predicts negative 10:00–10:30 XAUUSD returns
incremental to nominal yield, dollar and gold moves. A non-yielding asset's
opportunity cost depends on real returns, not every nominal-rate change.
Require timestamped executable TIPS quotes, liquidity controls and broker
ticks; stale TIPS marks and future daily closes are ineligible. Compare
real-yield, nominal-only and gold-only models on identical dates.
Distinct from T010's auction-specific pressure and T029's FOMC information
classification. Reject without incremental later-date after-cost prediction.
**Account constraint:** exclude restricted news windows and stale-price
catch-up. **Status:** untested.

### T054 — Conflicting payroll components and delayed repricing

**Type:** information-complexity directional hypothesis; inference.
**Confidence:** very low.

[BLS Employment Situation](https://www.bls.gov/news.release/empsit.toc.htm)
publishes payroll, unemployment, earnings and prior-month revisions in
one release. This is a data-structure fact, not evidence of delayed gold
response. **Prediction:** when a positive headline-payroll surprise is
offset by negative prior-month revisions, minutes +5 to +20 XAUUSD returns
are more positive than on equally sized positive-headline surprises
without negative revisions, controlling for initial gold/yield moves and
earnings. Complexity may slow incorporation of the total employment change.
Freeze component standardization on archived real-time vintages and survey
availability before events. Require first-release tables, delivery times,
surveys and broker ticks. Distinct from T001's price-sign following:
this predicts incremental correction from conflicting public components.
Reject without later-event incremental prediction and net benefit.
**Account constraint:** news policy must permit any proposed translation.
**Status:** untested; actual release archives must be audited first.

### T055 — Attention volume and unsigned jump hazard

**Type:** attention-driven risk forecast; inference. **Confidence:** very low.

[Sobti (2025)](https://doi.org/10.1016/j.irfa.2025.104380)
finds gold jump information in market-psych attention measures. Increased
attention can synchronize trading even without directional text.
**Prediction:** the count of distinct gold news items actually delivered
over a completed five-minute interval improves next-five-minute XAUUSD
jump-probability forecasts beyond sentiment, previous returns, volatility
and calendar effects. Deduplicate syndicated/repeated articles and
normalize counts using preceding same-clock sessions. Compare with a
sentiment-only model and time-shuffled counts. Require licensed
point-in-time news, receipt logs and broker ticks. Distinct from T020's
negative wording: the primary outcome is unsigned hazard conditional on
the same sentiment. Reject without incremental untouched-date calibration;
no automatic long/short follows. **Account constraint:** news-gap rules
and lost opportunities must enter any later gate evaluation. **Status:** untested.

### T056 — Self-exciting gold-jump clusters

**Type:** event-arrival risk forecast; inference. **Confidence:** very low.

[Buccioli and Kokholm, Shock waves and golden shores](https://doi.org/10.1080/1351847X.2021.1897026)
model mutually exciting jumps using **daily closing gold and equity
prices**; it is not an intraday empirical finding or an established broker
strategy. Feedback trading may cause one
discontinuous move to raise the chance of another. **Prediction:** a
decay-weighted count of previously detected XAUUSD jumps improves
next-15-minute jump hazard beyond total variance, jump magnitude and clock.
Identify events online using past-only thresholds; compare a fixed-decay
intensity model with independent arrivals and recent-variance benchmarks.
Require broker ticks and enough independent episodes; do not label jumps
using a full-day variance estimate unavailable at decision time. Distinct
from T032's jump share: test arrival clustering after holding magnitude
constant. Reject without forward hazard improvement across regimes.
**Account constraint:** gates must include actual stop gaps and intraday
equity excursions. **Status:** untested.

### T057 — Futures settlement-window liquidity concentration

**Type:** benchmark-conditioned execution hypothesis; inference.
**Confidence:** very low.

[CME's gold settlement procedure](https://www.cmegroup.com/trading/metals/files/daily-settlement-procedure-gold-futures.pdf)
uses a defined trading window to determine the active-contract settlement.
Benchmark-targeting flow may attract liquidity at that window.
**Prediction:** XAUUSD orders during the historically applicable GC
settlement window have lower implementation shortfall than matched
orders immediately before and after, conditional on volatility and size.
Audit procedure versions: the [2010 notice](https://www.cmegroup.com/tools-information/lookups/advisories/market-regulation/SER-5147.html)
specifies 13:29–13:30 New York, while [newer client documentation](https://cmegroupclientsite.atlassian.net/wiki/spaces/EPICSANDBOX/pages/457088147/Gold)
displays
13:25–13:30. Do not impose today's clock on old samples. Require dated
procedures, contract calendars and broker fills/ticks. Distinct from
T014's London auction and H6's directional close momentum. Compare frozen
signals and all original opportunities; reject without broker cost
improvement. **Account constraint:** never use a final settlement before
publication or assume the CFD settles with GC. **Status:** untested;
effective-date mapping unresolved.

### T058 — Futures interruption and broker stop-fill risk

**Type:** market-design execution risk; inference. **Confidence:** very low.

[CME price-limit documentation](https://www.cmegroup.com/trading/price-limits.html)
and the [GC contract rules](https://www.cmegroup.com/rulebook/COMEX/1a/113.pdf)
describe exchange fluctuation controls. A key hedging venue's interruption
could reduce spot liquidity even if the CFD remains open.
**Prediction:** after confirmed GC limit/velocity interruptions, next-five-
minute XAUUSD rejection rates and stop slippage exceed matched high-volatility
intervals without interruption. Use actual exchange status messages and
historically effective rules; proximity to a displayed limit is not a halt.
Require status/receipt logs and broker ticks/fills. Control for jump size,
news and pre-event spread. Distinct from T017's ordinary post-jump spread
recovery: the treatment is loss of access to a hedging venue. Reject if no
incremental forward execution deterioration. **Account constraint:** broker
hours, exceptional-market execution rules and gap-sensitive equity limits
must be modeled. **Status:** untested; no assumption of CFD trade access.

### T059 — Physical gold ETF premium and underlying purchase pressure

**Type:** intermediation-demand directional hypothesis; inference.
**Confidence:** very low.

The [GLD prospectus](https://files.spdrgoldshares.com/spdr-cms/2026-01/GLD-Sing-Prospectus-2025-09-05-Corrected.pdf.pdf)
describes basket creation/redemption against gold; [BIS Box C](https://www.bis.org/publications/qr-202603/markets-recalibrate-amid-shifting-currents)
discusses persistent precious-metal ETF premia during strong demand.
**Prediction:** a sustained positive GLD premium to synchronized indicative
gold value during 10:00–10:15 New York predicts positive 10:15–11:00 XAUUSD
returns beyond initial gold/GLD returns. Authorized-participant inventory
adjustment may transmit demand to bullion. Require live-available indicative
value or a correctly reconstructed gold-per-share value, ETF quote receipt
times, basket terms and broker ticks. Compare matched return states and
time-shuffled premia; do not use the final NAV before publication.
Distinct from T025's leveraged funds and T035's Chinese physical premium.
Reject without persistent prediction after latency/costs. **Account
constraint:** no delayed-feed convergence strategy. **Status:** untested.

### T060 — Stressed London–New York basis and broker execution fragility

**Type:** physical-market segmentation risk forecast; inference.
**Confidence:** very low.

[WGC's gold deposit-rate paper](https://www.gold.org/goldhub/research/gold-deposit-rates-guidance-paper)
describes 2020 delivery friction when transport and refining disrupted
COMEX bar supply. **Prediction:** an unusually large carry-adjusted GC–London
spot basis observed at 09:00 New York predicts higher XAUUSD slippage and
spread over 09:00–10:00, beyond spot variance and GC liquidity. Frictions
in converting bar location/size may weaken hedging capacity. Require
synchronized actual spot and contract quotes, financing inputs, days to
delivery and broker fills; a continuous back-adjusted futures series cannot
measure this basis. Compare basis-neutral dates matched on initial risk;
reject without incremental forward cost prediction. Distinct from T058's
trading interruption and T035's Shanghai premium. **Account constraint:**
this is a risk state, not a cross-feed spot catch-up trade. **Status:** untested.

### T061 — Gold borrowing tightness and intraday short-side execution

**Type:** financing/borrow-risk hypothesis; inference. **Confidence:** very low.

[LBMA, Gold—A New Lease of Life?](https://www.lbma.org.uk/alchemist/issue-54/gold-a-new-lease-of-life)
warns that high lease rates do not reliably identify gold price direction.
Borrow scarcity may still constrain intermediaries' short-side capacity.
**Prediction:** high point-in-time short-tenor gold lease-rate states
predict a larger buy-versus-sell execution-cost asymmetry over the next
liquid intraday hour after conditioning on volatility, basis and order size.
The sign of that cost difference is a two-sided primary test; no automatic
bullish price claim. Require licensed historical borrowing quotes with
availability times and paired broker fills. Compare with financing-normal
states on matching clocks. Distinct from T060's geographical delivery
basis; test incremental borrowing tightness. Reject without stable forward
cost asymmetry. **Account constraint:** actual CFD financing and execution
terms must be measured rather than copied from bullion lending.
**Status:** untested.

### T062 — Annual commodity-index weight resets and intraday gold flow

**Type:** mandated-flow directional hypothesis; inference. **Confidence:** very low.

[Bloomberg Commodity Index methodology](https://data.bloomberglp.com/professional/sites/10/BCOM-Methodology.pdf)
describes annual rebalancing and commodity target weights. Restoring target
weights can require selling a commodity after relative appreciation,
independent of new fundamentals. **Prediction:** the gold-specific signed
rebalance quantity estimated from published target weights, pre-event
weights and prior disclosed tracking AUM predicts same-sign XAUUSD returns
in the specified daily implementation window. Lock the window from the
historically effective methodology, not observed price moves. Compare
with matching January non-rebalance windows and placebo quantities;
control for ordinary roll flow, prior returns and news. Require vintage
methodologies/AUM, GC contracts and broker ticks. Distinct from T034's
contract-roll liquidity and T025's daily leverage reset. Reject without
incremental later-event net benefit; few annual events limit inference.
**Account constraint:** no retroactive end-of-day weight information or
equity-limit breach. **Status:** untested.

### T063 — Official gold-reserve disclosure surprise

**Type:** demand-information directional hypothesis; inference.
**Confidence:** very low.

China's [official reserve release](https://www.safe.gov.cn/safe/file/file/20220811/5078a079430643b4a7382787a6446db4.pdf)
reports monetary gold. Disclosure of previously unknown official buying
could revise expectations of persistent demand. **Prediction:** the
unexpected change in reported gold **quantity**, measured against a
pre-release frozen forecast, positively predicts XAUUSD returns over
minutes +5 to +35 after public delivery. Dollar-value changes caused by
valuation are not purchases. Require quantity vintages, actual release and
receipt times, prior public guidance and broker ticks; if disclosures
occur while the broker is closed, exclude rather than capture a fictional
entry. Control for simultaneous reserve/FX announcements and initial gold
returns. Distinct from T020's textual sentiment: the signal is a quantity
surprise. Reject without multiple-event incremental prediction and net
benefit. **Account constraint:** unscheduled-news rules and publication
access restrictions must be respected. **Status:** untested.

### T064 — Published auction imbalance and residual benchmark demand

**Type:** auction order-flow directional hypothesis; inference.
**Confidence:** very low.

[LBMA's auction FAQ](https://www.lbma.org.uk/prices-and-data/lbma-gold-price/lbma-gold-price)
says end-of-round aggregate bid/offer volumes are distributed during the
auction. Unbalanced benchmark demand may persist across rounds.
**Prediction:** the signed imbalance of a completed non-final PM round
predicts same-sign XAUUSD return over the next 60 seconds after actual
data receipt, beyond the gold move already completed. Require licensed
round feeds, receipt logs, historical thresholds and broker ticks.
Compare round-price-move-only and randomized-imbalance controls; auction
algorithm price adjustment is not itself a broker trading profit.
Distinct from T014's first-minute price signal and T024's futures taker
flow: this observes benchmark-specific demand directly. Reject if all
adjustment precedes receipt or fails costs. **Account constraint:** no
delayed-price exploitation; only lawfully accessible round information
may enter. **Status:** untested.

### T065 — Prolonged auction and uncertainty about completion

**Type:** auction-conditioned execution risk; inference. **Confidence:** very low.

[ICE's methodology](https://www.ice.com/iba/lbma-precious-metals)
runs repeated rounds and has no fixed benchmark publication time.
Unresolved auction demand may prolong price uncertainty.
**Prediction:** conditional on the initial move and spread, an auction
still running after five minutes has higher next-five-minute XAUUSD
execution shortfall than completed auctions at the same elapsed time.
Use only completion status actually received by that decision; do not
label a live auction by its eventual duration. Require real-time status,
round timestamps and broker fills. Compare fixed signals across matched
auction states and same-clock non-auction placebo windows. Distinct from
T064's imbalance direction and T017's generic post-jump gate. Reject
without incremental forward cost prediction and improved opportunity
P/L for a frozen wait rule. **Account constraint:** final benchmark
values cannot be used before publication; account loss limits still
apply while waiting. **Status:** untested.

### T066 — Crypto deleveraging and gold tail-risk transmission

**Type:** cross-market risk hypothesis; inference. **Confidence:** very low.

[Higher-order moment nexus of dollar, oil, gold and bitcoin](https://orca.cardiff.ac.uk/id/eprint/167621/)
reports linked realized moments using intraday inputs. It does not prove
minute-scale crypto-to-gold causation. Shared leveraged investors could
transmit liquidation stress. **Prediction:** a completed burst of verified
BTC forced liquidations improves next-30-minute XAUUSD downside-excursion
forecasts beyond BTC returns/variance, dollar moves and gold's own state.
Require exchange liquidation messages with receipt times and broker ticks;
reported exchange coverage may change. Compare with equally large BTC
returns without liquidation bursts and gold-only forecasts. Distinct from
T050's oil covariance: the proposed channel is leveraged forced sales.
Reject without incremental forward calibration outside one crash episode.
**Account constraint:** no weekend broker fills or stale-price catch-up;
model intraday equity gaps. **Status:** untested.

### T067 — Geopolitical threat state and gold jump sensitivity

**Type:** geopolitical risk-conditioned hazard; horizon-transfer inference.
**Confidence:** very low.

[Huang et al.](https://doi.org/10.1080/10242694.2021.2007333)
find geopolitical risk related more strongly to gold volatility/jumps than
returns using daily measures built from high-frequency data. Threat-driven
precautionary demand may alter sensitivity to new information.
**Prediction:** a previously published geopolitical-threat index improves
the next session's intraday five-minute jump hazards beyond EPU, gold risk
and the scheduled calendar, without assuming a gold-long direction.
Use archived vintages and publication lags; a monthly final index cannot
label its earlier intraday bars. Compare EPU-only and threat-augmented
forecasts, with time-of-day matching and episode-blocked inference.
Distinct from T033's domestic economic-policy uncertainty only if the
geopolitical variable adds independent information. Reject otherwise.
Require index release records and broker ticks. **Account constraint:**
any gate must retain stop-gap and daily equity-risk accounting.
**Status:** untested.

### T068 — Persistent order splitting beyond net volume imbalance

**Type:** microstructure directional hypothesis; theoretical transfer.
**Confidence:** very low.

[Lillo, Mike and Farmer](https://doi.org/10.1103/PhysRevE.71.066122)
show how split metaorders can generate persistent trade signs.
Persistence of flow need not imply predictable prices if liquidity adapts.
**Prediction:** holding five-minute GC net taker volume constant, more
persistent same-sign runs predict stronger same-sign XAUUSD returns over
the next five minutes. Compute a frozen run statistic from completed
exchange trades and compare with run-shuffled sequences preserving total
volume/sign counts. Require trade/receipt timestamps, reliable aggressor
classification and broker ticks. Distinct from T024's total imbalance:
the incremental input is sequencing, representing unfinished parent flow.
Reject if persistence is absorbed by liquidity or vanishes after receipt,
costs and untouched dates. **Account constraint:** no external slow-feed
arbitrage. **Status:** untested.

### T069 — Repeated trade sizes and informational price impact

**Type:** stealth-flow directional hypothesis; cross-asset inference.
**Confidence:** very low.

[Chen (2019)](https://doi.org/10.1016/j.jbusres.2019.04.032)
associates stock trade-size clustering with lower temporary and greater
permanent impact. Repeated execution sizes may reveal informed algorithmic
flow. **Prediction:** GC same-side trade-size clustering predicts larger
next-five-minute signed XAUUSD returns than size-diverse flow with the
same net volume, trade count and run persistence. Freeze concentration
measurement and size-bin rules before results; compare with sizes shuffled
within the completed interval. Require unbundled exchange trades,
receipt times and broker quotes. Distinct from T068's sign sequencing and
T015's round **prices**. Reject if reporting aggregation explains the
effect or no executable incremental prediction survives.
**Account constraint:** no delayed-feed exploitation; actual broker costs
and risk limits govern translation. **Status:** untested.

### T070 — Hidden-liquidity replenishment and failed price pressure

**Type:** absorption/reversal hypothesis; cross-asset inference.
**Confidence:** very low.

[Who can see the iceberg's peak?](https://doi.org/10.1111/jfir.12414)
examines iceberg use by informed and liquidity traders in equities.
Hidden size is not automatically an informed signal. **Prediction:**
when GC aggressive buys repeatedly execute against independently confirmed
ask replenishment with little price progress, the next five-minute
XAUUSD return is lower than for equal buy volume without replenishment.
Freeze the detection rule; infer only native refreshes supported by
message semantics and audit detection errors. Require MBO messages,
sequence handling, receipt times and broker ticks. Compare matched
imbalance, depth, volatility and news states. Distinct from T044's
displayed snapshot and T030's withdrawal: this tests latent supply.
Reject without forward incremental reversal and net benefit.
**Account constraint:** no spoofing, fictitious liquidity assumption or
slow-feed catch-up. **Status:** untested.

### T071 — Aggressive multi-level sweeps and transient overshoot

**Type:** liquidity-shock directional hypothesis; cross-asset inference.
**Confidence:** very low.

[Xu et al.](https://arxiv.org/abs/1602.00731)
find stronger price resiliency after aggressive stock market orders,
versus more continuation after less-aggressive orders. **Prediction:**
after GC sweeps consuming several price levels, next-five-minute XAUUSD
returns oppose the sweep more than after equal-size, equal-return
single-level executions. A concentrated liquidity shock may overshoot
the persistent information value. Require atomic trade-event reconstruction,
depth updates, receipt logs and broker ticks; equal timestamps alone
cannot reliably group an event. Compare matched jump size, net flow,
spread and news; exclude scheduled releases initially. Distinct from
T002/H9's return-size fades: the conditioning variable is aggressiveness
at fixed return size. Reject without incremental after-cost reversal.
**Account constraint:** no assumed fills during price gaps or delayed
broker response. **Status:** untested.

### T072 — Observable dealer inventory and subsequent quote skew

**Type:** inventory-pressure directional hypothesis; theoretical inference.
**Confidence:** very low; proprietary data needed.

[Avellaneda and Stoikov (2008)](https://math.nyu.edu/inmemoriam/avellaneda/HighFrequencyTrading.pdf)
model inventory-dependent dealer reservation prices. **Prediction:** a
bullion dealer's independently observed net-long inventory predicts a
negative component of its subsequent 15-minute executable gold quote
changes relative to a synchronized composite, controlling for customer-flow
information and market returns. Inventory aversion may induce temporary
discounting. Require consensually supplied dealer inventory/customer-flow
and quote data; public GC net taker volume is not dealer inventory.
Compare inventory-neutral periods with matched public market states and
test whether any quote skew predicts later broker midpoint reversal.
Distinct from T047's daily inventory horizon and T070's hidden orders.
Reject without true inventory data or forward effect. **Account constraint:**
do not exploit stale quotes or confidential information without authorized
access. **Status:** untested; inaccessible-data risk high.

### T073 — Missing London bullion participation on local holidays

**Type:** participation-conditioned execution hypothesis; inference.
**Confidence:** very low.

[ICE's benchmark calendar](https://www.ice.com/iba/lbma-precious-metals)
documents non-publication days. Absence of a major bullion center may
reduce the liquidity supporting a still-open broker market.
**Prediction:** on UK bullion holidays when the broker and relevant
overseas venues remain open, 08:00–12:00 London XAUUSD execution shortfall
is higher than matched non-holiday weekdays after controlling for volatility
and news. Verify actual desk/venue availability; a missing auction alone
does not prove all London trading is closed. Require historical calendars
and broker fills/ticks. Compare same-weekday/same-season controls and
report all opportunities for a fixed holiday gate. Distinct from T008's
ordinary session overlap and T021's weekly reopen. Reject without an
incremental cost effect and net opportunity benefit. **Account constraint:**
broker-specific holiday hours and equity limits apply. **Status:** untested.

### T074 — Opening-range boundary continuation family

**Type:** directional; canonical legacy consolidation. **Confidence:** rejected
for the tested broker configurations.

[RIP Backtest](https://ripbacktest.com/posts/gold-opening-range-breakout/)
reports failed gold ORB robustness after costs. The proposed mechanism was
that early-session price discovery establishes a range whose breach attracts
persistent flow. **Prediction:** the first confirmed breach predicts
same-direction intraday returns sufficient to cover execution costs.
This entry consolidates H1/H2/H4/H5 and EXP-001/003/004/005/006/007/014;
range lengths, RSI, width filters and retests are variants, not separate
counts. [[Research Hypotheses]] contains their frozen rules and outcomes.
EXP-001 lost 74.74% (PF 0.70); EXP-003 lost 63.67% (PF 0.69); EXP-004
lost 92.22% (PF 0.55). The listed filters/entry variants also failed.
Require broker tick execution, correct local session clocks and matched
random-anchor breakouts for any independent future research. Reject on
nonpositive net expectancy or failure of untouched-date robustness; do not
tune the exposed family. **Account constraint:** protection and intraday
equity limits. **Status:** tested variants rejected; no new experiment.

### T075 — Rest-of-day to COMEX-close momentum

**Type:** directional; canonical H6/EXP-008/009 record. **Confidence:**
rejected on the tested XAUUSD mapping.

[Baltussen et al.](https://doi.org/10.1016/j.jfineco.2021.04.029)
report cross-futures close momentum with hedging-demand evidence, excluding
trading costs. **Prediction:** the sign of XAUUSD's prior COMEX close to
12:59 New York return predicts its 13:00–13:30 return. This tests accumulated
day-position adjustment, distinct from T023's midday half-hour predictor.
Corrected EXP-008 returned -55.72% ROI, PF 0.27 over 1,626 trades; gross
results were negative in every year and both directions. EXP-009 reproduced
the ledger and found momentum worse than matched long, short and random
controls. See [[Research Hypotheses#H6 — COMEX-close intraday momentum]].
Exact session completeness, weekend state and executable bid/ask data are
essential; the first invalid run is not the final result. Rejection gates
already failed. **Account constraint:** an unprotected time-exit simulation
does not establish equity-limit survival. **Status:** rejected; no
reversal or clock tuning authorized.

### T076 — Unconditional day-versus-overnight gold drift

**Type:** session risk-premium directional hypothesis; canonical legacy record.
**Confidence:** tested translations rejected.

[Blose and Gondhalekar](https://scholarworks.gvsu.edu/fsdg/975/)
report historical overnight returns greater than daytime returns; the
[2018 follow-up](https://doi.org/10.1007/s12197-017-9403-0)
examines gold-related assets. Regional risk-bearing/demand differences
could produce a clock-time return premium. **Prediction:** a fixed COMEX
daytime short and/or broker post-reopen-to-morning long has positive net
expectancy. These are two legs of one session-asymmetry mechanism and count
once: H7/EXP-011 and EXP-016. EXP-011 short lost 44.04%, PF 0.82; even
removing its fixed spread left a small net loss. EXP-016's locked
18:05–08:20 weekday long lost $289.5016/oz aggregate, PF 0.9296, across
1,235 development trades. See [[Overnight Gold Drift - preregistered 2026-11-03]]
and [[Research Hypotheses#H7 — Negative COMEX daytime return]].
Require historical broker hours, fees and financing; no shifting clocks
after failure. **Account constraint:** rollover/weekend and equity limits.
**Status:** rejected in tested configurations; confirmation remains unopened
where the legacy note specifies it.

### T077 — Asian replenishment after a negative U.S. gold session

**Type:** conditional intersession reversal; canonical EXP-015 record.
**Confidence:** development rejected.

[WGC's H1 2026 outlook](https://www.gold.org/goldhub/research/gold-mid-year-outlook-2026)
describes U.S. pullbacks and Asian rebounds in that half-year. The transfer
mechanism was regional dip-buying/replenishment, not an established cause.
**Prediction:** after a negative 08:20–16:00 New York gold session,
a 20:00–02:00 long beats unconditional Asian exposure after costs.
Distinct from T002's same-open extreme-move fade and T076's unconditional
session premium. The locked [[Asia Rebound Screen - preregistered 2026-11-03]]
failed on independent M5 bid/ask data: 588 development trades, -$0.6204/oz
mean net P/L and PF 0.7152. Actual ticks are still needed for definitive
broker stop sequencing; the negative screen is not proof for every broker.
Its declared development gate failed, so 2024–2025 confirmation was not
opened. **Account constraint:** historical rollover, protective stop fills
and intraday equity limits. **Status:** rejected screen; no conditional
threshold, hours or stop retuning authorized.

### T078 — Futures-to-spot price-discovery catch-up

**Type:** cross-venue convergence; canonical scan-B record. **Confidence:**
historical leadership evidence, unproven execution and unsuitable for FTMO.

[Hauptfleisch, Putniņš and Lucey](https://www.research.ed.ac.uk/files/82358957/GoldILS_JFutMkt_Forthcoming.pdf)
find New York futures often lead London spot information incorporation.
**Prediction:** after a synchronized futures move not yet reflected in
spot, XAUUSD catches up within five minutes strongly enough to cover
spread, slippage and delivery delay. Price leadership does not prove an
executable lag. The frozen candidate is recorded in [[Intraday Thesis Scan - 2026-11-03]].
Require licensed bid/ask feeds, clock-error bounds, receipt logs, roll IDs
and delay placebos; reject if plausible latency removes the return.
Distinct from T024's completed flow forecasting a later non-overlapping
interval: this explicitly targets a contemporaneous pricing discrepancy.
**Account constraint:** the vault's documented FTMO path excludes delayed-
price/external-slow-feed exploitation; this hypothesis is retained for
research provenance, not as a permitted trading plan. **Status:** untested
economically; excluded from that account path.

### T079 — Failed range breakout and trapped-position liquidation

**Type:** directional reversal; canonical H3/EXP-002 record.
**Confidence:** severely negative partial run with unresolved execution.

[Open Market Journal](https://openmarketjournal.com/research/gold-opening-range-breakout)
reports range re-entry path frequencies; those are not profitable trade
win rates. Failed acceptance outside the range may force breakout traders
to unwind toward the opposite boundary. **Prediction:** after the first
London range breach closes back inside, a next-bar fade has positive net
expectancy. The frozen M1-close rule and opposite-boundary target are in
[[../03 - Implementation/cTrader cBot - London Failed Breakout]].
Corrected EXP-002 had zero protection-modification failures but depleted
the account before the requested end date: 828 closed trades, -100.01%
ROI, PF 0.48; stop/risk execution remains unresolved. It is an incomplete,
negative run, not a clean full-period test. Require ticks and planned-
versus-actual stop audit. Distinct from T074's continuation and T071's
liquidity-sweep conditioning. **Account constraint:** account depletion and
excess planned-risk losses prevent promotion. **Status:** negative partial
run; no retuning authorized.

### T080 — VWAP deviation and inventory mean reversion

**Type:** directional; source-reported negative strategy. **Confidence:** very low.

[RIP's gold VWAP-band study](https://ripbacktest.com/posts/vwap-bands-mean-reversion-strategy-gold/)
reports negative net results and weak placebo comparisons for fading
two-standard-deviation deviations. The hypothesized mechanism is inventory
pressure reverting toward the session's volume-weighted transaction
reference. **Prediction:** a completed M5 close between two and three
standard deviations from session VWAP predicts an executable move toward
VWAP before its frozen stop/time exit. Source London/New York results
both averaged about -$0.26/oz after assumed $0.30 costs; these are its
results, not a project replication. Require broker bid/ask ticks and an
explicit tick-volume-versus-trade-volume definition. Compare matched random
session anchors and same-time opposite trades. Distinct from T038's
trend-aligned pullback; this bets against an extended inventory deviation.
Reject without incremental net expectancy on untouched data. **Account
constraint:** stop distances, actual fill costs and equity limits.
**Status:** source-negative; untested here, low priority.

### T081 — Actual stop-order concentration and cascade hazard

**Type:** endogenous forced-flow directional hypothesis; FX-to-gold inference.
**Confidence:** very low; proprietary data needed.

[Osler's dealer-order research](https://www.newyorkfed.org/research/staff_reports/sr125.html)
distinguishes trend-intensifying stops from trend-reflecting take-profits.
**Prediction:** a documented concentration of pending gold stop orders
crossed by the broker price predicts a larger same-direction next-five-
minute move than matched crossings without stops, controlling for roundness,
depth, return size and take-profit quantity. Require authorized pre-cross
dealer/broker order-book snapshots with order types; chart highs and round
levels cannot stand in for actual stop inventory. Compare volume-matched
ordinary market-order crossings. Distinct from T015's price-grid proxy:
the explanatory variable is measured forced-order inventory, not another
round-number rule. Reject without incremental post-receipt net prediction.
**Account constraint:** lawful data access, no manipulation or stale-price
exploitation. **Status:** untested; public candles cannot test the mechanism.

### T082 — Slow volatility memory beyond the current intraday state

**Type:** multi-timescale risk forecast; inference. **Confidence:** low.

[Cai, Cheung and Wong](https://doi.org/10.1002/1096-9934%28200103%2921%3A3%3C257%3A%3AAID-FUT4%3E3.0.CO%3B2-W)
find long-memory gold volatility dependencies after removing intraday
patterns. Persistent heterogeneous investment horizons may carry risk
across sessions. **Prediction:** prior-day and prior-week realized variance
improve next-30-minute XAUUSD variance forecasts beyond the current
60-minute variance, clock, signed semivariance and jump share. Compare a
fixed HAR-style multi-horizon model with the same intraday-only model;
estimate on earlier sessions and evaluate forward loss and coverage.
Require continuous broker quotes across prior sessions, exact closure
handling and past-only normalization. Distinct from T028's within-day
block location and T007's deterministic clock profile. Reject without
incremental accuracy after the current risk state is controlled.
**Account constraint:** any sizing use preserves the risk budget and
equity-limit model; forecasting volatility alone creates no directional
expectancy. **Status:** untested.

### T083 — Cross-venue cojump identifies a persistent information shock

**Type:** common-information directional hypothesis; inference.
**Confidence:** very low.

[Sobti (2025)](https://doi.org/10.1016/j.irfa.2025.104380)
studies gold futures/ETF jumps and cojumps. A synchronous multi-venue move
may be more informative than an isolated liquidity shock.
**Prediction:** after equal-size completed XAUUSD jumps, next-five-minute
signed returns show more continuation when GC and GLD also jumped in
the same completed interval than when neither did. Condition on news,
own jump size, all venues' liquidity and receipt lag. Require synchronized
licensed quotes and independent jump detection; do not label events from
future intervals. Compare against own-jump-only and time-shifted-venue
controls. Distinct from T032's own discontinuity share and T078's price
discrepancy: gold has already moved on every venue before entry.
Reject without incremental later-date executable continuation.
**Account constraint:** no slow-feed catch-up and no assumed fills during
news restrictions. **Status:** untested.

### T084 — Carry-curve state and intraday gold risk premium

**Type:** slow-state directional forecast; horizon-transfer inference.
**Confidence:** very low.

[Koijen et al., Carry](https://www.nber.org/papers/w19325.pdf)
find carry predicts returns across asset classes including commodities;
it is not gold-specific intraday evidence. Persistent compensation for
risk bearing may accrue partly during a liquid intraday window.
**Prediction:** a pre-open carry measure from fixed, actual GC maturities
positively predicts the 08:20–13:30 New York XAUUSD return after controls
for financing, lease rate, past gold returns and macro state. Freeze the
carry sign convention explicitly; contango is not automatically high
positive carry. Require dated contract prices, maturities and broker ticks.
Compare return forecasts with/without carry on identical days, excluding
exceptional delivery dislocations. Distinct from T060's abnormal basis
liquidity risk and T076's unconditional clock drift. Reject without
incremental later-date net benefit. **Account constraint:** broker swaps
are separate from futures carry; equity limits apply. **Status:** untested.

### T085 — Producer hedging pressure and intraday compensation

**Type:** risk-transfer directional hypothesis; contested, horizon-transfer
inference. **Confidence:** very low.

[Gorton, Hayashi and Rouwenhorst](https://www.nber.org/papers/w13249)
reject participant positions as important risk-premium determinants in
their broad commodity study after inventory signals; this is contrary
evidence. Producers' short hedging nevertheless motivates a falsifiable
low-priority gold question. **Prediction:** the latest published net
producer/merchant short position positively predicts the next eligible
08:20–13:30 XAUUSD return after controlling for carry, inventories,
momentum and variance. Require vintage CFTC category data and **release**
times, not Tuesday-position timestamps as if public then. Compare models
with/without positions on matched dates; classification mixes physical
businesses and OTC exposures. Distinct from T026's margin-driven forced
liquidation: this tests compensation to voluntary risk absorption.
Reject without incremental untouched-date net benefit. **Account
constraint:** correct public-data timing and intraday equity limits.
**Status:** untested; source evidence lowers priority.

### T086 — Month-end multi-asset portfolio rebalancing into gold

**Type:** allocation-flow directional hypothesis; cross-asset inference.
**Confidence:** very low.

[Ogden's standardized-payment hypothesis](https://doi.org/10.1111/j.1540-6261.1990.tb02435.x)
links month-turn stock returns to liquidity flows; it does not establish
gold rebalancing. Fixed-weight multi-asset portfolios could buy gold
after its weight falls relative to equities and bonds.
**Prediction:** a gold rebalance-demand estimate from one fixed public
portfolio's start-month weights and returns observed by 14:00 New York
predicts same-sign 14:00–16:00 XAUUSD returns on month-end days.
Compare identical demand estimates on non-month-end placebos; control
for gold's own return, FX fixes and scheduled news. Require fixed
allocation mandates and point-in-time prices; no final closing weights.
Distinct from T062's commodity-index annual mandates and T025's leverage
reset. Reject unless incremental net performance survives independent
month ends. **Account constraint:** news windows and equity limits;
inferred demand is not an observed order. **Status:** untested.

### T087 — Unrealized investor gains and underreaction to gold news

**Type:** behavioral directional hypothesis; equity-to-gold inference.
**Confidence:** very low; ownership data needed.

[Frazzini (2006)](https://people.stern.nyu.edu/afrazzin/pdf/The%20Disposition%20Effect%20and%20Underreaction%20to%20news%20-%20Frazzini.pdf)
links disposition behavior with equity news underreaction. Selling
winning holdings may absorb positive news and delay adjustment.
**Prediction:** after equally positive gold-specific news, the +5 to
+35-minute XAUUSD signed return is larger when independently measured
holder unrealized gains are high, conditional on initial returns and
sentiment. Require authorized holder cost-basis/position vintages,
news receipt times and broker ticks; a recent-high distance alone cannot
prove investor gains. Compare low-gain matched news events and
sentiment-only forecasts. Distinct from T020's unconditional negative
wording and T072's dealer risk inventory. Reject without incremental
forward net prediction or credible holder data. **Account constraint:**
news execution rules and lawful position-data access. **Status:** untested.

### T088 — Bid–ask bounce creates non-executable apparent reversal

**Type:** microstructure execution null; inference. **Confidence:** moderate
mechanism, untested magnitude on this feed.

[Roll (1984)](https://doi.org/10.1111/j.1540-6261.1984.tb03897.x)
shows trading costs can induce negative serial dependence in transaction
price changes without predictable fundamental value. **Prediction:**
one-trade reversal measured on GC transaction prints is materially weaker
on synchronized midquotes and fails to yield positive next-quote XAUUSD
net P/L. This is a falsifiable explanation for apparent scalping alpha,
not a proposed profitable fade. Compare trade-price and midpoint predictors
at identical times, controlling for quote changes, spread and trade sign.
Require original exchange trades/quotes and broker ticks; a bid-only CFD
bar is not an alternating transaction series. Distinct from T071's
economic overshoot: bounce needs no genuine value overshoot. Reject this
explanation if comparable midpoint reversal survives costs and controls.
**Account constraint:** actual bid/ask fills and no stale-feed execution.
**Status:** untested; execution-null thesis.

### T089 — Option term-structure inversion and immediate event risk

**Type:** forward uncertainty-horizon risk forecast; inference.
**Confidence:** very low.

[Implied volatility surface predictability in commodities](https://arxiv.org/abs/1909.11009)
finds value in modeling IV term structure for commodity-option forecasts,
including precious metals. This does not prove spot intraday risk
forecasting. Concentrated near-term event uncertainty may lift short
expiry IV above longer expiry IV. **Prediction:** a pre-10:00 New York
GC option term-structure inversion improves 10:00–11:00 XAUUSD jump
hazard beyond IV level, skew, own variance and the known event calendar.
Require synchronized option surfaces, maturity/forward adjustments and
broker ticks; do not compare different moneyness or stale expiries.
Compare level-only and slope-augmented forward forecasts. Distinct from
T031's overall IV level: test the concentration of risk across horizons.
Reject without incremental tail calibration. **Account constraint:** any
gate must count skipped opportunities and news/equity restrictions.
**Status:** untested.

### T090 — Upside variance risk premium and intraday compensation

**Type:** option-risk-premium directional hypothesis; horizon-transfer inference.
**Confidence:** very low.

[Indriawan et al. (2020)](https://doi.org/10.1080/00036846.2020.1735619)
find precious-metal predictability in upside variance risk premia,
using ETF options at longer horizons. Compensation for upside gold
insurance may differ from expected physical variance.
**Prediction:** an upside implied-minus-forecast-realized variance premium
available before 10:00 New York improves 10:00–12:00 XAUUSD signed-return
forecasts beyond IV level, skew, carry and recent returns. Fit the sign
on training dates and freeze it before forward evaluation; do not assume
the source's commodity portfolio result proves a gold slope. Require
option surfaces and a past-only physical-variance forecast at the same
horizon, plus broker ticks. Compare with/without the premium.
Distinct from T031's risk-level forecast and T049's skewness: the
mechanism is insurance compensation. Reject without incremental later-date
net benefit. **Account constraint:** news and equity limits. **Status:** untested.

### T091 — Pre-FOMC risk-bearing drift in gold

**Type:** anticipation-premium directional hypothesis; contrary source evidence.
**Confidence:** very low; low priority.

[Speculative Trading in Energy Markets](https://doi.org/10.1177/01956574251369707)
also examines gold cumulative abnormal returns and reports no pre-release
drift. This cautions against copying an equity pre-FOMC trade.
The conjectured mechanism is compensation earned while bearing unresolved
policy risk before public information. **Prediction:** a fixed 12:00–13:55
New York XAUUSD long on scheduled FOMC statement dates earns a positive
net excess return over matched non-FOMC dates. Require actual historical
release schedules, broker ticks and no early-release contamination;
close before the event and skip any shifted statement schedule. Compare
with matched short/random and no-trade controls. Distinct from T016's
post-release easing response and T027's uncertainty resolution.
Reject without later-event excess net expectancy; prior literature
lowers the prior. **Account constraint:** pre-event pending/exit restrictions
still need checking. **Status:** untested here; source-negative.

### T092 — Cumulative-loss interruption when losses persist

**Type:** strategy-state risk policy; inference. **Confidence:** very low.

[Kaminski and Lo](https://dspace.mit.edu/entities/publication/bb69ca4b-0cdc-487f-831d-63b2e84fafee)
analyze when cumulative-loss stop policies add or subtract value, with
daily index-futures evidence. Persistent unfavorable states could make
another intraday signal less valuable after losses.
**Prediction:** for an independently frozen positive-net XAUUSD signal,
stopping new entries after two initial-risk units of realized session
loss improves expected session P/L and equity-limit survival versus
continuing. Keep open-trade protection unchanged and evaluate the same
signal opportunity stream, recording skipped outcomes at executable prices.
Compare with random interruptions having the same exposure reduction;
reduced activity alone is insufficient. Require ticks and signal/fill
ledgers on untouched dates. Distinct from individual protective stops:
this tests persistence across **separate trades**. Reject without
incremental net value and survival. **Account constraint:** actual daily
equity limits override this optional policy. **Status:** conditional,
untested; no validated positive signal currently exists.

### T093 — Cost-aware position hysteresis

**Type:** turnover-control execution hypothesis; theoretical transfer.
**Confidence:** very low.

[Gârleanu and Pedersen](https://www.nber.org/papers/w15205)
derive partial adjustment toward an aim portfolio when returns are
predictable and trading is costly. Repeated weak forecast changes may
consume more spread than expected return.
**Prediction:** a frozen XAUUSD forecast-to-position rule with one
predeclared no-trade band around the current position improves net
opportunity P/L versus immediate full repositioning at each forecast
update, at matched ex-ante risk. Estimate costs and forecast decay on
earlier data; freeze the band and max holding time. Require forecast
vintages, ticks and fills; compare gross/net P/L, turnover and delayed
exposure. Distinct from T045's splitting one entry: this governs repeated
forecast-driven adjustments. Reject if lower turnover merely hides
missed alpha or increases tail loss. **Account constraint:** aggregated
exposure, hedging/netting semantics and intraday equity limits.
**Status:** conditional research; untested.

### T094 — Drawdown-constrained growth sizing

**Type:** portfolio risk-allocation hypothesis; theoretical transfer.
**Confidence:** very low.

[Busseti, Ryu and Boyd](https://web.stanford.edu/~boyd/papers/kelly.html)
add a probabilistic wealth-drawdown constraint to Kelly growth allocation.
**Prediction:** for an unchanged independently validated XAUUSD opportunity
stream, risk-constrained allocation produces higher forward terminal
log wealth than a fixed-fraction rule at the **same realized breach
frequency target**, after all trading costs. Freeze probability estimation
and risk constraint from preceding data; report estimation error and
serial dependence. Require full intraday bid/ask mark-to-market paths,
not just trade closes. Benchmark against simply reducing fixed risk to
the same survival level. Distinct from T007's market-volatility stop
distance: this allocates wealth under a path-survival objective.
Reject without later-date improvement at matched risk; sizing cannot
turn negative underlying expectancy positive. **Account constraint:**
model exact daily and total equity barriers. **Status:** conditional,
untested; a positive signal is a prerequisite.

### T095 — Correlated strategy signals share one gold risk budget

**Type:** simultaneous-exposure risk hypothesis; theory-based inference.
**Confidence:** low mechanism, untested value.

[Markowitz (1952)](https://doi.org/10.1111/j.1540-6261.1952.tb01525.x)
formalizes portfolio variance and covariance. Several XAUUSD strategies
may be multiple labels for the same underlying exposure.
**Prediction:** a past-only covariance-aware aggregate exposure cap across
frozen signals reduces intraday equity-barrier hits relative to independent
per-strategy risk budgets, at matched average absolute gold exposure.
Require timestamped simultaneous signal streams, broker ticks, executable
netting rules and costs. Compare with a simple gross-position cap and
equal exposure reduction; avoid claiming diversification merely because
strategy names differ. Distinct from T094's wealth growth optimization:
the treatment addresses concurrent correlated positions, not growth sizing.
Reject without forward survival improvement and retained positive net
expectancy. **Account constraint:** sum open risk across the entire
account. **Status:** conditional; no collection of validated signals
currently exists.

### T096 — Daily equity-reset headroom and position closure

**Type:** account-boundary risk hypothesis; rule-based inference.
**Confidence:** strong accounting premise, untested policy value.

[FTMO's objectives](https://ftmo.com/en/trading-objectives/)
define equity-based loss limits and CE(S)T daily recalculation; account
products differ. Floating loss persists through the boundary while the
permitted reference can change. **Prediction:** for positions spanning
that reset, a rule closing before reset whenever projected new-day
headroom is insufficient reduces barrier failures more than a matched
random early-close policy, without destroying positive net expectancy.
Freeze the headroom buffer before results; simulate both reference
balances and equity including fees/swaps. Require the exact historical
account contract, IANA reset zone, ticks and fills. Distinct from T047's
market-maker overnight liquidity: the mechanism is the account's moving
loss barrier. Reject without matched-opportunity survival benefit net of
missed trades. **Account constraint:** no generic percentage or account
type assumed. **Status:** conditional; untested.

### T097 — Same-clock return persistence across trading days

**Type:** recurring institutional-flow directional hypothesis; equity-to-gold
inference. **Confidence:** very low.

[Heston, Korajczyk and Sadka](https://arxiv.org/abs/1005.3535)
find continuation at same-clock half-hour intervals across days in a
stock cross-section. Repeated institutional execution schedules might
create a similar single-asset time pattern, but the transfer is substantial.
**Prediction:** the past-20-session mean XAUUSD return in each fixed
half-hour predicts that half-hour's next-session return after removing
the unconditional clock mean, overall daily momentum and event calendar.
Freeze the eligible London/New York block grid, lag and estimator before
testing; correct inference across blocks rather than selecting one winner.
Require broker quotes and historical local clocks. Compare with
shuffled-day, adjacent-block and constant-clock controls. Distinct from
T023's within-day predictor and T076's unconditional session premium.
Reject without incremental later-date net prediction. **Account
constraint:** no retroactive full-session features; intraday equity and
news restrictions. **Status:** untested.

### T098 — Position contraction versus fresh participation

**Type:** participation-state directional hypothesis; inference.
**Confidence:** very low.

[CME's open-interest explanation](https://www.cmegroup.com/education/lessons/open-interest)
distinguishes outstanding contracts from trading volume. Falling OI with
rising price is consistent with position closure but cannot identify
short covering by itself. Exhausted closure flow might leave less demand.
**Prediction:** following equal prior-day gold gains, next-day 08:20–09:20
New York XAUUSD returns are lower when the latest **already released**
aggregate GC open interest contracted than when it expanded, controlling
for carry, volume and CFTC categories. Use all relevant maturities to
avoid mistaking a contract roll for market exit. Require vintage
preliminary/final OI with availability times and broker ticks. Compare
identical return states; no claim of observing actual short inventory.
Distinct from T085's producer-category hedging premium and T068's
intraday order sequencing. Reject without incremental later-date net
prediction. **Account constraint:** correct publication timing and
equity limits. **Status:** untested.

### T099 — Old quotes predict failed execution, not free profit

**Type:** quote-firmness execution hypothesis; FX-to-CFD inference.
**Confidence:** very low.

[GFXC's last-look guidance](https://www.globalfxc.org/press-releases/press-p210818/)
describes a final accept/reject decision on an electronic FX quote;
it does not establish this broker's XAUUSD execution model.
**Prediction:** conditional on quoted spread, volatility and size, greater
age of the latest received broker quote predicts higher rejection/requote
probability or worse implementation shortfall for a frozen signal.
Require quote source/receipt timestamps, order acknowledgements and fills;
first establish whether the broker uses relevant validity checks.
Compare a fixed quote-age skip with identical ungated opportunities,
including missed-price movement. Distinct from T078's attempted catch-up:
this predicts execution **failure** and never trades a stale discrepancy.
Reject without incremental forward cost prediction and net gate value.
**Account constraint:** no exploitation of price errors or delays.
**Status:** untested.

### T100 — Option protection against an intraday stop gap

**Type:** cross-instrument tail-risk hedge; inference. **Confidence:** very low.

[CME's metals weekly options](https://www.cmegroup.com/markets/metals/metals-weekly-options.html)
provide short-term event-risk instruments, not a guaranteed CFD hedge.
A convex payoff may cover a discontinuous gold loss a stop cannot fill
at its trigger. **Prediction:** buying a prespecified protective GC
option alongside a fixed XAUUSD position lowers intraday extreme equity
loss more than simply reducing CFD size at equal total initial cost/risk.
Require tradable option bid/ask ticks, hedge ratio, expiry/exercise rules,
basis risk and both accounts' mark-to-market and fees. Close both legs
intraday; no expiry delivery assumption. Compare unhedged and smaller-size
controls on identical opportunities. Distinct from T039's exit observation
and T094's sizing: protection is a separate convex payoff. Reject if
premium/spread destroys net expectancy or protection arrives too late.
**Account constraint:** an option held outside a prop account may not
count toward that account's equity limit. **Status:** untested.

### T101 — Stop-limit price protection versus unfilled loss exposure

**Type:** protective-order execution hypothesis; market-design inference.
**Confidence:** low mechanism, untested broker magnitude.

[CME order definitions](https://www.cmegroup.com/rulebook/files/CME_Definitions.pdf)
distinguish stop-limit from stop-with-protection mechanisms. Price bounds
can leave exposure unfilled when liquidity disappears; broker CFD semantics
may differ. **Prediction:** for identical XAUUSD entries and stop triggers,
a stop-limit reduces adverse fill slippage **conditional on filling** but
increases unconditional extreme loss or equity-barrier failure relative
to the broker's ordinary stop. Freeze limit offset and emergency fallback;
never report only filled orders. Require supported broker order types,
ticks, acknowledgements and actual gap fills; OHLC touches are insufficient.
Compare all original positions including nonfills and partial fills.
Distinct from T046's passive **entry** selection and T100's convex hedge:
this tests protective-order completion risk. Reject the claimed net safety
of stop-limit protection if unconditional tail losses rise.
**Account constraint:** unfilled protective orders still count as open
equity risk. **Status:** untested; no live order changes authorized.

## Counting rule

Add a new T-number only after reviewing a source and confirming it is not a
renamed existing canonical thesis. A legacy H/scan/experiment may be admitted
once as an explicitly mapped canonical entry with its existing result; it is
not a new discovery and may never be counted a second time. T012 is retired
and excluded from the distinct count. Every entry must
include type, source finding, whether the trading prediction is direct or an
inference, falsifiable direction/condition, key data need, and the main
prop-firm constraint. Keep rejected ideas in the catalog with their result;
never count parameter tweaks as new theses.

## Reviewed but not counted

- [World Gold Council, *Gold the safe haven versus silver the wildcard*
  (2026)](https://www.gold.org/goldhub/research/gold-safe-haven-versus-silver-wildcard)
  reports that gold leads silver at the one-minute horizon in its intraday
  Granger-causality comparison, while silver provides no comparable leading
  signal for gold. A proposed XAGUSD-to-XAUUSD catch-up trade would run
  against that finding. The reported direction would instead predict
  **silver** from gold, outside this XAUUSD catalog; no thesis counted.
- [ECB monetary policy decisions and press conferences](https://www.ecb.europa.eu/press/pr/date/2022/html/ecb.pr220627~73acedf868.ga.html)
  are distinct scheduled releases at 14:15 and 14:45 CET, and
  [ECB communication research](https://www.ijcb.org/journal/v5n2/explaining-monetary-policy-press-conferences)
  finds incremental information in the conference for financial markets.
  This does not establish a distinct, executable XAUUSD return effect;
  counting generic post-ECB continuation would duplicate T001/T016's
  announcement-repricing mechanism. Keep the two timestamps as potential
  event controls for those theses rather than a new count.

- [Adrangi, Chatrath and David (2000)](https://www.tandfonline.com/doi/abs/10.1080/096031000331644)
  find that silver futures bore most convergence to the historical gold–silver
  spread in their 15-minute sample. That result does not imply a directional
  XAUUSD trade from a silver deviation. No gold catch-up thesis was added.
- [Smales, *Asymmetric volatility response to news sentiment in gold
  futures*](https://espace.curtin.edu.au/bitstream/20.500.11937/13017/2/212610_212610.pdf)
  uses daily aggregate news sentiment and daily settlement returns; its
  intraday quotes supply **daily volatility measures**. Its tentative
  overreaction/reversal language does not establish a minute-scale reversal,
  so no additional intraday fade thesis was counted from it.
- [Golez and Jackwerth, *Pinning in the S&P 500
  futures*](https://www.sciencedirect.com/science/article/pii/S0304405X12001365)
  documents an option-expiry pinning mechanism for equity-index futures, not
  gold. No GC/spot-gold expiry pinning evidence was verified, so an XAUUSD
  strike-magnet trade was not added.
