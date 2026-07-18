---
description: "AI cMind провайдер-agnostic — Anthropic, OpenAI, Azure OpenAI, Google Gemini и любые OpenAI-совместимые эндпоинты включая локальные модели (Ollama, LM Studio, vLLM). Выберите провайдера, модель и эндпоинт; каждая AI-функция работает без изменений."
---

# AI features

AI-слой cMind **провайдер-agnostic**. Каждая функция общается через единый провайдеро-нейтральный шов
(`IAiClient.CompleteAsync`); **роутирующий клиент** резолвит активные креденшелы провайдера и диспетчеризирует
к соотвествующему wire-адаптеру. Вы выбираете провайдера + модель + эндпоинт (и, если провайдеру нужен,
ключ); каждая существующая функция работает без изменений с тем же gating, шифрованием, resilience и
деградацией.

**Batteries included:** **встроенный локальный LLM поставляется с приложением и включён по умолчанию**
(Microsoft.ML.OnnxRuntimeGenAI, напр. Phi-3.5-mini) — поэтому каждый deployment имеет работающий AI **без API-ключа
и без внешнего сервиса**. White-label deployment может убрать его и ограничить, каких провайдеров пользователи могут
добавлять. Помимо встроенного, подключите любого внешнего провайдера.

Поддерживаемые провайдеры:

- **Встроенный локальный AI** (`BuiltInOnnx`) — in-process ONNX GenAI модель, без ключа, поставляется + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** и **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Любой OpenAI-совместимый эндпоинт**, включая **локальные модели** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) и OpenAI-совместимые облака (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — через один OpenAI-совместимый адаптер, отличающийся только base URL + модель + ключ.

Ровно **один** провайдер активен в любой момент. Креденшелы хранятся **зашифрованно**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
локальному эндпоинту **не нужен** ключ. При **отсутствии** активного провайдера каждая функция возвращает disabled
result и остальное приложение работает без изменений (без ключа можно build, test и run платформы).

**Back-compat:** существующий deployment's legacy `App:Ai:ApiKey` (или старый зашифрованный
`ai.api_key` setting) автоматически чтится как default active **Anthropic** провайдер — ничего делать не нужно.

AI не настроен → AI-страницы dims actions и показывают баннер плюс one-time prompt добавить провайдера в
**Settings → AI** (`AiFeatureNotice`). Status на `GET /api/ai/status` (`{ enabled, kind, model }`);
провайдеры управляются (owner-only) через `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}` и `POST /api/ai/providers/{id}/test` connectivity ping.

## Deployment default vs собственный провайдер пользователя

AI-креденшелы имеют две области видимости:

