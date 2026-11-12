---
tags: [xauusd, orb, ctrader, cbot, h2, stop-entry]
status: completed-rejected
experiment: EXP-007
---

# cTrader cBot — NY15 Immediate Stop Entry

EXP-007 completes the three-entry H2 comparison by testing an immediate
boundary breakout against EXP-001's M1-close confirmation and EXP-006's
first-touch retest.

## Locked trigger and OCO behavior

1. Build the 09:30–09:45 New York opening range from exactly 15 complete M1
   bars.
2. From 09:45 through 11:59:59, monitor the executable quote stream.
3. The first bid strictly above the range high triggers a buy. The first bid
   strictly below the range low triggers a sell.
4. This is a synthetic OCO: the first boundary trigger permanently consumes
   the session before sizing or order submission. The opposite direction
   cannot trigger later, even if the first order is rejected.
5. Enter immediately by market order at the available ask for a buy or bid for
   a sell. This keeps the range signal on the bid-based M1 price series while
   charging the executable spread to long entries.
6. Gap handling: if the first available post-range quote is already outside a
   boundary, enter at that available executable quote. Position size and 1R are
   calculated from the actual expected entry geometry, not the boundary.
7. No trigger at or after 12:00 New York; an untriggered session expires.

The strategy uses `OnTick` for the boundary event rather than placing two
broker-side pending orders. cTrader's Robot API version used here cannot attach
protection to a stop order before it fills. The quote-triggered OCO permits the
market order to carry stop and target protection atomically from creation.

## Unchanged comparison rules

- Stop at the opposite NY15 opening-range boundary.
- Target at 1R from expected executable market entry.
- One consumed trigger per session.
- Close any open position at 16:00 New York.
- Default position risk is 0.5% of equity.
- Live trading is disabled by default.
- Missing or wrong-side attached protection triggers an immediate safety close
  and an explicit log entry.

## Artifacts

- Source: `ctrader/XauUsdNy15ImmediateStopOrb.cs`.
- Project: `ctrader/XauUsdNy15ImmediateStopOrb.csproj`.
- Build marker: `EXP007_NY15_IMMEDIATE_STOP_1R_V1_20261101`.
- Source SHA-256: `baabcab93c6193efa1a96a924514719b4052b33c3ca71334c3df6ccef2d2174b`.
- Release `.algo` SHA-256: `81a97ebaa040afd4952f6648fc6e0aa3bd68457232de3fee318069f1f60cc514`.

Release build verified on 2026-11-01 against `cTrader.Automate` 1.0.21 with
zero warnings and zero compiler errors. The generated cBot is at
`ctrader/bin/Ny15ImmediateStopOrb/Release/net6.0/XAUUSD.algo`.
This build was published to
`/Users/beni/cAlgo/Sources/Robots/XAUUSD ORB NY15 Immediate Stop.algo` for cTrader.
cTrader may rewrite the imported package in its watched source directory, so
the release hash above refers to the repository build artifact.

## Evaluation

Primary success requires positive net expectancy and profit factor above 1.0,
plus improvement in conditional trade quality over EXP-001. Secondary metrics
are drawdown, trade count, yearly stability, long/short results, and gross-
versus-net performance.

M1 server-bar backtesting may synthesize the quote path within each minute.
Therefore, this run is a comparative bar-model benchmark, not a tick-level
execution audit. The entire test period is already exposed and cannot be
described as untouched out-of-sample validation.

## Result received 2026-11-01

The completed run returned -75.83% ROI, 0.69 profit factor, -$4.59 average
trade, and 76.56% maximum balance drawdown across 1,652 trades. Trigger counts
reconciled exactly, every order had correctly sided protection, and no order or
protection failures occurred. Gross and net results were negative in every
calendar-year slice. Reject EXP-007; see
[[../outputs/EXP-007-ny15-immediate-stop-2026-11-01/RUN-NOTE|archived run note]].

