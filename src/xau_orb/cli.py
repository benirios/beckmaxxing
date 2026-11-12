from __future__ import annotations

import argparse
import json
from pathlib import Path

from .config import load_config
from .data import load_m1_csv
from .engine import run_backtest
from .metrics import full_report


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Run the XAUUSD ORB baseline")
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--data", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--include-final-test",
        action="store_true",
        help="release the post-validation holdout; use only after freezing the protocol",
    )
    return parser


def main() -> None:
    args = build_parser().parse_args()
    config = load_config(args.config)
    bars = load_m1_csv(args.data)
    if not args.include_final_test:
        local_dates = bars.index.tz_convert(config.session.timezone).date
        bars = bars.loc[local_dates <= config.validation.validation_end]
    result = run_backtest(bars, config)
    report = full_report(result.trades)

    args.output.mkdir(parents=True, exist_ok=True)
    result.trades.to_csv(args.output / "trades.csv", index=False)
    result.skipped_sessions.to_csv(args.output / "skipped_sessions.csv", index=False)
    with (args.output / "summary.json").open("w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, allow_nan=False)
        handle.write("\n")

    overall = report["overall"]
    print(f"experiment={config.experiment.name}")
    print(f"final_test={'included' if args.include_final_test else 'locked'}")
    print(f"bars={len(bars)} trades={overall['trades']} skipped={len(result.skipped_sessions)}")
    print(f"artifacts={args.output.resolve()}")


if __name__ == "__main__":
    main()

