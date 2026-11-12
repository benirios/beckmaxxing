from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime, timedelta
from zoneinfo import ZoneInfo

import pandas as pd

from .config import CostConfig, ResearchConfig


TRADE_COLUMNS = (
    "session_date",
    "split",
    "direction",
    "signal_at",
    "entry_at",
    "exit_at",
    "range_high",
    "range_low",
    "entry_raw",
    "entry_fill",
    "stop",
    "target",
    "exit_raw",
    "exit_fill",
    "exit_reason",
    "risk_per_oz",
    "gross_pnl_per_oz",
    "net_pnl_per_oz",
    "gross_r",
    "net_r",
)
SKIPPED_COLUMNS = (
    "session_date",
    "reason",
    "missing_minutes",
    "first_missing",
)


@dataclass(frozen=True)
class BacktestResult:
    trades: pd.DataFrame
    skipped_sessions: pd.DataFrame


def _utc_bound(session_date: date, clock, timezone: str) -> pd.Timestamp:
    local = datetime.combine(session_date, clock, tzinfo=ZoneInfo(timezone))
    return pd.Timestamp(local).tz_convert("UTC")


def _split_label(session_date: date, config: ResearchConfig) -> str:
    if session_date <= config.validation.train_end:
        return "train"
    if session_date <= config.validation.validation_end:
        return "validation"
    return "final_test"


def _fills(
    direction: str,
    entry_raw: float,
    exit_raw: float,
    costs: CostConfig,
) -> tuple[float, float, float, float]:
    half_spread = costs.round_trip_spread / 2.0
    friction = half_spread + costs.slippage_per_side
    if direction == "long":
        entry_fill = entry_raw + friction
        exit_fill = exit_raw - friction
        gross = exit_raw - entry_raw
        net = exit_fill - entry_fill - costs.round_trip_commission
    else:
        entry_fill = entry_raw - friction
        exit_fill = exit_raw + friction
        gross = entry_raw - exit_raw
        net = entry_fill - exit_fill - costs.round_trip_commission
    return entry_fill, exit_fill, gross, net


def _resolve_exit(
    trade_bars: pd.DataFrame,
    direction: str,
    stop: float,
    target: float,
) -> tuple[pd.Timestamp, float, str]:
    for timestamp, bar in trade_bars.iterrows():
        if direction == "long":
            if bar.open <= stop:
                return timestamp, float(bar.open), "stop_gap"
            if bar.open >= target:
                return timestamp, target, "target"
            stop_hit = bar.low <= stop
            target_hit = bar.high >= target
        else:
            if bar.open >= stop:
                return timestamp, float(bar.open), "stop_gap"
            if bar.open <= target:
                return timestamp, target, "target"
            stop_hit = bar.high >= stop
            target_hit = bar.low <= target

        if stop_hit and target_hit:
            return timestamp, stop, "ambiguous_stop_first"
        if stop_hit:
            return timestamp, stop, "stop"
        if target_hit:
            return timestamp, target, "target"

    last_timestamp = trade_bars.index[-1]
    return last_timestamp + timedelta(minutes=1), float(trade_bars.iloc[-1].close), "time"


