# Multi-Provider / Cross-Platform AI

**Goal:** every AI feature (F1–F9, RiskGuard, ResearchDesk, Agent, MCP `AiTools`, all `AiEndpoints`)
today talks to Anthropic only. Make the AI layer **provider-agnostic**: OpenAI, Azure OpenAI,
Google Gemini, and **any OpenAI-compatible endpoint including local models** (Ollama, LM Studio,
vLLM, llama.cpp `server`, LocalAI, OpenRouter, Groq, Together, Mistral, DeepSeek). A user picks a
provider + model + endpoint, supplies a key (optional for local), and every existing feature works
unchanged. Same resilience, gating, encryption, degradation, and **all three test tiers green with
any provider including a local LLM** — E2E runs identically regardless of which is configured.

**House bars (CLAUDE.md, non-negotiable):** strict DDD, three test tiers every change (unit +
integration + E2E, failure paths included), zero warnings, no `DateTime.UtcNow`, no secrets/magic
strings/raw logging, MudBlazor dialogs + mobile-first + branded UI, docs in the same commit,
`TimeProvider` injected, modern C# 14.

---

## 0. Design north star

**The feature-facing seam already exists and does not change.** `Core.Ai.IAiClient.CompleteAsync(
AiTextRequest, ct)` is provider-neutral; `AiFeatureService` and its ~20 methods, `AiRiskGuard`,
`ResearchDesk`, `AiAgentDecisionEngine`, MCP `AiTools`, and every `AiEndpoints` route call *only*
that seam. So the migration touches the **implementation below `IAiClient`**, not the 20 features
above it. That is the whole point of the current abstraction — we extend it, we don't rewrite it.

What is Anthropic-specific and must move behind a provider adapter:
1. **Wire format** — request body shape, auth header (`x-api-key` vs `Authorization: Bearer` vs
   `?key=`), version header, endpoint path (`v1/messages` vs `/v1/chat/completions` vs
   `:generateContent`), response JSON parse (`content[].text` vs `choices[].message.content` vs
   `candidates[].content.parts[].text`).
2. **Capabilities** — server-side web search (Anthropic tool, OpenAI `web_search`/Responses API,
   Gemini grounding, **none on most local models**) and vision/image input differ per provider.
3. **Credentials + endpoint + model selection** — one key today → per-provider config.

**Degrade-not-break stays law.** No configured provider → every feature returns `AiResult.Fail`
(unchanged gate). A provider that can't web-search or can't do vision → the feature still runs
text-only (or returns a typed capability-unsupported `AiResult.Fail`), never throws. Local endpoint
down → resilience pipeline → typed failure, app runs.

**How mature apps do this (research, grounding the design):**
- **LiteLLM / LangChain / Vercel AI SDK / Semantic Kernel** all converge on: a *normalized internal
  request*, and *per-provider adapters* that translate to each wire format. The dominant wire is
  **OpenAI Chat Completions** (`POST {base}/v1/chat/completions`) — Azure OpenAI, Mistral, Groq,
  Together, OpenRouter, DeepSeek, **Ollama, LM Studio, vLLM, llama.cpp, LocalAI** all expose it. So
  **one `OpenAiCompatibleProvider` covers the entire long tail incl. every local runtime**, differing
  only by base URL + model + (optional) key.
- **Native adapters** are only needed where the wire genuinely differs and matters: **Anthropic**
  (keep the existing one — Messages API) and **Google Gemini** (`generateContent`). Everything else
  is OpenAI-compatible.
- **Local-first** posture: key is optional; base URL is user-set; capability flags default to
  text-only unless the user opts in. This mirrors how Ollama/LM Studio are consumed in practice.

Net: **3 adapters** (Anthropic native, Gemini native, OpenAI-compatible) reach ~everything, with the
OpenAI-compatible one doing the heavy lifting for cloud *and* local.

---

## 1. Domain model (src/Core — DDD)

### 1.1 Value objects & enums (`src/Core/Ai/`)
- `AiProviderKind` enum: `Anthropic`, `OpenAiCompatible`, `AzureOpenAi`, `Gemini`. (Ollama/LM Studio/
  vLLM/Groq/OpenRouter/etc. are **not** enum values — they are `OpenAiCompatible` with a different
  base URL. Keeps the switch small.)
