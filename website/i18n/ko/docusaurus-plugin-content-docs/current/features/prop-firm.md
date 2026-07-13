---
description: "소매 펀드(FTMO 스타일)는 평가 계정을 판매합니다: 트레이더는 펀드되기 전에 수익 목표를 달성하면서 위험 제한(최대 일일 손실, 최대 총/트레일링 드로우다운, 일관성, 시간 제한) 내에서 유지해야 합니다."
---

# Prop-firm 챌린지 시뮬레이션

소매 펀드(FTMO 스타일)는 **평가 계정**을 판매합니다: 트레이더는 펀드되기 전에 수익 목표를 달성하면서 위험 제한(최대 일일 손실, 최대 총/트레일링 드로우다운, 일관성, 시간 제한) 내에서 유지해야 합니다. cMind를 사용하면 사용자가 **모든 업계 형태의 커스텀 챌린지**를 생성하고 `TradingAccount`에 바인딩하고, **카피트레이딩 작업처럼 실행** — 시작/중지, 노드에서 호스트, **cTrader Open API를 통해 라이브 추적**할 수 있습니다. Aggregate가 모든 규칙을 결정론적으로 평가합니다; 통과 또는 위반 시 챌린지를 종료하고 표시하며 사용자에게 알립니다.

## 도메인 (제한된 컨텍스트: PropFirm)

`PropFirmChallenge` = aggregate root(모듈 `Core.PropFirm`), 강력한 ID로만 `TradingAccount`를 참조합니다(크로스 aggregate FK 없음). 자체 규칙 평가, 단계/상태 머신, 노드 임대를 소유합니다.

### 값 개체 및 규칙 세트

