# Kalendarz ekonomiczny

cMind wysyła własny **kalendarz ekonomiczny** — harmonogram wydań, aktualności, prognozy, rewizje i model
wpływu-driven-by-data — źródłem z **autorytetu głównego** (banki centralne i krajowe agencje statystyczne),
z **zerem zależności** od ForexFactory, FXStreet, Investing.com czy jakiekolwiek agregator. Jest
point-in-time poprawna, przechowuje ≥10 lat historii, i jest podpięta do handlu, publicznego API, MCP,
cBotów, AI, alertów i backtestów. Jest to zdekupowany moduł: może być wyłączony z zerem efektu na
trading core.

> **Status.** Domena core (model wpływu, country→symbol mapowanie, polityka news-window, point-in-time
> łańcuchy rewizji, dwa-tier gating) **i** persistence (schemat `calendar` Postgres, append-only
> read/write side, konektor FRED i config-gated worker ingestion) są zaimplementowane i przetestowane
> (unit + Testcontainers integration). JWT REST API, MCP tools i UI lądują w kolejnych fazach rollout
> opisanych poniżej.

## Co go wyróżnia

Powtarzające się skargi na wiodące kalendarze stały się naszymi ograniczeniami projektowymi:

- **Brak ciepych zmian impact-rating.** Nasz rating impaktu jest **deterministyczny, wersjonowany i
  audytowalny**. Każda zmiana to zarejestrowana rewizja z timestamp — nigdy cichy overwrite. Użytkownik
  może zobaczyć dokładnie *dlaczego* event jest High.
- **Jeden anchor UTC per event.** Każdy event jest zakotwiczony do pojedynczego instant UTC z harmonogramu
  głównego źródła; strefa czasowa źródła jest przechowywana, i per-user rendering używa jawnej
  IANA strefy czasowej z DST obsługiwanym przez zone database — nigdy ręczny toggle ±1h.
- **Pełne łańcuchy rewizji, wszędzie.** Oryginalna wartość i każda rewizja to first-class, exposed
  identycznie przez API, MCP i cBot surface.
- **≥10 lat historii, brak ściany.** Nierestrakcyjny range przeglądania; brak limitu 60-dniowego, brak
  registration gate.
- **Point-in-time przez konstrukcję.** Każdy fakt niesie `KnownAt` (kiedy *my* się o tym dowiedzieliśmy) i
  `EffectiveAt` (instant event). "Jak kalendarz wyglądał w czase T" to first-class query, więc
  backtested reguła wiadomości zachowuje się dokładnie jak live — brak look-ahead z używania
  revised wartości w historii.

## Model wpływu

Score wpływu to czista, deterministyczna funkcja w `[0, 100]`, grouped do Low / Medium / High /
Critical. Jego inputy to tylko dane znane w scoring time (brak future leak):

- **Series prior** — baseline waga per klasa indykatora (rate decision przeważaży CPI, które przeważyży
  mały survey).
- **Realized-volatility footprint** — median absolute return głównych affected symboli w oknie
  po tym wydaniem tej series w *przeszłości*: "to wydanie historycznie przesywa cenę tyle".
- **Surprise sensitivity** — jak silnie absolute surprise (z-score) historycznie koreluje z
  post-release move.

Score mieszaczy z fixed wagami i stamps `ImpactModelVersion`. Recompute jest jawną, zarejestrowaną
operacją która produkuje **nową rewizję** — nigdy mutate — więc score jest zawsze
reproducible z jego inputów.

## Country → currency → symbol mapowanie

Najczęściej cytowany algo integration papercut jest rozwiązany raz, jako czista funkcja: country mapuje
do jego waluty (każdy euro-area member fanuuje w EUR), i waluta mapuje do symboli watchlist cytuące
ją na każdej nodze. Więc **EURUSD jest affected przez zarówno EU jak i US events**; XAUUSD jest
USD-exposed; US500 mapuje na USD. To napędza news filter, affected-symbols resolution i blackout math.

## News-window polityka

