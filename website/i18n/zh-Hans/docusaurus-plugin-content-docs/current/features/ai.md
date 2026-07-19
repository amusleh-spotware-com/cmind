---
description: "cMind AI是提供商无关的——Anthropic、OpenAI、Azure OpenAI、Google Gemini和任何OpenAI兼容的终点，包括本地模型（Ollama、LM Studio、vLLM）。选择提供商、模型和终点；每个AI功能的工作方式不变。"
---

# AI功能

cMind的AI层是**提供商无关的**。每个功能通过单个提供商中立的接缝（`IAiClient.CompleteAsync`）进行通信；一个**路由客户端**解析活跃的提供商凭证并分配到匹配的线路适配器。您选择提供商+模型+终点（如果提供商需要，再加上密钥）；每个现有功能都使用相同的网关、加密、恢复能力和降级功能而不改变工作方式。

**电池已包含：** 一个**内置本地LLM随应用程序一起发送并默认启用**（Microsoft.ML.OnnxRuntimeGenAI，例如Phi-3-mini）——所以每个部署都有可用的AI，**无需API密钥，无需外部服务**。白标签部署可以移除它并限制用户可以添加哪些提供商。除了内置的，连接任何外部提供商。

支持的提供商：

- **内置本地AI**（`BuiltInOnnx`）——进程内ONNX GenAI模型，无密钥，已发送+默认启用。
- **Anthropic**（Claude——Messages API）
- **OpenAI**和**Azure OpenAI**（Chat Completions）
- **Google Gemini**（`generateContent`）
- **任何OpenAI兼容的终点**，包括**本地模型**（Ollama、LM Studio、vLLM、
  llama.cpp `server`、LocalAI）和OpenAI兼容的云（**Kimi / Moonshot**位于
  `https://api.moonshot.ai/v1/`、OpenRouter、Groq、Together、Mistral、DeepSeek）——全部通过一个
  OpenAI兼容的适配器，仅在基本URL+模型+密钥上有所不同。添加提供商对话框提供
  单击**预设**（Kimi、OpenAI、OpenRouter、Groq、DeepSeek、Mistral、Ollama、LM Studio），
  用基本URL+示例模型填充。

正好**一个**提供商在任何时候处于活跃状态。凭证**已加密**存储
（`AiProviderCredential`聚合+`IAiProviderStore`+`ISecretProtector`，`EncryptionPurposes.AiApiKey`）；
本地终点**不需要**密钥。没有**活跃的**提供商时，每个功能都返回禁用结果，应用程序的其余部分保持不变（不需要密钥来构建、测试或运行平台）。

**向后兼容：** 现有部署的传统`App:Ai:ApiKey`（或旧的加密`ai.api_key`
设置）自动被识别为默认活跃的**Anthropic**提供商——无需任何操作。

AI未配置→AI页面将操作变暗并显示横幅加一次性提示以在
**Settings → AI**（`AiFeatureNotice`）中添加提供商。状态在`GET /api/ai/status`（`{ enabled, kind, model }`）；
提供商通过`GET/PUT /api/ai/providers`、`POST /api/ai/providers/{id}/activate`、
`DELETE /api/ai/providers/{id}`和`POST /api/ai/providers/test`连接性检查进行管理（仅所有者）。

## 部署默认值与用户自己的提供商

AI凭证有两个范围：

- **部署默认值（所有者管理）。** 所有者配置提供商（或通过
  `App:Ai:Providers[]`/传统的`App:Ai:ApiKey`发送）。它成为**所有用户的共享默认值**——
  所以经纪商或托管提供商可以为所有用户资助AI，**无需按用户设置，无需按用户限制**。通过上述仅所有者的`/api/ai/providers`路由进行管理。
- **用户自己的提供商（自助）。** 任何登录的用户都可以在
  `GET/PUT /api/ai/my-providers`、`POST /api/ai/my-providers/{id}/activate`、
  `DELETE /api/ai/my-providers/{id}`下添加自己的提供商。存在时，他们**自己的活跃提供商会覆盖他们自己的AI功能的部署默认值**；删除它会回退到默认值。

**解析顺序**（在`AiProviderStore`中，每个请求用户）：用户自己的活跃凭证→部署默认值→传统配置密钥→无（AI禁用）。正好一个凭证在**每个范围**内是活跃的（每个`OwnerUserId`有一个部分唯一索引），每个范围是独立解析的，所以用户激活自己的密钥不会扰乱共享默认值。后台/非Web上下文（无请求用户）始终解析部署默认值。

## 提供商能力矩阵

能力按提供商默认，所有者可覆盖。当能力关闭时，功能
**降级，永不抛出**：网络搜索被静默删除；视觉返回类型化的
不支持能力失败。

| 提供商 | 类型 | 默认基本URL | 需要密钥 | 网络搜索 | 视觉 | 备注 |
|---|---|---|---|---|---|---|
| 内置本地AI | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | shipped ONNX GenAI model, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | via OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | via OpenAI-compatible adapter |

完整的按提供商设置指南（密钥、URL、模型ID、UI步骤）：请参阅
[AI提供商——设置目录](../deployment/ai-providers.md)。

