# COT cBot API

Commitment of Traders 데이터는 cBot 및 외부 클라이언트용 인증된 REST API를 통해 노출되므로, 전략이 포지셔닝(순 포지션, 미결제약정의 %, COT 지수)을 신호 입력으로 가져올 수 있습니다.
통화 강세 시장 API와 동일한 **JWT 메커니즘과 `market:read` 스코프**를 재사용합니다 — 하나의 토큰, 하나의 스키마.

## Authentication

1. 앱에서 시장 데이터 API 클라이언트(소유자)를 발급하고 **`market:read`** 스코프를 부여합니다.
2. 클라이언트 아이디/시크릿을 단기 베어러 토큰으로 교환합니다:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   응답에는 `token`, `expiresAt` 및 부여된 `scopes`가 포함됩니다.
3. 모든 COT 호출에서 토큰을 보냅니다:

   ```http
   Authorization: Bearer <token>
   ```

누락되거나 유효하지 않은 토큰은 `401`을 반환합니다; `market:read` 없는 토큰은 `403`을 반환합니다.

## Endpoints

기본 경로 `/api/market/v1/cot`. 모든 응답은 JSON입니다.

| Method & path | Purpose |
|---------------|---------|
| `GET /markets` | 추적된 계약 시장 목록. 선택적 `group`(Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) 및 키워드 `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | 시장의 최신 주간 스냅샷. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | 기간 동안의 주간 과거. |

Parameters:

- `code` — CFTC 계약 시장 코드(예: Euro FX의 경우 `099741`; `/markets`에서 얻음).
- `kind` — `Legacy`(기본값), `Disaggregated` 또는 `Tff`.
- `combined` — 선물+옵션의 경우 `true`, 선물만의 경우 `false`(기본값).
- `asOf`(ISO-8601, 선택사항) — 특정 시점 앵커: 그 시점에 공개된 보고서만 반환되므로,
  백테스트는 룩어헤드를 보지 않습니다.

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

동일한 읽기 모델은 AI 클라이언트용 MCP 도구로 사용 가능합니다: `CotMarkets`, `CotLatest`, `CotHistory`
및 `CotHealth` — 각각 선택적 `asOf`를 통해 특정 시점 정확입니다. 전체 모습은
[Commitment of Traders 기능](./cot-report.md)을 참고하세요.

## Gating

API는 페이지와 동일한 2계층 게이트 뒤에 있습니다: `App:Branding:EnableCot` 및 `App:Features:Cot`.
둘 중 하나라도 꺼지면 `/api/market/v1/cot` 아래의 모든 경로가 `404`를 반환합니다.
