---
description: "สร้าง รัน และ backtest cTrader cBots (C# และ Python ทั้งคู่ .NET) จาก in-browser Monaco IDE ที่รันบน official ghcr.io/spotware/ctrader-console image"
---

# Build & backtest cBots

สร้าง รัน backtest cTrader cBots (C# **และ** Python ทั้งคู่ .NET) จาก in-browser Monaco
IDE ที่รันบน official `ghcr.io/spotware/ctrader-console` image

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project ด้วย
  `dotnet build` **ใน throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`) ดังนั้น untrusted user MSBuild targets ไม่ถึง host NuGet restore cached
  ข้าม builds ผ่าน shared volume Web host ต้องมี Docker socket access
- C# + Python starter templates อยู่ใน `src/Nodes/Builder/Templates/`

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`) Transition replace entity (id change),
  container id ถูกพาไป
- `NodeScheduler` เลือก least-loaded eligible node; `ContainerDispatcherFactory` route ไปยัง
  remote node HTTP agent หรือ local Docker dispatcher
- Completion pollers reconcile exited containers (backtest containers self-exit ผ่าน
  `--exit-on-stop`); report มี → completed (เก็บ `ReportJson`), ไม่มี → failed
- Live container logs stream ไปยัง browser ผ่าน SignalR; backtest equity curves parse จาก
  report + charted

## cTrader Console CLI notes

Backtests ต้องการ `--data-mode` (default `m1`), dates เป็น `dd/MM/yyyy HH:mm` และ
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only) ดู
`ContainerCommandHelpers`

## Nodes & scale

Execution capacity scale โดยเพิ่ม node agents (self-register + heartbeat) ดู
[node discovery](../operations/node-discovery.md) และ [scaling](../deployment/scaling.md)
