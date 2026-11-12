# Backtest Checklist

Started 2026-11-10 at the user’s request. Process one frozen test at a time; audit the returned report, events, log and parameter set before moving to the next. Compilation and synthetic tests are not market backtests.

**Next: audit and classify the completed workaround batch** in [[Workaround backtest batch - 2026-11-11]]. T002/EXP-017 is already recorded separately; do not mark it pending solely because a tick rerun remains a distinct future validation.

Statuses below come from the thesis index and experiment register. “No recorded test” means no market result is documented here; it does not prove that no external run exists. Legacy partial/invalid runs remain unresolved. Data-dependent studies stay in this checklist until their inputs and comparisons can be tested; do not manufacture standalone signals for them.

| Thesis | Name | Existing evidence / next action | cBot type | Source |
|---|---|---|---|---|
| T001 | Post-release continuation after major U.S. data | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdMacroReleaseContinuation.cs` |
| T002 | Tokyo-open price-pressure fade | EXP-017 V2 M1 negative (-3.08%, PF 0.51); tick-data rerun pending | Native orders | `XauUsdTokyoPressureReversal.cs` |
| T003 | Session-conditioned continuation strength | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdSessionContinuationStrength.cs` |
| T004 | Volatility-conditioned return feedback | Workaround run recorded; needs audit; not ranked | Observation / diagnostic | `XauUsdVolatilityReturnFeedback.cs` |
| T005 | Calendar-only macro execution exclusion | No recorded test; input/spec review pending | Native orders | `XauUsdMacroCalendarGate.cs` |
| T006 | Gold reaction to extreme equity drawdowns | No recorded test; input/spec review pending | Native orders | `XauUsdEquityDrawdownFlightToGold.cs` |
| T007 | Market-open volatility forecast for stop distance | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdClockVolatilityStopRisk.cs` |
| T008 | European-overlap execution-cost advantage | No recorded test; input/spec review pending | Native orders | `XauUsdEuropeanOverlapGate.cs` |
| T009 | Dollar-fix pressure and post-fix gold reversal | No recorded test; input/spec review pending | Native orders | `XauUsdDollarFixGoldReversal.cs` |
| T010 | Treasury-auction yield reversal and gold response | No recorded test; input/spec review pending | Native orders | `XauUsdTreasuryAuctionGoldRebound.cs` |
| T011 | U.S. equity-shock flight-to-gold decay | Workaround run recorded; needs audit; not ranked | Observation / diagnostic | `XauUsdEquityShockDecay.cs` |
| T013 | COMEX volatility state and Asian gold response | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdComexStateAsianResponse.cs` |
| T014 | PM gold auction opening continuation | Excluded from workaround batch; verified auction/calendar inputs unavailable | Native orders | `XauUsdLondonPmAuctionContinuation.cs` |
| T015 | Gold round-number approach rejection | No recorded test; input/spec review pending | Native orders | `XauUsdRoundNumberRejection.cs` |
| T016 | Asymmetric FOMC easing-surprise continuation | No recorded test; input/spec review pending | Native orders | `XauUsdFomcEasingSurpriseContinuation.cs` |
| T017 | Post-jump spread recovery before XAUUSD entry | No recorded test; input/spec review pending | Native orders | `XauUsdPostJumpSpreadRecovery.cs` |
| T018 | Downside semivariance and near-term stop risk | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdSemivarianceStopRisk.cs` |
| T019 | Forecast disagreement and release-window risk | No recorded test; input/spec review pending | Native orders | `XauUsdSurveyDispersionExposureGate.cs` |
| T020 | Negative gold-news sentiment and downside jump hazard | No recorded test; input/spec review pending | Native orders | `XauUsdNegativeGoldNewsShort.cs` |
| T021 | Weekly boundary quote-quality gate | No recorded test; input/spec review pending | Native orders | `XauUsdWeeklyBoundaryQuoteGate.cs` |
| T022 | Shanghai night-information transfer to the next day open | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdShanghaiNightTransfer.cs` |
| T023 | U.S. cash-session midday move and late gold return | Local rejection | Native orders | `XauUsdUsMiddayLateTransfer.cs` |
| T024 | Futures order imbalance and next-interval gold jump risk | No recorded test; input/spec review pending | Native orders | `XauUsdFuturesImbalanceNextBar.cs` |
| T025 | Leveraged gold-fund rebalancing pressure | No recorded test; input/spec review pending | Native orders | `XauUsdLeveragedFundRebalance.cs` |
| T026 | Announced gold-margin increase and intraday liquidation | No recorded test; input/spec review pending | Native orders | `XauUsdMarginIncreaseLiquidation.cs` |
| T027 | FOMC uncertainty resolution and subsequent intraday range | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdFomcUncertaintyResolution.cs` |
| T028 | Segmented-session variance forecasts the Shanghai close | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdShanghaiSegmentedVariance.cs` |
| T029 | Central-bank information shocks versus pure policy shocks | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdCentralBankInformationShock.cs` |
| T030 | Quote withdrawal versus traded-volume imbalance | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdQuoteWithdrawalTailRisk.cs` |
| T031 | Option-implied uncertainty beyond realized volatility | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdGvzIncrementalVariance.cs` |
| T032 | Discontinuous jump share and subsequent tail risk | Workaround run recorded; needs audit; not ranked | Observation / diagnostic | `XauUsdJumpShareTailRisk.cs` |
| T033 | Previously published policy uncertainty and intraday jump hazard | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdPolicyUncertaintyJumpHazard.cs` |
| T034 | Predictable index rolls and broker execution liquidity | No recorded test; input/spec review pending | Native orders | `XauUsdIndexRollExecution.cs` |
| T035 | Chinese physical premium and London-morning gold demand | No recorded test; input/spec review pending | Native orders | `XauUsdShanghaiPremiumLondonDemand.cs` |
| T036 | Dealer gamma and ordinary-session return feedback | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdDealerGammaFeedback.cs` |
| T037 | Slow moving-average trend persistence | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdSlowMovingAverage.cs` |
| T038 | VWAP-aligned pullback continuation | Excluded from workaround batch; full-text rule audit unavailable | Native orders | `XauUsdVwapPullbackContinuation.cs` |
| T039 | Candle-close exit confirmation versus transient wicks | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdCandleConfirmedEmaExit.cs` |
| T040 | Realized skewness and conditional intraday return tails | Workaround run recorded; needs audit; not ranked | Observation / diagnostic | `XauUsdRealizedSkewQuantile.cs` |
| T041 | Low permutation entropy and subsequent directional predictability | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdEntropySignPersistence.cs` |
| T042 | Broad-dollar shock and residual gold response | No recorded test; input/spec review pending | Native orders | `XauUsdDollarShockResidualShort.cs` |
| T043 | Transaction acceleration and future intraday variance | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdTransactionDurationVariance.cs` |
| T044 | Standing bid/ask depth imbalance and next gold move | No recorded test; input/spec review pending | Native orders | `XauUsdStandingDepthImbalance.cs` |
| T045 | Split execution versus one market order | No recorded test; input/spec review pending | Native orders | `XauUsdSplitExecution.cs` |
| T046 | Passive entry and adverse-selection cost | No recorded test; input/spec review pending | Native orders | `XauUsdPassiveEntrySelection.cs` |
| T047 | Dealer inventory horizon and pre-break execution deterioration | No recorded test; input/spec review pending | Native orders | `XauUsdPreBreakExecutionGate.cs` |
| T048 | Equity-market liquidity distress and gold execution cost | No recorded test; input/spec review pending | Native orders | `XauUsdEquityDepthCostGate.cs` |
| T049 | Gold option-implied skewness and intraday direction | No recorded test; input/spec review pending | Native orders | `XauUsdImpliedSkewDirection.cs` |
| T050 | Oil–gold co-volatility and gold excursion risk | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdOilGoldCovarianceTail.cs` |
| T051 | Pre-existing sentiment and event-time liquidity withdrawal | No recorded test; input/spec review pending | Native orders | `XauUsdSentimentReleaseLiquidityGate.cs` |
| T052 | Cash-demand stress overrides equity flight to gold | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdCashStressFlightComparison.cs` |
| T053 | Real-yield opportunity cost at an intraday horizon | No recorded test; input/spec review pending | Native orders | `XauUsdRealYieldOpportunityCost.cs` |
| T054 | Conflicting payroll components and delayed repricing | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdPayrollComponentConflict.cs` |
| T055 | Attention volume and unsigned jump hazard | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdNewsAttentionJumpHazard.cs` |
| T056 | Self-exciting gold-jump clusters | Workaround run recorded; needs audit; not ranked | Observation / diagnostic | `XauUsdSelfExcitingJumpHazard.cs` |
| T057 | Futures settlement-window liquidity concentration | No recorded test; input/spec review pending | Native orders | `XauUsdSettlementWindowExecution.cs` |
| T058 | Futures interruption and broker stop-fill risk | No recorded test; input/spec review pending | Native orders | `XauUsdFuturesInterruptionExecution.cs` |
| T059 | Physical gold ETF premium and underlying purchase pressure | No recorded test; input/spec review pending | Native orders | `XauUsdEtfPremiumDemand.cs` |
| T060 | Stressed London–New York basis and broker execution fragility | No recorded test; input/spec review pending | Native orders | `XauUsdDeliveryBasisExecutionGate.cs` |
| T061 | Gold borrowing tightness and intraday short-side execution | No recorded test; input/spec review pending | Native orders | `XauUsdLeaseRateExecutionAsymmetry.cs` |
| T062 | Annual commodity-index weight resets and intraday gold flow | No recorded test; input/spec review pending | Native orders | `XauUsdAnnualIndexWeightReset.cs` |
| T063 | Official gold-reserve disclosure surprise | No recorded test; input/spec review pending | Native orders | `XauUsdReserveQuantitySurprise.cs` |
| T064 | Published auction imbalance and residual benchmark demand | No recorded test; input/spec review pending | Native orders | `XauUsdPublishedAuctionImbalance.cs` |
| T065 | Prolonged auction and uncertainty about completion | No recorded test; input/spec review pending | Native orders | `XauUsdAuctionCompletionWait.cs` |
| T066 | Crypto deleveraging and gold tail-risk transmission | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdCryptoLiquidationTail.cs` |
| T067 | Geopolitical threat state and gold jump sensitivity | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdGeopoliticalThreatHazard.cs` |
| T068 | Persistent order splitting beyond net volume imbalance | No recorded test; input/spec review pending | Native orders | `XauUsdPersistentTradeSignFlow.cs` |
| T069 | Repeated trade sizes and informational price impact | No recorded test; input/spec review pending | Native orders | `XauUsdRepeatedTradeSizeFlow.cs` |
| T070 | Hidden-liquidity replenishment and failed price pressure | No recorded test; input/spec review pending | Native orders | `XauUsdHiddenSupplyReplenishment.cs` |
| T071 | Aggressive multi-level sweeps and transient overshoot | No recorded test; input/spec review pending | Native orders | `XauUsdAggressiveSweepReversal.cs` |
| T072 | Observable dealer inventory and subsequent quote skew | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdDealerInventoryQuoteSkew.cs` |
| T073 | Missing London bullion participation on local holidays | No recorded test; input/spec review pending | Native orders | `XauUsdLondonHolidayExecutionGate.cs` |
| T074 | Opening-range boundary continuation family | Local variants rejected | Native orders | `XauUsdNy15OrbBaseline.cs` |
| T075 | Rest-of-day to COMEX-close momentum | Local rejection | Native orders | `XauUsdGcCloseControls.cs` |
| T076 | Unconditional day-versus-overnight gold drift | Local translations rejected | Native orders | `XauUsdOvernightDrift.cs` |
| T077 | Asian replenishment after a negative U.S. gold session | Development rejected | Native orders | `XauUsdAsiaReplenishment.cs` |
| T078 | Futures-to-spot price-discovery catch-up | Excluded from FTMO path | Observation / diagnostic | `XauUsdFuturesSpotCatchupDiagnostic.cs` |
| T079 | Failed range breakout and trapped-position liquidation | Negative partial run; execution unresolved | Native orders | `XauUsdLondonFailedBreakout.cs` |
| T080 | VWAP deviation and inventory mean reversion | Source-negative; untested here | Native orders | `XauUsdVwapBandReversion.cs` |
| T081 | Actual stop-order concentration and cascade hazard | No recorded test; input/spec review pending | Native orders | `XauUsdMeasuredStopCascade.cs` |
| T082 | Slow volatility memory beyond the current intraday state | Blocked; required audited broker-session inputs unavailable | Observation / diagnostic | `XauUsdSlowVarianceMemory.cs` |
| T083 | Cross-venue cojump identifies a persistent information shock | No recorded test; input/spec review pending | Native orders | `XauUsdCommonInformationCojump.cs` |
| T084 | Carry-curve state and intraday gold risk premium | No recorded test; input/spec review pending | Native orders | `XauUsdCarryCurvePremium.cs` |
| T085 | Producer hedging pressure and intraday compensation | Contrary source evidence; untested here | Native orders | `XauUsdProducerHedgingPremium.cs` |
| T086 | Month-end multi-asset portfolio rebalancing into gold | No recorded test; input/spec review pending | Native orders | `XauUsdMonthEndPortfolioDemand.cs` |
| T087 | Unrealized investor gains and underreaction to gold news | No recorded test; input/spec review pending | Native orders | `XauUsdHolderGainsNewsDrift.cs` |
| T088 | Bid–ask bounce creates non-executable apparent reversal | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdBidAskBounceNull.cs` |
| T089 | Option term-structure inversion and immediate event risk | No recorded test; input/spec review pending | Observation / diagnostic | `XauUsdOptionTermStructureHazard.cs` |
| T090 | Upside variance risk premium and intraday compensation | No recorded test; input/spec review pending | Native orders | `XauUsdUpsideVariancePremium.cs` |
| T091 | Pre-FOMC risk-bearing drift in gold | Source-negative; untested here | Native orders | `XauUsdPreFomcDrift.cs` |
| T092 | Cumulative-loss interruption when losses persist | Conditional on positive signal; untested | Native orders | `XauUsdCumulativeLossInterruption.cs` |
| T093 | Cost-aware position hysteresis | Conditional on positive signal; untested | Native orders | `XauUsdPositionHysteresis.cs` |
| T094 | Drawdown-constrained growth sizing | Conditional on positive signal; untested | Native orders | `XauUsdRiskConstrainedGrowth.cs` |
| T095 | Correlated strategy signals share one gold risk budget | Conditional on positive signal; untested | Native orders | `XauUsdCovarianceRiskCap.cs` |
| T096 | Daily equity-reset headroom and position closure | Conditional on positive signal; untested | Native orders | `XauUsdResetHeadroomPolicy.cs` |
| T097 | Same-clock return persistence across trading days | Workaround run recorded; needs audit; not ranked | Native orders | `XauUsdSameClockPersistence.cs` |
| T098 | Position contraction versus fresh participation | No recorded test; input/spec review pending | Native orders | `XauUsdOpenInterestContraction.cs` |
| T099 | Old quotes predict failed execution, not free profit | No recorded test; input/spec review pending | Native orders | `XauUsdQuoteAgeExecutionGate.cs` |
| T100 | Option protection against an intraday stop gap | Cross-account hedge limits; untested | Native orders | `XauUsdQuotedOptionHedge.cs` |
| T101 | Stop-limit price protection versus unfilled loss exposure | No recorded test; input/spec review pending | Native orders | `XauUsdStopLimitProtection.cs` |
