---
description: "전체 재현 가능한 복사 트레이딩 테스트 제품군. 두 레이어: 결정론적 테스트 (xUnit, 네트워크 없음) 및 라이브 E2E 테스트 (실제 cTrader 데모 계정)."
---

# 복사 트레이딩 테스트 제품군 (결정론적 + 라이브)

전체 재현 가능한 복사 트레이딩 테스트 제품군. 두 레이어:

1. **결정론적 테스트** (xUnit, 네트워크 없음) — 복사 수학 + 엔진 논리. 빠름, CI, 시크릿 없음. 모든 머니 매니저먼트 모드, 모든 필터/옵션, 엔진 레지리에이션 커버.
2. **라이브 E2E 테스트** (실제 cTrader 데모 계정) — 실제 계정 간 실제 주문을 배치 및 복사하는 실제 `CopyEngineHost`. 단위 테스트처럼 완전 자동화되고 다시 실행 가능: 로컬 gitignored 파일에서 캐시된 자격 증명을 읽고, 액세스 토큰을 자체 갱신하고, 시크릿이 없으면 깔끔하게 건너뛰기 (CI가 초록 유지).

라이브 펀딩 계정에서 절대 실행되지 않습니다 — 모든 계정 **데모**, 모든 라이브 테스트는 열었던 위치를 닫습니다.

## 레이아웃

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — 모든 사이징 모드 + 반올림 + 최소/최대ロット
  CopyDecisionEngineTests.cs     — 방향/리버스/슬리피지/지연/심볼 필터/사이즈제로
  CopyEngineHostTests.cs         — 인메모리 가짜 세션 대해 호스트 복사 논리를 테스트
  FakeTradingSession.cs          — 결정론적 IOpenApiTradingSession (주문/닫기/수정 기록)
  OpenApiConnectionTests.cs      — 연결 / 재연결 / 백오프 / 치명적 결함 (레지리에이션)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — gitignored 시크릿을 로드하고, 갱신된 토큰을 저장
  LiveTokenBootstrapTests.cs     — 원샷: 앱 DB에서 토큰을 토큰 캐시로 해독
  LiveCopyFixture.cs             — 액세스 토큰을 회전하고 데모 계정 목록을 노출
  LiveCopyScenario.cs            — 하나의 실제 복사 시나리오를 종단에서 실행 (열기 → 복사 → 검증 → 정리)
  CopyTradingLiveTests.cs        — 라이브 시나리오 (1:1, 1:many, reverse, …)
```

## 시크릿 (로컬, gitignored — 절대 커밋 안 함)

모든 자격 증명은 `<repo>/secrets/` 아래에 있습니다 (`.gitignore`에 이미 있음). 개발자는 **처음 두 파일만 작성합니다**; 세 번째 (토큰)는 온보딩에 의해 자동으로 생성됩니다.

`secrets/openapi-test-app.local.json` — Open API 앱:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — 승인할 cID 로그인 자격 증명 (하나 또는 여러):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **온보딩이 작성**, 다중 cID, 매 실행 시 갱신:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

갱신 토큰은 **만료되지 않으므로** 원샷 온보딩 후 라이브 테스트가 무기한 작동합니다: 각 실행은 각 cID의 갱신 토큰을 새 액세스 토큰으로 교환합니다 (회전) — 브라우저 없음, 프롬프트 없음.

## 원샷 온보딩 (완전 자동 — 저장된 자격 증명 저장 외에 개발자 상호작용 없음)

온보딩은 저장된 cID 자격 증명으로 헤드리스 브라우저에서 실제 cTrader ID 로그인을驱动하고 로컬 HTTPS 리스너 (앱의 등록된 리다이렉트 `https://localhost:7080/openapi/callback`)에서 OAuth 콜백을 캡처하고, 코드를 토큰으로 교환하고, 계정 목록을 로드하고, 다중 cID 토큰 캐시를 씁니다. 머신당 한 번 실행합니다 (또는 cID 추가 시):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

