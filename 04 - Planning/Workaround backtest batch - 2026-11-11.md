---
tags: [ctrader, preregistration, workaround-batch]
status: completed
---

# Workaround backtest batch — 2026-11-11

User authorized running the previously identified feasible candidates and removing
strategies without a valid workaround from the active queue. This changes scope;
excluded strategies are not rejections, completed tests, or deleted research.
Order flow and options are excluded. Preserve their existing evidence.

## Frozen before viewing new strategy outcomes

Available-history native ticks: 2025-06-23–2026-10-24, exposed development history,
USD 10,000, commission 30/million, requested spread 1 (native null metadata remains
unverified). No optimization. All decisions use completed past information.
The quote exporter V2 preserves invalid quotes in a sidecar; input windows with
invalid quotes/missing required minutes are excluded. No bars are filled.

Canonical candidate scope from the preceding shortlist:
T001, T003, T004, T007, T011, T014, T018, T022, T023, T032, T037, T038,
T039, T040, T041, T056, T080, T082, T091, T097.

- T003: Tokyo 09:00–09:30 Japan, London 08:00–08:30 London, NY 09:30–10:00 NY;
  each followed separately for 30 minutes. Complete midpoint windows and boundary
  quote ages <=2 seconds; zero moves excluded. Primary/opposite/random separate.
- T022/T023: original native rules and four original direction variants. Verified
  SHFE/NYSE calendars; no weekday approximation for holidays or early closes.
- T001: actual BLS archived release clocks, including shutdown postponements;
  first five-minute midpoint move, prior SIX same-release valid moves P75
  nearest-rank, 6-event warmup; strict exceedance; original 25-minute hold.
  Primary/opposite/random. Official publication clock is not a measured network receipt.
- T037: SMA 48/288 completed broker M5 closes (4h/24h), one 10:00 NY decision per
  full NYSE session, 60-minute exit, max one trade/day. Longer histories motivated
  by the source paper's 5-minute economic timescale, not its best parameter search.
  One frozen setting; no sweep. Canonical source uses broker BID M5, explicitly
  distinct from midpoint feature calculations. Primary/opposite/random.
- Generic feature strategies: initial hard-stop distance = preceding complete
  60-minute midpoint high-low range, $50 initial account-currency risk; no target
  unless the canonical adapter attaches its documented entry-known VWAP target.
  Native minimum-stop/volume guards remain authoritative. No stop guarantee.
- T007/T018/T039: identical T003 NY signal opportunities, original 30-minute exit,
  $50 initial risk, baseline versus alternative. Original stop is prior30m range.
  T007 scales by sqrt(prior60 same-clock mean RV/current prior30m RV).
  T018 scales by sqrt(current negative semivariance/prior60 same-clock mean
  negative semivariance). All six M5 returns completed before decision.
- T041: one 10:00 NY decision/day; previous M5 return predicts next5m. Entropy:
  normalized permutation entropy, dimension3 lag1, previous20 valid London-session
  M5-return sequences (08:00–16:00); state cutoff past60 session entropies, computed
  before current London open. Prior60m variance/spread and jump frequency retained.
  Primary/opposite/random and deterministic past-data shuffled-predictor control.
- T080: expressly acknowledged BROKER QUOTE-TICK weighting (not exchange volume);
  London08:00 session anchor, M5 decisions 09:00–15:00 London, 60m exit, prior60m
  range stop. Canonical 2–3 sigma eligibility. Frozen placebo anchor chosen by
  date hash among 07:00/07:15/07:30/07:45 London. Primary/opposite/random/placebo.
- T097: half-hour blocks London09:00 and NY10:00, prior20 VALID session returns;
  prior60-session unconditional means and ridge OLS coefficients on earlier
  completed dates; nonnegative block coefficient, ridge1e-6 standardized features.
  Adjacent control is preceding half-hour on prior dates. Shuffled control samples
  20 preceding60 dates with fixed date seed. No choosing a winning block.
  Primary/opposite/random/constant-clock/shuffled-days/adjacent-block separate.
- T091: original noon–13:55 NY on verified normal 14:00 FOMC days; matched ordinary
  control date is the preceding same weekday seven days earlier when eligible.
  Actual official statement publication clocks retained for contamination handling;
  they are not measured feed-arrival timestamps. Five original native branches.
