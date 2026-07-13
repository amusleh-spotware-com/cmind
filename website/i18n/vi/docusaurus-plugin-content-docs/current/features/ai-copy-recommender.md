---
description: "AI helper. Recommend safe copy-trading destination settings từ follower risk profile + source (master) account description. Exposed over REST API, MCP…"
---

# AI copy-profile recommender

AI helper. Recommend safe copy-trading destination settings từ follower risk profile + source (master) account description. Exposed over REST API, MCP tool, Copy Trading page. Advisory only — không bao giờ create/mutate profile; human (hoặc follow-up MCP call) apply settings.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — build request from
  `AiPrompts.CopyProfileSystem` prompt, return `AiResult` whose text = JSON object của suggested
  settings: `riskMode` (một `MoneyManagementMode` name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, short `rationale`.
- Giống mọi AI feature, gated on `App:Ai:ApiKey`: không có key → call return
  `AiResult.Fail(disabled)`, app không bị ảnh hưởng.

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delegates to AI service) |
| UI | Copy Trading page → **AI suggest** button; recommendation renders in an inline alert |

Recommendation not auto-applied on purpose: follower reviews, rồi create profile /
destination through normal Copy Trading dialog (hoặc MCP client parses JSON + calls create
endpoints).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  forwarded to AI client under copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: không có API key → real
  `AnthropicAiClient` + `AiFeatureService` degrade to failure result (app runs without key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI suggest** button calls endpoint + renders
  result (graceful "not configured" message in test env), proving UI → endpoint → AI path.
