---
tags: [experiments, xauusd, orb]
status: active
---

# Experiment Register

This register counts every executed strategy variant, including failures. Create one linked note per run from [[../Templates/Experiment Note Template]].

| ID | Date | Hypothesis | Configuration | Data manifest | Split exposed | Result | Note |
|---|---|---|---|---|---|---|---|
| EXP-000 | 2026-10-25 | Pipeline verification | baseline NY 09:30 | synthetic unit tests only | none | 5 tests passed; no market result | [[../03 - Implementation/Baseline Strategy Specification]] |
| EXP-001 | 2026-10-25 | H2 NY 15-minute ORB | cTrader NY15, close-confirmed, 1R, 0.5% equity risk | FTMO Platform XAUUSD M1 server bars | 2020-05-06–2026-10-24; includes 2022+ holdout | -74.74% ROI; -75.84% max balance DD; 1,653 trades; PF 0.70 | [[../03 - Implementation/cTrader cBot - NY15 Baseline]] |
| EXP-002 | 2026-10-25 onward | H3 London failed-breakout reversal | 08:00 London 30m range; first failed close breakout; opposite-range target | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission setting | 2020-05-06–2024-02-20 exposed; requested end 2026-10-24 not reached | First run invalid (205 protection failures). Corrected-build partial run: 828 closed trades, -100.01% ROI, PF 0.48; equity reached zero; zero protection failures | [[../03 - Implementation/cTrader cBot - London Failed Breakout]] |
| EXP-003 | 2026-10-30 | Comparative NY 30-minute ORB baseline | 09:30 New York 30m range; M1 close confirmation; next-bar market entry; opposite-boundary stop; 1R target; 16:00 exit; 0.5% equity risk | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed | -63.67% ROI; 65.36% max balance DD; 1,558 trades; PF 0.69; all years negative; no missing protections | [[../outputs/EXP-003-ny30-2026-10-30/RUN-NOTE]] |
| EXP-004 | 2026-10-30 | H4 five-minute New York ORB benchmark | 09:30 New York 5m range; M1 close confirmation; next-bar market entry; opposite-boundary stop; 1R target; 16:00 exit; 0.5% equity risk | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed | -92.22% ROI; 92.98% max balance DD; 1,663 trades; PF 0.55; no missing protections | [[../outputs/EXP-004-ny5-2026-10-30/RUN-NOTE]] |
| EXP-005 | 2026-11-01 | H1 volatility-filtered NY30 ORB | EXP-003 rules; trade only when current NY30 range width is at/above nearest-rank P70 of previous 60 valid sessions; 60-session warmup | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed | Rejected: -9.94% ROI; 11.09% max balance DD; 456 trades; PF 0.86; negative expectancy; protection/filter integrity passed | [[../outputs/EXP-005-ny30-width-p70-2026-11-01/RUN-NOTE]] |
| EXP-006 | 2026-11-01 | H2 NY15 breakout-retest ORB | First M1 close breakout fixes direction; first later boundary touch must close back outside; next-bar entry; opposite-boundary stop; 1R target; 12:00 entry deadline; 16:00 exit; 0.5% equity risk | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed | Rejected: -45.90% ROI; 47.02% max balance DD; 672 trades; PF 0.66; every year negative; state/protection integrity passed | [[../outputs/EXP-006-ny15-retest-2026-11-01/RUN-NOTE]] |
| EXP-007 | 2026-11-01 | H2 NY15 immediate stop-entry benchmark | First bid beyond either NY15 boundary triggers immediate protected market entry and consumes synthetic OCO; opposite-boundary stop; 1R target; 12:00 deadline; 16:00 exit; 0.5% equity risk | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed | Rejected: -75.83% ROI; 76.56% max balance DD; 1,652 trades; PF 0.69; every year negative; trigger/protection integrity passed | [[../outputs/EXP-007-ny15-immediate-stop-2026-11-01/RUN-NOTE]] |
| EXP-008 | 2026-11-02 | H6 COMEX-close intraday momentum | Sign of prior 13:30 close-to-current 13:00 return; enter same direction at 13:00; exit 13:30; 1.0× equity notional; no stop/target | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; previously exposed; two invalid V1 attempts preserved | V2 rejected: -55.72% ROI; 55.78% max balance DD; 1,626 trades; PF 0.27; gross negative in both directions and every year; session integrity passed | [[../outputs/EXP-008-gc-close-momentum-v2-2026-11-02/RUN-NOTE]] |
| EXP-009 | 2026-11-02 | Matched direction controls for EXP-008 | Four separate runs: EXP-008 momentum, always long, always short, fixed-seed deterministic random; common 13:00–13:30 window and EXP-008 eligibility; 1.0× equity notional | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | Same fully exposed 2020-05-06–2026-10-24 interval | Complete, 4/4 modes. Momentum -55.72%, PF 0.27; long -55.54%, PF 0.29; short -51.17%, PF 0.32; random -50.48%, PF 0.33. Momentum exactly reproduced EXP-008 and was worse than every control | [[../outputs/EXP-009-exp008-momentum-2026-11-02/RUN-NOTE]] |
| EXP-010 | deprecated 2026-11-02 | Source-market replication of GC close momentum | Python futures engine; cancelled because experiments are executed as cTrader cBots | No licensed market data supplied | None; no market split exposed | Deprecated before any market run; synthetic tests are not trading evidence | [[../03 - Implementation/GC Futures Replication Engine]] |
| EXP-011 | completed 2026-11-02 | H7 negative COMEX daytime-return anomaly transfers to XAUUSD | Short at exactly 08:20 New York; exit exactly 13:30; no stop/target; 1.0x equity notional; matched always-long control | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; all dates previously exposed | Rejected: `ShortDay` -44.04% ROI, PF 0.82, 1,670 trades, six of seven years negative; better than `LongControl` (-67.03%, PF 0.75) but fails profitability gates. Zero-spread net still -$136.39 | [[../outputs/EXP-011-short-day-2026-11-02/RUN-NOTE]] |
| EXP-012 | completed 2026-11-02 | H8 GLD fifth-half-hour momentum transfers to XAUUSD's final NYSE half-hour | Sign of 11:30–12:00 New York return; enter same direction exactly 15:30; exit 16:00; 30 complete predictor bars; 1.0x equity notional; no stop/target | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; all dates previously exposed | Rejected: -52.32% ROI, PF 0.27, 1,616 trades, every year and both directions negative; raw predictor/target correlation -0.015 and zero-spread net -$997.06. Controls not warranted because primary fails absolute gates | [[../outputs/EXP-012-gld-r5-primary-2026-11-02/RUN-NOTE]] |
| EXP-013 | completed 2026-11-03 | H9 extreme 30-minute XAUUSD move reverses | At 10:00, fade 09:30–10:00 move if magnitude >=2x prior 20 completed M1 ATR; exit 10:30; one trade/day; 1x equity; no stop/target | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; all dates exposed | Rejected: -50.52% ROI, PF 0.45, 1,117 trades, every year and both directions negative; gross after spread -$4,316.04; all positions held exactly 30 minutes | [[../outputs/EXP-013-extreme-move-reversal-2026-11-03/RUN-NOTE]]; invalid first attempt: [[../outputs/EXP-013-invalid-wrong-bot-2026-11-03/RUN-NOTE]] |
| EXP-014 | completed 2026-11-03 | H5 London 30-minute ORB with RSI agreement | Exact 08:00–08:29 London range; $0.60 minimum; one-tick breakout from 08:30–11:59:59; latest completed Wilder M30 RSI(14) must agree across 50; opposite-boundary stop; no target; 15:00 exit; fixed 1 unit | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission | 2020-05-06–2026-10-24; all dates previously exposed | Rejected: -14.65% ROI; PF 0.77; 1,185 trades; every year net negative; execution integrity passed | [[../outputs/EXP-014-london-rsi-orb-2026-11-03/RUN-NOTE]] |
| EXP-015 | development rejected 2026-11-03 | Asia rebound after negative U.S. session | Locked conditional long, 20-window median-range stop, 20:00–02:00 New York, matched controls | EV Trading Labs/Dukascopy-attributed M5 bid/ask, 2018–2023 used | 2018–2023 development tested; 2024–2025 reserved and untested | Rejected: 588 trades, −$0.6204/oz net mean, PF 0.7152; five of six years negative; −$282.875/oz total even before commission and slippage | [[../outputs/EXP-015-asia-rebound-screen/RUN-NOTE]] |
| EXP-016 | development rejected 2026-11-03 | Older gold overnight-positive/daytime-negative finding transfers to weekday XAUUSD | 18:05–08:20 New York unconditional one-ounce long; 1% protective stop; same-date short control; observed bid/ask and modeled fees | EV Trading Labs/Dukascopy-attributed M5 bid/ask | 2018–2023 development tested; 2024–2025 reserved and untested | Rejected: 1,235 trades, -$0.2344/oz net mean, PF 0.9296, four of six years negative; gross after observed spread -$116.8216/oz | [[../outputs/EXP-016-overnight-gold-screen/RUN-NOTE]] |
| EXP-017 | preregistered 2026-11-10 | T002 Tokyo-open price-pressure fade | Frozen Fade mode; prior60 P90; prior20 median-range stop; 1 unit; 09:30–10:00 Tokyo | FTMO server ticks, available-history replication; spread metadata null | Requested 2020-05-06–2026-10-24, already exposed development dates | V1 invalid; V2 M1 negative. 2025-06-23–2026-10-24 tick replication: +$25.18, 41 trades, PF 1.10; under review (log gaps); original six-year tick request incomplete | [[EXP-017 - Tokyo Pressure Fade]]; [[../outputs/EXP-017-tokyo-fade-v2-m1-2026-11-10/RUN-NOTE]] |

