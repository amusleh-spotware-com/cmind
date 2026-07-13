---
description: "White-label namestitev redko ladij vsako funkcijo. Funkcijske zastavice omogočajo operaterju vključiti/izključiti glavne funkcije produkta — ob namestitvi prek konfiguracije, ali kasneje med izvajanjem, brez…"
---

# Funkcijske zastavice

White-label namestitev redko ladij vsako funkcijo. Funkcijske zastavice omogočajo operaterju vključiti/izključiti glavne
funkcije produkta — ob namestitvi prek konfiguracije, ali kasneje med
izvajanjem, brez ponovnega uvajanja. **Vse funkcije so privzeto omogočene**; namestitev navede samo tiste ki jih spreminja.

## Model

- `Core.Features.FeatureFlag` — enum funkcij ki jih je mogoče vključiti: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Corey admin
  površine (nadzorna plošča, uporabniki, vozlišča, avtentikacija) nikoli ni mogoče vključiti, niso tukaj.
- `Core.Options.FeaturesOptions` — konfiguracijska osnova, vezana iz `App:Features`. Vsaka lastnost
  privzeto `true`.
- `Core.Features.IFeatureGate` — razrešuje **efektivno** stanje: konfiguracijska osnova prelita
  z izbirnim lastnik-setvim runtime prevzemom. Implementiran od `Infrastructure.Features.FeatureGate`,
  predpomni prevzete kratko (`FeatureSettings.OverrideCacheTtl`), invalidira ob spremembi.

Runtime prevzemi shranjeni kot `AppSetting` vrstice ključane `feature.<FeatureFlag>` (vrednost `true`/`false`).
Brez vrstice = "uporabi konfiguracijsko osnovno".

## Dva načina za onemogočanje funkcije

### 1. Konfiguracija namestitve (osnova)

Nastavi zastavico na `false` pod `App:Features`. Primer `appsettings.json`:

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

Ali prek spremenljivk okolja (dvojni podčrtaj):

```
App__Features__CopyTrading=false
```

Osnova vrat **startup registracija** ozadnjih delavcev (`Nodes.AddNodes`) in MCP orodij
(`Mcp` strežnik), torej funkcija onemogočena v konfiguraciji nikoli ne zažene svojih gostovanih
storitev niti ne razkriva svojih MCP orodij.

### 2. Runtime prevzem (lastnik)

Lastnik lahko preklopi katerokoli funkcijo v živo iz **Settings → Features** (`/settings/features`) ali API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Lastnik)
PUT  /api/features/{flag}      body { "enabled": false }  -> nastavi prevzem             (Lastnik)
PUT  /api/features/{flag}      body { "enabled": null  }  -> počisti prevzem (povrni)  (Lastnik)
```

Runtime spremembe učinkujejo takoj za vrata ob času zahteve (navigacija, API). Ozadnji
delavci in MCP orodja vključena ob startu, poberejo runtime spremembo ob naslednjem procesu restart.

## Kaj vsako vrata uveljavljajo

| Plast | Mehanizem | Čas |
|-------|-----------|-----|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` filter končne točke → `404` ko onemogočeno | Runtime |
| Navigacija | `NavMenu` skrije povezave prek `IFeatureGate.IsEnabled` | Runtime |
| Ozadnji delavci | pogojni `AddHostedService` v `Nodes.AddNodes` | Startup (konfiguracija) |
| MCP orodja | pogojni `WithTools<>` v MCP strežniku | Startup (konfiguracija) |

Funkcija dosegljiva z globinsko povezavo medtem ko onemogočena upodobi prazno stran — njena API vrne `404`;
nav je več ne površinsko.

## Preslikava zastavica → površina

| Zastavica | Skupine API | Nav | Delavci / MCP |
|-----------|-------------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots skupina → cBots (param sets per-cBot dialog) | MCP `CBotTools` |
| Backtesting | (deli `/api/instances`) | cBots skupina → Backtest | — |
| Execution | `/api/instances` | cBots skupina → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI skupina → AI; Settings → AI (ključ) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI skupina → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI skupina → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop skupina → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop skupina → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI skupina → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testi

- **Enote** — `UnitTests/Features/FeaturesOptionsTests.cs`: osnovni privzeti, preslikava na zastavico.
- **Integracija** — `IntegrationTests/FeatureGateTests.cs`: konfiguracijska osnova, runtime prevzem premaga
  konfiguracijo in vztraja kot `AppSetting`, čiščenje povrne v osnovo (resničen Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: onemogočanje `CopyTrading` med izvajanjem skrije njegovo nav povezavo in
  `404`s `/api/copy`, ponovno omogočanje obnovi oboje.
