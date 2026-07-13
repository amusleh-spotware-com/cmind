---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = 모든 복사 트레이딩 단위 테스트가 실행되는 인메모리 IOpenApiTradingSession. 작업: 실제 cTrader Open API 서버를 충분히 유사하게 모방하여 단위 테스트가 라이브 티어에서만 catch할 수 있는 동작을 커버합니다."
---

# FakeTradingSession — cTrader Open API 충실도 계약

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = 모든 복사 트레이딩 단위 테스트가 실행되는 인메모리 `IOpenApiTradingSession`. 작업: 실제 cTrader Open API 서버를 충분히 유사하게 모방하여 단위 테스트가 라이브 티어에서만 catch할 수 있는 동작을 커버합니다. 이 문서 = 충실도 계약: 가짜가 모델링하는 것, 얼마나 충실하게, 규칙을 정직하게 유지합니다.

> **구속 규칙 (CLAUDE.md):** 가짜는 cTrader-충실性を 유지합니다. 테스트를 통과하도록 **약화하지 마세요**. 의존하는 모든 새로운 실제 동작을 여기에서 모델화하고 충실도 테스트로 고정합니다.

## 충실도 매트릭스 (F1–F13)

계획 `plans/copy-trading-overhaul.md` §7.6을 추적합니다. 범례: ✅ 모델링됨 · ◑ 부분적 (옵트인 / 확장 중) · ⬜ 아직 모델링되지 않음.

| # | 실제 Open API 동작 | 가짜 상태 | 모델링 방법 |
|---|------------------------|-------------|-------------------|
| F1 | 시장 주문이 **부분 체결**할 수 있음 | ◑ | `PartialFillFractionForCtid[ctid] = f`는 `f×volume`만 체결; 재조정 후 G5의 Phase-1 true-up가 닫습니다. Accept→체결 이벤트 쌍은 아직 나오지 않았습니다. |
| F2 | 볼륨이 **step으로 정규화**되고 **최소** 미만이거나 **최대** 초과하면 거부됨 | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)`가 step으로 내림하고 `CtraderRejectException(VolumeTooLow/High)`를 throw합니다. |
| F3 | **잘못된 SL/TP** 거부 (사이드 + digits) | ⬜ | M6 SL/TP 정밀도 정규화와 쌍을 이루는 Phase 0a/1에서 계획. |
| F4 | 가격이 **digits로 정수 스케일**; `pipPosition` | ◑ | `SymbolDetails`가 이제 `Digits`(및 `MaxVolume`)를 전달하고, `PipPosition`이 시장 범위 허용치를驱动하고, `Digits`가 SL/TP 정밀도 정규화를驱动합니다 (M6). 전체 정수 가격 스케일링은 아직 보류. |
| F5 | **시장 범위** 체결은 현물 가격이 `base ± slippage` 내에 있을 때만 가능, 그렇지 않으면 거부 | ✅ | `IsMarketRangeRejected`가 라이브 현물 (`SetSpot`)을 `baseSlippagePrice ± slippageInPoints`와 비교합니다. 레거시 `RejectMarketRangeForCtid` 플래그가 여전히 강제 거부합니다. |
| F6 | **대기トリガー→체결** dual 이벤트 (주문에 `positionId` + OPEN Position이 있음) | ◑ | `PushOpen(..., orderId:)`가 체결된 대기 이벤트를 재현합니다; FX‑Blue/cMAM 이중 복사 중복 제거는 `CopyEngineHostTests.Filled_pending_does_not_double_open`에서 커버됩니다. |
| F7 | **서버 주도 닫기** (SL/TP 히트, 스톱아웃) | ⬜ | 현재는 테스트 푸시 (`PushClose`); 가격驱动 SL/TP 히트 + 스톱아웃 닫기는 계획되었습니다. |
| F8 | **계정별** 심볼 테이블 / 세부정보 | ◑ | 심볼 이름/ID는 가짜별; 계정별 상이한 테이블 (브로커 간)은 보류. |
| F9 | 완전한 **계정 상태** (잔액, equity, 마진, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (진입/스왑/커미션의 항목/스왑/커미션 via `SetPositionValuation`) + `SetSpot`가 비례 equity sizing (G2, 단위 테스트에서 테스트됨)으로 실제 equity를 공급합니다. 사용 마진은 재조정 API에 의해 노출되지 않으므로 free-margin이 equity로 보고됩니다. |
| F10 | 이벤트가 **서버 타임스탬프**를 전달함 | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — 실제 세션은 deal의 `ExecutionTimestamp`에서 읽습니다; `PushOpen`/`PushPending`는 `serverTimestamp:`를 받아들여서 `FakeTimeProvider`-주도 테스트가 실제 복사 지연을驱动합니다 (G1). |
| F11 | **거래 모드 / 일정** (비활성화 / 청산 전용 / 닫힘) | ⬜ | Phase 2b에서 계획. |
| F12 | **유형화된 오류 분류** (`ProtoOAErrorRes` 코드) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X`가 one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …)을 throw합니다. |
| F13 | **토큰 무효화** — 오래된 토큰 → 인증 오류 | ✅ | `InvalidateToken(ctid)`가 첨부된 토큰을 오래된 것으로 표시합니다; 거래 호출은 실제 `OpenApiException`을 `OpenApiErrorKind.TokenInvalid` (코드 `CH_ACCESS_TOKEN_INVALID`)로 throw하여 라이브 서버처럼 작동하며 `SwapAccessTokenAsync`가 새 토큰을 설치할 때까지 계속됩니다. M1 토큰-鲁棒性 테스트에 공급합니다. |

충실도 테스트는 `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`에 있습니다.

## 옵트인, 기본값은 레거시 동작을 보존합니다

모든 충실도 노브는 **기본값 꺼짐**이므로 가짜는 항상 간단한 항상-체결 동작을 유지하여 신경 쓰지 않는 테스트를 위해. 테스트는 계정별로 옵트인합니다:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## 특성화 + 적합성 (계획됨, 가짜 ≡ 실제 유지)

실제 서버 (이동 중)와 가짜를 정직하게 유지하는 두 가지 메커니즘 (Phase 0a에서 추적, 착륙):

1. **라이브 특성화** (`LiveApiCharacterization`, 데모 계정, 시크릿 게이트, 시장 닫힘 시 `Inconclusive`): 실제 Open API를驱动하여 정확한 와이어 사실 (이벤트 시퀀스, 스케일링, 거부 코드)을 golden fixture에 기록하여 테스트 프로젝트에 체크인됩니다. 시크릿은 fixture에 없습니다 — 관찰된 형태만.
2. **적합성 하네스**: *동일한* 시나리오 제품군을 두 번 실행 — 한 번은 `FakeTradingSession`에 대해, 한 번은 라이브 세션에 대해 (시크릿이 있을 때) — 동일한 관찰 가능한 결과를 주장합니다. 실제 서버 변경 → 라이브 레그 실패 → 가짜 업데이트. 이것이 "단위 테스트가 모든 것을 커버"를 신뢰할 수 있게 만듭니다.

라이브 자격 증명: `secrets/dev-credentials.local.json` (또는 레거시 분할 파일) — `docs/testing/dev-credentials.md` 참조.
