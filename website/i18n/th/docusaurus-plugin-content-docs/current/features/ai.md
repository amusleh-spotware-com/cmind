---
description: "cMind AI รองรับทุก provider — Anthropic, OpenAI, Azure OpenAI, Google Gemini และ endpoint ที่เข้ากันได้กับ OpenAI รวมถึงโมเดลในพื้นที่ (Ollama, LM Studio, vLLM) เลือก provider, model และ endpoint แต่ทุก AI feature ทำงานเหมือนเดิม"
---

# AI features

AI layer ของ cMind เป็น **provider-agnostic** ทุก feature พูดคุยกับ seam ที่เป็นกลางต่อ provider
(`IAiClient.CompleteAsync`) **routing client** resolve credential ของ active provider และ dispatch
ไปยัง wire adapter ที่ตรงกัน คุณเลือก provider + model + endpoint (และถ้าต้องการ key) ทุก existing
feature ทำงานเหมือนเดิมโดยไม่เปลี่ยนแปลง พร้อม gating, encryption, resilience และ degradation เหมือนกัน

**Batteries included:** **built-in local LLM มาพร้อม app และเปิดใช้งานโดย default**
(Microsoft.ML.OnnxRuntimeGenAI, เช่น Phi-3-mini) — ดังนั้นทุก deployment มี AI ที่ใช้งานได้
**ไม่ต้องมี API key และไม่มีบริการภายนอก** white-label deployment สามารถเอาออกได้และจำกัดว่า users
สามารถเพิ่ม provider อะไรได้ นอกเหนือจาก built-in แล้ว เชื่อมต่อ provider ภายนอกใดก็ได้

Provider ที่รองรับ:

- **Built-in local AI** (`BuiltInOnnx`) — โมเดล ONNX GenAI in-process ไม่ต้องมี key มาพร้อมและเปิด default
- **Anthropic** (Claude — Messages API)
- **OpenAI** และ **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Endpoint ที่เข้ากันได้กับ OpenAI ใดก็ได้** รวมถึง **local models** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) และ cloud ที่เข้ากันได้กับ OpenAI (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — ทั้งหมดผ่าน OpenAI-compatible adapter เดียว แตกต่างกันที่ base URL + model + key เท่านั้น

**หนึ่ง** provider เท่านั้นที่ active ได้ในเวลาเดียว Credentials ถูกเก็บ **แบบ encrypted**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`,
`EncryptionPurposes.AiApiKey`) local endpoint ไม่ต้องการ **key** เมื่อ **ไม่มี** active provider
ทุก feature คืนค่า disabled result และ app ที่เหลือทำงานเหมือนเดิม (ไม่ต้องมี key เพื่อ build, test หรือ run แพลตฟอร์ม)

**Back-compat:** deployment ที่มีอยู่แล้วมี legacy `App:Ai:ApiKey` (หรือ setting แบบ encrypted `ai.api_key`)
ถูกยอมรับโดยอัตโนมัติเป็น default active **Anthropic** provider — ไม่ต้องทำอะไร

AI ไม่ได้ configure → AI pages ทำให้ actions หม่นหมองและแสดง banner พร้อม prompt ครั้งเดียวเพื่อเพิ่ม
provider ใน **Settings → AI** (`AiFeatureNotice`) สถานะที่ `GET /api/ai/status`
(`{ enabled, kind, model }`) providers จัดการ (เจ้าของเท่านั้น) ผ่าน `GET/PUT /api/ai/providers`,
`POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}` และ
`POST /api/ai/providers/{id}/test` connectivity ping

## Deployment default vs provider ของ user เอง

AI credentials มีสองขอบเขต:

- **Deployment default (เจ้าของจัดการ).** เจ้าของ configure provider (หรือ ship ผ่าน
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`) มันกลายเป็น **shared default สำหรับทุก user** —
  broker หรือ hosting provider สามารถ fund AI สำหรับทุก user ได้ **ไม่ต้องมี per-user setup และไม่มี
  per-user limit** จัดการผ่าน owner-only `/api/ai/providers` routes ข้างต้น
