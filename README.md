<img width="1491" height="358" alt="beckmaxxing" src="https://github.com/user-attachments/assets/3e8f2b58-67bb-4109-ac2b-a46c327990df" />

# Beckmaxxing

A local backtesting platform for coding agents, built around historical cTrader tick data, frozen strategy rules, and reproducible research.

Give your agent a trading hypothesis. Use Beckmaxxing to inspect the rules, check the inputs, run the cBot through the native cTrader CLI, and review the evidence in one place.

```bash
git clone https://github.com/benirios/beckmaxxing.git
cd beckmaxxing
python3 -m backtest_lab.server --port 8765
```

Open **http://127.0.0.1:8765**.

The current research workspace focuses on **XAUUSD**. The interface is called **Gold Research Lab**.

---

# Why I Built This

Running backtests manually takes hours.

Find a paper. Translate the idea into rules. Build the cBot. Configure the test. Wait for it to finish. Open the report. Copy the numbers somewhere. Try to remember which version produced which result.

Then do it again.

I wanted the agent to handle that loop while keeping the research inspectable.

Beckmaxxing brings together:

- a catalog of strategy hypotheses and their source contracts
- native cTrader backtests using historical broker ticks
- a JSON interface that coding agents can use without a browser
- a dark dashboard for performance, risk, and strategy summaries
- source and package hashes tied to each attempt
- native reports, logs, and Obsidian research notes

The objective is less time operating the backtester and more time asking whether an idea survives the evidence.

---

# How It Works

```text
HYPOTHESIS → FROZEN RULES → PREFLIGHT → NATIVE BACKTEST → AUDIT → REVIEW
```

## 1. Inspect the Strategy

Browse the strategy catalog, read the rules, and check which inputs the strategy needs.

Some strategies can use broker data directly. Others require timestamped external features, event calendars, or frozen opportunities. Missing inputs stay visible and block execution.

## 2. Check the Run

Preflight verifies the source, matching native package, account connection, and required inputs before a new test can start.

An idempotency key identifies the request. Retrying the same request returns the original job instead of launching another test.

## 3. Run the Native Engine

The local Python server invokes the official cTrader CLI. One backtest runs at a time.

New platform runs request historical server ticks. Observer builds capture account snapshots while the replay is running, without changing the canonical strategy rules.

## 4. Inspect the Result

The dashboard reads the native report and shows the numbers behind the run. Reports, logs, parameter files, and identity hashes remain available for inspection.

A completed process is an execution result. A strategy still needs its data, fills, costs, protections, and validation history reviewed.

---

# The Dashboard

| View | What you get |
|---|---|
| **Strategies** | Searchable catalog, source contracts, input requirements, and run controls |
| **Runs & evidence** | Ranked reports, performance metrics, strategy summaries, equity observations, logs, and downloads |
| **What’s next?** | Proposed improvements to comparison, data quality, validation, analysis, and workflow |

Each saved report can show:

- net profit and return
- profit factor
- balance curve and sampled equity minimum/maximum range
- maximum equity drawdown, in percent and account currency
- winning, losing, and total closed trades
- win rate and average trades per elapsed calendar day
- average trade, largest win/loss, and consecutive win/loss streaks
- commissions, swaps, ending equity, and long/short breakdown
- a quick explanation of how the strategy works

Runs sort best to worst by **return ÷ maximum equity drawdown**. Diagnostics, invalid attempts, and runs with incomplete ranking metrics appear as unranked.

This is a comparison aid. Test periods, sample sizes, and data coverage can differ. Missing report fields display as `—`; they are not estimated.

---

# Getting Started

## Requirements