## 内置本地AI（已发送，默认启用）

cMind发送一个**真实的本地LLM，通过
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/)在进程内运行**（例如Phi-3-mini的紧凑指令模型）。它**不需要API密钥，不需要外部服务**，在首次启动时——当没有配置提供商且白标签网关允许时——它**被种子化并自动激活**，所以每个部署都有开箱即用的可用AI。

- 模型目录（`genai_config.json`+分词器+权重）由
  `App:Ai:BuiltIn:ModelPath`（默认`models/onnx`，相对于应用程序基本目录）配置。当模型
  文件不存在时，提供商**降级为带有安装提示的类型化失败**——它永不抛出，
  应用程序的其余部分不受影响。
- 它为每个文本AI功能提供支持。作为一个紧凑模型，它仅支持文本（没有服务器端网络搜索或
  视觉），生成被序列化（一个模型实例，在懒加载后重用）。
- **多个内置模型可以共存。** 每个下载的模型位于`ModelPath/<key>`下；一个精选目录（Phi-3.5-mini默认，加上Phi-3-mini-128k）可以从**Settings → AI**下载和切换。选择一个内置子模型在进程内加载它。获取/捆绑模型：请参阅[AI提供商→内置](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)。

## 白标签控制

白标签部署通过`App:Branding`限制AI（在每个提供商upsert上服务器端强制执行）：

- `AllowBuiltInAi`（默认`true`）——设置`false`以**完全移除内置模型**。
- `AllowLocalProviders`（默认`true`）——设置`false`以禁止本地/自托管终点（环回/
  私有OpenAI兼容，例如Ollama/LM Studio/vLLM）。
- `AllowedAiProviderKinds`（默认empty=all）——仅列出部署许可的类型（例如
  `["Anthropic","OpenAiCompatible"]`）以锁定用户可以添加哪些提供商。
- `AllowAiModelManagement`（默认`true`）——设置`false`以隐藏**模型浏览**、**按页面模型
  选择器**和**按功能模型绑定**。所有这些都是所有者可调的运行时间，来自**Settings →
  Deployment**（在`IOptionsMonitor`上实时叠加）并编目在`WhiteLabelCatalog`中。

## 扩展：未来的内置模型

AI层是**基于适配器且为增长而构建的**。每个提供商是由
`AiProviderKind`选择的`IAiProvider`；面向功能的接缝（`IAiClient`/`AiFeatureService`）永不改变。稍后添加新的
内置模型运行时（另一个ONNX模型、不同的进程内引擎、GGUF/llama.cpp
in-proc等）是本地化的改变：添加`AiProviderKind`、实现一个`IAiProvider`适配器、
注册它，以及（可选）线路默认种子化+对话框选项——没有功能、终点或MCP工具
改变。内置ONNX提供商是这个模式的参考实现。

## 能力

- **构建cBot**——在`/ai/build`进行基于项目的工作坊：**创建新cBot**（唯一的名称+语言）或**改进现有的**有源代码的，然后在`/ai/build/{projectId}`上与模型进行**对话**来编写和完善其代码。**每个提示和模型回复都带有时间戳进行持久化**并能在导航/重新加载后保持；模型的源代码在每一回合都应用到项目。从同一页面**构建**和**运行** cBot（或在完整编辑器中打开它）。每个项目在列表中显示其**最后更改时间**和查看/删除控制。
- **按页面模型选择**——每个AI功能页面和对话框都显示一个**模型选择器**，列出您可能使用的模型（您自己的提供商+部署默认值）。它预选功能保存的绑定（如果已设置），否则**默认**模型，您选择的模型适用于那一个操作（作为`?modelId=`发送并由`RoutingAiClient`对该调用强制执行）。当部署禁用模型管理时隐藏。
- **浏览并选择模型，按功能**——浏览提供商终点宣传的模型（LM Studio/Ollama/vLLM/llama.cpp上的`GET /v1/models`或内置目录），而不是手动输入ID，并**将每个AI功能绑定到不同的模型**，所以几个模型同时为不同的功能服务（未绑定的功能回退到范围的默认提供商）。
- **参数优化**——闭环：AI提出参数集，每个持久化+在节点间进行回测（`optimize-run`/`optimize-params`）。
- **自主投资组合代理**——命令驱动的提议和完整的决策日志（`AgentMandate`→`AgentProposal`）。
- **行为风险警卫**——`AiRiskGuard`后台服务评估运行的机器人，可以在关键风险时**自动停止**（可选）。
- **Prop公司风险敞口监管人**——抽取/敞口限制和自动平仓。
- **市场警报**——`AlertRule`引擎和AI情绪（在提供商支持的地方以网络搜索为基础）。
- **分析**——cBot审查、回测分析、事后分析、市场情绪、图表视觉设计、市场精选。

## 表面

