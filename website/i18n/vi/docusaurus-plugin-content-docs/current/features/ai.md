---
description: "cMind AI là provider-agnostic — Anthropic, OpenAI, Azure OpenAI, Google Gemini, và bất kỳ endpoint tương thích OpenAI nào bao gồm cả mô hình cục bộ (Ollama, LM Studio, vLLM). Chọn provider, model và endpoint; mọi tính năng AI hoạt động không thay đổi."
---

# Tính năng AI

Lớp AI của cMind là **provider-agnostic**. Mọi tính năng giao tiếp qua một seam trung lập với provider (`IAiClient.CompleteAsync`); một **routing client** giải quyết credential của provider đang active và gửi request đến adapter phù hợp. Bạn chọn provider + model + endpoint (và, nếu provider cần, một key); mọi tính năng hiện có hoạt động không thay đổi với cùng gating, mã hóa, khả năng phục hồi và degradation.

**Pin có sẵn:** một **LLM cục bộ tích hợp sẵn được ship cùng app và được bật theo mặc định** (Microsoft.ML.OnnxRuntimeGenAI, ví dụ Phi-3-mini) — vì vậy mọi deployment đều có AI hoạt động **không cần API key và không cần dịch vụ bên ngoài**. Deployment white-label có thể gỡ bỏ nó và giới hạn provider nào người dùng được thêm. Ngoài built-in, kết nối bất kỳ provider bên ngoài nào.

Các provider được hỗ trợ:

