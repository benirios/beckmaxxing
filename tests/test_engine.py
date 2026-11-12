from __future__ import annotations

from datetime import date, datetime, time, timedelta
from zoneinfo import ZoneInfo

import pandas as pd
import pytest

from xau_orb.config import (
    CostConfig,
    ExperimentConfig,
    ResearchConfig,
    SessionConfig,
    StrategyConfig,
    ValidationConfig,
)
from xau_orb.engine import run_backtest


def config() -> ResearchConfig:
    return ResearchConfig(
        ExperimentConfig("test", "XAUUSD", "mid"),
        SessionConfig("America/New_York", time(9, 30), 15, time(12), time(16), 0),
        StrategyConfig(
            "close", "next_bar_open", "opposite_range_boundary", 1.0, 1, "stop_first"
        ),
        CostConfig(0.30, 0.02, 0.0),
        ValidationConfig(date(2016, 12, 31), date(2021, 12, 31)),
    )


def session_bars(session_date: date, ambiguous: bool = False) -> pd.DataFrame:
    start_local = datetime.combine(
        session_date, time(9, 30), tzinfo=ZoneInfo("America/New_York")
    )
    index = pd.date_range(start_local, periods=390, freq="1min").tz_convert("UTC")
    frame = pd.DataFrame(
        {"open": 100.0, "high": 100.5, "low": 99.5, "close": 100.0},
        index=index,
    )
    frame.index.name = "timestamp"
    frame.iloc[:15, frame.columns.get_loc("high")] = 101.0
    frame.iloc[:15, frame.columns.get_loc("low")] = 99.0
    frame.iloc[15] = [100.0, 101.3, 99.8, 101.2]
    frame.iloc[16] = [101.2, 101.4, 101.1, 101.3]
    if ambiguous:
        frame.iloc[17] = [101.3, 103.5, 98.8, 101.5]
    else:
        frame.iloc[17] = [101.3, 103.5, 101.2, 103.0]
    return frame


def test_close_confirmation_enters_next_bar_and_deducts_costs() -> None:
    result = run_backtest(session_bars(date(2026, 3, 9)), config())
    assert len(result.trades) == 1
    trade = result.trades.iloc[0]
    assert trade.direction == "long"
    assert trade.entry_raw == pytest.approx(101.2)
    assert trade.stop == pytest.approx(99.0)
    assert trade.target == pytest.approx(103.4)
    assert trade.exit_reason == "target"
    assert trade.gross_r == pytest.approx(1.0)
    assert trade.net_pnl_per_oz == pytest.approx(2.2 - 0.30 - 0.04)
    assert trade.net_r < 1.0
    assert pd.Timestamp(trade.signal_at).hour == 13  # EDT is UTC-4.


def test_same_bar_stop_and_target_is_resolved_stop_first() -> None:
    result = run_backtest(session_bars(date(2026, 1, 5), ambiguous=True), config())
    trade = result.trades.iloc[0]
    assert trade.exit_reason == "ambiguous_stop_first"
    assert trade.exit_raw == pytest.approx(99.0)
    assert trade.net_r < -1.0
    assert pd.Timestamp(trade.signal_at).hour == 14  # EST is UTC-5.


def test_missing_minute_skips_session_instead_of_filling() -> None:
    bars = session_bars(date(2026, 3, 9)).drop(
        session_bars(date(2026, 3, 9)).index[5]
    )
    result = run_backtest(bars, config())
    assert result.trades.empty
    assert result.skipped_sessions.iloc[0].reason == "missing_required_bars"
    assert result.skipped_sessions.iloc[0].missing_minutes == 1

