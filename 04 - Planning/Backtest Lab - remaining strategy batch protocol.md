---
tags: [ctrader, experiments, preregistration]
status: active
---

# Remaining strategy batch through Backtest Lab

User authorization: continue running remaining C# strategies, save their results
to Obsidian, and show prior, completed and current results in the local platform.

## Fixed rules and period selection

Retain original strategy logic, parameters, volume and exit rules. The original
2020-05-06–2026-10-24 development request remains incomplete after native exit 86.
Determine broker tick availability before selecting a separate available-history
replication window. Choose dates from availability only, never from profitability.
Use the longest verified common window ending 2026-10-24 that can be acquired
within the original exposed dates. Mark shorter runs as available-history
replications, not completion of the original six-year request or untouched tests.

Start with read-only DATA-001 metadata at 2026-10-19–2026-10-20:
`MarketData.GetTicks(SymbolName).GetServerFirstTime()`. Preserve raw returned time
and any unsupported-operation error. Cross-check it against the already verified
recent replay before treating it as global broker-history coverage. This utility
submits no orders and cannot count as a strategy result.

Initial eligible batch: T002/EXP-017 Fade; T006/EXP-018 with US500.cash reference;
T015/EXP-019 RoundHundreds followed by its frozen NonRoundPlacebo branch. Do not
run absent external-feature strategies with empty inputs. Show their exact input
requirements in the platform ledger. No parameter sweep or result-led date choice.

## Current account observations

Observer builds insert only an account/closed-position logger and an hourly timer
into generated copies. Canonical `.cs` files remain unchanged. Both canonical
and generated source hashes and package hashes are saved before each run.
Observer version LAB_LIVE_V1 prints UTC, actual account balance, equity, realized
balance change and the closed-position count. The dashboard displays the last
emitted snapshot with its timestamp; it never fabricates interim equity or P/L
when the native engine has not emitted data. These builds add no order methods.

Requested native context: XAUUSD M1, server ticks, USD 10,000 initial balance,
commission 30 per million USD volume and spread argument 1. The earlier tick
report had null spread metadata; record actual native semantics and retain a
review issue rather than claiming a verified fixed-spread scenario.

## Recording

After every attempt, retain native report, log, parameter file, build/source
identity, hashes and an Obsidian note. Regenerate `Backtest Lab Results.md` and
the platform's source ledger. Keep diagnostic, failed, no-trade and reviewed
market results distinct. A positive or terminal run alone never promotes a bot.