- `AiModelId` value object — wraps the model string (`claude-opus-4-8`, `gpt-4o`, `gemini-2.0-flash`,
  `llama3.1:8b`, `qwen2.5-coder`). Non-empty invariant, trimmed.
- `AiEndpoint` value object — wraps base URL. Validates absolute URI; **allows `http://` for
  loopback/private hosts** (local LLMs) but requires `https` otherwise (guard against leaking a key
  over plaintext to a remote host). Unit-tested boundary.
- `AiProviderCapabilities` value object — `SupportsWebSearch`, `SupportsVision`, `SupportsSystemRole`
  (some local chat templates fold system into the first user turn), `SupportsTools`. Computed default
  per `AiProviderKind`, user-overridable.

### 1.2 Provider credential aggregate (`Core.Domain`)
`AiProviderCredential` aggregate (replaces the single-key model):
- Identity: `AiProviderCredentialId` (strong ID) + owner-scoped (this app is owner-managed for AI
  config, same as today's `IAiKeyStore` — Owner policy).
- State: `AiProviderKind Kind`, `AiEndpoint BaseUrl`, `AiModelId Model`, encrypted API key (nullable —
  local needs none), `AiProviderCapabilities`, `MaxTokens`, `bool IsActive`, `CreatedAt`/`UpdatedAt`
  (from `TimeProvider`).
- Behavior (intention-revealing, no public setters): `Create(...)`, `Rotate(newKey, now)`,
  `Retarget(endpoint, model, now)`, `Activate(now)`/`Deactivate(now)`, `OverrideCapabilities(...)`.
- Invariant: exactly **one** active credential (enforced via the store, one-aggregate-per-transaction —
  activating one deactivates the previously active in the same flow through a domain method + store).
- Key stays encrypted via `ISecretProtector` (`EncryptionPurposes.AiApiKey`, add per-provider purposes
  if we want isolation — `EncryptionPurposes.AiProviderKey`).

**DDD note:** the *feature* contracts (`IAiClient`, `AiTextRequest`, `AiResult`) stay in `src/Core/Ai`
as pure Core. The credential persistence lives behind a repository interface in Core, EF impl in
Infrastructure — same pattern as `ICBotRepository` etc.

---

## 2. Infrastructure — provider adapters + routing

### 2.1 The seam is preserved
`IAiClient` interface unchanged. New internal port **below** it:

```csharp
// Core.Ai
public interface IAiProvider
{
    AiProviderKind Kind { get; }
    AiProviderCapabilities Capabilities { get; }
    Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct);
}
```

`AiProviderRequest` = the normalized `AiTextRequest` + resolved model + max tokens + the active
credential's key/base URL (so an adapter is stateless w.r.t. config; the router injects it).

### 2.2 Adapters (`src/Infrastructure/Ai/Providers/`)
- **`AnthropicAiProvider`** — refactor of today's `AnthropicAiClient` body verbatim (Messages API,
  `x-api-key`, `anthropic-version`, `v1/messages`, `content[].text`, `web_search` tool, image block).
- **`OpenAiCompatibleProvider`** — `POST {base}/chat/completions`, `Authorization: Bearer {key}` (omit
  header when key null → local), body `{model, max_tokens, messages:[{role:system},{role:user}]}`,
  parse `choices[0].message.content`. Vision → OpenAI image_url content parts (data URI from the
  `AiImage` base64). Web search → only if capability on (OpenAI hosted tools); otherwise silently
  text-only. **This adapter is what every local LLM uses.** Handles the system-role fold-down when
  `SupportsSystemRole == false`.
- **`AzureOpenAiProvider`** — thin subclass/config of the OpenAI-compatible one: `api-key` header
  instead of Bearer, `{base}/openai/deployments/{model}/chat/completions?api-version=...` path.
- **`GeminiAiProvider`** — `POST {base}/v1beta/models/{model}:generateContent?key={key}`, body
  `{systemInstruction, contents:[{role:user,parts:[...]}]}`, parse
  `candidates[0].content.parts[].text`. Vision → inline_data part. Grounding tool if capability on.

