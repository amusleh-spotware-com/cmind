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

## Backtest settings

The **Backtest** dialog exposes the user-tunable cTrader Console backtest settings, so you never have to
touch a command line:

- **Symbol / Timeframe** — the timeframe is a **dropdown of every cTrader period** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, and the Renko/Range/Heikin periods), in the
  console's canonical casing, so you always pick a valid `--period`.
- **From / To** — the backtest window (`--start` / `--end`).
- **Data mode** — one of the three cTrader modes (`--data-mode`): **Tick data** (`tick`, accurate),
  **m1 bars** (`m1`, fast), or **Open prices only** (`open`, fastest).
- **Starting balance** — defaults to `10000` (`--balance`). A **0 balance places no trades and makes
  cTrader emit an empty report it then crashes on** ("Message expected"), so a non-zero balance is
  always sent.
- **Commission** and **Spread** — `--commission` / `--spread` (spread in pips).

The data directory (`--data-file` / `--data-dir`) is managed by the app itself (a per-account cache, see
above), not exposed in the dialog.

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
**backtest** (on completion) alike — so the last run's logs stay viewable on the detail page and,
via the log toolbar, **copied to the clipboard** (Copy logs icon) or **downloaded** (Download logs
icon) even after the container is gone. Both act on the instance's full console log, not just the
on-screen tail.

An **uploaded** `.algo` was never built here, so its **Last Build** column on the cBots page is left
blank (it shows a build time only for cBots you build in the browser).

## Edit & re-run a stopped instance

A **stopped** instance (run or backtest) has an **Edit** control — an icon on its row in the list **and**
beside Start/Stop on its detail page — that opens a dialog **prefilled** with its current configuration.
You can change the **trading account, symbol, timeframe, parameter set and image tag** (and, for a
backtest, the **window and all backtest settings** above), then **Save & start** re-launches it with the
new settings (replacing the stopped instance). The control is **disabled while the instance is active** —
only a stopped instance can be edited.

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

Every parameter set **belongs to a cBot**: the New Parameter Set dialog lists all your cBots and you
**must pick one** — creation is blocked until a cBot is selected. A set's **name is unique per cBot**:
creating or renaming a set to a name another set of the same cBot already uses is rejected (a clear
error in the dialog, `409 Conflict` at the API). The same name may be reused on a **different** cBot.

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
