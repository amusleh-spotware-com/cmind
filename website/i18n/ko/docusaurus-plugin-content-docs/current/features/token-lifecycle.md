---
description: "cTrader의 Open API는 cTrader ID(cID)당 하나의 유효한 액세스 토큰을 허용합니다. 새 토큰이 발급되는 순간 — 예약된 새로고침 또는 사용자가同一 cID에서 다른 계정을 연결할 때 재승인 — 이전 액세스 토큰이 무효화됩니다."
---

# Open API 토큰 수명주기

cTrader의 Open API는 cTrader ID(cID)당 **하나의 유효한 액세스 토큰**을 허용합니다. 새 토큰이 발급되는 순간 — 예약된 새로고침 또는 사용자가同一 cID에서 다른 계정을 연결할 때 재승인 — 이전 액세스 토큰이 무효화됩니다. 원격 노드에서 실행 중인 복사 엔진이 해당 이제 사망한 토큰을持有하고 있으므로, 새 토큰이 라이브 연결을 끊지 않고 도달해야 합니다.

## 모델

- **`OpenApiAuthorization`**은 cID의 암호화된 액세스 + 새로고침 토큰을 보유하는 aggregate입니다. `(UserId, CtidUserId)`의 고유 인덱스가 **사용자당 cID당 정확히 하나의 인가**를 적용합니다.
- **`TokenVersion`** — 토큰이 회전할 때마다(`Refresh()`,同一 cID에서 다른 계정을 연결할 때 재인증 경로도 포함) 단조롭게 증가하는 카운터입니다. 단일 유효 토큰 규칙에 대한 버전 마커이며 실행 중인 호스트가 두 토큰 문자열이 우연히衝突할 경우에도 변경을 감지하는 데 사용됩니다.
- 토큰은 `ISecretProtector`(`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`)를 통해 저장에서 암호화됩니다. 평문으로 절대 로그되거나 저장되지 않습니다.

## 전파 (우아한 제자리 스왑)

1. 토큰이 회전합니다 → 새 토큰 + 증가된 `TokenVersion`이 지속됩니다.
2. 호스팅 노드의 `CopyEngineSupervisor`가 각 조정 주기마다 플랜을 다시 읽고 **토큰 시그니처**(액세스 토큰 + 버전)를 계산합니다. 변경은 회전을 의미합니다.
3. 호스트를 분해하고 다시 시작하는 대신(실행 중인 마스터의 실행 스트림이 끊길 것), 감독자는 **실행 중인 호스트에 새 토큰을 푸시합니다**.
4. 호스트가 기존 소켓에서 영향을 받는 계정을 다시 인증합니다(`ProtoOAAccountAuthReq` 다시) `SwapAccessTokenAsync`를 통해, 가벼운 조정을 수행합니다. 이전 토큰이 사망합니다; 복사 스트림이 절대 중지되지 않습니다.

이것이跨 cID 케이스를 안전하게 만드는 것입니다: 사용자가 실행 중mid에同一 cID의 다른 계정을 추가하면 이전 토큰이 무효화되고, 실행 중인 복사 프로필이 새 토큰에서 계속됩니다.

## 새로고침

`OpenApiTokenRefreshService`(백그라운드)가 만료 전에 선제적으로 인가를 새로고침합니다; `OpenApiAuthorization.IsExpiring(threshold, now)`가 게이트합니다. cTrader는 매 새로고침 시 **새로고침** 토큰을 회전하므로 새 새로고침 토큰이 즉시 지속됩니다; 지속할 수 없는 읽기 전용 캐시는 자체 무효화됩니다(비밀의 쓰기 가능한 복사본을 마운트하는 클러스터 내 테스트 Job과 관련 있음).

### 실패エスカレーション

실패한 새로고침은 조용하지 않습니다. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`가 `RefreshFailedAt`을 기록하고 `ConsecutiveRefreshFailures`를 증가시키며 항상 `AccessTokenRefreshFailed`(경고)를 발생시킵니다. 토큰이 현재 `App:OpenApi:TokenRefreshCriticalWindow`(기본값 6h) 내에 있고 새로고침이 계속 실패하면 **한 번** `AccessTokenRefreshCritical` 도메인 이벤트 + `Critical` 로그와 함께エスカレーション하여 소유자가 복사/prop-firm 작업이 토큰을 잃기 전에 다시 인증할 수 있도록 합니다. 실패 카운터와エスカレーション 래치는 다음 성공적인 `Refresh`에서 재설정됩니다. 서비스는 `TokenRefreshInterval`마다 재시도를 계속하므로 제공자/유지보수 가동 중지가 새로고침 엔드포인트가 반환될 때 자체 치유됩니다.

## 무효화 알림 및 자동 복구 (M1)

cID에 대한 부분/다시 인증이 실행 중인 복사 호스트가 여전히 보유한 토큰을 무효화합니다. 거래 호출이 `OpenApiErrorKind.TokenInvalid`로 거부되면 호스트가 고유한 **`CopyTokenInvalidated`** 알림(로그 1078)을 발생시킵니다 — 일반 실패가 아닙니다 — 그래서 알림 채널이 토큰에 주의가 필요함을 압니다. 복구는 자동입니다: 감독자는 매 주기마다 인증을 다시 읽고, 새로고침된 토큰이 토큰 시그니처를 변경하면 실행 중인 호스트에 푸시하여 **제자리 스왑**을 수행합니다 — 복사가 수동 재추가 없이 재개됩니다. `NotLinkable` 프로필(토큰/인증이 일시적으로 해결 불가)도 매 감독 주기마다 다시 평가되며 플랜이 다시 빌드되는 순간 호스트됩니다.

## 호스트 라이브니스 워치독 (M2)

감독자는 각 호스트된 프로필의 실행 작업을 봅니다. 호스트가 프로필이 여전히 이 노드에 할당된 동안 종료되거나 결함이 발생하면 워치독이 취소하고 **다음 주기에 다시 시작**합니다(로그 `CopyHostRestarted`), 그래서 끼운 호스트가 수동 재시작 없이 자체 치유됩니다 — 하나의 프로필 실패가 다른 것을 결�ahari 않습니다(프로필별 격리).

## 테스트

- **단위** — `TokenVersion`이 `Refresh`에서 증가; 호스트가 재시작 없이 제자리 스왑을 수행; 크로스 cID 무효화가 소스 및 대상 토큰을 스왑; **무효화된 대상 토큰이 `CopyTokenInvalidated`를 발생시키고 다음 토큰 푸시에서 자동 복구** (M1); 워치독 `IsHostDead` 결정이 완료/결함 호스트를 다시 시작하고 재할당된 프로필은 단독으로 둡니다 (M2).
- **통합** — `TokenVersion`이 실제 Postgres에서 EF를 통해 지속 및 증가; 토큰 시그니처가 문자열이 변경되지 않더라도 버전 충돌에서 변경됩니다.