- **Provider ของ user เอง (self-service).** user ที่ sign-in ใดก็ได้อาจเพิ่ม provider ของตัวเองภายใต้
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}` เมื่อมี provider active ของตัวเอง **มันจะ override deployment
  default สำหรับ AI features ของ user เอง** การเอาออกจะ fall back ไปที่ default

**Resolution order** (ใน `AiProviderStore`, ต่อ request user): user's own active credential →
deployment default → legacy config key → none (AI disabled) credential active **หนึ่งเดียว**
**ต่อ scope** (partial unique index ต่อ `OwnerUserId`) แต่ละ scope resolve โดยอิสระ
ดังนั้น user ที่ activate key ของตัวเองไม่เคยทำให้ shared default ถูกรบกวน
Background/non-Web contexts (ไม่มี request user) จะ resolve deployment default เสมอ

## Provider capability matrix

Capabilities default ต่อ provider และ owner สามารถ override ได้ เมื่อ capability ถูกปิด feature
**degrades ไม่เคย throws**: web search จะถูก drop โดยเงียบ vision คืนค่า typed
capability-unsupported failure

| Provider | Kind | Default base URL | ต้องการ Key | Web search | Vision | หมายเหตุ |
|---|---|---|---|---|---|---|
| Built-in local AI | `BuiltInOnnx` | n/a (in-process) | ไม่ | ✖ | ✖ | ONNX GenAI model ที่มาพร้อม, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | ใช่ | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | ใช่ | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | ใช่ | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | ใช่ | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | ไม่ | ✖ | model-dependent | ผ่าน OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | ไม่ | model-dependent | model-dependent | ผ่าน OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | ไม่ | ✖ | model-dependent | ผ่าน OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | ใช่ | ✖ | model-dependent | ผ่าน OpenAI-compatible adapter |

คู่มือ setup ต่อ provider เต็มรูปแบบ (keys, URLs, model ids, ขั้นตอน UI): ดู
[AI providers — setup catalog](../deployment/ai-providers.md)

## Built-in local AI (มาพร้อม, default-on)

cMind มาพร้อม **real local LLM ที่ทำงาน in-process** ผ่าน
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (compact instruct model เช่น
Phi-3-mini) ไม่ต้องการ **API key และไม่มีบริการภายนอก** และเมื่อ first startup — เมื่อไม่มี provider
 configure และ white-label gate อนุญาต — มัน **ถูก seeded และ activate โดยอัตโนมัติ** ดังนั้นทุก
deployment มี AI ที่ใช้งานได้ตั้งแต่เริ่มต้น

- ไดเรกทอรีโมเดล (`genai_config.json` + tokenizer + weights) configure โดย
  `App:Ai:BuiltIn:ModelPath` (default `models/onnx`, relative to app base directory) เมื่อไฟล์โมเดล
  ไม่มี provider **degrades เป็น typed failure พร้อม install hint** — ไม่เคย throws
  และ app ที่เหลือไม่ได้รับผลกระทบ
- ขับเคลื่อนทุก text AI feature เป็น compact model จึง text-only (ไม่มี server-side web search หรือ
  vision) และ generation เป็นแบบ serialised (หนึ่ง model instance, reuse หลัง lazy load)
- รับ/bundle โมเดล: ดู [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)

## White-label controls

white-label deployment จำกัด AI ผ่าน `App:Branding` (enforced server-side บนทุก provider upsert):

- `AllowBuiltInAi` (default `true`) — ตั้ง `false` เพื่อ **เอา built-in model ออกทั้งหมด**
- `AllowLocalProviders` (default `true`) — ตั้ง `false` เพื่อห้าม local/self-hosted endpoints
  (loopback / private OpenAI-compatible, เช่น Ollama/LM Studio/vLLM)
- `AllowedAiProviderKinds` (default empty = ทั้งหมด) — ระบุ kinds ที่ deployment อนุญาตเท่านั้น
  (เช่น `["Anthropic","OpenAiCompatible"]`) เพื่อล็อกว่า users สามารถเพิ่ม provider อะไรได้

## Extending: future built-in models

AI layer เป็น **adapter-based และสร้างเพื่อเติบโต** แต่ละ provider คือ `IAiProvider` ที่เลือกโดย
`AiProviderKind` seam ที่ feature หันหน้าเข้าหา (`IAiClient`/`AiFeatureService`) ไม่เคยเปลี่ยน
เพิ่ม built-in model runtime ใหม่ในภายหลัง (ONNX model อื่น, in-process engine อื่น, GGUF/llama.cpp
in-proc, etc.) เป็น localized change: เพิ่ม `AiProviderKind`, implement `IAiProvider` adapter หนึ่งตัว,
register มัน และ (ต้องการ) wire default seeding + dialog option — ไม่มี feature, endpoint หรือ MCP tool
changes Built-in ONNX provider คือ reference implementation ของ pattern นี้

## Capabilities

- **Build cBot** — prompt เป็นภาษาอังกฤษธรรมดา → runnable cBot ผ่าน **generate → build → AI-fix** self-repair loop (`build-strategy`) ที่ `/ai/build` **ซอร์สโค้ดที่สร้างขึ้นจะแสดง** เมื่อ build เสร็จสิ้น (พร้อมปุ่ม copy) พร้อมกับ build log — บนสำเร็จ *และ* บนความล้มเหลว — ดังนั้นคุณจึงเห็นสิ่งที่ AI เขียนเสมอ ไม่ใช่เพียงข้อผิดพลาด
- **Parameter optimization** — closed loop: AI เสนอ param sets, แต่ละ persisted + backtested
  ข้าม nodes (`optimize-run` / `optimize-params`)
- **Autonomous portfolio agent** — mandate-driven proposals พร้อม full decision journal
  (`AgentMandate` → `AgentProposal`)
- **Acting risk guard** — `AiRiskGuard` background service ประเมิน bots ที่ทำงาน สามารถ
  **auto-stop** บน critical risk (opt-in)
- **Prop-firm exposure guardian** — drawdown/exposure limits พร้อม auto-flatten
- **Market alerts** — `AlertRule` engine พร้อม AI sentiment (web-search grounded ที่ provider รองรับ)
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design,
  marketplace curation

## Surfaces

- Web endpoints ภายใต้ `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest,
  optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …)
