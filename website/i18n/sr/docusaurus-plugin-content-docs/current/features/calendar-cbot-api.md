# Kalendar REST и cBot API

Економски календар је изложен као **версиони, JWT-обезбеђен, rate-limited REST API** — главна интеграциона површина. Било кој спољни сервис, контролна табла или cBot се интегрише преко њега као производа. Има паралелност са FXStreet Calendar API и превазилази га: тачна временска референца `asOf`, потпуни ланци ревизија, детерминистички модел утицаја, аналитика изненађења, мапирање земља→симбол, и математика блокирања коју други календарски API-ји не излажу.

> **Статус.** JWT безбедност (издавање клијента + размена токена), контрола приступа, и основни read
> ендпоинти — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — су **имплементирани и интеграционо тестирани** (auth, scope enforcement,
> feature/white-label 404), плус **`events/batch`** (ограничено мултиплексирање) и откриви
> **`/openapi.json`** документ, **`ETag`/`If-None-Match` 304** на event/history читања, и
> **keyset cursor pagination** (`Link: rel="next"`), **SSE `stream`** (live `event: release` push,
> poll-backed), **HMAC-потписани webhook-ови** (`X-CMind-Signature: sha256=…`, owner-регистровани, испоручени од
> config-gated worker са перзистентним watermark-ом), и испоручени **типизирани клијент** (`CmindCalendarClient`).
> Пуна јавна API површина је имплементирана.

## Безбедност — JWT

API поново користи постојећу HS256 токен машинерију репоа (исти образац који користе CtraderCliNode агенти),
не нова шема:

- Админ апликације издаје **Calendar API клијент** (име + scope-ови + истец). Клијент размене свој id
  и secret на `POST /api/calendar/v1/token` за **краткотрајни HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 мин, `scope` claim). Само кратки JWT иде
  на захтеве (`Authorization: Bearer <jwt>`).
- Тајна клијента се чува **шифровано** преко `ISecretProtector` — никада plaintext, никада логовано.
- **Scope-ови** (минимална привилегија): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. cBot токен обично добија само `read` + `blackout`.
- Стандардна `JwtBearer` валидација (issuer, audience, lifetime, signing key; `alg=none` одбијен; строг
  clock skew). Per-client token-bucket rate limit + глобални limitер; `429` са `Retry-After`. Сви auth
  неуспеси се аудирају.
- Онемогућавање клијента зауставља будуће издавање токена одмах; кратак JWT животни век ограничава цурење
  токена. Цело `/api/calendar/**` стабло `404` када је функција онемогућена.

## Конвенције

- **Base path и верзионирање:** `/api/calendar/v1/...` (URL-верзионирано; адитивне промене не повећавају верзију).
- **Формат:** JSON; RFC 3339 UTC инстанте плус експлицитни `sourceTimeZone`; опциони `tz=` рендерује
  конвенционално локално време без губитка UTC референце.
- **Пагинација:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor у телу и `Link` header.
- **Кеширање:** `ETag` + `If-None-Match`; историјски опсези добијају дугачак TTL, предстојећи кратак.
- **Грешке:** RFC 7807 `problem+json`, никада голи `500`.
- **Degraded читања:** fault source/DB враћа `200` најбоље познате податке плус `X-Calendar-Freshness`
  / `stale=true` сигнал (или `503 Retry-After` само ако се заиста ништа не зна) — cBot одлучује.

## Ендпоинти

| Метод и путања | Намена | Кључни параметри |
|---|---|---|
| `POST /v1/token` | Размена client id+secret → кратки JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Догађаји у временском прозору (предстојећи или историјски) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Један догађај: пуни ланац ревизија, изненађење, рационалност утицаја, погођени симболи | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Уређена историја ревизија | — |
| `GET /v1/history` | Дубоко историјско повлачење за серију (≥10г) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Каталог праћених индикатора + cadence + извор | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Историјска серија actual/forecast/surprise z-score | `series`,`count`/`from,to` |
| `GET /v1/next` | Следеће релевантно ослобађање за симбол (земља→симбол мапирано) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Да ли је симбол унутар високо-утицајног прозора сада/у T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Резолуција догађај → симболи у watchlist-у | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Мутиплексирање неколико упита у једном round-trip-у | body: низ упита |
| `GET /v1/stream` (SSE) | Live push: ослобађања/ревизије/улазак у прозор | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Регистрација HMAC-потписаног callback-а за ослобађање/ревизију/блокирање | body: url, filters, secret |
| `GET /v1/health` | Свежина по извору + покривеност | — |

## Блокирање — cBot news filter

`GET /v1/blackout` враћа `{ inBlackout, event, startsAt, endsAt, stale }`. При неизвесности, подразумева
се **конзервативни одговор** (fail-closed по подразумеваном: "претпостави да је у блокираном прозору" за risk-off
ботове), плус `stale` ознака — празнина података никада не омогућава трговање кроз NFP. Ендпоинт је чисто
DB/cache читање са хард timeout-ом сервера; нема синхроног origin fetch-а на hot path-у.

Испоручени типизирани клијент (`Infrastructure.Calendar.CmindCalendarClient`) обмотава ово: показује његов `HttpClient`
ка API root-u, позива `GetTokenAsync(clientId, clientSecret)` једном, затим `GetBlackoutAsync(token, symbol)`
пре сваке наручбе — **fail-safe по конструкцији** (било koji non-success или parse error враћа
`InBlackout = true, Stale = true`, тако да празнина података никада не омогућава трговање). cBot прави паузу око вести
овако:

```csharp
// Pseudocode за cTrader cBot користећи WebRequest + Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ третирај као blackout
    return;                                                       // прескочи нове уносе у прозору вести
// ...иначе настави са постављањем наручбе
```

## Тачна временска референца за backtest-ове

Проследи `asOf` на било којој операцији читања да добијеш календар тачно онако како је изгледао у неком прошлом тренутку — actuals,
forecasts и ревизије *онако како су тада били*. Будући да су `asOf` читања чиста и кеширана, backtest
који туче историју добија идентичне бајтове сваки пут, а backtest-овано правило вести се понања тачно као
живо (без look-ahead-а од ревидираних вредности).

## Отпорност за algo позиваоце

API седи на trading hot path-у, тако да никада не избацује из живог бота: сваки пут враћа добро-формиран
`problem+json` или типизирано degraded body. Поново користи примитиве отпорности copy-trading-а —
стандардни HTTP resilience handler на сваком source клијенту, domain circuit breaker по извору, lease-guarded
singleton ingestion worker са startup реконцилијацијом, и health checks повезани са `/health`. Испоручени
типизирани клијент долази са retry + timeout + circuit-breaker преконфигурисаним
тако да аутори ботова наследе отпорност.

## Сестра: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model вози на **истој** JWT машинерији —
једна шема, једна signing тајна, један rate-limiter — додајући само `market:read` scope. Региструј API
клијента са тим scope-ом, размени га за токен тачно као горе, и позови:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// добиј токен преко POST /api/calendar/v1/token као горе, затим:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Токен без `market:read` добија `403`; истекао/манипулисан токен добија `401`. Ендпоинти су контролисани
feature flag-ом AI и сервирани под `/api/market/v1` тако да остају независни од calendar feature gate-а.
При run/backtest dispatch-у, deployment може инјектовати `CMIND_API_BASEURL` + краткотрајни
`market:read` токен тако да cBot позива назад без клијентске регистрације.
