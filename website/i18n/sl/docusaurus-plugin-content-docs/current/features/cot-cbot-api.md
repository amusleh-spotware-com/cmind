# COT cBot API

Podatki Commitment of Traders so izpostavljeni cBotom in zunanjim odjemalcem prek avtenticiranega REST API,
tako da strategija lahko premakne pozicioniranje (neto položaj, % odprtega interesa, COT indeks) kot
vhod signala. Ponovno uporablja **isti mehanizem JWT in obseg `market:read`** kot API tržnega valutnega
vrednost — en ključ, ena shema.

## Avtentifikacija

1. V aplikaciji izdajte odjemalca podatkov o trgu (lastnika) in mu dodelite obseg **`market:read`**.
2. Zamenjajte ID/skrivnost odjemalca za kratkoročni nosilni ključ:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Odgovor nosi `token`, `expiresAt` in dopuščene `scopes`.
3. Pošljite ključ na vsakem COT pozivu:

   ```http
   Authorization: Bearer <token>
   ```

Manjkajoči/neveljaven ključ vrne `401`; ključ brez `market:read` vrne `403`.

## Končne točke

Osnovna pot `/api/market/v1/cot`. Vsi odgovori so JSON.

| Način in pot | Namen |
|---------------|---------|
| `GET /markets` | Katalog sledenja pogodbe-tržne. Izbirni `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) in `q` ključna beseda. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Najnovejša tedensko slika za tržo. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Tedenska zgodovina čez okno. |

Parametri:

- `code` — CFTC koda pogodbe na tržo (npr. `099741` za Euro FX; pridobite ga iz `/markets`).
- `kind` — `Legacy` (privzeto), `Disaggregated` ali `Tff`.
- `combined` — `true` za termine + opcije, `false` (privzeto) samo za termine.
- `asOf` (ISO-8601, izbirni) — sidro točka v času: samo poročila, javna v tem trenutku, so
  vrnjena, zato pregled napak ne vidi vsega spredaj.

### Primer

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

## MCP orodja

Isti model branja je dostopen AI odjemalcem kot MCP orodja: `CotMarkets`, `CotLatest`, `CotHistory`
in `CotHealth` — vsak točka-v-času pravilen prek izbirnega `asOf`. Glejte
[funkcijo Commitment of Traders](./cot-report.md) za polno sliko.

## Vrata

API je za istimi dvostopenjskima vrati kot stran: `App:Branding:EnableCot` in `App:Features:Cot`.
S katerim koli zaprtem vsaka pot pod `/api/market/v1/cot` vrne `404`.
