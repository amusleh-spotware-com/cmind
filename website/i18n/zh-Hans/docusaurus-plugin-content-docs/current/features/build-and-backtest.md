---
description: "编译、运行、回测 cTrader cBots（C# 和 Python，均基于 .NET）——在浏览器内的 Monaco 编辑器中进行，在官方 ghcr.io/spotware/ctrader-console 镜像上执行。"
---

# 编译和回测 cBots

在浏览器内的 Monaco 编辑器中编译、运行、回测 cTrader cBots（C# **和** Python，均基于 .NET），在官方 `ghcr.io/spotware/ctrader-console` 镜像上执行。

## 编译

- **Builder** 页面托管 Monaco 编辑器；`CBotBuilder` 在**一次性容器**中使用 `dotnet build` 编译项目（`AppOptions.BuildImage`，工作目录在 `/work` 处挂载），防止不受信的用户 MSBuild 目标访问主机。NuGet 还原通过共享卷缓存，跨多次构建复用。Web 主机需要 Docker 套接字访问。
- C# + Python 入门模板位于 `src/Nodes/Builder/Templates/`。

## 运行和回测

- **Instances**（实例）= TPH 状态层级（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`）。状态转换会替换实体（id 改变），容器 id 保持不变。
- `NodeScheduler` 选择负载最低的合适 Node；`ContainerDispatcherFactory` 路由到远程 Node HTTP 代理或本地 Docker 分发器。
- 完成轮询器协调已退出的容器（回测容器通过 `--exit-on-stop` 自动退出）；若报告存在 → 标记为已完成（存储 `ReportJson`），若容器不存在 → 标记为失败。
- 实时容器日志通过 SignalR 流式传输到浏览器；回测权益曲线从报告中解析并绘制。

## 回测市场数据按账户缓存

cTrader Console 将历史 tick/bar 数据下载到其 `--data-dir`。该目录是一个**稳定的、持久化的缓存，以交易账户**（其账户号码）**为键** —— 从 Node 磁盘以其自身容器路径（`/mnt/data`）挂载，这是**与单个实例工作目录分离的独立非嵌套挂载**。因此，同一账户上的每次回测都**复用**已下载的数据，而不是每次运行时重新下载。（早前数据目录位于每个实例工作目录下，其 id 每次运行都改变，这导致每次回测都必须重新下载。）临时的单个实例工作目录仍然保存算法、参数、密码和报告；共享数据缓存计入 Node 的回测数据使用量，可通过 Node 清理操作清除。

## 回测设置

**Backtest** 对话框暴露用户可调整的 cTrader Console 回测设置，因此你永远无需触及命令行：

- **Symbol / Timeframe**（符号/时间周期）—— 时间周期是一个**每个 cTrader 周期的下拉列表**（`t1`…`t1000`、`m1`…`m45`、`h1`…`h12`、`D1`/`D2`/`D3`、`W1`、`Month1` 以及 Renko/Range/Heikin 周期），采用控制台的规范大小写，确保总是选择有效的 `--period`。
- **From / To**（起止时间）—— 回测窗口（`--start` / `--end`）。
- **Data mode**（数据模式）—— 三种 cTrader 模式之一（`--data-mode`）：**Tick data**（`tick`，精确）、**m1 bars**（`m1`，快速）或 **Open prices only**（`open`，最快）。
- **Starting balance**（起始余额）—— 默认为 `10000`（`--balance`）。**余额为 0 会导致不交易，并使 cTrader 生成空报告从而崩溃**（"Message expected"），因此总是发送非零余额。
- **Commission**（佣金）—— `--commission`。
- **Spread**（点差）—— `--spread`，一个**不能低于 0 的数字字段（单位为点）**。在 Tick data 模式下**隐藏**，因为 cTrader 从 tick 数据本身推导点差（不发送 `--spread`）。

数据目录（`--data-file` / `--data-dir`）由应用本身管理（单个账户缓存，见上文），不在对话框中暴露。

:::note cTrader 在空回测时崩溃
如果回测产生**无结果** —— 无交易，或所选日期/符号无市场数据 —— cTrader Console 自身的报告编写器抛出 `Message expected` 并在无报告的情况下退出。应用无法从上游修复该 bug，但它会检测到并将实例标记为**失败**并给出可执行的原因（"所选范围没有回测结果…"），而不是原始堆栈跟踪。选择有可用市场数据的更宽的日期范围并重试。
:::

## 实例详情页面

打开一个实例（`/instance/{id}`）会显示其实时状态、日志以及 —— 对于回测 —— 权益曲线。**浏览器标签页标题**反映特定实例（**cBot 名称 · 类型 · 符号**，例如 `TrendBot · Backtest · EURUSD`），以便一个实时运行标签页和一个回测标签页可以一眼区分。一个 cBot 的运行和回测被追踪为不同的**传系**（跨状态转换保持的稳定传系 id），因此页面只跟踪一个实例，从不混淆运行和回测的数据。

## 实例生命周期控制

每个实例行（及其详情页面）都有状态正确的控制。**活跃**实例显示 **Stop**；**终止**实例（Stopped / Completed / Failed）显示 **Start（▶）** 以使用相同的 cBot、账户、符号、时间周期、ParamSet 和镜像标签重新启动（运行作为运行重启，回测作为回测重启）。点击 Stop 会显示"Stopping…"提示并禁用该图标直到完成，新创建的运行立即出现在列表中 —— 无需刷新页面。

控制台日志在实例终止时**被保存** —— 对于运行（在 Stop 时）和**回测**（在完成时）—— 因此最后一次运行的日志在详情页面可查看，且通过日志工具栏可以**复制到剪贴板**（Copy logs 图标）或**下载**（Download logs 图标），即使容器已消失。两者都作用于实例的完整控制台日志，不仅仅是屏幕上的末尾部分。

**已完成的回测**也会以两种格式保存其 **cTrader 报告** —— 原始 **JSON**（权益曲线和 AI 分析读取的相同报告）和完整的 **HTML** 报告。两者都可从回测行**和**详情页面通过专用图标下载。只保留**最后一次运行的**报告，图标对任何未开始、正在运行或失败的回测都是**禁用的**（运行实例上从不显示）—— 只有已完成的回测有报告可下载。

**上传的** `.algo` 文件从未在此处编译，所以 cBots 页面上的 **Last Build** 列留空白（仅显示你在浏览器中编译的 cBots 的编译时间）。

## 编辑和重新运行已停止的实例

**已停止**的实例（运行或回测）有一个 **Edit**（编辑）控制 —— 列表中其行上的图标**以及**详情页面上 Start/Stop 旁边的图标 —— 打开一个**预填充**其当前配置的对话框。你可以更改**交易账户、符号、时间周期、ParamSet 和镜像标签**（对于回测，还有**窗口和以上所有回测设置**），然后**Save & start** 使用新设置重新启动（替换已停止的实例）。该控制在实例活跃时**被禁用** —— 只有已停止的实例可被编辑。

## 从代码编辑器运行

在代码编辑器中点击 **Run** 会打开一个对话框，而不是启动一个盲目、硬编码的运行：

- **Trading account**（交易账户）（必需）—— cBot 连接到的 cTrader 账户。
- **Parameter set**（参数集）（可选）—— 选择一个现有集合，或留空以使用 cBot 的**默认参数值**运行。选择器旁的 **+** 按钮可内联创建新的参数集（见下文）并选中它。
- **Symbol / Timeframe**（符号/时间周期）默认为 `EURUSD` / `h1` 并可更改；**Cancel** 或 **Run**。

点击 **Run** 时，编辑器保存 + 编译当前源代码，在所选账户上使用所选参数启动实例，然后跟踪实时容器日志。（日志流将已登录用户的身份验证 cookie 转发到 `/hubs/logs` SignalR 集线器，因此可以连接而不是以 `Invalid negotiation response received` 失败。）

## 参数集

**参数集**是一个已命名的、可复用的 cBot 参数覆盖集合，以平面 JSON 对象形式存储，将每个参数名映射到一个标量值，例如 `{"Period": 14, "Label": "trend"}`。在运行/回测时，它被转换为 cTrader `params.cbotset` 文件（`{ "Parameters": { … } }`）。你可以从 cBot 的 **Parameter sets** 对话框以原始 JSON 形式创建/编辑集合，或从 Run 对话框内联创建/编辑。

每个参数集**属于一个 cBot**：New Parameter Set 对话框列出你的所有 cBots，你**必须选择一个** —— 直到选择 cBot 才能创建。一个集合的**名称在 cBot 内唯一**：将集合创建或重命名为同一 cBot 的另一集合已使用的名称会被拒绝（对话框中的清晰错误，API 处的 `409 Conflict`）。相同的名称可在**不同的** cBot 上复用。

JSON 在保存时被**验证**：必须是单个平面对象，其值全为标量（string / number / bool）。非对象根、数组、嵌套对象、`null` 值或格式错误的 JSON 会被拒绝（对话框中的清晰错误，API 处的 `400 Bad Request`）。空对象 `{}` 被允许并意味着"无覆盖"。

## cTrader Console CLI 注意事项

回测需要 `--data-mode`（默认 `m1`）、格式为 `dd/MM/yyyy HH:mm` 的日期和 `params.cbotset` JSON 位置参数；`run` 拒绝 `--data-dir`（仅回测）。见 `ContainerCommandHelpers`。

## Nodes 和扩展

通过添加 Node 代理（自注册 + 心跳）来扩展执行容量。见[节点发现](../operations/node-discovery.md)和[扩展](../deployment/scaling.md)。

## 需要交易账户

运行或回测 cBot 需要一个 cTrader 交易账户来连接。在你在**交易账户**下添加一个之前，**运行新 cBot** / **回测新 cBot** 按钮被禁用（带提示工具提示），页面显示一个指向账户设置的提示 —— 你将不再会遇到来自无账户 bot 的原始 `stream connect failed` 错误。
