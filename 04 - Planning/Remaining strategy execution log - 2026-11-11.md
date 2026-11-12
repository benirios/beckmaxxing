---
tags: [ctrader, experiments, execution-log]
status: blocked_missing_external_inputs
---

# Remaining strategy execution log — 2026-11-11

User requested all remaining strategies be backtested through Gold Research Lab and logged in this vault. Platform restarted; saved broker login verified through its agent interface. No strategy parameters were changed.

## Scope and data decision

Broker metadata probe `20261111T175956_090469` returned first tick UTC `2025-06-23T00:00:00.0000000Z`. Recent October ticks already replayed successfully. Freeze available-history replication at 2025-06-23–2026-10-24 before viewing returns. Acquisition and continuous coverage remain subject to native verification. Original 2020-05-06–2026-10-24 requests remain incomplete.

Frozen queue: T002 Fade, T006 with US500.cash, T015 RoundHundreds, T015 NonRoundPlacebo. One native job at a time; original fixed volume, costs and rules. Native spread metadata must be checked.

Canonical catalog: 100 hypotheses — 94 missing audited inputs, 3 awaiting broker-data results, 3 with prior experiment results. Additional legacy sources include prior variants and 2 missing-input diagnostics.

Initial catalog/source counts: needs_inputs: 96, running: 1, pending: 2, prior_result: 14. Legacy aliases are included; this is not a count of independent hypotheses.

## Attempts

- T002 available-history replication launched as `20261111T181053_25439c`, key `exp017-v2-available-ticks-20250623-20261024`. Source and observer package passed preflight. Native engine is acquiring history.

## Missing inputs — still untested

These strategies cannot produce valid results until the listed inputs are audited and supplied. Empty rows, generated proxy events or M1 prices do not satisfy the contracts. Full contracts and coverage snapshot: `outputs/backtest-batch-2026-11-11/`.

