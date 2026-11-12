from __future__ import annotations

from pathlib import Path

import pandas as pd


REQUIRED_COLUMNS = ("timestamp", "open", "high", "low", "close")
PRICE_COLUMNS = ("open", "high", "low", "close")


def load_m1_csv(path: str | Path) -> pd.DataFrame:
    frame = pd.read_csv(path)
    missing = [column for column in REQUIRED_COLUMNS if column not in frame.columns]
    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    parsed: list[pd.Timestamp] = []
    for row_number, value in enumerate(frame["timestamp"], start=2):
        timestamp = pd.Timestamp(value)
        if timestamp.tzinfo is None or timestamp.utcoffset() is None:
            raise ValueError(
                f"Timestamp on CSV row {row_number} has no UTC offset: {value!r}"
            )
        parsed.append(timestamp.tz_convert("UTC"))

    frame = frame.copy()
    frame["timestamp"] = pd.DatetimeIndex(parsed)
    for column in PRICE_COLUMNS:
        frame[column] = pd.to_numeric(frame[column], errors="raise")
    if frame[list(PRICE_COLUMNS)].isna().any().any():
        raise ValueError("OHLC values cannot be null")
    if (frame[list(PRICE_COLUMNS)] <= 0).any().any():
        raise ValueError("OHLC values must be positive")
    if (frame["high"] < frame[["open", "low", "close"]].max(axis=1)).any():
        raise ValueError("Found a row whose high is below another OHLC value")
    if (frame["low"] > frame[["open", "high", "close"]].min(axis=1)).any():
        raise ValueError("Found a row whose low is above another OHLC value")
    if frame["timestamp"].duplicated().any():
        raise ValueError("Duplicate timestamps are not allowed")

    return frame.sort_values("timestamp").set_index("timestamp")

