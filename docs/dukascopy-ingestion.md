# Dukascopy sample ingestion

## Current access constraint

Dukascopy's current official bulk-data guide uses the requester-pays S3 bucket
`cfg-public-proper-wallaby` in `eu-west-1`. It requires an authenticated AWS
account and explicitly bills the requester. The former unauthenticated
`datafeed.dukascopy.com` path returned HTTP 429 during the 2026-10-27 access
check and directed clients to the new guide.

Official references:

- https://www.dukascopy.com/wiki/en/development/data-export/
- https://www.dukascopy.com/api/data/get/historical-data-export
- https://www.dukascopy.com/wiki/en/development/strategy-api/historical-data/history-ticks/

Do not substitute a scraped or relabeled third-party file for a Dukascopy raw
file. Either download through the authenticated official bucket/widget or
change the source decision explicitly in the decision log.

## Raw layout accepted by the importer

The importer accepts both layouts documented or cautioned about in the current
Dukascopy guide:

```text
XAUUSD/YYYY/MM/DD_ticks.bi5       # current daily layout
XAUUSD/YYYY/MM/DD/HHh_ticks.bi5   # legacy hourly layout
```

The path month is zero-indexed. Each LZMA-compressed record is decoded as
big-endian `>IIIff`: UTC millisecond offset, ask integer, bid integer, ask
volume, bid volume.

## Non-negotiable scale check

Dukascopy's guide says the point value varies for commodities and does not
publish a verified XAUUSD value in its generic decoder. Therefore the importer
has no default XAUUSD scale. A run must supply:

1. an explicit scale;
2. a reference XAUUSD price independently read from Dukascopy for the sample;
3. a note identifying that reference;
4. a tolerance within which the decoded median must fall.

This turns a silent 10x/100x scaling error into a hard failure.

## Sample procedure

Acquire one complete recent trading week and one older trading week. Keep the
provider's directory layout under `data/raw/dukascopy/` and never edit the raw
files. Then run, with the real download timestamp and independently checked
price substituted:

```bash
python -m xau_orb.ingest_cli \
  --input data/raw/dukascopy/XAUUSD/2025/09 \
  --instrument XAUUSD \
  --price-scale VERIFIED_SCALE \
  --reference-price VERIFIED_DUKASCOPY_PRICE \
  --reference-source "Dukascopy chart, 2025-10-06 HH:MM UTC" \
  --requested-start 2025-10-06 \
  --requested-end 2025-10-10 \
  --downloaded-at-utc 2026-10-27T12:00:00Z \
  --output-dir data/processed/dukascopy-sample-recent
```

Repeat into a separate output folder for the older week. The command writes:

- normalized bid/ask ticks as gzip CSV;
- deterministic UTC M1 bid, ask, and midpoint bars;
- a quality report covering spreads, jumps, stale quotes, large time gaps, and
  New York cash-session completeness;
- a provenance manifest containing raw and output SHA-256 hashes, byte counts,
  date coverage, scale evidence, timestamp semantics, and code version.

No missing ticks or bars are interpolated. Exact duplicate tick records,
crossed markets, invalid offsets, negative sizes, corrupt compression, and a
failed scale check stop the run.

## Sample acceptance gate

Do not acquire the full history until both weeks satisfy all of these:

- decoded prices agree with the independent Dukascopy reference;
- timestamps align with the provider chart in UTC;
- ask is never below bid;
- the M1 reconstruction agrees with sampled provider candles;
- missing minutes and long quote gaps are understood rather than repaired;
- spread and jump outliers have been inspected;
- raw/output hashes and the code version appear in the manifest.
