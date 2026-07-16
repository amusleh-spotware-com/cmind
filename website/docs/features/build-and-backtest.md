---
description: "Build, run, backtest cTrader cBots (C and Python, both .NET) from in-browser Monaco IDE, run on official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **and** Python, both .NET) from in-browser Monaco
IDE, run on official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project with
  `dotnet build` **in throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`), so untrusted user MSBuild targets no reach host. NuGet restore cached
  across builds via shared volume. Web host need Docker socket access.
- C# + Python starter templates live in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route to
  remote node HTTP agent or local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream to browser over SignalR; backtest equity curves parsed from
  report + charted.

## Backtest market data is cached per account

The cTrader Console downloads historical tick/bar data into its `--data-dir`. That directory is a
**stable, persistent cache keyed on the trading account** (its account number) — bind-mounted from the
node's disk at its own container path (`/mnt/data`), a **separate, non-nested mount** from the
per-instance work dir. So every backtest on the same account **reuses** the already-downloaded data
instead of re-downloading it each run. (Earlier the
data dir lived under the per-instance work dir, whose id changes every run, which forced a fresh
download every backtest.) The ephemeral per-instance work dir still holds the algo, params, password
and report; the shared data cache is counted in a node's backtest-data usage and cleared by the
node-clean action.

## Instance detail page

Opening an instance (`/instance/{id}`) shows its live status, logs and — for a backtest — the equity
curve. The **browser tab title** reflects the specific instance (**cBot name · kind · symbol**, e.g.
`TrendBot · Backtest · EURUSD`) so a live-run tab and a backtest tab are distinguishable at a glance.
A run and a backtest of the same cBot are tracked as distinct **lineages** (a stable lineage id carried
across state transitions), so the page follows exactly one instance and never mixes a run's data with a
backtest's.

## Instance lifecycle controls

Each instance row (and its detail page) has state-correct controls. An **active** instance shows
**Stop**; a **terminal** one (Stopped / Completed / Failed) shows **Start (▶)** to re-launch it with
the same cBot, account, symbol, timeframe, parameter set and image (a run restarts as a run, a
backtest as a backtest). Clicking Stop shows a "Stopping…" notice and disables the icon until it
resolves, and a newly created run appears in the list immediately — no page reload.

Console logs are **persisted when an instance terminates** — for a run (on Stop) and for a
**backtest** (on completion) alike — so the last run's logs stay viewable on the detail page and
downloadable via the **Download logs** icon even after the container is gone.

## Run from the code editor

Clicking **Run** in the code editor opens a dialog instead of firing a blind, hard-coded run:

- **Trading account** (required) — the cTrader account the cBot connects to.
- **Parameter set** (optional) — pick an existing set, or leave it empty to run with the cBot's
  **default parameter values**. A **+** button next to the selector creates a new parameter set
  inline (see below) and selects it.
- **Symbol / Timeframe** default to `EURUSD` / `h1` and can be changed; **Cancel** or **Run**.

On **Run** the editor saves + builds the current source, starts the instance on the chosen account
with the chosen parameters, then tails the live container logs. (The log stream forwards the
signed-in user's auth cookie to the `/hubs/logs` SignalR hub, so it connects instead of failing with
`Invalid negotiation response received`.)

## Parameter sets

A **parameter set** is a named, reusable set of cBot parameter overrides stored as a flat JSON
object mapping each parameter name to a scalar value, e.g. `{"Period": 14, "Label": "trend"}`. At
run/backtest time it is turned into the cTrader `params.cbotset` file
(`{ "Parameters": { … } }`). You can create/edit a set as raw JSON from the cBot's **Parameter
sets** dialog or inline from the Run dialog.

The JSON is **validated** on save: it must be a single flat object whose values are all scalars
(string / number / bool). A non-object root, an array, a nested object, a `null` value, or malformed
JSON is rejected (a clear error in the dialog, `400 Bad Request` at the API). An empty object `{}`
is allowed and means "no overrides".

## cTrader Console CLI notes

Backtests need `--data-mode` (default `m1`), dates as `dd/MM/yyyy HH:mm`, and
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). See
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). See
[node discovery](../operations/node-discovery.md) and [scaling](../deployment/scaling.md).
## A trading account is required

Running or backtesting a cBot needs a cTrader trading account to connect to. Until you add one under
**Trading accounts**, the **Run New cBot** / **Backtest New cBot** buttons are disabled (with a
tooltip) and the page shows a prompt linking to account setup — you no longer hit a raw
`stream connect failed` error from a bot with no account.
