---
description: "안전 관련 복사 이벤트 — 대상 거부 브레이커 트리핑, 계정 보호 또는 프로프-Rule 위반, 패닉 플래튼에 대한所有者별 피드. 기본값 켜짐."
---

# 복사 운영 알림 (Phase 2b)

안전 관련 복사 이벤트 — 대상 거부 브레이커 트리핑, 계정 보호 또는 프로프-Rule 위반, 패닉 플래튼에 대한所有者별 피드. **기본값 켜짐** (`App:Copy:NotificationsEnabled`, 기본값 `true`); 거짓으로 설정하여 음소거. 복사 컨텍스트의 자체 개념, 시장/AI `AlertRule` 애그리게이트와 별개.

## 작동 원리

실행-투명성 로그와 동일한 대역 외 호스트→싱크→드레이너 패턴:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → discards (no-op; unchanged engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves each profile's owner, batches
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- 호스트 `Notify(...)`는 논블로킹, 절대 예외 없음 — 절대 DBに触れない, 절대 복사를 지연시키지 않음.
- 드레이너는 각 알림의 프로필에서 소유 `UserId`를 확인합니다; 프로필이 사라진 알림(소유자 해석 불가)은 고아가 아닌 드롭됩니다.
- `CopyNotification` = 추가 전용, 행별 승인 가능 피드 (애그리게이트 아님).

## 무엇이 발생하는가

| 종류 | 심각도 | 발생 시 |
|------|----------|------|
| `DestinationTripped` | 경고 | G8 거부 예산 소진; 새 개설이 쿨다운 동안 일시 중지됨. |
| `AccountProtectionTriggered` | 위험 | ZuluGuard equity 하한/상한 위반; 개설 래치됨 (SellOut 청산). |
| `PropRuleBreached` | 위험 | 프로프 일일 손실 / 트레일링 드로다운 위반; 대상 플래튼 + 일 중 잠금. |
| `FlattenAll` | 위험 | 패닉 플래튼 실행; 모든 대상 닫힘 + 잠김. |
| `TokenInvalidated` | (예약됨) | 대상의 토큰이 무효화됨; 회전 대기 중. |

## API

- `GET /api/copy/notifications` (소유자 범위) — 모든 프로필에서 사용자의 최근 알림 (가장 최근 200개), plus **미승인** 카운트.
- `POST /api/copy/notifications/{id}/acknowledge` — 하나를 읽은 것으로 표시.

## 구성 (`App:Copy`)

| 설정 | 기본값 | 효과 |
|---------|--------|--------|
| `NotificationsEnabled` | `true` | 안전 알림发出 + 드레이너 실행. `false` → no-op 싱크. |

## 테스트

- **단위** (`CopyNotificationTests`) — 트리핑된 대상이 `DestinationTripped`를 발생; 패닉 플래튼이 프로필 수준 `FlattenAll`을 발생. 캡처 싱크를 통해.
- **통합** (`CopyNotificationDrainerTests`, 실제 Postgres) — 드레이너가 소유자 확인 + 지속; 알 수 없는 프로필의 알림 드롭.
- **DST** — 호스트가 fire-and-forget를发出하고 기본값 no-op 싱크, 그래서 복사 스트레스 스위트가 초록 유지 (23/23).
