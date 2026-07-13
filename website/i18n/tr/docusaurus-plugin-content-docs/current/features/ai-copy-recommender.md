---
description: "AI yardımcı. Takipçi risk profili + kaynak (ana) hesap açıklamasından güvenli kopyalama hedefine erişkin ayarları öner. REST API, MCP…"
---

# AI kopya profili tavsiyeci

AI yardımcı. Takipçi risk profili + kaynak (ana) hesap açıklamasından güvenli kopyalama hedefine erişkin ayarları öner. REST API, MCP aracı, Kopya Alım sayfası aracılığıyla ortaya çıktı. Yalnızca danışma — asla profil oluştur/mutasyon yap; insan (veya takip MCP çağrısı) ayarları uygula.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — `AiPrompts.CopyProfileSystem` istemiyle istekle, metin = önerilen ayarların JSON nesnesinin `AiResult` döndür: `riskMode` (bir `MoneyManagementMode` adı), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`, `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, kısa `rationale`.
- Her AI özelliği gibi, `App:Ai:ApiKey` üzerinde kapılı: anahtar yok → çağrı dön `AiResult.Fail(disabled)`, uygulama etkilenmeden.

## Yüzeyler

| Yüzey | Giriş |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (özellik `Ai`, rol User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (özellik `CopyTrading`, AI hizmetine devreder) |
| UI | Kopya Alım sayfası → **AI öner** düğmesi; öneri satır içi uyarıda oluşturulur |

Öneri amaçlı otomatik uygulanmaz: takipçi inceler, ardından normal Kopya Alım iletişim (veya MCP müşterisi JSON ayrıştırır + uç noktaları oluştur çağrısı) aracılığıyla profil / hedef oluşturur.

## Testler

- **Birim** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profili + kaynak açıklaması kopyalama profili sistem istemi altında AI müşterisine iletilir (NSubstitute).
- **Entegrasyon** — `IntegrationTests/AiRecommendDisabledTests.cs`: API anahtarı yok → gerçek `AnthropicAiClient` + `AiFeatureService` hata sonucuna bozunur (uygulama anahtarsız çalışır).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI öner** düğmesi çağrı uç noktası + sonuç oluşturulur (test env'de zarif "yapılandırılmadı" mesajı), UI → uç noktası → AI yolunu kanıtlayarak.
