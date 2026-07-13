---
description: "Build, run, backtest cTrader cBots (C# i Python, oboje .NET) z in-browser Monaco IDE, běží na oficiálním ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **i** Python, oboje .NET) z in-browser Monaco
IDE, běží na oficiálním `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** stránka hostuje Monaco editor; `CBotBuilder` kompiluje projekt s
  `dotnet build` **v throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`), takže untrusted user MSBuild targets nedosáhnou hosta. NuGet restore cached
  across builds přes shared volume. Web host potřebuje Docker socket access.
- C# + Python starter templaty žijí v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick nejméně zatížený eligible node; `ContainerDispatcherFactory` route to
  remote node HTTP agent nebo local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Živé container logy streamují do prohlížeče přes SignalR; backtest equity curves parsované z
  report + charted.

## cTrader Console CLI poznámky

Backtesty potřebují `--data-mode` (default `m1`), datumy jako `dd/MM/yyyy HH:mm`, a
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Viz
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale přidáváním node agentů (samo-registrace + heartbeat). Viz
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).
