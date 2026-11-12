from __future__ import annotations

import json
import sys
from dataclasses import replace
from datetime import date, datetime, time
from zoneinfo import ZoneInfo

import pandas as pd
import pytest

from xau_orb.gc_config import (
    ALLOWED_MODES,
    GcCostConfig,
    GcExperimentConfig,
    GcInstrumentConfig,
    GcProvenanceConfig,
    GcResearchConfig,
    GcSessionConfig,
    GcStrategyConfig,
    load_gc_config,
    validate_gc_config,
)
from xau_orb.gc_data import load_gc_m1_csv, load_gc_roll_schedule
from xau_orb.gc_engine import run_gc_close_replication, stable_random_key
from xau_orb.gc_metrics import gc_full_report
from xau_orb.gc_cli import main as gc_cli_main


def gc_config(start: date, end: date) -> GcResearchConfig:
    config = GcResearchConfig(
        GcExperimentConfig("EXP-010-test", start, end),
        GcProvenanceConfig("synthetic-test-only", "unit-test", "trade", "UTC"),
        GcInstrumentConfig("GC", 100, 0.10),
        GcSessionConfig(
            "America/New_York", time(8, 20), time(13), time(13, 30), 280
        ),
        GcStrategyConfig(tuple(ALLOWED_MODES), 20261102, 1, "skip"),
        GcCostConfig(1.0, 0.5, 2.50),
    )
    validate_gc_config(config)
    return config


def session_bars(
    session_date: date,
    contract: str,
    price: float,
    signal_close: float | None = None,
    entry_open: float | None = None,
    exit_open: float | None = None,
    session_close: float | None = None,
) -> pd.DataFrame:
    start = datetime.combine(
        session_date, time(8, 20), tzinfo=ZoneInfo("America/New_York")
    )
    index = pd.date_range(start, periods=311, freq="1min").tz_convert("UTC")
    rows = pd.DataFrame(
        {
            "timestamp": index,
            "contract": contract,
            "open": price,
            "high": price,
            "low": price,
            "close": price,
            "volume": 1,
        }
    )

    def set_bar(local_clock: time, *, open_value=None, close_value=None) -> None:
        stamp = pd.Timestamp(
            datetime.combine(
                session_date, local_clock, tzinfo=ZoneInfo("America/New_York")
            )
        ).tz_convert("UTC")
        row = rows["timestamp"] == stamp
        if open_value is not None:
            rows.loc[row, ["open", "high", "low", "close"]] = open_value
        if close_value is not None:
            rows.loc[row, ["high", "low", "close"]] = close_value

    set_bar(time(12, 59), close_value=signal_close or price)
    set_bar(time(13), open_value=entry_open or price)
    set_bar(time(13, 29), close_value=session_close or price)
    set_bar(time(13, 30), open_value=exit_open or price)
    return rows


def schedule(*rows: tuple[date, str]) -> pd.DataFrame:
    return pd.DataFrame(rows, columns=["session_date", "contract"])


def test_stable_random_key_matches_ctrader_control() -> None:
    key = stable_random_key(date(2020, 5, 7), 20261102)
    assert key == 2773626253
    assert key & 1 == 1  # sell


def test_protocol_rejects_time_or_seed_changes() -> None:
    config = gc_config(date(2026, 3, 9), date(2026, 3, 10))
    changed_time = replace(
        config,
        session=replace(
            config.session,
            session_open=time(8, 30),
            required_predictor_bars=270,
        ),
    )
    with pytest.raises(ValueError, match="locks session times"):
        validate_gc_config(changed_time)

    changed_seed = replace(
        config, strategy=replace(config.strategy, random_seed=12345)
    )
    with pytest.raises(ValueError, match="locks deterministic_random seed"):
        validate_gc_config(changed_seed)


