---
description: "Build run backtest cTrader cBots (C และ Python ทั้ง .NET) จาก in-browser Monaco IDE run บน official ghcr.io/spotware/ctrader-console image"
---

# Build & backtest cBots

Build run backtest cTrader cBots (C# **และ** Python ทั้ง .NET) จาก in-browser Monaco IDE run บน official `ghcr.io/spotware/ctrader-console` image

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project ด้วย `dotnet build` **ใน throwaway container** (`AppOptions.BuildImage` work dir bind-mount ที่ `/work`) ดังนั้น untrusted user MSBuild targets no reach host NuGet restore cached ข้ามบน builds ผ่าน shared volume Web host need Docker socket access
- C# + Python starter templates อยู่ใน `src/Nodes/Builder/Templates/`

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`) Transition replace entity (id เปลี่ยน) container id carried over
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route ไปยัง remote node HTTP agent หรือ local Docker dispatcher
- Completion pollers reconcile exited containers (backtest containers self-exit ผ่าน `--exit-on-stop`); report present → completed (store `ReportJson`) missing → failed
- Live container logs stream ไปยัง browser ผ่าน SignalR; backtest equity curves parsed จาก report + charted

## cTrader Console CLI notes

Backtests ต้อง `--data-mode` (default `m1`) dates เป็น `dd/MM/yyyy HH:mm` และ `params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only) ดู `ContainerCommandHelpers`

## Nodes & scale

Execution capacity scale โดย adding node agents (self-register + heartbeat) ดู [node discovery](../operations/node-discovery.md) และ [scaling](../deployment/scaling.md)
