---
tags: [architecture, python, research-pipeline]
status: implemented-foundation
---

# Repository Architecture

## Data flow

```text
raw provider files
       ↓
ingestion + provenance manifest
       ↓
normalized UTC bid/ask/mid bars
       ├── data-quality report
       ↓
pure session and strategy engine
       ↓
immutable trade ledger
       ├── split/direction/time metrics
       ├── walk-forward evaluation
       ├── Monte Carlo and cost stress
       └── experiment report
```

## Directory map

| Path | Responsibility |
|---|---|
| `configs/` | versioned experiment definitions |
| `data/raw/` | immutable provider downloads |
| `data/processed/` | normalized research inputs |
| `docs/` | detailed technical decisions |
| `outputs/` | generated, reproducible artifacts |
| `src/xau_orb/` | library and CLI |
| `tests/` | deterministic synthetic unit tests |
| `ctrader/` | broker-native cBot and build project |
| `01 - Research/` | evidence and hypotheses |
| `02 - Methodology/` | data, execution, and validation rules |
| `03 - Implementation/` | human-readable implementation map |
| `04 - Planning/` | roadmap, decisions, experiments |

## Module boundaries

- `config.py`: typed configuration and invariant checks.
- `data.py`: strict normalized CSV loading and OHLC validation.
- `ingestion.py`: strict Dukascopy decoding, scale validation, provenance, diagnostics, and tick-to-M1 aggregation.
- `ingest_cli.py`: reproducible raw-sample ingestion entry point.
- `engine.py`: session construction, signal events, exits, and fills.
- `metrics.py`: performance summaries.
- `cli.py`: reproducible artifact generation and final-test lock.
- `XauUsdNy15OrbBaseline.cs`: broker-native H2 correlation implementation.

Strategy choices belong in versioned experiment configuration. Provider decoding belongs in ingestion code. Statistical analysis consumes the immutable trade ledger rather than reaching into strategy internals.

## Reproducibility contract

An experiment is reproducible only when it records:

1. Git commit;
2. configuration file and hash;
3. input data manifest and hashes;
4. Python/environment versions;
5. generated trade ledger;
6. console logs and summary artifacts;
7. training/validation/final-test state.

## Code entry points

- [[../README|Repository README]]
- [[../docs/research-design|Detailed design document]]
- `python -m xau_orb.cli --help`
- `python -m xau_orb.ingest_cli --help`