def test_four_modes_share_dates_and_costs_are_explicit() -> None:
    first = session_bars(
        date(2026, 3, 9), "GCJ26", 2000, session_close=2000
    )
    second = session_bars(
        date(2026, 3, 10),
        "GCJ26",
        2010,
        signal_close=2010,
        entry_open=2010,
        exit_open=2012,
        session_close=2011,
    )
    config = gc_config(date(2026, 3, 9), date(2026, 3, 10))
    result = run_gc_close_replication(
        pd.concat([first, second], ignore_index=True),
        schedule((date(2026, 3, 9), "GCJ26"), (date(2026, 3, 10), "GCJ26")),
        config,
    )

    assert len(result.trades) == 4
    assert set(result.trades["mode"]) == set(ALLOWED_MODES)
    assert set(result.trades["session_date"]) == {"2026-03-10"}
    momentum = result.trades.set_index("mode").loc["momentum"]
    assert momentum.direction == "long"
    assert momentum.predictor_return == pytest.approx(0.005)
    assert momentum.gross_pnl_usd == pytest.approx(200.0)
    assert momentum.spread_cost_usd == pytest.approx(10.0)
    assert momentum.slippage_cost_usd == pytest.approx(10.0)
    assert momentum.commission_usd == pytest.approx(5.0)
    assert momentum.net_pnl_usd == pytest.approx(175.0)
    assert momentum.entry_fill == pytest.approx(2010.10)
    assert momentum.exit_fill == pytest.approx(2011.90)
    assert result.skipped_sessions.iloc[0].reason == "no_prior_valid_close"
    assert pd.Timestamp(momentum.entry_at) == pd.Timestamp("2026-03-10T17:00:00Z")


def test_contract_roll_skips_cross_contract_return_then_resumes() -> None:
    bars = pd.concat(
        [
            session_bars(date(2026, 4, 1), "GCJ26", 2000),
            session_bars(date(2026, 4, 2), "GCM26", 2100, session_close=2100),
            session_bars(
                date(2026, 4, 3),
                "GCM26",
                2110,
                signal_close=2110,
                entry_open=2110,
                exit_open=2111,
            ),
        ],
        ignore_index=True,
    )
    result = run_gc_close_replication(
        bars,
        schedule(
            (date(2026, 4, 1), "GCJ26"),
            (date(2026, 4, 2), "GCM26"),
            (date(2026, 4, 3), "GCM26"),
        ),
        gc_config(date(2026, 4, 1), date(2026, 4, 3)),
    )

    reasons = dict(
        zip(result.skipped_sessions.session_date, result.skipped_sessions.reason)
    )
    assert reasons["2026-04-01"] == "no_prior_valid_close"
    assert reasons["2026-04-02"] == "contract_roll"
    assert set(result.trades.session_date) == {"2026-04-03"}
    assert set(result.trades.prior_contract) == {"GCM26"}


def test_missing_predictor_minute_is_never_filled() -> None:
    first = session_bars(date(2026, 3, 9), "GCJ26", 2000)
    second = session_bars(date(2026, 3, 10), "GCJ26", 2010)
    missing = pd.Timestamp("2026-03-10T14:00:00Z")
    second = second.loc[second.timestamp != missing]
    result = run_gc_close_replication(
        pd.concat([first, second], ignore_index=True),
        schedule((date(2026, 3, 9), "GCJ26"), (date(2026, 3, 10), "GCJ26")),
        gc_config(date(2026, 3, 9), date(2026, 3, 10)),
    )
    skipped = result.skipped_sessions.set_index("session_date")
    assert skipped.loc["2026-03-10", "reason"] == "incomplete_predictor_session"
    assert skipped.loc["2026-03-10", "missing_minutes"] == 1
    assert result.trades.empty


def test_engine_rejects_off_tick_gc_prices() -> None:
    bars = session_bars(date(2026, 3, 9), "GCJ26", 2000)
    bars["open"] = bars["open"].astype(float)
    bars.loc[bars.index[0], "open"] = 2000.05
    with pytest.raises(ValueError, match="not aligned to tick_size"):
        run_gc_close_replication(
            bars,
            schedule((date(2026, 3, 9), "GCJ26")),
            gc_config(date(2026, 3, 9), date(2026, 3, 9)),
        )


