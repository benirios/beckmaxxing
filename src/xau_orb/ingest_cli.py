from __future__ import annotations

import argparse
from datetime import date, datetime
from pathlib import Path

from .ingestion import (
    aggregate_ticks_to_m1,
    build_manifest,
    build_quality_report,
    decode_files,
    discover_raw_files,
    validate_price_scale,
    write_json,
)


def parse_date(value: str) -> date:
    return date.fromisoformat(value)


def parse_datetime(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise argparse.ArgumentTypeError("timestamp must include a UTC offset")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Decode and audit Dukascopy bid/ask .bi5 tick files"
    )
    parser.add_argument("--input", nargs="+", required=True, help="Raw files or folders")
    parser.add_argument("--instrument", default="XAUUSD")
    parser.add_argument("--price-scale", type=float, required=True)
    parser.add_argument(
        "--reference-price",
        type=float,
        required=True,
        help="Price independently read from the provider chart for this sample",
    )
    parser.add_argument("--reference-source", required=True)
    parser.add_argument("--scale-tolerance-percent", type=float, default=10.0)
    parser.add_argument("--requested-start", type=parse_date, required=True)
    parser.add_argument("--requested-end", type=parse_date, required=True)
    parser.add_argument("--downloaded-at-utc", type=parse_datetime, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    return parser


def main() -> None:
    args = build_parser().parse_args()
    if args.requested_end < args.requested_start:
        raise SystemExit("requested end must not precede requested start")

    raw_paths = discover_raw_files(args.input)
    ticks = decode_files(raw_paths, args.price_scale)
    scale_validation = validate_price_scale(
        ticks, args.reference_price, args.scale_tolerance_percent
    )
    bars = aggregate_ticks_to_m1(ticks)
    quality = build_quality_report(ticks, bars)

    output_dir = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    stem = args.instrument.lower()
    ticks_path = output_dir / f"{stem}_ticks.csv.gz"
    bars_path = output_dir / f"{stem}_m1_bid_ask_mid.csv"
    quality_path = output_dir / f"{stem}_quality.json"
    manifest_path = output_dir / f"{stem}_manifest.json"

    ticks.to_csv(ticks_path, index=False, compression="gzip")
    bars.to_csv(bars_path, index=False)
    write_json(quality_path, quality)
    manifest = build_manifest(
        raw_paths=raw_paths,
        repository_root=args.repository_root,
        instrument=args.instrument,
        requested_start=args.requested_start,
        requested_end=args.requested_end,
        downloaded_at_utc=args.downloaded_at_utc,
        price_scale=args.price_scale,
        reference_source=args.reference_source,
        scale_validation=scale_validation,
        ticks=ticks,
        output_paths=[ticks_path, bars_path, quality_path],
    )
    write_json(manifest_path, manifest)

    print(f"Decoded {len(ticks):,} ticks into {len(bars):,} M1 bars")
    print(f"Manifest: {manifest_path}")
    print(f"Quality report: {quality_path}")


if __name__ == "__main__":
    main()
