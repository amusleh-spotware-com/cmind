---
description: "A fehér címkés telepítések ritkán szállítanak minden képességet. A funkcióváltógombok lehetővé teszik az üzemeltetőnek a fő terméképületeket ki/bekapcsolni — telepítési időpontban a config-on keresztül, vagy később futásidőben, nincs újratelepítés. **Minden funkció alapértelmezettként engedélyezett**; a telepítés csak azokat listázza, amelyeket megváltoztat."
---

# Funkcióváltógombok

A fehér címkés telepítések ritkán szállítanak minden képességet. A funkcióváltógombok lehetővé teszik az üzemeltetőnek a fő terméképületeket ki/bekapcsolni — telepítési időpontban a config-on keresztül, vagy később futásidőben, nincs újratelepítés. **Minden funkció alapértelmezettként engedélyezett**; a telepítés csak azokat listázza, amelyeket megváltoztat.

## Modell

- `Core.Features.FeatureFlag` — felhasználható funkciók felsorolása: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Mag adminisztrációs felületek (irányítópult, felhasználók, csomópontok, hitelesítés) soha nem felhasználható, nem itt.
- `Core.Options.FeaturesOptions` — config alap, az `App:Features` feltöltésből. Minden tulajdon alapértelmezettként `true`.
- `Core.Features.IFeatureGate` — az **effektív** állapot feloldása: config alap az opcionális tulajdonos-beállítási futásidejű felülírás felett. Az `Infrastructure.Features.FeatureGate` implementálta, gyorsan gyorsítótárazható felülírások (`FeatureSettings.OverrideCacheTtl`), az változás megadása.

A futásidejű felülírások az `AppSetting` sorokként tárolódnak, az `feature.<FeatureFlag>` kulcs szerint (érték `true`/`false`). Nincs sor = "config alap használata".

## Két módja a funkció letiltásának

### 1. Telepítési konfiguráció (alap)

Állítsa a zásteru `false` értéket az `App:Features` alatt. Például `appsettings.json`:

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

Vagy a környezeti változókon keresztül (kettős aláhúzás):

```
App__Features__CopyTrading=false
```

Az alap kapuk **indítási regisztrációja** a háttérben lévő munkavállaló (`Nodes.AddNodes`) és az MCP-eszközök (`Mcp` szerver), így a config-ban letiltott funkció soha nem indítja el az üzemeltetett szolgáltatásokat, sem nem tesz elérhetővé az MCP-eszközöket.

### 2. Futásidejű felülírás (tulajdonos)

A tulajdonos bármelyik funkciót élőben átválthatja a **Beállítások → Funkciók** (`/settings/features`) vagy API-ból:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Tulajdonos)
PUT  /api/features/{flag}      body { "enabled": false }  -> beállítási felülírás    (Tulajdonos)
PUT  /api/features/{flag}      body { "enabled": null  }  -> felülírás törlése (visszafordítás) (Tulajdonos)
```

A futásidejű változások azonnal hatályba lépnek a kérés-idejű kapu esetén (navigáció, API). A háttérben lévő munkavállaló és az MCP-eszközök az indításkor vannak megnyitva, a következő folyamat-újraindításon vegyen fel futásidejű módosítást.

## Mit kényszerít minden kapu

| Réteg | Mechanizmus | Időzítés |
|-------|-----------|---------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` végpont-szűrő → `404` letiltáskor | Futásidő |
| Navigáció | `NavMenu` elrejtési hivatkozások az `IFeatureGate.IsEnabled` keresztül | Futásidő |
| Háttérben lévő munkavállaló | feltételes `AddHostedService` a `Nodes.AddNodes`-ben | Indítás (config) |
| MCP-eszközök | feltételes `WithTools<>` az MCP szerver-ben | Indítás (config) |

Funkció eléri mély hivatkozási, míg letiltott végez üres oldal — az API `404`-t ad vissza; a nav többé nem teszi elérhetővé.

## Zásteru → felület térkép

| Zaszteru | API-csoportok | Nav | Munkavállaló / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots csoport → cBots (param készletek per-cBot párbeszédablak) | MCP `CBotTools` |
| Backtesting | (megosztja `/api/instances`) | cBots csoport → Backtest | — |
| Execution | `/api/instances` | cBots csoport → Futtatás | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Másolási kereskedés | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI csoport → AI; Beállítások → AI (kulcs) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI csoport → Portfolio ügynök | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI csoport → Riasztások | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop csoport → Prop őr | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop csoport → Kihívások | — |
| Accounts | `/api/ctids` | Kereskedelmi számolatok | — |
| OpenApi | `/api/openapi` | Beállítások → Open API | — |
| Mcp | `/api/mcp-keys` | AI csoport → MCP Kulcsok | — |
| Compliance | `/api/compliance` | Beállítások → Jogi & Adatvédelem | — |

## Tesztek

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: alap alapértelmezés, per-zaszteru leképezés.
- **Integráció** — `IntegrationTests/FeatureGateTests.cs`: config alap, futásidejű felülírás versenyfutás a config-tal és marad az `AppSetting` mint, az törlés visszafordítása az alapra (valódi Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: a `CopyTrading` letiltása futásidőben elrejtse az nav hivatkozást és a `404`s `/api/copy`, az újra-engedélyezés helyreállítja mindkettőt.
