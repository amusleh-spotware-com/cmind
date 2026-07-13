---
description: "White-label deploy vzácně lodní každá schopnost. Feature toggles umožňují operátor otočit hlavní product vlastnosti on/off — na deploy čas přes config, nebo později na…"
---

# Feature toggles

White-label deploy vzácně lodní každá schopnost. Feature toggles umožňují operátor otočit hlavní product vlastnosti on/off — na deploy čas přes config, nebo později na runtime, bez redeploy. **Všechny vlastnosti výchozí povolené**; nasazení pouze seznam ty to změní.

## Model

- `Core.Features.FeatureFlag` — enum of gateable vlastnosti: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Core admin povrchy (dashboard, uživatelé, uzly, auth) nikdy gateable, ne zde.
- `Core.Options.FeaturesOptions` — config baseline, bound z `App:Features`. Každá vlastnost výchozí `true`.
- `Core.Features.IFeatureGate` — resolves **efektivní** stav: config baseline overlay s volitelným owner-set runtime override. Implementováno `Infrastructure.Features.FeatureGate`, caches overrides stručně (`FeatureSettings.OverrideCacheTtl`), invalidates na změnu.

Runtime overrides uloženy jako `AppSetting` řady klíčem `feature.<FeatureFlag>` (value `true`/`false`). Žádný řádek = "use config baseline".

## Dva způsoby jak vypnout vlastnost

### 1. Deployment konfigurace (baseline)

Nastavit flag `false` pod `App:Features`. Příklad `appsettings.json`:

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

Nebo přes env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gates **startup registrace** background workerů (`Nodes.AddNodes`) a MCP nástrojů (`Mcp` server), takže vlastnost vypnuta v configu nikdy startuje jejího hosted services ani exponuje jejího MCP nástrojů.

### 2. Runtime override (owner)

Owner může otočit libovolnou vlastnost live z **Settings → Features** (`/settings/features`) nebo API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> nastavit override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (vrátit)  (Owner)
```

Runtime změny vejdou v platnost okamžitě pro request-time gates (navigace, API). Background workeři a MCP nástrojů gated na startup, sbírají runtime změnu na následující proces restart.

## Co každá brána vynucuje

| Vrstva | Mechanismus | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filtr → `404` když vypnuto | Runtime |
| Navigace | `NavMenu` skrývá linky via `IFeatureGate.IsEnabled` | Runtime |
| Background workeři | conditional `AddHostedService` v `Nodes.AddNodes` | Startup (config) |
| MCP nástrojů | conditional `WithTools<>` v MCP serveru | Startup (config) |

Vlastnost dosažena deep link zatímco vypnuto renders prázdná stránka — její API vrátí `404`; nav to už nepovrchuje.

## Flag → surface mapa

| Flag | API skupiny | Nav | Workeři / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots skupiny → cBots (param sets per-cBot dialog) | MCP `CBotTools` |
| Backtesting | (shares `/api/instances`) | cBots skupiny → Backtest | — |
| Execution | `/api/instances` | cBots skupiny → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI skupiny → AI; Settings → AI (klíč) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI skupiny → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI skupiny → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop skupiny → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop skupiny → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI skupiny → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testy

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline výchozí, per-flag mapování.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats config a persists jako `AppSetting`, clearing vrací baseline (reálný Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: vypnutí `CopyTrading` na runtime skryje nav link a `404`s `/api/copy`, re-enabling restores obojí.
