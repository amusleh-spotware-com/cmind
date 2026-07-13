---
description: "White-label deployment zriedka dodávajú každú schopnosť. Feature toggles umožňujú operátorovi zapínať/vypínať hlavné produktové funkcie — pri deployi cez config, alebo neskôr za…"
---

# Feature toggles

White-label deployment zriedka dodávajú každú schopnosť. Feature toggles umožňujú operátorovi zapínať/vypínať
hlavné produktové funkcie — pri deployi cez config, alebo neskôr za
behu, bez redeployu. **Všetky funkcie sú predvolene zapnuté**; deployment len
uvádza tie, ktoré mení.

## Model

- `Core.Features.FeatureFlag` — enum gateable funkcií: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Core admin
  surfaces (dashboard, users, nodes, auth) nikdy nie gateable, nie sú tu.
- `Core.Options.FeaturesOptions` — config baseline, bound z `App:Features`. Každá property
  predvolene `true`.
- `Core.Features.IFeatureGate` — resolvuje **effective** stav: config baseline preložený
  s voliteľným owner-set runtime override. Implementované `Infrastructure.Features.FeatureGate`,
  cachuje overrides krátkodobo (`FeatureSettings.OverrideCacheTtl`), invaliduje pri zmene.

Runtime overrides uložené ako `AppSetting` riadky s kľúčom `feature.<FeatureFlag>` (hodnota `true`/`false`).
Žiadny riadok = "použi config baseline".

## Dva spôsoby ako zakázať funkciu

### 1. Deployment konfigurácia (baseline)

Nastavte flag na `false` pod `App:Features`. Príklad `appsettings.json`:

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

Alebo cez env vars (dvojitý podčiarkovník):

```
App__Features__CopyTrading=false
```

Baseline gates **startup registráciu** background workers (`Nodes.AddNodes`) a MCP nástrojov
(`Mcp` server), takže funkcia zakázaná v config nikdy nenaštartuje svoje hosted services ani nevystaví
svoje MCP nástroje.

### 2. Runtime override (owner)

Owner môže prepnúť ľubovoľnú funkciu live z **Settings → Features** (`/settings/features`) alebo API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Zmeny za behu naberajú účinnosť okamžite pre request-time gates (navigácia, API). Background
workers a MCP nástroje gateované pri štarte, zoberú runtime zmenu pri ďalšom reštarte procesu.

## Čo každý gate enforceuje

| Vrstva | Mechanizmus | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` keď disabled | Runtime |
| Navigácia | `NavMenu` skrýva linky cez `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` v `Nodes.AddNodes` | Startup (config) |
| MCP nástroje | conditional `WithTools<>` v MCP serveri | Startup (config) |

Funkcia dosiahnutá cez deep link počas disabled renderuje prázdnu stránku — jej API vráti `404`;
nav ju už nepovrchuje.

## Flag → surface mapa

| Flag | API skupiny | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots skupina → cBots (param sets per-cBot dialóg) | MCP `CBotTools` |
| Backtesting | (zdieľa `/api/instances`) | cBots skupina → Backtest | — |
| Execution | `/api/instances` | cBots skupina → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI skupina → AI; Settings → AI (key) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI skupina → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI skupina → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop skupina → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop skupina → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI skupina → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testy

- **Jednotka** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integrácia** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats
  config a perzistuje ako `AppSetting`, clearing reverts to baseline (reálny Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: zakázanie `CopyTrading` za behu skryje jeho nav link a
  `404` `/api/copy`, re-zapnutie obnoví obe.