- MCP tools (`AiTools`) สำหรับ AI clients — ดู [mcp.md](mcp.md) Provider selection โปร่งใสต่อ MCP clients
- **AI** nav group — หนึ่ง Blazor **page ต่อ feature**: Build cBot (`/ai/build`), Review (`/ai/review`),
  Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio
  Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`) บวก Portfolio Agent, Alerts,
  MCP Keys Pages แชร์ `AiFeaturePageBase` + `AiOutputPanel` แต่ละแสดง `AiFeatureNotice` เมื่อไม่มี
  provider configure
- **Settings → AI** (`/settings/ai`, เจ้าของเท่านั้น) — provider list พร้อม **Add / edit provider dialog**
  (kind, base URL พร้อม per-kind hints รวม Ollama/LM Studio localhost preset, model, optional key,
  capability toggles, "set active") และปุ่ม **Test connection**

## Configuration

`App:Ai` รองรับทั้ง legacy single key และ multi-provider seeding:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — ยังคงถูกยอมรับเป็น
  default Anthropic provider
- Multi-provider: `ActiveProvider` (kind) และ `Providers[]`
  (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — imported เข้า store บน startup
  ถ้ายังไม่มี credentials ดังนั้น ops team สามารถ ship configured deployment (รวม local-LLM) ผ่าน
  appsettings/env เท่านั้น

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` ไม่เปลี่ยน สำหรับ tests/dev,
config key อยู่ใน [dev-credentials file](../testing/dev-credentials.md) ภายใต้ `Ai`

