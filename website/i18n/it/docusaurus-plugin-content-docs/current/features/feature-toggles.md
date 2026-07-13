---
description: "I deployment white-label raramente spediscono ogni capability. I feature toggle permettono all'operatore di accendere/spegnere le capability del prodotto principale — al deploy via config, o dopo a runtime, no redeploy."
---

# Feature toggle

I deployment white-label raramente spediscono ogni capability. I feature toggle permettono all'operatore di
accendere/spegnere le capability del prodotto principale — al deploy via config, o dopo a runtime, no
redeploy. **Tutte le feature default enabled**; il deployment elenca solo quelle che cambia.

## Modello

- `Core.Features.FeatureFlag` — enum di feature gateable: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Le superfici admin Core
  (dashboard, users, nodes, auth) mai gateable, non qui.
- `Core.Options.FeaturesOptions` — baseline config, bound da `App:Features`. Ogni proprietà
  default `true`.
- `Core.Features.IFeatureGate` — risolve stato **effective**: baseline config sovrapposto
  con opzionale override runtime impostato dall'owner. Implementato da `Infrastructure.Features.FeatureGate`,
  cache overrides brevemente (`FeatureSettings.OverrideCacheTtl`), invalida su cambiamento.

Gli override runtime memorizzati come righe `AppSetting` key `feature.<FeatureFlag>` (value `true`/`false`).
Nessuna riga = "use config baseline".

## Due modi per disabilitare una feature

### 1. Configurazione deployment (baseline)

Imposta flag `false` sotto `App:Features`. Esempio `appsettings.json`:

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

Oppure via env vars (double underscore):

```
App__Features__CopyTrading=false
```

Il baseline gates **startup registration** dei background workers (`Nodes.AddNodes`) e dei tool MCP
(`Mcp` server), così una feature disabilitata in config non fa mai partire i suoi hosted services né espone i
suoi tool MCP.

### 2. Override runtime (owner)

L'owner può flippare qualsiasi feature live da **Settings → Features** (`/settings/features`) o API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

I cambiamenti runtime hanno effetto immediato per i gate request-time (navigation, API). I Background
workers e i tool MCP gated all'avvio, raccolgono il cambiamento runtime al prossimo processo restart.

## Cosa ogni gate applica

| Layer | Meccanismo | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` quando disabilitato | Runtime |
| Navigation | `NavMenu` nasconde link via `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` in `Nodes.AddNodes` | Startup (config) |
| MCP tools | conditional `WithTools<>` nel MCP server | Startup (config) |

Feature raggiunta da deep link mentre disabilitata renderizza pagina vuota — la sua API restituisce `404`;
la nav non la surfacza più.

## Mappa flag → superficie

| Flag | API groups | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots group → cBots (param sets per-cBot dialog) | MCP `CBotTools` |
| Backtesting | (condivide `/api/instances`) | cBots group → Backtest | — |
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

## Test

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults, per-flag mapping.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override batte
  config e persiste come `AppSetting`, clearing ripristina baseline (Postgres reale).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabilitare `CopyTrading` a runtime nasconde il suo
  nav link e fa `404` su `/api/copy`, ri-abilitando ripristina entrambi.
