---
description: "테스트 스위트가 필요로 하는 모든 자격 증명은 단일 gitignored 파일에 있습니다: secrets/dev-credentials.local.json. 커밋된 템플릿을 복사하고 채우세요..."
---

# 개발자 자격 증명 — 모든 테스트를 위한 하나의 파일

테스트 스위트가 필요로 하는 모든 자격 증명은 단일 gitignored 파일에 있습니다: `secrets/dev-credentials.local.json`. 커밋된 템플릿을 복사하고 당신이 가진 것을 채우세요 — 모든 값은 선택 사항이고 누락된 값이 필요한 테스트는 깨끗하게 건너뜁니다.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# secrets/dev-credentials.local.json 편집
```

## 각 테스트 계층이 읽는 항목

| 계층 | 필요 | 출처 |
|------|-------|------|
| **단위** (`tests/UnitTests`) | 없음 | — 결정론적, 비밀 없음, 네트워크 없음 |
| **통합** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — 자동 |
| **라이브 복사** (`tests/IntegrationTests/CopyLive`) | OpenAPI 앱 + 토큰 캐시 | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E 온보딩** (`tests/E2ETests/CopyLive`) | OpenAPI 앱 + cID 로그인 | `OpenApi.App`, `OpenApi.Cids` |
| **E2E 실제 실행/백테스트** (`CBotRealRunBacktestTests`) | cID 로그인 + **데모** 계정 번호 | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI 기능** | Anthropic 키 | `Ai.ApiKey` (미설정 ⇒ AI 기능은 비활성화, 앱은 여전히 실행) |

## 스키마

저장소 루트의 `dev-credentials.example.json`을 참조하세요. 섹션:

- `OpenApi.App` — cTrader Open API 응용 프로그램의 `{ ClientId, ClientSecret }`.
- `OpenApi.Cids` — 헤드리스 OAuth 온보딩에서 사용되는 cTrader ID 로그인. 각 항목은 또한 **`Accounts`** 배열을 수행합니다 — 테스트 인프라가 앱에 링크하고 구동하는 것이 허용된 cTrader 거래 계정 번호 (로그인/계정 번호, 예: `3635817`). `CBotRealRunBacktestTests`는 비어있지 않은 `Accounts` 배열을 가진 첫 항목을 읽고, 해당 cID + 계정을 앱에 추가한 다음 정말 실행하고 백테스트합니다. **여기에 데모 계정 번호만 넣으세요** — 절대 라이브 계정 아닙니다; 실행/백테스트 테스트는 나열한 계정에 실제 주문을 배치합니다. 빈/생략된 `Accounts` ⇒ 실제 실행/백테스트 테스트는 깨끗하게 건너뜁니다.
- `OpenApi.Tokens` — 다중 cID 토큰 캐시 (승인된 cID당 하나 항목, 새로고침/액세스 토큰 + 계정 목록). 자동으로 온보딩 및 토큰 새로고침 단계에서 작성됨; 손으로 거의 편집하지 않습니다.
- `Owner` — E2E 아래 앱에 대한 시드 소유자 로그인.
- `Database.ConnectionString` — Testcontainers 대신 외부 Postgres를 지정하는 테스트만.
- `Ai.ApiKey` — AI 기능에 대한 Anthropic API 키.

## 우선순위

1. **환경 변수**는 모든 것을 재정의합니다 (예: `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — 통합 파일 (선호).
3. **레거시 분할 파일** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json`은 통합 파일이 없을 때 여전히 읽혀집니다. 기존 머신은 계속 작동합니다. 새로운 설정은 단일 파일을 사용해야 합니다.

## 안전

- `secrets/` 및 `*.local.json`은 gitignored — 여기의 아무것도 커밋되지 않습니다.
- 라이브 복사 테스트는 비 데모 계정에 대해 실행되기를 거부합니다 (`IsLive` 계정은 `LiveCopyFixture`에 의해 필터링됨). 토큰 캐시에 데모 계정만 유지하세요.
- 클러스터 내 (Kubernetes) 실행은 읽기 전용 비밀로 파일을 마운트합니다; 토큰 새로고침은 메모리에서 유지되고 읽기 전용 쓰기 백은 조용한 노옵입니다.
