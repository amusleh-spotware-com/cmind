---
description: "cMind spedisce il proprio calendario economico — schedule di rilascio, actual, forecast, revisioni e un modello di impatto data-driven — sourced da primary authorities, con zero dipendenza da aggregatori."
---

# Calendario economico

cMind spedisce il **proprio** calendario economico — schedule di rilascio, actual, forecast, revisioni e un
modello di impatto data-driven — sourced da **primary authorities** (banche centrali e agenzie statistiche
nazionali), con **zero dipendenza** da ForexFactory, FXStreet, Investing.com o qualsiasi aggregatore. È
point-in-time correct, mantiene ≥10 anni di storia, ed è cablato in trading, la public API, MCP, cBots,
AI, alerts e backtest. È un modulo decoupled: può essere disabilitato con zero effetto sul trading core.

> **Status.** P0–P4 sono implementati e rilasciati. Il domain core, la persistence (EF `calendar`
> schema, read/write append-only, sorgenti FRED + BLS + calendar banche centrali, ingestion worker
> gated da config con tracciamento della freschezza per sorgente), la JWT REST API versionata, l'UI
> mobile-first `/economic-calendar`, i tool MCP, la cBot JWT API, gli alert per eventi ad alto impatto,
> la pausa copy-trade news-blackout, l'overlay di eventi backtest, lo stream SSE, i webhook firmati
> HMAC e il `CmindCalendarClient` tipizzato sono tutti implementati e testati in integrazione. Gli
> extra P5 (analisi delle sorprese, export iCal/CSV, ricerca per parole chiave, consenso pluggable)
> sono gli elementi rimanenti — vedi le fasi di rollout sotto.

## Cosa lo differenzia

I reclami ricorrenti contro i calendari leading sono diventati i nostri design constraint:

- **Nessun silent impact-rating change.** Il nostro impact rating è **deterministico, versionato e auditabile**.
  Ogni cambiamento è una revision registrata con un timestamp — mai un silent overwrite. Un
  utente può vedere esattamente *perché* un evento è High.
- **Un UTC anchor per evento.** Ogni evento è ancorato a un singolo istante UTC dallo schedule ufficiale
  della fonte primaria; il timezone della fonte stessa è memorizzato, e il rendering per-user usa un
  timezone IANA esplicito con DST gestito dal zone database — mai un manual ±1h toggle.
- **Catene di revisione complete, ovunque.** Il valore originale e ogni revision sono first-class, esposte
  identicamente attraverso l'API, MCP e le superfici cBot.
