# COT cBot API

Данные Commitment of Traders предоставляются ботам cBot и внешним клиентам через аутентифицированный REST API,
поэтому стратегия может извлекать позиционирование (чистая позиция, % открытого интереса, индекс COT) как вход
сигнала. Он переиспользует **ту же машину JWT и область `market:read`**, что и API рынка пар валют — один
токен, одна схема.

## Аутентификация

1. В приложении выпустите клиента API данных рынка (владелец) и предоставьте ему область **`market:read`**.
2. Обменяйте id/секрет клиента на недолгоживущий токен на предъявителя:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Ответ содержит `token`, `expiresAt` и предоставленные `scopes`.
3. Отправьте токен для каждого вызова COT:

   ```http
   Authorization: Bearer <token>
   ```

Отсутствующий/неправильный токен возвращает `401`; токен без `market:read` возвращает `403`.

## Конечные точки

Базовый путь `/api/market/v1/cot`. Все ответы это JSON.

| Метод и путь | Назначение |
|---------------|---------|
| `GET /markets` | Каталог отслеживаемых контрактных рынков. Опционально `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) и ключевое слово `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Последний еженедельный снимок для рынка. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Еженедельная история за период. |

Параметры:

- `code` — код контрактного рынка CFTC (например, `099741` для Euro FX; получите его из `/markets`).
- `kind` — `Legacy` (по умолчанию), `Disaggregated` или `Tff`.
- `combined` — `true` для фьючерсов + опционов, `false` (по умолчанию) для только фьючерсов.
- `asOf` (ISO-8601, опционально) — якорь момента времени: возвращаются только отчеты, общедоступные в этот момент,
  поэтому бэктест не видит опережение.

### Пример

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

## Инструменты MCP

Та же модель чтения доступна для клиентов ИИ как инструменты MCP: `CotMarkets`, `CotLatest`, `CotHistory`
и `CotHealth` — каждый верен моменту времени через опциональный `asOf`. См.
[функцию Commitment of Traders](./cot-report.md) для полной картины.

## Gating

API находится за тем же двухуровневым шлюзом, что и страница: `App:Branding:EnableCot` и `App:Features:Cot`.
Если один из них отключен, каждый маршрут под `/api/market/v1/cot` возвращает `404`.
