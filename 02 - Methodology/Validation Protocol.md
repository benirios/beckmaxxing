---
tags: [xauusd, validation, statistics]
status: active
---

# Validation Protocol

## Chronological partitions

The baseline configuration declares:

| Partition | Dates | Purpose |
|---|---|---|
| Training | through 2016-12-31 | develop permitted variants |
| Validation | 2017-01-01 through 2021-12-31 | reject unstable choices |
| Final test | 2022-01-01 onward | one-time frozen-protocol evaluation |

The CLI excludes final-test bars by default. `--include-final-test` is the explicit holdout-release switch and should be used only after recording the frozen Git commit and experiment specification.

## Leakage controls

- Never optimize parameters on final-test results.
- Compute rolling thresholds from past sessions only, using a one-session shift.
- Fit any scaler, classifier, or regime boundary inside the training fold.
- Do not use a day's completed range or volatility if it was unavailable at entry.
- Keep feature timestamps and order-decision timestamps explicit.
- Record every attempted variant to expose selection pressure.

## Required metrics

- number of trades;
- win rate;
- average win and loss;
- gross and net expectancy in R;
- profit factor;
- daily Sharpe where meaningful;
- maximum drawdown;
- consecutive wins and losses;
- long versus short performance;
- monthly and yearly performance;
- session and volatility-regime results;
- spread, commission, and slippage sensitivity.

## Walk-forward design

Use expanding or rolling training windows followed by strictly later evaluation windows. Parameter selection occurs inside each historical training window. Aggregate only the unseen forward segments.

## Monte Carlo design

Use simulations appropriate to serially dependent intraday results:

- session or block resampling;
- trade-order reshuffling as a limited diagnostic, not the only simulation;
- parameter and execution-cost perturbation;
- drawdown, ruin, and loss-streak distributions;
- confidence intervals for expectancy and profit factor.

## Parameter sensitivity

Test a small, preregistered neighborhood around each candidate. Prefer wide plateaus of acceptable performance. An isolated maximum surrounded by poor outcomes is likely overfit.

## Baseline comparisons

- no-trade benchmark;
- five-minute ORB benchmark;
- matched time-of-day random entries;
- matched random-anchor ranges to test whether the session open adds information beyond generic volatility continuation;
- simple momentum and reversal alternatives with equivalent exposure;
- gross versus net performance.

## Reporting rule

Every report must state data source, sample dates, session zone, missing-data policy, execution assumptions, number of tried variants, and whether the displayed result is training, validation, or final test.

Before any session-based run, audit timestamps against provider charts and at least one known scheduled market event. A dataset can have valid-looking UTC offsets yet still encode a historically wrong broker-server clock; see [[../01 - Research/Evidence Methodology Audit - 2026-10-27#1. RIP Backtest — gold 30-minute ORB|the RIP Backtest clock audit]].

