---
description: "cMind AI je provider-agnostická — Anthropic, OpenAI, Azure OpenAI, Google Gemini a jakýkoliv OpenAI-kompatibilní endpoint včetně lokálních modelů (Ollama, LM Studio, vLLM). Zvolte provider, model a endpoint; každá AI funkce funguje nezměněná."
---

# AI funkce

AI vrstva cMind je **provider-agnostická**. Každá funkce mluví přes jednu provider-neutral seam
(`IAiClient.CompleteAsync`); **routing client** resolveruje aktivní provider credential a dispatchuje
na odpovídající wire adapter. Zvolíte provider + model + endpoint (a pokud provider potřebuje,
klíč); každá existující funkce funguje nezměněná se stejným gatingem, šifrováním, resiliencí a
degradací.

**Baterie v ceně:** **vestavěný lokální LLM je součástí aplikace a je defaultně zapnutý**
(Microsoft.ML.OnnxRuntimeGenAI, např. Phi-3-mini) — takže každé nasazení má fungující AI **bez API klíče
a bez externí služby**. White-label nasazení ho může odstranit a omezit, které providery mohou uživatelé
přidávat. Kromě vestavěného, připojte jakéhokoliv externího providera.

Podporovaní provideri:

- **Vestavěná lokální AI** (`BuiltInOnnx`) — in-process ONNX GenAI model, žádný klíč, shipped + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** a **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Jakýkoliv OpenAI-kompatibilní endpoint**, včetně **lokálních modelů** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) a OpenAI-kompatibilních cloudů (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — vše přes jeden OpenAI-kompatibilní adapter, liší se pouze base URL + model + klíč.

Přesně **jeden** provider je aktivní najednou. Credentials jsou uloženy **šifrovaně**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
lokální endpoint **nevyžaduje klíč**. S **žádným** aktivním providerem každá funkce vrací disabled
result a zbytek aplikace běží nezměněn (žádný klíč není potřebný pro build, test, nebo běh platformy).

**Back-compat:** existující nasazení legacy `App:Ai:ApiKey` (nebo starý šifrovaný `ai.api_key`
setting) je automaticky ctěn jako defaultní aktivní **Anthropic** provider — žádná akce potřebná.

AI nenakonfigurována → AI stránky ztlumí akce a ukážou banner plus one-time prompt pro přidání providera v
**Settings → AI** (`AiFeatureNotice`). Status na `GET /api/ai/status` (`{ enabled, kind, model }`);
provideri spravováni (pouze owner) přes `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, a `POST /api/ai/providers/{id}/test` connectivity ping.

## Deployment default vs vlastní provider uživatele

AI credentials mají dva scopes:

- **Deployment default (owner-managed).** Owner konfiguruje provider (nebo ho shipuje přes
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Stává se **sdíleným defaultem pro každého uživatele** —
  takže broker nebo hosting provider může financovat AI pro všechny své uživatele **bez per-user setup a bez
  per-user limitu**. Spravováno přes owner-only `/api/ai/providers` routes.
- **Vlastní provider uživatele (self-service).** Jakýkoliv přihlášený uživatel může přidat svého vlastního providera pod
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Když je přítomen, jeho **vlastní aktivní provider přepíše deployment
  default pro jeho vlastní AI funkce**; odstraněním se vrátí k defaultu.

**Resolution order** (in `AiProviderStore`, per request user): user's own active credential → the
deployment default → legacy config key → none (AI disabled). Přesně jedna credential je aktivní
**per scope** (partial unique index per `OwnerUserId`), a každý scope je resolverován nezávisle, takže
user activating their own key nikdy neruší sdílený default. Background/non-Web kontexty (bez request
user) vždy resolverují deployment default.

## Provider capability matrix

Capabilities defaultují per provider a jsou owner-přepsatelné. Když capability je off, funkce
**degraduje, nikdy nehazuje**: web search je tiše dropnuto; vision vrací typed
capability-unsupported failure.

| Provider | Kind | Default base URL | Key required | Web search | Vision | Poznámky |
|---|---|---|---|---|---|---|
| Vestavěná lokální AI | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | shipped ONNX GenAI model, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (lokální) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | přes OpenAI-kompatibilní adapter |
| LM Studio (lokální) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | přes OpenAI-kompatibilní adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | přes OpenAI-kompatibilní adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | přes OpenAI-kompatibilní adapter |

Full per-provider setup guides (keys, URLs, model ids, UI steps): viz
[AI providers — setup catalog](../deployment/ai-providers.md).

## Vestavěná lokální AI (shipped, default-on)

cMind shipuje **reálný lokální LLM běžící in-process** přes
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (compact instruct model jako
Phi-3-mini). Nepotřebuje **žádný API klíč a žádnou externí službu**, a při prvním startu — když žádný provider není
nakonfigurován a white-label gate to povoluje — je **seeded a activated automaticky**, takže každé
nasazení má fungující AI out of the box.

- Adresář modelu (`genai_config.json` + tokenizer + weights) je konfigurován
  `App:Ai:BuiltIn:ModelPath` (default `models/onnx`, relativní k app base adresáři). Když model
  soubory chybí, provider **degraduje na typed failure s install hint** — nikdy nehazuje,
  a zbytek aplikace je unaffected.
- Pohání každou textovou AI funkci. Jako compact model, je text-only (žádný server-side web search nebo
  vision) a generace je serializovaná (jedna model instance, znovupoužitá po lazy load).
- Získejte/bundle model: viz [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label ovládací prvky

White-label nasazení omezuje AI přes `App:Branding` (enforced server-side na každém provider upsert):

- `AllowBuiltInAi` (default `true`) — nastavte `false` pro **odstranění vestavěného modelu** celkově.
- `AllowLocalProviders` (default `true`) — nastavte `false` pro zakázání lokálních/self-hosted endpointů (loopback /
  privátní OpenAI-kompatibilní, např. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (default empty = all) — vyjmenujte pouze kinds, které deployment sanctionuje (např.
  `["Anthropic","OpenAiCompatible"]`) pro lockdown které providery mohou uživatelé přidávat.

## Rozšiřování: budoucí vestavěné modely

AI vrstva je **adapter-based a built to grow**. Každý provider je `IAiProvider` vybraný
`AiProviderKind`; feature-facing seam (`IAiClient`/`AiFeatureService`) se nikdy nemění. Přidání nového
vestavěného model runtime později (další ONNX model, jiný in-process engine, GGUF/llama.cpp
in-proc, atd.) je lokalizovaná změna: přidejte `AiProviderKind`, implementujte jeden `IAiProvider` adapter,
zaregistrujte ho, a (volitelně) wire default seeding + dialog option — žádná změna feature, endpointu, nebo MCP tool.
Vestavěný ONNX provider je reference implementation tohoto patternu.

## Capabilities

- **Build cBot** — plain-English prompt → runnable cBot přes **generate → build → AI-fix** self-repair loop (`build-strategy`), na `/ai/build`. **Vygenerovaný zdrojový kód je zobrazen** po dokončení buildu (s tlačítkem kopírování), vedle build logu — při úspěchu *i* při selhání — takže vždy vidíte, co AI napsala, nikoli jen chyby.
- **Parameter optimization** — closed loop: AI navrhuje param sets, každý persisted + backtested across nodes (`optimize-run` / `optimize-params`).
- **Autonomní portfolio agent** — mandate-driven proposals s full decision journal (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background service hodnotí běžící boty, může **auto-stop** na critical risk (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure limity s auto-flatten.
- **Market alerts** — `AlertRule` engine s AI sentiment (web-search grounded kde provider podporuje).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation.

## Surfaces

- Web endpoints under `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- MCP tools (`AiTools`) pro AI klienty — viz [mcp.md](mcp.md). Provider selection je transparentní pro MCP klienty.
- **AI** nav group — jedna Blazor **stránka per funkce**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), plus Portfolio Agent, Alerts, MCP Keys. Stránky sdílejí `AiFeaturePageBase` + `AiOutputPanel`; každá ukazuje `AiFeatureNotice` když žádný provider nakonfigurován.
- **Settings → AI** (`/settings/ai`, pouze owner) — seznam providerů s **Add / edit provider dialog** (kind, base URL s per-kind hints incl. Ollama/LM Studio localhost preset, model, volitelný klíč, capability toggles, "set active") a tlačítko **Test connection**.