- **≥10 anni di storia, no wall.** Browsing range senza restrizioni; no 60-day cap, no registration gate.
- **Point-in-time by construction.** Ogni fatto porta `KnownAt` (quando *noi* l'abbiamo saputo) e
  `EffectiveAt` (l'istante dell'evento). "Come il calendario appariva al tempo T" è una query first-class,
  così una regola news backtested si comporta esattamente come live — no look-ahead da using revised
  values in history.

## Il modello di impatto

L'impact score è una funzione pura, deterministica in `[0, 100]`, bandata a Low / Medium / High /
Critical. I suoi input sono solo dati noti al tempo di scoring (no future leak):

- **Series prior** — un peso baseline per classe di indicatore (una decisione sui tassi outweighs CPI, che
  outweighs un sondaggio minore).
- **Realized-volatility footprint** — la mediana del ritorno assoluto dei simboli primari affected nella
  finestra dopo i *passati* rilasci di questa series: "questo rilascio storicamente muove il prezzo di questo molto."
- **Surprise sensitivity** — quanto fortemente l'surprise assoluto (uno z-score) storicamente
  correlato con la mossa post-rilascio.

Il score miscela questi con pesi fixed e timbra un `ImpactModelVersion`. La ricomputazione è
un'operazione esplicita, logged che produce una **nuova revision** — mai una muta — così il score è sempre
riproducibile dai suoi input.

## Country → currency → symbol mapping

La singola algo-integration papercut più citata è risolta una volta, come funzione pura: un paese mappa alla
sua valuta (ogni membro euro-area si fans in a EUR), e una valuta mappa alla watchlist symbols che la
quotano su entrambe le gambe. Così **EURUSD è affected sia da eventi EU che US**; XAUUSD è USD-exposed;
US500 mappa a USD. Questo guida il news filter, la risoluzione affected-symbols e il blackout math.

## News-window policy

Una `NewsWindowRule` è `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Un'implementazione
singola, condivisa, pura risponde "è l'istante T dentro un blackout per il simbolo S?" — usata dal cBot
news filter, dalla pausa copy-trade e dall'AI risk guard, così non possono mai divergere. In incertezza la
risposta blackout defaulta al valore conservativo configurato (fail-closed per default) così un data gap
non green-light mai silenziosamente il trading attraverso un rilascio high-impact.

## Point-in-time & revisions

Actuals, forecasts e impact scores sono **append-only**. Ogni evento possiede una catena ordinata di
revisions, monotonic in `KnownAt`:

- `Scheduled` — l'evento è stato prima schedulato (prior impact, no actual).
- `Released` — il primo actual stampato è arrivato.
- `Revised` — un valore revisionato successivo è arrivato.
- `Rescheduled` — la fonte ha spostato l'istante di rilascio (auditabile, alertable).
- `Rescored` — l'impact score è stato ricomputato sotto una nuova model version.

Querying `as of` un istante passato restituisce esattamente la revision known then — la garanzia che uccide
look-ahead nei backtested news rules.

## Forecast / consensus

La survey median degli economisti **non** è liberamente pubblicata dalle fonti primarie — è il
proprietary value-add degli aggregatori, e non fabbrichiamo noi. Lo schema evento porta un nullable
`Forecast`; un deployment può cablare un consensus feed licensed attraverso la porta opzionale
`IForecastProvider` (bring-your-own key, off per default). I valori precedenti e le revisions vengono
sempre dalla fonte ufficiale.

## Data sources

Due layer decoupled, tutti primari — mai un aggregatore:

- **Schedule / timing:** FRED release calendar; agenzie statistiche nazionali (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); calendari banche centrali (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Actual values:** FRED (con date vintage per revisions e point-in-time), più BLS, BEA, Census,
  ECB SDW, Eurostat e OECD SDMX APIs.

Una fonte morta degrada la copertura **solo per quella fonte**; il calendario continua a servire tutto il
resto e surfacce il gap come metrica di freshness.

## Rate limiting & il backup plan

I provider esterni pubblicano rate limits (FRED allow ~120 requests/minute). Il calendario è costruito così
che **mai trip a provider's limit**, e così che essere throttled o cut off non degrada mai le letture:

- **Proactive throttling.** Ogni HTTP client della fonte passa attraverso un shared, thread-safe rate gate
  che spazia le richieste outbound a un budget configurato (`App:Calendar:FredRequestsPerMinute`, default
  100 — deliberatamente sotto il ceiling del provider). Le richieste sono queue e paced, mai bursted.
- **Honour `429 Retry-After`.** Se un provider ritorna mai `429 Too Many Requests`, il gate fa back off
  l'intera fonte del cooldown richiesto dal server (o `App:Calendar:RateLimitBackoff`, default 60s)
  prima della prossima chiamata — no tight retry loop.
- **Standard resilience.** Ogni source client eredita anche l'app-wide resilience handler (retry with
  backoff + jitter, circuit breaker, timeouts), così i blip transitori sono assorbiti e una fonte
  che faila persistently è parked (la sua copertura va stale) senza affecting gli altri.
- **The backup plan — the durable read-through cache.** Le letture sono **mai** servite chiamando un
  provider. Una volta che un range è fetched è persistito append-only in Postgres e servito da lì
  per sempre (vedi §"On-demand load"). Così anche quando una fonte è rate-limited o down, il calendario
  continua a rispondere da dati cached, point-in-time-correct; lo span mancante semplicemente resta uncovered
  e viene ritentato al prossimo ciclo di ingestion. Le risposte blackout additionalmente falliscono al
  default conservativo sotto incertezza, così un data gap non green-light mai il trading attraverso un rilascio.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) e il
  "fetch a span once, never again" cache tengono l'actual request volume ben sotto qualsiasi limite in
  normale operation — il rate gate è una safety net, non il common path.

## Enable / disable

Due tier indipendenti, esattamente come altri cMind features:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flippato dalla Features admin UI;
  no redeploy, prende effetto live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`). Un
  reseller lo imposta `false` per rimuovere la feature interamente; un operatore poi non può ri-abilitarla.

