---
description: "White-label deploy rzadko ship każdy capability. Feature toggles daj operator turn main product features on/off — na deploy time via config, lub later na…"
---

# Feature toggles

White-label deploy rzadko ship każdy capability. Feature toggles daj operator turn main
product features on/off — na deploy time via config, lub later na runtime, no
redeploy. **Wszystkie features default enabled**; deployment tylko lists które change.

## Model

- `Core.Features.FeatureFlag` — enum gateable features: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Core admin
  surfaces (dashboard, users, nodes, auth) nigdy gateable, nie tutaj.
- `Core.Options.FeaturesOptions` — config baseline, bound z `App:Features`. Każdy property
  defaults `true`.
- `Core.Features.IFeatureGate` — resolves **effective** state: config baseline overlaid
  z optional owner-set runtime override. Implemented przez `Infrastructure.Features.FeatureGate`,
  caches overrides briefly (`FeatureSettings.OverrideCacheTtl`), invalidates na change.

Runtime overrides stored jako `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`).
No row = "use config baseline".

## Dwie ways do disable feature

### 1. Deployment configuration (baseline)

Set flag `false` pod `App:Features`. Example `appsettings.json`:

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

Lub via env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gates **startup registration** z background workers (`Nodes.AddNodes`) i MCP tools
(`Mcp` server), więc feature disabled w config nigdy nie start hosted services ani expose
MCP tools.

### 2. Runtime override (owner)

Owner może flip każdy feature live z **Settings → Features** (`/settings/features`) lub API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Runtime changes take effect immediately dla request-time gates (navigation, API). Background
workers i MCP tools gated na startup, pick up runtime change na next process restart.

## Co każdy gate enforces

| Layer | Mechanism | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` gdy disabled | Runtime |
| Navigation | `NavMenu` hides links via `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` w `Nodes.AddNodes` | Startup (config) |
| MCP tools | conditional `WithTools<>` w MCP server | Startup (config) |

Feature reached przez deep link gdy disabled renders empty page — jego API returns `404`;
nav no longer surfaces to.

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

## Testy

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integracja** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats
  config i persists jako `AppSetting`, clearing reverts do baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabling `CopyTrading` na runtime hides jego nav link i
  `404`s `/api/copy`, re-enabling restores oba.
