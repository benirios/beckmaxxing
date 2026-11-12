from __future__ import annotations

import hashlib
import json
import lzma
import math
import re
import struct
import subprocess
from dataclasses import dataclass
from datetime import UTC, date, datetime, timedelta
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd


RECORD = struct.Struct(">IIIff")
DAILY_PATH = re.compile(
    r"(?:^|/)(?P<year>\d{4})/(?P<month>\d{2})/(?P<day>\d{2})_ticks\.bi5$"
)
HOURLY_PATH = re.compile(
    r"(?:^|/)(?P<year>\d{4})/(?P<month>\d{2})/(?P<day>\d{2})/"
    r"(?P<hour>\d{2})h_ticks\.bi5$"
)


@dataclass(frozen=True)
class RawFile:
    path: Path
    period_start: datetime
    timestamp_span_ms: int
    layout: str


def sha256_file(path: str | Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def identify_raw_file(path: str | Path) -> RawFile:
    path = Path(path)
    normalized = path.as_posix()
    match = DAILY_PATH.search(normalized)
    layout = "daily"
    timestamp_span_ms = 24 * 60 * 60 * 1000
    hour = 0
    if match is None:
        match = HOURLY_PATH.search(normalized)
        layout = "legacy-hourly"
        timestamp_span_ms = 60 * 60 * 1000
        if match is not None:
            hour = int(match.group("hour"))
    if match is None:
        raise ValueError(
            "Unrecognized Dukascopy path. Expected YYYY/MM/DD_ticks.bi5 or "
            "YYYY/MM/DD/HHh_ticks.bi5"
        )

    month = int(match.group("month")) + 1
    period_start = datetime(
        int(match.group("year")),
        month,
        int(match.group("day")),
        hour,
        tzinfo=UTC,
    )
    return RawFile(path, period_start, timestamp_span_ms, layout)


def discover_raw_files(inputs: Iterable[str | Path]) -> list[Path]:
    files: list[Path] = []
    for item in inputs:
        path = Path(item)
        if path.is_dir():
            files.extend(path.rglob("*.bi5"))
        elif path.is_file():
            files.append(path)
        else:
            raise FileNotFoundError(path)
    unique = sorted({path.resolve() for path in files})
    if not unique:
        raise ValueError("No .bi5 files found")
    return unique


def decode_bi5(path: str | Path, price_scale: float) -> pd.DataFrame:
    if not math.isfinite(price_scale) or price_scale <= 0:
        raise ValueError("price_scale must be a positive finite number")

    raw_file = identify_raw_file(path)
    try:
        payload = lzma.decompress(raw_file.path.read_bytes())
    except lzma.LZMAError as error:
        raise ValueError(f"Cannot decompress {raw_file.path}: {error}") from error
    if len(payload) % RECORD.size:
        raise ValueError(
            f"Decoded byte count for {raw_file.path} is not divisible by {RECORD.size}"
        )

    rows: list[tuple[datetime, float, float, float, float, int]] = []
    for sequence, (offset_ms, ask_raw, bid_raw, ask_size, bid_size) in enumerate(
        RECORD.iter_unpack(payload)
    ):
        if offset_ms >= raw_file.timestamp_span_ms:
            raise ValueError(
                f"Timestamp offset {offset_ms} is outside the {raw_file.layout} file span"
            )
        ask = ask_raw / price_scale
        bid = bid_raw / price_scale
        if bid <= 0 or ask <= 0:
            raise ValueError(f"Non-positive price in {raw_file.path} record {sequence}")
        if ask < bid:
            raise ValueError(f"Ask below bid in {raw_file.path} record {sequence}")
        if not math.isfinite(ask_size) or not math.isfinite(bid_size):
            raise ValueError(f"Non-finite size in {raw_file.path} record {sequence}")
        if ask_size < 0 or bid_size < 0:
            raise ValueError(f"Negative size in {raw_file.path} record {sequence}")
        rows.append(
            (
                raw_file.period_start + timedelta(milliseconds=offset_ms),
                bid,
                ask,
                float(bid_size),
                float(ask_size),
                sequence,
            )
        )

    return pd.DataFrame(
        rows,
        columns=("timestamp", "bid", "ask", "bid_size", "ask_size", "sequence"),
    )


def decode_files(paths: Iterable[str | Path], price_scale: float) -> pd.DataFrame:
    frames: list[pd.DataFrame] = []
    for source_order, path in enumerate(paths):
        frame = decode_bi5(path, price_scale)
        frame["source_order"] = source_order
        frames.append(frame)
    if not frames:
        raise ValueError("At least one raw file is required")

    ticks = pd.concat(frames, ignore_index=True)
    ticks = ticks.sort_values(
        ["timestamp", "source_order", "sequence"], kind="stable"
    ).reset_index(drop=True)
    duplicate_record = ticks.duplicated(
        subset=["timestamp", "bid", "ask", "bid_size", "ask_size"], keep=False
    )
    if duplicate_record.any():
        raise ValueError(
            f"Found {int(duplicate_record.sum())} rows belonging to exact duplicate tick records"
        )
    return ticks.drop(columns=["source_order", "sequence"])


def validate_price_scale(
    ticks: pd.DataFrame,
    reference_price: float,
    tolerance_percent: float = 10.0,
) -> dict[str, float | bool]:
    if ticks.empty:
        raise ValueError("Cannot validate price scale from an empty tick set")
    if not math.isfinite(reference_price) or reference_price <= 0:
        raise ValueError("reference_price must be a positive finite number")
    if not math.isfinite(tolerance_percent) or tolerance_percent <= 0:
        raise ValueError("tolerance_percent must be positive")

    median_mid = float(((ticks["bid"] + ticks["ask"]) / 2).median())
    deviation_percent = abs(median_mid - reference_price) / reference_price * 100
    passed = deviation_percent <= tolerance_percent
    result: dict[str, float | bool] = {
        "reference_price": reference_price,
        "observed_median_mid": median_mid,
        "deviation_percent": deviation_percent,
        "tolerance_percent": tolerance_percent,
        "passed": passed,
    }
    if not passed:
        raise ValueError(
            "Decoded median price does not match the reference price: "
            f"observed={median_mid:.8g}, reference={reference_price:.8g}, "
            f"deviation={deviation_percent:.2f}%"
        )
    return result


def aggregate_ticks_to_m1(ticks: pd.DataFrame) -> pd.DataFrame:
    required = {"timestamp", "bid", "ask", "bid_size", "ask_size"}
    missing = sorted(required.difference(ticks.columns))
    if missing:
        raise ValueError(f"Missing tick columns: {missing}")
    if ticks.empty:
        raise ValueError("Cannot aggregate an empty tick set")

    ordered = ticks.sort_values("timestamp", kind="stable").copy()
    ordered["mid"] = (ordered["bid"] + ordered["ask"]) / 2
    ordered["spread"] = ordered["ask"] - ordered["bid"]
    ordered["timestamp"] = pd.to_datetime(ordered["timestamp"], utc=True)
    ordered["minute"] = ordered["timestamp"].dt.floor("min")
    grouped = ordered.groupby("minute", sort=True, observed=True)

    bars = grouped.agg(
        bid_open=("bid", "first"),
        bid_high=("bid", "max"),
        bid_low=("bid", "min"),
        bid_close=("bid", "last"),
        ask_open=("ask", "first"),
        ask_high=("ask", "max"),
        ask_low=("ask", "min"),
        ask_close=("ask", "last"),
        mid_open=("mid", "first"),
        mid_high=("mid", "max"),
        mid_low=("mid", "min"),
        mid_close=("mid", "last"),
        bid_size=("bid_size", "sum"),
        ask_size=("ask_size", "sum"),
        spread_mean=("spread", "mean"),
        spread_max=("spread", "max"),
        tick_count=("timestamp", "size"),
    ).reset_index(names="timestamp")
    return bars


def _stale_quote_duration_seconds(ticks: pd.DataFrame) -> float:
    if len(ticks) < 2:
        return 0.0
    quote_changed = ticks[["bid", "ask"]].ne(ticks[["bid", "ask"]].shift()).any(axis=1)
    run_id = quote_changed.cumsum()
    durations = ticks.groupby(run_id, sort=False)["timestamp"].agg(["first", "last"])
    return float((durations["last"] - durations["first"]).dt.total_seconds().max())


def build_quality_report(ticks: pd.DataFrame, bars: pd.DataFrame) -> dict[str, object]:
    ordered = ticks.sort_values("timestamp", kind="stable").reset_index(drop=True)
    timestamps = pd.to_datetime(ordered["timestamp"], utc=True)
    midpoint = (ordered["bid"] + ordered["ask"]) / 2
    spread = ordered["ask"] - ordered["bid"]
    gaps = timestamps.diff().dt.total_seconds()
    jumps = midpoint.diff().abs()
    nonzero_jumps = jumps[jumps > 0]

    top_gaps = []
    for index in gaps.nlargest(20).dropna().index:
        top_gaps.append(
            {
                "from": timestamps.iloc[index - 1].isoformat(),
                "to": timestamps.iloc[index].isoformat(),
                "seconds": float(gaps.iloc[index]),
            }
        )

    bar_minutes = set(pd.to_datetime(bars["timestamp"], utc=True))
    session_gaps: list[dict[str, object]] = []
    local_dates = sorted(set(timestamps.dt.tz_convert("America/New_York").dt.date))
    for session_date in local_dates:
        start = pd.Timestamp(
            datetime.combine(session_date, datetime.min.time()).replace(
                hour=9, minute=30
            ),
            tz="America/New_York",
        ).tz_convert("UTC")
        expected = pd.date_range(start=start, periods=390, freq="min")
        missing = sum(minute not in bar_minutes for minute in expected)
        session_gaps.append(
            {
                "session_date_new_york": session_date.isoformat(),
                "expected_minutes": 390,
                "missing_minutes": missing,
                "complete": missing == 0,
            }
        )

    def quantiles(series: pd.Series) -> dict[str, float | None]:
        series = series.dropna()
        if series.empty:
            return {key: None for key in ("min", "p50", "p95", "p99", "max")}
        return {
            "min": float(series.min()),
            "p50": float(series.quantile(0.5)),
            "p95": float(series.quantile(0.95)),
            "p99": float(series.quantile(0.99)),
            "max": float(series.max()),
        }

    return {
        "first_tick_utc": timestamps.iloc[0].isoformat(),
        "last_tick_utc": timestamps.iloc[-1].isoformat(),
        "tick_count": int(len(ordered)),
        "m1_bar_count": int(len(bars)),
        "duplicate_timestamp_count": int(timestamps.duplicated(keep=False).sum()),
        "crossed_market_count": int((ordered["ask"] < ordered["bid"]).sum()),
        "bid_range": {
            "min": float(ordered["bid"].min()),
            "max": float(ordered["bid"].max()),
        },
        "ask_range": {
            "min": float(ordered["ask"].min()),
            "max": float(ordered["ask"].max()),
        },
        "spread": quantiles(spread),
        "absolute_midpoint_jump": quantiles(jumps.dropna()),
        "median_nonzero_midpoint_jump": (
            float(nonzero_jumps.median()) if not nonzero_jumps.empty else 0.0
        ),
        "max_unchanged_quote_seconds": _stale_quote_duration_seconds(ordered),
        "gaps_over_60_seconds": int((gaps > 60).sum()),
        "largest_gaps": top_gaps,
        "new_york_cash_session_completeness": session_gaps,
    }


def _git_version(root: Path) -> dict[str, object]:
    try:
        commit_result = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=root,
            check=True,
            capture_output=True,
            text=True,
        )
        status_result = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=root,
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return {"commit": None, "dirty": True}
    return {
        "commit": commit_result.stdout.strip(),
        "dirty": bool(status_result.stdout.strip()),
    }


