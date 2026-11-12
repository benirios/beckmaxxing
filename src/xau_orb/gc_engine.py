from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime, timedelta
from zoneinfo import ZoneInfo

import pandas as pd

from .gc_config import GcResearchConfig


GC_TRADE_COLUMNS = (
    "session_date",
    "contract",
    "mode",
    "random_key",
    "direction",
    "prior_close_date",
    "prior_contract",
    "prior_close",
    "signal_close",
    "predictor_return",
    "entry_at",
    "exit_at",
    "entry_raw",
    "exit_raw",
    "entry_fill",
    "exit_fill",
    "contracts",
    "gross_pnl_usd",
    "spread_cost_usd",
    "slippage_cost_usd",
    "commission_usd",
    "net_pnl_usd",
)
GC_SKIPPED_COLUMNS = (
    "session_date",
    "contract",
    "reason",
    "missing_minutes",
    "first_missing",
    "details",
)


@dataclass(frozen=True)
class GcBacktestResult:
    trades: pd.DataFrame
    skipped_sessions: pd.DataFrame


def stable_random_key(session_date: date, seed: int) -> int:
    value = session_date.year * 10000 + session_date.month * 100 + session_date.day
    value = (value & 0xFFFFFFFF) ^ (seed & 0xFFFFFFFF)
    value = (value ^ (value >> 16)) & 0xFFFFFFFF
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value = (value ^ (value >> 15)) & 0xFFFFFFFF
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    return (value ^ (value >> 16)) & 0xFFFFFFFF


def _utc_bound(session_date: date, clock, timezone: str) -> pd.Timestamp:
    local = datetime.combine(session_date, clock, tzinfo=ZoneInfo(timezone))
    return pd.Timestamp(local).tz_convert("UTC")


def _direction(mode: str, predictor_return: float, random_key: int) -> str:
    if mode == "momentum":
        return "long" if predictor_return > 0 else "short"
    if mode == "always_long":
        return "long"
    if mode == "always_short":
        return "short"
    if mode == "deterministic_random":
        return "long" if random_key & 1 == 0 else "short"
    raise ValueError(f"Unsupported direction mode: {mode}")


def _fills_and_pnl(
    direction: str,
    entry_raw: float,
    exit_raw: float,
    config: GcResearchConfig,
) -> dict[str, float]:
    contracts = config.strategy.contracts_per_trade
    tick_size = config.instrument.tick_size
    multiplier = config.instrument.contract_size_oz
    half_spread = config.costs.bid_ask_spread_ticks * tick_size / 2.0
    slippage = config.costs.slippage_ticks_per_side * tick_size
    side_friction = half_spread + slippage

    if direction == "long":
        entry_fill = entry_raw + side_friction
        exit_fill = exit_raw - side_friction
        raw_move = exit_raw - entry_raw
        fill_move = exit_fill - entry_fill
    else:
        entry_fill = entry_raw - side_friction
        exit_fill = exit_raw + side_friction
        raw_move = entry_raw - exit_raw
        fill_move = entry_fill - exit_fill

    gross = raw_move * multiplier * contracts
    execution_pnl = fill_move * multiplier * contracts
    spread_cost = (
        config.costs.bid_ask_spread_ticks
        * config.instrument.tick_value_usd
        * contracts
    )
    slippage_cost = (
        2
        * config.costs.slippage_ticks_per_side
        * config.instrument.tick_value_usd
        * contracts
    )
    commission = 2 * config.costs.commission_per_side_usd * contracts
    return {
        "entry_fill": entry_fill,
        "exit_fill": exit_fill,
        "gross_pnl_usd": gross,
        "spread_cost_usd": spread_cost,
        "slippage_cost_usd": slippage_cost,
        "commission_usd": commission,
        "net_pnl_usd": execution_pnl - commission,
    }


