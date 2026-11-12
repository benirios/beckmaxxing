---
tags: [roadmap, xauusd, orb]
status: active
---

# Research Roadmap

## Phase 1 — Verify external evidence

- [ ] Retrieve full versions of the cited papers and studies.
- [ ] Verify session definitions, data provenance, costs, and statistical tests.
- [x] Classify evidence as peer-reviewed, preprint, or independent backtest.
- [x] Record any methodology that cannot be reproduced.

## Phase 2 — Acquire and audit data

- [ ] Download two small Dukascopy XAU/USD tick samples.
- [ ] Verify XAUUSD price scale, timestamps, and bid/ask ordering.
- [x] Implement immutable raw-file manifest with SHA-256 hashes.
- [x] Implement deterministic aggregation into bid, ask, and midpoint M1 bars.
- [x] Implement gap, spread, stale-quote, and price-jump diagnostics.
- [ ] Acquire the full approved history only after sample validation.
- [ ] Acquire actual GC futures data with documented continuous-contract and
  08:20–13:30 New York session handling.

## Phase 3 — Complete the baseline engine

- [x] Implement timezone-aware 09:30 15-minute baseline.
- [x] Apply explicit spread, slippage, and commission.
- [x] Resolve same-bar ambiguity conservatively.
- [x] Reject missing required bars.
- [x] Lock the final-test period by default.
- [ ] Add monthly/yearly and volatility-regime reports.
- [ ] Add cost-grid orchestration.
- [ ] Add tick-replay execution.
- [x] Generate the NY15 cTrader research cBot.
- [ ] Correlate cTrader tick-data trades against the Python ledger.
- [x] Implement contract-aware GC futures source-market replication engine.

## Phase 4 — Establish comparative baselines

- [ ] Run 5, 15, 30, and 60-minute ORBs under identical assumptions.
- [ ] Compare London, 08:20 New York, and 09:30 New York anchors.
- [ ] Compare immediate, close-confirmed, and retest entries.
- [ ] Add matched time-of-day random-entry controls.
- [x] Complete matched 13:00–13:30 direction controls for H6 (EXP-009).

## Phase 5 — Test preregistered improvements

- [ ] H1: volatility-filtered 30-minute ORB.
- [ ] H2: New York 15-minute entry variants.
- [ ] H3: London failed-breakout reversal.
- [ ] H4: five-minute benchmark.
- [ ] H5: RSI filter.

## Phase 6 — Robustness and deployment decision

- [ ] Walk-forward evaluation.
- [ ] Block-resampled Monte Carlo.
- [ ] Parameter-neighborhood analysis.
- [ ] Severe transaction-cost stress.
- [ ] One-time frozen final-test evaluation.
- [ ] Replication on intended cTrader broker feed.
- [ ] Paper trading.
- [ ] Decide whether evidence justifies a cTrader cBot.

## Promotion gate

No candidate advances to automation unless it satisfies the success criteria in [[../01 - Research/Research Brief]] and the validation rules in [[../02 - Methodology/Validation Protocol]].

