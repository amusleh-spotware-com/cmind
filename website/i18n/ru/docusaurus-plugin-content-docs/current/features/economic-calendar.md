---
description: "cMind поставляет собственный экономический календарь — расписание релизов, фактические значения, прогнозы, пересмотры и модель воздействия на основе данных. Из primary authorities, без зависимости от ForexFactory, FXStreet, Investing.com."
---

# Economic calendar

cMind поставляет **собственный** экономический календарь — расписание релизов, фактические значения, прогнозы, пересмотры и
data-driven модель воздействия — из **primary authorities** (центральные банки и национальные
статистические агентства), с **нулевой зависимостью** от ForexFactory, FXStreet, Investing.com или любого
агрегатора. Он point-in-time корректен, хранит ≥10 лет истории и подключён к торговле,
публичному API, MCP, cBots, AI, алертам и бэктестам. Это decoupled module: может быть выключен без
влияния на trading core.

> **Status.** P0–P4 реализованы и выпущены. Доменное ядро, persistence (EF схема `calendar`, append-only read/write, источники FRED + BLS + central-bank-schedule, config-gated ingestion worker с per-source freshness tracking), versioned JWT REST API, mobile-first UI `/economic-calendar`, MCP tools, cBot JWT API, алерты high-impact событий, copy-trade news-blackout pause, backtest event overlay, SSE stream, HMAC-signed webhooks и типизированный `CmindCalendarClient` — всё реализовано и integration-tested. P5 extras (surprise analytics, iCal/CSV export, keyword search, pluggable consensus) — оставшиеся пункты, см. фазы rollout ниже.

## Чем это отличается

Повторяющиеся жалобы на ведущие календари стали нашими design constraints:

- **Никаких silent impact-rating изменений.** Наш impact rating **детерминированный, версионированный и
  аудируемый**. Каждое изменение — записанный пересмотр с timestamp — никогда silent overwrite. Пользователь
  может видеть точно *почему* событие High.
- **Один UTC anchor per событие.** Каждое событие привязано к одному UTC instant из официального
  расписания primary source; timezone источника сохраняется, и per-user рендеринг использует явный IANA
  timezone с DST, обрабатываемым через zone database — никогда manual ±1h toggle.
- **Полные цепочки пересмотров, везде.** Оригинальное значение и каждый пересмотр — first-class,
  экспонированы идентично через API, MCP и cBot surfaces.
- **≥10 лет истории, без стены.** Без ограничений browsing range; нет 60-дневного cap, нет registration gate.
- **Point-in-time по конструкции.** Каждый факт несёт `KnownAt` (когда *мы* узнали) и
  `EffectiveAt` (момент события). "Как календарь выглядел в момент T" — first-class query, поэтому
  бэктестнутое news rule ведёт себя точно как live — никакого look-ahead от использования revised values в истории.

## Модель воздействия

Impact score — чистая, детерминированная функция в `[0, 100]`, разбитая на Low / Medium / High /
Critical. Её входы — только данные, известные на момент оценки (никакого future leak):

- **Series prior** — baseline weight per класс индикатора (решение по ставке перевешивает CPI, которое
  перевешивает minor survey).
- **Realized-volatility footprint** — медианный абсолютный доход primary affected symbols в окне
  после *прошлых* релизов этого индикатора: "этот релиз исторически двигает цену настолько".
- **Surprise Sensitivity** — насколько сильно абсолютный surprise (z-score) исторически
  коррелировал с движением после релиза.

Счёт смешивает их с фиксированными весами и ставит `ImpactModelVersion`. Пересчёт —
явная, логируемая операция, производящая **новый пересмотр** — никогда mutate — поэтому счёт всегда
воспроизводим из его входов.

## Country → currency → symbol mapping

Самая частая algo integration жалоба решена однажды, как чистая функция: страна маппится к
своей валюте (каждый euro-area член фэнится в EUR), и валюта маппится к symbols watchlist,
котирующим её на любой ноге. Так **EURUSD затрагивается событиями EU и US**; XAUUSD USD-exposed;
US500 маппится к USD. Это драйвит news filter, resolution affected-symbols и blackout math.

## Политика news-window

