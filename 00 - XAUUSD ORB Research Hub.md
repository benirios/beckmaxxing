---
tags:
  - xauusd
  - orb
  - quantitative-research
status: active
updated: 2026-11-11
---

# XAUUSD ORB Research Hub

> [!success] Intraday hypothesis collection — 100 / 100
> Completed 2026-11-07. [[01 - Research/Thesis Index - 100 hypotheses|The index]]
> links 100 distinct research hypotheses, with sources, predictions and
> limitations in [[01 - Research/Thesis Catalog|the catalog]]. T012 is retired
> as a duplicate. The count includes risk/execution questions and negative
> results; it does not establish profitable strategies.

> [!abstract] Primary objective
> Determine whether any XAUUSD Opening Range Breakout variant demonstrates a statistically defensible and economically meaningful edge after realistic transaction costs.

This vault is both the research notebook and the executable Python repository. Negative findings are valid findings. No strategy is considered profitable until it survives out-of-sample testing, realistic execution costs, walk-forward analysis, and robustness checks.

## Navigation

### Research

- [[01 - Research/Research Brief|Research brief and success criteria]]
- [[01 - Research/Evidence Register|Evidence register]]
- [[01 - Research/Evidence Methodology Audit - 2026-10-27|Evidence methodology audit]]
- [[01 - Research/Research Hypotheses|Research hypotheses]]
- [[01 - Research/Intraday Session Microstructure Hypotheses|Intraday session microstructure hypotheses]]
- [[01 - Research/Intraday Thesis Scan - 2026-11-03|Macro, price-discovery, and session thesis scan]]
- [[01 - Research/London PM Gold Auction Opening - hypothesis 2026-11-04|London PM auction opening hypothesis (untested)]]
- [[01 - Research/FOMC Easing Surprise Continuation - hypothesis 2026-11-04|FOMC easing-surprise hypothesis (untested)]]
- [[01 - Research/Post-Jump Spread Recovery - hypothesis 2026-11-04|Post-jump spread recovery hypothesis (untested)]]
- [[01 - Research/Thesis Catalog|100-thesis catalog]]
- [[01 - Research/Thesis Index - 100 hypotheses|100-hypothesis index and completion audit]]

### Methodology

- [[02 - Methodology/Data Acquisition and Provenance|Data acquisition and provenance]]
- [[02 - Methodology/How We Test Strategies in cTrader|How we test strategies in cTrader]]
- [[02 - Methodology/Session and Execution Rules|Session and execution rules]]
- [[02 - Methodology/Validation Protocol|Validation protocol]]

### Implementation

- [[03 - Implementation/Baseline Strategy Specification|Baseline strategy specification]]
- [[03 - Implementation/cTrader cBot - NY15 Baseline|cTrader cBot — NY15 baseline]]
- [[03 - Implementation/cTrader cBot - GC Close Controls|cTrader cBot — GC close controls]]
- [[03 - Implementation/cTrader cBot - GC Daytime Short|cTrader cBot — GC daytime short]]
- [[03 - Implementation/cTrader cBot - GLD R5 Close Momentum|cTrader cBot — GLD R5 close momentum]]
- [[03 - Implementation/cTrader cBot - Extreme Move Reversal|cTrader cBot — extreme move reversal]]
- [[03 - Implementation/cTrader cBot - London RSI ORB|cTrader cBot — London RSI ORB]]
- [[03 - Implementation/GC Futures Replication Engine|GC futures replication engine]]
- [[03 - Implementation/Repository Architecture|Repository architecture]]
- [[README|Run instructions]]
- [[docs/research-design|Detailed technical research design]]

### Planning and records

- [[04 - Planning/Research Roadmap|Research roadmap]]
- [[04 - Planning/Decision Log|Decision log]]
- [[04 - Planning/Experiment Register|Experiment register]]
- [[04 - Planning/Backtest Lab Results|Platform strategy/result ledger]]
- [[04 - Planning/Remaining strategy execution log - 2026-11-11|Remaining strategy execution and missing-input log]]
- [[Templates/Experiment Note Template|Experiment note template]]

## Current state

