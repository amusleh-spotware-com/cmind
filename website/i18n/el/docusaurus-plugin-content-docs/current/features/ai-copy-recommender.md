---
description: "AI helper. Recommend safe copy-trading destination settings from follower risk profile + source (master) account description. Exposed over REST API, MCP…"
---

# AI copy-profile recommender

AI helper. Recommend safe copy-trading destination settings από follower risk profile + source (master) account description. Exposed πάνω από REST API, MCP tool, Copy Trading page. Advisory μόνο — ποτέ δεν δημιουργεί/mutate profile; ο χρήστης (ή follow-up MCP call) εφαρμόζει settings.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — κατασκευάστε request από
  `AiPrompts.CopyProfileSystem` prompt, επιστρέψτε `AiResult` του οποίου το text = JSON object των suggested
  settings: `riskMode` (ένα `MoneyManagementMode` name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, short `rationale`.
- Όπως κάθε AI feature, gated σε `App:Ai:ApiKey`: χωρίς key → κλήση return
  `AiResult.Fail(disabled)`, app unaffected.

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delegates στο AI service) |
| UI | Copy Trading page → **AI suggest** button; η recommendation renders σε inline alert |

Recommendation δεν auto-applied σκοπίμως: ο follower reviews, τότε δημιουργεί profile /
destination μέσω normal Copy Trading dialog (ή MCP client αναλύει JSON + καλεί create
endpoints).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  forwarded στο AI client κάτω από copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: χωρίς API key → πραγματικό
  `AnthropicAiClient` + `AiFeatureService` degrade σε failure result (app τρέχει χωρίς key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI suggest** button καλεί endpoint + renders
  result (graceful "not configured" message σε test env), proving UI → endpoint → AI path.
