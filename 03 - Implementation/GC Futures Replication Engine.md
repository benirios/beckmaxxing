---
tags: [gc, comex, futures, intraday-momentum, python, exp-010, deprecated]
status: deprecated-unrun
experiment: EXP-010
---

# GC Futures Replication Engine

> [!warning] Deprecated 2026-11-02
> This Python path was cancelled before any licensed market data was supplied or
> any market result was produced. The active research workflow uses cTrader
> cBots. The implementation remains in the repository only as an audit artifact;
> its console entry point has been removed from `pyproject.toml`.

EXP-010 implements the next evidence step after EXP-008/009 rejected transfer
of GC close momentum to spot XAUUSD. It tests the original mechanism on actual,
contract-identified COMEX GC futures rather than creating another XAUUSD cBot.

## Implementation

- Config and locked-protocol validation: `src/xau_orb/gc_config.py`
- Contract-level M1 and roll loaders: `src/xau_orb/gc_data.py`
- Four-mode engine and cost accounting: `src/xau_orb/gc_engine.py`
- Per-mode/year metrics: `src/xau_orb/gc_metrics.py`
- Reproducible CLI and SHA-256 manifest: `src/xau_orb/gc_cli.py`
- Protected config template:
  `configs/exp010_gc_close_momentum.template.yaml`
- Full usage and input contract: [[../docs/gc-futures-replication]]

The engine skips cross-contract predictors, rejects missing required minutes,
uses exact New York timestamps through DST, reproduces the cTrader control's
fixed random hash, records each cost component, and refuses to overwrite an
existing result directory.

## Verification

The full repository suite passes 22 tests. EXP-010 coverage includes:

- standard GC contract and protocol validation;
- timestamp-offset and duplicate-key rejection;
- exact 280-bar predictor completeness;
- 13:00/13:30 DST conversion;
- roll-boundary skipping and same-contract resumption;
- four matched direction modes;
- fixed random-key parity with EXP-009;
- tick-value, spread, slippage, commission, fill, and P/L arithmetic;
- per-mode/year summaries;
- input hashing and output overwrite protection.

## Final status

No real GC dataset was supplied, so EXP-010 has no market result and exposed no
market split. Do not describe its synthetic test suite as strategy evidence.
Research proceeds with the preregistered EXP-011 cTrader transfer test instead.

