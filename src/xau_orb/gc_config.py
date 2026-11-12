from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime, time, timedelta
from math import isfinite
from pathlib import Path
from zoneinfo import ZoneInfo

import yaml


ALLOWED_MODES = (
    "momentum",
    "always_long",
    "always_short",
    "deterministic_random",
)


@dataclass(frozen=True)
class GcExperimentConfig:
    name: str
    start_date: date
    end_date: date


@dataclass(frozen=True)
class GcProvenanceConfig:
    provider: str
    dataset: str
    price_basis: str
    source_timezone: str


@dataclass(frozen=True)
class GcInstrumentConfig:
    symbol: str
    contract_size_oz: float
    tick_size: float

    @property
    def tick_value_usd(self) -> float:
        return self.contract_size_oz * self.tick_size


@dataclass(frozen=True)
class GcSessionConfig:
    timezone: str
    session_open: time
    signal_time: time
    exit_time: time
    required_predictor_bars: int


@dataclass(frozen=True)
class GcStrategyConfig:
    direction_modes: tuple[str, ...]
    random_seed: int
    contracts_per_trade: int
    cross_contract_predictor: str


@dataclass(frozen=True)
class GcCostConfig:
    bid_ask_spread_ticks: float
    slippage_ticks_per_side: float
    commission_per_side_usd: float


@dataclass(frozen=True)
class GcResearchConfig:
    experiment: GcExperimentConfig
    provenance: GcProvenanceConfig
    instrument: GcInstrumentConfig
    session: GcSessionConfig
    strategy: GcStrategyConfig
    costs: GcCostConfig


def _clock(value: object, field: str) -> time:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be an explicit HH:MM time")
    return time.fromisoformat(value)


