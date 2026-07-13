---
description: "복사 전략의 검색 가능한 디렉토리. 제공자가 검증된 라이브 배지 (전략 소스 계정이 실제 돈을 거래, 데모 아님)와 성과 수수료로 복사 프로필을 목록으로 게시합니다."
---

# 복사 제공자 마켓플레이스 (Phase 4)

복사 전략의 검색 가능한 디렉토리. 제공자가 **검증된 라이브** 배지 (전략 소스 계정이 실제 돈을 거래, 데모 아님) plus 성과 수수료로 복사 프로필을 목록으로 게시합니다. 팔로어가 마켓플레이스를 浏览하고 실행 투명성 데이터에서 프로젝션된 성과 점수로 순위가 매겨집니다.

## 모델

- `CopyProviderListing` = 애그리게이트: `UserId`, `ProfileId`, 표시 이름, 설명, 성과 수수료, `VerifiedLive`, `Published` + `PublishedAt`. 프로필당 하나의 목록 (고유 인덱스).
- **검증된 라이브**는 게시 시 프로필 소스 `TradingAccount.IsLive`에서 파생됩니다 — 제공자가 자체 어설션할 수 없습니다.
- 성과 통계는 listing에 저장되지 **않습니다** — `CopyExecution` 투명성 로그에 대한 읽기 모델 프로젝션에서 파생됩니다 (체결률, 평균 지연, 평균 실현 슬리피지), 그래서 마켓플레이스가 항상 라이브 실행 품질을 반영합니다.

## 순위

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 점수: 체결률이 지배합니다 (×60), 낮은 지연 + 낮은 슬리피지가 추가 (각 ×20), 검증된 라이브 배지가 작은 신뢰 보너스를 추가합니다. 결정론적 + 단조롭므로 순서가 안정적입니다.

## API

- `POST /api/copy/profiles/{id}/publish` — 목록 프로필 게시/업데이트 (`DisplayName`, `Description`, `PerformanceFeePercent`); 검증된 라이브가 소스 계정에서 설정됩니다.
- `DELETE /api/copy/profiles/{id}/publish` — 게시 취소.
- `GET /api/copy/marketplace` — 게시된 모든 목록, 순위 매겨짐, 각각 성과 요약 (실행, 체결률, 평균 지연, 평균 슬리피지, 점수) + 검증된 라이브 배지 포함.

## 테스트

- **단위** (`CopyProviderListingTests`) — 애그리게이트 불변량: 표시 이름 필수; 게시가 타임스탬프 설정; 게시 취소가 숨김; 업데이트가 표시 필드 + 수수료 + 배지 교체.
- **통합** (`CopyMarketplaceTests`, 실제 Postgres) — 게시된 목록이 배지와 함께 지속; 프로필당 하나의 목록 (고유 인덱스); 순위 점수가 검증됨/높은 체결률 제공자를 선호합니다.

복사 호스트는 변경되지 않습니다 (목록 + 읽기 모델만 해당), 그래서 복사 DST 스트레스 스위트가 영향받지 않습니다.