- **Deployment default (owner-managed).** Владелец настраивает провайдера (или поставляет через
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Он становится **shared default для каждого пользователя** —
  поэтому брокер или хостинг-провайдер может fund AI для всех своих пользователей **без per-user setup и
  без per-user лимита**. Управляется через owner-only `/api/ai/providers` routes.
- **Собственный провайдер пользователя (self-service).** Любой авторизованный пользователь может добавить своего провайдера
  через `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Когда присутствует, **его активный провайдер переопределяет deployment
  default** для его собственных AI-функций; удаление возвращает к default.

**Порядок разрешения** (в `AiProviderStore`, per request user): собственный активный credential пользователя →
deployment default → legacy config key → none (AI disabled). Ровно один credential активен **per область**
(частичный уникальный индекс per `OwnerUserId`), каждая область разрешается независимо, поэтому
активация своего ключа пользователем никогда не беспокоит shared default. Фоновые/не-Web контексты (нет
request user) всегда разрешают deployment default.

## Матрица возможностей провайдеров

Возможности по умолчанию per провайдер и переопределяемы владельцем. Когда возможность выключена, функция
**деградирует, не бросает**: web search тихо дропается; vision возвращает typed
capability-unsupported failure.

| Провайдер | Kind | Default base URL | Ключ нужен | Web search | Vision | Notes |
|---|---|---|---|---|---|---|
| Built-in local AI | `BuiltInOnnx` | n/a (in-process) | нет | ✖ | ✖ | shipped ONNX GenAI model, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | да | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | да | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | да | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | да | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | нет | ✖ | model-dependent | via OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | нет | model-dependent | model-dependent | via OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | нет | ✖ | model-dependent | via OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | да | ✖ | model-dependent | via OpenAI-compatible adapter |

Полные per-провайдер гайды (ключи, URL, id модели, шаги UI): см.
[AI providers — setup catalog](../deployment/ai-providers.md).

## Встроенный локальный AI (поставляется, default-on)

cMind поставляет **реальную локальную LLM, работающую in-process** через
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (компактная instruct-модель типа
Phi-3.5-mini). Ей **не нужен API-ключ и внешний сервис**, и при первом старте — когда провайдер не
настроен и white-label gate позволяет — она **автоматически seed'ится и активируется**, поэтому каждый
deployment имеет работающий AI из коробки.

- Директория модели (`genai_config.json` + tokenizer + weights) настраивается через
  `App:Ai:BuiltIn:ModelPath` (по умолчанию `models/onnx`, относительно базовой директории приложения). Когда файлы модели
  отсутствуют, провайдер **деградирует до typed failure с install hint** — никогда не бросает,
  остальное приложение не затронуто.
- Питает каждую текстовую AI-функцию. Будучи компактной моделью, только текст (без server-side web search или
  vision) и генерация сериализована (один экземпляр модели, переиспользуется после lazy load).
- **Несколько встроенных моделей могут сосуществовать.** Каждая загруженная модель находится под `ModelPath/<key>`; курируемый каталог (Phi-3.5-mini по умолчанию, плюс Phi-3-mini-128k) можно загрузить и переключить из **Settings → AI**. Выбор встроенной подмодели загружает её in-process. Получить/бандлировать модель: см. [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label контроли

White-label deployment ограничивает AI через `App:Branding` (enforced server-side на каждом upsert провайдера):

- `AllowBuiltInAi` (по умолчанию `true`) — установить `false` чтобы **убрать встроенную модель** полностью.
- `AllowLocalProviders` (по умолчанию `true`) — установить `false` чтобы запретить локальные/self-hosted эндпоинты (loopback /
  private OpenAI-compatible, напр. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (по умолчанию empty = все) — список только тех kinds, которые deployment разрешает (напр.
  `["Anthropic","OpenAiCompatible"]`) для lock down каких провайдеров пользователи могут добавлять.
- `AllowAiTasks` (по умолчанию `true`) — установить `false` чтобы **убрать фоновую AI-задачу** функцию (страница
  `/ai/tasks` и API задач возвращают 404; runner прекращает claiming); синхронные AI-функции остаются работать.
- `AllowAiModelManagement` (по умолчанию `true`) — установить `false` чтобы скрыть **просмотр моделей** и **per-feature
  привязку моделей**. Оба tuneable владельцем at runtime из **Settings → Deployment** (overlaid live на
  `IOptionsMonitor`) и catalogued в `WhiteLabelCatalog`.

## Расширение: будущие встроенные модели

AI-слой **адаптерный и построен для роста**. Каждый провайдер — это `IAiProvider`, выбираемый по
`AiProviderKind`; feature-facing seam (`IAiClient`/`AiFeatureService`) никогда не меняется. Добавление новой
встроенной модели runtime позже (ещё одна ONNX модель, другая in-process engine, GGUF/llama.cpp
in-proc и т.д.) — это локализованное изменение: добавить `AiProviderKind`, реализовать один `IAiProvider`
адаптер, зарегистрировать, и (опционально) подключить default seeding + опцию диалога — без изменений
функций, эндпоинтов или MCP tools. Встроенный ONNX провайдер — это reference implementation этого паттерна.

## Возможности

- **Build cBot** — plain-English prompt → исполняемый cBot через **generate → build → AI-fix** self-repair loop (`build-strategy`), на `/ai/build`. **Сгенерированный исходный код отображается** по завершении сборки (с кнопкой копирования), рядом с логом сборки — как при успехе, *так и* при ошибке — чтобы вы всегда видели, что писал AI, а не только ошибки.
- **Фоновые AI-задачи** — запустить долгоживущую AI-работу (напр. собрать cBot) с моделью(ями) вашего выбора, затем оставить страницу и вернуться к результату. Выберите несколько моделей для сравнения — каждая работает как своя задача (`/ai/tasks`). Фоновый worker claims задачи на self-healing lease (reclaimed если узел погибает) и streams progress в per-task activity log.
- **Просмотр и выбор моделей, per feature** — browse моделей что провайдер-эндпоинт advertises (`GET /v1/models` на LM Studio / Ollama / vLLM / llama.cpp, или встроенный каталог) вместо hand-typing id, и **привязать каждую AI-функцию к другой модели** так несколько моделей serve различные функции одновременно (unbind функция falls back к scope's active провайдер).
- **Parameter optimization** — closed loop: AI предлагает наборы параметров, каждый персистится + бэктестится across nodes (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — мандат-драйвен proposals с полным decision journal (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` фоновый сервис оценивает работающие боты, может **auto-stop** при критическом риске (opt-in).
- **Prop-firm exposure guardian** — лимиты drawdown/exposure с auto-flatten.
- **Market alerts** — `AlertRule` engine с AI sentiment (web-search grounded где провайдер поддерживает).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation.

## Поверхности

- Web эндпоинты под `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …), плюс **фоновые задачи** (`/api/ai/tasks` create/list/detail/cancel/delete), **обнаружение моделей** (`/api/ai/models/probe`, `/api/ai/usable-models`) и **per-feature привязки** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP tools (`AiTools`) для AI-клиентов — см. [mcp.md](mcp.md). Выбор провайдера прозрачен для MCP-клиентов.
- **AI** nav группа — одна Blazor **страница per функция**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), **AI Tasks** (`/ai/tasks`), плюс Portfolio Agent, Alerts, MCP Keys. Страницы разделяют `AiFeaturePageBase` + `AiOutputPanel`; каждая показывает `AiFeatureNotice` когда провайдер не настроен.
- **Settings → AI** (`/settings/ai`, owner-only) — список провайдеров с диалогом **Add / edit provider** (kind, base URL с per-kind hints включая Ollama/LM Studio localhost preset, модель, опциональный ключ, переключатели возможностей, "set active") и кнопкой **Test connection**.

## Конфигурация

`App:Ai` поддерживает и legacy single key и multi-provider seeding:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — всё ещё чтится как
  default Anthropic провайдер.
- Multi-provider: `ActiveProvider` (kind) и `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — импортируются в store при старте если креденшелов ещё нет, поэтому
  ops team может поставить сконфигурированный (вкл. local-LLM) deployment чисто через appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` без изменений. Для tests/dev, config key
живёт в unified [dev-credentials file](../testing/dev-credentials.md) под `Ai`.

## Надёжность

Провайдер рассматривается как ненадёжный — ничего что он делает не может уронить приложение. Это
держится идентично для cloud и локальных эндпоинтов (мёртвый Ollama retry'ит затем деградирует точно
как throttled Anthropic):

- **Graceful degradation.** Каждый mode отказа (нет провайдера, HTTP 4xx/5xx/429, timeout, malformed body,
  empty content, unsupported capability) возвращает typed `AiResult.Fail(reason)` — клиент никогда
  не бросает в страницу, MCP tool или hosted service.
- **Resilience pipeline.** `AddAiHttpClient` даёт одному shared AI `HttpClient` bounded retry на
  transient 5xx / network failures (exponential backoff + jitter) плюс generous per-attempt и total
  timeouts (`AiHttp`), переиспользуемый каждым адаптером.

## Тестирование с fake local LLM

AI-слой доказан end-to-end **без любого внешнего зависимо** через `FakeLocalLlmServer` — крошечный
in-process **OpenAI-совместимый** эндпоинт, возвращающий детерминированный canned reply, wire-identical к
Ollama/LM Studio/vLLM. Он обеспечивает:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation.
- **Integration** — OpenAI-compatible adapter end-to-end, parameterized resilience theory across
  every adapter, и **MCP AI tools**.
- **E2E** — `AiLocalFixture` загружает приложение, указывающее на fake server (или **реального** провайдера когда
  разработчик устанавливает `AI_E2E_BASEURL` (+ опционально `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  реальные креденшелы выигрывают) и гоняет каждую AI-функцию через реальный UI. Добавление или изменение любой AI-функции
  **требует** E2E-тест через эту фикстуру (см. repo test mandate). Opt-in lane
  (`AI_LOCAL_LLM=1`) гоняет одно реальное завершение через **Ollama** Testcontainer.

