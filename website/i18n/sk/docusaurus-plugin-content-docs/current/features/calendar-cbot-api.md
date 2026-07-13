# Calendar REST & cBot API

Ekonomický kalendár je vystavený ako **verziované, JWT-zabezpečené, rate-limited REST API** — hlavná
integračná plocha. Akýkoľvek externý servis, dashboard alebo cBot sa integruje proti nemu ako voči produktu. Má
paritu s FXStreet Calendar API a prekonáva ho: point-in-time `asOf`, úplné
revidované reťazce, deterministické odôvodnenie dopadu, surprise analytika, country→symbol resolution a blackout
matematika, ktorú iné kalendárové API neodhaľujú.

> **Stav.** JWT zabezpečenie (vydávanie klienta + exchange tokenu), gating a hlavné čítacie
> endpoints — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — sú **implementované a integračne testované** (auth, scope enforcement,
> feature/white-label 404), plus **`events/batch`** (bounded multiplex) a discoverable
> **`/openapi.json`** dokument, **`ETag`/`If-None-Match` 304** na event/history čítaniach, a
> **keyset cursor pagination** (`Link: rel="next"`), **SSE `stream`** (live `event: release` push,
> poll-backed), **HMAC-signed webhooks** (`X-CMind-Signature: sha256=…`, owner-registrované, doručované
> config-gated workerom z perzistentného watermarku), a dodaný **typed client** (`CmindCalendarClient`).
> Plná verejná API plocha je implementovaná.

## Bezpečnosť — JWT

API znova používa existujúcu HS256 token machinery repozitára (rovnaký vzor, ktorý používajú CtraderCliNode agenti),
nie novú schému:

- Admin aplikácie vydá **Calendar API klienta** (názov + scopes + expiry). Klient vymení svoje id
  a secret na `POST /api/calendar/v1/token` za **krátkožijúci HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Iba krátky JWT jazdí
  na požiadavkách (`Authorization: Bearer <jwt>`).
- Secret klienta je uložený **encrypted** cez `ISecretProtector` — nikdy plaintext, nikdy logged.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. cBot token typicky dostane `read` + `blackout` iba.
- Štandardná `JwtBearer` validácia (issuer, audience, lifetime, signing key; `alg=none` rejected; tight
  clock skew). Per-client token-bucket rate limit + globálny limitér; `429` s `Retry-After`. Všetky auth
  zlyhania sú auditované.
- Zakázanie klienta okamžite zastaví budúce vydávanie tokenov; krátka životnosť JWT ohraničuje uniknutý
  token. Celý `/api/calendar/**` strom `404` keď je funkcia zakázaná.

## Konvencie

- **Base path & verzia:** `/api/calendar/v1/...` (URL-verziované; aditívne zmeny nebumperujú).
- **Formát:** JSON; RFC 3339 UTC instants plus explicitný `sourceTimeZone`; voliteľný `tz=` renderuje
  convenience local time bez straty UTC anchoru.
- **Stránkovanie:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor v tele a `Link` header.
- **Cachovanie:** `ETag` + `If-None-Match`; historické rozsahy dostávajú dlhý TTL, blížiace sa krátky.
- **Chyby:** RFC 7807 `problem+json`, nikdy holé `500`.
- **Degradované čítania:** source/DB fault vráti `200` najlepšie známe data plus `X-Calendar-Freshness`
  / `stale=true` signál (alebo `503 Retry-After` iba ak skutočne nič nie je známe) — cBot rozhoduje.

## Endpoints

| Metóda & path | Účel | Kľúčové params |
|---|---|---|
| `POST /v1/token` | Výmena client id+secret → krátky JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Události v okne (blížiace sa alebo historické) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Jedna udalosť: plný revidovaný reťazec, surprise, odôvodnenie dopadu, affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Usporiadaná história revízií | — |
| `GET /v1/history` | Hlboký historický pull pre sériu (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalóg sledovaných indikátorov + cadence + zdroj | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historické actual/forecast/surprise z-score série | `series`,`count`/`from,to` |
| `GET /v1/next` | Ďalšia relevantná release pre symbol (country→symbol mapped) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Či je symbol vnútri high-impact okna teraz/at T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve udalosť → symboly vo watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex niekoľkých dotazov v jednom round-trip | body: array of queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Registrácia HMAC-signed callback pre release/revision/blackout | body: url, filters, secret |
| `GET /v1/health` | Per-source freshness + coverage | — |

## Blackout — cBot news filter

`GET /v1/blackout` vráti `{ inBlackout, event, startsAt, endsAt, stale }`. Pri neistote predvolene
vracia **nakonfigurovanú konzervatívnu odpoveď** (fail-closed predvolene: "predpokladaj blackout" pre risk-off
boty), plus `stale` flag — dátová medzera nikdy neodblokuje obchodovanie cez NFP. Endpoint je čistý
DB/cache read s tvrdým server timeout; na hot-path nie je žiadne synchrónne origin fetch.

Dodaný typed client (`Infrastructure.Calendar.CmindCalendarClient`) wrapuje toto: nastavte jeho `HttpClient`
na API root, zavolajte `GetTokenAsync(clientId, clientSecret)` raz, potom `GetBlackoutAsync(token, symbol)`
pred každou objednávkou — je **fail-safe by construction** (akékoľvek non-success alebo parse error vráti
`InBlackout = true, Stale = true`, takže dátová medzera nikdy neodblokuje obchodovanie). cBot pozastaví okolo news
takto:

```csharp
// Pseudocode pre cTrader cBot používajúci WebRequest + Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...inak pokračuj s umiestnením objednávky
```

## Point-in-time pre backtesty

Pass `asOf` na akékoľvek čítanie pre získanie kalendára presne tak, ako stál v danom momente — aktuály,
forecasty a revízie *tak, ako vtedy boli*. Pretože `asOf` čítania sú čisté a cacheable, backtest
bušujúci históriu dostane identické bajty každý raz, a backtestované news pravidlo sa správa presne ako live
(bez look-ahead z revidovaných hodnôt).

## Odolnosť pre algo volajúcich

API sedí v trading hot-path, takže nikdy nehodí do live bota: každá cesta vráti
well-formed `problem+json` alebo typed degraded body. Znova používa copy-trading resilience primitives —
štandardný HTTP resilience handler na každom source klientovi, domain circuit breaker per source,
lease-guarded singleton ingestion worker so startup reconciliáciou, a health checks napojené na
`/health`. Dodaný typed client snippet prichádza s retry + timeout + circuit-breaker prekonfigurovaným
tak, aby bot autori zdedili odolnosť.

## Sibling: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model rides the **same** JWT machinery —
jedna schéma, jedno signing secret, jeden rate-limiter — pridáva iba `market:read` scope. Zaregistrujte API
klienta s týmto scope, vymeňte ho za token presne ako vyššie, a volajte:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// získaj token cez POST /api/calendar/v1/token ako vyššie, potom:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Token chýbajúci `market:read` dostane `403`; expirovaný/poškodený token dostane `401`. Endpoints sú gated
na AI feature flag a servírované pod `/api/market/v1` tak, aby zostali nezávislé od calendar
feature gate. Pri run/backtest dispatch deployment môže injectnúť `CMIND_API_BASEURL` + krátkožijúci
`market:read` token tak, aby cBot volal späť s nulovou klient registráciou.
