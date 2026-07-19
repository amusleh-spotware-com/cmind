---
description: "cMind AI је провајдер-агностик — Anthropic, OpenAI, Azure OpenAI, Google Gemini, и било која OpenAI-компатибилна крајња тачка укључујући локалне моделе (Ollama, LM Studio, vLLM). Изаберите провајдера, модел и крајњу тачку; свака AI функција ради непромењено."
---

# AI функције

AI слој cMind-а је **провајдер-агностик**. Свака функција говори са једном провајдер-неутралном спојницом
(`IAiClient.CompleteAsync`); **рутирајући клијент** решава активне креденцијале провајдера и шаље
на одговарајући wire адаптер. Ви бирате провајдера + модел + крајњу тачку (и, ако провајдер треба,
кључ); свака постојећа функција ради непромењено са истом контролом приступа, шифровањем, отпорношћу и
деградацијом.

**Батерије укључене:** **уграђени локални LLM се испоручује са апликацијом и укључен је по подразумевању**
(Microsoft.ML.OnnxRuntimeGenAI, нпр. Phi-3-mini) — тако да сваки deployment има радни AI **без API кључа
и без екстерног сервиса**. White-label deployment може да га уклони и ограничи које провајдере корисници могу
да додају. Поред уграђеног, повежите било ког екстерног провајдера.

Подржани провајдери:

