# COT cBot API

Data Commitment of Traders jsou vystavena cBots a externím klientům přes ověřené REST API,
aby strategie mohla vytahovat pozicování (čisté postavení, % otevřeného zájmu, index COT) jako vstup signálu.
Znovu používá **stejný mechanismus JWT a rozsah `market:read`** jako API trhu síly měny — jeden token, jedno schéma.

## Ověřování

1. V aplikaci vydejte klienta tržních dat (vlastníka) a udělte mu rozsah **`market:read`**.
2. Vyměňte ID/tajemství klienta za krátkodobý token nosiče:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Odpověď nese `token`, `expiresAt` a udělené `scopes`.
3. Odešlete token v každém volání COT:

   ```http
   Authorization: Bearer <token>
   ```

Chybějící/neplatný token vrací `401`; token bez `market:read` vrací `403`.

## Koncové body

Základní cesta `/api/market/v1/cot`. Všechny odpovědi jsou JSON.

| Metoda a cesta | Účel |
|---------------|---------|
| `GET /markets` | Katalog trhů se sledovanou smlouvou. Volitelné `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) a klíčové slovo `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Nejnovější týdenní snímek trhu. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Týdenní historie v okně. |

Parametry:

- `code` — kód trhu smlouvy CFTC (např. `099741` pro Euro FX; získejte z `/markets`).
- `kind` — `Legacy` (výchozí), `Disaggregated` nebo `Tff`.
- `combined` — `true` pro futures + opce, `false` (výchozí) pro samotné futures.
- `asOf` (ISO-8601, volitelné) — bod v čase: vráceny jsou pouze zprávy veřejné v ten okamžik,
  aby backtest neviděl předvídání.

### Příklad

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

## MCP nástroje

Stejný model čtení je dostupný klientům AI jako MCP nástroje: `CotMarkets`, `CotLatest`, `CotHistory`
a `CotHealth` — každý je správný bod v čase přes volitelné `asOf`. Podívejte se na
[funkci Commitment of Traders](./cot-report.md) pro úplný obrázek.

## Uzavření

API je za stejnou dvoustupňovou bránou jako stránka: `App:Branding:EnableCot` a `App:Features:Cot`.
Je-li buď vypnutá, každá trasa pod `/api/market/v1/cot` vrací `404`.