- **`Money`**(음이 아닌), **`MoneyAmount`**(부호 있음), **`Percent`**(0–100], **`TradingDayRequirement`**(0–365).
- **`EquitySnapshot`**`(equity, balance)` — aggregate에 공급되는 판독값.
- **`ActivitySnapshot`**`(openPositions, openedInNewsWindow, holdingOverWeekend)` — 비 equity 사실.
- **`DailyLossLimit`**`(percent, basis)` — basis `Equity`(실시간, 현물 P&L 포함) 또는 `Balance`(실현만).
- **`DrawdownLimit`** — `Static`(시작 잔고에서), `TrailingPercent`(피크 equity에서 트레일링), 또는 `TrailingThresholdDollar`(고정 금액으로 equity 피크를 트레일링한 다음 equity가 임계값에 도달하면 **시작 잔고에서 잠금** — 선물 스타일).
- **`ConsistencyRule`**`(maxSingleDayShareOfProfit)` — 어느 하루가 총 수익을 지배하는 동안 통과를 차단합니다.
- **`ChallengeRules`** carries above plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`, `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. 규칙 수학은 VO에 있습니다(`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); aggregate가 오케스트레이션합니다.

### 챌린지 종류 및 템플릿

`ChallengeTemplates.For(kind)`이 `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding` 또는 `Custom`(전체 제어)의 유효 프리셋을 빌드합니다. UI가 템플릿을 미리 채웁니다; 사용자는 люб로调整할 수 있습니다.

### 단계 및 상태

- **단계:** `Evaluation → Verification → Funded`(싱글스텝은 Verification 건너뜀).
- **상태:** `Active`, `Passed`, `Failed`, 수명주기 `Stopped`(추적 일시 중지) — `Create` starts 챌린지 `Active`; `Stop()`/`Resume()` 토글 `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`, `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### 규칙 평가

- **`RecordEquity(EquitySnapshot, now)`** — 일 경계에서 거래일을 롤링합니다(일관성 규칙에 대한 이전 날의 수익을 캡처), 피크/일일 피크를 업데이트한 다음 첫 번째 위반에서 실패합니다(일일 손실 → 드로우다운 → 시간 제한 → 비활성, 순서대로) 또는 수익 목표, 최소 거래일, 일관성 요구사항이 모두 충족되면 단계를 진행합니다. 순서 어긋난 스냅샷 및 터미널 챌린지의 레코드는 `DomainException`을 던집니다.
- **`RecordActivity(ActivitySnapshot, now)`** — 행동 규칙(최대 공개 포지션, 주말 홀딩, 뉴스 거래)을 평가하고 비활성성 규칙을 위해 활동을 스탬프합니다.
- Soft **`PropFirmDrawdownWarning`**는 equity 사용이 구성된 임계값을 교차할 때 한 번 발생합니다.

도메인 이벤트: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`, `PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## 라이브 추적 (실행) — 노드 호스트, 자체 치유

추적은 카피트레이딩 호스팅 스택을 정확히 미러링합니다; prop 추적기 = 복사 엔진의 **읽기 전용** cousin.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — 각 노드의 `BackgroundService`, `App:PropFirm:Enabled`에 게이트 적용. 각 주기마다 활성 챌린지를 자체 치유 임대로 **클레임**합니다(`AssignedNode` + `LeaseExpiresAt`; 사망한 노드의 챌린지는 임대가 만료되면 회수됩니다 — 카피 트레이딩과 동일한 원자적 `ExecuteUpdate` 클레임, 두 노드가 이중 추적 절대 안 함), 임대를 갱신하고, 제자리에 회전된 토큰을 푸시하고, 챌린지가 `Active`를 벗어난 호스트를 중지합니다.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — 챌린지당 하나. 계정에 대해 `IOpenApiTradingSession`을 열고, `App:PropFirm:EquityPollInterval`에서 라이브 equity를 재계산하고 aggregate에 공급합니다. 토큰을 제자리에서 교체합니다(세션 드롭 없음). 챌린지가 더 이상 `Active`가 아니면 종료됩니다.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader 충실한 equity 수학. equity는 Open API에서 전달되지 않으므로 파생됩니다: `equity = balance + Σ(unrealized P&L)`, 여기서 각 포지션의 P&L은 `priceDifference × units × quote→deposit rate + swap + commission`입니다(`units = wire volume / 100`; 롱은bid에서 재평가,숏은ask에서). 잔고는 `ProtoOATrader`에서; 포지션(진입 가격, 스왑, 커미션)은 조정에서; 라이브 bid/ask는 스팟 구독에서. 순수하고 격리됨 — 통화 변환 핫스팟은 자체적으로 단위 테스트됩니다.

## 알림

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`)는 pass/breach/warning 도메인 이벤트에 가입합니다( `IDomainEventHandler<>`로 등록, 성공적인 `SaveChanges` 후 디스패치), 구조화된 알림/감사 추적을 통해 사용자에게 알립니다(`LogMessages`). 라이브 UI는 동일한 상태 변경을 반영합니다. 이것 = 크로스 컨텍스트 반응 — 결코 챌린지 aggregate를 변형하지 않습니다.

## API (`/api/prop-firm`, 기능 `PropFirm`, 역할 User+)

| 메서드 | 경로 | 목적 |
|--------|-------|---------|
| GET | `/challenges` | 사용자의 챌린지 목록(종류, 단계, 상태, 라이브 equity, 임대) |
| GET | `/challenges/{id}` | 하나의 챌린지 |
| GET | `/templates` | 생성 대화상자의 업계 프리셋 |
| POST | `/challenges` | 템플릿 또는 완전히 커스텀 규칙 세트에서 생성 |
| POST | `/challenges/{id}/start` | 추적 재개(Stopped → Active) |
| POST | `/challenges/{id}/stop` | 추적 중지(Active → Stopped, 임대 해제) |
| POST | `/challenges/{id}/equity` | equity 스냅샷 기록 → 재평가(수동/라이브 피드 없음 경로) |
| DELETE | `/challenges/{id}` | 소프트 삭제(Active인 동안 차단) |

MCP: `Mcp/Tools/PropFirmTools.cs`가 목록/템플릿에서 생성/equity 기록/시작/중지를 노출, `PropFirm` 기능에 게이트 적용.

UI: `/prop-firm`(내비게이션 *Prop Firm*, `PropFirm` 플래그로 게이트) 챌린지를 **Start/Stop/Delete** 행 작업과 함께 나열합니다(Stopped일 때 Start, Active일 때 Stop, Active인 동안 Delete 비활성화), `NewPropFirmChallengeDialog`(템플릿 피커 + 전체 규칙 편집기)를 통해 생성합니다. 모든 생성/편집은 MudBlazor 대화상을 통해.

## 라이브 equity 피드 — 해결됨

이전 "라이브 계정 P&L 피드 없음" 격차 폐쇄됨: `App:PropFirm:Enabled` 설정 시 노드가 Open API를 통해 계정을 라이브로 추적하고 equity를 자동으로 공급합니다. 없으면(기본값), 도메인 및 **수동 equity** 경로(`POST …/equity`)가 변경 없이 실행됩니다 — 빌드/테스트/E2E에 cTrader 자격 증명 불필요.

## 테스트

- **단위** — `UnitTests/PropFirm/`: `PropFirmChallengeTests`(단계 진행, 최소일, 정적/트레일링 드로우다운, 일일 손실, 터미널/순서 어긋남 가드); `PropFirmChallengeRulesTests`(잔고 대 equity 일일 손실 기준, 트레일링 임계값 달러 트레일+잠금, 일관성 차단/허용, 시간 제한, 비활성, 최대 노출, 주말, 뉴스, 중지/재개, 임대 경계, 통과 시 임대 해제, 드로우다운 경고); `PropFirmValueObjectTests`(VO 범위 + 규칙-VO 수학); `PropFirmEquityCalculatorTests`(롱/숏 P&L, 스왑/커미션, quote→deposit 변환, 누락된 가격 결정); `PropFirmTrackingHostTests`(확장된 가짜 세션에 대해 라이브 equity가 pass/fail을 구동); `PropFirmAlertNotifierTests`. 시간은 명시적 / `FakeTimeProvider` — 벽 시계 읽기 없음.
- **통합** — `IntegrationTests/`: `PropFirmChallengePersistenceTests`(라운드 트립 + equity 기록 + 소프트 삭제, 강화된 규칙 + 임대 라운드 트립) 및 `PropFirmTrackingLeaseTests`(클레임, 경합 임대, 두 노드 ID에 걸친 임대 만료 후 회수) 실제 Postgres에서.
- **E2E** — `E2ETests/PropFirmTests.cs`: 생성 + equity 기록 → `Passed`; 중지→시작→위반 흐름; 템플릿 엔드포인트.
- **스트레스 / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: 많은 혼합 규칙 챌린지에 걸쳐 시드된 무작위 equity/활동 스트림(일 Rolls, 스파이크, 크래시, 중복 + 순서 어긋난 스냅샷, 노출/주말/뉴스), 끈적한 정확히 한 번 터미널 상태, 피크-바운드-현재 불변량, 합리적인 실패를 주장합니다.

## 구성 (`App:PropFirm`)

`Enabled`(기본값 꺼짐), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`, `DrawdownWarnThresholdPercent`, `NodeName`.
