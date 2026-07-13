---
description: "cMind AI je провајдер-агностик — Anthropic, OpenAI, Azure OpenAI, Google Gemini, i bilo koju OpenAI-kompatibilnu krajnju tacku ukljucujuci lokalne modele (Ollama, LM Studio, vLLM). Izaberite provajdera, model i krajnju tacku; svaka AI funkcija radi nepromijenjena."
---

# AI funkcije

AI sloj cMind-a je **провајдер-агностик**. Svaka funkcija govori sa jednom провајдер-неутралном спојницом
(`IAiClient.CompleteAsync`); **рутирајући клијент** рјешава активне креденцијале провајдера and dispatches
to the matching wire adapter. Ви бирате провајдера + модел + крајњу тачку (and, if the provider needs it,
a key); свака постојећа функција ради непромијењено with the same gating, encryption, resilience, and
degradation.

**Батерије укључене:** **уграђени локални LLM се испоручује са апликацијом и укључен је по подразумијевању**
(Microsoft.ML.OnnxRuntimeGenAI, e.g. Phi-3-mini) — so every deployment has working AI **with no API key
and no external service**. White-label deployment може да га уклони and restrict which providers users may
add. Поред уграђеног, повежите било ког екстерног провајдера.

Подржани провајдери:

- **Уграђени локални AI** (`BuiltInOnnx`) — in-process ONNX GenAI модел, без кључа, испоручен + укључен по подразумијевању.
- **Anthropic** (Claude — Messages API)
- **OpenAI** and **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- ****Било koja OpenAI-компатибилна крајња тачка****, укључујући **локалне моделе** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) and OpenAI-compatible clouds (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — све преко једног OpenAI-компатибилног адаптера, differing only by base URL + model + key.

Тачно **један** провајдер је активан у исто вријеме. Креденцијали се чувају **шифровано**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
локална крајња тачка треба **без кључа**. Када **нема** активног провајдера, every feature returns the disabled
result и остатак апликације ради непромијењено (кључ није потребан за изградбу, тестирање или покретање платформе).

**Повратна компатибилност:** постојећи `App:Ai:ApiKey` legacy deployment-а (or the old encrypted `ai.api_key`
setting) се аутоматски поштује kao подразумијевани активни **Anthropic** провајдер — није потребна никаква акција.

AI неконфигурисан → AI странице затамње акције and приказују banner плус једнократни позив да додате провајдера in
**Settings → AI** (`AiFeatureNotice`). Status at `GET /api/ai/status` (`{ enabled, kind, model }`);
providers managed (owner-only) via `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, and a `POST /api/ai/providers/test` connectivity ping.

## Deployment default vs a user's own provider

AI credentials have two scopes:

- **Deployment default (owner-managed).** The owner configures a provider (or ships one via
  `App:Ai:Providers[]` / the legacy `App:Ai:ApiKey`). It becomes the **shared default for every user** —
  so a broker or hosting provider can fund AI for all their users with **no per-user setup and no
  per-user limit**. Managed via the owner-only `/api/ai/providers` routes above.
- **A user's own provider (self-service).** Any signed-in user may add their own provider under
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. When present, their **own active provider overrides the deployment
  default for their own AI features**; removing it falls back to the default.

**Resolution order** (in `AiProviderStore`, per request user): the user's own active credential → the
deployment default → the legacy config key → none (AI disabled). Exactly one credential is active
**per scope** (a partial unique index per `OwnerUserId`), and each scope is resolved independently, so a
user activating their own key never disturbs the shared default. Background/non-Web contexts (no request
user) always resolve the deployment default.

## Provider capability matrix

Capabilities default per provider and are owner-overridable. When a capability is off the feature
**degrades, never throws**: web search is silently dropped; vision returns a typed
capability-unsupported failure.

| Provider | Kind | Default base URL | Key required | Web search | Vision | Notes |
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

Full per-provider setup guides (keys, URLs, model ids, UI steps): see
[AI providers — setup catalog](../deployment/ai-providers.md).

## Built-in local AI (shipped, default-on)

cMind ships a **real local LLM that runs in-process** via
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (a compact instruct model such as
Phi-3-mini). It needs **no API key and no external service**, and on first startup — when no provider is
configured and the white-label gate allows it — it is **seeded and activated automatically**, so every
deployment has working AI out of the box.

- The model directory (`genai_config.json` + tokenizer + weights) is configured by
  `App:Ai:BuiltIn:ModelPath` (default `models/onnx`, relative to the app base directory). When the model
  files are absent the provider **degrades to a typed failure with an install hint** — it never throws,
  and the rest of the app is unaffected.
- It powers every text AI feature. Being a compact model, it is text-only (no server-side web search or
  vision) and generation is serialised (one model instance, reused after a lazy load).
- Acquire/bundle the model: see [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label controls

A white-label deployment restricts AI via `App:Branding` (enforced server-side on every provider upsert):

- `AllowBuiltInAi` (default `true`) — set `false` to **remove the built-in model** entirely.
- `AllowLocalProviders` (default `true`) — set `false` to forbid local/self-hosted endpoints (loopback /
  private OpenAI-compatible, e.g. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (default empty = all) — list only the kinds the deployment sanctions (e.g.
  `["Anthropic","OpenAiCompatible"]`) to lock down which providers users may add.

## Extending: future built-in models

The AI layer is **adapter-based and built to grow**. Each provider is an `IAiProvider` selected by
`AiProviderKind`; the feature-facing seam (`IAiClient`/`AiFeatureService`) never changes. Adding a new
built-in model runtime later (another ONNX model, a different in-process engine, GGUF/llama.cpp
in-proc, etc.) is a localized change: add an `AiProviderKind`, implement one `IAiProvider` adapter,
register it, and (optionally) wire default seeding + a dialog option — no feature, endpoint, or MCP tool
changes. The built-in ONNX provider is the reference implementation of this pattern.

## Capabilities

- **Build cBot** — plain-English prompt → runnable cBot via **generate → build → AI-fix** self-repair loop (`build-strategy`), at `/ai/build`.
- **Parameter optimization** — closed loop: AI proposes param sets, each persisted + backtested across nodes (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — mandate-driven proposals with full decision journal (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background service assesses running bots, can **auto-stop** on critical risk (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure limits with auto-flatten.
- **Market alerts** — `AlertRule` engine with AI sentiment (web-search grounded where the provider supports it).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation.

## Surfaces

- Web endpoints under `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- MCP tools (`AiTools`) for AI clients — see [mcp.md](mcp.md). Избор провајдера је транспарентан MCP клијентима.
- **AI** nav group — one Blazor **page per feature**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), plus Portfolio Agent, Alerts, MCP Keys. Pages share `AiFeaturePageBase` + `AiOutputPanel`; each shows `AiFeatureNotice` when no provider is configured.
- **Settings → AI** (`/settings/ai`, owner-only) — provider list with an **Add / edit provider dialog** (kind, base URL with per-kind hints incl. an Ollama/LM Studio localhost preset, model, optional key, capability toggles, "set active") and a **Test connection** button.