`NewsWindowRule` to `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Pojedyncza,
shared, czysta implementacja odpowiada "czy instant T wewnątrz blackoutu dla symbolu S?" — używana
przez news filter cBot, copy-trade pausę i AI risk guard, więc nigdy nie mogą się różnić. Na
niepewności blackout odpowiedź domyślnie do skonfigurowanej conservative wartości (fail-closed
domyślnie) więc data gap nigdy nie silent green-light trading przez high-impact release.

## Point-in-time & rewizje

Aktualności, prognozy i impact scores to **append-only**. Każdy event owns ordered łańcuch rewizji,
monotonic w `KnownAt`:

- `Scheduled` — event był pierwszy zaplanowany (prior impact, brak actual).
- `Released` — pierwszy printed actual przyszedł.
- `Revised` — później revised wartość przyszła.
- `Rescheduled` — źródło przeniosło instant wydania (audytowalny, alertalny).
- `Rescored` — impact score był recomputed pod nową wersją modelu.

Querowanie `as of` przeszłego instant zwraca dokładnie rewizję znaną wtedy — gwarancja która
zabija look-ahead w backtested news rules.

## Forecast / consensus

Survey median ekonomistów to **nie** wolnie opublikowany przez główne źródła — to agregatorów
proprietary value-add, i my jej nie fabricujemy. Event schema niesie nullable `Forecast`; deployment
może wire licensed consensus feed przez opcjonalny port `IForecastProvider` (bring-your-own key, off
domyślnie). Poprzednie wartości i rewizje zawsze pochodzą z oficjalnego źródła.

## Źródła danych

Dwie zdekupowane warstwy, wszystkie główne — nigdy agregator:

- **Schedule / timing:** FRED release calendar; krajowe agencje statystyczne (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); harmonogramy banków centralnych (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Wartości faktyczne:** FRED (z vintage dates dla rewizji i point-in-time), plus BLS, BEA, Census,
  ECB SDW, Eurostat i OECD SDMX APIs.

Martwych źródło degraduje coverage dla **tylko tego źródła**; kalendarz drąży by serwować wszystko
inne i surface gap jako freshness metrykę.

## Rate limiting & backup plan

Zewnętrzni providery publikują rate limits (FRED pozwala ~120 żądań/minutę). Kalendarz jest zbudowany
żeby nigdy nie triped providera limit, i żeby bycie throttled albo cut off nigdy nie degraduje czytania:

- **Proaktywne throttling.** Każdy HTTP client źródła idzie przez shared, thread-safe rate gate
  który spacer outbound żądania do configured budżetu (`App:Calendar:FredRequestsPerMinute`, domyślnie
  100 — umyślnie pod providera ceiling). Żądania są queued i paced, nigdy bursted.
- **Honour `429 Retry-After`.** Jeśli provider kiedykolwiek zwróci `429 Too Many Requests`, gate
  cofa całe źródło o server-requested cooldown (lub `App:Calendar:RateLimitBackoff`, domyślnie 60s)
  przed następnym call — brak tight retry pętli.
- **Standardowa resilience.** Każdy source client również inherits app-wide resilience handler (retry
  z backoff + jitter, circuit breaker, timeouts), więc transient blips są absorbed i persistently
  failing źródło jest parked (jego coverage stanie się stale) bez affecting inne.
- **Backup plan — durable read-through cache.** Czytania są **nigdy** serwowane przez calling
  providera. Gdy range jest fetched jest persist append-only do Postgres i serwuje się stamtąd
  forever after (zobacz §"On-demand load"). Tak nawet gdy źródło jest rate-limited albo down,
  kalendarz drąży odpowiadać z cached, point-in-time-correct danych; brakujący span po prostu
  pozostaje uncovered i jest retried na następnym cyklu ingestion. Blackout odpowiedzi dodatkowo
  fail do conservative defaultu pod niepewnością, więc data gap nigdy green-light trading przez
  release.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursory) i
  "fetch span raz, nigdy znowu" cache trzyma aktualny request volume daleko poniżej każdego limitu
  w normalnej operacji — rate gate to safety net, nie wspólna ścieżka.

## Enable / disable

Dwie niezależne warstwy, dokładnie jak inne cMind features:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flipped z Features admin UI;
  brak redeploy, bierze efekt live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, domyślnie `true`).
  Reseller ustawia to `false` aby usunąć feature całkowicie; operator wtedy nie może re-enable go.

Efektywny stan to `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Gdy wyłączone,
entry nav jest ukryty i `/economic-calendar`, `/api/calendar/**` i MCP calendar tools zwracają
clean feature-disabled `404` — nigdy `500`. Persistowana historia jest retained na runtime toggle-off
więc re-enabling jest instant.

## Fazy rollout