Lo stato effective è `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Quando disabilitato,
la nav entry è hidden e `/economic-calendar`, `/api/calendar/**` e i tool MCP del calendario ritornano
un clean feature-disabled `404` — mai un `500`. La storia persistita è retained su un runtime toggle-off
così il ri-abilitare è instantaneo.

## Rollout phases

- **P0 — domain core** *(implementato)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite.
- **P1 — persistence + one source** *(implementato)*: EF `calendar` schema (own tables, append-only,
  hot indexes), the read-through `IEconomicCalendar` reader con point-in-time `asOf`, l'append-only
  write service idempotent, il FRED connector dietro un resilient typed client, e l'ingestion worker
  gated da config; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(implementato)*: la versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, e i core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) con scope enforcement e two-tier gating,
  integration-tested. Più la mobile-first **`/economic-calendar` page** — agenda gated, fully-localized
  (23 languages) di upcoming releases come phone-friendly cards con impact chips color-banded
  e un MudBlazor **filter dialog** (currencies + minimum impact + un **From-date** picker per saltare a
  **qualsiasi** data passata attraverso la full history — no 60-day cap, no wall); nav entry,
  smoke/mobile/a11y/E2E tested. Una **per-indicator series history page**
  (`/economic-calendar/series/{code}`, linked da ogni evento) elenca la full print history di una series.
  I surprise charts + infinite-scroll browser follow.
- **P3 — more sources & warm-up** *(started)*: un **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → i loro FRED ids) è seeded automaticamente all'avvio,
  e un one-time, idempotent, year-chunked **proactive backfill** pulla la loro ≥10-year history così il
  common case è warm senza aspettare che un utente miss. **Ingestion è on per default**
  (`App:Calendar:IngestionEnabled`, default `true`): la **central-bank schedule source** necessita **nessuna API
  key**, così il FOMC / ECB / BoE decision calendar popola out of the box — il backfill seeds those
  meeting dates attraverso **sia la storia recente che l'orizzonte forward**, così browsing *last month*
  (o qualsiasi finestra passata) mostra i meetings anche prima che qualsiasi FRED/BLS key sia configurata;
  le value series si riempiono una volta che le loro keys sono impostate. I workers onorano il two-tier gate
  del calendario — un white-label deployment o l'owner che disabilita la economic-calendar feature ferma
  l'ingestion, e `App:Calendar:IngestionEnabled=false` lo spegne esplicitamente. **Per-source freshness**
  è ora reale anch'esso: il worker registra l'ultimo poll riuscito di ogni fonte, il count di
  consecutive-failure e un tripped-circuit flag (persistiti in app settings, cross-process), e l'endpoint
  `/health` + il tool MCP `calendar_health` riportano un truthful verdict `stale` per fonte.
  **BLS** (a 2nd value source) e la **central-bank schedule source** (FOMC / ECB / BoE decision dates,
  backfilled attraverso history e synced forward into a horizon window dal worker) sono in. Ancora da
  venire: BEA/Census/ECB-SDW/Eurostat/OECD value sources e il reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(implementati — full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on the feature)* e gli
  **alerts `EconomicEvent` trigger** *(implementati — un `AlertRule` che spara N minuti prima di un
  upcoming release at/above a chosen impact, optionally narrowed a currencies; evaluato dal existing alert
  worker con no AI, de-duplicated per release; creato via
  `POST /api/alerts/rules/economic-event`)*. Il prop-guard news-blackout gate **e la copy-trade
  blackout pause** sono in (§5.1 — un opt-in `App:Copy:NewsPauseEnabled`, default off: uno source open
  il cui simbolo siede in un Critical-impact blackout è skipped, byte-identical hot path when off). L'
  **backtest event overlay** è in — `GET /api/calendar/v1/for-symbol` e il
  `calendar_events_for_symbol` MCP tool ritornano i point-in-time-correct events affecting a symbol in a
  window, e la **instance/backtest report page** renderizza gli high-impact releases caduti dentro il
  backtest window sotto l'equity curve (così un autore vede quali trades sono atterrati su NFP), gated e
  localized. L'intero piano è ora implementato.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Vedere il [cBot & REST API reference](calendar-cbot-api.md) per l'integration surface.

## La data source è richiesta (la feature è hidden senza una)

Il calendario surfaccia actual/forecast/previous values solo da una configured value source (FRED o
BLS). Senza `App:Calendar:FredApiKey` o `App:Calendar:BlsApiKey` la feature è **hidden** dalla
navigation; se è force-enabled (white-label/owner) senza una key, la pagina mostra un actionable
"configure a data source" notice invece di valori vuoti, e l'azione filter resta hidden finché una
fonte non è impostata. Le righe evento mostrano il **nome** della series (dal catalog), non il raw series code.