## Newly allocated runs — 2026-11-11

The available-history native runs below executed on 2026-11-11. Their shorter 2025-06-23–2026-10-24 window does not complete the original six-year request; spread metadata is null and all results remain exploratory. See [[Remaining strategy execution log - 2026-11-11]] for exact evidence and blockers.
The full outstanding source/input inventory is [[Remaining cTrader Strategy Runs - 2026-11-11]].

| ID | Hypothesis | Status | Specification |
|---|---|---|---|
| EXP-018 | T006 equity-shock gold response | Available-history ticks: +$322.77, 195 trades, PF 1.27; reference/timing/cost review and controls pending | [[EXP-018 - Equity Shock Gold Response]] |
| EXP-019 | T015 round-number rejection and placebo | Available-history ticks: primary -$330.85 / 73 trades / PF 0.53; placebo +$82.42 / 86 trades / PF 1.19; negative primary, review limits retained | [[EXP-019 - Round Number Rejection]] |

## EXP-002 — London failed-breakout reversal

**Status:** first cTrader run invalid due to execution defects. A subsequent corrected-build attempt had no protection-modification failures but depleted the account before the requested end date. Its early time-exit losses also exceed the logged planned stop risk materially, so execution integrity remains unresolved. Preserve both runs; the later result is a severe negative partial-period observation, not a completed full-period evaluation. The supplied cTrader directories do not contain underlying market bars for independent Python replication.