| Strategy | Required inputs |
|---|---|
| T001 — Post-release continuation after major U.S. data | SignedReceiptsUtc |
| T003 — Session-conditioned continuation strength | SignedReceiptsUtc |
| T004 — Volatility-conditioned return feedback | Observations |
| T005 — Calendar-only macro execution exclusion | Opportunities |
| T007 — Market-open volatility forecast for stop distance | Opportunities |
| T008 — European-overlap execution-cost advantage | Opportunities |
| T009 — Dollar-fix pressure and post-fix gold reversal | SignedReceiptsUtc |
| T010 — Treasury-auction yield reversal and gold response | SignedReceiptsUtc |
| T011 — U.S. equity-shock flight-to-gold decay | Observations |
| T013 — COMEX volatility state and Asian gold response | Observations |
| T014 — PM gold auction opening continuation | AuctionDates |
| T016 — Asymmetric FOMC easing-surprise continuation | EasingDates, TighteningDates |
| T017 — Post-jump spread recovery before XAUUSD entry | Opportunities |
| T018 — Downside semivariance and near-term stop risk | Opportunities |
| T019 — Forecast disagreement and release-window risk | Opportunities, SurveyDecisions |
| T020 — Negative gold-news sentiment and downside jump hazard | DeliveryTimesUtc |
| T021 — Weekly boundary quote-quality gate | Opportunities |
| T022 — Shanghai night-information transfer to the next day open | SessionPairs |
| T023 — U.S. cash-session midday move and late gold return | NyseDates |
| T024 — Futures order imbalance and next-interval gold jump risk | SignedReceiptsUtc |
| T025 — Leveraged gold-fund rebalancing pressure | SignedReceiptsUtc |
| T026 — Announced gold-margin increase and intraday liquidation | SignedReceiptsUtc |
| T027 — FOMC uncertainty resolution and subsequent intraday range | Observations |
| T028 — Segmented-session variance forecasts the Shanghai close | Observations |
| T029 — Central-bank information shocks versus pure policy shocks | Observations |
| T030 — Quote withdrawal versus traded-volume imbalance | Observations |
| T031 — Option-implied uncertainty beyond realized volatility | Observations |
| T032 — Discontinuous jump share and subsequent tail risk | Observations |
| T033 — Previously published policy uncertainty and intraday jump hazard | Observations |
| T034 — Predictable index rolls and broker execution liquidity | Opportunities |
| T035 — Chinese physical premium and London-morning gold demand | SignedReceiptsUtc |
| T036 — Dealer gamma and ordinary-session return feedback | Observations |
| T037 — Slow moving-average trend persistence | FeatureRows |
| T038 — VWAP-aligned pullback continuation | FeatureRows |
| T039 — Candle-close exit confirmation versus transient wicks | Opportunities |
| T040 — Realized skewness and conditional intraday return tails | Observations |
| T041 — Low permutation entropy and subsequent directional predictability | FeatureRows |
| T042 — Broad-dollar shock and residual gold response | FeatureRows |
| T043 — Transaction acceleration and future intraday variance | Observations |
| T044 — Standing bid/ask depth imbalance and next gold move | FeatureRows |
| T045 — Split execution versus one market order | Entries |
| T046 — Passive entry and adverse-selection cost | Opportunities |
| T047 — Dealer inventory horizon and pre-break execution deterioration | Opportunities |
| T048 — Equity-market liquidity distress and gold execution cost | Opportunities |
| T049 — Gold option-implied skewness and intraday direction | FeatureRows |
| T050 — Oil–gold co-volatility and gold excursion risk | Observations |
| T051 — Pre-existing sentiment and event-time liquidity withdrawal | Opportunities |
| T052 — Cash-demand stress overrides equity flight to gold | Observations |
| T053 — Real-yield opportunity cost at an intraday horizon | FeatureRows |
| T054 — Conflicting payroll components and delayed repricing | Observations |
| T055 — Attention volume and unsigned jump hazard | Observations |
| T056 — Self-exciting gold-jump clusters | Observations |
| T057 — Futures settlement-window liquidity concentration | Opportunities |
| T058 — Futures interruption and broker stop-fill risk | Opportunities |
| T059 — Physical gold ETF premium and underlying purchase pressure | FeatureRows |
| T060 — Stressed London–New York basis and broker execution fragility | Opportunities |
| T061 — Gold borrowing tightness and intraday short-side execution | Opportunities |
| T062 — Annual commodity-index weight resets and intraday gold flow | FeatureRows |
| T063 — Official gold-reserve disclosure surprise | FeatureRows |
| T064 — Published auction imbalance and residual benchmark demand | FeatureRows |
| T065 — Prolonged auction and uncertainty about completion | Opportunities, StatusUpdates |
| T066 — Crypto deleveraging and gold tail-risk transmission | Observations |
| T067 — Geopolitical threat state and gold jump sensitivity | Observations |
| T068 — Persistent order splitting beyond net volume imbalance | FeatureRows |
| T069 — Repeated trade sizes and informational price impact | FeatureRows |
| T070 — Hidden-liquidity replenishment and failed price pressure | FeatureRows |
| T071 — Aggressive multi-level sweeps and transient overshoot | FeatureRows |
| T072 — Observable dealer inventory and subsequent quote skew | InventoryEvents, ReceivedQuotes |
| T073 — Missing London bullion participation on local holidays | Opportunities |
| T076 — Unconditional day-versus-overnight gold drift | CompleteDates |
| T077 — Asian replenishment after a negative U.S. gold session | FeatureRows |
| T078 — Futures-to-spot price-discovery catch-up | Observations, ContractIdentifier |
| T080 — VWAP deviation and inventory mean reversion | FeatureRows |
| T081 — Actual stop-order concentration and cascade hazard | FeatureRows |
| T082 — Slow volatility memory beyond the current intraday state | Observations |
| T083 — Cross-venue cojump identifies a persistent information shock | FeatureRows |
| T084 — Carry-curve state and intraday gold risk premium | FeatureRows, NearContract, FarContract |
| T085 — Producer hedging pressure and intraday compensation | FeatureRows |
| T086 — Month-end multi-asset portfolio rebalancing into gold | FeatureRows |
| T087 — Unrealized investor gains and underreaction to gold news | FeatureRows |
| T088 — Bid–ask bounce creates non-executable apparent reversal | TradeRows, ContractIdentifier |
| T089 — Option term-structure inversion and immediate event risk | Observations |
| T090 — Upside variance risk premium and intraday compensation | FeatureRows |
| T091 — Pre-FOMC risk-bearing drift in gold | FeatureRows, ReleaseReceiptsUtc |
| T092 — Cumulative-loss interruption when losses persist | Opportunities, ValidationReference, ResetZone, ResetClock, LossSessionZone, LossSessionClock, RandomInterruptions |
| T093 — Cost-aware position hysteresis | Opportunities, ValidationReference, ResetZone, ResetClock, CostCalibrationReference |
| T094 — Drawdown-constrained growth sizing | Opportunities, ValidationReference, ResetZone, ResetClock |
| T095 — Correlated strategy signals share one gold risk budget | Opportunities, ValidationReference, ResetZone, ResetClock, CovarianceSnapshots |
| T096 — Daily equity-reset headroom and position closure | Opportunities, ValidationReference, ResetZone, ResetClock, RandomClosures |
| T097 — Same-clock return persistence across trading days | FeatureRows, BlockGrid, PastBlockReturns |
| T098 — Position contraction versus fresh participation | FeatureRows |
| T099 — Old quotes predict failed execution, not free profit | Opportunities |
| T100 — Option protection against an intraday stop gap | Opportunities, OptionQuotes, OptionContract, UnderlyingContract, ExerciseReference, CalibrationReference, ExpiryUtc |
| T101 — Stop-limit price protection versus unfilled loss exposure | Entries, BrokerSemanticsReference |
| legacy:XauUsdClockVolatilityStopRiskDiagnostics — ClockVolatilityStopRiskDiagnostics | Observations |
| legacy:XauUsdSemivarianceStopRiskDiagnostics — SemivarianceStopRiskDiagnostics | Observations |

