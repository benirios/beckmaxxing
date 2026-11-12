from __future__ import annotations

from collections import Counter
from math import sqrt

import pandas as pd


def _max_streak(values: pd.Series, wanted: bool) -> int:
    best = current = 0
    for value in values:
        if bool(value) is wanted:
            current += 1
            best = max(best, current)
        else:
            current = 0
    return best


def summarize_gc_trades(trades: pd.DataFrame) -> dict[str, object]:
    if trades.empty:
        return {
            "trades": 0,
            "win_rate": None,
            "gross_pnl_usd": 0.0,
            "net_pnl_usd": 0.0,
            "profit_factor": None,
            "max_drawdown_usd": 0.0,
        }

    net = trades["net_pnl_usd"].astype(float)
    wins = net > 0
    positive = net[net > 0]
    negative = net[net < 0]
    equity = net.cumsum()
    drawdown = equity.cummax().clip(lower=0) - equity
    daily = trades.groupby("session_date", sort=True)["net_pnl_usd"].sum().astype(float)
    daily_std = float(daily.std(ddof=1)) if len(daily) > 1 else 0.0
    sharpe = sqrt(252) * float(daily.mean()) / daily_std if daily_std > 0 else None
    return {
        "trades": int(len(trades)),
        "long_trades": int((trades["direction"] == "long").sum()),
        "short_trades": int((trades["direction"] == "short").sum()),
        "wins": int(wins.sum()),
        "losses": int((net < 0).sum()),
        "flat": int((net == 0).sum()),
        "win_rate": float(wins.mean()),
        "gross_pnl_usd": float(trades["gross_pnl_usd"].sum()),
        "spread_cost_usd": float(trades["spread_cost_usd"].sum()),
        "slippage_cost_usd": float(trades["slippage_cost_usd"].sum()),
        "commission_usd": float(trades["commission_usd"].sum()),
        "net_pnl_usd": float(net.sum()),
        "average_net_trade_usd": float(net.mean()),
        "profit_factor": (
            float(positive.sum() / abs(negative.sum())) if not negative.empty else None
        ),
        "daily_sharpe": sharpe,
        "max_drawdown_usd": float(drawdown.max()),
        "max_consecutive_wins": _max_streak(wins, True),
        "max_consecutive_losses": _max_streak(wins, False),
    }


def gc_full_report(trades: pd.DataFrame, skipped_sessions: pd.DataFrame) -> dict:
    report: dict[str, object] = {
        "by_mode": {},
        "by_mode_year": {},
        "skipped_sessions": int(len(skipped_sessions)),
        "skip_reasons": (
            dict(Counter(skipped_sessions["reason"]))
            if not skipped_sessions.empty
            else {}
        ),
    }
    if trades.empty:
        return report
    for mode, group in trades.groupby("mode", sort=True):
        report["by_mode"][str(mode)] = summarize_gc_trades(group)
        years: dict[str, object] = {}
        for year, year_group in group.groupby(
            pd.to_datetime(group["session_date"]).dt.year, sort=True
        ):
            years[str(year)] = summarize_gc_trades(year_group)
        report["by_mode_year"][str(mode)] = years
    return report