def run_backtest(bars: pd.DataFrame, config: ResearchConfig) -> BacktestResult:
    if not isinstance(bars.index, pd.DatetimeIndex) or bars.index.tz is None:
        raise ValueError("bars must use a timezone-aware DatetimeIndex")
    if not bars.index.is_monotonic_increasing or bars.index.has_duplicates:
        raise ValueError("bars index must be sorted and unique")

    local_index = bars.index.tz_convert(config.session.timezone)
    candidate_dates = sorted(set(local_index.date))
    trades: list[dict] = []
    skipped: list[dict] = []

    for session_date in candidate_dates:
        opening = _utc_bound(
            session_date, config.session.opening_time, config.session.timezone
        )
        range_end = opening + timedelta(minutes=config.session.opening_range_minutes)
        entry_deadline = _utc_bound(
            session_date, config.session.entry_deadline, config.session.timezone
        )
        exit_time = _utc_bound(
            session_date, config.session.exit_time, config.session.timezone
        )

        expected = pd.date_range(opening, exit_time, inclusive="left", freq="1min")
        session_bars = bars.loc[(bars.index >= opening) & (bars.index < exit_time)]
        missing = expected.difference(session_bars.index)
        if len(missing) > config.session.max_missing_minutes:
            skipped.append(
                {
                    "session_date": session_date.isoformat(),
                    "reason": "missing_required_bars",
                    "missing_minutes": len(missing),
                    "first_missing": missing[0].isoformat() if len(missing) else None,
                }
            )
            continue

        opening_bars = session_bars.loc[
            (session_bars.index >= opening) & (session_bars.index < range_end)
        ]
        if len(opening_bars) != config.session.opening_range_minutes:
            skipped.append(
                {
                    "session_date": session_date.isoformat(),
                    "reason": "incomplete_opening_range",
                    "missing_minutes": config.session.opening_range_minutes
                    - len(opening_bars),
                    "first_missing": None,
                }
            )
            continue

        range_high = float(opening_bars.high.max())
        range_low = float(opening_bars.low.min())
        confirmations = session_bars.loc[
            (session_bars.index >= range_end)
            & (session_bars.index < entry_deadline)
            & ((session_bars.close > range_high) | (session_bars.close < range_low))
        ]
        if confirmations.empty:
            continue

        signal_at = confirmations.index[0]
        signal_bar = confirmations.iloc[0]
        direction = "long" if signal_bar.close > range_high else "short"
        entry_at = signal_at + timedelta(minutes=1)
        if entry_at not in session_bars.index or entry_at > entry_deadline:
            skipped.append(
                {
                    "session_date": session_date.isoformat(),
                    "reason": "missing_entry_bar",
                    "missing_minutes": 1,
                    "first_missing": entry_at.isoformat(),
                }
            )
            continue

        entry_raw = float(session_bars.loc[entry_at, "open"])
        stop = range_low if direction == "long" else range_high
        risk = abs(entry_raw - stop)
        if risk <= 0:
            skipped.append(
                {
                    "session_date": session_date.isoformat(),
                    "reason": "non_positive_risk",
                    "missing_minutes": 0,
                    "first_missing": None,
                }
            )
            continue
        target = (
            entry_raw + config.strategy.target_r * risk
            if direction == "long"
            else entry_raw - config.strategy.target_r * risk
        )
        trade_bars = session_bars.loc[
            (session_bars.index >= entry_at) & (session_bars.index < exit_time)
        ]
        exit_at, exit_raw, exit_reason = _resolve_exit(
            trade_bars, direction, stop, target
        )
        entry_fill, exit_fill, gross_pnl, net_pnl = _fills(
            direction, entry_raw, exit_raw, config.costs
        )
        trades.append(
            {
                "session_date": session_date.isoformat(),
                "split": _split_label(session_date, config),
                "direction": direction,
                "signal_at": signal_at.isoformat(),
                "entry_at": entry_at.isoformat(),
                "exit_at": exit_at.isoformat(),
                "range_high": range_high,
                "range_low": range_low,
                "entry_raw": entry_raw,
                "entry_fill": entry_fill,
                "stop": stop,
                "target": target,
                "exit_raw": exit_raw,
                "exit_fill": exit_fill,
                "exit_reason": exit_reason,
                "risk_per_oz": risk,
                "gross_pnl_per_oz": gross_pnl,
                "net_pnl_per_oz": net_pnl,
                "gross_r": gross_pnl / risk,
                "net_r": net_pnl / risk,
            }
        )

    return BacktestResult(
        pd.DataFrame(trades, columns=TRADE_COLUMNS),
        pd.DataFrame(skipped, columns=SKIPPED_COLUMNS),
    )