- Observation studies T004/T011/T032/T040/T056/T082: no trading alpha is invented.
  Evaluate native tick-observed returns/variance/tails, not zero-PnL profitability.
  T004 repaired from erroneous 30m outcome to catalog's15m; original source archived.
  T011 uses prior4680 regular-session US500 M5 returns, negative P1/positive P99,
  received close time; gold windows0–5,5–20,20–50 remain disjoint.
  Remaining observation estimators use preceding60 valid same-clock sessions,
  minimum60, no future/current-day outcomes. One 11:00 NY observation daily.
  RV is one-minute log-return variance. Jump component=max(RV-bipower,0), with
  prior60m predictable minute variance and z=3.5 detector. T032 excursion threshold
  =2*sqrt(prior60m RV/2), two-sided; ridge logistic baselines versus jump addition.
  T040 lower quantile=.05, OLS location plus prior residual quantile, base controls
  return/variance/negative semivariance/jump share versus added standardized skew.
  T056 past jump-arrival exponential decay with30m half-life over previous6h;
  independent-arrival, variance-only and added-intensity logistic forecasts.
  T082 log-RV ridge OLS: current risk controls versus added prior completed broker NY18:00–17:00
  session and past5-valid-session variances; missing closure minutes invalidate.

Exact row files, calendar provenance, source hashes and immutable parameter
presets are retained under `configs/backtest-presets.json`,
`data/reference/workaround-20261111/`, and `outputs/workaround-batch-20261111/`.
Platform guard tests, compilation and data export are implementation evidence only.

## Exceptions found during preparation

T014 requires verified actual auction eligibility/start exceptions and a complete
high-impact USD exclusion calendar; a normal business-day list cannot establish
that. T038 requires full-text rule audit before testing; the referenced SSRN
full text returned403. Neither is silently approximated. They will be excluded
unless those exact prerequisites can be recovered in this session.

## Results

Native attempts will link here after completion. No candidate is promoted.

- **Launched t023-follow**: `20261111T191208_d3b8c4`. Rules and inputs frozen before launch.

- **Terminal t023-follow — needs_review**: [[Lab run 20261111T191208_d3b8c4 - T023]]. Native summary `{"robot": "XauUsdUsMiddayLateTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -90.23, "roi": -0.9, "trades": 323, "profit_factor": 0.9, "drawdown": 1.876652912312055}`. Audit pending; terminal does not mean valid/passed.

- **Launched t023-opposite**: `20261111T191338_673424`. Rules and inputs frozen before launch.

- **Terminal t023-opposite — needs_review**: [[Lab run 20261111T191338_673424 - T023]]. Native summary `{"robot": "XauUsdUsMiddayLateTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -313.12, "roi": -3.13, "trades": 323, "profit_factor": 0.69, "drawdown": 3.412191082700402}`. Audit pending; terminal does not mean valid/passed.

- **Launched t023-long**: `20261111T191508_47aa6a`. Rules and inputs frozen before launch.

- **Terminal t023-long — needs_review**: [[Lab run 20261111T191508_47aa6a - T023]]. Native summary `{"robot": "XauUsdUsMiddayLateTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -111.05, "roi": -1.11, "trades": 323, "profit_factor": 0.88, "drawdown": 3.1035881340269325}`. Audit pending; terminal does not mean valid/passed.

- **Launched t023-random**: `20261111T191638_df3d0c`. Rules and inputs frozen before launch.

- **Terminal t023-random — needs_review**: [[Lab run 20261111T191638_df3d0c - T023]]. Native summary `{"robot": "XauUsdUsMiddayLateTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -149.85, "roi": -1.5, "trades": 323, "profit_factor": 0.84, "drawdown": 2.828983589934459}`. Audit pending; terminal does not mean valid/passed.

- **Launched t022-follow**: `20261111T191814_b5b997`. Rules and inputs frozen before launch.


Preparation clarification before observation launches: past-session T082 RV uses
actual observed M1 midpoint close samples over complete NY18:00–next17:00 broker
sessions, preserving actual quote timestamps; no missing minute is filled.
Friday early closures and incomplete/invalid sessions do not qualify. The last
close must be within2s of the declared closure. This avoids pretending that
sparse overnight quotes arrive exactly on every minute boundary. Forecast
training-label boundary samples can differ from native strict-after endpoint
quotes; that limitation is retained explicitly. Preceding-session availability
is recorded and must be earlier than each decision.

- **Terminal t022-follow — needs_review**: [[Lab run 20261111T191814_b5b997 - T022]]. Native summary `{"robot": "XauUsdShanghaiNightTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -312.01, "roi": -3.12, "trades": 305, "profit_factor": 0.82, "drawdown": 4.888389911503801}`. Audit pending; terminal does not mean valid/passed.

