# AI copy-profile recommender

AI helper. Recommend safe copy-trading destination settings from follower risk profile + source (master) account description. Exposed over REST API, MCP tool, Copy Trading page. Advisory only — never create/mutate profile; human (or follow-up MCP call) apply settings.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — build request from
  `AiPrompts.CopyProfileSystem` prompt, return `AiResult` whose text = JSON object of suggested
  settings: `riskMode` (a `MoneyManagementMode` name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, short `rationale`.
- Like every AI feature, gated on `App:Ai:ApiKey`: no key → call return
  `AiResult.Fail(disabled)`, app unaffected.

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delegates to the AI service) |
| UI | Copy Trading page → **AI suggest** button; the recommendation renders in an inline alert |

Recommendation not auto-applied on purpose: follower reviews, then creates profile /
destination through normal Copy Trading dialog (or MCP client parses JSON + calls create
endpoints).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  forwarded to AI client under copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: no API key → real
  `AnthropicAiClient` + `AiFeatureService` degrade to failure result (app runs without key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI suggest** button calls endpoint + renders
  result (graceful "not configured" message in test env), proving UI → endpoint → AI path.