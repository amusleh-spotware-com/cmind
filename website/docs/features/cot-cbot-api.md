# COT cBot API

The Commitment of Traders data is exposed to cBots and external clients over an authenticated REST API,
so a strategy can pull positioning (net position, % of open interest, the COT index) as a signal input.
It reuses the **same JWT machinery and `market:read` scope** as the currency-strength market API — one
token, one scheme.

## Authentication

1. In the app, issue a market-data API client (owner) and grant it the **`market:read`** scope.
2. Exchange the client id/secret for a short-lived bearer token:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   The response carries `token`, `expiresAt` and the granted `scopes`.
3. Send the token on every COT call:

   ```http
   Authorization: Bearer <token>
   ```

A missing/invalid token returns `401`; a token without `market:read` returns `403`.

## Endpoints

Base path `/api/market/v1/cot`. All responses are JSON.

| Method & path | Purpose |
|---------------|---------|
| `GET /markets` | The tracked contract-market catalog. Optional `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) and `q` keyword. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | The latest weekly snapshot for a market. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Weekly history over a window. |

Parameters:

- `code` — the CFTC contract market code (e.g. `099741` for Euro FX; get it from `/markets`).
- `kind` — `Legacy` (default), `Disaggregated` or `Tff`.
- `combined` — `true` for futures + options, `false` (default) for futures-only.
- `asOf` (ISO-8601, optional) — point-in-time anchor: only reports public at that instant are returned,
  so a backtest sees no look-ahead.

### Example

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

## MCP tools

The same read model is available to AI clients as MCP tools: `CotMarkets`, `CotLatest`, `CotHistory`
and `CotHealth` — each point-in-time correct via an optional `asOf`. See the
[Commitment of Traders feature](./cot-report.md) for the full picture.

## Gating

The API is behind the same two-tier gate as the page: `App:Branding:EnableCot` and `App:Features:Cot`.
With either off every route under `/api/market/v1/cot` returns `404`.
