from datetime import date

from xau_orb.asia_rebound_screen import timestamp, trade


def test_new_york_session_anchor_tracks_dst():
    assert timestamp(date(2024, 1, 10), 20) == 1704934800  # 01:00 UTC next day
    assert timestamp(date(2024, 7, 10), 20) == 1720656000  # 00:00 UTC next day


def test_long_stop_uses_bid_and_gap_open_conservatively():
    first = {"ao": 2001.0, "o": 2000.5, "l": 1999.0}
    later = {"o": 1998.0, "l": 1997.0}
    result = trade([first, later], {"o": 2005.0}, 2.0, "long")
    assert result["exit_reason"] == "stop"
    assert result["exit"] == 1999.0  # first bar touches 1999 stop
    assert result["net"] < result["gross"]


def test_long_stop_gap_exits_at_worse_bid_open():
    first = {"ao": 2001.0, "o": 2000.5, "l": 2000.0}
    later = {"o": 1998.0, "l": 1997.0}
    result = trade([first, later], {"o": 2005.0}, 2.0, "long")
    assert result["exit_reason"] == "stop"
    assert result["exit"] == 1998.0
