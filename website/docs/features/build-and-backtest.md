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
