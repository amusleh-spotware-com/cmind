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

## 从代码编辑器运行

在代码编辑器中点击**运行**会打开一个对话框，而不是触发一次盲目的、硬编码的运行：

- **交易账户**（必填）— cBot 连接的 cTrader 账户。
- **参数集**（可选）— 选择一个现有参数集，或留空以使用 cBot 的**默认参数值**运行。选择器旁边的 **+** 按钮会就地创建一个新参数集（见下文）并选中它。
- **品种 / 时间周期**默认为 `EURUSD` / `h1`，可更改；**取消**或**运行**。

点击**运行**时，编辑器会保存并构建当前源代码，在所选账户上使用所选参数启动实例，然后实时跟踪容器日志。（日志流会将已登录用户的身份验证 Cookie 转发到 SignalR 中心 `/hubs/logs`，因此它会成功连接，而不是以 `Invalid negotiation response received` 失败。）

## 参数集

**参数集**是一组具名、可复用的 cBot 参数覆盖，以扁平 JSON 对象的形式存储，将每个参数名映射到一个标量值，例如 `{"Period": 14, "Label": "trend"}`。在运行/回测时，它会被转换为 cTrader 的 `params.cbotset` 文件（`{ "Parameters": { … } }`）。你可以在 cBot 的**参数集**对话框中以原始 JSON 形式创建/编辑参数集，或在运行对话框中就地创建。

JSON 在保存时会被**校验**：它必须是单个扁平对象，且其所有值均为标量（字符串 / 数字 / 布尔）。非对象根、数组、嵌套对象、`null` 值或格式错误的 JSON 都会被拒绝（对话框中显示明确错误，API 返回 `400 Bad Request`）。空对象 `{}` 是允许的，表示"无覆盖"。