## Configuration

`App:Ai` supports both the legacy single key and multi-provider seeding:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — still honoured as the
  default Anthropic provider.
- Multi-provider: `ActiveProvider` (kind) and `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — imported into the store on startup if no credentials exist yet, so an
  ops team can ship a configured (incl. local-LLM) deployment purely via appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` unchanged. For tests/dev, a config key
lives in the unified [dev-credentials file](../testing/dev-credentials.md) under `Ai`.

## Reliability

The provider is treated as unreliable — nothing it does can take the app down. This holds identically
for cloud and local endpoints (a dead Ollama retries then degrades exactly like a throttled Anthropic):

- **Graceful degradation.** Every failure mode (no provider, HTTP 4xx/5xx/429, timeout, malformed body,
  empty content, unsupported capability) returns a typed `AiResult.Fail(reason)` — the client never
  throws into a page, MCP tool, or hosted service.
- **Resilience pipeline.** `AddAiHttpClient` gives the one shared AI `HttpClient` a bounded retry on
  transient 5xx / network failures (exponential backoff + jitter) plus generous per-attempt and total
  timeouts (`AiHttp`), reused by every adapter.

## Testing with the fake local LLM

The AI layer is proven end-to-end **without any external dependency** by `FakeLocalLlmServer` — a tiny
in-process **OpenAI-compatible** endpoint returning a deterministic canned reply, wire-identical to
Ollama/LM Studio/vLLM. It backs:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation.
- **Integration** — the OpenAI-compatible adapter end-to-end, the parametrized resilience theory across
  every adapter, and the **MCP AI tools**.
- **E2E** — the `AiLocalFixture` boots the app pointed at the fake server (or a **real** provider when
  the developer sets `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  real creds win) and drives every AI feature through the real UI. Adding or changing any AI feature
  **requires** an E2E test through this fixture (see the repo test mandate). An opt-in lane
  (`AI_LOCAL_LLM=1`) runs one real completion through an **Ollama** Testcontainer.

## Built-in local AI — zero-setup by default

The built-in ONNX local LLM works out of the box: when its model directory is absent and
`App:Ai:BuiltIn:AutoDownload` is `true` (the default), the app downloads the model once in the
background from `App:Ai:BuiltIn:DownloadBaseUrl`. While the download runs, AI calls (and **Test
connection** in Settings → AI) return a clear "model is downloading (first-time setup)" message
rather than a hard failure. Air-gapped/metered deployments set `AutoDownload=false` and
pre-provision the model directory (`App:Ai:BuiltIn:ModelPath`). The white-label
`App:Branding:AllowBuiltInAi` gate still applies.
