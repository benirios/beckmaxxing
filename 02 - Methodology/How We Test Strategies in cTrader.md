---
tags: [xauusd, methodology, ctrader, experiments]
status: active
---

# How We Test Strategies in cTrader

Each strategy starts as a written, numbered experiment. We record its hypothesis, exact entry and exit rules, position size, session timezone, costs, data period, and pass or fail criteria in the [[../04 - Planning/Experiment Register|Experiment Register]] before viewing its result. This keeps later changes from being mistaken for the original test.

## From strategy to backtest

1. Write the strategy as a C# source file (`.cs`) in `ctrader/`. The file implements a cTrader cBot with the experiment's fixed rules and diagnostic log messages. Its `.csproj` is used to compile the robot.
2. Build and publish the cBot as a uniquely named `.algo` package. Record the source or build identifier so the tested robot can be distinguished from older versions.
3. **Manually run the cBot in cTrader Backtesting.** Select the intended broker and XAUUSD symbol, data mode, date range, starting balance, spread, commission, and the experiment's locked parameters. Check the selected robot and settings before starting; a valid build alone does not prove that cTrader ran the right cBot.
4. Save the cTrader report, trade or event history, log, and parameter set in a dedicated `outputs/EXP-.../` directory. Keep failed and invalid attempts as separate records.
5. Audit the run against the written rules: confirm which robot ran, actual dates covered, trade timing and direction, position size, stops and exits, order or protection failures, and missing or skipped sessions. A run with an execution defect is marked invalid rather than treated as a strategy result.
6. Report net and gross performance, trade count, profit factor, drawdown, yearly and directional results, and relevant cost sensitivity. Add the result and artifact link to the Experiment Register and record the decision to reject, investigate, or advance the hypothesis.

## What the result means

Our cTrader runs use the specified broker feed and backtesting settings. Most recorded experiments use FTMO Platform XAUUSD M1 server bars with fixed spread 1 and $30 per million commission, but each experiment's own note is authoritative. An M1 bar backtest does not establish tick-level fill quality. We therefore identify the data mode and execution limits in every report.

The Python engine and automated tests check implementation behavior on controlled data. They do not replace the manual cTrader market backtest or turn synthetic fixtures into trading evidence. Likewise, a profitable backtest alone does not authorize live deployment: the [[Validation Protocol]] requires costs, robustness, and independent replication before promotion.

The 2022–2026 period was exposed by earlier cTrader experiments. Tests on those same dates are labeled as exposed broker-feed replications, not untouched final-test results. We do not adjust a strategy's settings after inspecting its outcome and then present the adjusted run as the original hypothesis.
