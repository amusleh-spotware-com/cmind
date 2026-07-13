---
title: 실패 경로 커버리지 맵
description: 의무가 요구하는 모든 실패 시나리오를 실제로 운동하는 테스트에 매핑합니다 — 그래서 격차가 보이지 않고 가정되지 않습니다.
---

# 실패 경로 커버리지 맵

테스트 의무는 명확합니다: **실패 경로가 카운트됩니다** — 연결 끊김, 주문 거부, 동기화 해제/재동기화, 토큰 회전 또는 죽은 노드를 중단시킬 수 있는 변경은 동일한 커밋에서 이에 대한 테스트와 함께 제공되어야 합니다. 이 페이지는 각 필수 시나리오를 그것을 운동하는 테스트에 매핑하여 실제 격차가 *보이지 않고 가정되는* 대신 *보이게* 합니다. 실패 경로를 추가할 때 여기에 행을 추가하세요.

## 필수 시나리오 → 테스트

| 시나리오 | 티어 | 테스트 |
|---|---|---|
| **연결 끊김 → 재연결** | 단위 · 스트레스 · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` 및 `SyncTradingSession` (DST); `MiscUiTests` 재연결 모달 상태 |
| **주문 거부** | 단위 · 스트레스 | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **동기화 해제 / 재동기화** | 단위 · 스트레스 | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **토큰 회전 / 무효화** | 단위 · 통합 · 스트레스 | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (에스컬레이션 창); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (통합); DST `RotateTokens` |
| **노드 죽음 → 임대 회수** | 단위 · 통합 · 스트레스 | `NodeInstanceReclaimerTests` (단위 + 통합); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (통합); `CopyLeaseReclaimStressTests` |
| **AI 공급자 오류 (4xx/5xx/타임아웃/잘못된 형식)** | 단위 · 통합 | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (통합) |
| **AI 완전 비활성화 (키 없음)** | 단위 · 통합 · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **데이터베이스 일시적 실패 / 마이그레이션 잠금** | 통합 | `DatabaseResilienceTests`; `MigrationLockTests` |
| **노드 HTTP 에이전트 실패 / 재시도** | 통합 | `NodeAgentHttpResilienceTests` |
| **컨테이너 자체 종료 재조정** | 단위 | `BacktestCompletionPollerTests`; `RunCompletionPoller` 커버리지 in `ContainerCommandHelpersTests` |
| **프로펌 breach** | 단위 · 통합 | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **잘못된 입력 / 인증 거부 (UI + 브랜딩)** | 단위 · 통합 · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## 민スポット — 가정하기 전에 확인

이는 명시적 확인이worthwhile합니다 (확인 후 또는 채워진 후 위에 행을 추가하세요):

- **MCP 도구 인증 거부** — `McpKeyAuthHandler`가 나쁘거나 누락된 키를 거부합니다. 누락되거나 잘못된 키로 MCP 도구 엔드포인트를 호출하고 401을 주장하는 통합 테스트가 없습니다; 추가하세요.
- **cBot 빌드 실패 표면화** — 컴파일 오류는 인스턴스/UI에서 `Failed`와 빌드 출력으로 착륙해야 합니다. `CBotLifecycleTests`는 해피 패스를 커버합니다; 실패 분기가 주장됨을 확인하세요.
- **라이브 주문 실행** — 실제 cTrader 자격 증명 + 노드 클러스터가 필요한 실제 cTrader 자격 증명 간 종단간 복사 실행은 여전히 게이트됩니다; [라이브 복사 트레이딩](./live-copy-trading.md) 참조.

## 이것이 어떻게 시행되는가

결정론적 스트레스 제품군 (DST, `tests/StressTests`)은 압축된 시계에서 이러한 실패를 재생해야 하며 초록을 유지해야 합니다 — DST 시나리오를 통과하도록 약화하지 마세요; 코드를 수정하세요. [FakeTradingSession](./fake-trading-session.md)은 이 단위 테스트가 driving하는 cTrader-충실한 시뮬레이터입니다; 새로운 브로커 동작에 대해 확장하고 어설션을 완화하지 마세요.