`NewsWindowRule` = `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Одна,
shared, чистая реализация отвечает "is instant T inside a blackout for symbol S?" — используется
cBot news filter, copy-trade pause и AI risk guard, поэтому они никогда не расходятся. При неопределённости
ответ по умолчанию к настроенному консервативному значению (fail-closed по умолчанию), поэтому
data gap никогда silent не включает торговлю через high-impact релиз.

## Point-in-time & revisions

Фактические значения, прогнозы и impact scores — **append-only**. Каждое событие владеет упорядоченной цепочкой
пересмотров, монотонной по `KnownAt`:

- `Scheduled` — событие впервые запланировано (prior impact, без actual).
- `Released` — прибыло первое напечатанное actual.
- `Revised` — прибыло позжее revised значение.
- `Rescheduled` — источник сдвинул релиз (аудируемо, алертабельно).
- `Rescored` — impact score пересчитан под новой model version.

Запрос `as of` прошлого instant возвращает точно пересмотр, известный тогда — гарантия, убивающая
look-ahead в бэктестнутых news rules.

## Forecast / consensus

Медиана прогнозов экономистов **не** публикуется свободно primary sources — это proprietary value-add
агрегаторов, и мы её не выдумываем. Схема события несёт nullable `Forecast`; deployment может подключить
лицензированный consensus feed через опциональный `IForecastProvider` порт (bring-your-own key, off by default).
Предыдущие значения и пересмотры всегда из official source.

## Источники данных

Два decoupled слоя, все primary — никогда агрегатор:

- **Расписание / тайминг:** FRED release calendar; национальные статистические агентства (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); календари центральных банков (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Фактические значения:** FRED (с vintage датами для revisions и point-in-time), плюс BLS, BEA, Census,
  ECB SDW, Eurostat и OECD SDMX APIs.

Мёртвый источник деградирует покрытие **только этого источника**; календарь продолжает обслуживать всё остальное
и показывает gap как freshness metric.

## Rate limiting & backup plan

Внешние провайдеры публикуют rate limits (FRED позволяет ~120 requests/minute). Календарь построен так что
**никогда не превышает rate limit провайдера**, и так что being throttled или cut off никогда не деградирует reads:

- **Proactive throttling.** Каждый HTTP-клиент источника проходит через shared, thread-safe rate gate
  который распределяет outbound requests к настроенному budget (`App:Calendar:FredRequestsPerMinute`, default
  100 — намеренно ниже ceiling провайдера). Requests queue и pace, никогда не burstятся.
- **Honour `429 Retry-After`.** Если провайдер возвращает `429 Too Many Requests`, gate ставит весь
  source back off на server-requested cooldown (или `App:Calendar:RateLimitBackoff`, default 60s)
  перед следующим вызовом — никакого tight retry loop.
- **Standard resilience.** Каждый source клиент также наследует app-wide resilience handler (retry with
  backoff + jitter, circuit breaker, timeouts), поэтому transient blips абсорбируются и персистентно
  failing source parked (его coverage становится stale) без влияния на остальные.
- **Backup plan — durable read-through cache.** Reads **никогда** не обслуживаются вызовом
  провайдера. Однажды fetched диапазон персистится append-only в Postgres и обслуживается оттуда
  навсегда (см. §"On-demand load"). Поэтому даже когда source rate-limited или down, календарь
  продолжает отвечать из cached, point-in-time-correct данных; недостающий span просто остаётся uncovered и
  retry'ится в следующем ingestion cycle. Blackout answers additionally fail к консервативному
  default при неопределённости, поэтому data gap никогда не включает торговлю через релиз.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) и
  "fetch a span once, never again" cache держат реальный request volume далеко ниже любого лимита при нормальной
  работе — rate gate это safety net, не common path.

## Enable / disable

Два независимых tier, точно как другие фичи cMind:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) переключается из Features admin UI;
  no redeploy, применяется live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`).
  Reseller устанавливает `false` чтобы полностью убрать функцию; оператор затем не может её включить.

Effective state = `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Когда выключено,
nav entry скрыт и `/economic-calendar`, `/api/calendar/**` и MCP calendar tools возвращают
clean feature-disabled `404` — никогда `500`. Персистентная история сохраняется при runtime toggle-off,
поэтому re-enabling мгновенен.

## Фазы rollout

- **P0 — domain core** *(реализовано)*: aggregates, value objects, ports, модель воздействия,
  country→symbol mapping, политика news-window, two-tier gating, полный unit suite.
- **P1 — persistence + один источник** *(реализовано)*: EF `calendar` схема (собственные таблицы, append-only,
  hot indexes), read-through `IEconomicCalendar` reader с point-in-time `asOf`, idempotent
  append-only write service, FRED connector за resilient typed client, и config-gated
  ingestion worker; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(реализовано)*: versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, и core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) со scope enforcement и two-tier gating,
  integration-tested. Плюс mobile-first **`/economic-calendar` страница** — gated, fully-localized
  (23 языка) agenda предстоящих релизов как phone-friendly cards с цветными chip'ами impact
  и MudBlazor **filter dialog** (валюты + minimum impact + **From-date** picker для перехода к
  **любой** прошлой дате через всю историю — нет 60-дневного cap, нет стены); nav entry, smoke/mobile/a11y/E2E
  tested. **Per-indicator series history страница** (`/economic-calendar/series/{code}`, linked из каждого
  события) перечисляет полную историю печати серии.
- **P3 — больше источников & warm-up** *(started)*: **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → их FRED ids) seeded автоматически при старте,
  и one-time, idempotent, year-chunked **proactive backfill** тянет их ≥10-year history so the
  common case is warm without waiting. **Ingestion по умолчанию включён**
  (`App:Calendar:IngestionEnabled`, default `true`): **central-bank schedule source** нужен **нет API
  key**, поэтому FOMC / ECB / BoE календарь заполняется из коробки. **Per-source freshness**
  реализован: worker записывает last successful poll, consecutive-failure count и tripped circuit flag.
  **BLS** и **central-bank schedule source** запущены.
- **P4 — deep integration** *(реализовано)*: **MCP tools** (full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on the feature) и **alerts `EconomicEvent` trigger**
  (реализовано). **Prop-firm news-blackout gate и copy-trade blackout pause** реализованы.
  **Backtest event overlay** реализован — бэктест overlay high-impact релизов под equity curve.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

См. [cBot & REST API reference](calendar-cbot-api.md) для integration surface.