- **Launched t022-opposite**: `20261111T192000_d0b690`. Rules and inputs frozen before launch.

- **Terminal t022-opposite — needs_review**: [[Lab run 20261111T192000_d0b690 - T022]]. Native summary `{"robot": "XauUsdShanghaiNightTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -90.77, "roi": -0.91, "trades": 305, "profit_factor": 0.94, "drawdown": 4.538171976107569}`. Audit pending; terminal does not mean valid/passed.

- **Launched t022-long**: `20261111T192135_7868db`. Rules and inputs frozen before launch.

- **Terminal t022-long — needs_review**: [[Lab run 20261111T192135_7868db - T022]]. Native summary `{"robot": "XauUsdShanghaiNightTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -164.89, "roi": -1.65, "trades": 305, "profit_factor": 0.9, "drawdown": 3.9517435751391843}`. Audit pending; terminal does not mean valid/passed.

- **Launched t022-random**: `20261111T192305_4662ea`. Rules and inputs frozen before launch.

- **Terminal t022-random — needs_review**: [[Lab run 20261111T192305_4662ea - T022]]. Native summary `{"robot": "XauUsdShanghaiNightTransfer", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -380.15, "roi": -3.8, "trades": 305, "profit_factor": 0.79, "drawdown": 4.855292597642655}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-tokyo-follow**: `20261111T192441_733c1a`. Rules and inputs frozen before launch.

- **Terminal t003-tokyo-follow — needs_review**: [[Lab run 20261111T192441_733c1a - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -239.72, "roi": -2.4, "trades": 290, "profit_factor": 0.79, "drawdown": 2.862283889219199}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-tokyo-opposite**: `20261111T192616_6c6c81`. Rules and inputs frozen before launch.

- **Terminal t003-tokyo-opposite — needs_review**: [[Lab run 20261111T192616_6c6c81 - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -148.5, "roi": -1.49, "trades": 290, "profit_factor": 0.86, "drawdown": 2.7431522604837295}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-tokyo-random**: `20261111T192747_417e62`. Rules and inputs frozen before launch.

- **Terminal t003-tokyo-random — needs_review**: [[Lab run 20261111T192747_417e62 - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -566.26, "roi": -5.66, "trades": 290, "profit_factor": 0.56, "drawdown": 5.697154871870753}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-london-follow**: `20261111T192932_b366ee`. Rules and inputs frozen before launch.

- **Terminal t003-london-follow — needs_review**: [[Lab run 20261111T192932_b366ee - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -491.35, "roi": -4.91, "trades": 322, "profit_factor": 0.61, "drawdown": 5.130250955399835}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-london-opposite**: `20261111T193102_5afecc`. Rules and inputs frozen before launch.

- **Terminal t003-london-opposite — needs_review**: [[Lab run 20261111T193102_5afecc - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 108.76, "roi": 1.09, "trades": 322, "profit_factor": 1.12, "drawdown": 1.9306810224524265}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-london-random**: `20261111T193238_5623cc`. Rules and inputs frozen before launch.

- **Terminal t003-london-random — needs_review**: [[Lab run 20261111T193238_5623cc - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -426.5, "roi": -4.26, "trades": 322, "profit_factor": 0.64, "drawdown": 4.706177539020635}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-ny-follow**: `20261111T193408_d865ed`. Rules and inputs frozen before launch.

- **Terminal t003-ny-follow — needs_review**: [[Lab run 20261111T193408_d865ed - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 376.14, "roi": 3.76, "trades": 331, "profit_factor": 1.24, "drawdown": 2.3182312238347977}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-ny-opposite**: `20261111T193554_9771de`. Rules and inputs frozen before launch.

- **Terminal t003-ny-opposite — needs_review**: [[Lab run 20261111T193554_9771de - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -801.55, "roi": -8.02, "trades": 331, "profit_factor": 0.63, "drawdown": 8.099337053560284}`. Audit pending; terminal does not mean valid/passed.

- **Launched t003-ny-random**: `20261111T193730_ed580c`. Rules and inputs frozen before launch.

- **Terminal t003-ny-random — needs_review**: [[Lab run 20261111T193730_ed580c - T003]]. Native summary `{"robot": "XauUsdSessionContinuationStrength", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 378.64, "roi": 3.79, "trades": 331, "profit_factor": 1.24, "drawdown": 2.2711931591328747}`. Audit pending; terminal does not mean valid/passed.

