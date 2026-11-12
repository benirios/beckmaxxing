---
tags: [xauusd, baseline, implementation]
status: implemented
---

# Baseline Strategy Specification

## Experiment identity

`baseline_ny_0930_15m_close_confirmed_1r`

Source of truth: `configs/baseline_ny_0930.yaml`.

## Event sequence

```text
09:30–09:44 ET bars
        ↓
opening-range high and low fixed at 09:45
        ↓
first later M1 close strictly outside the range
        ↓
enter at next M1 open
        ↓
opposite boundary stop / 1R target / 16:00 time exit
```

## Locked rules

| Component | Rule |
|---|---|
| Instrument | XAUUSD |
| Data | M1 midpoint OHLC |
| Session zone | `America/New_York` |
| Opening range | 09:30–09:45, 15 minutes |
| Confirmation | completed M1 close outside range |
| Entry | next bar open |
| Entry deadline | 12:00 |
| Stop | opposite range boundary |
| Target | 1R |
| Trades per session | 1 |
| Time exit | 16:00 |
| Same-bar collision | stop first |
| Missing bars | skip session |

## Current artifacts

- Configuration: `configs/baseline_ny_0930.yaml`
- Engine: `src/xau_orb/engine.py`
- Data validation: `src/xau_orb/data.py`
- Metrics: `src/xau_orb/metrics.py`
- CLI: `src/xau_orb/cli.py`
- Tests: `tests/test_engine.py`, `tests/test_data.py`
- cTrader cBot: `ctrader/XauUsdNy15OrbBaseline.cs`
- cTrader correlation note: [[cTrader cBot - NY15 Baseline]]

## Verified behaviors

- DST mapping for EST and EDT.
- Breakout close is followed by next-bar entry.
- Spread and slippage reduce net R.
- Same-bar target/stop collision resolves stop-first.
- Missing M1 data skips the session.
- Final-test period is locked by default.

Current automated test result: **5 passed**.

## Not yet implemented

- provider downloaders and provenance manifests;
- tick-to-M1 bid/ask aggregation;
- observed-spread fills;
- retest and false-breakout variants;
- volatility and RSI filters;
- walk-forward orchestration;
- Monte Carlo robustness reports;
- live cTrader deployment.

## Run

See [[../README|README]] for installation and command examples.

