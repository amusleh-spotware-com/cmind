---
description: "编译、运行、回测cTrader cBots（C#和Python，两者皆.NET）——使用浏览器内Monaco IDE，运行于官方ghcr.io/spotware/ctrader-console镜像。"
---

# 编译与回测cBots

在浏览器内Monaco IDE中编译、运行、回测cTrader cBots（C# **和** Python，两者皆.NET），运行于官方`ghcr.io/spotware/ctrader-console`镜像。

## 编译

- **Builder**页面托管Monaco编辑器；`CBotBuilder`在一次性容器中使用`dotnet build`编译项目
  （`AppOptions.BuildImage`，工作目录以bind-mount方式挂载在`/work`），确保不受信的用户MSBuild目标无法访问主机。NuGet恢复通过共享卷在编译间缓存。Web主机需要Docker套接字访问。
- C#和Python起始模板位于`src/Nodes/Builder/Templates/`。

## 运行与回测

- **Instances**=TPH状态层级（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`）。状态转换时替换实体（ID变更），容器ID被保留。
- `NodeScheduler`选择负载最低的符合条件的节点；`ContainerDispatcherFactory`路由至远程节点HTTP代理或本地Docker调度器。
- 完成轮询器协调退出的容器（回测容器通过`--exit-on-stop`自我退出）；报告存在→完成（存储`ReportJson`），缺失→失败。
- 实时容器日志通过SignalR流向浏览器；回测收益曲线从报告解析并绘制。

## 回测市场数据按账户缓存

cTrader Console将历史Tick/柱状数据下载到其`--data-dir`。该目录是一个**稳定的、持久化的缓存，按交易账户键控**（其账户号）——从节点磁盘以bind-mount方式挂载在其自己的容器路径（`/mnt/data`），一个**独立的、非嵌套的挂载**与每个实例的工作目录分离。因此每个相同账户上的回测**都重用**已下载的数据，而不是每次运行都重新下载。（早期数据目录位于每个实例工作目录下，其ID每次运行都变更，这迫使每次回测都进行新的下载。）临时的每实例工作目录仍然保存算法、参数、密码和报告；共享数据缓存计入节点的回测数据使用量，由节点清理动作清除。

## 回测设置

**Backtest**对话框暴露了用户可调的cTrader Console回测设置，因此您永远无需接触命令行：

- **交易品种/周期** ——周期是**每个cTrader周期的下拉列表**（`t1`…`t1000`、
  `m1`…`m45`、`h1`…`h12`、`D1`/`D2`/`D3`、`W1`、`Month1`以及Renko/Range/Heikin周期），使用控制台的规范大小写，确保您始终选择有效的`--period`。
- **开始/结束** ——回测窗口（`--start`/`--end`）。
- **数据模式** ——三种cTrader模式之一（`--data-mode`）：**Tick数据**（`tick`，精确），
  **m1柱状**（`m1`，快速），或**仅开盘价**（`open`，最快）。
- **起始余额** ——默认为`10000`（`--balance`）。**0余额不执行任何交易，并使cTrader发出一个空报告然后崩溃**（"Message expected"），因此始终发送非零余额。
- **佣金**和**点差** ——`--commission`/`--spread`（点差以点数表示）。

数据目录（`--data-file`/`--data-dir`）由应用本身管理（每账户缓存，见上文），不在对话框中暴露。

## 实例详情页

打开一个实例（`/instance/{id}`）显示其实时状态、日志，以及——对于回测——收益曲线。**浏览器标签页标题**反映具体实例（**cBot名称·类型·交易品种**，例如`TrendBot·Backtest·EURUSD`），因此一个实时运行标签页和一个回测标签页一眼就能区分。相同cBot的运行和回测被作为不同的**谱系**跟踪（一个稳定的谱系ID跨状态转换携带），因此该页面精确跟踪一个实例，永不混合运行的数据与回测的数据。

## 实例生命周期控制

每个实例行（及其详情页）都有状态正确的控制。一个**活跃**实例显示**Stop**；一个**终态**的（Stopped/Completed/Failed）显示**Start（▶）**以用相同的cBot、账户、交易品种、周期、参数集和镜像重新启动它（运行以运行方式重启，回测以回测方式重启）。点击Stop显示"Stopping…"提示并禁用该图标直到解决，新创建的运行立即出现在列表中——无需页面重新加载。

控制台日志**在实例终止时被持久化**——对于运行（在Stop时）和**回测**（在完成时）都如此——因此上次运行的日志保持在详情页可查看，并通过日志工具栏，**复制到剪贴板**（复制日志图标）或**下载**（下载日志图标），即使在容器消失后也可用。两者都作用于实例的完整控制台日志，而不仅仅是屏幕上显示的尾部。

一个**上传**的`.algo`从未在此构建过，因此其在cBots页面上的**Last Build**列留空（仅对您在浏览器中构建的cBots显示构建时间）。

## 编辑并重新运行已停止的实例

一个**已停止**的实例（运行或回测）有一个**Edit**控制——列表中其行上的图标**以及**其详情页上Start/Stop旁的图标——打开一个**预填充**其当前配置的对话框。您可以更改**交易账户、交易品种、周期、参数集和镜像标签**（以及对于回测，**窗口和上述所有回测设置**），然后**Save & start**以新设置重新启动它（替换已停止的实例）。该控制在**实例活跃时被禁用**——仅已停止的实例才能被编辑。

## 从代码编辑器运行

在代码编辑器中点击**Run**打开一个对话框而不是触发一个盲目的、硬编码的运行：

- **交易账户**（必需）——cBot连接到的cTrader账户。
- **参数集**（可选）——选择现有集合，或将其留空以使用cBot的**默认参数值**运行。选择器旁的**+**按钮内联创建新参数集（见下文）并选择它。
- **交易品种/周期**默认为`EURUSD`/`h1`并可更改；**Cancel**或**Run**。

在**Run**上编辑器保存+编译当前源，在所选账户上使用所选参数启动实例，然后跟踪实时容器日志。（日志流将已登录用户的身份认证cookie转发至`/hubs/logs` SignalR hub，因此其连接而不是以`Invalid negotiation response received`失败。）

## 参数集

一个**参数集**是一个命名的、可重用的cBot参数覆盖集合，存储为扁平JSON对象，将每个参数名映射到一个标量值，例如`{"Period": 14, "Label": "trend"}`。在运行/回测时它被转换为cTrader`params.cbotset`文件（`{ "Parameters": { … } }`）。您可以从cBot的**Parameter sets**对话框或从Run对话框内联创建/编辑集合为原始JSON。

每个参数集**属于一个cBot**：New Parameter Set对话框列出您的所有cBots，您**必须选择一个**——直到选择cBot创建才被解除阻止。一个集合的**名称对每个cBot唯一**：创建或重命名一个集合为同一cBot的另一集合已使用的名称会被拒绝（对话框中显示清晰错误，API中为`409 Conflict`）。相同的名称可被重用于**不同的**cBot。

JSON被**验证**保存：它必须是一个单一扁平对象，其值都是标量（字符串/数字/布尔）。非对象根、数组、嵌套对象、`null`值或格式错误的JSON被拒绝（对话框中显示清晰错误，API中为`400 Bad Request`）。空对象`{}`被允许并意味着"无覆盖"。

## cTrader Console CLI备注

回测需要`--data-mode`（默认`m1`）、日期格式为`dd/MM/yyyy HH:mm`，以及
`params.cbotset` JSON位置参数；`run`拒绝`--data-dir`（仅回测）。参见`ContainerCommandHelpers`。

## 节点与扩展

执行容量通过添加节点代理扩展（自注册+心跳）。参见[节点发现](../operations/node-discovery.md)和[扩展](../deployment/scaling.md)。

## 需要交易账户

运行或回测cBot需要一个cTrader交易账户连接到。直到您在**Trading accounts**下添加一个，**Run New cBot**/
**Backtest New cBot**按钮被禁用（带提示文本）且页面显示一个提示链接至账户设置——您不再会看到来自没有账户的Bot的原始`stream connect failed`错误。
