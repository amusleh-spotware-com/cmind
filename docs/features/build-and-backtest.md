# Build & backtest cBots

Build, run, and backtest cTrader cBots (C# **and** Python, both .NET) from an in-browser Monaco
IDE, executed on the official `ghcr.io/spotware/ctrader-console` image.

## Build

- The **Builder** page hosts a Monaco editor; `CBotBuilder` compiles the project with
  `dotnet build` **inside a throwaway container** (`AppOptions.BuildImage`, work dir bind-mounted
  at `/work`), so untrusted user MSBuild targets can't reach the host. NuGet restore is cached
  across builds via a shared volume. The Web host needs Docker socket access.
- Starter templates for C# and Python live in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** are a TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). A transition replaces the entity (the id changes),
  and the container id is carried over.
- `NodeScheduler` picks the least-loaded eligible node; `ContainerDispatcherFactory` routes to a
  remote node's HTTP agent or the local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); a present report → completed (stores `ReportJson`), missing → failed.
- Live container logs stream to the browser over SignalR; backtest equity curves are parsed from
  the report and charted.

## cTrader Console CLI notes

Backtests require `--data-mode` (default `m1`), dates as `dd/MM/yyyy HH:mm`, and a
`params.cbotset` JSON positional arg; `run` rejects `--data-dir` (backtest-only). See
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scales by adding node agents (self-registering + heartbeating). See
[node discovery](../operations/node-discovery.md) and [scaling](../deployment/scaling.md).