- **P0 — domena core** *(zaimplementowana)*: agregaty, value objects, porty, model wpływu,
  country→symbol mapowanie, news-window polityka, two-tier gating, pełna unit suite.
- **P1 — persistence + jedno źródło** *(zaimplementowana)*: EF schemat `calendar` (własne tabele,
  append-only, hot indexes), read-through reader `IEconomicCalendar` z point-in-time `asOf`,
  idempotent append-only write service, FRED connector za resilient typed client, i config-gated
  ingestion worker; Testcontainers integration testy (persistence, PIT, idempotency, blackout).
- **P2 — publiczny JWT REST API + Web UI** *(zaimplementowana)*: wersjonowany, JWT-secured
  `/api/calendar/v1` API — client issuance, token exchange, i core read endpoints (events, history,
  series, surprises, next, blackout, affected-symbols, health) z scope enforcement i two-tier gating,
  integration-tested. Plus mobile-first **`/economic-calendar` strona** — gated, pełnie-lokalizowana
  (23 języki) agenda zbliżających się wydań jako phone-friendly karty z colour-banded impact chips
  i MudBlazor **filter dialog** (waluty + minimum impact + **From-date** picker aby jump do
  **każdej** przeszłej daty across pełna historia — brak limitu 60-dniowego, brak ściany); nav entry,
  smoke/mobile/a11y/E2E tested. Per-indykator **series historia strona** (`/economic-calendar/series/{code}`,
  linked z każdego event) listy series pełne print historia. Surprise charts + infinite-scroll
  browser następują.
- **P3 — więcej źródeł & warm-up** *(rozpoczęta)*: **core-series katalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → ich FRED ids) jest seeded automatycznie na startup,
  i one-time, idempotent, year-chunked **proaktywna backfill** ciągnąć ich ≥10-roku historii tak
  wspólny przypadek jest ciepły bez czekania na miss użytkownika. **Ingestion jest na domyślnie**
  (`App:Calendar:IngestionEnabled`, domyślnie `true`): **central-bank harmonogram źródło** potrzebuje
  **brak API key**, więc FOMC / ECB / BoE kalendarz decyzji populuje out of the box — backfill seeds
  te meeting daty across **zarówno recent historia i forward horizon**, więc przeglądanie *ostatnia
  miesiąc* (albo każde przeszłe okno) pokazuje meetings nawet zanim każdy FRED/BLS key jest
  skonfigurowany; wartość series fill w raz ich klucze są ustawione. Workers honoruję calendar
  two-tier gate — white-label deployment albo owner wyłączania economic-calendar feature zatrzymuje
  ingestion, i `App:Calendar:IngestionEnabled=false` wyłącza to jawnie. **Per-source freshness**
  jest teraz rzeczywista też: worker zapisuje każdy last successful poll źródła, consecutive-failure
  count i tripped-circuit flag (persisted w app settings, cross-process), i `/health` endpoint +
  `calendar_health` MCP tool report prawdziwą `stale` werdykt per źródło. **BLS** (2nd wartość
  źródło) i **central-bank harmonogram źródło** (FOMC / ECB / BoE daty decyzji, backfilled across
  historia i synced forward do horizon okna przez worker) są w. Ciągle do przyjścia: BEA/Census/ECB-SDW/
  Eurostat/OECD wartość źródła i reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(zaimplementowana — pełna read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated na feature)* i
  **alerts `EconomicEvent` trigger** *(zaimplementowana — `AlertRule` które fire N minut przed
  upcomingu release na/powyżej chosen impact, opcjonalnie narrowed do walut; evaluated przez existing
  alert worker z brak AI, de-duplicated per release; created via
  `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout gate **i copy-trade blackout
  pause** są w (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, domyślnie off: source otwórz którego
  symbol siedzi w Critical-impact blackout jest pominięty, byte-identical hot ścieżka gdy off).
  **Backtest event overlay** jest w — `GET /api/calendar/v1/for-symbol` i
  `calendar_events_for_symbol` MCP tool zwracają point-in-time-correct events affectujące symbol
  w oknie, i **instance/backtest report strona** renderuje high-impact releases które spadły
  wewnątrz backtest okna pod equity curve (tak autor widzi które trades lądowały na NFP),
  gated i lokalizowana. Cały plan jest teraz zaimplementowany.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Zobacz [cBot & REST API reference](calendar-cbot-api.md) dla integration surface.
