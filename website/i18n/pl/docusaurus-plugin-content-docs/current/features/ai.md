---
description: "cMind AI jest niezależny od dostawcy — Anthropic, OpenAI, Azure OpenAI, Google Gemini i każdy endpoint kompatybilny z OpenAI w tym modele lokalne (Ollama, LM Studio, vLLM). Wybierz dostawcę, model i endpoint; każda funkcja AI działa bez zmian."
---

# Funkcje AI

Warstwa AI cMind jest **niezależna od dostawcy**. Każda funkcja rozmawia z jednym neutralnym dla dostawcy seam
(`IAiClient.CompleteAsync`); **routing client** rozpoznaje aktywne poświadczenie dostawcy i wysyła
do pasującego wire adapter. Wybierz dostawcę + model + endpoint (i, jeśli dostawca go potrzebuje,
klucz); każda istniejąca funkcja działa bez zmian z tą samą bramy, szyfrowaniem, odpornością i
degradacją.

**Zasilane w pełni:** **wbudowany lokalny LLM dostarczany z aplikacją jest domyślnie włączony**
(Microsoft.ML.OnnxRuntimeGenAI, np. Phi-3-mini) — więc każde wdrożenie ma działające AI **bez klucza API
i bez usługi zewnętrznej**. Wdrożenie white-label może go usunąć i ograniczyć które dostawcy użytkownicy mogą
dodać. Poza wbudowanym, połącz każdego zewnętrznego dostawcę.

Obsługiwani dostawcy:

