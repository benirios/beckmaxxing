---
tags: [ctrader, data, diagnostic]
status: completed_diagnostic
---

# cTrader tick-history availability probe — 2026-11-11

The broker login now verifies successfully through the official CLI. The frozen
EXP-017 Tokyo V2 full-period tick attempt requested 2020-05-06–2026-10-24 and
exited with native code 86: `No historical data for the specified period`.
This is a data-availability failure, not a trading loss or rejection of T002.
See [[Lab run 20261111T171954_911469 - T002]].

Diagnostic plan, frozen before its launch: replay the unchanged Tokyo V2 Fade
source on 2026-10-19–2026-10-24, FTMO XAUUSD server ticks, M1 chart, one unit,
live disabled, $10,000 initial balance, fixed spread 1 pip, commission 30.
The native report must confirm the actual commission unit and spread. Request
key: `exp017-v2-history-probe-20261019-1024`.

This probe asks only whether the broker supplies recent historical ticks to
the CLI. Its five-session window cannot complete the 60-session signal warmup.
It is not a profitability experiment and cannot replace the pending six-year
EXP-017 run. Record native availability, actual dates, any report/log and failure
codes. Do not infer that all earlier history is missing from a single failure.

## Observed result

Native report and console confirm `tickDataFromServer`, the unchanged Tokyo V2
Fade build, the requested 2026-10-19–2026-10-24 interval, USD 10,000 initial balance
and commission USD 30 per million USD volume. Five Tokyo sessions logged warmup
counts 0–4; there were zero trades, as expected before the required 60-session
warmup. No profitability conclusion follows. Native spread metadata is null,
so the requested fixed 1-pip scenario has not been established by the report.

The cBot stopped and wrote the complete native report, but the legacy CLI
transport stayed alive. Its uniquely identified owned process was interrupted
only after those completion signals were verified. Native artifacts were not
edited. The subsequent audit-parser repair re-read the same report without
replaying prices, preserving the previous audit as a revision.

Artifacts and audit: [[Lab run 20261111T172214_eb8c93 - T002]] and
`outputs/backtest-lab/20261111T172214_eb8c93/`. Native report SHA-256:
`0d2362bf89e901df86c980e791ca59b1608f29617643945d4c4ec8e396de67fa`.

Conclusion: recent broker ticks and the existing .NET 6 package work through
the CLI. The requested six-year history remains unavailable in the original
attempt. Determine and audit available history before planning a separate
eligible strategy run; the original full-period experiment remains incomplete.