## Konfigurace

`App:Ai` podporuje legacy single key i multi-provider seeding:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — stále ctěno jako
  default Anthropic provider.
- Multi-provider: `ActiveProvider` (kind) a `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importováno do store při startu pokud žádné credentials ještě neexistují, takže
  ops team může shipovat nakonfigurované (incl. local-LLM) nasazení čistě přes appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` nezměněny. Pro testy/dev, config key
žije v unified [dev-credentials file](../testing/dev-credentials.md) under `Ai`.

## Reliability

Provider je treated as unreliable — nic co dělá nemůže shodit aplikaci. To platí identicky
pro cloud i lokální endpointy (mrtvý Ollama retryuje pak degraduje přesně jako throttled Anthropic):

- **Graceful degradation.** Každý failure mode (žádný provider, HTTP 4xx/5xx/429, timeout, malformed body,
  empty content, unsupported capability) vrací typed `AiResult.Fail(reason)` — klient nikdy
  nehazuje do stránky, MCP tool, nebo hosted service.
- **Resilience pipeline.** `AddAiHttpClient` dává jednomu sdílenému AI `HttpClient` bounded retry na
  transient 5xx / network failures (exponential backoff + jitter) plus generous per-attempt a total
  timeouts (`AiHttp`), znovupoužitý každým adapterem.

