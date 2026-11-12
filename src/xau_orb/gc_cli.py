from __future__ import annotations

import argparse
import hashlib
import json
from datetime import UTC, datetime
from pathlib import Path

from .gc_config import load_gc_config
from .gc_data import load_gc_m1_csv, load_gc_roll_schedule
from .gc_engine import run_gc_close_replication
from .gc_metrics import gc_full_report


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _file_manifest(path: Path) -> dict[str, object]:
    resolved = path.resolve()
    return {
        "path": str(resolved),
        "bytes": resolved.stat().st_size,
        "sha256": _sha256(resolved),
    }


def _write_json(path: Path, value: object) -> None:
    with path.open("w", encoding="utf-8") as handle:
        json.dump(value, handle, indent=2, allow_nan=False)
        handle.write("\n")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Run EXP-010 COMEX GC close-momentum replication"
    )
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--data", required=True, type=Path)
    parser.add_argument("--roll-schedule", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    if args.output.exists() and any(args.output.iterdir()):
        raise FileExistsError(
            f"Refusing to overwrite non-empty output directory: {args.output}"
        )

    config = load_gc_config(args.config)
    bars = load_gc_m1_csv(args.data)
    schedule = load_gc_roll_schedule(args.roll_schedule)
    result = run_gc_close_replication(bars, schedule, config)
    report = gc_full_report(result.trades, result.skipped_sessions)

    args.output.mkdir(parents=True, exist_ok=True)
    result.trades.to_csv(args.output / "trades.csv", index=False)
    result.skipped_sessions.to_csv(
        args.output / "skipped_sessions.csv", index=False
    )
    _write_json(args.output / "summary.json", report)
    manifest = {
        "experiment": config.experiment.name,
        "experiment_window": {
            "start_date": config.experiment.start_date.isoformat(),
            "end_date": config.experiment.end_date.isoformat(),
        },
        "generated_at_utc": datetime.now(UTC).isoformat(),
        "provider": config.provenance.provider,
        "dataset": config.provenance.dataset,
        "price_basis": config.provenance.price_basis,
        "inputs": {
            "config": _file_manifest(args.config),
            "data": _file_manifest(args.data),
            "roll_schedule": _file_manifest(args.roll_schedule),
        },
        "input_audit": {
            "bar_rows": len(bars),
            "first_bar_utc": bars["timestamp"].min().isoformat(),
            "last_bar_utc": bars["timestamp"].max().isoformat(),
            "contracts": sorted(str(value) for value in bars["contract"].unique()),
            "roll_schedule_rows": len(schedule),
            "roll_schedule_first_date": schedule["session_date"].min().isoformat(),
            "roll_schedule_last_date": schedule["session_date"].max().isoformat(),
        },
        "locked_contract": {
            "symbol": config.instrument.symbol,
            "contract_size_oz": config.instrument.contract_size_oz,
            "tick_size": config.instrument.tick_size,
            "tick_value_usd": config.instrument.tick_value_usd,
        },
        "costs": {
            "bid_ask_spread_ticks": config.costs.bid_ask_spread_ticks,
            "slippage_ticks_per_side": config.costs.slippage_ticks_per_side,
            "commission_per_side_usd": config.costs.commission_per_side_usd,
        },
        "locked_session": {
            "timezone": config.session.timezone,
            "session_open": config.session.session_open.isoformat(timespec="minutes"),
            "signal_time": config.session.signal_time.isoformat(timespec="minutes"),
            "exit_time": config.session.exit_time.isoformat(timespec="minutes"),
            "required_predictor_bars": config.session.required_predictor_bars,
        },
        "locked_strategy": {
            "direction_modes": list(config.strategy.direction_modes),
            "random_seed": config.strategy.random_seed,
            "contracts_per_trade": config.strategy.contracts_per_trade,
            "cross_contract_predictor": config.strategy.cross_contract_predictor,
        },
    }
    _write_json(args.output / "manifest.json", manifest)

    print(f"experiment={config.experiment.name}")
    print(f"provider={config.provenance.provider}")
    print(f"bars={len(bars)} scheduled_sessions={len(schedule)}")
    print(
        f"trade_rows={len(result.trades)} "
        f"skipped_sessions={len(result.skipped_sessions)}"
    )
    print(f"artifacts={args.output.resolve()}")


if __name__ == "__main__":
    main()

