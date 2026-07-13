---
description: "White-label deploy hiếm khi ship mọi capability. Feature toggles cho phép operator turn main product features on/off — at deploy time via config, or later at…"
---

# Feature toggles

White-label deploy hiếm khi ship mọi capability. Feature toggles cho phép operator turn main
product features on/off — at deploy time via config, or later at runtime, no
redeploy. **All features default enabled**; deployment chỉ lists ones nó change.

## Model

- `Core.Features.FeatureFlag` — enum của gateable features: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Core admin
  surfaces (dashboard, users, nodes, auth) never gateable, not here.
- `Core.Options.FeaturesOptions` — config baseline, bound from `App:Features`. Mọi property
  defaults `true`.
- `Core.Features.IFeatureGate` — resolves **effective** state: config baseline overlaid
  với optional owner-set runtime override. Implemented by `Infrastructure.Features.FeatureGate`,
  caches overrides briefly (`FeatureSettings.OverrideCacheTtl`), invalidates on change.

Runtime overrides stored as `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`).
No row = "use config baseline".

## Hai cách để disable a feature

### 1. Deployment configuration (baseline)

Set flag `false` under `App:Features`. Ví dụ `appsettings.json`:

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

Or via env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gates **startup registration** của background workers (`Nodes.AddNodes`) và MCP tools
(`Mcp` server), vì vậy feature disabled in config never starts its hosted services nor exposes its
MCP tools.

### 2. Runtime override (owner)

Owner có thể flip bất kỳ feature live từ **Settings → Features** (`/settings/features`) hoặc API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Runtime changes take effect immediately cho request-time gates (navigation, API). Background
workers và MCP tools gated at startup, pick up runtime change on next process restart.

## Mỗi gate enforce gì

| Layer | Mechanism | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` when disabled | Runtime |
| Navigation | `NavMenu` hides links via `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` in `Nodes.AddNodes` | Startup (config) |
| MCP tools | conditional `WithTools<>` in MCP server | Startup (config) |

Feature reached by deep link while disabled renders empty page — its API returns `404`;
nav no longer surfaces it.

## Flag → surface map

| Flag | API groups | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots group → cBots (param sets per-cBot dialog) | MCP `CBotTools` |
| Backtesting | (shares `/api/instances`) | cBots group → Backtest | — |
| Execution | `/api/instances` | cBots group → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI group → AI; Settings → AI (key) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI group → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI group → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop group → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop group → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI group → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Tests

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats
  config và persists as `AppSetting`, clearing reverts to baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabling `CopyTrading` at runtime hides its nav link và
  `404`s `/api/copy`, re-enabling restores both.