def run_gc_close_replication(
    bars: pd.DataFrame,
    roll_schedule: pd.DataFrame,
    config: GcResearchConfig,
) -> GcBacktestResult:
    required_bar_columns = {"timestamp", "contract", "open", "high", "low", "close"}
    missing_bar_columns = sorted(required_bar_columns.difference(bars.columns))
    if missing_bar_columns:
        raise ValueError(f"bars missing columns: {missing_bar_columns}")
    if not pd.api.types.is_datetime64_any_dtype(bars["timestamp"]):
        raise ValueError("bars.timestamp must be parsed datetimes")
    if bars["timestamp"].dt.tz is None:
        raise ValueError("bars.timestamp must be timezone-aware")
    if bars.duplicated(["contract", "timestamp"]).any():
        raise ValueError("bars contain duplicate contract/timestamp keys")
    if roll_schedule["session_date"].duplicated().any():
        raise ValueError("roll_schedule contains duplicate session dates")

    tick_size = config.instrument.tick_size
    for column in ("open", "high", "low", "close"):
        tick_units = bars[column].astype(float) / tick_size
        off_tick = (tick_units - tick_units.round()).abs() > 1e-7
        if off_tick.any():
            first = bars.loc[off_tick, ["timestamp", "contract", column]].iloc[0]
            raise ValueError(
                f"GC {column} is not aligned to tick_size={tick_size}: "
                f"contract={first['contract']} timestamp={first['timestamp']} "
                f"value={first[column]}"
            )

    schedule = roll_schedule.loc[
        (roll_schedule["session_date"] >= config.experiment.start_date)
        & (roll_schedule["session_date"] <= config.experiment.end_date)
    ].copy()
    if schedule.empty:
        raise ValueError("Roll schedule has no dates inside the experiment window")

    bars_by_contract = {
        str(contract): group.sort_values("timestamp", kind="stable").set_index(
            "timestamp", drop=False
        )
        for contract, group in bars.groupby("contract", sort=False, observed=True)
    }
    empty_session = bars.iloc[0:0].set_index("timestamp", drop=False)

    trades: list[dict[str, object]] = []
    skipped: list[dict[str, object]] = []
    previous_close: float | None = None
    previous_close_date: date | None = None
    previous_contract: str | None = None

    for scheduled in schedule.itertuples(index=False):
        session_date = scheduled.session_date
        contract = str(scheduled.contract)
        session_open = _utc_bound(
            session_date, config.session.session_open, config.session.timezone
        )
        signal_at = _utc_bound(
            session_date, config.session.signal_time, config.session.timezone
        )
        exit_at = _utc_bound(
            session_date, config.session.exit_time, config.session.timezone
        )
        predictor_close_at = signal_at - timedelta(minutes=1)
        session_close_at = exit_at - timedelta(minutes=1)

        contract_bars = bars_by_contract.get(contract)
        session_bars = (
            empty_session
            if contract_bars is None
            else contract_bars.loc[session_open:exit_at]
        )

        expected_predictor = pd.date_range(
            session_open, signal_at, inclusive="left", freq="1min"
        )
        predictor_bars = session_bars.loc[
            (session_bars.index >= session_open) & (session_bars.index < signal_at)
        ]
        missing_predictor = expected_predictor.difference(predictor_bars.index)

        reason: str | None = None
        details = ""
        missing_minutes = 0
        first_missing: str | None = None
        if session_bars.empty:
            reason = "missing_contract_data"
            details = "No selected-contract bars in the required session window"
        elif (
            len(missing_predictor) > 0
            or len(predictor_bars) != config.session.required_predictor_bars
        ):
            reason = "incomplete_predictor_session"
            missing_minutes = len(missing_predictor)
            first_missing = (
                missing_predictor[0].isoformat() if len(missing_predictor) else None
            )
        elif signal_at not in session_bars.index:
            reason = "missing_signal_bar"
            missing_minutes = 1
            first_missing = signal_at.isoformat()
        elif exit_at not in session_bars.index:
            reason = "missing_exit_bar"
            missing_minutes = 1
            first_missing = exit_at.isoformat()
        elif predictor_close_at not in session_bars.index:
            reason = "missing_predictor_close"
            missing_minutes = 1
            first_missing = predictor_close_at.isoformat()
        elif previous_close is None or previous_close_date is None or previous_contract is None:
            reason = "no_prior_valid_close"
        elif previous_contract != contract:
            reason = "contract_roll"
            details = f"prior_contract={previous_contract}; current_contract={contract}"
        else:
            signal_close = float(session_bars.loc[predictor_close_at, "close"])
            predictor_return = signal_close / previous_close - 1.0
            if predictor_return == 0:
                reason = "zero_predictor_return"
            else:
                entry_raw = float(session_bars.loc[signal_at, "open"])
                exit_raw = float(session_bars.loc[exit_at, "open"])
                random_key = stable_random_key(
                    session_date, config.strategy.random_seed
                )
                for mode in config.strategy.direction_modes:
                    direction = _direction(mode, predictor_return, random_key)
                    economics = _fills_and_pnl(
                        direction, entry_raw, exit_raw, config
                    )
                    trades.append(
                        {
                            "session_date": session_date.isoformat(),
                            "contract": contract,
                            "mode": mode,
                            "random_key": random_key,
                            "direction": direction,
                            "prior_close_date": previous_close_date.isoformat(),
                            "prior_contract": previous_contract,
                            "prior_close": previous_close,
                            "signal_close": signal_close,
                            "predictor_return": predictor_return,
                            "entry_at": signal_at.isoformat(),
                            "exit_at": exit_at.isoformat(),
                            "entry_raw": entry_raw,
                            "exit_raw": exit_raw,
                            "entry_fill": economics["entry_fill"],
                            "exit_fill": economics["exit_fill"],
                            "contracts": config.strategy.contracts_per_trade,
                            "gross_pnl_usd": economics["gross_pnl_usd"],
                            "spread_cost_usd": economics["spread_cost_usd"],
                            "slippage_cost_usd": economics["slippage_cost_usd"],
                            "commission_usd": economics["commission_usd"],
                            "net_pnl_usd": economics["net_pnl_usd"],
                        }
                    )

        if reason is not None:
            skipped.append(
                {
                    "session_date": session_date.isoformat(),
                    "contract": contract,
                    "reason": reason,
                    "missing_minutes": missing_minutes,
                    "first_missing": first_missing,
                    "details": details,
                }
            )

        # Store only an exact same-contract 13:29 close for the next scheduled
        # session. A missing scheduled session invalidates the chain rather than
        # silently bridging unknown data.
        if session_close_at in session_bars.index:
            close_value = float(session_bars.loc[session_close_at, "close"])
            if close_value > 0:
                previous_close = close_value
                previous_close_date = session_date
                previous_contract = contract
                continue
        previous_close = None
        previous_close_date = None
        previous_contract = None

    return GcBacktestResult(
        pd.DataFrame(trades, columns=GC_TRADE_COLUMNS),
        pd.DataFrame(skipped, columns=GC_SKIPPED_COLUMNS),
    )

