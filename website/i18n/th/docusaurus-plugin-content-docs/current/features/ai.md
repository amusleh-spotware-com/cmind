---
description: "cMind AI เป็น provider-agnostic — Anthropic OpenAI Azure OpenAI Google Gemini และ OpenAI-compatible endpoint ใด ๆ รวมถึง local models (Ollama LM Studio vLLM) เลือก provider model และ endpoint; ทุก ๆ AI feature ทำงาน unchanged"
---

# AI features

cMind AI layer เป็น **provider-agnostic** ทุก ๆ feature พูดเป็น single provider-neutral seam (`IAiClient.CompleteAsync`); a **routing client** resolves active provider credential และ dispatches ไปยัง matching wire adapter คุณ เลือก provider + model + endpoint (และ ถ้า provider ต้อง มัน key); ทุก ๆ existing feature ทำงาน unchanged ด้วย same gating encryption resilience และ degradation

**Batteries included:** a **built-in local LLM ships ด้วย app และ enabled โดย default** (Microsoft.ML.OnnxRuntimeGenAI e.g. Phi-3-mini) — ดังนั้นทุก ๆ deployment มี working AI **ด้วย no API key และ no external service** white-label deployment สามารถ remove มัน และ restrict ผู้ให้บริการใด users อาจ add Beyond built-in connect any external provider

Supported providers:

- **Built-in local AI** (`BuiltInOnnx`) — in-process ONNX GenAI model no key shipped + default-on
- **Anthropic** (Claude — Messages API)
- **OpenAI** และ **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Any OpenAI-compatible endpoint** รวมถึง **local models** (Ollama LM Studio vLLM llama.cpp `server` LocalAI) และ OpenAI-compatible clouds (OpenRouter Groq Together Mistral DeepSeek) — ทั้งหมด ผ่าน one OpenAI-compatible adapter differing เพียง โดย base URL + model + key

Exactly **one** provider เป็น active ที่ time เดียว Credentials stored **encrypted** (`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector` `EncryptionPurposes.AiApiKey`); local endpoint ต้องการ **no key** ด้วย **no** active provider ทุก ๆ feature ส่งกลับ disabled result และ rest ของ app วิ่ง unchanged (no key ต้อง build test หรือ run platform)

**Back-compat:** existing deployment legacy `App:Ai:ApiKey` (หรือ old encrypted `ai.api_key` setting) honored automatically เป็น default active **Anthropic** provider — zero action ต้อง

AI unconfigured → AI pages dim actions และ show banner บวก one-time prompt เพื่อ add provider ใน **Settings → AI** (`AiFeatureNotice`) Status ที่ `GET /api/ai/status` (`{ enabled kind model }`); providers managed (owner-only) ผ่าน `GET/PUT /api/ai/providers` `POST /api/ai/providers/{id}/activate` `DELETE /api/ai/providers/{id}` และ a `POST /api/ai/providers/test` connectivity ping

## Deployment default vs user ของคุณเอง provider

AI credentials มี two scopes:

- **Deployment default (owner-managed)** owner กำหนด provider (หรือ ships หนึ่ง ผ่าน `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`) มันกลาย **shared default สำหรับทุก ๆ user** — ดังนั้น broker หรือ hosting provider สามารถ fund AI สำหรับ ทั้งหมด users ของพวกเขา ด้วย **no per-user setup และ no per-user limit** Managed ผ่าน owner-only `/api/ai/providers` routes ด้านบน
- **A user ของคุณเอง provider (self-service)** any signed-in user อาจ add ของพวกเขาเอง provider ภายใต้ `GET/PUT /api/ai/my-providers` `POST /api/ai/my-providers/{id}/activate` `DELETE /api/ai/my-providers/{id}` เมื่อ present their **own active provider overrides deployment default สำหรับ their AI features**; removing มัน falls back ไปยัง default

