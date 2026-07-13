# 캘린더 REST 및 cBot API

경제 캘린더는 **버전화된, JWT 보안, 속도 제한 REST API**로 공개됩니다 — 플래그십 통합 표면입니다. 외부 서비스, 대시보드 또는 cBot이 제품으로 통합합니다. FXStreet 캘린더 API와 기능 패리티가 있으며 시점 `asOf`, 완전한 수정 체인, 결정론적 영향 근거, 서프라이즈 분석, 국가→심볼 해석 및 다른 캘린더 API가 공개하지 않는 블라인드아웃 수학을 능가합니다.

> **상태.** JWT 보안 (클라이언트 발급 + 토큰 교환), 게이팅 및 핵심 읽기 엔드포인트 — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` — **구현 및 통합 테스트됨** (인증, 범위 적용, 기능/화이트라벨 404), plus **`events/batch`** (제한된 멀티플렉스) 및 검색 가능한 **`/openapi.json`** 문서, **`ETag`/`If-None-Match` 304** 이벤트/이력 읽기, **키셋 커서 페이지네이션** (`Link: rel="next"`), **SSE `stream`** (라이브 `event: release` 푸시, 폴링 지원), **HMAC 서명 웹훅** (`X-CMind-Signature: sha256=…`, 소유자 등록, 구성 게이트된 작업자의 지속 워터마크에서 제공) 및 제공된 **타입 클라이언트** (`CmindCalendarClient`). 전체 공개 API 표면이 구현되었습니다.

## 보안 — JWT

API는 기존 HS256 토큰 기계([CtraderCliNode 에이전트](./adr/0003-external-nodes-http-jwt.md)와 동일한 패턴)를 재사용합니다, 새 체계가 아닙니다:

- 앱 관리자가 **캘린더 API 클라이언트**를 발급합니다 (이름 + 범위 + 만료). 클라이언트가 `POST /api/calendar/v1/token`에서 id 및 secret을 교환하여 단기 HS256 JWT (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15분, `scope` 클레임)를 받습니다. 단기 JWT만 요청에 탑니다 (`Authorization: Bearer <jwt>`).
- 클라이언트 secret은 `ISecretProtector`를 통해 암호화되어 저장됩니다 — 절대 평문, 절대 로그 안 함.
- **범위** (최소 권한): `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. cBot 토큰은 일반적으로 `read` + `blackout`만 얻습니다.
- 표준 `JwtBearer` 검증 (발급자, 청중, 수명, 서명 키; `alg=none` 거부; 엄격한 시계 기울기). 클라이언트별 토큰 버킷 속도限制 + 전역 제한기; `429` 및 `Retry-After`. 모든 인증 실패는 감사됩니다.
- 클라이언트 비활성화는 향후 토큰 발급을 즉시 중지합니다; 단기 JWT 수명이 leak된 토큰을 제한합니다. 기능이 비활성화되면 전체 `/api/calendar/**` 트리가 `404`를 반환합니다.

## 규칙

- **기본 경로 및 버전 관리:** `/api/calendar/v1/...` (URL 버전 관리; 추가 변경은 버전 업 안 함).
- **형식:** JSON; RFC 3339 UTC instants plus 명시적 `sourceTimeZone`; 선택적 `tz=`는 UTC 앵커를 잃지 않고 편의 현지 시간을 렌더링합니다.
- **페이지네이션:** 커서 기반 (`cursor`, `limit` ≤ 1000); 본문 및 `Link` 헤더의 `next` 커서.
- **캐싱:** `ETag` + `If-None-Match`; 과거 범위는 긴 TTL, 다가오는 것은 짧은 TTL.
- **오류:** RFC 7807 `problem+json`, 절대로裸露 `500` 안 함.
- **Degraded 읽기:** 소스/DB 결함은 `200` 최선의 알려진 데이터 plus `X-Calendar-Freshness` / `stale=true` 신호(또는 진짜로 아무것도 알려지지 않은 경우에만 `503 Retry-After`)를 반환합니다 — cBot이 결정합니다.

## 엔드포인트

