---
tags: [xauusd, ctrader, cbot, h7, daytime-return, exp-011]
status: completed-rejected
experiment: EXP-011
---

# cTrader cBot — GC Daytime Short

EXP-011 tests whether the historically negative COMEX Gold daytime return
transfers to executable spot XAUUSD. It is a new full-session anomaly test, not
a modification of the rejected EXP-008/009 final-half-hour momentum family.

## Locked protocol

1. Attach the robot to an XAUUSD M1 chart.
2. At exactly 08:20 New York, enter one market sell in `ShortDay` mode.
3. Size at 1.0x current-equity notional by default.
4. Close at exactly 13:30 New York, with no stop or profit target.
5. Trade at most once per New York date.
6. Log the expected 310 closed M1 holding bars and exact/delayed exit state.
7. After archiving the primary short run, repeat once with `LongControl` and no
   other parameter changes.

The class uses `TimeZones.EasternStandardTime`, so cTrader applies New York DST.
Live execution is disabled unless explicitly enabled. A new date closes any
orphaned managed position before resetting session state.

## Artifacts

### Original dual-mode V1 — archived control build

- Source: `ctrader/XauUsdGcDaytimeShort.cs`
- Project: `ctrader/XauUsdGcDaytimeShort.csproj`
- Build marker: `EXP011_GC_DAYTIME_SHORT_V1_20261102`
- Robot class: `XauUsdGcDaytimeShortV1`
- Source SHA-256: `9a51534f67ec77e80fbdc6ec41bbbf3db661dd19343f3711e1b91ff7b8b37f56`
- Repository release `.algo` SHA-256: `8a73c3d53bbc046587f5ac9df2c17aa3eb3b764b25dd8b9710dd5f297d993f20`
- Published cTrader `.algo` SHA-256: `44fb306d6f64d59839940ca347f0e39c73f0537d46ca1cbc5824359c55713f0b`
- Build package: `ctrader/bin/GcDaytimeShort/Release/net6.0/XAUUSD.algo`
- Published cTrader project: `/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Daytime Short V1/`
- Published package: `/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Daytime Short V1.algo`

Both repository and published-project release compilations on 2026-11-02 use
`cTrader.Automate` 1.0.21 and completed with zero warnings and zero errors. The
two `.algo` hashes differ because their project/assembly names differ; their
robot source is identical.

### Primary short-only V2 — use for the next run

The control was accidentally launched first because cTrader retained
`DirectionMode=1` from the reused instance. V2 removes the direction parameter
and hardcodes `TradeType.Sell`; saved parameter sets therefore cannot convert
the primary run into another long control. Session, timing, exposure, and exit
logic are unchanged.

- Source: `ctrader/XauUsdGcDaytimeShortOnly.cs`
- Project: `ctrader/XauUsdGcDaytimeShortOnly.csproj`
- Build marker: `EXP011_GC_DAYTIME_SHORT_ONLY_V2_20261102`
- Robot class: `XauUsdGcDaytimeShortOnlyV2`
- Source SHA-256: `0bc58a70f4a09a7f4c548322557bbb28c835a0089c7e80d8bb192e2911144354`
- Repository release `.algo` SHA-256: `6fbff2f55d14870dbe554bbebcd4c6cf9bc6b1283949aa5121498b6104bdae80`
- Published cTrader `.algo` SHA-256: `8e1acfd286aa698026c048b0ee125ef5a3b61331ac40e693260b81e4195172d6`
- Build package: `ctrader/bin/GcDaytimeShortOnly/Release/net6.0/XAUUSD.algo`
- Published cTrader project:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Daytime Short Only V2/`
- Published package:
  `/Users/beni/cAlgo/Sources/Robots/XAUUSD GC Daytime Short Only V2.algo`

Both repository and published-project V2 release builds completed with zero
warnings and zero errors against `cTrader.Automate` 1.0.21. Their `.algo`
hashes differ because the project/assembly names differ; their robot source is
byte-for-byte identical.

## Backtest protocol

- Symbol/timeframe: XAUUSD M1.
- Period: 2020-05-06 through 2026-10-24, already exposed.
- Starting balance: $10,000.
- Data: FTMO Platform M1 server bars.
- Costs: fixed spread 1 and commission $30 per million, matching EXP-008/009.
- Robot: `XauUsdGcDaytimeShortOnlyV2` from the uniquely named V2 project.
- Direction: fixed `ShortDay`; V2 exposes no direction parameter.
- The `LongControl` arm is already complete and archived.
- Leave `Equity Notional (x) = 1.0` and every other default unchanged.

Primary acceptance requires positive short net expectancy and profit factor
above 1.0 after costs, plus economically better performance than the matched
long control. Do not tune the session, direction, exposure, weekdays, stops,
targets, or filters after inspecting the run.

## Long control received 2026-11-02

The first supplied run used `DirectionMode=1`, so it completed the
`LongControl` arm before the primary short arm. It lost 67.03% with PF 0.75
across 1,670 trades; gross P/L before commission and swaps was -$5,635.72, and
every represented year was negative. The log passed order integrity checks.

Eighteen holiday/data-gap positions closed after the intended 13:30 timestamp,
including five next-session cleanups. Excluding them leaves -$6,633.80 and PF
0.748; restricting to all 1,629 complete-path trades leaves -$6,652.57 and PF
0.744. The anomaly does not explain the loss, and the same build must now be
used for the primary `ShortDay` arm. See
[[../outputs/EXP-011-long-control-2026-11-02/RUN-NOTE|archived control run]].

## Invalid wrong-bot attempt received 2026-11-02

The next supplied bundle came from `xauusd intraday` on M5, not the published
short-only V2 robot. It used 09:30–10:00 momentum, EMA/ATR rules, stops, targets,
and both trade directions. It contains no `GCDAY_*` marker, so its -92.68% ROI
is excluded completely from EXP-011. See
[[../outputs/EXP-011-invalid-wrong-bot-2026-11-02/RUN-NOTE|invalid attempt]].

## Primary ShortDay result received 2026-11-02

The correct fixed-sell V2 run lost 44.04% with PF 0.82 across 1,670 trades and
54.52% maximum equity drawdown. It outperformed the matched long control but
failed both absolute profitability gates. Six of seven annual slices lost.

Raw prices moved down by an average 0.01547% during the session, but the effect
is weak and unstable. Before spread, the direction generated only $890.09 on
the equity-sized ledger; the fixed spread cost $4,268.00. Even removing the
spread entirely leaves -$136.39 after commission and swaps. Complete-path and
exact-exit sensitivities remain severely negative. Reject EXP-011 and do not
tune this family. See
[[../outputs/EXP-011-short-day-2026-11-02/RUN-NOTE|primary result]].

## Evidence

- Blose and Gondhalekar, “Overnight gold returns,” *Applied Economics Letters*
  (2014): https://doi.org/10.1080/13504851.2014.922661
- Blose, Gondhalekar, and Kort, “Overnight versus day returns in gold and gold
  related assets,” *Journal of Economics and Finance* (2018):
  https://doi.org/10.1007/s12197-017-9403-0

These sources motivate the direction and holding window; they do not validate
this broker's XAUUSD instrument or the configured execution costs.
