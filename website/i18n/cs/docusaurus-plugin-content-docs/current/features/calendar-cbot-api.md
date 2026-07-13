# Calendar REST & cBot API

Ekonomický kalendář je zveřejněn jako **verzovaný, JWT-zabezpečený, rate-limited REST API** — povrch hlavní integrace. Jakákoli externí služba, dashboard nebo cBot se integruje proti němu jako produkt. Má paritu funkcí s FXStreet Calendar API a jde dále: point-in-time `asOf`, plné řetězce revizí, deterministické zdůvodnění dopadů, analýzu překvapení, rozlišení země→symbolu a blackout matematiku, kterou jiné kalendářní API nevystavují.

> **Status.** Zabezpečení JWT (vydání klienta + výměna tokenu), gating a základní koncové body čtení — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` — jsou **implementovány a integrační-testovány** (auth, vynucování oboru, 404 s bílým štítkem), plus **`events/batch`** (omezený multiplex) a objektivní **`/openapi.json`** dokument, **`ETag`/`If-None-Match` 304** na čtení událostí/historie a **keyset kurzorová paginace** (`Link: rel="next"`), **SSE `stream`** (live `event: release` push, poll-backed), **HMAC-podepsané webhooks** (`X-CMind-Signature: sha256=…`, registrovaný vlastníkem, doručený config-gated worker mimo trvalý vodotisk) a dodaný **typovaný klient** (`CmindCalendarClient`). Celý veřejný povrch API je implementován.

## Bezpečnost — JWT

API opakuje stávající HS256 tokenizační moudrost repo (stejný vzor jako agenti CtraderCliNode používají), ne nový systém:

- Správce aplikace vydá **klienta Calendar API** (jméno + rozsahy + vypršení). Klient si vyměňuje své ID a tajemství na `POST /api/calendar/v1/token` za **krátkoživý HS256 JWT** (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Pouze krátký JWT se jezdí na požadavcích (`Authorization: Bearer <jwt>`).
- Tajemství klienta je uloženo **šifrované** přes `ISecretProtector` — nikdy prostý text, nikdy zaznamenáno.
- **Rozsahy** (nejméně-oprávnění): `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. Token cBot obvykle dostane `read` + `blackout` pouze.
- Standardní `JwtBearer` ověřování (vydavatel, publikum, životnost, podpisový klíč; `alg=none` odmítnuto; těsný posun hodin). Rate limit tokenu na klienta + globální ograničovač; `429` s `Retry-After`. Všechny selhání ověřování jsou auditovány.
- Zakázání klienta okamžitě zastaví budoucí vydání tokenu; krátká životnost JWT omezuje uniklý token. Celý strom `/api/calendar/**` se `404`s, když je funkce zakázána.

## Konvence

- **Základní cesta a verzování:** `/api/calendar/v1/...` (URL-verzovaný; aditivní změny nezvyšují).
- **Formát:** JSON; RFC 3339 UTC okamžiky plus explicitní `sourceTimeZone`; volitelné `tz=` vyrenderuje místní čas pohodlí bez ztráty UTC kotvy.
- **Paginace:** kurzor-založená (`cursor`, `limit` ≤ 1000); `next` kurzor v těle a záhlaví `Link`.
- **Ukládání do mezipaměti:** `ETag` + `If-None-Match`; historické rozsahy dostanou dlouhé TTL, nadcházející krátké.
- **Chyby:** RFC 7807 `problem+json`, nikdy holý `500`.
- **Degradované čtení:** selhání zdroje/DB vrátí `200` nejlépe známé údaje plus signál `X-Calendar-Freshness` / `stale=true` (nebo `503 Retry-After` pouze pokud opravdu nic není známo) — cBot se rozhoduje.

## Koncové body

