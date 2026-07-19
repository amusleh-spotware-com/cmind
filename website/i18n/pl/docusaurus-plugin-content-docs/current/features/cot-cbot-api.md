# COT cBot API

Dane Commitment of Traders są udostępniane botom cBot i klientom zewnętrznym przez uwierzytelniany REST API,
więc strategia może pobierać pozycjonowanie (pozycja netto, % otwartych odsetek, indeks COT) jako wejście sygnału.
Ponownie używa **ten sam mechanizm JWT i zakres `market:read`** co API rynku walut — jeden
token, jeden schemat.

## Uwierzytelnianie

1. W aplikacji wydaj klientowi API danych rynkowych (właściciel) i przyznaj mu zakres **`market:read`**.
2. Wymień identyfikator klienta/sekret na krótkotrwały token okaziciela:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Odpowiedź nosi `token`, `expiresAt` i przyznane `scopes`.
3. Wyślij token na każde wywołanie COT:

   ```http
   Authorization: Bearer <token>
   ```

Brakujący/nieprawidłowy token zwraca `401`; token bez `market:read` zwraca `403`.

## Punkty końcowe

Ścieżka bazowa `/api/market/v1/cot`. Wszystkie odpowiedzi to JSON.

| Metoda & ścieżka | Cel |
|---------------|---------|
| `GET /markets` | Katalog śledzonych rynków kontraktów. Opcjonalnie `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) i słowo kluczowe `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Najnowsza cotygodniowa migawka dla rynku. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Cotygodniowa historia w oknie. |

Parametry:

- `code` — kod rynku kontraktu CFTC (np. `099741` dla Euro FX; pobierz go z `/markets`).
- `kind` — `Legacy` (domyślnie), `Disaggregated` lub `Tff`.
- `combined` — `true` dla futures + opcji, `false` (domyślnie) dla samych futures.
- `asOf` (ISO-8601, opcjonalnie) — kotwica punktu w czasie: zwracane są tylko raporty publiczne w tym momencie,
  więc backtest nie widzi look-ahead.

### Przykład

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

## Narzędzia MCP

Ten sam model odczytów jest dostępny dla klientów AI jako narzędzia MCP: `CotMarkets`, `CotLatest`, `CotHistory`
i `CotHealth` — każdy poprawny punktu w czasie poprzez opcjonalny `asOf`. Zobacz
[funkcję Commitment of Traders](./cot-report.md) dla pełnego obrazu.

## Gating

API znajduje się za tą samą dwustopniową bramą co strona: `App:Branding:EnableCot` i `App:Features:Cot`.
Gdy którekolwiek jest wyłączone, każda trasa poniżej `/api/market/v1/cot` zwraca `404`.
