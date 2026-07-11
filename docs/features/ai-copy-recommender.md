# AI copy-profile recommender

An AI helper that recommends safe copy-trading destination settings from a follower's stated risk profile
and a description of the source (master) account. Exposed over the REST API, an MCP tool, and the Copy
Trading page. Advisory only — it never creates or mutates a profile; the human (or a follow-up MCP call)
applies the settings.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — builds the request from
  the `AiPrompts.CopyProfileSystem` prompt and returns an `AiResult` whose text is a JSON object of suggested
  settings: `riskMode` (a `MoneyManagementMode` name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, and a short `rationale`.
- Like every AI feature, it is gated on `App:Ai:ApiKey`: with no key the call returns
  `AiResult.Fail(disabled)` and the app is unaffected.

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delegates to the AI service) |
| UI | Copy Trading page → **AI suggest** button; the recommendation renders in an inline alert |

The recommendation is intentionally not auto-applied: a follower reviews it, then creates a profile /
destination through the normal Copy Trading dialog (or an MCP client parses the JSON and calls the create
endpoints).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: the risk profile and source description are
  forwarded to the AI client under the copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: with no API key the real
  `AnthropicAiClient` + `AiFeatureService` degrade to a failure result (the app runs without a key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: the **AI suggest** button calls the endpoint and renders the
  result (the graceful "not configured" message in the test environment), proving the UI → endpoint → AI path.
