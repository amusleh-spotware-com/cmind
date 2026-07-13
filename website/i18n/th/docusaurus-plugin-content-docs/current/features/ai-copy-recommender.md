---
description: "AI helper. Recommend safe copy-trading destination settings from follower risk profile + source (master) account description. Exposed over REST API, MCP…"
---

# AI copy-profile recommender

AI helper ให้คำแนะนำการตั้งค่า copy-trading destination ที่ปลอดภัยจาก follower risk profile + source (master) account description เปิดให้ใช้ผ่าน REST API MCP tool Copy Trading page เป็นเพียงคำแนะนำเท่านั้น — ไม่ สร้าง/mutate profile; human (หรือ follow-up MCP call) ใช้การตั้งค่า

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — สร้าง request จาก
  `AiPrompts.CopyProfileSystem` prompt return `AiResult` ที่ text = JSON object ของ suggested
  settings: `riskMode` (a `MoneyManagementMode` name) `riskParameter` `maxDrawdownPercent` `dailyLossLimit`
  `direction` `copyStopLoss` `copyTakeProfit` `slippagePips` short `rationale`
- เช่นทุก AI feature gated on `App:Ai:ApiKey`: ไม่มี key → call return
  `AiResult.Fail(disabled)` app ไม่ได้รับผลกระทบ

## Surfaces

| Surface | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai` role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading` delegates to the AI service) |
| UI | Copy Trading page → **AI suggest** button; recommendation renders ใน inline alert |

คำแนะนำไม่ auto-applied by purpose: follower reviews จากนั้น สร้าง profile /
destination ผ่าน normal Copy Trading dialog (หรือ MCP client parses JSON + calls create
endpoints)

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  forwarded ไปยัง AI client ภายใต้ copy-profile system prompt (NSubstitute)
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: ไม่มี API key → real
  `AnthropicAiClient` + `AiFeatureService` degrade เป็น failure result (app runs ไม่มี key)
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI suggest** button calls endpoint + renders
  result (graceful "not configured" message ใน test env) proving UI → endpoint → AI path