`openapi-cids.local.json`의 모든 cID를 승인하고, `openapi-tokens.local.json`을 씁니다. 그 후 라이브 복사 테스트는 다른 것이 필요하지 않습니다. (cID의 cTrader ID 계정은 자동화를 완료하기 위해 로그인에 2FA/captcha가 없어야 합니다.)

**대체 부트스트랩** (계정이 이미 실행 중인 앱에서 승인된 경우): 저장된 토큰을 앱의 Postgres 볼륨에서 직접 해독하는 대신 재授权:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## 안전 — 데모만

라이브 테스트는 **데모 계정에서만 거래**: 픽스처가 토큰 캐시를 `IsLive == false`인 계정으로 필터링하고 데모 게이트웨이에서 연결하므로 라이브/펀디드 계정에 주문이 착륙할 수 없습니다. 테스트가 여는 모든 위치는 정리에서 닫습니다.

## 실행

```bash
# 결정론적 복사 테스트만 (빠름, 시크릿 없음, CI-안전)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# 실제 데모 계정에 대한 라이브 복사 테스트 (두 시크릿 파일 필요)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# 모든 것
dotnet test
```

시크릿 파일 없이 라이브 테스트는 건너뛰기 이유를 출력하고 no-op로 통과하므로 제품군이 어디서든 실행하기 안전합니다.

## 커버리지

### 머니 매니지먼트 / 사이징 (결정론적 — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (계약 크기 / 통화) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
잔액/레버리지/용량 불일치에 대한 스케일 **업** 및 **다운** (the "golden rule") ·ロット-step
반올림 · 최소ロット 건너뛰기 vs 강제-최소 · 최대ロット 캡 · 더 엄격한-의-바운드-대-사양 최소 & 최대 · 제로
마스터 잔액 건너뛰기.

### 의사 결정 필터 (결정론적 — `CopyDecisionEngineTests`)
심볼 허용 목록 / 블랙리스트 / 허용 · LongOnly / ShortOnly · 리버스가 효과적인 사이드를 뒤집습니다 ·
슬리피지 over限制 건너뛰기 + 정확히限制 허용 · 정체된-시그널 (최대 지연) 건너뛰기 · 사이즈제로 건너뛰기 ·
재연결 재조정 (열린-누락 중복 제거, 닫힘-고아).

### 복사 엔진 호스트 (결정론적 — `CopyEngineHostTests`, 인메모리 세션)
열린 것이 시장 주문을 미러합니다 (사이드 / 볼륨 / 라벨) · **리버스**가 사이드를 뒤집고 **SL/TP를 스왑합니다** ·
**심볼 매핑**이 대상 심볼을 확인합니다 · **한 슬레이브의 주문 실패가 다른 슬레이브에 계속 복사됩니다** ·
소스가 닫으면 미러된 복사본을 닫습니다 · 재연결 재동기화가 고아 복사본을 닫습니다.

### 연결 레지리에이션 (결정론적 — `OpenApiConnectionTests`)
앱 인증 후 연결됨 · 삭제된 연결이 재연결 및 재인증합니다 · 치명적 인증 오류 결함 ·
지수 백오프.

### 라이브, 실제 cTrader 데모 계정 (`CopyTradingLiveTests`)
토큰 갱신 + 계정 목록 · **1:1** 복사 실행 · **1:many** 복사가 모든 슬레이브에 미러합니다 ·
**리버스**가 마스터 매수를 슬레이브 매도로 전환합니다 · **cross-cID** 복사 (마스터가 하나의 cID, 슬레이브가 다른 cID 아래, 각각 자신의 토큰으로 인증). 각각 마스터에서 실제 최소ロット 포지션을 열고, 엔진이 미러할 때까지 기다립니다 (소스-포지션-ID 라벨로 슬레이브에서 일치), 주장, 모든 것을 닫습니다. 시장 닫힘은 **Inconclusive**로 보고되며 실패가 아닙니다.

## 로깅 및 감사 가능성

