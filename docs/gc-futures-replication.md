# EXP-010 GC futures close-momentum replication

> **Deprecated 2026-11-02:** cancelled before market-data acquisition or a
> market run because the active experiment workflow is cTrader-only. This page
> is retained for audit history; the engine's console script is no longer
> installed. EXP-010 has no trading result and exposed no market split.

## Historical status

The provider-neutral backtest engine is implemented and verified with synthetic
unit tests. No market result exists yet. A valid run requires licensed,
contract-level GC M1 data, an explicit roll schedule, and execution-cost inputs
from the intended venue or broker.

The engine deliberately refuses placeholder provenance, dates, and costs. It
also refuses to infer the active contract or compare prices across a roll gap.

## Locked market specification

EXP-010 targets standard COMEX Gold futures (`GC`). CME specifies a 100-troy-
ounce contract quoted in USD per ounce, with a minimum price fluctuation of
$0.10 per ounce, so one tick is $10 per contract. See the official
[CME Gold contract specifications](https://www.cmegroup.com/markets/metals/precious/gold.timeAndSales.html)
and [CME COMEX Gold overview](https://www.cmegroup.com/education/articles-and-reports/trading-comex-gold-and-silver).

The 08:20–13:30 New York window is the source paper's volume-based GC session,
not the full Globex trading day. The primary research reference is Baltussen,
Da, Lammers, and Martens, “Hedging demand and market intraday momentum,”
*Journal of Financial Economics* 142 (2021), 377–403,
[DOI](https://doi.org/10.1016/j.jfineco.2021.04.029).

## Input 1: contract-level M1 bars

CSV columns:

```text
timestamp,contract,open,high,low,close[,volume]
```

Requirements:

- `timestamp` must contain an explicit UTC offset; every timestamp is normalized
  to UTC by the loader.
- `contract` must identify the actual expiry, such as `GCZ25`. A generic `GC`
  continuous symbol is insufficient unless the provider also supplies the
  underlying contract identity for every bar.
- OHLC values must be transaction/trade prices in USD per ounce.
- Each `(contract, timestamp)` pair must be unique.
- Missing minutes are never filled or interpolated.
- Overlapping contract months are allowed; the roll schedule selects which one
  is active for each session.

## Input 2: explicit roll schedule

CSV columns:

```text
session_date,contract
2025-11-20,GCZ25
2025-11-21,GCZ25
2025-11-24,GCG26
```

Every intended trading session must map to exactly one contract. Create this
file from a documented, preregistered roll rule—such as a provider's official
continuous-contract mapping or an independently calculated volume rule. The
engine never guesses the front month.

The first session after a contract change is skipped because its predictor
would compare two different contracts. The new contract's 13:29 close then
seeds the following session. Back-adjusted prices are not used to create a
tradable signal.

## Input 3: completed experiment config

Copy [the template](../configs/exp010_gc_close_momentum.template.yaml) and fill:

- verified start and end dates;
- provider and exact dataset/version;
- full bid/ask spread in ticks;
- slippage in ticks per side;
- commission in USD per contract per side.

The checked-in template contains `REQUIRED` and `null` fields on purpose and
cannot run unchanged. Cost values are research scenarios, not facts supplied by
this repository.

## Locked strategy

For each roll-schedule session:

1. Require all 280 M1 bars from 08:20 through 12:59 New York.
2. Retain the prior valid session's exact 13:29 close only when it belongs to
   the same contract.
3. Compute `current 12:59 close / prior 13:29 close - 1`.
4. Enter at the exact 13:00 M1 open and exit at the exact 13:30 M1 open.
5. Run one contract for each of four modes on the same eligible dates:
   `momentum`, `always_long`, `always_short`, and `deterministic_random`.
6. Use random seed `20261102`; seed selection is prohibited.

There is no stop or target. The purpose is to replicate a 30-minute timing
effect, not to design a new futures strategy.

## Cost model

For one standard GC contract:

```text
tick value       = $0.10 × 100 oz = $10
gross P/L        = signed raw price change × 100 oz
spread cost      = full spread ticks × $10
slippage cost    = 2 × per-side slippage ticks × $10
commission       = 2 × per-side commission
net P/L          = gross − spread − slippage − commission
```

Entry and exit fill prices are also recorded. Since M1 transaction bars do not
contain the historical order book, spread and slippage remain explicit cost
scenarios. A bid/ask or tick adapter can be added only after the acquired
dataset's schema and semantics are verified.

## Run

Install the project once:

```bash
python -m pip install -e '.[dev]'
```

Then run:

```bash
gc-close-replication \
  --config configs/exp010_gc_close_momentum.yaml \
  --data data/processed/gc_contract_m1.csv \
  --roll-schedule data/processed/gc_roll_schedule.csv \
  --output outputs/EXP-010-gc-futures
```

Alternatively:

```bash
python -m xau_orb.gc_cli --help
```

The CLI refuses to overwrite a non-empty output directory.

## Outputs

- `trades.csv`: one row per eligible session and direction mode, including
  contract, predictor, raw/fill prices, and each cost component.
- `skipped_sessions.csv`: explicit data, roll, zero-return, and timestamp
  exclusions.
- `summary.json`: per-mode and per-year profitability, win rate, profit factor,
  Sharpe, drawdown, direction counts, and cost totals.
- `manifest.json`: SHA-256, byte size, and resolved path for the config, market
  data, and roll schedule, plus embedded strategy and contract settings.

## Acceptance rule

The momentum mode must show positive net expectancy, profit factor above 1.0,
and economically meaningful improvement over all three matched controls. The
effect must not depend on a single year or roll convention. A failure is
recorded as a source-market non-replication; timestamps, thresholds, direction,
roll rules, or costs are not tuned after seeing the result.

