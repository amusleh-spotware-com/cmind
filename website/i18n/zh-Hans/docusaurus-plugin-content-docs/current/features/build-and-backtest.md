---
description: "从浏览器内 Monaco IDE 构建、运行、回测 cTrader cBots（C# 和 Python，均为 .NET），在官方 ghcr.io/spotware/ctrader-console 镜像上运行。"
---

# 构建与回测 cBots

从浏览器内 Monaco IDE 构建、运行、回测 cTrader cBots（C# **和** Python，均为 .NET），在官方 `ghcr.io/spotware/ctrader-console` 镜像上运行。

## 构建

- **Builder** 页面托管 Monaco 编辑器；`CBotBuilder` 使用 `dotnet build` **在临时容器中**编译项目（`AppOptions.BuildImage`，工作目录绑定挂载在 `/work`），使不受信任的用户 MSBuild 目标无法到达主机。NuGet 还原通过共享卷在构建间缓存。Web 主机需要 Docker 套接字访问权限。
- C# 和 Python 初始模板位于 `src/Nodes/Builder/Templates/`。

## 运行与回测

- **Instances** = TPH 状态层次结构（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`）。转换替换实体（ID 变化），容器 ID 保留。
- `NodeScheduler` 选择负载最低的符合条件的 Node；`ContainerDispatcherFactory` 路由到远程 Node HTTP 代理或本地 Docker 调度程序。
- 完成轮询器协调已退出的容器（回测容器通过 `--exit-on-stop` 自动退出）；报告存在 → 已完成（存储 `ReportJson`），缺失 → 失败。
- 实时容器日志通过 SignalR 流式传输到浏览器；回测权益曲线从报告解析并绘制。

## 回测市场数据按账户缓存

cTrader Console 将历史 tick/bar 数据下载到其 `--data-dir`。该目录是一个**稳定的、持久的缓存，以交易账户**（其账号）**为键** — 从 Node 磁盘绑定挂载到其自己的容器路径（`/mnt/data`），是一个**与每实例工作目录分离的非嵌套挂载**。因此，在同一账户上的每次回测都**重用**已下载的数据，而不是每次运行都重新下载。（早前数据目录位于每实例工作目录下，其 ID 每次运行都变化，这导致每次回测都强制进行新的下载。）临时的每实例工作目录仍然保存算法、参数、密码和报告；共享数据缓存计入 Node 的回测数据使用中，并通过 Node 清理操作清除。

## 回测设置

**Backtest** 对话框公开用户可调的 cTrader Console 回测设置，所以您无需接触命令行：

- **Symbol / Timeframe** — timeframe 是 **cTrader 每个周期的下拉列表**（`t1`…`t1000`，`m1`…`m45`，`h1`…`h12`，`D1`/`D2`/`D3`，`W1`，`Month1` 以及 Renko/Range/Heikin 周期），采用控制台的规范大小写，使您始终选择有效的 `--period`。
- **From / To** — 回测时间窗口（`--start` / `--end`）。
- **Data mode** — cTrader 三种模式中的一种（`--data-mode`）：**Tick data**（`tick`，精确），**m1 bars**（`m1`，快速），或 **Open prices only**（`open`，最快）。
- **Starting balance** — 默认 `10000`（`--balance`）。**0 余额不会进行任何交易，并使 cTrader 发出空报告然后崩溃**（"Message expected"），因此总是发送非零余额。
- **Commission** — `--commission`。
- **Spread** — `--spread`，一个**不能低于 0 的数字字段，以点差为单位**。它**在 Tick 数据模式下隐藏**，其中 cTrader 从 tick 数据本身派生点差（不发送 `--spread`）。

数据目录（`--data-file` / `--data-dir`）由应用本身管理（按账户缓存，见上文），不在对话框中公开。

:::note cTrader 在空回测时崩溃
如果回测**产生无结果** — 无交易，或所选日期/Symbol 的市场数据不可用 — cTrader Console 自己的报告编写器会抛出 `Message expected` 并在没有报告的情况下退出。应用无法修复该上游 bug，但它检测到并将实例标记为**失败**，并包含可操作的原因（"所选范围内无回测结果…"），而不是原始堆栈跟踪。选择一个更宽的日期范围，其中有可用的市场数据并重试。
:::

## 实例详情页

打开一个实例（`/instance/{id}`）显示其实时状态、日志和（对于回测）权益曲线。**浏览器标签页标题**反映特定实例（**cBot 名称 · 类型 · Symbol**，例如 `TrendBot · Backtest · EURUSD`），使实时运行标签页和回测标签页一目了然地区分。运行和回测同一 cBot 被追踪为不同的**系列**（在状态转换间保留的稳定系列 ID），所以页面精确遵循一个实例，永不混合运行数据与回测数据。

## 实例生命周期控制

每个实例行（及其详情页）都有状态正确的控制。**活跃**实例显示**Stop**；**终端**实例（Stopped / Completed / Failed）显示 **Start (▶)** 以使用相同的 cBot、账户、Symbol、timeframe、ParamSet 和镜像重新启动它（运行重启为运行，回测重启为回测）。单击 Stop 显示"Stopping…"通知并禁用图标直到解决，新创建的运行立即出现在列表中 — 无需页面重新加载。

控制台日志**在实例终止时持久化** — 对于运行（在 Stop 时）和**回测**（在完成时）均如此 — 所以最后一次运行的日志保持在详情页可查看，并通过日志工具栏，**复制到剪贴板**（复制日志图标）或**下载**（下载日志图标），即使容器已消失也如此。两者都作用于实例的完整控制台日志，而不仅是屏幕上的尾部。

一个**上传的** `.algo` 从未在这里构建，所以其 cBots 页面上的**Last Build** 列留空（仅对您在浏览器中构建的 cBots 显示构建时间）。

## 编辑并重新运行已停止的实例

一个**停止的**实例（运行或回测）有一个**Edit** 控制 — 列表中其行上的图标**和**详情页上 Start/Stop 旁边的图标 — 打开一个**预填充**其当前配置的对话框。您可以更改**交易账户、Symbol、timeframe、ParamSet 和镜像标签**（以及对于回测，**时间窗口和上面的所有回测设置**），然后**Save & start** 使用新设置重新启动它（替换已停止的实例）。控制**在实例活跃时禁用** — 只有停止的实例可被编辑。

## 从代码编辑器运行

在代码编辑器中单击**Run** 打开一个对话框而不是触发盲目、硬编码的运行：

- **Trading account**（必需） — cBot 连接到的 cTrader 账户。
- **Parameter set**（可选） — 选择现有集，或留空以使用 cBot 的**默认参数值**运行。选择器旁的 **+** 按钮内联创建新 ParamSet（见下文）并选择它。
- **Symbol / Timeframe** 默认为 `EURUSD` / `h1`，可更改；**Cancel** 或 **Run**。

在**Run** 时，编辑器保存并构建当前源、在所选账户上以所选参数启动实例，然后追踪实时容器日志。（日志流将已签入用户的认证 cookie 转发到 `/hubs/logs` SignalR hub，所以它连接而不是失败并出现 `Invalid negotiation response received`。）

## 参数集

一个**参数集**是一个命名的、可重用的 cBot 参数覆盖集，存储为平面 JSON 对象，将每个参数名映射到标量值，例如 `{"Period": 14, "Label": "trend"}`。在运行/回测时，它被转换为 cTrader `params.cbotset` 文件（`{ "Parameters": { … } }`）。您可以从 cBot 的**Parameter sets** 对话框以原始 JSON 创建/编辑集，或从 Run 对话框内联创建/编辑。

每个参数集**属于一个 cBot**：新 Parameter Set 对话框列出您的所有 cBots，您**必须选择一个** — 创建被阻止直到选择 cBot。一个集的**名称在每个 cBot 内唯一**：将一个集创建或重命名为同一 cBot 的另一个集已使用的名称被拒绝（对话框中的清晰错误，API 处为 `409 Conflict`）。相同名称可在**不同的** cBot 上重用。

JSON **在保存时验证**：它必须是一个单一平面对象，其值全为标量（string / number / bool）。非对象根、数组、嵌套对象、`null` 值或格式错误的 JSON 被拒绝（对话框中的清晰错误，API 处为 `400 Bad Request`）。空对象 `{}` 被允许，表示"无覆盖"。

## cTrader Console CLI 笔记

回测需要 `--data-mode`（默认 `m1`）、日期格式 `dd/MM/yyyy HH:mm` 和 `params.cbotset` JSON 位置参数；`run` 拒绝 `--data-dir`（仅回测）。参见 `ContainerCommandHelpers`。

## Nodes 与扩展

执行容量通过添加 Node 代理（自注册 + 心跳）进行扩展。参见[node discovery](../operations/node-discovery.md) 和 [scaling](../deployment/scaling.md)。

## 需要交易账户

运行或回测一个 cBot 需要一个 cTrader 交易账户来连接。在您在**Trading accounts** 下添加一个之前，**Run New cBot** / **Backtest New cBot** 按钮被禁用（带工具提示），页面显示一个链接到账户设置的提示 — 您不再遇到来自没有账户的 bot 的原始 `stream connect failed` 错误。