모든 복사 트레이딩 작업은 소스 생성 구조화된 이벤트 (`Core/Logging/LogMessages.cs`, 이벤트 ID 1043–1055)를 통해 로깅됩니다, 완전 감사 가능 트레일:

| 이벤트 | ID | 의미 |
|-------|----|---------|
| CopyHostStarted | 1046 | 프로필의 엔진이 시작되었습니다 (소스 + 대상 수) |
| CopySourceOpen | 1047 | 마스터가 포지션을 열었습니다 (심볼 / 사이드 /로트) |
| CopyOrderPlaced | 1048 | 복사 주문이 슬레이브로 전송되었습니다 (심볼 / 사이드 / 볼륨 / 소스 ID) |
| CopySkipped | 1049 | 복사가 건너뛰어졌고 이유입니다 (슬리피지 / 방향 / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP가 슬레이브 복사본에 적용되었습니다 |
| CopyOpenFailed | 1051 | 슬레이브 복사-개설이 실패했습니다 (격리 — 다른 슬레이브 계속) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | 마스터 닫힘 → 슬레이브 복사본 닫힘 |
| CopyCloseFailed | 1054 | 슬레이브 복사-닫힘이 실패했습니다 |
| CopyResync | 1055 | 재연결 재조정 (소스 열린 수, 닫힌 고아) |
| CopyPartialClose | 1056 | 마스터 부분 닫힘이 미러링됨 — 슬레이브에서 비례 조각이 닫힘 |
| CopyScaleIn | 1057 | 마스터 스케일-인이 미러링됨 (옵트인) — 추가 볼륨이 슬레이브에 복사됨 |
| CopyPendingOrderPlaced | 1058 | 대기 limit/stop이 슬레이브에 미러링됨 (옵트인) |
| CopyPendingOrderCancelled | 1059 | 소스 대기 취소 → 슬레이브 대기가 취소됨 |
| CopyTrailingApplied | 1060 | 트레일링 스탑이 슬레이브 복사본에 적용됨 (옵트인) |
| CopyStopLossAmended | 1061 | 소스 SL 이동이 슬레이브 복사본을 다시 수정함 |
| CopyHostTokenRotated | 1062 | 액세스 토큰이 회전된 후 슈퍼바이저가 실행 중인 호스트를 다시 시작함 |

로그는 Serilog 압축 JSON으로 방출됩니다 (구조화된 소품: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), `OTEL_EXPORTER_OTLP_ENDPOINT` 설정 시 OTLP로 ship됩니다. **표준 구성을 통해 카테고리별로 완전 구성 가능** — 예: 복사 엔진 verbosity를 코드 변경 없이 낮추거나 높이려면:

```jsonc
// appsettings.json — Serilog 수준 재정의
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // CopyEngineHost 감사 트레일
  "Nodes.CopyTrading": "Information"        // 슈퍼바이저 / 토큰 갱신
} } }
```

`Audit_log_records_every_trading_operation` 호스트 테스트가 열기, 주문, 보호, 닫기에 대해 트레일이 fire됨을 주장합니다.

## 엣지 케이스 (실제 복사/MAM 플랫폼이 실패하는 방식에 대해 검증됨)

슬리피지 & 지연, 심볼 접미사/불일치, 재연결 시 중복 거래, 레버리지 불일치 & 마진-안전 사이징, 예치-통화/계약-크기 차이, 최소/최대ロット & 반올림, 거부된 주문, 방향 필터, 연결 끊김 후 고아 정리 — 모두 위에서 커버. 출처:
[레버리지 불일치](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[브로커 간 복사](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[복사 함정](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[슬리피지 & 지연](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[복사 트레이딩이 실패하는 이유](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[리스크 매개변수](https://www.mt4copier.com/risk-parameters/).

## 고급 미러링 커버리지 (부분 닫기 · 대기 주문 · SL-트레일링)

호스트는 시장 열기/닫기 외에 더 많은 것을 미러링합니다. 각 동작 = `CopyDestination`의 대상별 옵트인 플래그 (`MirrorPartialClose` 기본값 온, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` 기본값 오프), 의도 메서드로 가드됨, jsonb-지속됨.

| 동작 | 결정론적 테스트 (`CopyEngineHostTests`) | 라이브 테스트 |
|-----------|--------------------------------------------|-----------|
| 부분 닫기 → 비례 조각 | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4가 60%를 닫음) + 비활성화된 경로 | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| 스케일 인 | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| 대기 limit/stop 배치됨 | `Pending_order_is_placed_on_the_slave_when_enabled` (이론: Limit+Stop) + 비활성화된 경로 | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| 대기 취소 | `Source_pending_cancel_cancels_the_slave_pending` | (마스터에서 취소, 슬레이브가 취소됨을 주장하는 동일한 라이브 테스트) ✅ |
| 체결된 대기 이중-열기 안 함 | `Filled_pending_does_not_double_open` (주문 ID → 포지션 ID 중복 제거) | — |
| 트레일링 스탑 | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| 소스 SL 이동이 다시 수정함 | `Source_stop_loss_move_re-amends_the_copy` | — |
| 감사 이벤트 fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

위의 모든 라이브 테스트는 실제 cTrader 데모 계정에 대해 검증됨 녹색 (1:1, 1:many, reverse, cross-cID, 부분 닫기, 대기+취소, 트레일링).

`OpenApiTradingSession`의 와이어 추가: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, `AmendPositionSltpAsync`의 트레일링 플래그, 주문/대기 필드의 `ExecutionEvent`, `LoadSpotPriceAsync` (현물 구독 → bid/ask, 라이브 대기/트레일링 테스트에서 시장 외에 휴식 명령 배치 사용), `OpenPositionSnapshot`의 `StopLoss`/`TrailingStopLoss` (재조정 통해 복사의 트레일링 상태 관찰 가능). 대상 복사본은 **소스 포지션 ID**(대기 복사본은 소스 **주문 ID**)로 계속 라벨 지정되므로 재연결 재동기화가 ID 기반, 절대로 거래를 중복시키지 않습니다.

**cTrader 이벤트 함정 (라이브 검증됨):** 휴식 중인 대기 주문의 `ORDER_ACCEPTED`/`ORDER_CANCELLED` 실행 이벤트는 **열지 않은 `Position` placeholder** plus `Order`를 전달합니다. 스트림은 포지션 분기 *이전에* 이를 *주문* 이벤트로 분류해야 합니다 (포지션이 `OPEN`이 아닌 경우) 그렇지 않으면 대기 배치 미스가 포지션 닫기로 잘못 읽혀집니다. `SourceExecutionsAsync`가 이것을 수행합니다; 이것을 놓치면 모든 대기 미러링이 조용히 삭제됩니다.

## 토큰 회전 + 노드 어피니티

- **실행 중인 호스트에 회전.** `CopyEngineSupervisor`는 각 실행 호스트의 토큰 시그니처를 기록하고 매 재조정에서 DB에서 플랜을 다시 빌드합니다 (freshly rotated by `OpenApiTokenRefreshService`). 변경된 시그니처는 호스트를 다시 시작합니다 (`CopyHostTokenRotated`, 1062); 새 호스트의 `ResyncAsync`는 거래를 중복 없이 상태를 다시 빌드합니다. 중간 실행 중 강제 회전은 `IOpenApiTokenClient.RefreshAsync`를 통해 확인하여 라이브 호스트가 계속 복사함을 확인합니다.
- **노드 어피니티 (이중 복사 없음).** Web 로컬 노드와 `CopyAgent` 워커 모두 슈퍼바이저를 실행합니다. 각 실행 프로필은 하나의 노드에 의해排他的으로 클레임됩니다 (`CopyProfile.AssignedNode`, 원자적 `ExecuteUpdate` 클레임이 `CopyOptions.NodeName`(기본값 머신 이름)로 키가 지정됨). 슈퍼바이저는 자신이 소유한 프로필만 호스팅합니다; 중지/일시정지는 클레임을 해제합니다. 커버리지:
  - 도메인 (단위): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **통합 (실제 Postgres, Testcontainers)**: `CopyNodeAffinityTests`가 슈퍼바이저의 실제 `ClaimUnassignedProfilesAsync`를驱动 — 첫 번째 노드가 3개의 실행 프로필을 모두 클레임하고 두 번째는 **0** (이중 호스트 없음), 일시정지→재시작이 다른 노드에 대한 클레임을 해제합니다.
  - 회전 감지 (`TokenRotationSignatureTests`): 슈퍼바이저의 `TokenSignature`가 소스 또는 대상 토큰이 회전할 때 변경되고 그렇지 않으면 안정적입니다 (실제 회전에서만 호스트 다시 시작).

### 단일 사용 갱신 토큰 (중요)

cTrader **갱신 토큰은 단일 사용** — 각 갱신은 *새* 갱신 토큰을 반환하고 이전 것을 무효화합니다. 라이브 픽스처는 시작 시 갱신하고 회전된 토큰을 `secrets/openapi-tokens.local.json`에 지속합니다. 결과:
- 실행이 갱신하지만 새 토큰을 **지속할 수 없는** 경우 (예: 읽기 전용 마운트) 캐시된 토큰이死亡하고 다음 실행이 `ACCESS_DENIED`로 실패합니다. 헤드리스 온보딩로 재생성:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens`는 쓰기 실패를 삼키므로 읽기 전용 캐시가 실행을 충돌시키지 않지만 **라이브** 인 클러스터 제품군은 **쓰기 가능** 캐시가 필요합니다 (K8s Job이 Secret을 emptyDir에 복사 — 배포 문서 참조).

## Kubernetes 클러스터에서 제품군 실행

전체 제품군이 Helm 배포 앱에 대해 인 클러스터에서 실행되어 회귀가 인 클러스터에서 같은 것처럼 로컬에서 catch됩니다. [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite) 참조.

```bash
scripts/k8s-e2e.sh                                   # kind 클러스터, 결정론적 제품군 (시크릿 없음)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # 라이브
```

`Dockerfile.tests`가 러너 이미지를 빌드합니다; Helm `tests-job.yaml` (게이트 `tests.enabled=false`)이 인 클러스터 Postgres + Web에 대해 실행합니다. **기본값 = 결정론적 복사 제품군** (시크릿 없음, 토큰 회전 없음). 라이브 제품군의 경우 `tests.copySecret`을 gitignored `openapi-*.local.json`을 보유한 Secret으로 설정합니다; init-container가 `/app/secrets`의 **쓰기 가능** emptyDir에 복사합니다 (필요 — 단일 사용 갱신 토큰이 지속 가능해야 함). 복사 테스트는 Web + Postgres + 토큰 캐시만 필요합니다 — 권한 있는 노드 에이전트 불필요. 스크립트가 Job이 0으로 종료하고 로그에 `Passed!`가 포함되어야 함을 주장합니다.

**여기서 검증됨 (Docker, 클러스터 없음):** 테스트 이미지가 결정론적 제품군 (`101 passed`) 및 쓰기 가능 `secrets/` 마운트로 전체 **라이브** 제품군 (`8 passed`)을 실행합니다 — Kubernetes 제외. 작성 환경에서 `kind`/`kubectl`/`helm`을 사용할 수 없으므로 전체 `k8s-e2e.sh` 클러스터 실행이 여기가 아닙니다.

## 라이브 옵션 매트릭스 + 카오스 (LiveCopyMatrix / LiveCopyChaos)

`LiveCopyScenario` / `LiveCopyFixture`를 기반으로 하는 두 개의 데이터驱动 라이브 제품군, 결정론적 DST 스트레스 제품군의 라이브 대응:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` 옵션 매트릭스: 각 행마다 데모 계정에 대해 다르게 구성된 대상, 황금 결과 주장. 행: `one_to_one`, `half_multiplier`, `reverse` (반대 사이드), `manage_only` (아무것도 열지 않음), `trading_hours_closed` (창이 지금 제외 → 복사 없음), `source_label_block` (라벨 필터 → 복사 없음), `lot_sanity_block` (상한 → 복사 없음).
- **`LiveCopyChaos`** — 적대적 시작에 대한 복사 엔진: 호스트가 시작되기 전에 마스터가 이미 포지션을 보유하고 있어 수렴이 시작 시 `Sync-Open-on-Start`에서만 가능 plus 토글 끄기의 부정적 경우. 결정론적 소켓 플랩 / 토큰 회전 / 거부 카오스는 DST 제품군에서 종합적으로 커버됩니다; 이것은 라이브 재동기화 경로를 주장합니다.

둘 다 **시크릿 없이 깔끔하게 건너뛰기**, 시장 닫힘에서 **Inconclusive** 보고, 각 행이 여는 위치를 정리합니다 (데모 계정만). 제품군 **skip cleanly**이고 시크릿 없을 때, 시장 닫힘에서 **Inconclusive** 보고, 모든 행이 여는 위치를 정리합니다 (데모 계정만).

**픽스처 robustness:** `LiveCopyFixture`는 이제 캐시된 토큰이 갱신될 수 없을 때 깔끔한 건너뛰기로 Degradation됩니다 (cTrader 갱신 토큰은 단일 사용 — 오래된 캐시 또는 오프라인 실행이 더 이상 전체 라이브 컬렉션을 결함이 발생시키지 않음; 토큰 캐시를 새로 고치려면 OAuth 온보딩을 다시 실행하세요).

## 라이브 + 클러스터 제품군을 직접 실행 (핑거 없음 — 모두 셀프서비스 가능)

아래의 모든 것이 이 환경에서 사용 가능합니다. "자격 증명 불가", "토큰 오래됨" 또는 "kind/helm 설치 안 됨"을 주장하지 마세요 — 설정하고 실행합니다.

### 1. 새로운 Open API 토큰 받기 (헤드리스 OAuth, 브라우저 상호작용 없음)

cTrader 갱신 토큰은 단일 사용이므로 캐시된 토큰은 **무조건 오래됩니다**. 저장된 cID 사용자 이름/비밀번호 (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json` 또는 통합 `secrets/dev-credentials.local.json`)에서 스스로 재발급합니다. 온보딩 테스트가 **헤드리스 Edge** OAuth를 Playwright로驱动하여 `secrets/openapi-tokens.local.json`을 씁니다:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13초; 모든 cID를 승인하고, 캐시된 토큰을 새로 고칩니다. 라이브 제품군이 토큰 갱신 실패로 픽스처를 사용할 수 없다고 보고하면 다시 실행하세요.

### 2. 라이브 복사 제품군 실행 (실제 cTrader 데모 계정)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # 핵심 미러링 (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # 옵션 매트릭스 (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # 재동기화 카오스 (2)
```

실제 DEMO 주문을 배치 및 정리합니다 (절대로 라이브 계정 아님), 시장 닫림에서 **Inconclusive**를 보고합니다. 종단간 검증됨 녹색.

### 3. 실행 중인 앱 볼륨에서 토큰 부트스트랩 (대안)

앱 실행 + cID가 앱 내에서 연결된 경우 앱의 최신 갱신 토큰을 `app-pg-data` Postgres 볼륨에서 직접 추출하여 재授权하는 대신 — `LiveTokenBootstrapTests`, `CMIND_VOLUME_CONN` 설정하여.

### 4. Kubernetes 클러스터 E2E

`kind`, `helm`, Docker 사용 가능 (PATH에 없으면 `go install`/릴리스 바이너리 또는 `choco install kind kubernetes-helm`로 설치). 원샷 스크립트가 이미지를 빌드+로드하고, 차트를 배포하고, 인 클러스터 테스트 Job을 실행하고, 종료 0을 주장합니다:

```bash
scripts/k8s-e2e.sh                                 # 결정론적 복사 제품군 (시크릿 없음)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # 라이브 인 클러스터
```

[../deployment/kubernetes.md](../deployment/kubernetes.md) 참조.
