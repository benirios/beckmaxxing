from __future__ import annotations

from dataclasses import dataclass
from datetime import date, time
from pathlib import Path
from zoneinfo import ZoneInfo

import yaml


@dataclass(frozen=True)
class ExperimentConfig:
    name: str
    instrument: str
    price_basis: str


@dataclass(frozen=True)
class SessionConfig:
    timezone: str
    opening_time: time
    opening_range_minutes: int
    entry_deadline: time
    exit_time: time
    max_missing_minutes: int


@dataclass(frozen=True)
class StrategyConfig:
    confirmation: str
    entry: str
    stop: str
    target_r: float
    max_trades_per_session: int
    same_bar_policy: str


@dataclass(frozen=True)
class CostConfig:
    round_trip_spread: float
    slippage_per_side: float
    round_trip_commission: float


@dataclass(frozen=True)
class ValidationConfig:
    train_end: date
    validation_end: date


@dataclass(frozen=True)
class ResearchConfig:
    experiment: ExperimentConfig
    session: SessionConfig
    strategy: StrategyConfig
    costs: CostConfig
    validation: ValidationConfig


def _clock(value: str) -> time:
    return time.fromisoformat(value)


def load_config(path: str | Path) -> ResearchConfig:
    with Path(path).open("r", encoding="utf-8") as handle:
        raw = yaml.safe_load(handle)

    experiment = ExperimentConfig(**raw["experiment"])
    session_raw = dict(raw["session"])
    session_raw["opening_time"] = _clock(session_raw["opening_time"])
    session_raw["entry_deadline"] = _clock(session_raw["entry_deadline"])
    session_raw["exit_time"] = _clock(session_raw["exit_time"])
    session = SessionConfig(**session_raw)
    strategy = StrategyConfig(**raw["strategy"])
    costs = CostConfig(**raw["costs"])
    validation = ValidationConfig(
        train_end=date.fromisoformat(raw["validation"]["train_end"]),
        validation_end=date.fromisoformat(raw["validation"]["validation_end"]),
    )
    config = ResearchConfig(experiment, session, strategy, costs, validation)
    _validate(config)
    return config


def _validate(config: ResearchConfig) -> None:
    ZoneInfo(config.session.timezone)
    if config.experiment.price_basis != "mid":
        raise ValueError("The baseline adapter requires price_basis: mid")
    if config.session.opening_range_minutes <= 0:
        raise ValueError("opening_range_minutes must be positive")
    if not (
        config.session.opening_time
        < config.session.entry_deadline
        < config.session.exit_time
    ):
        raise ValueError("Expected opening_time < entry_deadline < exit_time")
    if config.session.max_missing_minutes != 0:
        raise ValueError("The baseline requires max_missing_minutes: 0")
    if config.strategy.confirmation != "close":
        raise ValueError("Only close confirmation is implemented")
    if config.strategy.entry != "next_bar_open":
        raise ValueError("Only next_bar_open entry is implemented")
    if config.strategy.stop != "opposite_range_boundary":
        raise ValueError("Only opposite_range_boundary stop is implemented")
    if config.strategy.target_r <= 0:
        raise ValueError("target_r must be positive")
    if config.strategy.max_trades_per_session != 1:
        raise ValueError("The baseline implements one trade per session")
    if config.strategy.same_bar_policy != "stop_first":
        raise ValueError("Only the conservative stop_first policy is implemented")
    if min(
        config.costs.round_trip_spread,
        config.costs.slippage_per_side,
        config.costs.round_trip_commission,
    ) < 0:
        raise ValueError("Costs cannot be negative")
    if config.validation.train_end >= config.validation.validation_end:
        raise ValueError("train_end must precede validation_end")

