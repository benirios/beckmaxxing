"""Locked EXP-016 independent-feed overnight gold screen; never sends orders."""

from __future__ import annotations

import argparse
import csv
from datetime import date, timedelta
import json
from pathlib import Path

from xau_orb.asia_rebound_screen import (
    COMMISSION_PER_MILLION_PER_SIDE,
    SLIPPAGE_PER_SIDE,
    exact_window,
    load_bars,
    summarize,
    timestamp,
)


def trade(window: list[dict], exit_bar: dict, side: str) -> dict | None:
    if side == "long":
        entry = window[0]["ao"]
        stop = 0.99 * entry
        if window[0]["o"] <= stop:
            return None
        fill = exit_bar["o"]
        reason = "time"
        for row in window:
            if row["l"] <= stop:
                fill = min(row["o"], stop)
                reason = "stop"
                break
        gross = fill - entry
    elif side == "short":
        entry = window[0]["o"]
        stop = 1.01 * entry
        if window[0]["ao"] >= stop:
            return None
        fill = exit_bar["ao"]
        reason = "time"
        for row in window:
            if row["ah"] >= stop:
                fill = max(row["ao"], stop)
                reason = "stop"
                break
        gross = entry - fill
    else:
        raise ValueError(side)
    commission = (entry + fill) * COMMISSION_PER_MILLION_PER_SIDE / 1_000_000
    slip = 2 * SLIPPAGE_PER_SIDE
    return {
        "entry": entry,
        "stop": stop,
        "exit": fill,
        "reason": reason,
        "gross_after_spread": gross,
        "commission": commission,
        "slippage": slip,
        "net": gross - commission - slip,
    }


def run(bars: dict[int, dict], end_year: int) -> tuple[list[dict], dict]:
    day = date(2018, 1, 1)
    last = date(end_year, 12, 31)
    ledger: list[dict] = []
    skipped = {"weekend_or_friday": 0, "missing_window": 0, "invalid_stop": 0}
    while day <= last:
        if day.weekday() >= 4:
            skipped["weekend_or_friday"] += 1
            day += timedelta(days=1)
            continue
        window = exact_window(bars, timestamp(day, 18, 5), 171)
        exit_bar = bars.get(timestamp(day + timedelta(days=1), 8, 20))
        if window is None or exit_bar is None:
            skipped["missing_window"] += 1
            day += timedelta(days=1)
            continue
        long = trade(window, exit_bar, "long")
        short = trade(window, exit_bar, "short")
        if long is None or short is None:
            skipped["invalid_stop"] += 1
            day += timedelta(days=1)
            continue
        mid_entry = (window[0]["o"] + window[0]["ao"]) / 2
        mid_exit = (exit_bar["o"] + exit_bar["ao"]) / 2
        ledger.append({
            "date_ny": day.isoformat(),
            "entry_time_utc": timestamp(day, 18, 5),
            "exit_time_utc": timestamp(day + timedelta(days=1), 8, 20),
            "raw_midpoint_change": round(mid_exit - mid_entry, 6),
            "entry_spread": round(window[0]["ao"] - window[0]["o"], 6),
            "exit_spread": round(exit_bar["ao"] - exit_bar["o"], 6),
            "long_entry": round(long["entry"], 6),
            "long_stop": round(long["stop"], 6),
            "long_exit": round(long["exit"], 6),
            "long_reason": long["reason"],
            "long_gross_after_spread": round(long["gross_after_spread"], 6),
            "long_commission": round(long["commission"], 6),
            "long_slippage": round(long["slippage"], 6),
            "long_net": round(long["net"], 6),
            "short_reason": short["reason"],
            "short_net": round(short["net"], 6),
        })
        day += timedelta(days=1)
    summary = {
        "eligible_dates": len(ledger),
        "skipped": skipped,
        "development_2018_2023": subset(ledger),
        "years": {str(year): subset([r for r in ledger if r["date_ny"].startswith(str(year))])
                  for year in range(2018, end_year + 1)},
    }
    if end_year == 2025:
        summary["development_2018_2023"] = subset([r for r in ledger if int(r["date_ny"][:4]) <= 2023])
        summary["confirmation_2024_2025"] = subset([r for r in ledger if int(r["date_ny"][:4]) >= 2024])
    return ledger, summary


def subset(rows: list[dict]) -> dict:
    long = [r["long_net"] for r in rows]
    short = [r["short_net"] for r in rows]
    return {
        "dates": len(rows),
        "long": summarize(long),
        "short_control": summarize(short),
        "midpoint_change_total": round(sum(r["raw_midpoint_change"] for r in rows), 4),
        "long_gross_after_spread_total": round(sum(r["long_gross_after_spread"] for r in rows), 4),
        "long_commission_total": round(sum(r["long_commission"] for r in rows), 4),
        "long_slippage_total": round(sum(r["long_slippage"] for r in rows), 4),
        "long_stop_exits": sum(r["long_reason"] == "stop" for r in rows),
        "short_stop_exits": sum(r["short_reason"] == "stop" for r in rows),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--end-year", type=int, choices=[2023, 2025], default=2023)
    parser.add_argument("--output", type=Path, default=Path("outputs/EXP-016-overnight-gold-screen"))
    args = parser.parse_args()
    paths = [Path(f"data/raw/evtl_xauusd_m5_{year}.json.gz") for year in range(2018, args.end_year + 1)]
    bars, manifest = load_bars(paths)
    ledger, summary = run(bars, args.end_year)
    args.output.mkdir(parents=True, exist_ok=True)
    with (args.output / f"ledger_through_{args.end_year}.csv").open("w", newline="") as handle:
        if ledger:
            writer = csv.DictWriter(handle, fieldnames=list(ledger[0]))
            writer.writeheader()
            writer.writerows(ledger)
    report = {"experiment": "EXP-016", "end_year": args.end_year, "data_manifest": manifest, "summary": summary}
    (args.output / f"summary_through_{args.end_year}.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
