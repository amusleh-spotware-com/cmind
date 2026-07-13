---
description: "복사 시도마다 포착된 지연, 실현 슬리피지, 체결 대 실패와 같은 복사 실행 사실 — 프로필별 투명성 보고서로 표면화. 기본값 꺼짐."
---

# 복사 실행 투명성 (Phase 3)

복사 시도마다 포착된 지연, 실현 슬리피지, 체결 대 실패와 같은 복사 실행 사실 — 프로필별 투명성 보고서로 표면화. **기본값 꺼짐**; `App:Copy:TransparencyEnabled=true`로 활성화. 꺼짐 시 복사 엔진은 변경 없이 동일: 호스트가 no-op 싱크로，发出하고 아무것도 작성 안 함.

## 작동 원리

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → discards (default; zero hot-path cost)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches every App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **핫 패스가 I/O에서 자유로움.** 호스트가 `ICopyEventSink.Record(...)`를 호출합니다 — 논블로킹, 절대 예외 발생 안 함 enqueue. 절대 await 안 함, 절대 DBに触れない, 절대 주문 실행 차단 안 함.
- **손실 선호 over back-pressure.** 채널 제한됨 (`CopyExecutionChannelCapacity`) 및 `DropOldest`: DB 드레이너가 멈추면 *가장 오래된* 투명성 행이 삭제되어 복사를 지연시키는 대신. 투명성 =尽力 telemetry이지 거래 의존성이 아닙니다.
- **대역 외 지속성.** `CopyExecutionDrainer`가 채널을 배치 (`CopyExecutionDrainBatchSize`)마다 `CopyExecutionDrainInterval`에서 드레인하고, 범위 `DataContext`를 통해 `CopyExecution` 행을 작성합니다. 종료 시 최종 플러시.
- **사실이지 명령이 아닙니다.** `CopyExecution` = 추가 전용 로그 (인스턴스 로그/`감사 로그`와 유사), 애그리게이트 아닙니다. 읽기 모델이 직접 쿼리합니다 (CQRS-lite), 애그리게이트가 메모리에 있습니다.

## 무엇이 기록되는가

대상에서 하나의 복사 시도마다 `CopyExecutionRecord`:

| 종류 | 발생 시 | 전달 |
|------|---------|------|
| `Opened` | 복사 주문 배치됨 | 심볼, 사이드, 와이어 볼륨, 마스터 가격, 실현 슬리피지 (포인트), 지연 (ms) |
| `Failed` | 복사 개설가throw/거부됨 | 심볼, 사이드, 마스터 볼륨/가격, 지연, 실패 이유 (예외 유형) |

(`Closed`/`Skipped`/`Reconciled`은 미래 확장을 위해 enum에 존재합니다.)

## 보고서

`GET /api/copy/profiles/{id}/transparency` (소유자 범위)는 가장 최근 500개 사실에 대해 반환합니다:

- **요약** — 전체, 개설, 실패, **체결률**, **평균 지연 (ms)**, **평균 슬리피지 (포인트)**.
- **최근** — 원시 최근 사실 (대상, 소스 포지션, 심볼, 사이드, 볼륨, 마스터 가격, 슬리피지, 지연, 이유, 타임스탬프).

## 구성 (`App:Copy`)

| 설정 | 기본값 | 효과 |
|---------|--------|--------|
| `TransparencyEnabled` | `false` | 노드에 대한 복사 사실 캡처 + 드레이너 켜기. |

채널 용량, 드레인 배치 크기, 드레인 간격 = `CopyDefaults` 상수 (`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## 테스트

- **단위** (`CopyTransparencyTests`) — 성공적 개설이 올바른 심볼/사이드/볼륨/지연로 `Opened` 사실을 방출; 거부된 개설이 이유와 함께 `Failed` 사실을 방출. 캡처 싱크를 통해 구동됩니다.
- **통합** (`CopyExecutionDrainerTests`, 실제 Postgres) — 드레이너가 버퍼된 사실을 `CopyExecution` 로그에 지속; 빈 싱크는 아무것도 작성 안 함.
- **DST** — 호스트 변경이 fire-and-forget 및 기본값 no-op 싱크, 그래서 결정론적 복사 스트레스 스위트가 초록 유지 (23/23).
