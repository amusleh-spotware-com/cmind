# Calendrier REST & API cBot

Le calendrier économique est exposé en tant que **API REST versionnée, sécurisée par JWT, avec limite de débit** — la surface d'intégration phare. Tout service externe, tableau de bord ou cBot s'intègre contre elle comme un produit. Elle a une parité de fonctionnalités avec l'API du calendrier FXStreet et la dépasse : `asOf` point-dans-le-temps, chaînes de révision complètes, rationale d'impact déterministe, analytique des surprises, résolution pays→symbole et mathématiques de blackout que d'autres API de calendrier n'exposent pas.

> **Statut.** La sécurité JWT (émission de client + échange de jetons), le gating et les points de terminaison de lecture centraux — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` — sont **implémentés et intégration-testés** (auth, application des scopes, 404 des fonctionnalités/white-label), plus **`events/batch`** (multiplex borné) et document **`/openapi.json`** découvrable, **`ETag`/`If-None-Match` 304** sur les lectures event/history, et **pagination du curseur de keyset** (`Link: rel="next"`), le **flux SSE** (push `event: release` en direct, poll-backed), **webhooks signés HMAC** (`X-CMind-Signature: sha256=…`, enregistrés par le propriétaire, livrés par un worker gated de config avec un repère persistant), et la **client typé** expédiée (`CmindCalendarClient`). La surface d'API publique complète est implémentée.

## Sécurité — JWT

L'API réutilise la machinerie de jetons HS256 existante du repo (le même modèle que les agents CtraderCliNode utilisent), pas un nouveau schéma :

- Un administrateur d'application émet un **client API Calendrier** (nom + scopes + expiration). Le client échange son id et son secret sur `POST /api/calendar/v1/token` pour un **JWT HS256 de courte durée** (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, réclamation `scope`). Seul le JWT court monte sur les requêtes (`Authorization: Bearer <jwt>`).
- Le secret du client est stocké **chiffré** via `ISecretProtector` — jamais en clair, jamais enregistré.
- **Scopes** (moindre privilège) : `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. Un jeton cBot reçoit généralement `read` + `blackout` uniquement.
- Validation `JwtBearer` standard (émetteur, audience, durée de vie, clé de signature ; `alg=none` rejeté ; skew d'horloge serré). Limite de débit par client token-bucket + limiteur global ; `429` avec `Retry-After`. Tous les échecs d'auth sont audités.
- La désactivation du client arrête l'émission de jetons futurs immédiatement ; la durée de vie courte du JWT limite un jeton divulgué. L'ensemble de l'arborescence `/api/calendar/**` `404`s quand la fonctionnalité est désactivée.

## Conventions

- **Chemin de base & versioning :** `/api/calendar/v1/...` (versionné par URL ; les changements additifs ne poussent pas).
- **Format :** JSON ; instants UTC RFC 3339 plus un `sourceTimeZone` explicite ; `tz=` optionnel rend une heure locale pratique sans perdre l'ancre UTC.
- **Pagination :** basée sur curseur (`cursor`, `limit` ≤ 1000) ; curseur `next` dans le corps et en-tête `Link`.
- **Cache :** `ETag` + `If-None-Match` ; les plages historiques obtiennent un TTL long, à venir un court.
- **Erreurs :** RFC 7807 `problem+json`, jamais un `500` nu.
- **Lectures dégradées :** une source/DB fault retourne `200` meilleures données connues plus un signal `X-Calendar-Freshness` / `stale=true` (ou `503 Retry-After` seulement si vraiment rien n'est connu) — le cBot décide.

## Points de terminaison

| Méthode & chemin | Objectif | Paramètres clés |
|---|---|---|
| `POST /v1/token` | Échange client id+secret → JWT court | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Événements dans une fenêtre (à venir ou historique) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Un événement : chaîne de révision complète, surprise, rationale d'impact, symboles affectés | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Historique de révision ordonné | — |
| `GET /v1/history` | Tirage historique profond pour une série (≥10 ans) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalogue des indicateurs suivis + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Série z-score actual/forecast/surprise historique | `series`,`count`/`from,to` |
| `GET /v1/next` | Prochaine sortie pertinente pour un symbole (pays→symbole mappé) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Un symbole est-il dans une fenêtre d'impact élevé maintenant/en T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Résoudre un événement → symboles dans une watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex plusieurs requêtes en un seul aller-retour | body: tableau de requêtes |
| `GET /v1/stream` (SSE) | Push en direct : sorties/révisions/entrée-fenêtre | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Enregistrer un rappel signé HMAC pour sortie/révision/blackout | body: url, filtres, secret |
| `GET /v1/health` | Fraîcheur par source + couverture | — |

## Blackout — le filtre d'actualités cBot

`GET /v1/blackout` retourne `{ inBlackout, event, startsAt, endsAt, stale }`. En cas d'incertitude, il revient à la **réponse conservatrice configurée** (fail-closed par défaut : "supposer en-blackout" pour les bots risk-off), plus un drapeau `stale` — un écart de données ne vert-light jamais le commerce via NFP. Le point de terminaison est une pure lecture DB/cache avec un timeout serveur dur ; il n'y a pas de récupération d'origine synchrone sur le chemin chaud.

Un client typé expédié (`Infrastructure.Calendar.CmindCalendarClient`) l'enveloppe : pointez son `HttpClient` sur la racine de l'API, appelez `GetTokenAsync(clientId, clientSecret)` une fois, puis `GetBlackoutAsync(token, symbol)` avant chaque ordre — c'est **fail-safe par construction** (toute non-réussite ou erreur d'analyse retourne `InBlackout = true, Stale = true`, donc un écart de données ne vert-light jamais le commerce). Un cBot pause autour des actualités comme ceci :

```csharp
// Pseudocode pour un cBot cTrader utilisant WebRequest + un jeton client API Calendrier.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ traiter comme blackout
    return;                                                       // sauter les nouvelles entrées dans la fenêtre d'actualités
// ...sinon procéder au placement de l'ordre
```

## Point-dans-le-temps pour les backtests

Passez `asOf` sur n'importe quelle lecture pour obtenir le calendrier exactement comme il était à un instant passé — les réels, les prévisions et les révisions *comme ils étaient alors*. Parce que les lectures `asOf` sont pures et cachables, un backtest martelant l'historique obtient des octets identiques à chaque fois, et une règle d'actualité backtestée se comporte exactement comme la règle en direct (pas d'anticipation à partir de valeurs révisées).

