# Feature toggles

White-label deployments rarely ship every capability. Feature toggles let an operator turn the main
product features on or off — at deploy time through configuration, or later at runtime without a
redeploy. **All features default to enabled**; a deployment only lists the ones it wants to change.

## Model

- `Core.Features.FeatureFlag` — the enum of gateable features: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `Accounts`, `OpenApi`, `Mcp`. Core admin
  surfaces (dashboard, users, nodes, auth) are never gateable and do not appear here.
- `Core.Options.FeaturesOptions` — the configuration baseline, bound from `App:Features`. Every property
  defaults to `true`.
- `Core.Features.IFeatureGate` — resolves the **effective** state: the configuration baseline overlaid
  with an optional owner-set runtime override. Implemented by `Infrastructure.Features.FeatureGate`,
  which caches overrides briefly (`FeatureSettings.OverrideCacheTtl`) and invalidates on change.

Runtime overrides are stored as `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`).
Absence of a row means "use the configuration baseline".

## Two ways to disable a feature

### 1. Deployment configuration (baseline)

Set the flag to `false` under `App:Features`. Example `appsettings.json`:

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

Or via environment variables (double underscore):

```
App__Features__CopyTrading=false
```

The baseline gates **startup registration** of background workers (`Nodes.AddNodes`) and MCP tools
(`Mcp` server), so a feature disabled in configuration never starts its hosted services or exposes its
MCP tools.

### 2. Runtime override (owner)

An owner can flip any feature live from **Settings → Features** (`/settings/features`) or the API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Runtime changes take effect immediately for the request-time gates (navigation and API). Background
workers and MCP tools are gated at startup and pick up a runtime change on the next process restart.

## What each gate enforces

| Layer | Mechanism | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` when disabled | Runtime |
| Navigation | `NavMenu` hides links via `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` in `Nodes.AddNodes` | Startup (config) |
| MCP tools | conditional `WithTools<>` in the MCP server | Startup (config) |

A feature reached by a deep link while disabled renders an empty page because its API returns `404`;
navigation no longer surfaces it.

## Flag → surface map

| Flag | API groups | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots, Parameter Sets | MCP `CBotTools` |
| Backtesting | (shares `/api/instances`) | Backtest | — |
| Execution | `/api/instances` | Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI Assistant, Strategy Builder | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop Guard | `PropGuardService` |
| Accounts | `/api/ctids` | cTrader Accounts | — |
| OpenApi | `/api/openapi` | Open API Apps | — |
| Mcp | `/api/mcp-keys` | MCP Keys | — |

## Tests

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats
  config and persists as `AppSetting`, clearing reverts to baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabling `CopyTrading` at runtime hides its nav link and
  `404`s `/api/copy`, and re-enabling restores both.
