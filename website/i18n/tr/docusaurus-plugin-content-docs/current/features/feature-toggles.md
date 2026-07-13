---
description: "White-label dağıtımlar nadiren her yeteneği gönderir. Özellik anahtarları, operatörün ana ürün özelliklerini açıp kapatmasına izin verir — dağıtım zamanında yapılandırmayla veya daha sonra…"
---

# Özellik anahtarları

White-label dağıtımlar nadiren her yeteneği gönderir. Özellik anahtarları, operatörün ana
ürün özelliklerini açıp kapatmasına izin verir — dağıtım zamanında yapılandırmayla veya daha sonra
çalışma zamanında, yeniden dağıtım olmadan. **Tüm özellikler varsayılan olarak etkindir**; dağıtım
yalnızca değiştirdiklerini listeler.

## Model

- `Core.Features.FeatureFlag` — kapılabilir özelliklerin enum'ı: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Çekirdek yönetici
  yüzeyleri (pano, kullanıcılar, düğümler, kimlik doğrulama) asla kapılamaz, burada değil.
- `Core.Options.FeaturesOptions` — `App:Features`'tan bağlanan yapılandırma temeli. Her özellik
  varsayılan olarak `true`.
- `Core.Features.IFeatureGate` — **etkin** durumu çözer: yapılandırma temeli, isteğe bağlı sahip-ayarlı
  çalışma zamanı geçersiz kılmasıyla kaplanır. `Infrastructure.Features.FeatureGate` tarafından uygulanır,
  geçersiz kılmaları kısa süre önbelleğe alır (`FeatureSettings.OverrideCacheTtl`), değişiklikte geçersiz kılar.

Çalışma zamanı geçersiz kılmaları `feature.<FeatureFlag>` anahtarlı `AppSetting` satırları olarak saklanır
(değer `true`/`false`). Satır yok = "yapılandırma temelini kullan".

## Bir özelliği devre dışı bırakmanın iki yolu

### 1. Dağıtım yapılandırması (temel)

`App:Features` altında bayrağı `false` yapın. Örnek `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

Veya env değişkenleri aracılığıyla (çift alt çizgi):

```
App__Features__CopyTrading=false
```

Temel, arka plan çalışanlarının (`Nodes.AddNodes`) ve MCP araçlarının (`Mcp` sunucusu) **başlangıç
kaydını** kapılar, böylece yapılandırmada devre dışı bırakılan özellik ne barındırılan hizmetlerini
başlatır ne de MCP araçlarını açığa çıkarır.

### 2. Çalışma zamanı geçersiz kılma (sahip)

Sahip, herhangi bir özelliği **Settings → Features** (`/settings/features`) veya API'den canlı olarak değiştirebilir:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Sahip)
PUT  /api/features/{flag}      body { "enabled": false }  -> geçersiz kılmayı ayarla   (Sahip)
PUT  /api/features/{flag}      body { "enabled": null  }  -> geçersiz kılmayı temizle   (Sahip)
```

Çalışma zamanı değişiklikleri istek-zamanı kapıları (gezinme, API) için hemen etkili olur. Arka plan
çalışanları ve MCP araçları başlangıçta kapılanır, çalışma zamanı değişikliğini bir sonraki işlem
yeniden başlatmasında alır.

## Her kapı neyi zorunlu kılar

| Katman | Mekanizma | Zamanlama |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` uç nokta filtresi → devre dışıyken `404` | Çalışma zamanı |
| Gezinme | `NavMenu`, `IFeatureGate.IsEnabled` aracılığıyla bağlantıları gizler | Çalışma zamanı |
| Arka plan çalışanları | `Nodes.AddNodes`'ta koşullu `AddHostedService` | Başlangıç (yapılandırma) |
| MCP araçları | MCP sunucusunda koşullu `WithTools<>` | Başlangıç (yapılandırma) |

Devre dışıyken derin bağlantıyla ulaşılan özellik boş sayfa oluşturur — API'si `404` döndürür;
gezinme artık onu göstermez.

## Bayrak → yüzey haritası

| Bayrak | API grupları | Gezinme | Çalışanlar / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots grubu → cBots (cBot başına param setleri iletişim kutusu) | MCP `CBotTools` |
| Backtesting | (`/api/instances` paylaşır) | cBots grubu → Backtest | — |
| Execution | `/api/instances` | cBots grubu → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI grubu → AI; Settings → AI (anahtar) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI grubu → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI grubu → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop grubu → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop grubu → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI grubu → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testler

- **Birim** — `UnitTests/Features/FeaturesOptionsTests.cs`: temel varsayılanlar, bayrak başına eşleme.
- **Entegrasyon** — `IntegrationTests/FeatureGateTests.cs`: yapılandırma temeli, çalışma zamanı geçersiz
  kılma yapılandırmayı yener ve `AppSetting` olarak kalıcılaşır, temizleme temele döner (gerçek Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: `CopyTrading`'i çalışma zamanında devre dışı bırakmak nav
  bağlantısını gizler ve `/api/copy`'yi `404`'ler, yeniden etkinleştirmek her ikisini de geri yükler.
