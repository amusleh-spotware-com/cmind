---
description: "AI pomocník. Doporučuje bezpečná nastavení copy-trading cíle z risk profilu followera + popisu zdrojového (master) účtu. Vystaveno přes REST API, MCP tool, stránku Copy Trading."
---

# AI doporučovač copy-profile

AI pomocník. Doporučuje bezpečná nastavení copy-trading cíle z risk profilu followera + popisu zdrojového (master) účtu. Vystaveno přes REST API, MCP tool, stránku Copy Trading. Pouze advisory — nikdy nevytváří/nemodifikuje profil; člověk (nebo následný MCP volání) aplikuje nastavení.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — sestaví požadavek z promptu `AiPrompts.CopyProfileSystem`, vrátí `AiResult`, jehož text = JSON objekt doporučených nastavení: `riskMode` (název `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`, `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, krátké `rationale`.
- Jako každá AI funkce, závislá na `App:Ai:ApiKey`: bez klíče → volání vrátí `AiResult.Fail(disabled)`, aplikace nedotčena.

## Plochý povrch

| Plochý | Vstup |
|--------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (funkce `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (funkce `CopyTrading`, deleguje na AI službu) |
| UI | Stránka Copy Trading → tlačítko **AI suggest**; doporučení se zobrazuje v inline alertu |

Doporučení není záměrně auto-aplikováno: follower zkontroluje, pak vytvoří profil / cíl přes normální Copy Trading dialog (nebo MCP klient parsuje JSON + volá create endpointy).

## Testy

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profil + popis zdroje forwardován AI klientovi pod copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: bez API klíče → skutečný `AnthropicAiClient` + `AiFeatureService` degraduje na selhávající výsledek (aplikace běží bez klíče).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: tlačítko **AI suggest** volá endpoint + renderuje výsledek (graceful zpráva "not configured" v testovacím prostředí), dokazuje cestu UI → endpoint → AI.
