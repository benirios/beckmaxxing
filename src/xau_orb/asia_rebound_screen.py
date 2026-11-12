"""Locked EXP-015 independent-feed screen; never sends orders."""

from __future__ import annotations

import argparse
import csv
from datetime import date, datetime, time, timedelta
import gzip
import hashlib
import json
from pathlib import Path
from statistics import median
from zoneinfo import ZoneInfo


NY = ZoneInfo("America/New_York")
FIELDS = {"ts", "o", "h", "l", "c", "ao", "ah", "al", "ac"}
COMMISSION_PER_MILLION_PER_SIDE = 30.0
SLIPPAGE_PER_SIDE = 0.02


def load_bars(paths: list[Path]) -> tuple[dict[int, dict], list[dict]]:
    bars: dict[int, dict] = {}
    manifest = []
    for path in paths:
        raw = path.read_bytes()
        records = json.loads(gzip.decompress(raw))
        if not isinstance(records, list):
            raise ValueError(f"{path}: expected a list")
        previous = None
        for row in records:
            if not FIELDS <= row.keys():
                raise ValueError(f"{path}: missing fields")
            ts = row["ts"]
            if not isinstance(ts, int) or ts % 300 or ts in bars:
                raise ValueError(f"{path}: invalid or duplicate timestamp {ts}")
            if previous is not None and ts <= previous:
                raise ValueError(f"{path}: timestamps not strictly ascending")
            previous = ts
            if row["ao"] < row["o"] or row["ac"] < row["c"]:
                raise ValueError(f"{path}: negative bid/ask spread at {ts}")
            if not row["l"] <= min(row["o"], row["c"]) <= max(row["o"], row["c"]) <= row["h"]:
                raise ValueError(f"{path}: invalid bid OHLC at {ts}")
            if not row["al"] <= min(row["ao"], row["ac"]) <= max(row["ao"], row["ac"]) <= row["ah"]:
                raise ValueError(f"{path}: invalid ask OHLC at {ts}")
            bars[ts] = row
        manifest.append({"path": str(path), "sha256": hashlib.sha256(raw).hexdigest(), "rows": len(records)})
    return bars, manifest


def timestamp(day: date, hour: int, minute: int = 0) -> int:
    return int(datetime.combine(day, time(hour, minute), NY).timestamp())


def exact_window(bars: dict[int, dict], start: int, count: int) -> list[dict] | None:
    rows = [bars.get(start + i * 300) for i in range(count)]
    return None if any(row is None for row in rows) else rows  # type: ignore[return-value]


def commission(entry: float, exit_price: float) -> float:
    return (entry + exit_price) * COMMISSION_PER_MILLION_PER_SIDE / 1_000_000


def trade(window: list[dict], exit_bar: dict, distance: float, side: str) -> dict:
    if side == "long":
        entry = window[0]["ao"]
        stop = entry - distance
        if stop >= window[0]["o"]:
            return {"valid": False, "reason": "stop_at_or_above_bid"}
        fill = exit_bar["o"]
        exit_reason = "time"
        for row in window:
            if row["l"] <= stop:
                fill = min(row["o"], stop)
                exit_reason = "stop"
                break
        gross = fill - entry
    else:
        entry = window[0]["o"]
        stop = entry + distance
        if stop <= window[0]["ao"]:
            return {"valid": False, "reason": "stop_at_or_below_ask"}
        fill = exit_bar["ao"]
        exit_reason = "time"
        for row in window:
            if row["ah"] >= stop:
                fill = max(row["ao"], stop)
                exit_reason = "stop"
                break
        gross = entry - fill
    return {
        "valid": True,
        "side": side,
        "entry": entry,
        "stop": stop,
        "exit": fill,
        "exit_reason": exit_reason,
        "gross": gross,
        "commission": commission(entry, fill),
        "slippage": 2 * SLIPPAGE_PER_SIDE,
        "net": gross - commission(entry, fill) - 2 * SLIPPAGE_PER_SIDE,
    }


def summarize(values: list[float]) -> dict:
    wins = sum(v for v in values if v > 0)
    losses = -sum(v for v in values if v < 0)
    return {
        "trades": len(values),
        "net_total_per_oz": round(sum(values), 4),
        "net_mean_per_trade_per_oz": round(sum(values) / len(values), 4) if values else None,
        "win_rate": round(sum(v > 0 for v in values) / len(values), 4) if values else None,
        "profit_factor": round(wins / losses, 4) if losses else None,
    }