- Web终点在`/api/ai/*`下（AI Build聊天`build/{id}/prompt`+`build/{id}/messages`、生成项目、审查、分析回测、优化参数、优化运行、事后分析、情绪、视觉、精选等）。每个功能终点接受可选的`?modelId=<credential>`以在选定的模型上运行该一个调用。加上**模型发现**（`/api/ai/models/probe`、`/api/ai/usable-models`）和**按功能绑定**（`/api/ai/feature-bindings`、`/api/ai/my-feature-bindings`）。cBot项目、构建和运行重复使用构建器终点（`/api/builder/projects…`）。
- MCP工具（`AiTools`）适用于AI客户端——请参阅[mcp.md](mcp.md)。提供商选择对MCP客户端是透明的。
- **AI**导航组——每个功能一个Blazor**页面**：构建cBot（`/ai/build`）、审查（`/ai/review`）、辩论（`/ai/debate`）、市场情绪（`/ai/sentiment`）、敞口检查（`/ai/exposure`）、投资组合摘要（`/ai/digest`）、调优顾问（`/ai/tune`）、优化（`/ai/optimize`），加上投资组合代理、警报、MCP密钥。页面共享`AiFeaturePageBase`+`AiOutputPanel`+一个`AiModelSelect`；当没有配置提供商时每个都显示`AiFeatureNotice`。
- **Settings → AI**（`/settings/ai`，仅所有者）——提供商列表和**添加/编辑提供商对话框**（类型、带有按类型提示和单击**预设**（Kimi/Moonshot、Ollama、LM Studio）的基本URL，模型、可选密钥、能力切换、"设置为默认值"）和**测试连接**按钮。

## 配置

`App:Ai`支持传统单密钥和多提供商种子化：

- 传统：`ApiKey`、`Model`（默认`claude-opus-4-8`）、`BaseUrl`、`MaxTokens`——仍然被识别为
  默认Anthropic提供商。
- 多提供商：`ActiveProvider`（类型）和`Providers[]`（`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`）——在启动时导入到存储中，如果还没有凭证存在，所以
  ops团队可以通过appsettings/env完全发送配置的（包括本地LLM）部署。

`RiskGuardEnabled`、`RiskGuardAutoStop`、`RiskGuardInterval`不变。对于测试/开发，配置密钥
位于统一的[开发凭证文件](../testing/dev-credentials.md)下的`Ai`。

## 可靠性

提供商被视为不可靠——它做的任何事情都不能让应用下线。这对云和本地终点完全成立（死的Ollama重试然后降级完全像被限制的Anthropic）：

- **优雅降级。** 每个失败模式（无提供商、HTTP 4xx/5xx/429、超时、格式错误的主体、
  空内容、不支持的能力）返回类型化的`AiResult.Fail(reason)`——客户端永远不会抛出到页面、MCP工具或托管服务中。
- **恢复力管道。** `AddAiHttpClient`给一个共享AI`HttpClient`一个有限的重试在
  暂时的5xx/网络失败（指数退避+抖动）加上慷慨的每次尝试和总
  超时（`AiHttp`），由每个适配器重复使用。

## 用假本地LLM测试

AI层被端到端证明了**没有任何外部依赖**通过`FakeLocalLlmServer`——一个小的
进程内**OpenAI兼容**终点返回确定性的罐头回复，线路相同于
Ollama/LM Studio/vLLM。它支持：

- **单元**——每适配器请求翻译+响应解析测试、路由/能力降级。
- **集成**——OpenAI兼容适配器端到端、参数化的恢复力理论跨越
  每个适配器、**MCP AI工具**。
- **E2E**——`AiLocalFixture`启动指向假服务器的应用（或当
  开发者设置`AI_E2E_BASEURL`（+可选`AI_E2E_API_KEY`/`AI_E2E_KIND`/`AI_E2E_MODEL`）时的**真实**提供商——
  真实凭证赢）并通过真实UI驱动每个AI功能。添加或改变任何AI功能
  **需要**通过这个fixture的E2E测试（请参阅repo测试命令）。一个可选in lane
  (`AI_LOCAL_LLM=1`)通过**Ollama** Testcontainer运行一个真实完成。

## 内置本地AI——默认零设置

内置ONNX本地LLM开箱即用：当其模型目录不存在且
`App:Ai:BuiltIn:AutoDownload`是`true`（默认值）时，应用在
后台从`App:Ai:BuiltIn:DownloadBaseUrl`下载模型一次。在下载运行时，AI调用（以及Settings → AI中的**测试连接**）返回一个清晰的"模型正在下载（首次设置）"消息
而不是硬失败。空网络/计量部署设置`AutoDownload=false`和
预配置模型目录（`App:Ai:BuiltIn:ModelPath`）。白标签
`App:Branding:AllowBuiltInAi`网关仍然适用。

下载也**在启动时预热**当内置模型是活跃提供商时，所以它在第一个AI点击之前就准备好了，而不是用"下载中……"失败该点击。**Settings → AI**在内置提供商卡上呈现实时安装状态——*Model ready*/*Downloading model…*/*Model not installed*/*Download failed*——带有**Download model**（或**Retry download**）按钮，按需踢一次性后台获取（`GET /api/ai/built-in/status`、`POST /api/ai/built-in/install`）。从Settings启用内置提供商重复使用已经种子化的行而不是添加重复，所以它在单一活跃提供商约束上永不冲突。
