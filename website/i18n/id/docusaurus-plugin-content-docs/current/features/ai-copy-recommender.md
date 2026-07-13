---
description: "AI helper. Rekomendasikan pengaturan tujuan copy-trading yang aman dari profil risiko follower + deskripsi akun sumber (master). Diekspos عبر REST API, MCP…"
---

# AI copy-profile recommender

AI helper. Rekomendasikan pengaturan tujuan copy-trading yang aman dari profil risiko follower +
deskripsi akun sumber (master). Diekspos melalui REST API, MCP tool, halaman Copy Trading. Advisory
saja — tidak pernah membuat/memutasi profil; manusia (atau follow-up MCP call) menerapkan pengaturan.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — membangun request
  dari prompt `AiPrompts.CopyProfileSystem`, mengembalikan `AiResult` yang teksnya = objek JSON pengaturan
  yang disarankan: `riskMode` (nama `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`,
  `dailyLossLimit`, `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, `rationale` pendek.
- Seperti setiap fitur AI, gate pada `App:Ai:ApiKey`: tanpa key → panggilan mengembalikan
  `AiResult.Fail(disabled)`, aplikasi tidak terpengaruh.

## Permukaan

| Permukaan | Entri |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (fitur `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (fitur `CopyTrading`, mendelegasikan ke AI service) |
| UI | Halaman Copy Trading → tombol **AI suggest**; rekomendasi render di inline alert |

Rekomendasi tidak auto-diterapkan dengan sengaja: follower meninjau, lalu membuat profil / tujuan
melalui dialog Copy Trading normal (atau MCP client mem-parse JSON + memanggil endpoint create).

## Tes

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: profil risiko + deskripsi sumber
  diteruskan ke AI client di bawah copy-profile system prompt (NSubstitute).
- **Integrasi** — `IntegrationTests/AiRecommendDisabledTests.cs`: tanpa API key → `AnthropicAiClient`
  nyata + `AiFeatureService` menurun ke failure result (aplikasi berjalan tanpa key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: tombol **AI suggest** memanggil endpoint + render
  result (pesan "not configured" yang graceful di test env), membuktikan path UI → endpoint → AI.
