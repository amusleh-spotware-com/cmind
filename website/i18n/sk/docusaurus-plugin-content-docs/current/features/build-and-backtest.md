---
description: "Build, run, backtest cTrader cBots (C# a Python, oboje .NET) z in-browser Monaco IDE, spustite na official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, spustite, backtest cTrader cBots (C# **a** Python, oboje .NET) z in-browser Monaco
IDE, spustite na official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile projekt s
  `dotnet build` **v throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  na `/work`), takže neverihodný user MSBuild targets nemôže dosiahnuť host. NuGet restore cached
  cross builds cez shared volume. Web host potrebuje Docker socket access.
- C# + Python starter templates žijú v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route na
  remote node HTTP agent alebo local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit cez
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream na browser cez SignalR; backtest equity curves parsed z
  report + charted.

## cTrader Console CLI notes

Backtesty potrebujú `--data-mode` (default `m1`), dates ako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Pozrite
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). Pozrite
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).
