---
description: "남아 있는 복사 트레이딩 작업의 전체 검증 — 아래 모두 실제로 실행, 저자만 된 것이 아닙니다."
---

# 복사 트레이딩 검증 실행 (2026-07-10)

남아 있는 복사 트레이딩 작업의 전체 검증 — 아래 모두 **실제로 실행**, 저자만 된 것이 아닙니다.

## 라이브 (실제 cTrader 데모 계정) — 8/8 통과
1:1 · 1:many · reverse · cross-cID · partial-close · **대기 limit + 취소** · **trailing stop** · token-refresh.
라이브 시나리오 `RunPendingAsync` / `RunTrailingAsync` 추가됨 (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## 통합 (실제 Postgres, Testcontainers) — 통과
- `CopyNodeAffinityTests` — 슈퍼바이저 실제 원자적 클레임: 첫 번째 노드가 모든 실행 프로필을 클레임, 두 번째는 **0** 클레임 (이중 복사 없음); 일시정지가 해제 + 회수.
- `TokenRotationSignatureTests` — 실제 토큰 회전에서만 시그니처 변경.

## 인 클러스터 (kind + Helm) — 통과
`kind`/`kubectl`/`helm` 설치, 실제 kind 클러스터에 대해 `scripts/k8s-e2e.sh` 실행:
- **결정론적 Job: 101 통과** 인 클러스터.
- **라이브 Job: 8 통과** 인 클러스터 (init-container `seed-secrets`가 Secret → 쓸 수 있는 emptyDir로 복사, 실제 데모 계정).
- Job `Complete 1/1`, 스크립트 종료 0.

## 검증 중 발견된 버그 (수정 + 재검증됨)
- **대기 이벤트**: cTrader는 휴식 중인 limit/stop `ORDER_ACCEPTED`/`CANCELLED`에 *열지 않은 Position placeholder*를 첨부합니다. `SourceExecutionsAsync`는 이제 포지션 분기 전에 order 이벤트として분류하지만 limit/stop *체결* (예: 스탑 로스 트리거关闭)는 닫기 경로로 통과시킵니다.
- **단일 사용 갱신 토큰**: cTrader는 모든 갱신에서 갱신 토큰을 회전합니다. 쓸 수 없는 읽기 전용 캐시가 지속할 수 없으면 자체 무효화됩니다. 라이브 K8s Job은因此 Secret을 **쓸 수 있는** emptyDir로 복사합니다; Job은 결정론적 제품군을 기본으로 합니다. `SaveTokens`는現在 best-effort입니다. 라이브 심볼은 FX로 강제됩니다 (BTCUSD trailing 수정에서 브로커 거부).
- 스크립트 이미지 이름이 Helm `registry/repository` 분할 + `pullPolicy=Never`와 일치하도록 수정됨.

## 고급 미러링 + 토큰 수명주기 + 확장 프로그램 (2026-07-10) — 결정론적 티어 통과

후속 프로그램은 주문 유형 필터링, 대기 주문 만료 복사, 시장 범위 / 스탑 리밋 슬리피지 미러링, SL/TP 복사 토글, 우아한 제자리 토큰 스왑 (cID당 단일 유효 토큰), cTrader 충실한 시뮬레이터, 자체 복구 노드 임대, 통합된 개발-자격 증명 파일을 추가합니다.

- **단위 — 210 통과** (`dotnet test tests/UnitTests`). 새로운 복사 커버리지: 주문 유형 필터 (열린 + 대기), 시장 범위 슬리피지 미러 + 기본 가격, 만료 복사 온/오프, 스탑 리밋 슬리피지, 대기 수정, 마스터 열린 것으로 시작, 연결 끊김→마스터 거래→재연결 재동기화 (열린 것 누락 + 닫힘 고아), 제자리 토큰 스왑 (재시작 없음), cross-cID 무효화, 도메인 불변량, 임대 소유권, 토큰 버전 범프.
- **통합 (실제 Postgres, Testcontainers) — 통과**: `CopyNodeAffinityTests` (원자적 클레임, 이중 복사 없음, 일시정지 해제, **만료 임대 회수 by another node**), `TokenRotationSignatureTests` (토큰 버전 범프 시 시그니처 변경), `OpenApiAuthorizationPersistenceTests` (TokenVersion 지속 + 갱신 시 증가).
- **E2E** (`tests/E2ETests`): 대상 옵션 라운드트립이 이제 주문 유형 필터, 복사 만료, 복사 슬리피지를 완전히 주장합니다.
- **빌드**: `TreatWarningsAsErrors`에서清洁; 변경된 파일에서 Rider `get_file_problems`清洁.

라이브 시나리오 (실제 cTrader 데모 계정)는 보류 중지, 시장 범위, 만료, 마스터 열린 것으로 시작, 중간 실행 토큰 회전에 대해 작성되었습니다; 통합된 `secrets/dev-credentials.local.json`로 실행됩니다.

## 알려진 후속
클러스터 내 라이브 실행이 단일 사용 토큰을 회전했습니다; `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`로 로컬 캐시를 재생성하세요
(cTrader가 실행 직후 OAuth 페이지를 스로틀했습니다 — 클리어 시 재시도).