The first variant is fixed in [[../01 - Research/Research Hypotheses#H3 — London failed-breakout reversal|H3]]. Do not tune its range length, session, cutoff, stop, target, or costs after inspecting this run. If only M1 OHLC is available, label it exploratory and resolve intrabar stop/target ambiguity stop-first. Bid/ask ticks or bars are preferred for execution-cost modeling.

### Preliminary run received 2026-10-25 — invalid

The log confirms the London failed-breakout cBot ran (UK time zone; `LFB_*` events), despite the stale `PositionLabel` value `XAU_NY15_ORB_V1` in `parameters.cbotset`.

| Field | Reported result |
|---|---:|
| Broker / symbol / data | FTMO Platform / XAUUSD / M1 bars from server, not tick replay |
| Requested period | 2020-05-06–2026-10-24 |
| Actual last event | 2024-02-07; cBot aborted when equity fell below zero |
| Starting balance / ending balance | $10,000 / $1.14 |
| Ending equity / ROI | -$0.10 / -100% |
| Maximum balance / equity drawdown | 99.99% / 100.00% |
| Closed trades | 822 (134 wins, 688 losses; 16.30% win rate) |
| Profit factor / average trade | 0.14 / -$12.16 |
| Closed-trade net P/L / commissions | -$9,998.86 / -$1,874.10 |
| Long / short net P/L | -$2,257.30 / -$7,741.56 |
| Protection modification failures | 205 `InvalidStopLossTakeProfit` events |

The 205 failures followed a successful market order with initial stop/target distances; the cBot then tried to modify those protections to exact prices. Failed modifications triggered immediate market closes. That materially corrupted the tested execution path. The run also ended before its requested 2026 end date. **Do not infer that H3 itself has a -100% edge from this run.** The report is retained as an invalid implementation run, not a valid strategy result.

The cBot source has been corrected to keep the stop and target attached by the initial market order and avoid immediate absolute-price modifications. The new partial run has zero protection failures, but it still stopped when account equity reached zero on 2024-02-20. Its `.cbotset` still uses the NY15 position label, while the log's London `LFB_*` events confirm the strategy code ran. Treat 2020–2024 as exposed for any tuning. The requested period remains incomplete; any further run must use the corrected cBot build and correct the label, and must not be described as untouched out-of-sample evidence.

Archived input bundle: `outputs/EXP-002-invalid-2026-10-25/` (`report.html`, `events.json`, `log.txt`, `parameters.cbotset`). SHA-256: report `9d6bb69c90e36617fa9b129bd3e54c9c8c56b4f35a176e35381e7a70f5d43b25`; events `046199884c76b78c27a505c05cc22d96d5afac32f2b6459319a7e5e769b85cda`; log `1493a78d8ef9a5ffdf3b24c8a98438b918a359d7c7cfc66627525db0102f2d6d`; parameter set `9f6e9c7d3462ef40954e664353c109eadc83c5f90af62b9b9617731f2e71e057`.

The subsequently refreshed cTrader files were byte-for-byte identical to this archived bundle, so they were the same failed run rather than a fresh corrected run. The active cTrader source project was still on the old implementation. It has now been synced and rebuilt, and a separate bot named `XAUUSD ORB London Failed Breakout` was published to `/Users/beni/cAlgo/Sources/Robots/XAUUSD ORB London Failed Breakout.algo` to make the corrected version unambiguous in cTrader.

### Corrected-build partial run received 2026-10-27

The run was launched from the same cTrader backtesting folder. It is distinct from the first failed bundle: report, events, and log hashes differ. The strategy logs contain London `LFB_*` events and no stop-protection modification or order-failure events, so the original implementation defect did not recur. However, the run still aborted when account equity reached zero on 2024-02-20, leaving 2024-02-20 through the requested 2026-10-24 end date untested.

| Field | Result |
|---|---:|
| Starting / ending balance | $10,000 / $0.53 |
| Ending equity / ROI | -$0.71 / -100.01% |
| Max balance / equity drawdown | 99.9947% / 100.0072% |
| Closed trades | 828 (225 wins, 603 losses; 27.17% win rate) |
| Profit factor / average trade | 0.48 / -$12.08 |
| Closed trade net P/L / commissions | -$9,999.47 / -$2,276.82 |
| Long / short net P/L | -$2,902.11 / -$7,097.36 |
| Last log event | 2024-02-20 08:32 UTC; equity reached zero |
| Protection modification failures | 0 |
| Order failures / signal rejections | 0 / 6 |
| Data / costs | FTMO XAUUSD M1 server bars; fixed spread 1; $30/million commission |
| Saved position label | `XAU_NY15_ORB_V1` (stale; London `LFB_*` events confirm code path) |

All represented calendar years are negative. The largest closed trade loss is `$1,080.91`; the maximum losing streak is 16 trades overall. Interpret as a severe negative partial-period result from the corrected execution path, with config-label and early-stop caveats. Since this interval was already exposed by earlier EXP-002 inspection, it is not an untouched holdout.

Archived unchanged bundle: `outputs/EXP-002-partial-2026-10-27/` (`report.html`, `events.json`, `log.txt`, `parameters.cbotset`, `RUN-NOTE.md`). The note contains the full SHA-256 hashes, yearly breakdown, and the stop/risk discrepancy requiring investigation.

## EXP-001 — cTrader NY15 baseline

### Verified backtest result

Source files supplied from `/Users/beni/cAlgo/Data/cBots/XAUUSD ORB/4da7d15d-f4cb-46d8-a161-1aea3da4ea93-Default/Backtesting/` (`report.html`, `events.json`, `log.txt`, `parameters.cbotset`).

| Field | Result |
|---|---:|
| Broker / symbol | FTMO Platform / XAUUSD |
| Test period | 2020-05-06 to 2026-10-24 (6y 5m 18d) |
| Data | M1 bars from server; not tick replay |
| Starting / ending balance | $10,000 / $2,525.72 |
| Net profit / ROI | -$7,474.28 / -74.74% |
| Maximum balance / equity drawdown | 75.84% / 76.01% |
| Trades / wins / losses | 1,653 / 711 / 942 |
| Win rate / profit factor | 43.01% / 0.70 |
| Average trade | -$4.52 |
| Long / short net profit | -$3,507.25 / -$3,967.03 |
| Commission / swaps | -$1,701.76 / -$173.92 |
| Strategy parameters | Target 1R; 0.5% equity risk; percent-equity sizing; live disabled |
| Backtest friction | Fixed spread setting 1; commission setting $30 per million USD volume |

Every calendar year represented in the report is net negative. The trade history's gross P/L is -$5,598.60; reported commissions and swaps total another -$1,875.68. Thus, commissions and swaps worsen the result, but they do not explain it by themselves: gross performance was already substantially negative.

Yearly net P/L from the report's closed-trade history:

| Year | Trades | Win rate | Net P/L |
|---|---:|---:|---:|
| 2020 (partial) | 169 | 46.7% | -$691.45 |
| 2021 | 253 | 40.3% | -$2,094.86 |
| 2022 | 258 | 43.4% | -$1,401.89 |
| 2023 | 252 | 37.7% | -$1,645.51 |
| 2024 | 256 | 42.2% | -$831.04 |
| 2025 | 255 | 45.5% | -$360.24 |
| 2026 (through Oct 24) | 210 | 47.1% | -$449.29 |

**Conclusion:** reject this tested NY15 close-confirmed, 1R configuration as implemented on this FTMO XAUUSD feed. Do not optimize against these results. The run includes 2022–2026, so the previously reserved final-test era has now been exposed and cannot serve as an untouched holdout for subsequent tuning.

**Execution caveats:** the run used M1 server bars rather than tick data, with a fixed spread setting; it is therefore not a tick-level execution audit. The log also records two `InvalidStopLossTakeProfit` failures while moving protection to the opening-range boundary (July 4, 2023 and February 19, 2024); those positions were immediately closed. Review these two trades separately, although they are too few to explain the overall loss.

The report, event stream, parameters, and log should be retained with the experiment artifacts if the cTrader working directory is later cleaned. The original source path is recorded above.

## Rules

1. Allocate the ID before running the experiment.
2. Link the exact configuration and data manifest.
3. Record every exposed split.
4. Preserve the trade ledger and summary artifacts.
5. Never rewrite a failed run into a different hypothesis after seeing its outcome.
6. Mark any chronological partition as exposed once its results have been inspected; do not call it an untouched holdout afterward.

