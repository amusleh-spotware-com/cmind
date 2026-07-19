---
description: "L'AI di cMind è agnostica rispetto al provider — Anthropic, OpenAI, Azure OpenAI, Google Gemini, e qualsiasi endpoint compatibile con OpenAI inclusi modelli locali (Ollama, LM Studio, vLLM). Scegli un provider, modello ed endpoint; ogni funzionalità AI funziona invariata."
---

# Funzionalità AI

Il layer AI di cMind è **agnostico rispetto al provider**. Ogni feature parla con una singola seam
provider-neutral (`IAiClient.CompleteAsync`); un **routing client** risolve la credenziale del provider
attivo e dispatcha all'adapter wire matching. Scegli un provider + modello + endpoint (e, se il provider
lo richiede, una chiave); ogni feature esistente funziona invariata con la stessa gated, crittografia,
resilience e degradation.

**Batterie incluse:** un **LLM locale integrato spedisce con l'app ed è abilitato per default**
(Microsoft.ML.OnnxRuntimeGenAI, es. Phi-3-mini) — così ogni deployment ha AI funzionante **senza API key
e senza servizio esterno**. Un deployment white-label può rimuoverlo e limitare quali provider gli utenti
possono aggiungere. Oltre al built-in, connetti qualsiasi provider esterno.

Provider supportati:

- **AI locale integrata** (`BuiltInOnnx`) — modello ONNX GenAI in-process, no key, spedito + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** e **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Qualsiasi endpoint compatibile con OpenAI**, inclusi **modelli locali** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) e cloud compatibili con OpenAI (**Kimi / Moonshot** a
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — tutti tramite l'unico
  adapter compatibile OpenAI, differing solo per base URL + modello + key. La finestra di dialogo Add-provider offre
  **preset** one-click (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio) che compilano
  l'URL base + un modello di esempio.

Esattamente **uno** provider è attivo alla volta. Le credenziali sono memorizzate **crittografate**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
un endpoint locale non richiede **nessuna key**. Con **nessun** provider attivo, ogni feature restituisce il
risultato disabled e il resto dell'app funziona invariato (nessuna key necessaria per build, test o
run della piattaforma).

**Back-compat:** la `App:Ai:ApiKey` legacy di un deployment esistente (o la vecchia impostazione crittografata
`ai.api_key`) è onorata automaticamente come provider **Anthropic** attivo default — nessuna azione necessaria.

AI non configurata → le pagine AI dim actions e mostrano un banner più un prompt one-time per aggiungere un
provider in **Settings → AI** (`AiFeatureNotice`). Status a `GET /api/ai/status` (`{ enabled, kind, model }`);
provider gestiti (solo owner) via `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, e un `POST /api/ai/providers/test` connectivity ping.

## Deployment default vs il provider personale di un utente

Le credenziali AI hanno due scope:

- **Deployment default (owner-managed).** Il proprietario configura un provider (o ne spedisce uno via
  `App:Ai:Providers[]` / la legacy `App:Ai:ApiKey`). Diventa il **default condiviso per ogni utente** —
  così un broker o provider di hosting può finanziare AI per tutti i loro utenti con **nessun setup
  per-utente e nessun limite per-utente**. Gestito tramite le route owner-only `/api/ai/providers` sopra.
- **Il provider personale di un utente (self-service).** Qualsiasi utente loggato può aggiungere il proprio
  provider sotto `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Quando presente, il loro **provider attivo proprio override il default
  del deployment per le loro funzionalità AI**; rimuoverlo ripristina il default.

**Ordine di risoluzione** (in `AiProviderStore`, per request user): la credenziale attiva dell'utente →
il default del deployment → la chiave di configurazione legacy → none (AI disabled). Esattamente una
credenziale è attiva **per scope** (un partial unique index per `OwnerUserId`), e ogni scope è risolto
indipendentemente, quindi un utente che attiva la propria chiave non disturba mai il default condiviso.
Background/non-Web contexts (no request user) risolvono sempre il default del deployment.

## Matrice capacità provider

Le capacità default per provider e sono owner-overridable. Quando una capacità è off la feature
**degrada, non lancia mai**: ricerca web silently droppata; vision restituisce un typed
capability-unsupported failure.

| Provider | Kind | Default base URL | Key richiesta | Web search | Vision | Note |
|---|---|---|---|---|---|---|
| AI locale integrata | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | ONNX GenAI model spedito, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (locale) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | via adapter compatibile OpenAI |
| LM Studio (locale) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | via adapter compatibile OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | via adapter compatibile OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | via adapter compatibile OpenAI |

Guide di setup per-provider complete (chiavi, URL, id modello, passi UI): vedere
[Provider AI — catalogo di configurazione](../deployment/ai-providers.md).

## AI locale integrata (spedita, default-on)