def build_manifest(
    *,
    raw_paths: Iterable[str | Path],
    repository_root: str | Path,
    instrument: str,
    requested_start: date,
    requested_end: date,
    downloaded_at_utc: datetime,
    price_scale: float,
    reference_source: str,
    scale_validation: dict[str, float | bool],
    ticks: pd.DataFrame,
    output_paths: Iterable[str | Path],
) -> dict[str, object]:
    if downloaded_at_utc.tzinfo is None or downloaded_at_utc.utcoffset() is None:
        raise ValueError("downloaded_at_utc must have an explicit UTC offset")
    repository_root = Path(repository_root).resolve()
    def display_path(path_value: Path) -> str:
        try:
            return str(path_value.relative_to(repository_root))
        except ValueError:
            return str(path_value)

    raw_entries = []
    for path_value in sorted(Path(path).resolve() for path in raw_paths):
        raw_file = identify_raw_file(path_value)
        raw_entries.append(
            {
                "path": display_path(path_value),
                "bytes": path_value.stat().st_size,
                "sha256": sha256_file(path_value),
                "layout": raw_file.layout,
                "period_start_utc": raw_file.period_start.isoformat(),
            }
        )

    outputs = []
    for path_value in sorted(Path(path).resolve() for path in output_paths):
        outputs.append(
            {
                "path": display_path(path_value),
                "bytes": path_value.stat().st_size,
                "sha256": sha256_file(path_value),
            }
        )

    received_dates = pd.to_datetime(ticks["timestamp"], utc=True).dt.date
    requested_dates = set(pd.date_range(requested_start, requested_end, freq="D").date)
    received_set = set(received_dates)
    return {
        "schema_version": 1,
        "manifest_created_at_utc": datetime.now(UTC).isoformat(),
        "provider": "Dukascopy Bank SA",
        "feed_type": "historical best bid/ask ticks",
        "instrument": instrument.upper(),
        "requested_date_range": {
            "start": requested_start.isoformat(),
            "end": requested_end.isoformat(),
        },
        "received_date_range": {
            "start": min(received_set).isoformat(),
            "end": max(received_set).isoformat(),
        },
        "downloaded_at_utc": downloaded_at_utc.astimezone(UTC).isoformat(),
        "timestamp_semantics": (
            "UTC milliseconds from the containing day or legacy hour; output bars use "
            "UTC bar-open timestamps"
        ),
        "field_semantics": {
            "bid": "best bid in USD per troy ounce",
            "ask": "best ask in USD per troy ounce",
            "bid_size": "provider-reported best-bid quantity",
            "ask_size": "provider-reported best-ask quantity",
        },
        "price_scale": price_scale,
        "price_scale_reference": reference_source,
        "price_scale_validation": scale_validation,
        "missing_calendar_dates": sorted(
            day.isoformat() for day in requested_dates.difference(received_set)
        ),
        "parsing_errors": [],
        "raw_files": raw_entries,
        "outputs": outputs,
        "aggregation": "deterministic UTC M1 first/max/min/last; no gap filling",
        "code_version": _git_version(repository_root),
    }


def write_json(path: str | Path, value: object) -> None:
    Path(path).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
