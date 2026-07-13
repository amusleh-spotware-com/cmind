---
title: AI İstemcisi Ham HTTP Kullanır, SDK Değil
---

# Anthropic'i Ham HTTP Üzerinden Çağırın, SDK Değil

**Bağlam:** Anthropic SDK bağımlılığı ekler, sürümleri başka kütüphanelerle çakışabilir, temelleri iptal eder. Metin/JSON istekleriyle, HTTP çağrıları tarafından çalışan Endpoint'ler basitçe.

**Karar:** `IAiClient` yazılı bir `HttpClient` yoluyla Anthropic'i çağırır, SDK'sını değil.

- İstek: JSON gövdesi ile POST
- Yanıt: Doğrudan JSON ayrıştırma
- Yeniden denemeler: Polly politikası

**Sonuçlar:**

✅ **Minimal Bağımlılık:** SDK sürüm sorunları olmaz.

✅ **Hızlı:** Doğrudan Malibu; kütüphane yükleme yok.

❌ **Sürüm Kopya:** API değişiklikleri el ile izlenmesi gerekir.

❌ **Hata İşleme:** SDK, HTTP hatalarını onu niceliklerir; biz elle tutmalı.

Uygulama: [src/Infrastructure/Ai/AnthropicClient.cs →](https://github.com/amusleh-spotware-com/cmind/blob/main/src/Infrastructure/Ai/AnthropicClient.cs)
