---
description: "화이트라벨 배포는 모든 기능을 배포하기 어렵습니다. 기능 토글을 사용하면 운영자는 주요 제품 기능을 켜고/끌 수 있습니다 — 배포 시간에 구성을 통해 또는 나중에..."
---

# 기능 토글

화이트라벨 배포는 모든 기능을 배포하기 어렵습니다. 기능 토글을 사용하면 운영자는 주요 제품 기능을 켜고/끌 수 있습니다 — 배포 시간에 구성을 통해 또는 나중에 런타임에, 재배포 없음. **모든 기능은 기본적으로 활성화됩니다**; 배포는 변경하는 것만 나열합니다.

## 모델

- `Core.Features.FeatureFlag` — 게이트 가능한 기능의 열거형: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Core 관리 표면 (대시보드, 사용자, 노드, 인증)은 게이트 가능하지 않습니다.
- `Core.Options.FeaturesOptions` — 구성 기준선, `App:Features`에서 바인딩됨. 모든 속성은 기본값 `true`.
- `Core.Features.IFeatureGate` — 해결 **효과적** 상태: 선택적 소유자 설정 런타임 재정의로 오버레이된 구성 기준선. `Infrastructure.Features.FeatureGate`로 구현되고, 재정의를 간략히 캐시합니다 (`FeatureSettings.OverrideCacheTtl`), 변경 시 무효화합니다.

런타임 재정의는 `AppSetting` 행으로 저장됩니다. `feature.<FeatureFlag>` 키 지정 (값 `true`/`false`). 행 없음 = "구성 기준선 사용".

## 기능을 비활성화하는 두 가지 방법

### 1. 배포 구성 (기준선)

`App:Features` 아래에서 플래그 `false`를 설정합니다. 예시 `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

또는 환경 변수를 통해 (이중 언더스코어):

```
App__Features__CopyTrading=false
```

기준선 게이트 **시작 등록** 백그라운드 워커 (`Nodes.AddNodes`) 및 MCP 도구 (`Mcp` 서버)이므로 구성에서 비활성화된 기능은 호스팅된 서비스를 시작하거나 MCP 도구를 노출하지 않습니다.

### 2. 런타임 재정의 (소유자)

소유자는 **설정 → 기능** (`/settings/features`) 또는 API에서 모든 기능을 실시간으로 뒤집을 수 있습니다:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (소유자)
PUT  /api/features/{flag}      본문 { "enabled": false }  -> 재정의 설정             (소유자)
PUT  /api/features/{flag}      본문 { "enabled": null  }  -> 재정의 클리어 (되돌림)  (소유자)
```

런타임 변경은 요청 시 게이트 (탐색, API)에 즉시 적용됩니다. 백그라운드 워커 및 MCP 도구는 시작 시 게이트되고 다음 프로세스 재시작에서 런타임 변경을 선택합니다.

## 각 게이트가 적용하는 항목

| 계층 | 메커니즘 | 타이밍 |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` 엔드포인트 필터 → 비활성화 시 `404` | 런타임 |
| 탐색 | `NavMenu`는 `IFeatureGate.IsEnabled`를 통해 링크를 숨깁니다 | 런타임 |
| 백그라운드 워커 | `Nodes.AddNodes`에서 조건부 `AddHostedService` | 시작 (구성) |
| MCP 도구 | MCP 서버에서 조건부 `WithTools<>` | 시작 (구성) |

비활성화된 동안 딥 링크로 도달한 기능은 빈 페이지를 렌더링합니다 — 해당 API는 `404` 반환; 탐색은 더 이상 노출하지 않습니다.

## 플래그 → 표면 지도

| 플래그 | API 그룹 | 탐색 | 워커 / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots 그룹 → cBots (매개변수 세트 cBot당 대화) | MCP `CBotTools` |
| Backtesting | (공유 `/api/instances`) | cBots 그룹 → Backtest | — |
| Execution | `/api/instances` | cBots 그룹 → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI 그룹 → AI; 설정 → AI (키) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI 그룹 → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI 그룹 → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop 그룹 → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop 그룹 → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | 설정 → Open API | — |
| Mcp | `/api/mcp-keys` | AI 그룹 → MCP Keys | — |
| Compliance | `/api/compliance` | 설정 → Legal & Privacy | — |

## 테스트

- **단위** — `UnitTests/Features/FeaturesOptionsTests.cs`: 기준선 기본값, 플래그별 매핑.
- **통합** — `IntegrationTests/FeatureGateTests.cs`: 구성 기준선, 런타임 재정의가 구성을 이기고 `AppSetting`으로 유지됨, 클리어는 기준선으로 되돌림 (실제 Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: 런타임에서 `CopyTrading`을 비활성화하면 탐색 링크를 숨기고 `/api/copy`를 `404`, 다시 활성화하면 둘 다 복원합니다.