- **Wbudowany lokalny AI** (`BuiltInOnnx`) — w procesie model ONNX GenAI, brak klucza, dostarczony + domyślnie włączony.
- **Anthropic** (Claude — Messages API)
- **OpenAI** i **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Każdy endpoint kompatybilny z OpenAI**, w tym **modele lokalne** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) i chmury kompatybilne z OpenAI (**Kimi / Moonshot** na
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — wszystko przez jeden
  adapter kompatybilny z OpenAI, różniące się tylko base URL + model + klucz. Dialog dodawania dostawcy oferuje
  jednym klikiem **presets** (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio) które wypełniają
  base URL + przykładowy model.

Dokładnie **jeden** dostawca jest aktywny jednocześnie. Poświadczenia są przechowywane **szyfrowane**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
endpoint lokalny potrzebuje **bez klucza**. Z **żadnym** aktywnym dostawcą, każda funkcja zwraca wynik niedostępny
i reszta aplikacji działa bez zmian (brak klucza potrzebny do budowania, testowania lub uruchamiania platformy).

**Back-compat:** istniejące wdrożenie legacy `App:Ai:ApiKey` (lub stare szyfrowane `ai.api_key`
ustawienie) jest honorowane automatycznie jako domyślny aktywny dostawca **Anthropic** — zero działań potrzebnych.

AI nieskonfigurowany → strony AI przyciemniają akcje i pokazują baner plus jednorazowy prompt do dodania dostawcy w
**Ustawienia → AI** (`AiFeatureNotice`). Status na `GET /api/ai/status` (`{ enabled, kind, model }`);
dostawcy zarządzani (tylko właściciel) przez `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}` i `POST /api/ai/providers/test` connectivity ping.

## Domyślne wdrożenie vs własny dostawca użytkownika

Poświadczenia AI mają dwa zakresy:

- **Domyślne wdrożenie (zarządzane przez właściciela).** Właściciel konfiguruje dostawcę (lub wysyła jeden przez
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Staje się **wspólnym domyślnie dla każdego użytkownika** —
  więc broker lub hosting provider może finansować AI dla wszystkich swoich użytkowników z **żadną konfiguracją na użytkownika i żadnym limitem
  na użytkownika**. Zarządzane przez route właściciela `/api/ai/providers` powyżej.
- **Własny dostawca użytkownika (self-service).** Każdy zalogowany użytkownik może dodać swojego dostawcę pod
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Gdy obecny, ich **własny aktywny dostawca zastępuje domyślne wdrożenie dla ich funkcji AI**; usunięcie go wraca
  do domyślnego.

**Kolejność rozwiązania** (w `AiProviderStore`, na żądanie użytkownika): użytkownik's własne aktywne poświadczenie → domyślne
wdrożenie → legacy klucz config → brak (AI wyłączony). Dokładnie jedno poświadczenie jest aktywne
**per scope** (partial unique index per `OwnerUserId`), i każdy scope jest rozwiązywany niezależnie, więc
użytkownik aktywujący swój klucz nigdy nie zakłóca wspólnego domyślnego. Konteksty w tle/nie-Web (żaden użytkownik żądania)
zawsze rozwiązują domyślne wdrożenie.

## Matryca możliwości dostawcy

Możliwości domyślnie na dostawcy i są przesłoniane przez właściciela. Gdy możliwość jest wyłączona funkcja
**degraduje, nigdy nie wyrzuca**: web search jest cichо porzucany; vision zwraca typed
failure nieobsługiwaną możliwością.

| Dostawca | Rodzaj | Domyślny base URL | Klucz wymagany | Web search | Vision | Notatki |
|---|---|---|---|---|---|---|
| Wbudowany lokalny AI | `BuiltInOnnx` | n/a (w procesie) | nie | ✖ | ✖ | dostarczony model ONNX GenAI, domyślnie włączony |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | tak | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | tak | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | tak | ✅ | ✅ | ścieżka wdrożenia + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | tak | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (lokalny) | `OpenAiCompatible` | `http://localhost:11434/v1/` | nie | ✖ | model-zależny | przez adapter kompatybilny z OpenAI |
| LM Studio (lokalny) | `OpenAiCompatible` | `http://localhost:1234/v1/` | nie | model-zależny | model-zależny | przez adapter kompatybilny z OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | Twój URL | nie | ✖ | model-zależny | przez adapter kompatybilny z OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL dostawcy | tak | ✖ | model-zależny | przez adapter kompatybilny z OpenAI |

Pełne przewodniki instalacji na dostawcę (klucze, URLe, ID modeli, kroki UI): zobacz
[Dostawcy AI — katalog instalacji](../deployment/ai-providers.md).

## Wbudowany lokalny AI (dostarczony, domyślnie włączony)

cMind dostarcza **rzeczywisty lokalny LLM który uruchamia się w procesie** przez
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (kompaktowy model instrukcji taki jak
Phi-3.5-mini). Potrzebuje **brak klucza API i brak usługi zewnętrznej**, i na pierwszym starcie — gdy żaden dostawca nie jest
skonfigurowany i brama white-label zezwala — jest **seeded i aktywowany automatycznie**, więc każde
wdrożenie ma działające AI od razu z pudełka.

- Katalog modelu (`genai_config.json` + tokenizer + wagi) jest konfigurowany przez
  `App:Ai:BuiltIn:ModelPath` (domyślnie `models/onnx`, względnie do katalogu bazowego aplikacji). Gdy pliki modelu
  są nieobecne dostawca **degraduje do typed failure z wskazówką instalacji** — nigdy nie wyrzuca,
  i reszta aplikacji jest nienaruszona.
- Wspiera każdą funkcję AI tekstu. Będąc kompaktowym modelem, jest tekstem tylko (brak web search po stronie serwera ani
  vision) i generacja jest serializowana (jedna instancja modelu, ponownie użyta po leniwym ładowaniu).
- **Wiele wbudowanych modeli może istnieć równocześnie.** Każdy pobrany model znajduje się pod `ModelPath/<key>`; kuratowana katalog (Phi-3.5-mini domyślnie, plus Phi-3-mini-128k) można pobrać i przełączyć z **Ustawienia → AI**. Wybranie wbudowanego podmodelu ładuje go w procesie. Aby pobrać/spakować model: zobacz [Dostawcy AI → wbudowany](../deployment/ai-providers.md#wbudowany-lokalny-ai-onnx-wysłany).

## Kontrole white-label

Wdrożenie white-label ogranicza AI przez `App:Branding` (wymuszane po stronie serwera na każdym dostawcy upsert):

- `AllowBuiltInAi` (domyślnie `true`) — ustaw `false` aby **usunąć wbudowany model** całkowicie.
- `AllowLocalProviders` (domyślnie `true`) — ustaw `false` aby zakazać lokalnych/samodzielnie hostowanych endpoints (loopback /
  prywatne kompatybilne z OpenAI, np. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (domyślnie puste = wszystko) — wypisz tylko rodzaje które wdrożenie sankcjonuje (np.
  `["Anthropic","OpenAiCompatible"]`) aby zablokować które dostawcy użytkownicy mogą dodać.
- `AllowAiModelManagement` (domyślnie `true`) — ustaw `false` aby ukryć **przeglądanie modeli**, **selektor modeli na stronie**, i **wiązanie modelu na funkcję**. Wszystko jest tunable przez właściciela w runtime z **Ustawienia → Deployment** (nałożone na żywo na `IOptionsMonitor`) i skatalogowane w `WhiteLabelCatalog`.

## Rozszerzanie: przyszłe wbudowane modele

Warstwa AI jest **oparta na adapterze i zbudowana aby rosnąć**. Każdy dostawca jest `IAiProvider` wybrany przez
`AiProviderKind`; feature-facing seam (`IAiClient`/`AiFeatureService`) nigdy się nie zmienia. Dodawanie nowego
wbudowanego runtime modelu później (inny model ONNX, inny in-process engine, GGUF/llama.cpp
in-proc, itd.) to zlokalizowana zmiana: dodaj `AiProviderKind`, zaimplementuj jeden adapter `IAiProvider`,
zarejestruj go, i (opcjonalnie) wire default seeding + opcję dialog — brak funcji, endpoint, lub zmian narzędzia MCP. Wbudowany
dostawca ONNX jest implementacją referencyjną tego wzorca.

## Możliwości

- **Zbuduj cBot** — zwykły angielski prompt → uruchamiany cBot przez **generate → build → AI-fix** self-repair pętla (`build-strategy`), na `/ai/build`. **Wygenerowany kod źródłowy jest pokazywany** gdy build się kończy (z przyciskiem kopiuj), razem z logiem buildu (również kopiowalnym) — zarówno na sukces *jak i* na porażkę. **Nawet nieudany build jest zapisywany w twoich cBotach** (z rzeczywistą unikalną nazwą) i oferuje link *Otwórz w edytorze* abyś mógł naprawić błędy kompilacji i przebudować, zamiast tracić pracę.
- **Selektor modelu na stronie** — każda strona funkcji AI i dialog pokazuje **selektor modelu** wymieniający modele które możesz używać (twoi własni dostawcy + domyślne wdrożenia). Wstępnie wybiera wiązanie zapisane na funkcję jeśli ustawione, inaczej **domyślny** model, i model który wybrałeś stosuje się do tej jednej akcji (wysłane jako `?modelId=` i wymuszane przez `RoutingAiClient` dla tego wezwania). Ukryte gdy wdrożenie wyłącza zarządzanie modelami.
- **Przeglądaj i wybieraj modele dla każdej funkcji** — przeglądaj modele, które reklamuje endpoint dostawcy (`GET /v1/models` na LM Studio / Ollama / vLLM / llama.cpp, lub katalog wbudowany) zamiast ręcznego pisania id, i **wiąż każdą funkcję AI do innego modelu** aby kilka modeli obsługiwało różne funkcje jednocześnie (niezwiązana funkcja powraca do domyślnego dostawcy zakresu).
- **Optymalizacja parametrów** — zamknięta pętla: AI proponuje param sets, każdy persystentny + backtestowany przez nodes (`optimize-run` / `optimize-params`).
- **Agent portfolio autonomiczny** — proposal napędzane mandatem z pełnym dziennikiem decyzji (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` usługa w tle ocenia uruchomione boty, może **auto-stop** na krytycznym ryzyku (opt-in).
- **Prop-firm exposure guardian** — limity drawdown/exposure z auto-flatten.
- **Alerty rynkowe** — silnik `AlertRule` z sentiment AI (web-search grounded gdzie dostawca go wspiera).
- **Analiza** — przegląd cBot, analiza backtest, post-mortem, sentiment rynkowy, chart-vision design, marketplace curation.

## Powierzchnie

- Web endpoints pod `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Każdy endpoint funkcji akceptuje opcjonalne `?modelId=<credential>` aby uruchomić to jedno wezwanie na wybranym modelu. Plus **odkrywanie modeli** (`/api/ai/models/probe`, `/api/ai/usable-models`) i **wiązania na funkcję** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Narzędzia MCP (`AiTools`) dla klientów AI — zobacz [mcp.md](mcp.md). Wybór dostawcy jest transparentny dla klientów MCP.
- **AI** grupa nav — jedna strona Blazor **na funkcję**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), plus Portfolio Agent, Alerts, MCP Keys. Strony dzielą `AiFeaturePageBase` + `AiOutputPanel` + `AiModelSelect`; każda pokazuje `AiFeatureNotice` gdy żaden dostawca nie jest skonfigurowany.
- **Ustawienia → AI** (`/settings/ai`, tylko właściciel) — lista dostawcy z dialogiem **Dodaj / edytuj dostawcę** (rodzaj, base URL z wskazówkami per-kind i jednym klikiem **presets OpenAI-compatible** incl. **Kimi/Moonshot**, Ollama i LM Studio, model, opcjonalny klucz, toggles możliwości, "ustaw jako domyślny") i przycisk **Test połączenia**.

## Konfiguracja

`App:Ai` wspiera zarówno legacy pojedynczy klucz i multi-provider seeding:

- Legacy: `ApiKey`, `Model` (domyślnie `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — wciąż honorowane jako
  domyślny dostawca Anthropic.
- Multi-provider: `ActiveProvider` (rodzaj) i `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importowane do magazynu na starcie jeśli żadne poświadczenia nie istnieją, więc ops
  team może wysłać skonfigurowane (incl. local-LLM) wdrożenie czysto przez appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` bez zmian. Dla testów/dev, config key
żyje w ujednoliconym [dev-credentials file](../testing/dev-credentials.md) pod `Ai`.

## Niezawodność

Dostawca jest traktowany jako zawodny — nic co robi nie może zabrać aplikację w dół. To trzyma identycznie
dla chmury i lokalnych endpoints (martwy Ollama retry wtedy degraduje dokładnie jak throttled Anthropic):

- **Łagodna degradacja.** Każdy tryb niepowodzenia (brak dostawcy, HTTP 4xx/5xx/429, timeout, malformed body,
  pusta zawartość, nieobsługiwana możliwość) zwraca typed `AiResult.Fail(reason)` — klient nigdy
  nie wyrzuca do strony, narzędzia MCP, lub usługi hostowanej.
- **Pipeline odporności.** `AddAiHttpClient` daje jeden wspólny AI `HttpClient` bounded retry na
  transient 5xx / network failures (exponential backoff + jitter) plus hojne per-attempt i total
  timeouts (`AiHttp`), ponownie użyte przez każdy adapter.

## Testowanie z fake lokalnym LLM

Warstwa AI jest sprawdzona end-to-end **bez jakichkolwiek zależności zewnętrznych** przez `FakeLocalLlmServer` — mały
w procesie endpoint **kompatybilny z OpenAI** zwracający deterministyczną canned reply, wire-identical do
Ollama/LM Studio/vLLM. To wspiera:

- **Unit** — na adapter request-translation + response-parse testy, routing/capability degradacja.
- **Integracja** — adapter kompatybilny z OpenAI end-to-end, teoria odporności parametryzowana przez
  każdy adapter, i **narzędzia MCP AI**.
- **E2E** — `AiLocalFixture` uruchamia aplikację wskazywaną na fake server (lub **rzeczywisty** dostawca gdy
  developer ustawia `AI_E2E_BASEURL` (+ opcjonalny `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  rzeczywiste poświadczenia wygrywają) i prowadzi każdą funkcję AI przez rzeczywisty UI. Dodawanie lub zmiana każdej funkcji AI
  **wymaga** testu E2E przez ten fixture (zobacz test mandate repo). Opt-in lane
  (`AI_LOCAL_LLM=1`) uruchamia jeden prawdziwy completion przez Testcontainer **Ollama**.

## Wbudowany lokalny AI — zero-setup domyślnie

Wbudowany lokalny LLM ONNX działa od razu: gdy jego katalog modelu jest nieobecny i
`App:Ai:BuiltIn:AutoDownload` jest `true` (domyślnie), aplikacja pobiera model raz w tle
z `App:Ai:BuiltIn:DownloadBaseUrl`. Podczas pobierania, wezwania AI (i **Test connection** w Ustawienia → AI)
zwracają jasne "model jest pobierany (konfiguracja jednorazowa)" zamiast twardej porażki. Air-gapped/metered wdrożenia
ustawiają `AutoDownload=false` i pre-provision katalog modelu (`App:Ai:BuiltIn:ModelPath`). Brama white-label
`App:Branding:AllowBuiltInAi` wciąż się stosuje.

Pobieranie jest również **wstępnie ogrzewane przy starcie** gdy wbudowany model jest aktywnym dostawcą, więc jest gotowy przed pierwszym kliknięciem AI zamiast niepowodzenia tego kliknięcia z "downloading…". **Ustawienia → AI** wyświetla live stan instalacji na karcie wbudowanego dostawcy — *Model gotowy* / *Pobieranie modelu…* / *Model nie zainstalowany* / *Pobieranie nie powiodło się* — z przyciskiem **Pobierz model** (lub **Ponów pobieranie**) który aktywuje jednorazowe pobieranie w tle na żądanie (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Włączenie wbudowanego dostawcy z Ustawień ponownie używa już seeded wiersza zamiast dodawania duplikatu, więc nigdy nie konfliktuje z ograniczeniem single-active-provider.