def test_loader_requires_contract_and_aware_timestamps(tmp_path) -> None:
    naive = tmp_path / "naive.csv"
    naive.write_text(
        "timestamp,contract,open,high,low,close\n"
        "2026-03-10 12:59:00,GCJ26,2000,2000,2000,2000\n",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="no UTC offset"):
        load_gc_m1_csv(naive)

    duplicate = tmp_path / "duplicate.csv"
    duplicate.write_text(
        "timestamp,contract,open,high,low,close\n"
        "2026-03-10T16:59:00Z,GCJ26,2000,2000,2000,2000\n"
        "2026-03-10T16:59:00Z,GCJ26,2000,2000,2000,2000\n",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="duplicate contract/timestamp"):
        load_gc_m1_csv(duplicate)


def test_roll_loader_rejects_ambiguous_contract_mapping(tmp_path) -> None:
    path = tmp_path / "roll.csv"
    path.write_text(
        "session_date,contract\n"
        "2026-03-10,GCJ26\n"
        "2026-03-10,GCM26\n",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="exactly once"):
        load_gc_roll_schedule(path)


def test_config_template_refuses_missing_provenance_and_costs() -> None:
    with pytest.raises(ValueError, match="start_date.*REQUIRED placeholder"):
        load_gc_config("configs/exp010_gc_close_momentum.template.yaml")


def test_report_contains_mode_and_year_breakdowns() -> None:
    first = session_bars(date(2026, 1, 5), "GCG26", 2000)
    second = session_bars(
        date(2026, 1, 6),
        "GCG26",
        2010,
        signal_close=2010,
        entry_open=2010,
        exit_open=2012,
    )
    result = run_gc_close_replication(
        pd.concat([first, second], ignore_index=True),
        schedule((date(2026, 1, 5), "GCG26"), (date(2026, 1, 6), "GCG26")),
        gc_config(date(2026, 1, 5), date(2026, 1, 6)),
    )
    report = gc_full_report(result.trades, result.skipped_sessions)
    assert set(report["by_mode"]) == set(ALLOWED_MODES)
    assert report["by_mode"]["momentum"]["trades"] == 1
    assert report["by_mode_year"]["momentum"]["2026"]["net_pnl_usd"] == pytest.approx(
        175.0
    )


def test_cli_writes_hashed_artifacts_and_refuses_overwrite(
    tmp_path, monkeypatch
) -> None:
    config_path = tmp_path / "config.yaml"
    data_path = tmp_path / "bars.csv"
    roll_path = tmp_path / "roll.csv"
    output = tmp_path / "result"
    config_path.write_text(
        """experiment:
  name: EXP-010-cli-test
  start_date: "2026-03-09"
  end_date: "2026-03-10"
provenance:
  provider: synthetic-test-only
  dataset: cli-unit-test
  price_basis: trade
  source_timezone: UTC
instrument:
  symbol: GC
  contract_size_oz: 100
  tick_size: 0.10
session:
  timezone: America/New_York
  session_open: "08:20"
  signal_time: "13:00"
  exit_time: "13:30"
  required_predictor_bars: 280
strategy:
  direction_modes: [momentum, always_long, always_short, deterministic_random]
  random_seed: 20261102
  contracts_per_trade: 1
  cross_contract_predictor: skip
costs:
  bid_ask_spread_ticks: 1
  slippage_ticks_per_side: 0.5
  commission_per_side_usd: 2.5
""",
        encoding="utf-8",
    )
    pd.concat(
        [
            session_bars(date(2026, 3, 9), "GCJ26", 2000),
            session_bars(
                date(2026, 3, 10),
                "GCJ26",
                2010,
                signal_close=2010,
                entry_open=2010,
                exit_open=2012,
            ),
        ],
        ignore_index=True,
    ).to_csv(data_path, index=False)
    schedule(
        (date(2026, 3, 9), "GCJ26"), (date(2026, 3, 10), "GCJ26")
    ).to_csv(roll_path, index=False)
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "gc-close-replication",
            "--config",
            str(config_path),
            "--data",
            str(data_path),
            "--roll-schedule",
            str(roll_path),
            "--output",
            str(output),
        ],
    )

    gc_cli_main()

    assert len(pd.read_csv(output / "trades.csv")) == 4
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["inputs"]["data"]["sha256"]
    assert manifest["locked_contract"]["tick_value_usd"] == pytest.approx(10.0)
    summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
    assert set(summary["by_mode"]) == set(ALLOWED_MODES)
    with pytest.raises(FileExistsError, match="Refusing to overwrite"):
        gc_cli_main()