Each adapter: identical try/catch → typed `AiResult.Fail` degradation as today, source-generated log
messages (`LogMessages`), no raw `ILogger.Log*`, no magic strings (new `AiConstants` per wire).

### 2.3 Routing client
`RoutingAiClient : IAiClient` (registered as `IAiClient`, replacing direct `AnthropicAiClient`):
- `Enabled` ⇐ active credential exists (via store).
- `CompleteAsync`: resolve active `AiProviderCredential` → pick matching `IAiProvider` by `Kind` →
  build `AiProviderRequest` (inject key/base/model/caps) → if request needs web-search/vision but
  capability off, either drop the flag (search) or return typed `AiResult.Fail` (vision on a text-only
  local model) → delegate.
- Adapters resolved via keyed DI (`IServiceProvider.GetKeyedService<IAiProvider>(kind)` or a
  `Func<AiProviderKind, IAiProvider>` factory) — modern DI, no service-locator smell in features.

### 2.4 Credential store (replaces `IAiKeyStore`)
`AiProviderStore : IAiProviderStore` — supersedes `AiKeyStore`:
- CRUD over `AiProviderCredential` (encrypted key, cached active credential like today's key cache
  with the same TTL and cache-key pattern).
- `ActiveCredential` (cached), `HasActive`, `SetActiveAsync`, `UpsertAsync`, `RemoveAsync`.
- **Back-compat:** on first read, if no `AiProviderCredential` rows exist but the legacy `ai.api_key`
  AppSetting **or** `App:Ai:ApiKey` config is present, synthesize a default **Anthropic** active
  credential (existing model/base URL) so **every current deployment keeps working with zero action**.
  A one-time migration step (below) can also import it.
