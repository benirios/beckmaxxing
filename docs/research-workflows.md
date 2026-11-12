# XAUUSD Research Workflows

The local [Gold Research Lab](../backtest_lab/README.md) manages the strategy catalog,
native cTrader CLI backtests and Obsidian run notes. Start it with
`python3 -m backtest_lab.server` and open <http://127.0.0.1:8765>.
Agents can use `python3 -m backtest_lab.agent capabilities` and
`python3 -m backtest_lab.agent preflight T002` without a browser; see
[the agent protocol](../backtest_lab/AGENTS.md).
Broker authentication and native package compatibility must be verified before
new market results can be recorded.

[Open the Obsidian research hub](../00%20-%20XAUUSD%20ORB%20Research%20Hub.md) for the evidence register, hypotheses, methodology, decisions, and roadmap.

[Intraday thesis scan (2026-11-03)](../01%20-%20Research/Intraday%20Thesis%20Scan%20-%202026-11-03.md) records source-backed strategy and risk-control candidates, prop-firm rule checks, and the subsequent EXP-014 through EXP-016 rejections. No candidate has cleared the after-cost validation gate.

This repository tests whether an XAUUSD opening-range breakout has a repeatable edge **after costs**. It does not assume that ORB is profitable.

The compiled cTrader correlation implementation is documented in [cTrader cBot — NY15 Baseline](../03%20-%20Implementation/cTrader%20cBot%20-%20NY15%20Baseline.md), with source at `ctrader/XauUsdNy15OrbBaseline.cs`.

The first executable baseline is deliberately narrow:

- one-minute, timezone-aware OHLC input;
- 09:30–09:45 `America/New_York` opening range;
- breakout confirmed by a one-minute close outside the range;
- entry at the next bar's open (never at the already-observed confirmation close);
- opposite opening-range boundary as stop;
- 1R target, one trade per session, 12:00 entry cutoff, 16:00 time exit;
- conservative stop-first handling when a one-minute bar touches both stop and target;
- explicit spread, slippage, and commission deductions.

See [docs/research-design.md](../docs/research-design.md) for the data-source decision, ambiguity policy, validation boundaries, and planned architecture.

The Dukascopy decoder, provenance manifest, and tick-to-M1 audit pipeline are
implemented, but real sample acquisition is still blocked on authenticated
access to Dukascopy's requester-pays S3 archive. See
[docs/dukascopy-ingestion.md](../docs/dukascopy-ingestion.md) for the exact sample
procedure and acceptance gate.

The EXP-010 COMEX GC source-market replication engine is also implemented. It
requires contract-identified M1 bars, an explicit date-to-contract roll
schedule, and declared futures costs; it will not infer or back-adjust rolls.
See [docs/gc-futures-replication.md](../docs/gc-futures-replication.md).

## Input contract

CSV columns:

```text
timestamp,open,high,low,close[,volume]
```

`timestamp` must include a UTC offset (prefer `Z`). OHLC values must be midpoint prices for the baseline adapter. Never silently label bid-only bars as midpoint bars.

Example:

```text
2024-01-03T14:30:00Z,2040.10,2040.35,2039.90,2040.20,152
```

## Run

```bash
python -m pip install -e '.[dev]'
python -m xau_orb.cli \
  --config configs/baseline_ny_0930.yaml \
  --data data/processed/xauusd_m1_mid.csv \
  --output outputs/baseline_ny_0930
```

Outputs are `trades.csv`, `skipped_sessions.csv`, and `summary.json`. A skipped session is evidence, not an inconvenience: missing required one-minute bars are never filled or invented.

Normal runs automatically exclude dates after the configured validation boundary. Only after the research protocol and parameters are frozen should the final holdout be evaluated once:

```bash
python -m xau_orb.cli \
  --config configs/baseline_ny_0930.yaml \
  --data data/processed/xauusd_m1_mid.csv \
  --output outputs/frozen_final_test \
  --include-final-test
```

Run tests with:

```bash
pytest
```

Run the GC replication after completing its protected config template:

```bash
gc-close-replication \
  --config configs/exp010_gc_close_momentum.yaml \
  --data data/processed/gc_contract_m1.csv \
  --roll-schedule data/processed/gc_roll_schedule.csv \
  --output outputs/EXP-010-gc-futures
```

Ingest authenticated raw Dukascopy samples with:

```bash
python -m xau_orb.ingest_cli --help
```

## Repository layout

```text
configs/                 versioned experiment definitions
data/raw/                immutable provider downloads (not committed)
data/processed/          normalized research inputs (not committed)
docs/                    research and architecture decisions
outputs/                 generated results (not committed)
src/xau_orb/             data validation, engine, costs, metrics, CLI
tests/                   deterministic synthetic unit tests
```

Synthetic data appears only in unit tests and is clearly marked. It is never used as market evidence.

