# Kalender-REST & cBot-API

Der wirtschaftskalender wird als eine **versionierte, JWT-gesicherte, rate-limitierte REST-API** — die Flaggschiff-Integrationsoberfläche — bereitgestellt. Jeder externe Service, Dashboard oder cBot integriert sich dagegen als Produkt. Es hat Feature-Parität mit der FXStreet Calendar API und geht über sie hinaus: Point-in-Time `asOf`, vollständige Revision Chains, deterministische Auswirkungslogik, Überraschungs-Analytik, Land→Symbol-Auflösung und Blackout-Mathematik, die andere Kalender-APIs nicht verfügbar machen.

> **Status.** Die JWT-Sicherheit (Client-Ausstellung + Token-Austausch), das Gating und die Core-Read-Endpunkte — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` — sind **implementiert und integrationsgetestet** (Auth, Scope-Durchsetzung, Feature/White-Label 404), plus **`events/batch`** (begrenzte Multiplex) und ein auffindbar **`/openapi.json`**-Dokument, **`ETag`/`If-None-Match` 304** auf dem Event/History-Reads, und **keyset-Cursor-Pagination** (`Link: rel="next"`), das **SSE `stream`** (live `event: release` Push, Poll-gestützt), **HMAC-signierte Webhooks** (`X-CMind-Signature: sha256=…`, vom Besitzer registriert, geliefert von einem Config-gated Worker aus einer persistiert Wasserlinie), und der versandter **typisierter Client** (`CmindCalendarClient`). Die volle öffentliche API-Oberfläche ist implementiert.

## Sicherheit — JWT

Die API wiederverwendet die vorhandene HS256-Token-Maschinerie des Repos (das gleiche Muster, das die CtraderCliNode-Agenten verwenden), nicht ein neues Schema:

- Ein App-Administrator stellt einen **Kalender-API-Client** aus (Name + Scopes + Ablauf). Der Client tauscht seine ID und das Geheimnis unter `POST /api/calendar/v1/token` gegen einen **kurzfristigen HS256-JWT** aus (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` Claim). Nur das kurze JWT fährt auf Anfragen (`Authorization: Bearer <jwt>`).
- Das Client-Geheimnis wird **verschlüsselt** via `ISecretProtector` gespeichert — nie Klartext, nie geloggt.
- **Scopes** (Least-Privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. Ein cBot-Token wird typischerweise nur `read` + `blackout` erhalten.
- Standard `JwtBearer`-Validierung (Aussteller, Publikum, Lebensdauer, Signaturschlüssel; `alg=none` abgelehnt; enger Clock-Skew). Pro-Client-Token-Bucket-Rate-Limit + globaler Limiter; `429` mit `Retry-After`. Alle Auth-Ausfälle werden geprüft.
- Die Deaktivierung des Clients stoppt die zukünftige Token-Ausstellung sofort; die kurze JWT-Lebensdauer begrenzt ein durchgesickertes Token. Der gesamte `/api/calendar/**`-Baum `404`s wenn das Feature deaktiviert ist.

## Konventionen

- **Basispfad & Versionierung:** `/api/calendar/v1/...` (URL-versioniert; additive Änderungen erhöhen nicht die Nummer).
- **Format:** JSON; RFC 3339 UTC-Instanzen plus ein explizite `sourceTimeZone`; optionale `tz=` rendert eine Komfort-Lokalzeit, ohne die UTC-Anker zu verlieren.
- **Pagination:** Cursor-basiert (`cursor`, `limit` ≤ 1000); `next` Cursor im Body und ein `Link`-Header.
- **Caching:** `ETag` + `If-None-Match`; historische Bereiche erhalten ein langes TTL, bevorstehend ein kurzes.
- **Fehler:** RFC 7807 `problem+json`, nie ein reines `500`.
- **Heruntergestufte Reads:** ein Source/DB-Fehler gibt `200` beste-bekannte Daten plus ein `X-Calendar-Freshness` / `stale=true`-Signal zurück (oder nur `503 Retry-After` wenn wirklich nichts bekannt ist) — der cBot entscheidet.

## Endpunkte

| Methode & Pfad | Zweck | Wichtige Parameter |
|---|---|---|
| `POST /v1/token` | Austausch von Client-ID+Secret → kurzes JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events in einem Fenster (bevorstehend oder historisch) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Ein Event: vollständige Revision Chain, Überraschung, Auswirkungslogik, betroffene Symbole | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Geordnete Revisionshistorie | — |
| `GET /v1/history` | Deep Historical Pull für eine Serie (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalog der verfolgten Indikatoren + Kadenz + Quelle | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historische Actual/Forecast/Überraschungs-z-Score-Serie | `series`,`count`/`from,to` |
| `GET /v1/next` | Nächste relevante Veröffentlichung für ein Symbol (Land→Symbol-Mapping) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Ist ein Symbol jetzt/bei T in einem High-Impact-Fenster | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Lösen Sie ein Event → Symbole in einer Watchlist auf | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplexen Sie mehrere Abfragen in eine Rundfahrt | body: Array von Abfragen |
| `GET /v1/stream` (SSE) | Live Push: Veröffentlichungen/Revisionen/Fenster-Eingang | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Registrieren Sie einen HMAC-signierten Callback für Release/Revision/Blackout | body: URL, Filter, Geheimnis |
| `GET /v1/health` | Pro-Source-Aktualität + Abdeckung | — |

## Blackout — der cBot-News-Filter

`GET /v1/blackout` gibt `{ inBlackout, event, startsAt, endsAt, stale }` zurück. Bei Unsicherheit wird die **konfigurierte konservative Antwort** (Fail-Closed standardmäßig: "nehmen Sie an, in-Blackout" für Risk-Off-Bots) angenommen, plus ein `stale`-Flag — eine Datenlücke gibt nie das Grünlicht zum Handeln durch NFP. Der Endpunkt ist ein reiner DB/Cache-Read mit einem harten Server-Timeout; es gibt keinen synchronen Origin-Fetch auf dem heißen Pfad.

Ein versandter typisierter Client (`Infrastructure.Calendar.CmindCalendarClient`) wickelt dies ein: zeigen Sie seinen `HttpClient` auf die API-Root, rufen Sie `GetTokenAsync(clientId, clientSecret)` einmal auf, dann `GetBlackoutAsync(token, symbol)` vor jeder Order — es ist **Fail-Safe durch Konstruktion** (jeder Non-Success oder Parse-Fehler gibt `InBlackout = true, Stale = true` zurück, sodass eine Datenlücke nie das Grünlicht zum Handeln gibt). Ein cBot pausiert um Nachrichten so:

```csharp
// Pseudocode für einen cTrader cBot mit WebRequest + einem Calendar API Client Token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // Fail-Safe: Stale ⇒ als Blackout behandeln
    return;                                                       // Überspringen Sie neue Einträge im News-Fenster
// ...ansonsten fahren Sie fort, um die Order zu platzieren
```

## Point-in-Time für Backtests

Passieren Sie `asOf` auf jeden Read um den Kalender genau so zu erhalten, wie er zu einem Moment in der Vergangenheit stand — die Actuals, Forecasts und Revisionen *wie sie damals waren*. Da `asOf`-Reads rein und cachebar sind, schlägt ein Backtest, das History hämmert, identische Bytes erhalten, und eine backteste News-Regel verhält sich genau wie die Live-Version (kein Look-Ahead aus revidierten Werten).

## Resilienz für Algo-Aufrufer

Die API sitzt in einem Trading-Hot-Path, daher wird sie nie in einen Live-Bot geworfen: jeder Pfad gibt ein wohlgeformtes `problem+json` oder ein typisiertes heruntergestuftes Body zurück. Es wiederverwendet Copy-Trading-Resilienz-Primitive — der Standard-HTTP-Resilience-Handler auf jedem Source-Client, ein Domain-Leistungsschalter pro Source, ein Lease-bewachter Singleton-Ingestion-Worker mit Startup-Versöhnung und Health-Checks in `/health`. Der versandter typisierter Client-Snippet kommt mit Retry + Timeout + Leistungsschalter vorkonfiguriert, sodass Bot-Autoren Resilienz erben.

## Geschwister: KI-Währungsstärke (`market:read`)

Die [KI-Makro-Währungsstärke](./currency-strength.md) Read-Modell fährt die **gleiche** JWT-Maschinerie — ein Schema, ein Signaturgeheimnis, ein Rate-Limiter — Addierung nur ein `market:read`-Scope. Registrieren Sie einen API-Client mit diesem Scope, tauschen Sie ihn für ein Token wie oben aus, und rufen Sie auf:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// Erhalten Sie ein Token über POST /api/calendar/v1/token wie oben, dann:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Ein Token ohne `market:read` bekommt `403`; ein abgelaufenes/manipuliertes Token bekommt `401`. Die Endpunkte werden auf dem AI-Feature-Flag gated und unter `/api/market/v1` serviert, sodass sie unabhängig vom Kalender-Feature-Gate bleiben. Bei Run/Backtest-Versand kann eine Bereitstellung `CMIND_API_BASEURL` + ein kurzfristiges `market:read`-Token einfügen, sodass ein cBot mit Null-Client-Registrierung zurückruft.
