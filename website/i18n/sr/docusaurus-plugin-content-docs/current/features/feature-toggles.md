---
description: "White-label deploy retko isporučuje svaku mogućnost. Feature toggles dozvoljavaju operateru da uključi/isključi glavne funkcije proizvoda — u vreme deploy-a preko konfiguracije, ili kasnije u runtime-u, bez redploy-a."
---

# Feature toggles

White-label deploy retko isporučuje svaku mogućnost. Feature toggles dozvoljavaju operateru da uključi/isključi glavne
funkcije proizvoda — u vreme deploy-a preko konfiguracije, ili kasnije u runtime-u, bez
redploy-a. **Sve funkcije podrazumevano uključene**; deploy navodi samo one koje menja.

## Model

- `Core.Features.FeatureFlag` — enum gateable funkcija: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Core admin
  surfaces (dashboard, users, nodes, auth) nikad gateable, ne ovde.
- `Core.Options.FeaturesOptions` — config baseline, vezan iz `App:Features`. Svako svojstvo
  podrazumevano `true`.
- `Core.Features.IFeatureGate` — resoluje **efektivno** stanje: config baseline preklopljen
  opcionim owner-set runtime override-om. Implementiran od `Infrastructure.Features.FeatureGate`,
  kesira override-ove kratko (`FeatureSettings.OverrideCacheTtl`), invalidira na promenu.

Runtime override-ovi su uskladišteni kao `AppSetting` redovi key-ovani `feature.<FeatureFlag>` (vrednost `true`/`false`).
Nema reda = "koristi config baseline".

## Dva načina da se isključi funkcija

### 1. Deployment konfiguracija (baseline)

Postavi flag na `false` pod `App:Features`. Primer `appsettings.json`:

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

Ili preko env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gating **startup registration** background workera (`Nodes.AddNodes`) i MCP alata
(`Mcp` server), tako da funkcija isključena u config-u nikad ne pokreće svoje hosted servise niti izlaže svoje
MCP alate.

### 2. Runtime override (owner)

Owner može prebaciti bilo koju funkciju live iz **Settings → Features** (`/settings/features`) ili API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> postavi override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> očisti override (revert)     (Owner)
```

Runtime promene stupaju na snagu odmah za request-time gate-ove (navigacija, API). Background
workeri i MCP alati gate-ovani na startup, primaju runtime promenu na sledeći process restart.

## Šta svaki gate enforces

| Sloj | Mehanizam | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` kada je isključeno | Runtime |
| Navigacija | `NavMenu` skriva linkove preko `IFeatureGate.IsEnabled` | Runtime |
| Background workeri | conditional `AddHostedService` u `Nodes.AddNodes` | Startup (config) |
| MCP alati | conditional `WithTools<>` u MCP serveru | Startup (config) |

Funkcija dosegnuta deep link-om dok je isključena prikazuje praznu stranicu — njen API vraća `404`;
nav je više ne površava.

## Flag → surface mapa

| Flag | API grupe | Nav | Workeri / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots grupa → cBots (param sets per-cBot dijalog) | MCP `CBotTools` |
| Backtesting | (deli `/api/instances`) | cBots grupa → Backtest | — |
| Execution | `/api/instances` | cBots grupa → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI grupa → AI; Settings → AI (key) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI grupa → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI grupa → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop grupa → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop grupa → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI grupa → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testovi

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override pobeđuje
  config i perzistira kao `AppSetting`, clearing reverts to baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: isključivanje `CopyTrading` u runtime-u skriva njegov nav link i
  `404`-uje `/api/copy`, ponovno uključivanje vraća oba.