- Python 3.11 or newer
- the official [cTrader CLI](https://help.ctrader.com/ctrader-cli/setup/) for native execution
- a cTrader ID linked to the broker account you want to test
- a .NET SDK capable of building the included `net6.0` cBots, with NuGet access
- Node.js 18 or newer and npm if you want to run frontend checks

The current runner and launcher have been used on macOS. Native execution depends on the installed cTrader CLI, broker connectivity, and available history. The Python web server uses the standard library; there is no frontend build step.

## Start the Platform

From the repository root:

```bash
python3 -m backtest_lab.server --port 8765
```

On macOS, you can also open `Start Backtest Lab.command`.

The server binds to `127.0.0.1`. Keep it local; it is not designed for public hosting.

## Build a Strategy

For example, build the Tokyo pressure-fade cBot:

```bash
python3 -m backtest_lab.build XauUsdTokyoPressureReversal
```

The builder preserves the canonical source and records the source, generated observer source, and native package hashes. Compiled packages are generated locally rather than included in Git.

## Connect and Run

1. Open the dashboard and choose **Connect cTrader**.
2. Enter your cTrader ID, cTrader ID password, broker, and trading account number in the local connection dialog.
3. Verify the connection, then select a strategy and inspect its rules and test dates.
4. Run the test and review it under **Runs & evidence**.

Credentials live outside the repository under `~/.config/xauusd-backtest-lab/`. The API does not return your password.

A fresh clone has no local market data, native packages, saved reports, or downloaded reference PDFs/HTML. Source links and provenance metadata are retained. Presets that reference generated research inputs remain blocked until those exact inputs are available and verified.

---

# Agent Workflow

Coding agents use the same local runner through its JSON client:

```bash
python3 -m backtest_lab.agent capabilities
python3 -m backtest_lab.agent health
python3 -m backtest_lab.agent strategies --status ready
python3 -m backtest_lab.agent strategy T002
python3 -m backtest_lab.agent preflight T002
```

Once preflight returns `can_launch: true`, use its source hash to submit the frozen test:

```bash
python3 -m backtest_lab.agent backtest T002 \
  --idempotency-key tokyo-fade-ticks-001 \
  --source-sha256 SOURCE_SHA256_FROM_PREFLIGHT
```

Follow the returned job ID:

```bash
python3 -m backtest_lab.agent wait JOB_ID --timeout 30
python3 -m backtest_lab.agent logs JOB_ID --cursor 0 --limit 8192
python3 -m backtest_lab.agent result JOB_ID
```

Retry a lost launch response with the **same request and key**. A wait timeout does not stop the job.

The agent client reads its private session descriptor automatically. Do not paste credentials or session tokens into prompts or command arguments.

See the [agent protocol](backtest_lab/AGENTS.md) and [runner documentation](backtest_lab/README.md) for the full execution and recovery rules.

---

# Research First

The platform automates execution and evidence collection. It does not automatically establish an edge, promote a strategy, or place live orders.

`needs_review` means a native report is available for review. It does not mean the strategy passed validation.

Historical M1 reports remain labelled as M1 observations. Requested dates do not establish uninterrupted data coverage. Tests using synthetic fixtures verify software behavior, not market performance.

The current catalog also contains observation studies and risk controls. They should not be interpreted as independent trading signals just because they compile or finish running.

---

# What’s Next?

The same proposals are available in the dashboard’s **What’s next?** tab.

| Improvement | Why it matters |
|---|---|
| Matched-window run comparisons | Overlay normalized curves using comparable dates, costs, baselines, and controls |
| Visible coverage and cost audits | Show missing ticks, warmup, and requested versus observed execution assumptions |
| Trade-level analysis | Inspect fills, holding times, fees, drawdown duration, and return distributions |
| Frozen validation workflows | Separate discovery from walk-forward and held-out evaluation |
| Bounded experiment queues | Run a declared sequence of tests while preserving provenance and failed attempts |
| Research exports | Share trade CSVs and concise summaries with audit context and identity hashes |

These are future improvements, not claims about features already implemented.

---

# Development

Install the research and test dependencies:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -e '.[dev]'
```

Run the checks:

```bash
python -m pytest -q
npm ci
npm test
npm run check
```

The repository also includes deterministic Python research engines and C# research tools. See [research workflows](docs/research-workflows.md), [research design](docs/research-design.md), and the [C# research tools](research-cs/README.md).

## Repository Layout

```text
backtest_lab/          local server, JSON agent client, builder, and dashboard
ctrader/               cBot sources, project files, and strategy catalog
configs/               experiment definitions and frozen presets
src/xau_orb/           deterministic research engines and data tools
research/              research feature preparation and batch scripts
research-cs/           C# research tools
tests/                 software checks using explicit test fixtures
docs/                  setup, research design, and workflow documentation
01–04 research folders hypothesis evidence, methodology, implementation, planning
outputs/               generated run artifacts, kept local
data/                  raw and processed market inputs, kept local
```

Open the repository as an Obsidian vault to browse the [research hub](00%20-%20XAUUSD%20ORB%20Research%20Hub.md). Each new platform attempt writes its own run note under `04 - Planning/`.