**Resolution order** (ใน `AiProviderStore` per request user): user ของคุณเอง active credential → deployment default → legacy config key → none (AI disabled) exactly one credential เป็น active **per scope** (partial unique index per `OwnerUserId`) และ scope แต่ละ resolved independently ดังนั้น user activating their own key ไม่เคย disturbs shared default Background/non-Web contexts (no request user) always resolve deployment default

## Provider capability matrix

Capabilities default per provider และ owner-overridable เมื่อ capability off feature **degrades never throws**: web search silently dropped; vision ส่งกลับ typed capability-unsupported failure

| Provider | Kind | Default base URL | Key required | Web search | Vision | Notes |
|---|---|---|---|---|---|---|
| Built-in local AI | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | shipped ONNX GenAI model default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent` `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | via OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | via OpenAI-compatible adapter |

Full per-provider setup guides (keys URLs model ids UI steps): ดู [AI providers — setup catalog](../deployment/ai-providers.md)

## Built-in local AI (shipped default-on)

cMind ships **real local LLM ที่ run in-process** ผ่าน [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (compact instruct model เช่น Phi-3-mini) มัน ต้องการ **no API key และ no external service** และ on first startup — เมื่อ no provider configured และ white-label gate allows มัน — มัน **seeded และ activated automatically** ดังนั้นทุก ๆ deployment มี working AI out ของ box

- model directory (`genai_config.json` + tokenizer + weights) configured โดย `App:Ai:BuiltIn:ModelPath` (default `models/onnx` relative ไปยัง app base directory) เมื่อ model files absent provider **degrades ไปยัง typed failure ด้วย install hint** — มัน ไม่เคย throws และ rest ของ app unaffected
- มัน powers ทุก ๆ text AI feature being compact model มัน text-only (no server-side web search หรือ vision) และ generation serialised (one model instance reused หลัง lazy load)
- Acquire/bundle model: ดู [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)

## White-label controls

white-label deployment restricts AI ผ่าน `App:Branding` (enforced server-side บน ทุก ๆ provider upsert):

- `AllowBuiltInAi` (default `true`) — ตั้ง `false` ไปยัง **remove built-in model** ทั้งหมด
- `AllowLocalProviders` (default `true`) — ตั้ง `false` เพื่อ forbid local/self-hosted endpoints (loopback / private OpenAI-compatible e.g. Ollama/LM Studio/vLLM)
- `AllowedAiProviderKinds` (default empty = ทั้งหมด) — list เพียง kinds deployment sanctions (e.g. `["Anthropic","OpenAiCompatible"]`) เพื่อ lock down ผู้ให้บริการใด users อาจ add

## Extending: future built-in models

AI layer เป็น **adapter-based และ built เพื่อ grow** ทุก ๆ provider เป็น `IAiProvider` selected โดย `AiProviderKind`; feature-facing seam (`IAiClient`/`AiFeatureService`) ไม่เคย เปลี่ยน เพิ่ม new built-in model runtime later (another ONNX model different in-process engine GGUF/llama.cpp in-proc etc.) เป็น localized เปลี่ยน: เพิ่ม `AiProviderKind` implement one `IAiProvider` adapter register มัน และ (optionally) wire default seeding + dialog option — no feature endpoint หรือ MCP tool changes built-in ONNX provider เป็น reference implementation ของ pattern นี้

## Capabilities

- **Build cBot** — plain-English prompt → runnable cBot ผ่าน **generate → build → AI-fix** self-repair loop (`build-strategy`) ที่ `/ai/build`
- **Parameter optimization** — closed loop: AI proposes param sets ทุก ๆ persisted + backtested ข้ามบน nodes (`optimize-run` / `optimize-params`)
- **Autonomous portfolio agent** — mandate-driven proposals ด้วย full decision journal (`AgentMandate` → `AgentProposal`)
- **Acting risk guard** — `AiRiskGuard` background service assesses running bots สามารถ **auto-stop** บน critical risk (opt-in)
- **Prop-firm exposure guardian** — drawdown/exposure limits ด้วย auto-flatten
- **Market alerts** — `AlertRule` engine ด้วย AI sentiment (web-search grounded where provider supports มัน)
- **Analysis** — cBot review backtest analysis post-mortems market sentiment chart-vision design marketplace curation

## Surfaces

- Web endpoints ภายใต้ `/api/ai/*` (build-strategy generate-project review analyze-backtest optimize-params optimize-run post-mortem sentiment vision curate …)
- MCP tools (`AiTools`) สำหรับ AI clients — ดู [mcp.md](mcp.md) Provider selection transparent ไปยัง MCP clients
- **AI** nav group — one Blazor **page per feature**: Build cBot (`/ai/build`) Review (`/ai/review`) Debate (`/ai/debate`) Market Sentiment (`/ai/sentiment`) Exposure Check (`/ai/exposure`) Portfolio Digest (`/ai/digest`) Tune Advisor (`/ai/tune`) Optimize (`/ai/optimize`) บวก Portfolio Agent Alerts MCP Keys Pages share `AiFeaturePageBase` + `AiOutputPanel`; ทุก ๆ อัน shows `AiFeatureNotice` เมื่อ no provider configured
- **Settings → AI** (`/settings/ai` owner-only) — provider list ด้วย **Add / edit provider dialog** (kind base URL ด้วย per-kind hints incl. Ollama/LM Studio localhost preset model optional key capability toggles "set active") และ a **Test connection** button

## Configuration

`App:Ai` supports ทั้ง legacy single key และ multi-provider seeding:

- Legacy: `ApiKey` `Model` (default `claude-opus-4-8`) `BaseUrl` `MaxTokens` — still honored เป็น default Anthropic provider
- Multi-provider: `ActiveProvider` (kind) และ `Providers[]` (`{ Kind BaseUrl Model ApiKey? MaxTokens? Capabilities? }`) — imported เป็น store บน startup ถ้า ไม่มี credentials yet ดังนั้น ops team สามารถ ship configured (incl. local-LLM) deployment purely ผ่าน appsettings/env

`RiskGuardEnabled` `RiskGuardAutoStop` `RiskGuardInterval` unchanged สำหรับ tests/dev config key อยู่ใน unified [dev-credentials file](../testing/dev-credentials.md) ภายใต้ `Ai`

## Reliability

provider ถูก treated เป็น unreliable — nothing มัน ทำ สามารถ take app ลง นี่ hold identically สำหรับ cloud และ local endpoints (dead Ollama retries แล้ว degrades exactly เหมือน throttled Anthropic):

- **Graceful degradation** ทุก ๆ failure mode (no provider HTTP 4xx/5xx/429 timeout malformed body empty content unsupported capability) ส่งกลับ typed `AiResult.Fail(reason)` — client ไม่เคย throws เป็น page MCP tool หรือ hosted service
- **Resilience pipeline** `AddAiHttpClient` ให้ one shared AI `HttpClient` bounded retry บน transient 5xx / network failures (exponential backoff + jitter) บวก generous per-attempt และ total timeouts (`AiHttp`) reused โดย ทุก ๆ adapter

## Testing ด้วย fake local LLM

AI layer proven end-to-end **โดยไม่มี external dependency** โดย `FakeLocalLlmServer` — tiny in-process **OpenAI-compatible** endpoint ส่งกลับ deterministic canned reply wire-identical ไปยัง Ollama/LM Studio/vLLM มันรองรับ:

- **Unit** — per-adapter request-translation + response-parse tests routing/capability degradation
- **Integration** — OpenAI-compatible adapter end-to-end parametrized resilience theory ข้ามบน ทุก ๆ adapter และ **MCP AI tools**
- **E2E** — `AiLocalFixture` boots app pointed ที่ fake server (หรือ **real** provider เมื่อ developer sets `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — real creds win) และ drives ทุก ๆ AI feature ผ่าน real UI เพิ่มหรือเปลี่ยน any AI feature **requires** E2E test ผ่าน fixture นี้ (ดู repo test mandate) An opt-in lane (`AI_LOCAL_LLM=1`) runs one real completion ผ่าน **Ollama** Testcontainer