- **Launched t001-follow**: `20261111T193926_82c635`. Rules and inputs frozen before launch.

- **Terminal t001-follow — needs_review**: [[Lab run 20261111T193926_82c635 - T001]]. Native summary `{"robot": "XauUsdMacroReleaseContinuation", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -93.42, "roi": -0.93, "trades": 9, "profit_factor": 0.16, "drawdown": 1.3380853748827417}`. Audit pending; terminal does not mean valid/passed.

- **Launched t001-opposite**: `20261111T194117_0c066a`. Rules and inputs frozen before launch.

- **Terminal t001-opposite — needs_review**: [[Lab run 20261111T194117_0c066a - T001]]. Native summary `{"robot": "XauUsdMacroReleaseContinuation", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 80.63, "roi": 0.81, "trades": 9, "profit_factor": 4.8, "drawdown": 0.45599707339258055}`. Audit pending; terminal does not mean valid/passed.

- **Launched t001-random**: `20261111T194303_abc7de`. Rules and inputs frozen before launch.

- **Terminal t001-random — needs_review**: [[Lab run 20261111T194303_abc7de - T001]]. Native summary `{"robot": "XauUsdMacroReleaseContinuation", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -23.55, "roi": -0.24, "trades": 9, "profit_factor": 0.69, "drawdown": 0.8559578316799451}`. Audit pending; terminal does not mean valid/passed.

- **Launched t037-primary**: `20261111T194449_c29b1e`. Rules and inputs frozen before launch.

- **Terminal t037-primary — needs_review**: [[Lab run 20261111T194449_c29b1e - T037]]. Native summary `{"robot": "XauUsdSlowMovingAverage", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0}`. Audit pending; terminal does not mean valid/passed.

- **Launched t037-opposite**: `20261111T194811_0b6a43`. Rules and inputs frozen before launch.

- **Terminal t037-opposite — needs_review**: [[Lab run 20261111T194811_0b6a43 - T037]]. Native summary `{"robot": "XauUsdSlowMovingAverage", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0}`. Audit pending; terminal does not mean valid/passed.

- **Launched t037-random**: `20261111T195021_03f6ac`. Rules and inputs frozen before launch.

- **Terminal t037-random — needs_review**: [[Lab run 20261111T195021_03f6ac - T037]]. Native summary `{"robot": "XauUsdSlowMovingAverage", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0}`. Audit pending; terminal does not mean valid/passed.

- **Launched t007-baseline**: `20261111T195227_420d0b`. Rules and inputs frozen before launch.

- **Terminal t007-baseline — needs_review**: [[Lab run 20261111T195227_420d0b - T007]]. Native summary `{"robot": "XauUsdClockVolatilityStopRisk", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 24.7, "roi": 0.25, "trades": 263, "profit_factor": 1.01, "drawdown": 5.175609524262113}`. Audit pending; terminal does not mean valid/passed.

- **Launched t007-gated**: `20261111T195427_47b4aa`. Rules and inputs frozen before launch.

- **Terminal t007-gated — needs_review**: [[Lab run 20261111T195427_47b4aa - T007]]. Native summary `{"robot": "XauUsdClockVolatilityStopRisk", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 573.72, "roi": 5.74, "trades": 263, "profit_factor": 1.32, "drawdown": 2.5972713397087297}`. Audit pending; terminal does not mean valid/passed.

- **Launched t018-baseline**: `20261111T195623_c16059`. Rules and inputs frozen before launch.

- **Terminal t018-baseline — needs_review**: [[Lab run 20261111T195623_c16059 - T018]]. Native summary `{"robot": "XauUsdSemivarianceStopRisk", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 24.7, "roi": 0.25, "trades": 263, "profit_factor": 1.01, "drawdown": 5.175609524262113}`. Audit pending; terminal does not mean valid/passed.

- **Launched t018-gated**: `20261111T195824_46b4d8`. Rules and inputs frozen before launch.

- **Terminal t018-gated — needs_review**: [[Lab run 20261111T195824_46b4d8 - T018]]. Native summary `{"robot": "XauUsdSemivarianceStopRisk", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -398.9, "roi": -3.99, "trades": 263, "profit_factor": 0.91, "drawdown": 11.583412434741446}`. Audit pending; terminal does not mean valid/passed.

- **Launched t039-baseline**: `20261111T200024_740f98`. Rules and inputs frozen before launch.