## Résilience pour les appelants d'algo

L'API s'assoit dans un chemin commercial chaud, donc elle ne jette jamais dans un bot en direct : chaque chemin retourne un `problem+json` bien formé ou un corps dégradé typé. Elle réutilise les primitives de résilience du copie-trading — le gestionnaire de résilience HTTP standard sur chaque client source, un disjoncteur de domaine par source, un worker d'ingestion singleton gardé par bail avec réconciliation de démarrage, et des contrôles de santé câblés sur `/health`. Le snippet client typé expédié est livré avec retry + timeout + circuit-breaker préconfigurés pour que les auteurs de bots héritent de la résilience.

## Frère : force de devise IA (`market:read`)

La [force de devise macro IA](./currency-strength.md) modèle de lecture monte la **même** machinerie JWT — un schéma, un secret de signature, un limiteur de débit — ajoutant seulement une portée `market:read`. Enregistrez un client API avec ce scope, échangez-le pour un jeton exactement comme ci-dessus, et appelez :

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtenir un jeton via POST /api/calendar/v1/token comme ci-dessus, puis :
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Un jeton manquant `market:read` obtient `403` ; un jeton expiré/falsifié obtient `401`. Les points de terminaison sont gatés sur le drapeau de fonctionnalité IA et servis sous `/api/market/v1` afin qu'ils restent indépendants de la porte de fonctionnalité du calendrier. À la dispatch de lancer/backtest, un déploiement peut injecter `CMIND_API_BASEURL` + un jeton `market:read` de courte durée afin qu'un cBot rappelle avec zéro enregistrement de client.
