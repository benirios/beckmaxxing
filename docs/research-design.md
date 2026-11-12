# Research design and technical architecture

## Decision summary

### 1. Data source

Use **Dukascopy XAU/USD bid/ask ticks** as the primary discovery dataset, then require replication on the intended cTrader broker feed before treating an edge as deployable.

Why Dukascopy for discovery:

- its Historical Data Export supports commodities and resolutions from tick to monthly;
- JForex history exposes best bid and ask, which allows observed-spread execution rather than a fictional constant spread;
- raw timestamps are UTC and can be normalized reproducibly;
- it is accessible enough to build a long-history pilot without buying an institutional feed.

Official references:

- https://www.dukascopy.com/swiss/english/marketwatch/historical/
- https://www.dukascopy.com/wiki/en/development/strategy-api/historical-data/history-ticks/
- https://www.dukascopy.com/wiki/en/development/data-export/

Important limitation: spot gold is decentralized. Dukascopy is one executable quote stream, not a universal XAUUSD tape. Results must be replicated using the intended broker. The cTrader Open API supports separate historical bid/ask tick requests, but requests are limited to one-week windows and the returned history is broker-backend specific:

- https://help.ctrader.com/open-api/symbol-data/

The initial pipeline intentionally does not auto-download data. Provider credentials, instrument scale, coverage dates, file layout, and licensing must be verified first. Its input is a normalized, audited M1 midpoint CSV. The next ingestion milestone will preserve raw bid/ask ticks and produce bid, ask, and midpoint bars with a provenance manifest and hashes.

HistData can be useful as a cross-check, not the primary source: its own FAQ says M1 bars are bid-based, ask is present only in generic tick data, timestamps are fixed EST without daylight-saving adjustment, and the free data has no warranty.

### 2. Session time handling

All stored timestamps are timezone-aware UTC. Session construction converts each timestamp through the IANA zone `America/New_York`; no fixed UTC offset is used. Consequently, 09:30 New York automatically maps to 14:30 UTC in standard time and 13:30 UTC in daylight time.

Input timestamps without an explicit offset are rejected. Bars are interpreted as bar-open timestamps. The 15-minute range therefore contains 09:30 through 09:44, and the earliest confirmation bar is 09:45.

Each session requires every M1 bar from 09:30 through 15:59 in the baseline configuration. If any are absent, the session is skipped and reported. This strict policy can later be relaxed only through a versioned experiment, never by silently forward-filling prices.

### 3. Transaction costs

The baseline input is midpoint OHLC. A long pays half the configured spread on entry and half on exit; a short does the symmetric calculation. Slippage is adverse on every fill, and round-trip commission is deducted once. Thus:

```text
net PnL/oz = gross directional move
             - round-trip spread
             - 2 × per-side slippage
             - round-trip commission
```

The initial transparent stress assumption is `$0.30/oz` round-trip spread plus `$0.02/oz` slippage per side. It is a scenario, not a claim about a broker. Later experiments will use time-varying observed bid/ask spreads and multiple cost scenarios. All PnL is first reported per ounce; broker lot sizing is deliberately outside this research baseline.

### 4. Entry and exit ambiguity policy

- A breakout requires a completed M1 close strictly beyond the opening-range boundary.
- Entry occurs at the next M1 open. Entering at the confirmation close would use a price known only after that bar completed.
- Stop is the opposite range boundary. Target is `1R`, where `R` is the raw midpoint distance from entry to stop.
- Only the first confirmed direction is eligible, and at most one trade is taken per session.
- Confirmations must occur before 12:00 New York. An open position is closed using the 15:59 bar close at 16:00.
- Gap through a stop fills at the next bar open (worse than the stop when applicable). A gap through a target receives the target price, not an optimistic better fill.
- If one M1 bar touches both stop and target and tick order is unavailable, the stop is assumed first. The event is explicitly marked `ambiguous_stop_first`.

This is conservative, deterministic, and auditable. Tick replay should later replace the ambiguity rule wherever tick data are available.

### 5. Validation without leakage

The baseline parameters are declared before viewing results. Chronological partitions are fixed in configuration:

- training: through 2016-12-31;
- validation: 2017-01-01 through 2021-12-31;
- final test: 2022-01-01 onward.

The final-test partition is not used to select sessions, filters, stops, targets, or cost assumptions. Any later volatility/range percentile filter must be computed from past sessions only (`shift(1)` before rolling calculations). Parameter selection happens within training; validation is used to reject unstable choices; final test is evaluated once after the research protocol is frozen.

The CLI enforces this operationally: it excludes bars after `validation_end` unless `--include-final-test` is deliberately supplied. That flag is the holdout-release switch and its use should be recorded with the frozen experiment commit hash.

Walk-forward evaluation will use expanding or rolling training windows followed by strictly later test windows. Monte Carlo will use session/block resampling rather than claiming iid trades. Cost stress tests and matched time-of-day random-entry baselines will be specified before final-test evaluation.

## Architecture

```text
raw provider files
       |
       v
ingestion + provenance manifest       (next milestone)
       |
       v
normalized UTC bid/ask/mid bars
       |
       +--> data-quality report
       |
       v
pure session/strategy engine
       |
       v
immutable trade ledger
       |
       +--> metrics by split/direction/year/regime
       +--> walk-forward and Monte Carlo validation (next milestone)
       +--> experiment report
```

Current modules:

- `config.py`: typed, versioned experiment configuration and invariants.
- `data.py`: strict normalized-CSV loading and OHLC validation.
- `engine.py`: timezone-aware session construction and deterministic event simulation.
- `metrics.py`: trade-level and daily-session performance summaries.
- `cli.py`: reproducible command entry point and artifact writing.

Strategy decisions are configuration inputs; data loading, execution friction, and reporting are separate concerns. The trade ledger is the boundary between simulation and statistical analysis.