## Встроенный локальный AI — нулевая настройка по умолчанию

Встроенный ONNX локальный LLM работает из коробки: когда директория моделей отсутствует и
`App:Ai:BuiltIn:AutoDownload` равно `true` (по умолчанию), приложение загружает модель один раз в
фоне из `App:Ai:BuiltIn:DownloadBaseUrl`. Пока загрузка работает, AI-вызовы (и **Test
connection** в Settings → AI) возвращают четкое "модель загружается (first-time setup)" сообщение
вместо hard failure. Air-gapped/metered deployments устанавливают `AutoDownload=false` и
pre-provision директорию моделей (`App:Ai:BuiltIn:ModelPath`). White-label
`App:Branding:AllowBuiltInAi` gate всё ещё применяется.

Загрузка также **предварительно подготавливается при старте**, когда встроенная модель является активным провайдером, поэтому она готова перед первым нажатием AI вместо того чтобы при этом показать «загружается…». **Settings → AI** показывает live состояние установки на карточке встроенного провайдера — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — с кнопкой **Download model** (или **Retry download**), которая запускает одноразовую фоновую загрузку по требованию (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Включение встроенного провайдера из Settings переиспользует уже готовую строку вместо добавления дубликата, поэтому никогда не конфликтует с ограничением одного активного провайдера.