- **Terminal t039-baseline — needs_review**: [[Lab run 20261111T200024_740f98 - T039]]. Native summary `{"robot": "XauUsdCandleConfirmedEmaExit", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 143.44, "roi": 1.43, "trades": 263, "profit_factor": 1.09, "drawdown": 3.734677932549578}`. Audit pending; terminal does not mean valid/passed.

- **Launched t039-gated**: `20261111T200235_4a77ea`. Rules and inputs frozen before launch.

- **Terminal t039-gated — needs_review**: [[Lab run 20261111T200235_4a77ea - T039]]. Native summary `{"robot": "XauUsdCandleConfirmedEmaExit", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -4.76, "roi": -0.05, "trades": 263, "profit_factor": 1, "drawdown": 5.500021722044671}`. Audit pending; terminal does not mean valid/passed.

- **Launched t080-primary**: `20261111T200445_0fa252`. Rules and inputs frozen before launch.

- **Terminal t080-primary — needs_review**: [[Lab run 20261111T200445_0fa252 - T080]]. Native summary `{"robot": "XauUsdVwapBandReversion", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -1507.62, "roi": -15.08, "trades": 940, "profit_factor": 0.86, "drawdown": 18.804375588652896}`. Audit pending; terminal does not mean valid/passed.

- **Launched t080-opposite**: `20261111T200647_ec0b14`. Rules and inputs frozen before launch.

- **Terminal t080-opposite — needs_review**: [[Lab run 20261111T200647_ec0b14 - T080]]. Native summary `{"robot": "XauUsdVwapBandReversion", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -1625.9, "roi": -16.26, "trades": 1007, "profit_factor": 0.85, "drawdown": 21.21602385312559}`. Audit pending; terminal does not mean valid/passed.

- **Launched t080-random**: `20261111T200913_4b5da5`. Rules and inputs frozen before launch.

- **Terminal t080-random — needs_review**: [[Lab run 20261111T200913_4b5da5 - T080]]. Native summary `{"robot": "XauUsdVwapBandReversion", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -1906.2, "roi": -19.06, "trades": 975, "profit_factor": 0.83, "drawdown": 21.372389909143845}`. Audit pending; terminal does not mean valid/passed.

- **Launched t080-placebo**: `20261111T201120_ff3871`. Rules and inputs frozen before launch.

- **Terminal t080-placebo — needs_review**: [[Lab run 20261111T201120_ff3871 - T080]]. Native summary `{"robot": "XauUsdVwapBandReversion", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -610.8, "roi": -6.11, "trades": 881, "profit_factor": 0.94, "drawdown": 11.520833936470435}`. Audit pending; terminal does not mean valid/passed.

- **Launched t041-primary**: `20261111T201825_2778d9`. Rules and inputs frozen before launch.

