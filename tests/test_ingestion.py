from __future__ import annotations

import lzma
import struct
from pathlib import Path

import pandas as pd
import pytest

from xau_orb.ingestion import (
    aggregate_ticks_to_m1,
    build_quality_report,
    decode_bi5,
    identify_raw_file,
    validate_price_scale,
)


def write_bi5(path: Path, rows: list[tuple[int, int, int, float, float]]) -> None:
    path.parent.mkdir(parents=True)
    payload = b"".join(struct.pack(">IIIff", *row) for row in rows)
    path.write_bytes(lzma.compress(payload))


def test_daily_decoder_uses_zero_based_month_and_explicit_scale(tmp_path) -> None:
    path = tmp_path / "XAUUSD" / "2025" / "09" / "06_ticks.bi5"
    write_bi5(path, [(1_000, 265_025, 265_015, 2.0, 3.0)])

    ticks = decode_bi5(path, price_scale=100)

    assert ticks.loc[0, "timestamp"] == pd.Timestamp("2025-10-06T00:00:01Z")
    assert ticks.loc[0, "ask"] == pytest.approx(2650.25)
    assert ticks.loc[0, "bid"] == pytest.approx(2650.15)
    assert ticks.loc[0, "ask_size"] == pytest.approx(2.0)
    assert ticks.loc[0, "bid_size"] == pytest.approx(3.0)


def test_legacy_hourly_decoder_uses_hour_start(tmp_path) -> None:
    path = tmp_path / "XAUUSD" / "2020" / "09" / "05" / "14h_ticks.bi5"
    write_bi5(path, [(30_000, 191_025, 191_015, 1.0, 1.0)])

    raw_file = identify_raw_file(path)
    ticks = decode_bi5(path, price_scale=100)

    assert raw_file.layout == "legacy-hourly"
    assert ticks.loc[0, "timestamp"] == pd.Timestamp("2020-10-05T14:00:30Z")


def test_decoder_rejects_crossed_market(tmp_path) -> None:
    path = tmp_path / "XAUUSD" / "2025" / "09" / "06_ticks.bi5"
    write_bi5(path, [(1_000, 265_000, 265_010, 1.0, 1.0)])

    with pytest.raises(ValueError, match="Ask below bid"):
        decode_bi5(path, price_scale=100)


def test_scale_validation_rejects_implausible_decoding() -> None:
    ticks = pd.DataFrame(
        {
            "bid": [26.50, 26.51],
            "ask": [26.51, 26.52],
        }
    )
    with pytest.raises(ValueError, match="does not match"):
        validate_price_scale(ticks, reference_price=2650.0, tolerance_percent=5)


def test_tick_aggregation_preserves_bid_ask_and_midpoint() -> None:
    ticks = pd.DataFrame(
        {
            "timestamp": pd.to_datetime(
                [
                    "2025-10-06T14:30:01Z",
                    "2025-10-06T14:30:20Z",
                    "2025-10-06T14:31:01Z",
                ]
            ),
            "bid": [2650.0, 2650.2, 2650.1],
            "ask": [2650.2, 2650.4, 2650.3],
            "bid_size": [1.0, 2.0, 3.0],
            "ask_size": [4.0, 5.0, 6.0],
        }
    )

    bars = aggregate_ticks_to_m1(ticks)

    first = bars.iloc[0]
    assert first["timestamp"] == pd.Timestamp("2025-10-06T14:30:00Z")
    assert first["bid_open"] == pytest.approx(2650.0)
    assert first["bid_close"] == pytest.approx(2650.2)
    assert first["ask_high"] == pytest.approx(2650.4)
    assert first["mid_open"] == pytest.approx(2650.1)
    assert first["mid_close"] == pytest.approx(2650.3)
    assert first["tick_count"] == 2


def test_quality_report_does_not_fill_missing_minutes() -> None:
    ticks = pd.DataFrame(
        {
            "timestamp": pd.to_datetime(
                ["2025-10-06T13:30:01Z", "2025-10-06T13:32:01Z"]
            ),
            "bid": [2650.0, 2651.0],
            "ask": [2650.2, 2651.2],
            "bid_size": [1.0, 1.0],
            "ask_size": [1.0, 1.0],
        }
    )
    bars = aggregate_ticks_to_m1(ticks)

    report = build_quality_report(ticks, bars)

    assert len(bars) == 2
    assert report["gaps_over_60_seconds"] == 1
    sessions = report["new_york_cash_session_completeness"]
    assert sessions[0]["missing_minutes"] == 388
