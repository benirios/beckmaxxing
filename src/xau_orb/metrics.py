from __future__ import annotations

from math import sqrt

import numpy as np
import pandas as pd


def _max_streak(wins: pd.Series, value: bool) -> int:
    best = current = 0
    for item in wins:
        if bool(item) is value:
            current += 1
            best = max(best, current)
        else:
            current = 0
    return best


def summarize(trades: pd.DataFrame) -> dict:
    if trades.empty:
        return {
            "trades": 0,
            "win_rate": None,
            "gross_expectancy_r": None,
            "net_expectancy_r": None,
            "profit_factor": None,
            "daily_sharpe": None,
            "max_drawdown_r": None,
        }

    net = trades.net_r.astype(float)
    wins = net > 0
    profits = net[net > 0]
    losses = net[net < 0]
    equity = np.concatenate(([0.0], net.cumsum().to_numpy()))
    drawdowns = np.maximum.accumulate(equity) - equity
    daily = trades.groupby("session_date", sort=True).net_r.sum().astype(float)
    daily_std = float(daily.std(ddof=1)) if len(daily) > 1 else 0.0
    sharpe = sqrt(252) * float(daily.mean()) / daily_std if daily_std > 0 else None
    profit_factor = (
        float(profits.sum() / abs(losses.sum())) if not losses.empty else None
    )
    return {
        "trades": int(len(trades)),
        "win_rate": float(wins.mean()),
        "average_win_r": float(profits.mean()) if not profits.empty else None,
        "average_loss_r": float(losses.mean()) if not losses.empty else None,
        "gross_expectancy_r": float(trades.gross_r.mean()),
        "net_expectancy_r": float(net.mean()),
        "profit_factor": profit_factor,
        "daily_sharpe": sharpe,
        "max_drawdown_r": float(drawdowns.max()),
        "total_net_r": float(net.sum()),
        "max_consecutive_wins": _max_streak(wins, True),
        "max_consecutive_losses": _max_streak(wins, False),
    }


def full_report(trades: pd.DataFrame) -> dict:
    report = {"overall": summarize(trades), "by_direction": {}, "by_split": {}}
    if trades.empty:
        return report
    for direction, group in trades.groupby("direction", sort=True):
        report["by_direction"][str(direction)] = summarize(group)
    for split, group in trades.groupby("split", sort=True):
        report["by_split"][str(split)] = summarize(group)
    return report