- **Terminal t041-primary — failed**: [[Lab run 20261111T201825_2778d9 - T041]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t041-opposite**: `20261111T202324_bcc697`. Rules and inputs frozen before launch.

- **Terminal t041-opposite — failed**: [[Lab run 20261111T202324_bcc697 - T041]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t041-random**: `20261111T202813_4d506a`. Rules and inputs frozen before launch.

- **Terminal t041-random — needs_review**: [[Lab run 20261111T202813_4d506a - T041]]. Native summary `{"robot": "XauUsdEntropySignPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 51.54, "roi": 0.52, "trades": 93, "profit_factor": 1.16, "drawdown": 1.1063982138505595}`. Audit pending; terminal does not mean valid/passed.

- **Launched t041-shuffled**: `20261111T213426_d63238`. Rules and inputs frozen before launch.

- **Terminal t041-shuffled — failed**: [[Lab run 20261111T213426_d63238 - T041]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t091-primary**: `20261111T224522_8f3457`. Rules and inputs frozen before launch.

- **Terminal t091-primary — failed**: [[Lab run 20261111T224522_8f3457 - T091]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t091-opposite**: `20261111T234634_6e195f`. Rules and inputs frozen before launch.

- **Terminal t091-opposite — failed**: [[Lab run 20261111T234634_6e195f - T091]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t091-random**: `20261112T003605_cffe5a`. Rules and inputs frozen before launch.

- **Terminal t091-random — needs_review**: [[Lab run 20261112T003605_cffe5a - T091]]. Native summary `{"robot": "XauUsdPreFomcDrift", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -12.2, "roi": -0.12, "trades": 9, "profit_factor": 0.94, "drawdown": 2.3702749558643625}`. Audit pending; terminal does not mean valid/passed.

- **Launched t091-nonfomc**: `20261112T003746_09f4f0`. Rules and inputs frozen before launch.

- **Terminal t091-nonfomc — needs_review**: [[Lab run 20261112T003746_09f4f0 - T091]]. Native summary `{"robot": "XauUsdPreFomcDrift", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -4.07, "roi": -0.04, "trades": 9, "profit_factor": 0.94, "drawdown": 0.6284825258595345}`. Audit pending; terminal does not mean valid/passed.

- **Launched t091-notrade**: `20261112T003927_25315a`. Rules and inputs frozen before launch.

- **Terminal t091-notrade — needs_review**: [[Lab run 20261112T003927_25315a - T091]]. Native summary `{"robot": "XauUsdPreFomcDrift", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-primary**: `20261112T004113_a5b447`. Rules and inputs frozen before launch.

- **Terminal t097-primary — needs_review**: [[Lab run 20261112T004113_a5b447 - T097]]. Native summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 49.62, "roi": 0.5, "trades": 262, "profit_factor": 1.02, "drawdown": 4.565499894004779}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-opposite**: `20261112T004319_25ea74`. Rules and inputs frozen before launch.

- **Terminal t097-opposite — needs_review**: [[Lab run 20261112T004319_25ea74 - T097]]. Native summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -349.72, "roi": -3.5, "trades": 262, "profit_factor": 0.85, "drawdown": 6.89434155981737}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-random**: `20261112T004545_0f5790`. Rules and inputs frozen before launch.

- **Terminal t097-random — failed**: [[Lab run 20261112T004545_0f5790 - T097]]. Native summary `{"code": "EXECUTION_FAILED", "message": "No verified native market report was produced.", "retryable": false, "next_action": "Inspect the native log and artifacts before planning another attempt."}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-constantclock**: `20261112T004621_d66a55`. Rules and inputs frozen before launch.

- **Terminal t097-constantclock — needs_review**: [[Lab run 20261112T004621_d66a55 - T097]]. Native summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 1.7, "roi": 0.02, "trades": 262, "profit_factor": 1, "drawdown": 5.0210422146658855}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-shuffled**: `20261112T004816_8b7cca`. Rules and inputs frozen before launch.

- **Terminal t097-shuffled — needs_review**: [[Lab run 20261112T004816_8b7cca - T097]]. Native summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -35.45, "roi": -0.35, "trades": 262, "profit_factor": 0.98, "drawdown": 5.826892016887082}`. Audit pending; terminal does not mean valid/passed.

- **Launched t097-adjacent**: `20261112T005213_ce5663`. Rules and inputs frozen before launch.

- **Terminal t097-adjacent — needs_review**: [[Lab run 20261112T005213_ce5663 - T097]]. Native summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -22.91, "roi": -0.23, "trades": 262, "profit_factor": 0.99, "drawdown": 5.254992475756665}`. Audit pending; terminal does not mean valid/passed.

- **Launched t004-observe**: `20261112T005459_4e9a4a`. Rules and inputs frozen before launch.

- **Terminal t004-observe — needs_review**: [[Lab run 20261112T005459_4e9a4a - T004]]. Native summary `{"robot": "XauUsdVolatilityReturnFeedback", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0, "observations": 492}`. Audit pending; terminal does not mean valid/passed.

- **Launched t011-observe**: `20261112T005645_9ba83d`. Rules and inputs frozen before launch.

## Live equity curve added to Gold Research Lab

The current-run panel now renders the cTrader observer's native balance/equity snapshots as a live line chart. Observer V2 now flushes timestamped snapshots to `equity-live.csv` inside each run directory, avoiding cTrader console buffering. The graph reports the sample count and latest timestamp and only connects observed samples. Observer-only derived packages preserve the canonical strategy source and rules; V2 grants file access only to write this run-scoped artifact. Verified during actual in-progress trading replays: the T091 primary curve updated from 5,369 snapshots at about 46% completion, and the T097 random curve from 10,314 snapshots at about 67%. Observation-only bots correctly remain flat when they place no orders. Jobs built before observer instrumentation cannot produce a live curve.

- **Terminal t011-observe — needs_review**: [[Lab run 20261112T005645_9ba83d - T011]]. Native summary `{"robot": "XauUsdEquityShockDecay", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0, "observations": 310}`. Audit pending; terminal does not mean valid/passed.

- **Launched t032-observe**: `20261112T005851_cd3ceb`. Rules and inputs frozen before launch.

- **Terminal t032-observe — needs_review**: [[Lab run 20261112T005851_cd3ceb - T032]]. Native summary `{"robot": "XauUsdJumpShareTailRisk", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0, "observations": 205}`. Audit pending; terminal does not mean valid/passed.

- **Launched t040-observe**: `20261112T010026_7e4bd7`. Rules and inputs frozen before launch.

- **Launched t040-observe**: `20261112T010026_7e4bd7`. Rules and inputs frozen before launch.

- **Terminal t040-observe — needs_review**: [[Lab run 20261112T010026_7e4bd7 - T040]]. Native summary `{"robot": "XauUsdRealizedSkewQuantile", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0, "observations": 169}`. Audit pending; terminal does not mean valid/passed.

- **Launched t056-observe**: `20261112T010407_3a32b1`. Rules and inputs frozen before launch.

- **Terminal t056-observe — needs_review**: [[Lab run 20261112T010407_3a32b1 - T056]]. Native summary `{"robot": "XauUsdSelfExcitingJumpHazard", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 0, "roi": 0, "trades": 0, "profit_factor": 0, "drawdown": 0, "observations": 211}`. Audit pending; terminal does not mean valid/passed.

- **Retry launched t041-primary**: `[[Lab run 20261112T010753_4d4e1f - T041]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t041-primary — needs_review**: [[Lab run 20261112T010753_4d4e1f - T041]]. Summary `{"robot": "XauUsdEntropySignPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -285.07, "roi": -2.85, "trades": 93, "profit_factor": 0.44, "drawdown": 3.0540340621527853}`. Independent audit still required.

- **Retry launched t041-opposite**: `[[Lab run 20261112T011157_d7c5c2 - T041]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t041-opposite — needs_review**: [[Lab run 20261112T011157_d7c5c2 - T041]]. Summary `{"robot": "XauUsdEntropySignPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 85.68, "roi": 0.86, "trades": 93, "profit_factor": 1.29, "drawdown": 0.757478011104116}`. Independent audit still required.

- **Retry launched t041-shuffled**: `[[Lab run 20261112T011358_8d0f92 - T041]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t041-shuffled — needs_review**: [[Lab run 20261112T011358_8d0f92 - T041]]. Summary `{"robot": "XauUsdEntropySignPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -48.85, "roi": -0.49, "trades": 92, "profit_factor": 0.87, "drawdown": 1.5831394995248649}`. Independent audit still required.

- **Retry launched t091-primary**: `[[Lab run 20261112T011549_bd2b8b - T091]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t091-primary — needs_review**: [[Lab run 20261112T011549_bd2b8b - T091]]. Summary `{"robot": "XauUsdPreFomcDrift", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 122.08, "roi": 1.22, "trades": 9, "profit_factor": 1.91, "drawdown": 1.6855398838332103}`. Independent audit still required.

- **Retry launched t091-opposite**: `[[Lab run 20261112T011811_90f2ea - T091]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t091-opposite — needs_review**: [[Lab run 20261112T011811_90f2ea - T091]]. Summary `{"robot": "XauUsdPreFomcDrift", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": -187.26, "roi": -1.87, "trades": 9, "profit_factor": 0.18, "drawdown": 1.8723056169168646}`. Independent audit still required.

- **Retry launched t097-random**: `[[Lab run 20261112T012011_a5a2f1 - T097]]`. Frozen parameters and inputs unchanged; V2 only streams live equity snapshots.

- **Retry terminal t097-random — needs_review**: [[Lab run 20261112T012011_a5a2f1 - T097]]. Summary `{"robot": "XauUsdSameClockPersistence", "symbol": "XAUUSD", "period": "1y 4m 1d (23/06/2025 - 24/10/2026)", "data_mode": "tickDataFromServer", "net": 283.92, "roi": 2.84, "trades": 262, "profit_factor": 1.13, "drawdown": 3.39467417600833}`. Independent audit still required.


## Final batch closeout — 2026-11-12

All workaroundable candidates are terminal. The 53 runnable presets completed as `needs_review`; six initial CLI/cBot interruptions were retried with unchanged frozen strategy parameters and V2 observer instrumentation. T082 remains blocked for lack of audited broker-session inputs. No strategy is promoted. Order-flow and options work remains outside this batch.

Run window: 2025-06-23 through 2026-10-24; XAUUSD tick history from server, $10,000 initial equity. Requested 1-pip spread is not confirmed by the native report, so every outcome remains provisional pending cost/evidence audit.

### Strategy summary

| Strategy | Presets | Result summary |
|---|---:|---|
| T001 | 3 | t001-follow: -93.42, PF 0.16, n=9; t001-opposite: +80.63, PF 4.80, n=9; t001-random: -23.55, PF 0.69, n=9 |
| T003 | 9 | t003-tokyo-follow: -239.72, PF 0.79, n=290; t003-tokyo-opposite: -148.50, PF 0.86, n=290; t003-tokyo-random: -566.26, PF 0.56, n=290; t003-london-follow: -491.35, PF 0.61, n=322; t003-london-opposite: +108.76, PF 1.12, n=322; t003-london-random: -426.50, PF 0.64, n=322; t003-ny-follow: +376.14, PF 1.24, n=331; t003-ny-opposite: -801.55, PF 0.63, n=331; t003-ny-random: +378.64, PF 1.24, n=331 |
| T004 | 1 | t004-observe: 492 obs |
| T007 | 2 | t007-baseline: +24.70, PF 1.01, n=263; t007-gated: +573.72, PF 1.32, n=263 |
| T011 | 1 | t011-observe: 310 obs |
| T018 | 2 | t018-baseline: +24.70, PF 1.01, n=263; t018-gated: -398.90, PF 0.91, n=263 |
| T022 | 4 | t022-follow: -312.01, PF 0.82, n=305; t022-opposite: -90.77, PF 0.94, n=305; t022-long: -164.89, PF 0.90, n=305; t022-random: -380.15, PF 0.79, n=305 |
| T023 | 4 | t023-follow: -90.23, PF 0.90, n=323; t023-opposite: -313.12, PF 0.69, n=323; t023-long: -111.05, PF 0.88, n=323; t023-random: -149.85, PF 0.84, n=323 |
| T032 | 1 | t032-observe: 205 obs |
| T037 | 3 | t037-primary: +0.00, PF 0.00, n=0; t037-opposite: +0.00, PF 0.00, n=0; t037-random: +0.00, PF 0.00, n=0 |
| T039 | 2 | t039-baseline: +143.44, PF 1.09, n=263; t039-gated: -4.76, PF 1.00, n=263 |
| T040 | 1 | t040-observe: 169 obs |
| T041 | 4 | t041-primary: -285.07, PF 0.44, n=93; t041-opposite: +85.68, PF 1.29, n=93; t041-random: +51.54, PF 1.16, n=93; t041-shuffled: -48.85, PF 0.87, n=92 |
| T056 | 1 | t056-observe: 211 obs |
| T080 | 4 | t080-primary: -1507.62, PF 0.86, n=940; t080-opposite: -1625.90, PF 0.85, n=1007; t080-random: -1906.20, PF 0.83, n=975; t080-placebo: -610.80, PF 0.94, n=881 |
| T091 | 5 | t091-primary: +122.08, PF 1.91, n=9; t091-opposite: -187.26, PF 0.18, n=9; t091-random: -12.20, PF 0.94, n=9; t091-nonfomc: -4.07, PF 0.94, n=9; t091-notrade: +0.00, PF 0.00, n=0 |
| T097 | 6 | t097-primary: +49.62, PF 1.02, n=262; t097-opposite: -349.72, PF 0.85, n=262; t097-random: +283.92, PF 1.13, n=262; t097-constantclock: +1.70, PF 1.00, n=262; t097-shuffled: -35.45, PF 0.98, n=262; t097-adjacent: -22.91, PF 0.99, n=262 |

Six transient native-run interruptions were recovered: T041 primary/opposite/shuffled, T091 primary/opposite, and T097 random. Original failed attempts remain in the batch ledger; the V2 retries are the selected terminal records. Each retry has an Obsidian run note linked above in its individual launch/terminal entry.

T014 and T038 remain excluded from this batch because the required verified auction/calendar evidence and SSRN full-text rule audit were unavailable. T082 could be expressed as an observation study but no audited broker session-hour/coverage input satisfied its frozen requirements, so it was not run.

Live equity: the Gold Research Lab current-run panel now streams actual account-equity snapshots to a canvas curve during replay using the V2 observer. Verified on an active trade run: the curve updated from 5,369 snapshots while T091 primary was around 46% complete, and 10,314 snapshots while T097 random was around 67% complete. Chart samples are account snapshots; flat curves for observation-only bots are expected. The web app is served locally at `http://127.0.0.1:8765`.

Machine-readable outcome ledger: `outputs/workaround-batch-20261111/final-summary.json`.