- **Built-in local AI** (`BuiltInOnnx`) — mô hình ONNX GenAI in-process, không cần key, được ship + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** và **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Bất kỳ endpoint tương thích OpenAI nào**, bao gồm **mô hình cục bộ** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) và những đám mây tương thích OpenAI (**Kimi / Moonshot** tại
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — tất cả qua một adapter
  tương thích OpenAI, chỉ khác base URL + model + key. Dialog thêm provider cung cấp **preset** một cách nhấp
  (**Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio**) để điền base URL + một sample model.

Chính xác **một** provider active tại một thời điểm. Credentials được lưu trữ **đã mã hóa**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
endpoint cục bộ cần **không có key**. Khi **không có** provider active, mọi tính năng trả về kết quả disabled
và phần còn lại của app chạy không thay đổi (không cần key để build, test hoặc chạy platform).

**Back-compat:** deployment hiện có sử dụng `App:Ai:ApiKey` legacy (hoặc setting `ai.api_key` đã mã hóa)
được tự động công nhận như provider **Anthropic** active mặc định — không cần hành động gì.

AI chưa cấu hình → trang AI mờ các hành động và hiển thị banner cùng lời nhắc một lần để thêm provider trong
**Settings → AI** (`AiFeatureNotice`). Trạng thái tại `GET /api/ai/status` (`{ enabled, kind, model }`);
provider được quản lý (chỉ owner) qua `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, và `POST /api/ai/providers/test` connectivity ping.

## Deployment mặc định vs provider riêng của người dùng

AI credentials có hai phạm vi:

- **Deployment mặc định (owner quản lý).** Owner cấu hình một provider (hoặc ship qua
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Nó trở thành **shared default cho mọi user** —
  vì vậy broker hoặc hosting provider có thể tài trợ AI cho tất cả người dùng với **không cần setup per-user và không có giới hạn per-user**. Được quản lý qua các route owner-only `/api/ai/providers` bên trên.
- **Provider riêng của người dùng (self-service).** Bất kỳ user đã đăng nhập nào có thể thêm provider của riêng họ dưới
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Khi có, **provider active riêng của họ ghi đè deployment mặc định cho AI features của chính họ**; xóa nó sẽ quay về mặc định.

**Thứ tự giải quyết** (trong `AiProviderStore`, per request user): credential active riêng của user → deployment mặc định → legacy config key → none (AI disabled). Chính xác một credential active **per scope** (một partial unique index per `OwnerUserId`), và mỗi scope được giải quyết độc lập, vì vậy user kích hoạt key riêng không bao giờ ảnh hưởng đến shared default. Background/non-Web contexts (không có request user) luôn giải quyết deployment mặc định.

## Ma trận khả năng provider

Khả năng mặc định theo provider và có thể bị owner ghi đè. Khi một khả năng tắt, tính năng
**degrades, không bao giờ throw**: web search bị drop thầm; vision trả về typed
capability-unsupported failure.

| Provider | Kind | Default base URL | Key required | Web search | Vision | Ghi chú |
|---|---|---|---|---|---|---|
| Built-in local AI | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | shipped ONNX GenAI model, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | via OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | via OpenAI-compatible adapter |

Hướng dẫn setup per-provider đầy đủ (keys, URLs, model ids, các bước UI): xem
[AI providers — setup catalog](../deployment/ai-providers.md).

## Built-in local AI (shipped, default-on)

cMind ship một **LLM cục bộ thực sự chạy in-process** qua
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (một instruct model nhỏ gọn như
Phi-3-mini). Nó **không cần API key và không cần dịch vụ bên ngoài**, và khi khởi động lần đầu — khi không có provider nào được cấu hình và white-label gate cho phép — nó **được seed và kích hoạt tự động**, vì vậy mọi deployment đều có AI hoạt động ngay từ đầu.

- Thư mục model (`genai_config.json` + tokenizer + weights) được cấu hình bởi
  `App:Ai:BuiltIn:ModelPath` (mặc định `models/onnx`, relative đến app base directory). Khi các file model
  absent, provider **degrades thành typed failure với install hint** — nó không bao giờ throw,
  và phần còn lại của app không bị ảnh hưởng.
- Nó cung cấp năng cho mọi text AI feature. Là một model nhỏ gọn, nó chỉ hỗ trợ text (không có server-side web search hoặc
  vision) và generation được serialize (một model instance, được reuse sau lazy load).
- **Nhiều built-in models có thể cùng tồn tại.** Mỗi downloaded model nằm dưới `ModelPath/<key>`; một curated catalog (Phi-3.5-mini mặc định, cộng Phi-3-mini-128k) có thể được tải về và chuyển đổi từ **Settings → AI**. Chọn một built-in submodel tải nó in-process. Acquire/bundle model: xem [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label controls

Deployment white-label giới hạn AI qua `App:Branding` (enforced server-side trên mọi provider upsert):

- `AllowBuiltInAi` (mặc định `true`) — đặt `false` để **gỡ bỏ built-in model** hoàn toàn.
- `AllowLocalProviders` (mặc định `true`) — đặt `false` để cấm local/self-hosted endpoints (loopback /
  private OpenAI-compatible, ví dụ Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (mặc định empty = all) — liệt kê chỉ các kinds mà deployment cho phép (ví dụ
  `["Anthropic","OpenAiCompatible"]`) để khóa down các provider người dùng được thêm.
- `AllowAiModelManagement` (mặc định `true`) — đặt `false` để ẩn **model browsing**, **per-page model selector**, và **per-feature model binding**. Tất cả đều có thể được owner-tunable tại runtime từ **Settings → Deployment** (overlaid live trên `IOptionsMonitor`) và catalogued trong `WhiteLabelCatalog`.

## Extending: future built-in models

Lớp AI là **adapter-based và được xây dựng để phát triển**. Mỗi provider là một `IAiProvider` được chọn bởi
`AiProviderKind`; seam hướng feature (`IAiClient`/`AiFeatureService`) không bao giờ thay đổi. Thêm một new
built-in model runtime sau này (model ONNX khác, một in-process engine khác, GGUF/llama.cpp
in-proc, v.v.) là một thay đổi localized: thêm `AiProviderKind`, implement một `IAiProvider` adapter,
register nó, và (tùy chọn) wire default seeding + một dialog option — không có thay đổi feature, endpoint, hoặc MCP tool
changes. Built-in ONNX provider là reference implementation của pattern này.

## Capabilities

- **Build cBot** — plain-English prompt → runnable cBot qua **generate → build → AI-fix** self-repair loop (`build-strategy`), tại `/ai/build`. **Mã nguồn được tạo ra được hiển thị** khi build kết thúc (với nút copy), cùng với build log (cũng có thể sao chép) — khi thành công *và* khi thất bại. **Ngay cả một build thất bại cũng được lưu vào cBots của bạn** (với tên duy nhất thực tế) và cung cấp link *Mở trong trình chỉnh sửa* để bạn có thể sửa các lỗi biên dịch và xây dựng lại, thay vì mất công việc.
- **Per-page model selection** — mọi trang tính năng AI và hộp thoại hiển thị **model selector** liệt kê các models bạn có thể sử dụng (providers của bạn + deployment defaults). Nó pre-selects binding được lưu của feature nếu được đặt, nếu không **default** model, và model bạn chọn áp dụng cho hành động đó (được gửi dưới dạng `?modelId=` và được ép buộc bởi `RoutingAiClient` cho lệnh gọi đó). Ẩn khi deployment vô hiệu hóa quản lý model.
- **Browse & select models, per feature** — browse các models mà provider endpoint advertises (`GET /v1/models` trên LM Studio / Ollama / vLLM / llama.cpp, hoặc built-in catalog) thay vì hand-typing một id, và **bind mỗi AI feature thành một model khác** vì vậy vài models phục vụ các features khác nhau cùng một lúc (một unbound feature quay về scope's default provider).
- **Parameter optimization** — closed loop: AI proposes param sets, each persisted + backtested across nodes (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — mandate-driven proposals với full decision journal (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background service đánh giá các bot đang chạy, có thể **auto-stop** on critical risk (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure limits với auto-flatten.
- **Market alerts** — `AlertRule` engine với AI sentiment (web-search grounded where provider supports it).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation.

## Surfaces

- Web endpoints dưới `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Mọi tính năng endpoint chấp nhận một tùy chọn `?modelId=<credential>` để chạy lệnh gọi đó trên một model được chọn. Cộng **model discovery** (`/api/ai/models/probe`, `/api/ai/usable-models`) và **per-feature bindings** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP tools (`AiTools`) cho AI clients — xem [mcp.md](mcp.md). Provider selection transparent đối với MCP clients.
- **AI** nav group — một Blazor **page per feature**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), cộng Portfolio Agent, Alerts, MCP Keys. Pages share `AiFeaturePageBase` + `AiOutputPanel` + một `AiModelSelect`; mỗi cái hiển thị `AiFeatureNotice` khi không có provider nào được cấu hình.
- **Settings → AI** (`/settings/ai`, owner-only) — provider list với **Add / edit provider dialog** (kind, base URL với per-kind hints và **preset** một cách nhấp bao gồm **Kimi/Moonshot**, Ollama và LM Studio, model, optional key, capability toggles, "set as default") và **Test connection** button.

