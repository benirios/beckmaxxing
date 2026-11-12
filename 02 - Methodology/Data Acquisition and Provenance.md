---
tags: [xauusd, data, provenance]
status: planned
---

# Data Acquisition and Provenance

## Source decision

Use Dukascopy XAU/USD bid/ask ticks for initial discovery, then require replication on the intended cTrader broker feed before deployment.

Why:

- Dukascopy exposes historical best bid and ask.
- Tick data permits observed-spread execution and resolves M1 path ambiguity.
- UTC timestamps support reproducible timezone conversion.
- cTrader replication tests whether the result survives the target broker's quote stream.

Official references:

- https://www.dukascopy.com/swiss/english/marketwatch/historical/
- https://www.dukascopy.com/wiki/en/development/strategy-api/historical-data/history-ticks/
- https://www.dukascopy.com/wiki/en/development/data-export/
- https://help.ctrader.com/open-api/symbol-data/

HistData is a secondary cross-check only. Its M1 bars are bid-based, ask is included only in generic tick data, and timestamps use fixed EST without DST adjustment: https://ftpsite.histdata.com/f-a-q/

### Access status — 2026-10-27

Dukascopy's official bulk-data guide now points to the requester-pays S3 bucket `cfg-public-proper-wallaby` in `eu-west-1`. Authenticated AWS credentials are required and download charges belong to the requester. The former public `datafeed.dukascopy.com` URL returned HTTP 429 and directed the client to that guide. This machine currently has neither AWS CLI nor configured AWS credentials, so no market sample has been claimed or manufactured.

The local decoder and audit pipeline are implemented and tested against deterministic binary fixtures. See [[../docs/dukascopy-ingestion|Dukascopy sample ingestion]]. Real-data status remains **not acquired** until an authenticated official download is hashed and recorded.

## Required provenance manifest

Every acquired batch must record:

- provider and account/feed type;
- instrument identifier;
- requested and received date range;
- download timestamp in UTC;
- raw file names, byte counts, and SHA-256 hashes;
- source timezone and timestamp semantics;
- bid/ask field definitions;
- verified XAUUSD price scale;
- missing files, corrupt records, and parsing errors;
- aggregation code version or Git commit.

## Storage policy

```text
data/raw/         immutable provider files
data/processed/   normalized UTC bid/ask/mid datasets
outputs/          reproducible experiment artifacts
```

Raw files are never edited. Processed outputs can always be regenerated from raw data plus a manifest and code version.

## Normalized tick schema

| Field | Meaning |
|---|---|
| `timestamp` | timezone-aware UTC event time |
| `bid` | best bid in USD/oz |
| `ask` | best ask in USD/oz |
| `bid_size` | provider quantity, if available |
| `ask_size` | provider quantity, if available |
| `provider` | immutable source identifier |

Reject ticks with non-positive prices, `ask < bid`, duplicate event records that cannot be explained, or unverified scale.

## Normalized M1 schema

Preserve separate bid, ask, and midpoint OHLC columns. Never label a bid-only bar as midpoint data. Bars use UTC bar-open timestamps.

The current baseline accepts:

```text
timestamp,open,high,low,close[,volume]
```

and requires those OHLC fields to represent midpoint prices.

## Data-quality gates

- explicit timezone on every timestamp;
- monotonic, unique bar index;
- valid OHLC relationships;
- no silently interpolated bars;
- gap report by session and month;
- spread distribution by time of day;
- suspicious price-jump and stale-quote diagnostics;
- cross-check a sample against the provider's chart.

## Immediate ingestion sequence

1. Download one recent week and one older week.
2. Verify file layout and XAUUSD scale manually.
3. Decode into normalized ticks.
4. Compare reconstructed M1 bars with provider charts.
5. Produce the provenance and gap reports.
6. Only then acquire the full research history.

## Implemented ingestion controls

- current daily and legacy hourly `.bi5` path recognition;
- zero-based provider-month conversion;
- strict 20-byte big-endian record decoding;
- explicit commodity price scale with independent reference-price validation;
- rejection of corrupt files, invalid offsets, crossed quotes, negative sizes, and exact duplicate tick records;
- deterministic UTC bid/ask/midpoint M1 aggregation without gap filling;
- spread, jump, stale-quote, time-gap, and New York session-completeness diagnostics;
- SHA-256 provenance for every raw file and generated output.

