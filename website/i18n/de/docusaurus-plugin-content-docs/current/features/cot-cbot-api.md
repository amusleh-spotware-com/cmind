# COT cBot API

Die Commitment of Traders-Daten werden cBots und externen Clients über eine authentifizierte REST-API offengelegt,
damit eine Strategie Positionierung (Netto-Position, % des offenen Interesses, COT-Index) als Signalinput abrufen kann.
Sie verwendet denselben **JWT-Mechanismus und Bereich `market:read`** wie die Währungsstärke-Markt-API — ein Token, ein Schema.

## Authentifizierung

1. Geben Sie in der App einen Marktdaten-API-Client (Besitzer) aus und gewähren Sie ihm den Bereich **`market:read`**.
2. Tauschen Sie die Client-ID/das Geheimnis gegen ein kurzlebiges Bearer-Token:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Die Antwort trägt `token`, `expiresAt` und die gewährten `scopes`.
3. Senden Sie das Token bei jedem COT-Aufruf:

   ```http
   Authorization: Bearer <token>
   ```

Ein fehlendes/ungültiges Token gibt `401` zurück; ein Token ohne `market:read` gibt `403` zurück.

## Endpunkte

Basispfad `/api/market/v1/cot`. Alle Antworten sind JSON.

| Methode und Pfad | Zweck |
|---------------|---------|
| `GET /markets` | Der katalog der Vertragsmarkt-Katalog. Optional `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) und `q` Stichwort. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Der neueste Wochensnapshot für einen Markt. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Wöchentlicher Verlauf über ein Fenster. |

Parameter:

- `code` — der CFTC-Kontraktmarkt-Code (z. B. `099741` für Euro FX; von `/markets` abrufen).
- `kind` — `Legacy` (Standard), `Disaggregated` oder `Tff`.
- `combined` — `true` für Futures + Optionen, `false` (Standard) für nur Futures.
- `asOf` (ISO-8601, optional) — Zeitpunktanker: Nur Berichte, die in diesem Moment öffentlich sind, werden zurückgegeben,
  damit ein Backtest kein Look-Ahead sieht.

### Beispiel

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

## MCP-Tools

Dasselbe Lesemodell steht KI-Clients als MCP-Tools zur Verfügung: `CotMarkets`, `CotLatest`, `CotHistory`
und `CotHealth` — jedes ist zeitpunktgenau über ein optionales `asOf`. Siehe
[Commitment of Traders Feature](./cot-report.md) für das Gesamtbild.

## Gating

Die API befindet sich hinter demselben zweistufigen Gating wie die Seite: `App:Branding:EnableCot` und `App:Features:Cot`.
Wenn einer deaktiviert ist, gibt jede Route unter `/api/market/v1/cot` `404` zurück.