## Configuration

`App:Ai` hỗ trợ cả legacy single key và multi-provider seeding:

- Legacy: `ApiKey`, `Model` (mặc định `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — vẫn được công nhận như
  default Anthropic provider.
- Multi-provider: `ActiveProvider` (kind) và `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — imported vào store khi startup nếu chưa có credentials, vì vậy
  ops team có thể ship một deployment đã cấu hình (incl. local-LLM) hoàn toàn qua appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` không thay đổi. Cho tests/dev, một config key
nằm trong [dev-credentials file](../testing/dev-credentials.md) dưới `Ai`.

## Reliability

Provider được coi là unreliable — không có gì nó làm có thể làm app down. Điều này giữ nguyên
cho cả cloud và local endpoints (một Ollama chết retry rồi degrade chính xác như một Anthropic bị throttle):

- **Graceful degradation.** Mọi failure mode (no provider, HTTP 4xx/5xx/429, timeout, malformed body,
  empty content, unsupported capability) trả về typed `AiResult.Fail(reason)` — client không bao giờ throw vào page, MCP tool, hoặc hosted service.
- **Resilience pipeline.** `AddAiHttpClient` cung cấp cho one shared AI `HttpClient` một bounded retry on
  transient 5xx / network failures (exponential backoff + jitter) cộng generous per-attempt và total
  timeouts (`AiHttp`), được reuse bởi mọi adapter.

## Testing với fake local LLM

Lớp AI được chứng minh end-to-end **không có dependency bên ngoài** bởi `FakeLocalLlmServer` — một tiny
in-process **OpenAI-compatible** endpoint trả về deterministic canned reply, wire-identical đến
Ollama/LM Studio/vLLM. Nó backing:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation.
- **Integration** — OpenAI-compatible adapter end-to-end, the parametrized resilience theory across
  mọi adapter, và **MCP AI tools**.
- **E2E** — `AiLocalFixture` boots app pointed at fake server (hoặc **real** provider khi
  developer set `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  real creds win) và drives mọi AI feature qua real UI. Thêm hoặc thay đổi bất kỳ AI feature nào
  **yêu cầu** một E2E test qua fixture này (xem repo test mandate). Một opt-in lane
  (`AI_LOCAL_LLM=1`) chạy một real completion qua **Ollama** Testcontainer.

## Built-in local AI — zero-setup by default

LLM local ONNX tích hợp hoạt động ngay out of the box: khi thư mục model của nó absent và
`App:Ai:BuiltIn:AutoDownload` là `true` (mặc định), app tải model một lần trong nền từ `App:Ai:BuiltIn:DownloadBaseUrl`. Trong khi download chạy, các lệnh gọi AI (và **Test connection** trong Settings → AI) trả về một thông báo rõ ràng "model đang tải (first-time setup)" chứ không phải là một failure cứng. Các deployment air-gapped/metered đặt `AutoDownload=false` và pre-provision thư mục model (`App:Ai:BuiltIn:ModelPath`). Cổng white-label `App:Branding:AllowBuiltInAi` vẫn áp dụng.

Lệnh download cũng **được pre-warmed khi startup** khi built-in model là active provider, vì vậy nó sẵn sàng trước khi nhấp AI đầu tiên thay vì bị fail với "downloading…". **Settings → AI** hiển thị live install state trên built-in provider card — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — với nút **Download model** (hoặc **Retry download**) kích động fetch background one-time theo yêu cầu (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Bật built-in provider từ Settings tái sử dụng hàng được seed sẵn thay vì thêm một bản sao, vì vậy nó không bao giờ xung đột trên ràng buộc single-active-provider.
