---
description: "每个cMind支持的AI提供商的设置目录——Anthropic、OpenAI、Azure OpenAI、Google Gemini和每个OpenAI兼容的终点，包括本地模型（Ollama、LM Studio、vLLM、llama.cpp、LocalAI）和OpenAI兼容的云。"
---

# AI提供商——设置目录

cMind的AI层是提供商无关的（请参阅[AI功能](../features/ai.md)）。通过两种方式配置提供商：

1. **UI（所有者）：** Settings → AI → **添加提供商** → 选择类型、基本URL、模型、密钥（本地可选）、能力切换、**设置活跃** → **测试连接**。
2. **配置/env（ops）：** 种子`App:Ai:Providers[]`和`App:Ai:ActiveProvider`——在首次启动时导入到存储中，当不存在凭证时。示例（env，提供商索引`0`）：

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (对于无密钥的本地终点省略)
   ```

正好一个提供商在任何时候处于活跃状态。密钥被加密存储；本地终点不需要任何。

## 安全性：http vs https

纯文本`http://`**仅**被接受用于环回/私有（内网）主机——本地LLM情况
（Ollama、LM Studio、vLLM、本地部署盒）。任何在公共互联网上可路由的主机**必须**是
`https://`，所以API密钥永远不会以明文发送。空网络/本地部署：指向您的
内部终点（环回或私有IP）的基本URL，如果运行时未认证，则将密钥留空。

## 内置本地AI（ONNX，已发送）

cMind发送一个**真实的进程内本地LLM**（Microsoft.ML.OnnxRuntimeGenAI），**默认启用**——无密钥，无外部服务。在首次启动时，当没有配置提供商且
`App:Branding:AllowBuiltInAi`是`true`时，它被种子化并自动激活。

- **配置：** `App:Ai:BuiltIn:Enabled`（默认`true`）、`App:Ai:BuiltIn:ModelPath`（默认
  `models/onnx`，相对于应用程序基本目录）、`App:Ai:BuiltIn:MaxTokens`（默认`1024`）。
- **模型文件：** 指向`ModelPath`到包含ONNX GenAI模型的目录——`genai_config.json`、
  分词器和`.onnx`权重。CPU **Phi-3.5-mini-instruct**构建工作良好，例如：

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3.5-mini-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-awq-block-128-acc-level-4/* \
    --local-dir ./models
  # 然后将App:Ai:BuiltIn:ModelPath设置到该文件夹（包含genai_config.json）
  ```

  将文件夹与您的部署镜像/Helm卷捆绑，或在运行时挂载它。当文件
  不存在时，内置降级为清晰的"模型未安装"消息——应用仍然运行；配置
  另一个提供商或安装模型。
- **GPU：** 用CUDA/DirectML ONNX GenAI构建交换CPU包/模型；代码路径不变。

## 白标签：限制AI

在`App:Branding`下设置（服务器端强制执行——禁止的upsert返回`400`）：

- `AllowBuiltInAi: false`——完全移除已发送的内置模型。
- `AllowLocalProviders: false`——禁止本地/自托管终点（Ollama/LM Studio/vLLM和任何
  环回/私有OpenAI兼容URL）。
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]`——仅允许这些类型（空=全部）。

## 用未来的内置模型扩展

提供商层是基于适配器的（`IAiProvider`由`AiProviderKind`键控），所以未来内置模型
运行时可以添加而不触及任何AI功能：添加一种类型、实现一个适配器、注册它。
ONNX内置是参考实现。请参阅[AI功能→扩展](../features/ai.md#extending-future-built-in-models)。

## 云提供商

### Anthropic (Claude)

- 密钥：[console.anthropic.com](https://console.anthropic.com/) → API keys。
- 基本URL：`https://api.anthropic.com/` · 模型：例如`claude-opus-4-8`。
- 能力：网络搜索+视觉默认启用。

### OpenAI

- 密钥：[platform.openai.com/api-keys](https://platform.openai.com/api-keys)。
- 基本URL：`https://api.openai.com/v1/` · 模型：例如`gpt-4o`。
- 类型：**OpenAiCompatible**。如果使用视觉模型，在对话框中启用视觉。

### Azure OpenAI

- 密钥+终点：Azure门户→您的Azure OpenAI资源。
- 基本URL：`https://<resource>.openai.azure.com/` · 模型：您的**部署名称**。
- 类型：**AzureOpenAi**（使用`api-key`头+`api-version`查询和部署路径）。

### Google Gemini

- 密钥：[aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey)。
- 基本URL：`https://generativelanguage.googleapis.com/` · 模型：例如`gemini-2.0-flash`。
- 类型：**Gemini**。网络搜索接地+视觉默认启用。

### 其他OpenAI兼容的云（OpenRouter、Groq、Together、Mistral、DeepSeek）

- 类型：**OpenAiCompatible**。基本URL=提供商的OpenAI兼容终点，模型=其模型ID，
  ApiKey=提供商密钥。不需要cMind改变——一个适配器为所有提供服务。

## 本地模型（无密钥）

所有本地运行时都暴露OpenAI Chat Completions线路，所以使用**Kind: OpenAiCompatible**与运行时的
基本URL和提供的模型名称；将密钥留空。

### Ollama

```
# 从https://ollama.com安装，然后：
ollama pull llama3.1:8b
```

- 基本URL：`http://localhost:11434/v1/` · 模型：拉取的名称（例如`llama3.1:8b`、`qwen2.5-coder`）。
- 无API密钥。能力默认为仅文本；仅对视觉模型启用视觉。

### LM Studio

- 启动本地服务器（开发者→启动服务器）。
- 基本URL：`http://localhost:1234/v1/` · 模型：加载的模型ID。无API密钥。

### vLLM / llama.cpp `server` / LocalAI

- 服务一个OpenAI兼容的终点（每个都发送一个）。
- 基本URL：您的提供的URL（例如`http://localhost:8000/v1/`）· 模型：提供的模型名称。无密钥
  除非您在前面放置认证。

## 验证

- 对话框中的**测试连接**运行一个小的ping完成并报告成功+延迟——对于
  确认本地终点理想。
- 自动化：应用的E2E套件默认针对进程内假OpenAI兼容
  服务器驱动每个AI功能，或当设置`AI_E2E_BASEURL`（+可选`AI_E2E_API_KEY`/
  `AI_E2E_KIND`/`AI_E2E_MODEL`）时的您的真实提供商。请参阅[AI功能→测试](../features/ai.md#testing-with-the-fake-local-llm)。

## 切换/旋转

- **切换活跃提供商：** Settings → AI → 另一卡上**设置活跃**（激活一个停用其余）。
- **旋转密钥：** 编辑提供商并提供新密钥（留空以保持存储的）。
- **移除：** 删除卡。没有活跃提供商时，AI功能禁用，应用的其余部分
  保持不变。
