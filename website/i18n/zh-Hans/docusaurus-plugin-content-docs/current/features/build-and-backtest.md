---
description: "在浏览器内 Monaco IDE 中构建、运行、回测 cTrader cBots（C# 和 Python，均基于 .NET），在官方 ghcr.io/spotware/ctrader-console 镜像上运行。"
---

# 构建与回测 cBots

在浏览器内 Monaco IDE 中构建、运行、回测 cTrader cBots（C# **和** Python，均基于 .NET），在官方 `ghcr.io/spotware/ctrader-console` 镜像上运行。

## 构建

- **Builder** 页面托管 Monaco 编辑器；`CBotBuilder` 使用 `dotnet build` **在临时容器中**编译项目（`AppOptions.BuildImage`，工作目录挂载在 `/work`），因此不受信任的用户 MSBuild 目标无法访问主机。NuGet 恢复通过共享卷跨构建缓存。Web 主机需要 Docker 套接字访问。
- C# + Python 启动模板位于 `src/Nodes/Builder/Templates/`。

## 运行与回测

- **Instances** = TPH 状态层次（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`）。状态转换替换实体（id 变化），容器 id 保留。
- `NodeScheduler` 选择负载最低的符合条件的 node；`ContainerDispatcherFactory` 路由到远程 node HTTP 代理或本地 Docker dispatcher。
- 完成轮询器协调已退出的容器（回测容器通过 `--exit-on-stop` 自动退出）；报告存在 → 已完成（存储 `ReportJson`），缺失 → 失败。
- 实时容器日志通过 SignalR 流式传输到浏览器；回测权益曲线从报告中解析并绘制图表。

## 回测市场数据按账户缓存

cTrader Console 将历史 tick/bar 数据下载到其 `--data-dir` 中。该目录是一个**稳定的持久缓存，由交易账户（其账户号）标记** — 从 node 的磁盘挂载到其容器路径（`/mnt/data`），是一个**独立的、非嵌套的挂载点**，不同于每个实例的工作目录。因此同一账户上的每次回测**重用**已下载的数据，而不是每次运行时重新下载。（之前数据目录位于每个实例的工作目录下，其 id 每次运行都会变化，这导致每次回测都需要重新下载。）临时的每实例工作目录仍然保存算法、参数、密码和报告；共享数据缓存被计入 node 的回测数据使用量，并通过 node 清理操作清除。

## 回测设置

**Backtest** 对话框公开了 cTrader Console 回测 CLI 接受的每个设置，因此您无需接触命令行：

- **From / To** — 回测窗口（`--start` / `--end`）。
- **Data mode** — `m1`（1 分钟 bar）或 `tick`（`--data-mode`）。
- **Starting balance** — 默认为 `10000`（`--balance`）。**0 余额不会下单，并会导致 cTrader 生成空报告然后崩溃**（"消息预期"），因此始终发送非零余额。
- **Commission** 和 **Spread**（`--commission` / `--spread`，点差以点为单位）。
- **Advanced options** — 自由格式的 `name=value` 每行框，用于 cTrader 支持的任何其他回测选项（例如 `applyCommissionAutomatically=true`）；每行变成一个 `--name value` CLI 参数。

## 实例详情页

打开一个实例（`/instance/{id}`）显示其实时状态、日志和 — 对于回测 — 权益曲线。**浏览器标签页标题**反映特定实例（**cBot 名称 · 类型 · 符号**，例如 `TrendBot · Backtest · EURUSD`），因此实时运行标签页和回测标签页一眼就能区分。同一 cBot 的运行和回测被跟踪为不同的**谱系**（一个稳定的谱系 id 跨越状态转换保留），因此页面恰好跟踪一个实例，从不混合运行数据和回测数据。

## 实例生命周期控制

每个实例行（及其详情页）有状态正确的控制。一个**活跃**实例显示**Stop**；一个**终止**的（Stopped / Completed / Failed）显示**Start（▶）**以使用相同的 cBot、账户、符号、时间框架、参数集和镜像重新启动它（运行以运行方式重启，回测以回测方式重启）。点击 Stop 显示"Stopping…"提示并禁用图标直到解决，新创建的运行立即出现在列表中 — 无需页面重新加载。

控制台日志在实例终止时**被持久化** — 对于运行（在 Stop 时）和**回测**（在完成时）— 因此最后一次运行的日志保持可在详情页上查看，并通过日志工具栏，**复制到剪贴板**（复制日志图标）或**下载**（下载日志图标），即使在容器消失后也是如此。两者都作用于实例的完整控制台日志，而不仅仅是屏幕上的尾部。

一个**上传的** `.algo` 从未在此处构建，因此其在 cBots 页面上的**最后构建**列为空白（它仅为在浏览器中构建的 cBots 显示构建时间）。

## 编辑并重新运行已停止的实例

一个**已停止**的实例（运行或回测）有一个**Edit** 控制 — 列表中其行上的图标**并且**在其详情页的 Start/Stop 旁 — 打开一个对话框**预填充**其当前配置。您可以更改**交易账户、符号、时间框架、参数集和镜像标签**（并且，对于回测，**窗口和上述所有回测设置**），然后**Save & start** 使用新设置重新启动它（替换已停止的实例）。控制在实例活跃时**被禁用** — 只有已停止的实例可以编辑。

## 从代码编辑器运行

点击代码编辑器中的**Run** 打开一个对话框，而不是触发盲目的硬编码运行：

- **Trading account**（必需）— cBot 连接到的 cTrader 账户。
- **Parameter set**（可选）— 选择现有集，或将其留空以使用 cBot 的**默认参数值**运行。选择器旁的**+** 按钮创建新参数集（见下文）并选择它。
- **Symbol / Timeframe** 默认为 `EURUSD` / `h1` 可以更改；**Cancel** 或 **Run**。

在**Run** 时编辑器保存 + 构建当前源代码，使用选择的账户和选择的参数启动实例，然后跟踪实时容器日志。（日志流将已登录用户的身份验证 cookie 转发到 `/hubs/logs` SignalR hub，因此它连接而不是以 `Invalid negotiation response received` 失败。）

## 参数集

一个**参数集**是一个命名的、可重用的 cBot 参数覆盖集，存储为映射每个参数名到标量值的平面 JSON 对象，例如 `{"Period": 14, "Label": "trend"}`。在运行/回测时它被转换为 cTrader `params.cbotset` 文件（`{ "Parameters": { … } }`）。您可以从 cBot 的**Parameter sets** 对话框创建/编辑一个集作为原始 JSON，或从 Run 对话框内联创建。

每个参数集**属于一个 cBot**：新参数集对话框列出所有 cBots，您**必须选择一个** — 在选择 cBot 前创建被阻止。一个集的**名称在每个 cBot 上唯一**：创建或重命名一个集到同一 cBot 的另一个集已使用的名称被拒绝（对话框中的清晰错误，API 的 `409 Conflict`）。相同的名称可以在**不同的** cBot 上重用。

JSON 在保存时**被验证**：它必须是一个单个平面对象，其值都是标量（字符串/数字/布尔值）。非对象根、数组、嵌套对象、`null` 值或格式错误的 JSON 被拒绝（对话框中的清晰错误，API 的 `400 Bad Request`）。空对象 `{}` 被允许，表示"无覆盖"。

## cTrader Console CLI 说明

回测需要 `--data-mode`（默认 `m1`），日期格式为 `dd/MM/yyyy HH:mm`，以及 `params.cbotset` JSON 位置参数；`run` 拒绝 `--data-dir`（仅回测）。请参阅 `ContainerCommandHelpers`。

## Nodes 与扩展

通过添加 node 代理（自注册 + 心跳）扩展执行容量。请参阅[节点发现](../operations/node-discovery.md)和[扩展](../deployment/scaling.md)。

## 需要交易账户

运行或回测 cBot 需要一个 cTrader 交易账户来连接到。直到您在**Trading accounts** 下添加一个，**Run New cBot** / **Backtest New cBot** 按钮被禁用（带有提示），页面显示一个链接到账户设置的提示 — 您不再会从没有账户的 bot 得到原始的 `stream connect failed` 错误。
