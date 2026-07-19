# COT cBot API

Podaci Commitment of Traders su izloženi cBots-ima i spoljnim klijentima preko autentifikovanog REST API-ja,
tako da strategija može povući pozicioniranje (neto pozicija, % otvorene kamate, COT indeks) kao
ulazni signal. Ponovo koristi **istu JWT mašineriju i `market:read` obim** kao API valute-tržišta —
jedan ključ, jedan šem.

## Autentifikacija

1. U aplikaciji izdajte klijenta podataka o tržištu (vlasnik) i dodelite mu **`market:read`** obim.
2. Razmenjujte ID/tajnu klijenta za kratkotrajan token nosivača:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Odgovor nosi `token`, `expiresAt` i dodeljene `scopes`.
3. Pošaljite token na svaki COT poziv:

   ```http
   Authorization: Bearer <token>
   ```

Nedostaju token/nevažeći vraća `401`; token bez `market:read` vraća `403`.

## Krajnje tačke

Osnovna putanja `/api/market/v1/cot`. Svi odgovori su JSON.

| Metoda i putanja | Svrha |
|---------------|---------|
| `GET /markets` | Praćeni katalogu pogodbe-tržišta. Opcija `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) i `q` ključna reč. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Najnovija nedenja slika za tržište. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Nedenja istorija preko prozora. |

Parametri:

- `code` — CFTC šifra pogodbe tržišta (npr. `099741` za Euro FX; pridobijte je iz `/markets`).
- `kind` — `Legacy` (zadana vrednost), `Disaggregated` ili `Tff`.
- `combined` — `true` za fučerse + opcije, `false` (zadana vrednost) samo za fučerse.
- `asOf` (ISO-8601, opcija) — sidro tačke u vremenu: samo izveštaji javni u tom trenutku su vraćeni,
  tako da backtest ne vidi bez gleda unapred.

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

## MCP alata

Isti model čitanja je dostupan AI klijentima kao MCP alata: `CotMarkets`, `CotLatest`, `CotHistory`
i `CotHealth` — svaka tačka-u-vremenu ispravna preko opcije `asOf`. Pogledajte
[funkciju Commitment of Traders](./cot-report.md) za целу sliku.

## Zaključavanje

API je iza istog dvostepenog zaključavanja kao stranica: `App:Branding:EnableCot` i `App:Features:Cot`.
Sa bilo kojim isključenim svaka ruta pod `/api/market/v1/cot` vraća `404`.
