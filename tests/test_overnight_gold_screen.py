from datetime import date, timedelta

from xau_orb.overnight_gold_screen import run, trade
from xau_orb.asia_rebound_screen import timestamp


def bar(ts, bid=100.0, ask=100.2, low=None, high=None):
    return {
        "ts": ts, "o": bid, "h": bid if high is None else high,
        "l": bid if low is None else low, "c": bid,
        "ao": ask, "ah": ask if high is None else high + (ask - bid),
        "al": ask if low is None else low + (ask - bid), "ac": ask,
    }


def test_long_stop_uses_bid_low_and_gap_open():
    ts = timestamp(date(2021, 1, 4), 18, 5)
    window = [bar(ts), bar(ts + 300, bid=98.0, ask=98.2, low=97.8)]
    got = trade(window, bar(ts + 600), "long")
    assert got is not None
    assert got["reason"] == "stop"
    assert got["exit"] == 98.0  # worse than the 99.198 protective level
    assert got["net"] < got["gross_after_spread"]


def test_short_uses_ask_and_stop_first():
    ts = timestamp(date(2021, 1, 4), 18, 5)
    window = [bar(ts), bar(ts + 300, bid=100.9, ask=101.1, high=101.0)]
    got = trade(window, bar(ts + 600, bid=99, ask=99.2), "short")
    assert got is not None
    assert got["reason"] == "stop"
    assert got["exit"] == 101.1


def test_window_requires_all_bars_and_dst_shift():
    monday = date(2018, 3, 12)  # first US business day after DST starts
    t0 = timestamp(monday, 18, 5)
    assert t0 == timestamp(monday - timedelta(days=7), 18, 5) + 7 * 86400 - 3600
    bars = {t0 + i * 300: bar(t0 + i * 300) for i in range(171)}
    exit_ts = timestamp(monday + timedelta(days=1), 8, 20)
    bars[exit_ts] = bar(exit_ts, 101.0, 101.2)
    ledger, _ = run(bars, 2023)
    assert len(ledger) == 1
    assert ledger[0]["long_reason"] == "time"
    assert ledger[0]["long_net"] > 0
    del bars[t0 + 5 * 300]
    ledger, _ = run(bars, 2023)
    assert not ledger