## Testování s fake lokálním LLM

AI vrstva je prověřena end-to-end **bez jakékoliv externí závislosti** přes `FakeLocalLlmServer` — tiny
in-process **OpenAI-kompatibilní** endpoint vracející deterministic canned reply, wire-identical to
Ollama/LM Studio/vLLM. Zálohuje:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation.
- **Integration** — the OpenAI-compatible adapter end-to-end, the parametrized resilience theory across
  every adapter, and the **MCP AI tools**.
- **E2E** — `AiLocalFixture` bootuje aplikaci namířenou na fake server (nebo **reálného** providera když
  developer nastaví `AI_E2E_BASEURL` (+ volitelně `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  reálné creds vyhrají) a žene každou AI funkci přes reálné UI. Přidání nebo změna jakékoliv AI funkce
  **vyžaduje** E2E test přes tuto fixture (viz repo test mandate). Opt-in lane
  (`AI_LOCAL_LLM=1`) spouští jeden reálný completion přes **Ollama** Testcontainer.

## Vestavěná lokální AI — nula-setup defaultně

Vestavěný ONNX lokální LLM funguje z krabice: když jeho adresář modelu chybí a
`App:Ai:BuiltIn:AutoDownload` je `true` (výchozí), aplikace stáhne model jednou na pozadí
z `App:Ai:BuiltIn:DownloadBaseUrl`. Zatímco stahování běží, AI volání (a **Test connection** v Settings → AI) vrátí jasnou zprávu „model se stahuje (první nastavení)" místo tvrdého selhání. Air-gapped/metered nasazení nastavují `AutoDownload=false` a předem zajišťují adresář modelu (`App:Ai:BuiltIn:ModelPath`). White-label gate `App:Branding:AllowBuiltInAi` stále platí.

Stahování je také **pre-warmed při startu** když je vestavěný model aktivním providerem, takže je připraven před prvním AI kliknutím místo selhání kliknutí s „stahování…". **Settings → AI** zobrazuje stav live instalace na kartě vestavěného providera — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — s tlačítkem **Download model** (nebo **Retry download**), které spustí jednou-background fetch na požádání (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Povolení vestavěného providera z Settings znovupoužije již-seeded řádek místo přidání duplikátu, takže nikdy nekoliduje s one-active-provider constraintem.
