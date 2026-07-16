---
description: "编译、运行、回测 cTrader cBot（C# 和 Python，均基于 .NET）——通过浏览器内 Monaco IDE 编辑，运行于官方 ghcr.io/spotware/ctrader-console 镜像。"
---

# Build & backtest cBots

通过浏览器内 Monaco IDE 编辑，编译、运行、回测 cTrader cBot（C# **和** Python，均基于 .NET），运行于官方 `ghcr.io/spotware/ctrader-console` 镜像。

## Build

- **Builder** 页面托管 Monaco 编辑器；`CBotBuilder` 在**一次性容器**中使用 `dotnet build` 编译项目（`AppOptions.BuildImage`，工作目录在 `/work` 挂载），以防不受信任的用户 MSBuild 目标访问主机。NuGet 还原通过共享卷跨构建缓存。Web 主机需要 Docker 套接字访问权限。
- C# 和 Python 启动器模板位于 `src/Nodes/Builder/Templates/`。

## Run & backtest

- **Instances** = TPH 状态层次结构（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`）。状态转换替换实体（id 改变），容器 id 保留。
- `NodeScheduler` 选择负载最低的合格节点；`ContainerDispatcherFactory` 路由到远程节点 HTTP 代理或本地 Docker 分派器。
- 完成轮询器协调已退出的容器（回测容器通过 `--exit-on-stop` 自动退出）；报告存在 → 已完成（存储 `ReportJson`），缺失 → 失败。
- 实时容器日志通过 SignalR 流式传输到浏览器；回测权益曲线从报告解析并制成图表。

## Backtest market data is cached per account

cTrader Console 将历史 tick/bar 数据下载到其 `--data-dir`。该目录是一个**稳定的持久缓存，以交易账户**（其账户号）**作为键**——从节点磁盘挂载到其自有容器路径（`/mnt/data`），是一个**独立的、非嵌套的挂载点**，与每个实例的工作目录分开。因此，同一账户上的每次回测**重用**已下载的数据，而不是每次运行都重新下载。（早期数据目录位于每个实例的工作目录下，其 id 每次运行都改变，这强制每次回测都进行新的下载。）临时的每个实例工作目录仍然保存算法、参数、密码和报告；共享数据缓存计入节点的回测数据使用量，并由节点清理操作清除。

## Backtest settings

**Backtest** 对话框公开 cTrader Console 回测 CLI 接受的每项设置，因此您永远无需接触命令行：

- **From / To** — 回测窗口（`--start` / `--end`）。
- **Data mode** — cTrader 三种模式之一（`--data-mode`）：**Tick data**（`tick`，精确）、**m1 bars**（`m1`，快速）或 **Open prices only**（`open`，最快）。
- **Starting balance** — 默认为 `10000`（`--balance`）。**0 余额不产生交易，cTrader 会发出空报告然后崩溃**（"Message expected"），因此始终发送非零余额。
- **Commission** 和 **Spread** — `--commission` / `--spread`（spread 单位为点差）。
- **Data file**（可选）— 节点端的历史数据文件路径（`--data-file`）；留空则使用已下载/缓存的数据。
- **Expose environment variables** — 一个切换，将主机环境变量传递给 cBot（`--environment-variables` 标志）。

## Instance detail page

打开一个实例（`/instance/{id}`）显示其实时状态、日志，以及对于回测，显示权益曲线。**浏览器标签页标题**反映具体实例（**cBot 名称 · 类型 · 交易对**，例如 `TrendBot · Backtest · EURUSD`），因此实时运行标签和回测标签可以一目了然地区分。同一 cBot 的运行和回测作为不同的**lineages** 跟踪（稳定的 lineage id 跨状态转换保留），因此页面恰好跟踪一个实例，从不混淆运行和回测的数据。

## Instance lifecycle controls

每个实例行（及其详情页）都有状态正确的控件。一个**活跃的**实例显示 **Stop**；一个**终端的**实例（Stopped / Completed / Failed）显示 **Start (▶)** 以使用相同的 cBot、账户、交易对、时间框架、参数集和镜像重新启动它（运行作为运行重启，回测作为回测重启）。单击 Stop 显示"Stopping…"通知并禁用该图标直到解决，新创建的运行立即出现在列表中——无需页面重新加载。

控制台日志在实例**终止时保留** ——对于运行（在 Stop 时）和**回测**（在完成时）——因此上次运行的日志在详情页上保持可查看，并且通过日志工具栏，**复制到剪贴板**（复制日志图标）或**下载**（下载日志图标），即使在容器消失后也是如此。两者都作用于实例的完整控制台日志，而不仅仅是屏幕上的尾部。

**uploaded** 的 `.algo` 从未在此构建过，因此其在 cBots 页面的 **Last Build** 列留空（仅对在浏览器中构建的 cBots 显示构建时间）。

## Edit & re-run a stopped instance

一个**已停止的**实例（运行或回测）有一个 **Edit** 控件——列表中其行上的图标**以及**详情页上 Start/Stop 旁的图标——打开一个**预填充**其当前配置的对话框。您可以更改**交易账户、交易对、时间框架、参数集和镜像标签**（以及对于回测，**窗口和上述所有回测设置**），然后 **Save & start** 使用新设置重新启动它（替换已停止的实例）。该控件在**实例活跃时禁用**——仅一个已停止的实例可以编辑。

## Run from the code editor

单击代码编辑器中的 **Run** 打开一个对话框，而不是触发盲目的硬编码运行：

- **Trading account**（必需）— cBot 连接到的 cTrader 账户。
- **Parameter set**（可选）— 选择现有集合，或留空以使用 cBot 的**默认参数值**运行。参数选择器旁的 **+** 按钮可内联创建新参数集（见下文）并选中它。
- **Symbol / Timeframe** 默认为 `EURUSD` / `h1`，可更改；**Cancel** 或 **Run**。

单击 **Run** 时，编辑器保存 + 构建当前源代码，在选定账户上以选定参数启动实例，然后跟踪实时容器日志。（日志流转发已登录用户的 auth cookie 到 `/hubs/logs` SignalR hub，以便它连接而不是因 `Invalid negotiation response received` 而失败。）

## Parameter sets

一个**parameter set** 是一个命名的、可重用的 cBot 参数覆盖集合，存储为平面 JSON 对象，将每个参数名映射到标量值，例如 `{"Period": 14, "Label": "trend"}`。在运行/回测时，它转换为 cTrader `params.cbotset` 文件
（`{ "Parameters": { … } }`）。您可以从 cBot 的 **Parameter sets** 对话框作为原始 JSON 创建/编辑集合，或从 Run 对话框内联创建/编辑。

每个参数集**属于一个 cBot**：新参数集对话框列出所有 cBot，您**必须选择一个**——选择 cBot 前创建被阻止。一个集合的**名称在每个 cBot 内唯一**：将集合创建或重命名为同一 cBot 的另一个集合已使用的名称会被拒绝（对话框中有清晰错误，API 处 `409 Conflict`）。相同名称可在**不同的** cBot 上重用。

JSON 在保存时**被验证**：它必须是单一平面对象，其值都是标量（string / number / bool）。非对象根、数组、嵌套对象、`null` 值或格式错误的 JSON 会被拒绝（对话框中有清晰错误，API 处 `400 Bad Request`）。空对象 `{}` 被允许，意味着"无覆盖"。

## cTrader Console CLI notes

回测需要 `--data-mode`（默认 `m1`）、`dd/MM/yyyy HH:mm` 格式的日期和 `params.cbotset` JSON 位置参数；`run` 拒绝 `--data-dir`（仅回测）。参见 `ContainerCommandHelpers`。

## Nodes & scale

执行容量通过添加节点代理扩展（自注册 + 心跳）。参见[node discovery](../operations/node-discovery.md)和[scaling](../deployment/scaling.md)。

## A trading account is required

运行或回测 cBot 需要一个 cTrader 交易账户来连接。在您在**Trading accounts**下添加一个之前，**Run New cBot** / **Backtest New cBot** 按钮被禁用（带工具提示），页面显示一个链接到账户设置的提示——您不再会遇到一个没有账户的 bot 的原始 `stream connect failed` 错误。
