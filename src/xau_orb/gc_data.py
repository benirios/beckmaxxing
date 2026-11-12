from __future__ import annotations

from datetime import date
from pathlib import Path

import pandas as pd


GC_REQUIRED_COLUMNS = ("timestamp", "contract", "open", "high", "low", "close")
GC_PRICE_COLUMNS = ("open", "high", "low", "close")
ROLL_REQUIRED_COLUMNS = ("session_date", "contract")


def load_gc_m1_csv(path: str | Path) -> pd.DataFrame:
    frame = pd.read_csv(path)
    missing = [column for column in GC_REQUIRED_COLUMNS if column not in frame.columns]
    if missing:
        raise ValueError(f"Missing required GC columns: {missing}")
    if frame.empty:
        raise ValueError("GC M1 data file contains no rows")

    parsed: list[pd.Timestamp] = []
    for row_number, value in enumerate(frame["timestamp"], start=2):
        timestamp = pd.Timestamp(value)
        if timestamp.tzinfo is None or timestamp.utcoffset() is None:
            raise ValueError(
                f"Timestamp on GC CSV row {row_number} has no UTC offset: {value!r}"
            )
        parsed.append(timestamp.tz_convert("UTC"))

    result = frame.copy()
    result["timestamp"] = pd.DatetimeIndex(parsed)
    result["contract"] = result["contract"].astype("string").str.strip()
    if result["contract"].isna().any() or (result["contract"] == "").any():
        raise ValueError("GC contract identifiers cannot be blank")

    for column in GC_PRICE_COLUMNS:
        result[column] = pd.to_numeric(result[column], errors="raise")
    if result[list(GC_PRICE_COLUMNS)].isna().any().any():
        raise ValueError("GC OHLC values cannot be null")
    if (result[list(GC_PRICE_COLUMNS)] <= 0).any().any():
        raise ValueError("GC OHLC values must be positive")
    if (result["high"] < result[["open", "low", "close"]].max(axis=1)).any():
        raise ValueError("Found a GC row whose high is below another OHLC value")
    if (result["low"] > result[["open", "high", "close"]].min(axis=1)).any():
        raise ValueError("Found a GC row whose low is above another OHLC value")

    if "volume" in result.columns:
        result["volume"] = pd.to_numeric(result["volume"], errors="raise")
        if result["volume"].isna().any() or (result["volume"] < 0).any():
            raise ValueError("GC volume must be finite and non-negative")

    duplicate = result.duplicated(subset=["contract", "timestamp"], keep=False)
    if duplicate.any():
        raise ValueError(
            f"Found {int(duplicate.sum())} rows with duplicate contract/timestamp keys"
        )
    return result.sort_values(["timestamp", "contract"], kind="stable").reset_index(
        drop=True
    )


def load_gc_roll_schedule(path: str | Path) -> pd.DataFrame:
    frame = pd.read_csv(path)
    missing = [column for column in ROLL_REQUIRED_COLUMNS if column not in frame.columns]
    if missing:
        raise ValueError(f"Missing roll-schedule columns: {missing}")
    if frame.empty:
        raise ValueError("GC roll schedule contains no rows")

    result = frame.copy()
    parsed_dates: list[date] = []
    for row_number, value in enumerate(result["session_date"], start=2):
        try:
            parsed_dates.append(date.fromisoformat(str(value)))
        except ValueError as error:
            raise ValueError(
                f"Invalid ISO session_date on roll CSV row {row_number}: {value!r}"
            ) from error
    result["session_date"] = parsed_dates
    result["contract"] = result["contract"].astype("string").str.strip()
    if result["contract"].isna().any() or (result["contract"] == "").any():
        raise ValueError("Roll-schedule contract identifiers cannot be blank")
    if result["session_date"].duplicated().any():
        raise ValueError("Roll schedule must map each session_date exactly once")
    return result.sort_values("session_date", kind="stable").reset_index(drop=True)