- **Уграђени локални AI** (`BuiltInOnnx`) — in-process ONNX GenAI модел, без кључа, испоручен + укључен по подразумевању.
- **Anthropic** (Claude — Messages API)
- **OpenAI** и **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Било која OpenAI-компатибилна крајња тачка**, укључујући **локалне моделе** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) и OpenAI-компатибилне cloud-ове (**Kimi / Moonshot** на
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — све преко једног
  OpenAI-компатибилног адаптера, различито само по base URL + модел + кључ. Дијалог за додавање провајдера нуди
  **one-click preset-е** (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio) који попуњавају
  base URL + sample модел.

Тачно **један** провајдер је активан у исто време. Креденцијали се чувају **шифровано**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
локална крајња тачка треба **без кључа**. Када **нема** активног провајдера, свака функција враћа онемогућен
result и остатак апликације ради непромењено (кључ није потребан за изградњу, тестирање или покретање платформе).

**Повратна компатибилност:** постојећи `App:Ai:ApiKey` legacy deployment-а (или стари шифровани `ai.api_key`
подешавање) се аутоматски поштује kao подразумевани активан **Anthropic** провајдер — није потребна никаква акција.

AI неконфигурисан → AI странице затамње акције и приказују банер плус једнократни позив да додате провајдера у
**Settings → AI** (`AiFeatureNotice`). Статус на `GET /api/ai/status` (`{ enabled, kind, model }`);
провајдери се управљају (owner-only) преко `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, и `POST /api/ai/providers/test` connectivity ping.

## Deployment подразумевано vs сопствени провајдер корисника

AI креденцијали има два scope-а:

- **Deployment подразумевано (owner-managed).** Власник конфигурише провајдера (или испоручује преко
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Он постаје **дељени подразумевани за сваког корисника** —
  тако да брокер или hosting провајдер може да финансира AI за све своје кориснике **без per-user подешавања и без
  per-user лимита**. Управља се преко owner-only `/api/ai/providers` рута горе.
- **Сопствени провајдер корисника (self-service).** Било koji пријављени корисник може да дода сопственог провајдера преко
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Када постоји, **сопствени активан провајдер превазилази deployment
  подразумевани за њихове сопствене AI функције**; уклањање враћа на подразумевани.

**Редослед резолуције** (у `AiProviderStore`, по request кориснику): сопствена активна креденцијала корисника → deployment
подразумевано → legacy config кључ → ништа (AI онемогућен). Тачно једна креденцијала је активна
**по scope** (парцијални unique index по `OwnerUserId`), и сваки scope се разрешава независно, тако да активирање
сопственог кључа никада не дира дељени подразумевани. Background/non-Web контексти (без request корисника) увек
разрешавају deployment подразумевани.

## Матрица могућности провајдера

Могућности су подразумеване по провајдеру и могу се превазићи од стране власника. Када је могућност искључена, функција
**деградира, никада не избацује**: web претраживање се тихо одбацује; vision враћа типизиран
capability-unsupported неуспех.

| Провајдер | Врста | Подразумевани base URL | Кључ потребан | Web претрага | Vision | Напомене |
|---|---|---|---|---|---|---|
| Уграђени локални AI | `BuiltInOnnx` | н/а (in-process) | не | ✖ | ✖ | испоручени ONNX GenAI модел, подразумевано укључен |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | да | ✅ | ✅ | Messages API, `web_search` алат |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | да | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | да | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | да | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (локални) | `OpenAiCompatible` | `http://localhost:11434/v1/` | не | ✖ | model-dependent | преко OpenAI-компатибилног адаптера |
| LM Studio (локални) | `OpenAiCompatible` | `http://localhost:1234/v1/` | не | model-dependent | model-dependent | преко OpenAI-компатибилног адаптера |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | не | ✖ | model-dependent | преко OpenAI-компатибилног адаптера |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | да | ✖ | model-dependent | преко OpenAI-компатибилног адаптера |

Пуни водичи за подешавање по провајдеру (кључevi, URL-ови, model id-ови, UI кораци): види
[AI providers — setup catalog](../deployment/ai-providers.md).

## Уграђени локални AI (испоручен, подразумевано укључен)

cMind испоручује **праве локални LLM који ради in-process** преко
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (компактан instruct модел kao што je
Phi-3-mini). Треба му **без API кључа и без екстерног сервиса**, и при првом покретању — када провајдер није
конфигурисан и white-label gate дозвољава — **семљи се и активира аутоматски**, тако да сваки
deployment има радни AI од првог тренутка.

- Директоријум модела (`genai_config.json` + tokenizer + weights) се конфигурише са
  `App:Ai:BuiltIn:ModelPath` (подразумевано `models/onnx`, релативно према app base директоријуму). Када model
  фајлови недостају, провајдер **деградира до типизираног неуспеха са инсталационим наговештајем** — никада не избацује,
  и остатак апликације је нетакнут.
- Покреће сваку text AI функцију. Будући компактан модел, само је text-only (без server-side web претраге или
  vision) и генерација је сериализована (једна model инстанца, поново коришћена након lazy load-а).
- **Више уграђених модела може сосуществовати.** Сваки преузети модел живи под `ModelPath/<key>`; куриран каталог
  (Phi-3.5-mini подразумевано, плус Phi-3-mini-128k) се може преузети и замењивати из **Settings → AI**. Избор
  уграђеног подмодела учитава га in-process. Прибави/свучи модел: види [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label контроле

White-label deployment ограничава AI преко `App:Branding` (enforced server-side на сваком provider upsert):

- `AllowBuiltInAi` (подразумевано `true`) — постави `false` да **у потпуности уклони уграђени модел**.
- `AllowLocalProviders` (подразумевано `true`) — постави `false` да забрани локалне/self-hosted ендпоинте (loopback /
  приватни OpenAI-компатибилни, нпр. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (подразумевано празно = све) — наведи само врсте које deployment одобрава (нпр.
  `["Anthropic","OpenAiCompatible"]`) да закљуцаш које провајдере корисници могу да додају.
- `AllowAiModelManagement` (подразумевано `true`) — постави `false` да сакријеш **model browsing**, **per-page model
  selector**, и **per-feature model binding**. Сви су owner-tunable у време извршавања из **Settings →
  Deployment** (преклоњени live на `IOptionsMonitor`) и каталогирани у `WhiteLabelCatalog`.

## Проширивање: будући уграђени модели

AI слој је **адаптер-базиран и изграђен за раст**. Сваки провајдер је `IAiProvider` селектован од стране
`AiProviderKind`; feature-facing seam (`IAiClient`/`AiFeatureService`) се никада не мења. Додавање новог
уграђеног model runtime-а касније (други ONNX модел, други in-process engine, GGUF/llama.cpp
in-proc, итд.) је локализована промена: додај `AiProviderKind`, имплементирај један `IAiProvider` адаптер,
региструј га, и (опционо) повежи подразумевано семљивање + опцију дијалога — без промена функције, ендпоинта или MCP алата.
Уграђени ONNX провајдер је референтна имплементација овог обрасца.

## Могућности

- **Build cBot** — plain-English prompt → покретачки cBot преко **generate → build → AI-fix** self-repair петље (`build-strategy`), на `/ai/build`. **Генерисани изворни код је приказан** када се градња заврши (са дугметом копирај), заједно са логом градње (такође копирајт-е) — на успех *и* на неуспех. **Чак и неуспешна градња се чува у вашим cBot-има** (са правим јединственим именом) и нуди **Open in editor** везу како бисте исправили compile грешке и поново градили.
- **Per-page model selection** — свака AI функција страница и дијалог показује **model selector** који наводи моделе које можеш користити (твоји сопствени провајдери + deployment подразумевани). Пре-селектује binding сачуван за функцију ако је постављен, иначе **default** модел, и модел који бираш се примењује нату једну акцију (слан као `?modelId=` и принуђен од стране `RoutingAiClient` за тај позив). Скривен када deployment деактивира управљање моделима.
- **Browse & select models, per feature** — брши моделе које крајња тачка провајдера оглашава (`GET /v1/models` на LM Studio / Ollama / vLLM / llama.cpp, или уграђени каталог) уместо ручног куцања id-а, и **везати сваку AI карактеристику другачијем моделу** тако да неколико модела служи различитим карактеристикама одједном (неповезана карактеристика пада на подразумевани провајдер опсега).
- **Parameter optimization** — затворена петља: AI предлаже param set-ове, сваки перзистован + backtested преко чворова (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — mandate-driven предлози са пуним decision journal-ом (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background сервис процењује активне ботове, може **аутоматски зауставити** на критичан ризик (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure лимити са аутоматским изравнавањем.
- **Market alerts** — `AlertRule` engine са AI sentiment-ом (web-search grounded тамо где провајдер подржава).
- **Analysis** — cBot рецензија, backtest анализа, post-mortems, market sentiment, chart-vision дизајн, marketplace curation.

## Површине

- Web ендпоинти под `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Свака ендпоинт функције прихвата опционални `?modelId=<credential>` да покреће тај један позив на одабраном моделу. Плус **model discovery** (`/api/ai/models/probe`, `/api/ai/usable-models`) и **per-feature bindings** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP алати (`AiTools`) за AI клијенте — види [mcp.md](mcp.md). Избор провајдера је транспарентан MCP клијентима.
- **AI** навигациона група — једна Blazor **страница по функцији**: Изгради cBot (`/ai/build`), Рецензија (`/ai/review`), Дебата (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Оптимизуј (`/ai/optimize`), плус Portfolio Agent, Alerts, MCP Keys. Странице деле `AiFeaturePageBase` + `AiOutputPanel` + `AiModelSelect`; свака приказује `AiFeatureNotice` када провајдер није конфигурисан.
- **Settings → AI** (`/settings/ai`, owner-only) — листа провајдера са **Add / edit provider дијалогом** (врста, base URL са per-kind наговештајима и **one-click preset-има** укључујући **Kimi/Moonshot**, Ollama и LM Studio, модел, опциони кључ, capability toggle-ови, "set as default") и **Test connection** дугметом.

## Конфигурација

`App:Ai` подржава и legacy један кључ и multi-provider семљивање:

- Legacy: `ApiKey`, `Model` (подразумевано `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — и даље се поштује као
  подразумевани Anthropic провајдер.
- Multi-provider: `ActiveProvider` (врста) и `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — увезено у store при покретању ако креденцијали еще не постоје, тако да
  ops тим може да испоручи конфигурисан (укључујући локални-LLM) deployment чисто преко appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` непромењени. За тестове/dev, config кључ
живи у унификованој [dev-credentials file](../testing/dev-credentials.md) под `Ai`.

## Поузданост

Провајдер се третира као непоуздан — ништа што он ради не може да обори апликацију. Ово важи идентично
за cloud и локалне ендпоинте (мртав Ollama ретрија онда деградира тачно као throttle-ован Anthropic):

- **Graceful деградација.** Сваки mód неуспеха (без провајдера, HTTP 4xx/5xx/429, timeout, malformed body,
  празан садржај, неподржана могућност) враћа типизиран `AiResult.Fail(reason)` — клијент никада
  не избацује у страницу, MCP алат или hosted сервис.
- **Resilience pipeline.** `AddAiHttpClient` даје једном дељеном AI `HttpClient`-у ограничени retry на
  транзијентним 5xx / network неуспесима (exponential backoff + jitter) плус великодушни per-attempt и укупни
  timeout-ови (`AiHttp`), поново коришћени од стране сваког адаптера.

## Тестирање са лажним локалним LLM

AI слој се доказује end-to-end **без било које екстерне зависности** од стране `FakeLocalLlmServer` — ситан
in-process **OpenAI-компатибилни** ендпоинт који враћа детерминистички canned reply, wire-identical to
Ollama/LM Studio/vLLM. Он подржава:

- **Unit** — per-adapter request-translation + response-parse тестови, рутирање/capability деградација.
- **Integration** — OpenAI-компатибилни адаптер end-to-end, параметризована resilience теорија преко
  сваког адаптера, и **MCP AI алати**.
- **E2E** — `AiLocalFixture` покреће апликацију усмерену на лажни сервер (или **правог** провајдера када
  developer постави `AI_E2E_BASEURL` (+ опциоо `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  прави креденцијали побеђују) и вози сваку AI функцију кроз реални UI. Додавање или мењање било које AI функције
  **захтева** E2E тест кроз ову fixture (види repo test mandate). Opt-in lane
  (`AI_LOCAL_LLM=1`) покреће једно право completion преко **Ollama** Testcontainer-а.

## Уграђени локални AI — nulla-setup по подразумевању

Уграђени ONNX локални LLM ради out of the box: када његов директоријум модела недостаје и
`App:Ai:BuiltIn:AutoDownload` је `true` (подразумевано), апликација преузима модел једном у
позадини са `App:Ai:BuiltIn:DownloadBaseUrl`. Док се преузимање извршава, AI позиви (и **Test
connection** у Settings → AI) враћају јасну поруку "model is downloading (first-time setup)"
уместо хард неуспеха. Air-gapped/metered deployments постављају `AutoDownload=false` и
пре-обезбеђују директоријум модела (`App:Ai:BuiltIn:ModelPath`). White-label
`App:Branding:AllowBuiltInAi` gate се и даље примењује.

Преузимање је такође **пре-загрејано при покретању** када је уграђени модел активан провајдер, тако да је спреман прије первог AI клика уместо да клик пропадне са "downloading…". **Settings → AI** приказује стање live инсталирања на картици уграђеног провајдера — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — са дугметом **Download model** (или **Retry download**) које активира односи background преузимање на захтјев (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Активирање уграђеног провајдера из Settings поново користи већ посијан ред уместо додавања дупликата, тако да никада не дође у сукоб са ограничењем једног активног провајдера.
