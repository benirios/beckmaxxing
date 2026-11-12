from __future__ import annotations

import pytest

from xau_orb.data import load_m1_csv


def test_loader_rejects_naive_timestamps(tmp_path) -> None:
    path = tmp_path / "bars.csv"
    path.write_text(
        "timestamp,open,high,low,close\n"
        "2026-01-05 14:30:00,100,101,99,100\n",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="no UTC offset"):
        load_m1_csv(path)


def test_loader_normalizes_offset_to_utc(tmp_path) -> None:
    path = tmp_path / "bars.csv"
    path.write_text(
        "timestamp,open,high,low,close\n"
        "2026-03-09T09:30:00-04:00,100,101,99,100\n",
        encoding="utf-8",
    )
    bars = load_m1_csv(path)
    assert str(bars.index[0]) == "2026-03-09 13:30:00+00:00"

