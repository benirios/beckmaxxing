"""XAUUSD opening-range breakout research tools."""

from .config import ResearchConfig, load_config
from .engine import BacktestResult, run_backtest

__all__ = ["BacktestResult", "ResearchConfig", "load_config", "run_backtest"]

