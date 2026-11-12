---
tags: [xauusd, ctrader, cbot, control, intraday-momentum, comex-close]
status: completed
experiment: EXP-009
---

# cTrader cBot — GC Close Controls

EXP-009 is a diagnostic benchmark for EXP-008. It tests whether direction
selection added information beyond merely holding XAUUSD from 13:00 to 13:30
New York. It is not a new trading hypothesis and must not be optimized.

## Locked modes

Run the same cBot four times, changing only `Direction Mode`:

1. `Exp008Momentum` — same sign rule as EXP-008.
2. `AlwaysLong` — buy every eligible session.
3. `AlwaysShort` — sell every eligible session.
4. `DeterministicRandom` — direction from the session date and fixed seed
   `20261102`; do not search for a better seed.

Every mode requires the exact 13:00 entry, all 280 M1 predictor bars, a valid
prior session 13:30 close, and a non-zero predictor return. This deliberately
matches EXP-008's eligible-date rules even when a control does not need the
predictor economically.

## Locked execution

- Enter at 13:00 and close at 13:30 New York.
- Use 1.0× current-equity notional exposure.
- No stop loss or take profit.
- Run all modes on identical dates, XAUUSD feed, data type, spread, commission,
  starting balance, and account settings.
- Prefer bid/ask tick data. If only M1 server bars are available, record that
  limitation and do not describe the run as a tick-execution audit.
- Run one mode per backtest. Do not run multiple instances simultaneously.

## Artifacts

- Source: `ctrader/XauUsdGcCloseControls.cs`.
- Project: `ctrader/XauUsdGcCloseControls.csproj`.
- Robot class: `XauUsdGcCloseControlsV1`.
- Build marker: `EXP009_GC_CLOSE_CONTROLS_V1_20261102`.
- Default mode: `AlwaysLong`.
- Fixed random seed: `20261102`.
- Source SHA-256: `22abb16b97dd5e1dd03f0ac8490b33cb4464961ebba19e8b0f84eb1c94a58a34`.
- Repository release `.algo` SHA-256: `f600ce9760f135bb1284acc6d7864c09a40137ef6133d1cff726b0f2d83a0b74`.
- cTrader-published `.algo` SHA-256 after its source-project rebuild:
  `44cc90a90e93ef3a40599f7cbb810089d91d03a50a404b0b89e837cfd6fcc262`.

The repository and uniquely named cTrader source-project builds completed with
zero warnings and zero errors. cTrader detected the source project and produced
`/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Close Controls V1.algo`; the editable
project is under
`/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Close Controls V1/`. Select the
`XauUsdGcCloseControlsV1` robot in cTrader.

The log prefix is `GCCCTRL_`. Every start, signal, entry, close request, and
closed position records the selected mode. Random-mode signals also record the
stable random key, allowing exact reproduction.

## Interpretation

Compare gross and net P/L, profit factor, average trade, yearly stability, and
long/short counts across the four modes. The primary diagnostic is incremental
performance of `Exp008Momentum` versus the three controls on matched dates.
None of these controls becomes a strategy candidate merely because it is best
in this already-exposed sample.

## Final result

All four valid modes received 2026-11-02 use the same 1,626 matched dates and
predictor records:

- `AlwaysLong`: -55.54% ROI, PF 0.29, -$4,486.66 gross; see
  [[../outputs/EXP-009-always-long-2026-11-02/RUN-NOTE|always-long run note]].
- `AlwaysShort`: -51.17% ROI, PF 0.32, -$4,117.02 gross; see
  [[../outputs/EXP-009-always-short-2026-11-02/RUN-NOTE|always-short run note]].
- `DeterministicRandom`: -50.48% ROI, PF 0.33, -$3,997.52 reported
  gross; see
  [[../outputs/EXP-009-deterministic-random-2026-11-02/RUN-NOTE|random run note]].
- `Exp008Momentum`: -55.72% ROI, PF 0.27, -$4,577.07 reported gross;
  its economic ledger and equity curve exactly reproduce EXP-008 V2. See
  [[../outputs/EXP-009-exp008-momentum-2026-11-02/RUN-NOTE|momentum reproduction]].

Every annual slice in every mode is negative after the fixed spread and before
commission/swaps. EXP-008 momentum is worse than all three controls. A
mechanical zero-spread diagnostic remains negative after actual commissions and
swaps for every mode; momentum is approximately -$1,438.88.

EXP-009 is complete. Direction selection added no detectable economic value to
the 13:00–13:30 XAUUSD window. Do not tune these modes or select a different
random seed using the exposed dates.
