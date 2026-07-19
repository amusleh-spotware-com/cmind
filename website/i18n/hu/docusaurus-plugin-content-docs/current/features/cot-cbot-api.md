# COT cBot API

A Kereskedők Elkötelezettsége adatai cBotoknak és külső klienseknek egy hitelesített REST API-n keresztül vannak kitéve, így egy stratégia lekérhet pozicionálást (nettó pozíciót, nyílt kamat %-át, COT indexet) jel bemenetként. Újrahasználja ugyanazt a **JWT mechanizmust és `market:read` hatókört** mint a valuta-erősség piaci API — egy token, egy séma.

## Hitelesítés

1. Az alkalmazásban adjon ki egy piaci adatok API klienst (tulajdonos) és adja meg a **`market:read`** hatókört.
2. Cserélje ki az ügyfél azonosítóját/titkot egy rövid élettartamú bejelentkezési tokenre:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   A válasz tartalmazza a `token`, `expiresAt` és az adott `scopes` értékeket.
3. Küldje el a tokent minden COT híváson:

   ```http
   Authorization: Bearer <token>
   ```

A hiányzó/érvénytelen token `401` értéket ad vissza; a `market:read` nélküli token `403` értéket ad vissza.

## Végpontok

Alap útvonal `/api/market/v1/cot`. Minden válasz JSON formátum.

| Metódus és útvonal | Cél |
|---------------|---------|
| `GET /markets` | A nyomon követett szerződés-piaci katalógus. Opcionális `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) és `q` kulcsszó. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | A legutolsó heti pillanatkép egy piacról. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Heti történet egy ablak felett. |

Paraméterek:

- `code` — a CFTC szerződés piaci kódja (pl. `099741` Euro FX-hez; szerezze meg a `/markets` adatokból).
- `kind` — `Legacy` (alapértelmezett), `Disaggregated` vagy `Tff`.
- `combined` — `true` határidősökhöz + opciók, `false` (alapértelmezett) csak határidősökhöz.
- `asOf` (ISO-8601, opcionális) — pontbeli idő horgony: csak az adott pillanatban nyilvános jelentések adódnak vissza, így egy backtest nem lát előrelátást.

### Példa

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

## MCP eszközök

Ugyanez az olvasási modell AI kliensek számára MCP eszközökként érhető el: `CotMarkets`, `CotLatest`, `CotHistory` és `CotHealth` — mindegyik pontbeli időben helyes egy opcionális `asOf` segítségével. Tekintse meg a [Kereskedők Elkötelezettsége funkció](./cot-report.md) a teljes képhez.

## Kapu

Az API ugyanazon kétszintű kapu mögött van, mint az oldal: `App:Branding:EnableCot` és `App:Features:Cot`. Az egyik kikapcsolása esetén az `/api/market/v1/cot` alatti minden útvonal `404` értéket ad vissza.