| Area | Status | Evidence |
|---|---|---|
| Intraday hypothesis collection | Complete: 100 distinct entries; 101 IDs issued, T012 retired; no new backtests | [[01 - Research/Thesis Index - 100 hypotheses]] |
| Repository architecture | Complete | [[03 - Implementation/Repository Architecture]] |
| M1 midpoint baseline | Implemented | `src/xau_orb/` |
| Automated tests | 5 passing | `tests/` |
| Final-test leakage guard | Implemented | `--include-final-test` required |
| cTrader NY15 cBot | Negative result verified from report | [[03 - Implementation/cTrader cBot - NY15 Baseline]] |
| EXP-002 London failed-breakout cBot | Corrected-build run had zero protection failures but account equity reached zero by 2024-02-20; requested period incomplete | [[03 - Implementation/cTrader cBot - London Failed Breakout]] |
| EXP-003 NY30 cBot | -63.67% ROI; PF 0.69; every represented year negative; protection integrity passed | [[outputs/EXP-003-ny30-2026-10-30/RUN-NOTE]] |
| EXP-004 NY5 cBot | -92.22% ROI; PF 0.55; materially worse than NY15 and NY30; protection integrity passed | [[outputs/EXP-004-ny5-2026-10-30/RUN-NOTE]] |
| EXP-005 NY30 P70 width filter | Rejected: -9.94% ROI; PF 0.86; 456 trades; filter and protection integrity passed | [[outputs/EXP-005-ny30-width-p70-2026-11-01/RUN-NOTE]] |
| EXP-006 NY15 first-touch retest | Rejected: -45.90% ROI; PF 0.66; 672 trades; every year negative; state and protection integrity passed | [[outputs/EXP-006-ny15-retest-2026-11-01/RUN-NOTE]] |
| EXP-007 NY15 immediate stop entry | Rejected: -75.83% ROI; PF 0.69; 1,652 trades; every year negative; trigger and protection integrity passed | [[outputs/EXP-007-ny15-immediate-stop-2026-11-01/RUN-NOTE]] |
| EXP-008 GC-close intraday momentum | Corrected V2 rejected: -55.72% ROI; PF 0.27; 1,626 trades; gross negative in both directions and every year; session integrity passed | [[outputs/EXP-008-gc-close-momentum-v2-2026-11-02/RUN-NOTE]] |
| EXP-009 matched direction controls | Complete, 4/4 modes. Momentum -55.72%, PF 0.27 and worse than long, short, and fixed-seed random controls; exact EXP-008 economic-ledger reproduction | [[outputs/EXP-009-exp008-momentum-2026-11-02/RUN-NOTE]] |
| EXP-010 actual GC futures replication | Deprecated before any market run; no market data supplied and no split exposed | [[03 - Implementation/GC Futures Replication Engine]] |
| EXP-011 GC daytime short | Rejected: primary short -44.04% ROI, PF 0.82, six of seven years negative; better than long control but zero-spread net still -$136.39 | [[outputs/EXP-011-short-day-2026-11-02/RUN-NOTE]] |
| EXP-012 GLD R5 close momentum | Rejected: -52.32% ROI, PF 0.27, every year negative; predictor/target raw correlation -0.015 and zero-spread net -$997.06 | [[outputs/EXP-012-gld-r5-primary-2026-11-02/RUN-NOTE]] |
| EXP-013 extreme-move reversal | Rejected: -50.52% ROI, PF 0.45, 1,117 trades; gross negative in every year and both directions; execution integrity passed | [[outputs/EXP-013-extreme-move-reversal-2026-11-03/RUN-NOTE]] |
| EXP-014 London RSI ORB | Rejected: -14.65% ROI; PF 0.77; 1,185 trades; every year net negative; execution integrity passed | [[outputs/EXP-014-london-rsi-orb-2026-11-03/RUN-NOTE]] |
| Dukascopy ingestion pipeline | Implemented; 10 automated tests passing | [[docs/dukascopy-ingestion]] |
| Historical data acquisition | Not started; authenticated provider access needed | [[02 - Methodology/Data Acquisition and Provenance]] |
| cTrader six-year run | -74.74% ROI; 75.84% max balance drawdown | [[04 - Planning/Experiment Register#EXP-001 — cTrader NY15 baseline|EXP-001]] |
| Available-history tick batch | T002 +$25.18; T006 +$322.77; T015 primary -$330.85; placebo +$82.42. Exploratory; spread/cost and execution review limits; 94 hypotheses still missing inputs | [[04 - Planning/Remaining strategy execution log - 2026-11-11]] |
| Walk-forward / Monte Carlo | Planned | [[02 - Methodology/Validation Protocol]] |
| Live cTrader deployment | Deferred | Only after a robust edge is established |

## Research guardrails

1. Never manufacture, fill, or silently repair missing market data.
2. Treat every cost assumption as a scenario, not a broker fact.
3. Keep signal prices separate from executable fill prices.
4. Do not select parameters using the final-test period.
5. Record negative results and rejected hypotheses.
6. Distinguish XAUUSD spot evidence from COMEX Gold futures evidence.
7. Use timezone-aware timestamps and IANA session zones.

## Immediate next action

Four available-history native tick replays have been recorded and reviewed with
explicit limitations. See [[04 - Planning/Remaining strategy execution log - 2026-11-11]]
for results, native evidence and the missing inputs for 94 remaining hypotheses.
Supply audited datasets before those hypotheses can run. Resolve execution and
cost review issues and freeze matched controls before validating positive
primaries. Original six-year requests remain incomplete. No strategy is promoted.