| Metoda & cesta | Účel | Klíčové parametry |
|---|---|---|
| `POST /v1/token` | Exchange klient id+secret → krátký JWT | tělo: `clientId`, `clientSecret` |
| `GET /v1/events` | Události v okně (nadcházející nebo historické) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Jedna událost: plný řetězec revizí, překvapení, zdůvodnění dopadu, ovlivněné symboly | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Seřazená historie revizí | — |
| `GET /v1/history` | Hluboké historické stažení pro sérii (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalog sledovaných indikátorů + kadence + zdroj | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historická skutečná/prognóza/překvapení z-skóre série | `series`,`count`/`from,to` |
| `GET /v1/next` | Další relevantní vydání pro symbol (země→symbol mapován) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Je symbol uvnitř vysokoimpaktního okna nyní/na T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Vyřešit událost → symboly v seznamu sledování | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex více dotazů v jedné cestě | tělo: pole dotazů |
| `GET /v1/stream` (SSE) | Live push: vydání/revize/vstup do okna | `currencies`,`minImpact` (rozsah `calendar:stream`) |
| `POST /v1/webhooks` | Registr HMAC-podepsaného zpětnýho volání pro vydání/revizi/blackout | tělo: url, filtry, tajemství |
| `GET /v1/health` | Čerstvost na zdroj + pokrytí | — |

## Blackout — filtr cBot zpráv

`GET /v1/blackout` vrací `{ inBlackout, event, startsAt, endsAt, stale }`. U nejistoty se standardně nastavuje **nakonfigurovaná konzervativní odpověď** (selhání-uzavřeno ve výchozím nastavení: "předpokládat v-blackoutu" pro obezřetné boty), plus příznak `stale` — data mezera nikdy nezelenáče obchodování přes NFP. Koncový bod je čisté čtení DB/cache s tvrdým server timeout; na horké cestě nema žádné synchronní získávání původu.

Dodaný typovaný klient (`Infrastructure.Calendar.CmindCalendarClient`) to zabaluje: nasměrujte jeho `HttpClient` na kořen API, zavolejte `GetTokenAsync(clientId, clientSecret)` jednou, pak `GetBlackoutAsync(token, symbol)` před každým obchodem — je **failsafe konstrukcí** (jakýkoliv úspěch nebo chyba analýzy vrací `InBlackout = true, Stale = true`, takže data mezera nikdy nezelenáče obchodování). cBot se takto pozastavuje kolem zpráv:

```csharp
// Pseudokód pro cTrader cBot pomocí WebRequest + tokenu klienta Calendar API.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // failsafe: stale ⇒ léčit jako blackout
    return;                                                       // přeskočit nové položky v okně zpráv
// ...jinak pokračujte v umístění obchodu
```

## Point-in-time pro backtesty

Předejte `asOf` na jakékoli čtení, abyste získali kalendář přesně tak, jak stál v minulém okamžiku — skutečné, prognózy a revize *jak tehdy byly*. Protože čtení `asOf` jsou čistá a cachable, backtest mlacející historii dostane totožné bajty pokaždé a testovaní zpravodajské pravidlo se chová přesně jako živé (bez pohledu vpřed z upravených hodnot).

## Odolnost pro algo volající

API sedí v obchodní horké cestě, takže nikdy nehodí do živého bota: každá cesta vrací dobře formovanou `problem+json` nebo typované degradované tělo. Opakuje primitivy odolnosti copy-tradingu — standardní handler odolnosti HTTP na každém zdrojovém klientovi, doménová pojistka na zdroj, lease-strážený singleton ingestion worker se startovací rekonciliací a health checks zapojené do `/health`. Dodaný typovaný klient snippet přichází s retry + timeout + circuit-breaker předkonfigurován, aby autoři botů dědili odolnost.

## Bratranec: AI currency-strength (`market:read`)

Model čtení [AI macro currency-strength](./currency-strength.md) jezdí **stejné** JWT moudrostí — jeden systém, jeden podpisový klíč, jeden rate-limiter — přidávajíc pouze rozsah `market:read`. Zaregistrujte API klienta s tímto rozsahem, vyměňte jej za token přesně jak výše a zavolejte:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// získat token přes POST /api/calendar/v1/token výše, pak:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Token bez `market:read` dostane `403`; vypršlý/pozměněný token dostane `401`. Koncové body jsou gatingované na vlajku AI funkce a servírované pod `/api/market/v1`, takže zůstávají nezávislé na feature gate kalendáře. Při spuštění/backtestu dispatch nasazení může vložit `CMIND_API_BASEURL` + krátkoživý `market:read` token, takže cBot se volá zpět s nulovou registrací klienta.
