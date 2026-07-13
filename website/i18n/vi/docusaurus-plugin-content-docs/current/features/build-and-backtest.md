---
description: "Build, run, backtest cTrader cBots (C# và Python, cả hai .NET) từ in-browser Monaco IDE, chạy trên official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **and** Python, cả hai .NET) từ in-browser Monaco
IDE, chạy trên official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project với
  `dotnet build` **in throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`), vì vậy untrusted user MSBuild targets không reach host. NuGet restore cached
  across builds via shared volume. Web host cần Docker socket access.
- C# + Python starter templates sống trong `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route to
  remote node HTTP agent hoặc local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream to browser over SignalR; backtest equity curves parsed from
  report + charted.

## cTrader Console CLI notes

Backtests cần `--data-mode` (mặc định `m1`), dates as `dd/MM/yyyy HH:mm`, và
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Xem
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). Xem
[node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).