- Keep `IAiKeyStore` as a thin shim over the new store during transition, or delete it and update the
  4 call sites (`AiEndpoints`, DI, tests) — prefer delete + update for cleanliness (DDD ubiquitous
  language: it's a *provider store* now).

### 2.5 Resilience (same infrastructure, all providers)
`AddAiHttpClient` today wires one typed `HttpClient<IAiClient, AnthropicAiClient>` with the `AiHttp`
timeout+retry pipeline. New shape:
- Register **one resilience-wrapped `HttpClient` per adapter type** (Anthropic, OpenAiCompatible,
  Azure, Gemini) — or a single named `"ai"` client shared by all adapters (base URL/headers set
  per-request). **All reuse the identical `AiHttp.RetryCount / AttemptTimeout / TotalTimeout`
  pipeline** → the resilience, retry-on-5xx, generous timeouts, and typed-failure guarantee are
  identical for cloud and local. Local endpoints get the same treatment (a dead Ollama → retry →
  typed fail).
- Keep `AiHttpResilienceTests`' guarantee and **parametrize it per adapter** (see §5).

---

## 3. Configuration & options (`AppOptions.Ai`)

Extend, keep back-compat:
```
App:Ai:
  ApiKey            (legacy — still honoured → becomes default Anthropic credential)
  Model, BaseUrl, MaxTokens   (legacy Anthropic defaults — retained)
  RiskGuard*        (unchanged)
  ActiveProvider    (new — kind of the config-seeded active provider)
  Providers[]       (new — deployment-seeded credentials:
                      { Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? })
```
- `AiProviderOptions` record per entry; `AiOptions.Providers` list.
- Config-seeded providers are imported into the store on startup if absent (idempotent), so an ops
  team can ship a local-LLM deployment purely via appsettings/env (e.g. a locked-down box pointing at
  an on-prem vLLM, no UI needed).
- New `AiConstants` groups per wire: `OpenAiConstants` (`chat/completions`, `Authorization`,
  `choices`), `GeminiConstants` (`:generateContent`, `candidates`), Azure path/version. Default base
  URLs per kind (`https://api.openai.com/v1/`, `https://generativelanguage.googleapis.com/`,
  Ollama hint `http://localhost:11434/v1/`). No literals in adapters.

---

## 4. Web + MCP surface (UI = dialogs, mobile-first)

### 4.1 Endpoints (`src/Web/Endpoints/AiEndpoints.cs`)
- Replace `/api/ai/key` (single key) with `/api/ai/providers` CRUD (Owner-only):
  `GET /providers` (list, keys redacted — return `hasKey`, kind, model, base URL, active flag,
  capabilities), `PUT /providers` (upsert), `POST /providers/{id}/activate`, `DELETE /providers/{id}`.
- `GET /status` unchanged shape (`{ enabled }`) so the existing gate JS/E2E is untouched; extend with
  active provider kind + model for display.
- Keep legacy `PUT /key` as a compatibility alias that upserts an Anthropic credential (optional).
- All 20 feature routes (`/generate`, `/review`, `/sentiment`, `/vision`, `/optimize-run`, …)
  **unchanged** — they call `IAiFeatureService`, which is provider-agnostic.

### 4.2 Settings UI (MudBlazor dialog, mobile-first, branded)
- Settings → AI: a provider list (card per configured provider on mobile, active badge) + **an
  "Add / edit provider" dialog** (never inline form): pick Kind, base URL (prefilled hint per kind,
  incl. an Ollama/LM Studio localhost preset), model, key (hidden when local/optional), capability
  toggles (web search / vision) with per-kind defaults, "Set active" switch.
- A "Test connection" button in the dialog → calls a `POST /api/ai/providers/test` that does a tiny
  ping completion and reports success/latency (great UX for local endpoints).
- Reuse `AiFeatureNotice` gate component; it now reads active-provider status.

### 4.3 MCP (`src/Mcp/Tools/AiTools.cs`)
Unchanged — it depends on `IAiFeatureService`. Provider selection is deployment/owner config, so MCP
clients transparently use whatever is active.

---

## 5. Testing — all tiers, every provider, incl. local LLM (the crux)

**Requirement:** "test suite works with all including local LLMs; E2E works without any diff with any
of them or local LLMs." Strategy: make provider round-trips **deterministic via an in-process fake**
so no external creds/models are needed in CI, **plus** an opt-in real-local-LLM lane.

### 5.1 Unit (`tests/UnitTests/Ai/`)
- Per-adapter **request-translation + response-parse** tests using a fake `HttpMessageHandler`:
  assert `AnthropicAiProvider` emits Messages wire + parses `content[].text`;
  `OpenAiCompatibleProvider` emits `chat/completions` + parses `choices[].message.content` (and omits
  `Authorization` when key null → local); `GeminiAiProvider` emits `generateContent` + parses
  `candidates[]`. Vision + web-search flag mapping per capability. System-role fold-down.
- `RoutingAiClient` selection: active kind → right adapter; capability-off degradation (drops web
  search; typed fail on vision-unsupported).
- `AiProviderCredential` aggregate invariants (single-active, rotate, retarget, `AiEndpoint`
  http-only-for-loopback boundary, `AiModelId`/`AiEndpoint` value-object guards).
- Feature tests (`AiFeatureServiceTests` etc.) keep using `Substitute.For<IAiClient>()` — unaffected.

### 5.2 Integration (`tests/IntegrationTests/`)
- **Generalize `AiHttpResilienceTests`** from `AnthropicAiClient` to a `[Theory]` over **every
  adapter** (fake `SequenceHandler` returns 503×2 then 200 with that wire's success body) — proves the
  identical retry/timeout/typed-fail guarantee holds for **cloud and local (OpenAI-compatible)** alike.
- `AiProviderStore` against real Postgres (Testcontainers): upsert, activate-exclusivity, encrypted
  key round-trip, legacy `ai.api_key` → default Anthropic credential import.
- **In-process fake OpenAI-compatible server** (`FakeLocalLlmServer` — a minimal Kestrel/`TestServer`
  serving `/v1/chat/completions` with a canned deterministic reply) → drive `OpenAiCompatibleProvider`
  end-to-end. This is the "local LLM" stand-in every CI run uses: byte-identical wire to Ollama/LM
  Studio/vLLM, zero external dependency, fully deterministic.
- **Opt-in real local LLM lane** (`[Trait]` + `AI_LOCAL_LLM=1`): spin an **Ollama Testcontainer**
  (`ollama/ollama`), pull a tiny model (e.g. `qwen2.5:0.5b`/`tinyllama`), and run one real completion
  through `OpenAiCompatibleProvider`. Skipped by default (CI time/pull weight), runnable locally and
  in a nightly job — satisfies "self-serviceable, never excuse-skip live" per repo memory.

### 5.2b Fake local LLM — the deterministic testing backbone (implemented)

`FakeLocalLlmServer` is a minimal in-process **OpenAI-compatible** HTTP endpoint (serves
`POST /v1/chat/completions` with a canned, deterministic reply; wire-identical to Ollama / LM Studio /
vLLM / llama.cpp). It exists in both `tests/IntegrationTests` and `tests/E2ETests` and is the standard
way to test the AI layer with **zero external dependency**:
- **Integration:** drives `OpenAiCompatibleProvider` end-to-end (`LocalLlmProviderTests`) and the MCP AI
  tools (`McpAiToolsLocalLlmTests`).
- **E2E:** `AiLocalFixture` (collection `ai-local`) boots the app pointed at the fake server (or a
  **real** provider when the dev sets `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY`/`AI_E2E_KIND`/
  `AI_E2E_MODEL`) — real creds win, otherwise fake). `AiFeatureLocalTests` drives every AI feature
  through the real UI (Review, Sentiment, Debate, settings dialog, mobile) and asserts the output
  renders. **MANDATORY (CLAUDE.md): every new/changed AI feature ships an E2E test via the fake LLM.**
  This lane also caught a latent bug — the AI feature pages bound `Output="Output"` (literal) instead
  of `Output="@Output"` (the field), so results never rendered; fixed across all pages.

### 5.3 E2E (`tests/E2ETests/`) — no diff across providers
- The existing keyless-gate tests (`AiTests`) stay green (no provider configured → notice + disabled).
- **New provider-agnostic E2E:** the `AppFixture` boots an **in-process fake OpenAI-compatible
  endpoint** (the same `FakeLocalLlmServer`) and the test (a) opens Settings → AI, (b) adds a
  "Local (OpenAI-compatible)" provider via the dialog pointing at that endpoint, sets active, (c)
  runs one feature (e.g. Review) and asserts the deterministic canned output renders. Because the
  fake speaks the OpenAI wire, **this same E2E validates every OpenAI-compatible target (all local
  runtimes + most clouds) without change** — that is the "works with any of them / no diff" property.
- New settings route/dialog added to `PageSmokeTests` per mandate.
- `FakeTradingSession` untouched (not AI).

---

## 6. Migration & back-compat (money-moving app → zero surprise)

- **No breaking change for existing deployments:** legacy `ai.api_key` AppSetting and `App:Ai:ApiKey`
  config continue to work — synthesized/imported as a default active **Anthropic** credential. Model
  ID default stays `claude-opus-4-8`.
- **EF migration** (if `AiProviderCredential` is a table): `dotnet ef migrations add AddAiProviders`
  (canonical layout). One-time data migration copies the encrypted `ai.api_key` into the new table as
  an Anthropic credential. If we instead persist providers as JSON rows in existing `AppSettings`, no
  schema migration — decide in §8 (recommend a proper table for DDD + queryability).
- `EncryptionPurposes.AiApiKey` reused (or add `AiProviderKey`); existing encrypted key stays readable.

---

## 7. Docs (same commit, canonical Docusaurus)

**Docs must list every supported provider and give a step-by-step setup guide for each.** The docs
are the source of truth for "what's supported" — a provider isn't "done" until its setup guide ships.

- Rewrite `website/docs/features/ai.md`: provider-agnostic framing, provider picker, and a
  **capability matrix table** — one row per supported provider, columns: Kind, default base URL, auth
  (key required?), web-search, vision, notes. Covers at minimum:
  Anthropic · OpenAI · Azure OpenAI · Google Gemini · Ollama (local) · LM Studio (local) · vLLM
  (local/on-prem) · llama.cpp server (local) · LocalAI · OpenRouter · Groq · Together · Mistral ·
  DeepSeek. (Local + the OpenAI-compatible clouds all note "via OpenAI-compatible adapter".)
- New `website/docs/deployment/ai-providers.md` = **the provider catalog + setup guides**. One
  section per provider, each with: where to get the key/URL, exact `App:Ai:Providers[]` /
  env-var snippet, the model id to use, capability caveats, and the UI dialog steps
  (Settings → AI → Add provider → Test connection). Explicit **local-model walkthroughs**:
  - **Ollama** — install, `ollama pull <model>`, base URL `http://localhost:11434/v1/`, no key.
  - **LM Studio** — start local server, base URL `http://localhost:1234/v1/`, model = loaded model id.
  - **vLLM / llama.cpp / LocalAI** — serve OpenAI-compatible endpoint, base URL + served model name.
  Plus the https-vs-loopback rule and on-prem/air-gapped guidance.
- `website/docs/operations`: rotating keys, switching the active provider, "test connection",
  per-capability degradation behaviour (e.g. web-search/vision unavailable on a local model).
- Update `sidebars.ts` for the new pages; `npm run build` must report **zero broken links** before PR.

---

## 8. Phased work breakdown (each phase = its own green build + tests + docs)

**P0 — Core seam & value objects.** `AiProviderKind`, `AiModelId`, `AiEndpoint`,
`AiProviderCapabilities`, `IAiProvider`/`AiProviderRequest`. Unit tests for value objects. No behavior
change yet (Anthropic still sole impl). *Decision to lock here: table vs AppSettings-JSON for
credentials — recommend `AiProviderCredential` table.*

**P1 — Anthropic adapter refactor.** Extract `AnthropicAiClient` body into `AnthropicAiProvider`;
introduce `RoutingAiClient : IAiClient` delegating to it; DI keyed resolution. Green with unchanged
behavior; existing tests pass. Generalize resilience registration (all adapters share `AiHttp`).

**P2 — OpenAI-compatible adapter (covers all local + most clouds).** `OpenAiCompatibleProvider`,
`FakeLocalLlmServer`, unit + integration (fake server) + parametrized resilience theory. This alone
delivers "local LLMs work."

**P3 — Gemini + Azure adapters.** `GeminiAiProvider`, `AzureOpenAiProvider`; unit + resilience-theory
rows. Capability matrix wired.

**P4 — Credential store + config seeding + back-compat.** `AiProviderCredential` aggregate + repo +
EF config + migration; `AiProviderStore` replacing `AiKeyStore`; legacy-key import; `AppOptions.Ai.
Providers` seeding. Integration tests (Postgres). Retire/alias `IAiKeyStore`.

**P5 — Web endpoints + Settings dialog + MCP.** `/api/ai/providers` CRUD + `test` ping; MudBlazor
provider dialog (mobile-first, branded); gate/status wiring; `PageSmokeTests` route. E2E
provider-agnostic test against the in-process fake OpenAI endpoint.

**P6 — Real local-LLM lane + docs + final sweep.** Ollama Testcontainer opt-in test; docs (ai.md,
deployment, operations, sidebars); analyzer sweep + `get_file_problems` clean on every touched file;
`caveman:cavecrew-reviewer` on the diff; full `dotnet test` (incl. opt-in local lane run locally).

---

## 9. Touch map (files)

- **New (Core):** `Ai/AiProviderKind.cs`, `Ai/AiModelId.cs`, `Ai/AiEndpoint.cs`,
  `Ai/AiProviderCapabilities.cs`, `Ai/IAiProvider.cs`, `Ai/AiProviderRequest.cs`,
  `Domain/AiProviderCredential.cs` (+ strong ID), `Domain/IAiProviderRepository.cs`,
  `Ai/IAiProviderStore.cs`.
- **New (Infra):** `Ai/Providers/AnthropicAiProvider.cs`, `OpenAiCompatibleProvider.cs`,
  `AzureOpenAiProvider.cs`, `GeminiAiProvider.cs`, `Ai/RoutingAiClient.cs`, `Ai/AiProviderStore.cs`,
  `Persistence/Configurations/AiProviderCredentialConfiguration.cs`, EF migration.
- **Modified (Infra):** `Ai/AiHttpClientRegistration.cs` (per-adapter/shared resilience),
  `DependencyInjection.cs` (register adapters keyed, routing client, store), `Core/Logging` messages,
  `Core/Constants/AppConstants.cs` (OpenAI/Gemini/Azure constants, default base URLs).
- **Modified (Core):** `Options/AppOptions.cs` (`AiOptions.Providers`, `ActiveProvider`,
  `AiProviderOptions`), `Constants/EncryptionPurposes` (opt. `AiProviderKey`).
- **Modified (Web):** `Endpoints/AiEndpoints.cs` (providers CRUD/test, status extension), Settings
  page + new provider dialog component.
- **Delete/alias:** `Ai/AiKeyStore.cs` + `IAiKeyStore` (→ `AiProviderStore`/`IAiProviderStore`).
- **Tests:** new unit (adapters, routing, aggregate), integration (store + fake-server + parametrized
  resilience + opt-in Ollama), E2E (provider-agnostic + keyless gate retained), `PageSmokeTests`.
- **Docs:** `website/docs/features/ai.md`, `deployment/ai-providers.md`, `operations/*`, `sidebars.ts`.

---

## 10.5 Built-in AI, demo, and white-label (implemented — extends original scope)

- **Built-in local LLM, shipped + default-on.** A real in-process model via
  **Microsoft.ML.OnnxRuntimeGenAI** (`AiProviderKind.BuiltInOnnx`, `OnnxGenAiProvider`, singleton, lazy
  model load, serialized generation). On first startup, when no provider is configured and the
  white-label gate allows it, it is **seeded + activated automatically**, so every deployment has working
  AI with **no key**. Model dir = `App:Ai:BuiltIn:ModelPath` (default `models/onnx`); absent ⇒ typed
  "model not installed" failure (never throws). Powers all text features (text-only model).
- **Demo provider** (`AiProviderKind.Demo`, `DemoAiProvider`) — a zero-dependency in-process fake that
  returns canned, prompt-aware text. One-click **"Try demo AI"** in Settings → AI lets any user see the
  AI features work live before wiring a real provider.
- **White-label AI limits** (`App:Branding`, enforced by `AiProviderPolicy` server-side on every upsert):
  `AllowBuiltInAi` (remove the built-in), `AllowLocalProviders` (forbid loopback/private endpoints),
  `AllowedAiProviderKinds` (restrict to a sanctioned set). Violations ⇒ `400` / `DomainException`.
- **Flexible for future built-in models.** The adapter design (`IAiProvider` keyed by `AiProviderKind`)
  means a future in-process model runtime is a localized add (kind + adapter + registration), with no
  change to any AI feature, endpoint, or MCP tool. The ONNX provider is the reference pattern — documented
  in `website/docs/features/ai.md` and `website/docs/deployment/ai-providers.md`.
- **Tests:** unit (ONNX degrade path, demo, policy), integration (built-in default-seed + policy reject,
  opt-in real ONNX lane `AI_ONNX_MODEL`), E2E (demo live via UI; opt-in ONNX UI + non-UI `AI_ONNX_MODEL`).

## 10. Open decisions (defaults chosen; flag if you disagree)

1. **Credential persistence:** dedicated `AiProviderCredential` table (recommended — DDD aggregate,
   queryable, clean migration) vs JSON in `AppSettings` (no migration, weaker model). *Default: table.*
2. **Multi-credential vs single-active:** support N stored providers with one active (recommended,
   enables fast switching + "test connection") vs a single replaceable credential. *Default: N + one
   active.*
3. **Web-search on non-Anthropic:** wire OpenAI/Gemini native grounding now, or ship capability-gated
   text-only first and add grounding in a follow-up. *Default: capability-gated now, grounding for
   OpenAI/Gemini in P3, local always text-only.*
4. **Real-local-LLM CI lane:** opt-in Ollama Testcontainer (default, skipped unless `AI_LOCAL_LLM=1`)
   vs always-on nightly. *Default: opt-in + nightly job.*
