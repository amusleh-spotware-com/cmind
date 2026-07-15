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
