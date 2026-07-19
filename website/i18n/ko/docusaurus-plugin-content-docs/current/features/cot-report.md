# Commitment of Traders (COT)

cMind는 내장 **Commitment of Traders** 보고서를 제공합니다 — 미국 선물 시장에서의 롱·숏 포지션의 주간 CFTC 분석(상업적 헤지, 대형 투기꾼, 펀드), 대화형 과거 차트, 정규화된 **COT 지수**, cBot용 인증된 REST API, AI 클라이언트용 MCP 도구가 포함됩니다. 데이터는 **CFTC 공개 Socrata 데이터셋**에서 직접 가져옵니다 — API 키 없음, 집계자 없음. 경제 달력과 마찬가지로 거래 핵심에 영향을 주지 않고 비활성화할 수 있는 분리된 모듈입니다.

## What it gives you

- **모든 3가지 보고서 형식, 선물만 또는 선물+옵션 결합:**
  - **Legacy** — 비상업(대형 투기꾼), 상업(헤지), 보고불가.
  - **Disaggregated** — 생산자/상인, 스왑 딜러, 운용 자산, 기타 보고 대상.
  - **Traders in Financial Futures (TFF)** — 딜러, 자산 관리자, 레버리지 펀드, 기타 보고 대상.
- **큐레이션된 시장 목록** — FX 메이저, 금/은/구리, 원유 & 천연가스, 국채, 주가지수, 암호화폐 및 주요 곡물/소프트 상품 — 각각 안정적인 CFTC 계약 코드로 매핑되고 명확한 경우 거래 가능한 심볼로 매핑됩니다(예: Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **COT 지수(0–100)** — 현재 투기꾼의 순 포지션이 과거 범위 내 어디에 있는지(기본값 ~3년 룩백). 극값 근처의 수치는 종종 반전을 선행하는 혼잡한 포지셔닝을 표시합니다; 보고서는 **롱 극값**(≥80) 또는 **숏 극값**(≤20)으로 표시합니다.
- **특정 시점 정확성.** 주간 보고서는 화요일에 측정되지만 다음 금요일에만 공개됩니다; 모든 읽기가 그 공개 순간을 준수하므로 백테스트된 포지셔닝 신호는 공개 전 보고서를 볼 수 없습니다(룩어헤드 없음).

## Using the page

왼쪽 네비게이션에서 **Commitment of Traders**를 엽니다. **시장**과 **보고서 타입**(Legacy /
Disaggregated / Financial)을 선택하고 **Futures + options**을 토글하여 선물만과 결합 버전 사이를 전환합니다. 페이지는 다음을 표시합니다:

- **시간별 순 포지셔닝** — 역사 기간 전체에 걸친 각 트레이더 카테고리의 순 포지션(롱 − 숏)의 대화형 선 차트.
- **COT 지수** — 0–100 지수의 선 차트, 최신 수치 및 극값 레이블 포함.
- **최신 스냅샷** — 트레이더 카테고리별 롱/숏/순/미결제약정% 표, 플러스 총 미결제약정 및 보고서 날짜.

## How the data flows

주간 수집 워커는 추적된 시장의 6개 CFTC 데이터셋을 가져오고 시장 목록을 업데이트한 후 각 새 보고서를 **멱등적으로** 추가합니다(재실행은 스냅샷을 복제하지 않음). 첫 번째 실행은 여러 해의 과거를 채웁니다; 이후 실행은 최근 주를 재동기화하여 늦은 수정을 포착합니다.
모든 것은 키 없이 기본 제공됩니다; 선택적 Socrata 앱 토큰은 레이트 제한만 높입니다.

## Configuration

모든 키는 `App:Cot` 아래에 있습니다([feature toggles](./feature-toggles.md) 및
[white-label owner settings](./white-label-owner-settings.md) 참고):

| Key | Default | Purpose |
|-----|---------|---------|
| `IngestionEnabled` | `true` | 주간 수집 워커 실행 여부. |
| `PollInterval` | `6h` | 워커가 CFTC 데이터셋을 폴링하는 빈도. |
| `BackfillYears` | `5` | 첫 실행 시 가져올 과거 연도 수. |
| `ReconcileLookbackWeeks` | `4` | 각 사이클에서 수정을 포착하기 위해 재동기화될 최근 주 수. |
| `SocrataAppToken` | — | 익명 레이트 제한을 높이는 선택적 토큰. |
| `CotIndexLookbackWeeks` | `156` | COT 지수 범위로 사용되는 주간 보고서(~3년). |

## Gating

가시성은 경제 달력과 동일한 2계층 게이트입니다: 화이트라벨 하드 게이트
`App:Branding:EnableCot`(빌드 수준) **및** 런타임 기능 토글 `App:Features:Cot`. 둘 중 하나라도 꺼지면 네비게이션 링크, 페이지, REST API 및 MCP 도구가 모두 사라집니다(API는 `404` 반환). 데이터 소스가 키리스이므로 데이터 소스 키 게이트가 없습니다 — 활성화됨은 보이는 것을 의미합니다.

## For developers

- Domain: `Core.Cot` — `CotMarket` 및 `CotReport` 집계, `CotPositions` 값 객체,
  `CotIndexCalculator` 도메인 서비스, 및 `ICotReports` / `ICotSource` 포트.
- Infrastructure: `Infrastructure.Cot` — `CftcSocrataSource` 안티컬럽션 파서, 레이트 게이트,
  추가 전용 쓰기 서비스, 읽기 쪽 및 주간 수집 워커(EF `cot` 스키마).
- cBot & AI 액세스: [COT cBot API](./cot-cbot-api.md)(REST, `market:read` JWT) 및 MCP 도구
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
