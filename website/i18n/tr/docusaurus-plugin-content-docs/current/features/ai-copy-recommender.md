---
description: "Yapay zekâ yardımcısı. Takipçi risk profili + kaynak (master) hesap açıklamasından güvenli kopya-işlem hedef ayarları önerir. REST API, MCP…"
---

# Yapay zekâ kopya-profili önericisi

Yapay zekâ yardımcısı. Takipçi risk profili + kaynak (master) hesap açıklamasından güvenli kopya-işlem hedef ayarları önerir. REST API, MCP aracı ve Kopya İşlem sayfası üzerinden sunulur. Yalnızca tavsiye niteliğindedir — asla profil oluşturmaz/değiştirmez; ayarları bir insan (veya sonraki bir MCP çağrısı) uygular.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — `AiPrompts.CopyProfileSystem`
  komut isteminden istek oluşturur ve metni önerilen ayarların JSON nesnesi olan bir `AiResult` döndürür:
  `riskMode` (bir `MoneyManagementMode` adı), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips` ve kısa bir `rationale`.
- Her yapay zekâ özelliği gibi `App:Ai:ApiKey` ile kapılıdır: anahtar yoksa çağrı
  `AiResult.Fail(disabled)` döndürür, uygulama etkilenmez.

## Yüzeyler

| Yüzey | Giriş |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (özellik `Ai`, rol User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (özellik `CopyTrading`, yapay zekâ servisine devreder) |
| UI | Kopya İşlem sayfası → **AI öner** düğmesi; öneri satır içi bir uyarıda görüntülenir |

Öneri bilinçli olarak otomatik uygulanmaz: takipçi inceler, ardından normal Kopya İşlem iletişim
kutusuyla profili / hedefi oluşturur (veya MCP istemcisi JSON'u ayrıştırıp oluşturma uç noktalarını çağırır).

## Testler

- **Birim** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profili + kaynak açıklaması
  kopya-profili sistem komutu altında yapay zekâ istemcisine iletilir (NSubstitute).
- **Entegrasyon** — `IntegrationTests/AiRecommendDisabledTests.cs`: API anahtarı yok → gerçek
  `AnthropicAiClient` + `AiFeatureService` başarısızlık sonucuna zarifçe iner (uygulama anahtarsız çalışır).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI öner** düğmesi uç noktayı çağırır + sonucu
  görüntüler (test ortamında zarif "yapılandırılmadı" mesajı), UI → uç nokta → yapay zekâ yolunu kanıtlar.
