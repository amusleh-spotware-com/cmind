---
description: "머니 매니저 성과 수수료를 고수원 기준에 — 표준 복사 트레이딩 모델 (cTrader Copy, Darwinex, ZuluTrade profit-share): 제공자가 팔로어의 피크 equity 이상의 *신규* 수익에 대한 비율을 청구합니다."
---

# 복사 성과 수수료 (Phase 4)

머니 매니저 **성과 수수료를 고수원 기준에**, 표준 복사 트레이딩 모델 (cTrader Copy, Darwinex, ZuluTrade profit-share): 제공자가 팔로어의 피크 equity 이상의 *신규* 수익에 대한 비율을 청구합니다 — 시작 잔액에 대해서는 절대, 회복된 지면에 대해서는 절대 두 번 청구하지 않습니다. **`App:Copy:FeesEnabled`로 옵트인** (기본값 꺼짐).

## 모델 (고수원)

대상별 (팔로어 계정), 각 정산 시:

1. **첫 번째 정산**은 현재 equity에서 고수원(HWM)을 시드합니다 → 청구 없음 (팔로어가 예치금에 청구되지 않음).
2. **신규 고수** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, 그런 다음 `HWM ← equity`.
3. **고수원 이하 또는 해당**: 수수료 없음, HWM 변경 안 함 — 팔로어는 먼저 이전 피크를 회복해야 하므로 동일한 수익에 대해 두 번 청구되지 않습니다.

수수료 산술은 `CopyDestination.SettleFee(equity)`의 도메인 불변량입니다 — 애그리게이트가 소유합니다; 정산 서비스는 폴링된 equity만 공급하고 반환된 금액을 기록합니다. `PerformanceFee`는 50%로 캡된 값 객체이므로 잘못된 구성도 팔로어의 전체 수익을 청구할 수 없습니다.

## 어떻게 정산하는가

```
CopyFeeSettlementService (BackgroundService, FeesEnabled일 때만)
   │  매 App:Copy:FeeSettlementInterval마다
   ├─ 실행 중인 프로필 및 수수료 구성 대상 로드
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader가 세션을 열고,
   │                                               잔액 + floating P&L를 계산 (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM 논리를 애그리게이트에서 실행
   └─新高更新 + CopyFeeAccrual 추가 저장 (新高에서만)
```

- `ICopyEquityReader`는 Core 추상화입니다; 라이브 구현 (`OpenApiCopyEquityReader`)이 유일한 인프라 pieces — 따라서 정산 + HWM 논리가 가짜 리더로 테스트에서 운동됩니다, 라이브 브로커 없음.
- `CopyFeeAccrual`는 추가 전용 로그입니다 (HWM-이전, equity, fee %, fee amount, settled-at) — 수수료 보고서 및 청구를 위한 사실 로그이지 애그리게이트가 아닙니다.

## 구성 및 API

| `App:Copy` 설정 | 기본값 | 효과 |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | 정산 서비스 실행. |
| `FeeSettlementInterval` | `1h` | equity 폴링 및 수수료 정산 빈도. |

대상별: `PerformanceFeePercent` (0–50)는 대상에서 설정됩니다 (대상 추가/편집 요청).

- `GET /api/copy/profiles/{id}/fees` — 프로필의 수수료 발생 + 총 청구액.

## 테스트

- **단위** (`CopyPerformanceFeeTests`) — HWM 불변량: 첫 번째 정산이 시드 + 아무것도 청구 안 함; 新高新는 피크 이상의 수익만 청구; 고수원 이하/해당는 아무것도 청구 안 하고 피크가 절대 후퇴하지 않음; 드로다운 후 이전 피크 회복만 청구; 0%는 절대 청구 안 함; VO가 범위 외 백분율을 거부합니다.
- **통합** (`CopyFeeSettlementTests`, 실제 Postgres, 가짜 equity 리더) — 10k 시드(청구 없음, 마크 시드됨), 12k(400 청구, 마크 진행), 11k(청구 없음, 마크 유지); 발생이 올바른 소유자/금액으로 지속됨.

복사 호스트는 수수료의 영향을 받지 않습니다 (정산은 별도의 DB 작업이므로) 복사 DST 스트레스 스위트가 영향받지 않습니다 (23/23).
