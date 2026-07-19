# COT cBot API

Commitment of Traders verilerine, kimliği doğrulanmış bir REST API üzerinden cBots ve harici
istemcilere sunulur, böylece bir strateji konumlandırmayı (net pozisyon, açık faizin yüzdesi, COT
endeksi) bir sinyal girişi olarak çekebilir. **Aynı JWT makinesi ve `market:read` kapsamını**
para birim gücü pazar API'si olarak yeniden kullanır — bir jeton, bir şema.

## Kimlik Doğrulama

1. Uygulamada, bir pazar veri istemcisi (sahibi) çıkartın ve ona **`market:read`** kapsamı verin.
2. İstemci kimliği/sırrını kısa süreli taşıyıcı jetonla değiştirin:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Yanıt `token`, `expiresAt` ve verilen `scopes`'i taşır.
3. Her COT çağrısında jetonu gönderin:

   ```http
   Authorization: Bearer <token>
   ```

Eksik/geçersiz jeton `401` döndürür; `market:read` olmayan jeton `403` döndürür.

## Uç Noktalar

Temel yol `/api/market/v1/cot`. Tüm yanıtlar JSON'dur.

| Yöntem & yol | Amaç |
|---------------|---------|
| `GET /markets` | İzlenen sözleşme-pazar kataloğu. İsteğe bağlı `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) ve `q` anahtar sözcüğü. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Bir pazar için en son haftalık anlık görüntü. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Pencere üzerinde haftalık geçmiş. |

Parametreler:

- `code` — CFTC sözleşmesi-pazarı kodu (örn. `099741` Euro FX için; `/markets`'den alın).
- `kind` — `Legacy` (varsayılan), `Disaggregated` veya `Tff`.
- `combined` — vadeli işlem + seçenekler için `true`, yalnızca vadeli işlem için `false` (varsayılan).
- `asOf` (ISO-8601, isteğe bağlı) — zaman içinde bir nokta sabitlemesi: yalnızca o anında kamuya açık
  olan raporlar döndürülür, bu nedenle geriye dönük test ileriye bakmaz.

### Örnek

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

## MCP Araçları

Aynı okuma modeli, AI istemcilerine MCP araçları olarak sunulur: `CotMarkets`, `CotLatest`,
`CotHistory` ve `CotHealth` — her biri isteğe bağlı `asOf` yoluyla zaman içinde doğru bir nokta.
Bkz.
[Commitment of Traders özelliği](./cot-report.md) tam resim için.

## Kapı

API, sayfayla aynı iki katmanlı kapının arkasındadır: `App:Branding:EnableCot` ve
`App:Features:Cot`. Her ikisi kapatıldığında `/api/market/v1/cot` altındaki her rota `404` döndürür.