cMind spedisce un **vero LLM locale che gira in-process** tramite
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (un compact instruct model come
Phi-3.5-mini). Non ha bisogno **di nessuna API key e nessun servizio esterno**, e al primo avvio — quando nessun
provider è configurato e il white-label gate lo permette — viene **seeded e attivato automaticamente**, così
ogni deployment ha AI funzionante out of the box.

- La directory del modello (`genai_config.json` + tokenizer + pesi) è configurata da
  `App:Ai:BuiltIn:ModelPath` (default `models/onnx`, relativo alla directory base dell'app). Quando i file del modello
  sono assenti il provider **degrada a un failure typed con un hint di installazione** — non lancia mai,
  e il resto dell'app non è affected.
- Alimenta ogni feature AI testuale. Essendo un modello compact, è solo testo (no ricerca web lato server o
  vision) e la generazione è serializzata (una istanza modello, riusata dopo un lazy load).
- **Più modelli integrati possono coesistere.** Ogni modello scaricato risiede sotto `ModelPath/<key>`; un catalogo curato (Phi-3.5-mini predefinito, più Phi-3-mini-128k) può essere scaricato e scambiato da **Settings → AI**. Selezionando un submodel integrato lo carica in-process. Acquisire/bundle un modello: vedere [Provider AI → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Controlli white-label

Un deployment white-label restringe AI via `App:Branding` (applicato server-side su ogni provider upsert):

- `AllowBuiltInAi` (default `true`) — impostare `false` per **rimuovere completamente il modello integrato**.
- `AllowLocalProviders` (default `true`) — impostare `false` per proibire endpoint locali/self-hosted (loopback /
  private OpenAI-compatible, es. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (default empty = tutti) — elencare solo i tipi che il deployment sanziona (es.
  `["Anthropic","OpenAiCompatible"]`) per bloccare quali provider gli utenti possono aggiungere.
- `AllowAiModelManagement` (default `true`) — impostare `false` per nascondere **navigazione modelli**, lo **selettore modello per-page**, e **binding modelli per-feature**. Tutti sono owner-tunable a runtime da **Settings → Deployment** (sovrapposti live su `IOptionsMonitor`) e catalogati in `WhiteLabelCatalog`.

## Estendere: futuri modelli integrati

Il layer AI è **adapter-based e costruito per crescere**. Ogni provider è un `IAiProvider` selezionato da
`AiProviderKind`; la seam feature-facing (`IAiClient`/`AiFeatureService`) non cambia mai. Aggiungere un
nuovo runtime modello integrato più tardi (un altro modello ONNX, un diverso engine in-process, GGUF/llama.cpp
in-proc, ecc.) è un cambiamento localizzato: aggiungere un `AiProviderKind`, implementare un adapter
`IAiProvider`, registrarlo, e (opzionalmente) cablare default seeding + un'opzione dialog — nessun cambiamento
di feature, endpoint o tool MCP. Il provider ONNX integrato è l'implementazione di riferimento di questo pattern.

## Funzionalità

- **Build cBot** — prompt in inglese semplice → cBot runnable via **generate → build → AI-fix** self-repair loop (`build-strategy`), a `/ai/build`. Il **codice sorgente generato viene mostrato** quando la build finisce (con un pulsante di copia), insieme al build log (anch'esso copiabile) — sia in caso di successo *che* di fallimento. **Anche una build fallita viene salvata nei tuoi cBot** (con il nome univoco effettivo) e offre un link *Apri nell'editor* per correggere gli errori di compilazione e rifare la build, anziché perdere il lavoro.
- **Selezione modello per-page** — ogni pagina feature AI e dialog mostra un **selettore modello** che elenca i modelli che puoi usare (i tuoi provider + i default del deployment). Pre-seleziona il binding salvato della feature se impostato, altrimenti il modello **default**, e il modello che scegli si applica a quell'azione (inviato come `?modelId=` e forzato da `RoutingAiClient` per quella chiamata). Nascosto quando il deployment disabilita la gestione del modello.
- **Sfoglia e seleziona modelli, per feature** — sfoglia i modelli che un endpoint provider pubblicizza (`GET /v1/models` su LM Studio / Ollama / vLLM / llama.cpp, o il catalogo integrato) invece di digitare manualmente un id, e **vincolare ogni feature AI a un modello diverso** così più modelli servono feature diverse contemporaneamente (una feature non vincolata ricade al provider dello scope predefinito).
- **Ottimizzazione parametri** — closed loop: AI propone param set, ciascuno persistito + backtestato attraverso nodi (`optimize-run` / `optimize-params`).
- **Agente portfolio autonomo** — proposte mandate-driven con journal decisionale completo (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — servizio background `AiRiskGuard` valuta i bot in esecuzione, può **auto-stop** su rischio critico (opt-in).
- **Prop-firm exposure guardian** — limiti drawdown/exposure con auto-flatten.
- **Alert di mercato** — motore `AlertRule` con sentiment AI (grounding ricerca web dove il provider lo supporta).
- **Analisi** — revisione cBot, analisi backtest, post-mortem, sentiment di mercato, design chart-vision, curatela marketplace.

## Superfici

- Endpoint Web sotto `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Ogni endpoint feature accetta un opzionale `?modelId=<credential>` per eseguire quella chiamata su un modello scelto. Plus **scoperta modelli** (`/api/ai/models/probe`, `/api/ai/usable-models`) e **binding per-feature** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Tool MCP (`AiTools`) per client AI — vedere [mcp.md](mcp.md). La selezione provider è trasparente ai client MCP.
- **Nav group AI** — una Blazor **page per feature**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), più Portfolio Agent, Alerts, MCP Keys. Le pagine condividono `AiFeaturePageBase` + `AiOutputPanel` + un `AiModelSelect`; ciascuna mostra `AiFeatureNotice` quando nessun provider è configurato.
- **Settings → AI** (`/settings/ai`, solo owner) — lista provider con dialog **Add / edit provider** (kind, base URL con hint per-kind e preset one-click inclusi **Kimi/Moonshot**, Ollama e LM Studio, modello, key opzionale, toggle capacità, "set as default") e pulsante **Test connection**.

## Configurazione

`App:Ai` supporta sia la legacy single key che il multi-provider seeding:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — ancora onorati come provider Anthropic default.
- Multi-provider: `ActiveProvider` (kind) e `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importati nell'archivio all'avvio se ancora non esistono credenziali, così un
  team ops può spedire un deployment configurato (incl. local-LLM) puramente via appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` invariati. Per test/dev, una chiave di config
vive nel file [dev-credentials](../testing/dev-credentials.md) unificato sotto `Ai`.

## Affidabilità

Il provider è trattato come inaffidabile — nulla di ciò che fa può far andare giù l'app. Questo vale
identicamente per endpoint cloud e locali (un Ollama morto ritenta poi degrada esattamente come un Anthropic
throttled):

- **Graceful degradation.** Ogni failure mode (no provider, HTTP 4xx/5xx/429, timeout, body malformato,
  content vuoto, capacità unsupported) restituisce un typed `AiResult.Fail(reason)` — il client non lancia
  mai in una pagina, tool MCP o hosted service.
- **Pipeline di resilience.** `AddAiHttpClient` dà al singolo `HttpClient` AI condiviso un bounded retry su
  5xx transitori / failure di rete (exponential backoff + jitter) più generosi per-attempt e total
  timeout (`AiHttp`), riutilizzato da ogni adapter.

## Testing con il fake local LLM

Il layer AI è provato end-to-end **senza nessuna dipendenza esterna** da `FakeLocalLlmServer` — un tiny
endpoint **OpenAI-compatible** in-process che restituisce una canned reply deterministica, wire-identical a
Ollama/LM Studio/vLLM. Supporta:

- **Unit** — test di request-translation + response-parse per adapter, routing/capability degradation.
- **Integration** — adapter end-to-end compatibile OpenAI, la teoria di resilience parametrizzata attraverso
  ogni adapter, e gli **MCP AI tools**.
- **E2E** — `AiLocalFixture` boot dell'app puntata al fake server (o un provider **reale** quando lo
  sviluppatore imposta `AI_E2E_BASEURL` (+ opzionale `AI_E2E_API_KEY` / `AI_E2E_KIND` /
  `AI_E2E_MODEL`) — credenziali reali vincono) e guida ogni feature AI attraverso l'UI reale. Aggiungere
  o cambiare qualsiasi feature AI **richiede** un test E2E attraverso questa fixture (vedere il
  mandate del repo test). Una lane opt-in (`AI_LOCAL_LLM=1`) esegue una completion reale attraverso un
  **Ollama** Testcontainer.

## AI locale integrata — zero-setup per default

Il LLM locale ONNX integrato funziona out of the box: quando la sua directory modello è assente e
`App:Ai:BuiltIn:AutoDownload` è `true` (il default), l'app scarica il modello una volta in
background da `App:Ai:BuiltIn:DownloadBaseUrl`. Mentre il download gira, le chiamate AI (e **Test
connection** in Settings → AI) restituiscono un chiaro messaggio "model is downloading (first-time setup)"
piuttosto che un hard failure. Deployment air-gapped/metered impostano `AutoDownload=false` e
pre-provisionano la directory modello (`App:Ai:BuiltIn:ModelPath`). Il white-label gate
`App:Branding:AllowBuiltInAi` si applica ancora.

Lo scaricamento è anche **pre-riscaldato all'avvio** quando il modello integrato è il provider attivo, così è
pronto prima del primo click AI piuttosto che fallire quel click con "downloading…". **Settings → AI**
mostra lo stato di installazione live sulla scheda del provider integrato — *Modello pronto* / *Download del modello in corso…* /
*Modello non installato* / *Download fallito* — con un pulsante **Scarica modello** (o **Riprova download**) che
avvia il fetch in background one-time su richiesta (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`).
Abilitare il provider integrato da Settings riusa la riga già seminata invece di aggiungerne una duplicata,
così non entra mai in conflitto con il vincolo single-active-provider.
