---
description: "AI helper. Doporučuje bezpečná copy-trading destination nastavení z follower risk profilu + source (master) účtu. Vystaveno přes REST API, MCP tool, Copy Trading page. Pouze advisory — nikdy nevytváří/nemutuje profil; human (nebo follow-up MCP call) aplikuje nastavení."
---

# AI copy-profile doporučovač

AI helper. Doporučuje bezpečná copy-trading destination nastavení z follower risk profilu + source (master) účtu. Vystaveno přes REST API, MCP tool, Copy Trading page. Pouze advisory — nikdy nevytváří/nemutuje profil; human (nebo follow-up MCP call) aplikuje nastavení.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — build request from
  `AiPrompts.CopyProfileSystem` prompt, return `AiResult` whose text = JSON object of suggested
  settings: `riskMode` (a `MoneyManagementMode` name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, short `rationale`.
- Jako každá AI funkce, gated on `App:Ai:ApiKey`: no key → call return
  `AiResult.Fail(disabled)`, app unaffected.

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delegates to the AI service) |
| UI | Copy Trading page → **AI suggest** button; doporučení se renderuje v inline alert |

Doporučení není auto-aplikováno záměrně: follower review, pak creates profile /
destination through normal Copy Trading dialog (nebo MCP client parses JSON + calls create
endpoints).

## Testy

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  forwarded to AI client under copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: no API key → real
  `AnthropicAiClient` + `AiFeatureService` degraduje to failure result (app runs without key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI suggest** button calls endpoint + renders
  result (graceful "not configured" message in test env), proving UI → endpoint → AI path.