def _required_text(value: object, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be explicitly provided")
    result = value.strip()
    if result.upper().startswith("REQUIRED"):
        raise ValueError(f"{field} still contains a REQUIRED placeholder")
    return result


def _required_date(value: object, field: str) -> date:
    if isinstance(value, date) and not isinstance(value, datetime):
        return value
    text = _required_text(value, field)
    return date.fromisoformat(text)


def _required_float(value: object, field: str) -> float:
    if value is None or isinstance(value, bool):
        raise ValueError(f"{field} must be explicitly provided")
    try:
        result = float(value)
    except (TypeError, ValueError) as error:
        raise ValueError(f"{field} must be a number") from error
    if not isfinite(result) or result < 0:
        raise ValueError(f"{field} must be finite and non-negative")
    return result


def load_gc_config(path: str | Path) -> GcResearchConfig:
    with Path(path).open("r", encoding="utf-8") as handle:
        raw = yaml.safe_load(handle)
    if not isinstance(raw, dict):
        raise ValueError("GC config must contain a YAML mapping")

    experiment_raw = raw["experiment"]
    experiment = GcExperimentConfig(
        name=_required_text(experiment_raw.get("name"), "experiment.name"),
        start_date=_required_date(
            experiment_raw.get("start_date"), "experiment.start_date"
        ),
        end_date=_required_date(experiment_raw.get("end_date"), "experiment.end_date"),
    )

    provenance_raw = raw["provenance"]
    provenance = GcProvenanceConfig(
        provider=_required_text(provenance_raw.get("provider"), "provenance.provider"),
        dataset=_required_text(provenance_raw.get("dataset"), "provenance.dataset"),
        price_basis=_required_text(
            provenance_raw.get("price_basis"), "provenance.price_basis"
        ),
        source_timezone=_required_text(
            provenance_raw.get("source_timezone"), "provenance.source_timezone"
        ),
    )

    instrument_raw = raw["instrument"]
    instrument = GcInstrumentConfig(
        symbol=_required_text(instrument_raw.get("symbol"), "instrument.symbol"),
        contract_size_oz=_required_float(
            instrument_raw.get("contract_size_oz"), "instrument.contract_size_oz"
        ),
        tick_size=_required_float(
            instrument_raw.get("tick_size"), "instrument.tick_size"
        ),
    )

    session_raw = raw["session"]
    session = GcSessionConfig(
        timezone=_required_text(session_raw.get("timezone"), "session.timezone"),
        session_open=_clock(session_raw.get("session_open"), "session.session_open"),
        signal_time=_clock(session_raw.get("signal_time"), "session.signal_time"),
        exit_time=_clock(session_raw.get("exit_time"), "session.exit_time"),
        required_predictor_bars=int(session_raw.get("required_predictor_bars", 0)),
    )

    strategy_raw = raw["strategy"]
    modes_raw = strategy_raw.get("direction_modes")
    if not isinstance(modes_raw, list):
        raise ValueError("strategy.direction_modes must be a YAML list")
    strategy = GcStrategyConfig(
        direction_modes=tuple(str(item) for item in modes_raw),
        random_seed=int(strategy_raw.get("random_seed")),
        contracts_per_trade=int(strategy_raw.get("contracts_per_trade")),
        cross_contract_predictor=_required_text(
            strategy_raw.get("cross_contract_predictor"),
            "strategy.cross_contract_predictor",
        ),
    )

    costs_raw = raw["costs"]
    costs = GcCostConfig(
        bid_ask_spread_ticks=_required_float(
            costs_raw.get("bid_ask_spread_ticks"), "costs.bid_ask_spread_ticks"
        ),
        slippage_ticks_per_side=_required_float(
            costs_raw.get("slippage_ticks_per_side"),
            "costs.slippage_ticks_per_side",
        ),
        commission_per_side_usd=_required_float(
            costs_raw.get("commission_per_side_usd"),
            "costs.commission_per_side_usd",
        ),
    )

    config = GcResearchConfig(
        experiment, provenance, instrument, session, strategy, costs
    )
    validate_gc_config(config)
    return config


def validate_gc_config(config: GcResearchConfig) -> None:
    ZoneInfo(config.session.timezone)
    if config.provenance.price_basis != "trade":
        raise ValueError("EXP-010 requires provenance.price_basis: trade")
    if config.provenance.source_timezone.upper() != "UTC":
        raise ValueError("Normalize source timestamps to UTC before EXP-010")
    if config.experiment.start_date > config.experiment.end_date:
        raise ValueError("experiment.start_date must not follow end_date")
    if config.instrument.symbol != "GC":
        raise ValueError("EXP-010 is locked to the standard COMEX GC contract")
    if config.instrument.contract_size_oz != 100:
        raise ValueError("Standard GC contract_size_oz must be 100")
    if config.instrument.tick_size != 0.10:
        raise ValueError("Standard GC tick_size must be 0.10 USD/oz")
    if not (
        config.session.session_open
        < config.session.signal_time
        < config.session.exit_time
    ):
        raise ValueError("Expected session_open < signal_time < exit_time")
    if (
        config.session.session_open != time(8, 20)
        or config.session.signal_time != time(13, 0)
        or config.session.exit_time != time(13, 30)
    ):
        raise ValueError("EXP-010 locks session times to 08:20, 13:00, and 13:30")

    anchor = date(2000, 1, 1)
    predictor_minutes = int(
        (
            datetime.combine(anchor, config.session.signal_time)
            - datetime.combine(anchor, config.session.session_open)
        ).total_seconds()
        // 60
    )
    if config.session.required_predictor_bars != predictor_minutes:
        raise ValueError(
            "required_predictor_bars must equal every minute from session_open "
            "through the minute before signal_time"
        )
    if config.session.exit_time != (
        datetime.combine(anchor, config.session.signal_time) + timedelta(minutes=30)
    ).time():
        raise ValueError("EXP-010 locks the holding period to exactly 30 minutes")

    modes = config.strategy.direction_modes
    if len(modes) != len(set(modes)):
        raise ValueError("strategy.direction_modes contains duplicates")
    if set(modes) != set(ALLOWED_MODES):
        raise ValueError(
            "EXP-010 requires exactly momentum, always_long, always_short, "
            "and deterministic_random"
        )
    if config.strategy.contracts_per_trade != 1:
        raise ValueError("EXP-010 is locked to one GC contract per mode and session")
    if config.strategy.random_seed != 20261102:
        raise ValueError("EXP-010 locks deterministic_random seed to 20261102")
    if config.strategy.cross_contract_predictor != "skip":
        raise ValueError("Cross-contract predictor returns must use policy: skip")