| 메서드 및 경로 | 목적 | 주요 매개변수 |
|---|---|---|
| `POST /v1/token` | 클라이언트 id+secret → 단기 JWT 교환 | body: `clientId`, `clientSecret` |
| `GET /v1/events` | 창 내 이벤트 (다가오거나 과거) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | 하나의 이벤트: 완전한 수정 체인, 서프라이즈, 영향 근거, 영향 심볼 | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | 순서화된 수정 이력 | — |
| `GET /v1/history` | 시리즈의 심층 과거 풀 (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | 추적 중인 표시기 + 캐던스 + 출처의 카탈로그 | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | 과거 actual/forecast/서프라이즈 z-점수 시리즈 | `series`,`count`/`from,to` |
| `GET /v1/next` | 심볼에 대한 다음 관련 발표 (국가→심볼 매핑됨) | `symbol`,`minImpact` |
| `GET /v1/blackout` | 심볼이 지금/at T의 높은 영향 창 내에 있는가 | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | 이벤트 → 시계 목록의 심볼 해석 | `eventId`,`watchlist` |
| `POST /v1/events:batch` | 하나의 라운드트립에서 여러 쿼리 멀티플렉스 | body: 쿼리 배열 |
| `GET /v1/stream` (SSE) | 라이브 푸시: releases/revisions/window-enter | `currencies`,`minImpact` (범위 `calendar:stream`) |
| `POST /v1/webhooks` | release/revision/blackout용 HMAC 서명 콜백 등록 | body: url, 필터, secret |
| `GET /v1/health` | 소스별新鲜도 + 커버리지 | — |

## 블라인드아웃 — cBot 뉴스 필터

`GET /v1/blackout`은 `{ inBlackout, event, startsAt, endsAt, stale }`를 반환합니다. 불확실성 시 **구성된 보수적 답변**으로 기본값됩니다 (fail-closed: risk-off 봇은 "블라인드아웃 가정"), plus `stale` 플래그 — 데이터 격차가 NFP를 통해 거래를 녹색으로 만들지 않습니다. 엔드포인트는 핫 패스에서 동기 원본 가져오기 없이 하드 서버 시간限制으로 순수 DB/캐시 읽기입니다.

제공된 타입 클라이언트 (`Infrastructure.Calendar.CmindCalendarClient`)가 이를 래핑합니다: `HttpClient`를 API 루트에 지정하고 `GetTokenAsync(clientId, clientSecret)`를 한 번 호출한 다음 `GetBlackoutAsync(token, symbol)`를 각 주문 전 호출합니다 — **fail-safe 구축** (비성공 또는 구문 분석 오류는 `InBlackout = true, Stale = true`를 반환하여 데이터 격차가 거래를 녹색으로 만들지 않음). cBot은 다음과 같이 뉴스 주변에서 일시 중지합니다:

```csharp
// cTrader cBot이 WebRequest + 캘린더 API 클라이언트 토큰을 사용하는 의사코드.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## 백테스트를 위한 시점

읽기에서 `asOf`를 전달하여 과거瞬시로 정확히 그 시점에 보았던 대로 캘린더를 얻습니다 — 실제, 예측 및 수정이 *그때였는지*를 확인합니다. `asOf` 읽기가 순수하고 캐시 가능하므로 이력을锤子하는 백테스트가 매번 동일한 바이트를 얻으며, 백테스트된 뉴스 규칙은 라이브와 정확히 동일하게 동작합니다 (수정된 값의 look-ahead 없음).

## algo 호출자를 위한 レジリエンス

API는 거래 핫 패스에 있으므로 라이브 봇에 절대 예외를 발생시키지 않습니다: 모든 경로가 잘 형성된 `problem+json` 또는 유형화된 degraded 본문을 반환합니다. 복사 트레이딩의 レジリエンス 기본 요소를 재사용합니다 — 각 소스 클라이언트의 표준 HTTP レジリエンス 핸들러, 소스별 도메인 서킷 브레이커, 시작 재조정와 함께 스타트업 재조정, `/health`에 연결된 건강 확인. 제공된 타입 클라이언트 스니펫에는 재시도 + 시간限制 + 서킷 브레이커가 미리 구성되어 있으므로 봇 작성자가 레지リエンス를 상속합니다.

## 자매: AI 통화 강도 (`market:read`)

[AI 매크로 통화 강도](./currency-strength.md) 읽기 모델은 동일한 JWT 기계 — 하나의 체계, 하나의 서명 secret, 하나의 속도限制로 rides하며 `market:read` 범위만 추가합니다. 해당 범위로 API 클라이언트를 등록하고, 위와 동일하게 토큰을 교환하고, 다음을 호출합니다:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// 위의 POST /api/calendar/v1/token으로 토큰을 얻은 후:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

`market:read`가 없는 토큰은 `403`을 얻습니다; 만료/변조된 토큰은 `401`을 얻습니다. 엔드포인트는 AI 기능 플래그에 게이트되며 `/api/market/v1`에서 제공되므로 캘린더 기능 게이트와 독립적으로 유지됩니다. 실행/백테스트 디스패치에서 배포는 `CMIND_API_BASEURL` + 단기 `market:read` 토큰을 주입하여 cBot이 클라이언트 등록 없이 콜백할 수 있습니다.
