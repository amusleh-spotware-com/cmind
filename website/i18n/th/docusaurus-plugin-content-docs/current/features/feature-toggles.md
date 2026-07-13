---
description: "White-label deploy rarely ขาย ทุก ๆ capability feature toggles let operator turn main product features on/off — ที่ deploy time ผ่าน config หรือ later ที่ runtime no redeploy **ทั้งหมด features default enabled**; deployment เพียง lists ones มันเปลี่ยน"
---

# Feature toggles

white-label deploy rarely ขาย ทุก ๆ capability feature toggles let operator turn main product features on/off — ที่ deploy time ผ่าน config หรือ later ที่ runtime no redeploy **ทั้งหมด features default enabled**; deployment เพียง lists ones มันเปลี่ยน

## Model

- `Core.Features.FeatureFlag` — enum ของ gateable features: `Authoring` `Backtesting` `Execution` `CopyTrading` `Ai` `PortfolioAgent` `Alerts` `PropGuard` `PropFirm` `Accounts` `OpenApi` `Mcp` `Compliance` core admin surfaces (dashboard users nodes auth) ไม่เคย gateable ไม่มี here
- `Core.Options.FeaturesOptions` — config baseline bound จาก `App:Features` ทุก ๆ property defaults `true`
- `Core.Features.IFeatureGate` — resolves **effective** state: config baseline overlaid ด้วย optional owner-set runtime override implemented โดย `Infrastructure.Features.FeatureGate` caches overrides briefly (`FeatureSettings.OverrideCacheTtl`) invalidates บน change

runtime overrides stored เป็น `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`) ไม่มี row = "ใช้ config baseline"

## สองวิธี เพื่อ disable feature

### 1. Deployment configuration (baseline)

ตั้ง flag `false` ภายใต้ `App:Features` example `appsettings.json`:

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

หรือ ผ่าน env vars (double underscore):

```
App__Features__CopyTrading=false
```

baseline gates **startup registration** ของ background workers (`Nodes.AddNodes`) และ MCP tools (`Mcp` server) ดังนั้น feature disabled ใน config ไม่เคย start hosted services ของมัน หรือ expose MCP tools ของมัน

### 2. Runtime override (owner)

owner สามารถ flip any feature live จาก **Settings → Features** (`/settings/features`) หรือ API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> ตั้ง override             (owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (owner)
```

runtime changes take effect immediately สำหรับ request-time gates (navigation API) background workers และ MCP tools gated ที่ startup pick ขึ้น runtime เปลี่ยน บน next process restart

## สิ่งที่ gate enforces

| Layer | Mechanism | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` เมื่อ disabled | runtime |
| navigation | `NavMenu` hides links ผ่าน `IFeatureGate.IsEnabled` | runtime |
| background workers | conditional `AddHostedService` ใน `Nodes.AddNodes` | startup (config) |
| MCP tools | conditional `WithTools<>` ใน MCP server | startup (config) |

feature reached โดย deep link ขณะ disabled renders empty page — API ของมัน returns `404`; nav ไม่มา longer surfaces มัน

## flag → surface map

| flag | API groups | nav | workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots` `/api/paramsets` `/api/builder` | cbots group → cbots (param sets per-cbot dialog) | MCP `CBotTools` |
| Backtesting | (shares `/api/instances`) | cbots group → backtest | — |
| Execution | `/api/instances` | cbots group → run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | copy trading | `CopyEngineSupervisor` `OpenApiTokenRefreshService` MCP `CopyTools` |
| Ai | `/api/ai` | AI group → AI; settings → AI (key) | `AiRiskGuard` MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI group → portfolio agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI group → alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | prop group → prop guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | prop group → challenges | — |
| Accounts | `/api/ctids` | trading accounts | — |
| OpenApi | `/api/openapi` | settings → open API | — |
| Mcp | `/api/mcp-keys` | AI group → MCP keys | — |
| Compliance | `/api/compliance` | settings → legal & privacy | — |

## Tests

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline defaults per-flag mapping
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: config baseline runtime override beats config และ persists เป็น `AppSetting` clearing reverts เป็น baseline (real postgres)
- **E2E** — `E2ETests/FeatureToggleTests.cs`: disabling `CopyTrading` ที่ runtime hides nav link ของมัน และ `404`s `/api/copy` re-enabling restores ทั้งสอง
