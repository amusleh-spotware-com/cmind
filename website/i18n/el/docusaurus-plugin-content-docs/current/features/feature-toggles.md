---
description: "White-label deploy rarely ship every capability. Feature toggles let operator turn main product features on/off — at deploy time via config, or later at…"
---

# Feature toggles

Τα White-label deploys σπάνια έχουν κάθε capability. Τα Feature toggles επιτρέπουν στο operator να ενεργοποιήσει/απενεργοποιήσει τα κύρια
product features — κατά deploy time μέσω config, ή αργότερα κατά runtime, χωρίς
redeploy. **Όλα τα features default enabled**; deployment μόνο λίστα αυτά που αλλάζει.

## Model

- `Core.Features.FeatureFlag` — enum των gateable features: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Core admin
  surfaces (dashboard, users, nodes, auth) ποτέ gateable, όχι εδώ.
- `Core.Options.FeaturesOptions` — config baseline, bound από `App:Features`. Κάθε property
  defaults `true`.
- `Core.Features.IFeatureGate` — resolves **effective** state: config baseline overlaid
  με προαιρετικό owner-set runtime override. Implemented by `Infrastructure.Features.FeatureGate`,
  caches overrides briefly (`FeatureSettings.OverrideCacheTtl`), invalidates κατά αλλαγή.

Runtime overrides αποθηκεύονται ως `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`).
Χωρίς row = "χρησιμοποιήστε config baseline".

## Δύο τρόποι για να απενεργοποιήσετε ένα feature

### 1. Deployment configuration (baseline)

Θέστε flag `false` κάτω από `App:Features`. Παράδειγμα `appsettings.json`:

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

Ή μέσω env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gates **startup registration** των background workers (`Nodes.AddNodes`) και MCP tools
(`Mcp` server), ώστε το feature disabled σε config ποτέ δεν start τα hosted services του ούτε expose το MCP tools.

### 2. Runtime override (owner)

Ο owner μπορεί να flip οποιοδήποτε feature live από **Settings → Features** (`/settings/features`) ή API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Τα Runtime changes παίρνουν effect αμέσως για request-time gates (navigation, API). Τα Background
workers και MCP tools gated κατά startup, pick up runtime change στο επόμενο process restart.

## Τι εφαρμόζει κάθε gate

| Layer | Mechanism | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` όταν disabled | Runtime |
| Navigation | `NavMenu` κρύβει links μέσω `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` σε `Nodes.AddNodes` | Startup (config) |
| MCP tools | conditional `WithTools<>` στο MCP server | Startup (config) |

Το Feature reached by deep link ενώ disabled renders empty page — το API του επιστρέφει `404`;
nav δεν περισσότερο surfaces αυτό.

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
  config και persists ως `AppSetting`, clearing reverts στο baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabling `CopyTrading` κατά runtime κρύβει το nav link του και
  `404`s `/api/copy`, re-enabling αποκαθιστά και τα δύο.