def run(bars: dict[int, dict], end_year: int) -> tuple[list[dict], dict]:
    first = date(2018, 1, 1)
    last = date(end_year, 12, 31)
    day = first
    prior_ranges: list[float] = []
    ledger: list[dict] = []
    skipped = {"missing_asia": 0, "missing_us": 0, "warmup": 0, "invalid_stop": 0}
    while day <= last:
        if day.weekday() >= 5:
            day += timedelta(days=1)
            continue
        asia = exact_window(bars, timestamp(day, 20), 72)
        exit_bar = bars.get(timestamp(day + timedelta(days=1), 2))
        if asia is None or exit_bar is None:
            skipped["missing_asia"] += 1
            day += timedelta(days=1)
            continue
        night_range = max(row["h"] for row in asia) - min(row["l"] for row in asia)
        distance = median(prior_ranges[-20:]) if len(prior_ranges) >= 20 else None
        prior_ranges.append(night_range)
        if distance is None:
            skipped["warmup"] += 1
            day += timedelta(days=1)
            continue
        us = exact_window(bars, timestamp(day, 8, 20), 92)
        if us is None:
            skipped["missing_us"] += 1
            day += timedelta(days=1)
            continue
        us_open = (us[0]["o"] + us[0]["ao"]) / 2
        us_close = (us[-1]["c"] + us[-1]["ac"]) / 2
        long = trade(asia, exit_bar, distance, "long")
        if not long["valid"]:
            skipped["invalid_stop"] += 1
            day += timedelta(days=1)
            continue
        signal = us_close < us_open
        short = trade(asia, exit_bar, distance, "short") if signal else None
        ledger.append({
            "date_ny": day.isoformat(),
            "us_midpoint_change": round(us_close - us_open, 4),
            "signal": signal,
            "stop_distance": round(distance, 4),
            "conditional_long_net": round(long["net"], 4) if signal else 0.0,
            "conditional_long_gross_after_spread": round(long["gross"], 4) if signal else 0.0,
            "conditional_long_commission": round(long["commission"], 4) if signal else 0.0,
            "conditional_long_slippage": round(long["slippage"], 4) if signal else 0.0,
            "unconditional_long_net": round(long["net"], 4),
            "same_date_short_net": round(short["net"], 4) if short and short["valid"] else None,
            "conditional_exit": long["exit_reason"] if signal else "no_trade",
        })
        day += timedelta(days=1)
    summary = {"skipped": skipped, "eligible_dates": len(ledger), "years": {}, "splits": {}}
    for label, rows in [(str(y), [r for r in ledger if r["date_ny"].startswith(str(y))]) for y in range(2018, end_year + 1)]:
        summary["years"][label] = subset(rows)
    summary["splits"]["development_2018_2023"] = subset([r for r in ledger if int(r["date_ny"][:4]) <= 2023])
    if end_year >= 2024:
        summary["splits"]["confirmation_2024_2025"] = subset([r for r in ledger if int(r["date_ny"][:4]) >= 2024])
    return ledger, summary


def subset(rows: list[dict]) -> dict:
    conditional = [r["conditional_long_net"] for r in rows if r["signal"]]
    unconditional = [r["unconditional_long_net"] for r in rows]
    opposite = [r["same_date_short_net"] for r in rows if r["same_date_short_net"] is not None]
    result = {
        "eligible_dates": len(rows),
        "conditional_long": summarize(conditional),
        "unconditional_long": summarize(unconditional),
        "same_date_short": summarize(opposite),
        "conditional_gross_after_spread_per_oz": round(sum(r["conditional_long_gross_after_spread"] for r in rows), 4),
        "conditional_commission_per_oz": round(sum(r["conditional_long_commission"] for r in rows), 4),
        "conditional_slippage_per_oz": round(sum(r["conditional_long_slippage"] for r in rows), 4),
        "conditional_mean_per_eligible_date": round(sum(conditional) / len(rows), 4) if rows else None,
        "unconditional_mean_per_eligible_date": round(sum(unconditional) / len(rows), 4) if rows else None,
    }
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--end-year", type=int, choices=[2023, 2025], default=2023)
    parser.add_argument("--output", type=Path, default=Path("outputs/EXP-015-asia-rebound-screen"))
    args = parser.parse_args()
    paths = [Path(f"data/raw/evtl_xauusd_m5_{year}.json.gz") for year in range(2018, args.end_year + 1)]
    bars, manifest = load_bars(paths)
    ledger, summary = run(bars, args.end_year)
    args.output.mkdir(parents=True, exist_ok=True)
    with (args.output / f"ledger_through_{args.end_year}.csv").open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(ledger[0]) if ledger else [])
        if ledger:
            writer.writeheader()
            writer.writerows(ledger)
    report = {"experiment": "EXP-015", "end_year": args.end_year, "data_manifest": manifest, "summary": summary}
    (args.output / f"summary_through_{args.end_year}.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
