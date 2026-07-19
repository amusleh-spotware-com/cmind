# COT cBot API

I dati del Commitment of Traders sono esposti a cBot e client esterni su un'API REST autenticata,
quindi una strategia può estrarre il posizionamento (posizione netta, % dell'interesse aperto, l'indice COT) come input di segnale.
Riutilizza il **stesso macchinario JWT e l'ambito `market:read`** dell'API del mercato della forza valutaria — un
token, uno schema.

## Authentication

1. Nell'app, emetti un client API di dati di mercato (proprietario) e concedigli l'ambito **`market:read`**.
2. Scambia l'id/secret del client per un token bearer di breve durata:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   La risposta contiene `token`, `expiresAt` e gli `scopes` concessi.
3. Invia il token su ogni chiamata COT:

   ```http
   Authorization: Bearer <token>
   ```

Un token mancante/non valido restituisce `401`; un token senza `market:read` restituisce `403`.

## Endpoints

Percorso base `/api/market/v1/cot`. Tutte le risposte sono JSON.

| Method & path | Purpose |
|---------------|---------|
| `GET /markets` | Il catalogo di mercati di contratti tracciati. Facoltativo `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) e parola chiave `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | L'ultimo snapshot settimanale per un mercato. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Cronologia settimanale su una finestra. |

Parameters:

- `code` — il codice di mercato di contratto CFTC (ad es. `099741` per Euro FX; ottenilo da `/markets`).
- `kind` — `Legacy` (predefinito), `Disaggregated` o `Tff`.
- `combined` — `true` per futures + opzioni, `false` (predefinito) per solo futures.
- `asOf` (ISO-8601, opzionale) — ancoraggio point-in-time: solo i report pubblici a quell'istante vengono restituiti,
  quindi un backtest non vede look-ahead.

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

Lo stesso modello di lettura è disponibile per i client AI come strumenti MCP: `CotMarkets`, `CotLatest`, `CotHistory`
e `CotHealth` — ognuno corretto point-in-time via un `asOf` opzionale. Vedi la
[feature Commitment of Traders](./cot-report.md) per il quadro completo.

## Gating

L'API è dietro lo stesso gate a due livelli della pagina: `App:Branding:EnableCot` e `App:Features:Cot`.
Con uno spento ogni rotta sotto `/api/market/v1/cot` restituisce `404`.
