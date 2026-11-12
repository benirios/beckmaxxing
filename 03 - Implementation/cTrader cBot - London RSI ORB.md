---
tags: [xauusd, ctrader, cbot, london, orb, rsi, h5, exp-014]
status: completed-rejected
experiment: EXP-014
---

# cTrader cBot — London RSI ORB

EXP-014 tests the remaining preregistered H5 family using the public LD-B rule
from RIP Backtest as the hypothesis source. LD-B was the source study's best
full-sample net variant but failed its robustness gates; this experiment asks
whether the gross effect appears on the FTMO XAUUSD feed under executable
bid/ask fills and the project's stricter missing-bar rule.

## Locked rule

- Session zone: London local time with historical daylight-saving changes.
- Monday through Friday only.
- Opening range: exact M1 bars from 08:00 through 08:29.
- Completeness adaptation: require all 30 bars, rather than the source's
  allowance of 20, and skip an incomplete session without filling data.
- Minimum range: $0.60.
- Entry window: 08:30 through 11:59:59.
- Breakout: bid reaches one tick above the range high or one tick below the
  range low.
- RSI filter: Wilder RSI(14) on M30 closes. At the breakout, use the latest
  completed M30 value. Long requires RSI > 50; short requires RSI < 50.
- Each side is evaluated at its first breakout only. If RSI rejects one side,
  that side is disabled while the other remains available.
- Entry: immediate market order after an allowed trigger. A buy fills at ask;
  a sell fills at bid.
- Stop: one tick beyond the opposite range boundary, attached to the initial
  order. No profit target.
- Exit: stop or market close at 15:00 London, seven hours after the session
  open.
- Exposure: fixed 1 XAUUSD unit, matching the source's one-ounce reporting.
- One trade maximum per London date.
- No changes to session, RSI period or threshold, minimum range, entry window,
  stop, exit, or direction after inspecting the result.

The cTrader M1-server-bar engine may synthesize an intraminute quote path. The
test is therefore a broker-feed replication and comparative bar-model result,
not a tick-level execution audit. Unlike the source's single OHLC stream, the
cBot triggers on bid and pays the configured executable spread through ask/bid
market fills.

## Artifacts

- Source: `ctrader/XauUsdLondonRsiOrb.cs`
- Project: `ctrader/XauUsdLondonRsiOrb.csproj`
- Build marker: `EXP014_LONDON_RSI_ORB_V1_20261103`
- Robot class: `XauUsdLondonRsiOrbV1`
- Source SHA-256:
  `6d40534c75d81035b582ec2034b1a3a0b6f6a9177275ce7a2d97a573d3966478`
- Published package:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD London RSI ORB V1.algo`
- Published project:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD London RSI ORB V1/`
- Published `.algo` SHA-256:
  `3d5c838d61961c6bddfa67faaa18e5eb04a910d17ab016d88ff83b5c649fabd9`

The repository and uniquely named published cTrader projects build with zero
warnings and errors using `cTrader.Automate` 1.0.21. The published source is
byte-for-byte identical to the repository source.

## Backtest

Use XAUUSD M1, 2020-05-06–2026-10-24, $10,000 initial balance, fixed spread 1,
and $30/million commission. Keep the fixed one-unit exposure and all locked
rules unchanged. Primary gates are positive net expectancy and PF above 1.0
after costs. Secondary reporting must include gross versus net P/L, yearly and
directional stability, RSI rejections by side, range skips, protection
integrity, stop versus time exits, and a mechanical zero-spread sensitivity.

The completed run is recorded in
[[../outputs/EXP-014-london-rsi-orb-2026-11-03/RUN-NOTE|EXP-014 RUN-NOTE]].
It failed both primary gates with -$1,464.89 net P/L and PF 0.77.

## Hypothesis source

- RIP Backtest, “Gold Opening-Range Breakout,” protocol v1.0:
  https://ripbacktest.com/posts/gold-opening-range-breakout/
- Frozen specification:
  https://ripbacktest.com/posts/gold-opening-range-breakout/gold_orb_spec.md
