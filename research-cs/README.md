# Intraday thesis scan: C# research runners

This project implements Candidates A, B, and C from `01 - Research/Intraday Thesis Scan - 2026-11-03.md`. It operates on explicit, audited inputs. No quote/event inputs for A, B, or C are available in this repository. EXP-014 London RSI ORB already has a cTrader cBot in `ctrader/XauUsdLondonRsiOrb.cs` and remains the first manual broker-feed run.

It also implements the subsequently preregistered EXP-015 Asia rebound screen in `Exp015AsiaRebound.cs`.

The later EXP-016 overnight drift screen is implemented in `Exp016OvernightDrift.cs`.

Build: `dotnet build research-cs/ResearchCandidates.csproj`

All quote CSV timestamps must include `Z` or an explicit UTC offset, be strictly ascending, and have `timestamp_utc,bid,ask`. GC quotes additionally require `contract` (explicit contract ID). Event CSV requires `timestamp_utc,kind,status,overlap`: `kind` is `CPI` or `EMPLOYMENT`, `status` is `actual` for on-time confirmed releases or another value for delayed/off-schedule releases, and `overlap` is `true` if another major release overlaps. Supply only actual release history, including ineligible rows for an auditable skip count. Prices are USD per ounce. Inputs must be synchronized and include the broker's executable bid/ask, rather than M1 OHLC.

## A: post-release continuation

`dotnet run --project research-cs -- a spot.csv events.csv 30 0.05 a-results.csv`

The numerical arguments are commission in USD per million notional and adverse slippage in USD per ounce on **each** fill. The first 60 complete eligible events are warmup for the thresholded strategy. `move` and `prior60_median` are fractional midpoint returns. The output records direction, stop/time exit, net P/L for one ounce, and opposite/fixed-seed direction controls on the same entry/exit quotes. `unfiltered_net_per_ounce` records the separately counted unfiltered event-direction diagnostic, including warmup events. The companion `*-summary.csv` reports count, net/mean P/L, and profit factor by release type. The output is research only; check the venue's current news rules before any use.

## B: GC-to-spot lead-lag diagnostic

`dotnet run --project research-cs -- b spot.csv gc.csv 25 30 0.05 0.30 b-results.csv`

Arguments after the files: measured maximum cross-feed clock error in milliseconds (must be <=100), commission per million, adverse per-side slippage, and estimated all-in round-trip cost per ounce. The prior 20 UTC dates must have pre-audited complete trading-session coverage; the runner shifts both GC five-second-change percentile and same-minute spot spread median by whole dates. It rejects stale quotes older than one second and contract changes within a signal, and reports matched sign-randomized, five-second delayed quote, and 250/1000 ms latency scenarios with a companion `*-summary.csv`. The 100 ms limit is an implementation integrity cutoff, not evidence of tradability. Contract roll/halts must be excluded in the supplied, audited files. FTMO prohibits exploiting delayed/external price feeds; this mode is for a different venue's research only.

## C: calendar-only exclusion

`dotnet run --project research-cs -- c frozen-trades.csv events.csv c-results`

The frozen trade ledger requires `trade_id,strategy,entry_utc,net_profit`, with the original strategy costs already included in `net_profit`. Outputs `c-results-trades.csv` and `c-results-summary.csv` show skipped entries from 08:25:00 through 08:45:59.999 Eastern on confirmed release dates and compare each frozen baseline with the exclusion on all dates and event dates. This is a trade-level diagnostic: it cannot simulate changed equity sizing or overlapping positions after skipped trades. A promoted result still needs a full strategy rerun and positive net expectancy after costs.

## EXP-015: Asia rebound after a negative U.S. session

`dotnet run --project research-cs -- exp015 data/raw exp015-results`

The runner accepts the directory of `evtl_xauusd_m5_YYYY.json.gz` yearly files directly. It also accepts an M5 CSV with `timestamp_utc,bid_open,bid_high,bid_low,bid_close,ask_open,ask_high,ask_low,ask_close`, sorted by UTC bar-open time. Use the specified EV Trading Labs/Dukascopy-attributed feed, with provenance and hashes recorded separately. The runner requires all 92 U.S. bars, all 72 Asian bars, and the next-day 02:00 exit bar. It skips missing or daylight-saving-transition windows rather than inventing bars. Every complete Asian window enters the previous-20-window bid-range history, including dates with missing U.S. bars. It applies executable bid/ask, $30/million each side, $0.02 adverse slippage each side, stop-first M5 ordering, and the unconditional-long and signal-date-short controls. The default `end_year` is **2023**; an explicit final argument of `2025` is required to inspect the reserved confirmation split. Do not use that option after the rejected development screen. Outputs `exp015-results-trades.csv` and `exp015-results-summary.csv` include the selected date split and each included year.

Independent check on 2026-11-03: the C# runner read the six source files for 2018–2023 directly; their SHA-256 hashes match `outputs/EXP-015-asia-rebound-screen/RUN-NOTE.md`. It produced 1,172 eligible dates and 588 conditioned-long trades. Every eligible date, signal, stop distance, and unconditional-long net P/L matched the separately published Python ledger within its four-decimal rounding. C# conditioned-long net total was −$364.789754/oz with PF 0.715176, agreeing with the published −$364.7898/oz and PF 0.7152. Direct gzip and CSV-conversion results were byte-for-byte identical. The 2024–2025 split was not run.

## EXP-016: overnight gold drift after reopening

`dotnet run --project research-cs -- exp016 data/raw exp016-results`

The runner accepts the same EV Trading Labs yearly `evtl_xauusd_m5_YYYY.json.gz` files as EXP-015, or a sorted M5 CSV with the columns listed above. It requires all 171 bars from 18:05 through next-day 08:15 New York and the 08:20 exit bar, on Monday–Thursday entry dates. The primary buys one ounce at the 18:05 ask open with a 1% stop; a same-date short with a symmetric 1% stop is the control. Both pay observed bid/ask, $30/million commission per side, and $0.02 adverse slippage per side. The default `end_year` is **2023**. The reserved 2024–2025 period requires an explicit `2025` argument and should not be run after the negative development result.

The 2018–2023 C# run produced 1,235 long trades, −$289.501564/oz total, −$0.234414/oz mean, and PF 0.929553. All 1,235 dates, entry/stop/exit prices, exit reasons, and long/short net values match the separately published Python ledger within its rounding. The result fails the preregistered gates; the 2024–2025 split was not inspected. See `outputs/EXP-016-overnight-gold-screen/CSHARP-REPLICATION.md`.

The summary reports conditioned-long mean per trade and mean per eligible date. The latter counts no-trade dates as zero P/L, so it can be compared with unconditional long over the same date universe. On matched signal dates the two long variants have identical fills; that row is retained as a check, not the promotion comparison.

All 2020–2026 dates previously inspected by the project are exposed development data. Record these runs and their inputs in the Experiment Register before interpreting results; reserve later untouched dates for validation.