## Reliability

provider ถือว่าไม่น่าเชื่อถือ — ไม่มีอะไรที่มันทำสามารถทำให้ app ล่ม นี่ใช้เหมือนกันสำหรับ
cloud และ local endpoints (Ollama ตาย retry แล้ว degrade เหมือน Anthropic ที่ throttle):

- **Graceful degradation.** ทุก failure mode (no provider, HTTP 4xx/5xx/429, timeout, malformed body,
  empty content, unsupported capability) คืนค่า typed `AiResult.Fail(reason)` — client ไม่เคย
  throws เข้า page, MCP tool หรือ hosted service
- **Resilience pipeline.** `AddAiHttpClient` ให้ AI `HttpClient` ที่ใช้ร่วมกัน bounded retry บน
  transient 5xx / network failures (exponential backoff + jitter) บวก generous per-attempt และ total
  timeouts (`AiHttp`) reuse โดยทุก adapter

## Testing with the fake local LLM

AI layer พิสูจน์แล้ว end-to-end **โดยไม่มี external dependency** โดย `FakeLocalLlmServer` — tiny
in-process **OpenAI-compatible** endpoint ที่คืนค่า deterministic canned reply,
wire-identical กับ Ollama/LM Studio/vLLM มันสนับสนุน:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation
- **Integration** — OpenAI-compatible adapter end-to-end, parametrized resilience theory ข้ามทุก adapter
  และ **MCP AI tools**
- **E2E** — `AiLocalFixture` boot app ชี้ไปที่ fake server (หรือ **real** provider เมื่อ
  developer ตั้ง `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` /
  `AI_E2E_MODEL`) — real creds ชนะ) และ drive ทุก AI feature ผ่าน real UI การเพิ่มหรือเปลี่ยน
  AI feature **ต้อง** มี E2E test ผ่าน fixture นี้ (ดู repo test mandate) Lane แบบ opt-in
  (`AI_LOCAL_LLM=1`) run หนึ่ง real completion ผ่าน **Ollama** Testcontainer

## Built-in local AI — zero-setup by default

Built-in ONNX local LLM ทำงานได้ทันที: เมื่อไดเรกทอรี model ไม่มี และ
`App:Ai:BuiltIn:AutoDownload` เป็น `true` (default) app ดาวน์โหลด model ครั้งเดียวในพื้นหลัง
จาก `App:Ai:BuiltIn:DownloadBaseUrl` ขณะที่ download ทำงาน AI calls (และ **Test connection** ใน Settings → AI) คืนค่า
ข้อความที่ชัดเจน "model กำลังดาวน์โหลด (first-time setup)" แทนความล้มเหลวที่หนักแน่น
Air-gapped/metered deployments ตั้ง `AutoDownload=false` และ pre-provision model directory
(`App:Ai:BuiltIn:ModelPath`) White-label `App:Branding:AllowBuiltInAi` gate ยังคงใช้ได้

ดาวน์โหลดยังถูก **pre-warmed บน startup** เมื่อ built-in model เป็น active provider ดังนั้น
มันพร้อมก่อนการคลิก AI ครั้งแรก แทนการล้มเหลวด้วย "downloading…" **Settings → AI** แสดง
live install state บน built-in provider card — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — พร้อมปุ่ม **Download model** (หรือ **Retry download**) ที่ทำให้ one-time background fetch ทำงานตามความต้องการ (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`) การเปิดใช้งาน built-in provider จาก Settings นำ back row ที่ seeded แล้วแทนการเพิ่มรายการ duplicate
ดังนั้นจึงไม่เกิดความขัดแย้งกับ single-active-provider constraint