- Terminal T002 primary: **needs_review**. Native evidence: [[Lab run 20261111T181053_25439c - T002]]. Summary: `{"robot": "XauUsdTokyoPressureReversal", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 25.18, "roi": 0.25, "trades": 41, "profit_factor": 1.1, "drawdown": 1.8509356566446915}`. Execution audit still required; this terminal state does not establish validity.

- T006 primary available-history replication launched as `20261111T181528_6e8c25`; key `exp018-available-ticks-20250623-20261024`.

- Terminal T006 primary: **needs_review**. Native evidence: [[Lab run 20261111T181528_6e8c25 - T006]]. Summary: `{"robot": "XauUsdEquityDrawdownFlightToGold", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 322.77, "roi": 3.23, "trades": 195, "profit_factor": 1.27, "drawdown": 2.8838529810471014}`. Execution audit still required; this terminal state does not establish validity.

- T015 primary available-history replication launched as `20261111T182129_f60476`; key `exp019-round-available-ticks-20250623-20261024`.

- Terminal T015 primary: **needs_review**. Native evidence: [[Lab run 20261111T182129_f60476 - T015]]. Summary: `{"robot": "XauUsdRoundNumberRejection", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -330.85, "roi": -3.31, "trades": 73, "profit_factor": 0.53, "drawdown": 3.675745194131954}`. Execution audit still required; this terminal state does not establish validity.

- T015 placebo available-history replication launched as `20261111T182319_43e199`; key `exp019-placebo-available-ticks-20250623-20261024`.

- Terminal T015 placebo: **needs_review**. Native evidence: [[Lab run 20261111T182319_43e199 - T015]]. Summary: `{"robot": "XauUsdRoundNumberRejection", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 82.42, "roi": 0.82, "trades": 86, "profit_factor": 1.19, "drawdown": 1.2915267776288204}`. Execution audit still required; this terminal state does not establish validity.

## Final checkpoint

All four frozen native replays reached `needs_review`; no unfinished platform
jobs remain. They are available-history exploratory results, not completion of
the original six-year requests or validation/promotion. All reports confirm
server ticks, USD 10,000 initial balance and USD 30/million commission. All have
null spread metadata; actual fixed 1-pip execution is unverified. Native start
was 2025-06-23 22:00:02.050 UTC, later than the requested midnight. All reports
end with no open positions or pending orders. The platform saved reports,
console logs, parameters, source/observer/package identities and hashes, plus
one Obsidian note per attempt. No separate events.json was emitted by this CLI;
the native report history and console are retained, and missing event records
are not reconstructed as observed facts.

| Strategy / branch | Trades | Net USD | PF | Max equity DD | Assessment |
|---|---:|---:|---:|---:|---|
| T002 Tokyo Fade | 41 | +25.18 | 1.10 | 1.85% | Exploratory; 2026 negative; 2 report trades lack console signal records |
| T006 Equity shock | 195 | +322.77 | 1.27 | 2.88% | Exploratory positive; reference-feed audit and controls pending |
| T015 RoundHundreds | 73 | -330.85 | 0.53 | 3.68% | Negative primary; underperforms placebo |
| T015 NonRoundPlacebo | 86 | +82.42 | 1.19 | 1.29% | Control only; no promotion |

Detailed execution/annual/directional reviews: [[EXP-017 - Tokyo Pressure Fade]],
[[EXP-018 - Equity Shock Gold Response]], [[EXP-019 - Round Number Rejection]].
Machine-readable audit, normalized report-trade CSVs, before/after coverage and
SHA-256 manifest: `outputs/backtest-batch-2026-11-11/`.

**The all-strategies objective remains incomplete.** 94 canonical hypotheses
(plus two legacy diagnostic sources) still lack required audited external inputs,
listed individually above. The repository contains no such event/feature datasets;
its reference calendar alone does not satisfy these contracts. The platform
preflight gates them. Required source locations were requested from the user
while the ready batch continued. Do not mark them completed, run empty inputs,
substitute fabricated signals or infer returns from compilation.

Further validation of positive primaries requires their frozen matched controls,
cost sensitivity and independent confirmation. These were not part of the three
presets exposed by the current platform API; no strategy rules were changed to
force execution. Original pre-2025-06-23 tick history remains unavailable in the
recorded full-period attempt. No candidate is approved for live use.
