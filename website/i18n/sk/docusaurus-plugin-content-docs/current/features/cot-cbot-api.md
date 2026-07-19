# COT cBot API

Údaje o Commitment of Traders sú vystavené ľuďom cBot a externým klientom prostredníctvom autentifikovaného
REST API, aby stratégia mohla ťahať pozície (čistá pozícia, % otvorený záujem, index COT) ako vstup signálu.
Opätovne používa **rovnaký mechanizmus JWT a rozsah `market:read`** ako API trhu pár mien — jeden
token, jedna schéma.

## Autentifikácia

1. V aplikácii vydajte klienta API údajov trhu (vlastník) a udeľte mu rozsah **`market:read`**.
2. Vymeňte id/tajný kľúč klienta za krátkotrvajúci token nositeľa:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Odpoveď obsahuje `token`, `expiresAt` a udeleného `scopes`.
3. Poskytnite token pri každom volaní COT:

   ```http
   Authorization: Bearer <token>
   ```

Chýbajúci/neplatný token vráti `401`; token bez `market:read` vráti `403`.

## Koncové body

Základná cesta `/api/market/v1/cot`. Všetky odpovede sú JSON.

| Metóda a cesta | Účel |
|---------------|---------|
| `GET /markets` — Katalóg sledovaných trhov zmlúv. Voliteľne `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) a kľúčové slovo `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` — Najnovší týždenný snímok pre trh. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` — Týždenná história v okne. |

Parametre:

- `code` — kód trhu zmlúvy CFTC (napr. `099741` pre Euro FX; získajte ho z `/markets`).
- `kind` — `Legacy` (predvolené), `Disaggregated` alebo `Tff`.
- `combined` — `true` pre futures + opcie, `false` (predvolené) pre iba futures.
- `asOf` (ISO-8601, voliteľne) — ukotvenie bodu v čase: vrátia sa iba správy verejné v danom okamihu,
  takže backtest nevníma look-ahead.

### Príklad

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## Nástroje MCP

Rovnaký model čítania je dostupný pre klientov AI ako nástroje MCP: `CotMarkets`, `CotLatest`, `CotHistory`
a `CotHealth` — každý presný bod v čase cez voliteľný `asOf`. Pozri
[funkciu Commitment of Traders](./cot-report.md) pre úplný obraz.

## Gating

API je za rovnakou dvoustupňovou bránou ako stránka: `App:Branding:EnableCot` a `App:Features:Cot`.
Ak je jeden vypnutý, každá cesta pod `/api/market/v1/cot` vráti `404`.
