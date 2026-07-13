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
<!-- [ZH-HANS] Translation needed -->
