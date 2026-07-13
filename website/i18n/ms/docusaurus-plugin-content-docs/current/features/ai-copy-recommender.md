---
description: "Pembantu AI. Mengesyorkan tetapan destinasi salinan perdagangan yang selamat daripada profil risiko pengikut + penerangan akaun sumber (master). Didedahkan melalui REST API, MCP…"
---

# Pengesyok profil salinan AI

Pembantu AI. Mengesyorkan tetapan destinasi salinan perdagangan yang selamat daripada profil risiko pengikut + penerangan akaun sumber (master). Didedahkan melalui REST API, alat MCP, halaman Salinan Perdagangan. Nasihat sahaja — tidak pernah buat/mutus profil; manusia (atau panggilan MCP susulan) menggunakan tetapan.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — bina permintaan daripada
  prompt `AiPrompts.CopyProfileSystem`, kembalikan `AiResult` yang teksnya = objek JSON tetapan yang dicadangkan:
  `riskMode` (nama `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, `rationale` ringkas.
- Seperti setiap ciri AI, dikawal oleh `App:Ai:ApiKey`: tiada kunci → panggilan kembalikan
  `AiResult.Fail(disabled)`, apl tidak terjejas.

## Permukaan

| Permukaan | Entri |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (ciri `Ai`, peran User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (ciri `CopyTrading`, delegat ke perkhidmatan AI) |
| UI | Halaman Salinan Perdagangan → butang **AI suggest**; cadangan dipaparkan dalam alert inline |

Cadangan tidak digunakan secara automatik dengan sengaja: pengikut semak, kemudian buat profil /
destinasi melalui dialog Salinan Perdagangan biasa (atau klien MCP parse JSON + panggilan cipta
tamat).

## Ujian

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: profil risiko + penerangan sumber
  diagihkan ke klien AI di bawah prompt sistem copy-profile (NSubstitute).
- **Integrasi** — `IntegrationTests/AiRecommendDisabledTests.cs`: tiada kunci API → `AnthropicAiClient` sebenar
  + `AiFeatureService` mundur ke hasil kegagalan (apl berjalan tanpa kunci).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: butang **AI suggest** memanggil endpoint + papar
  hasil (mesej "tidak dikonfigurasi" baik dalam env ujian), membuktikan laluan UI → endpoint → AI.
