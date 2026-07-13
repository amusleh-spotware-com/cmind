---
title: Kesin DDD, Saf Core
---

# Kesin Domain-Driven Design, Saf `Core`

**Bağlam:** cMind gerçek para taşıyor. Hatalı iş mantığı kullanıcı sermayesine maliyetli olabilir. Kod değişikliklerinin sonuçları ayarlamak için iş kurallarının açık, testlenebilir, altyapıdan izole olması gerekir.

**Karar:** Kesin Domain-Driven Design uygulayın:

- `src/Core` saf etki alanı: varlıklar, agregalar, değer nesneleri, güçlü tipi ID'ler, etki alanı olayları, Core tarafı arayüzler.
- **Sıfır altyapı bağımlılıkları** `Core` içinde: EF, HttpClient, Docker, ASP.NET yok.
- Tüm iş mantığı agregalar ve domain hizmetleri üzerinde yaşar.
- Uç noktalar ve UI bileşenleri yalnızca orkestrasyonu yapır.

**Sonuçlar:**

✅ **Düşük riski yeniden**: Domain mantığı eğik etmek eski temel ek değiştirme ihtiyaç değil.

✅ **Testlenebilir:** Ağ, veritabanı veya kütüphaneler olmadan birim testleri çalışır; dış bağımlılıklar tehdit değildir.

✅ **Saklı alan kalıp'ı:** Etki alanı değişiklikleri (yeni özellik, bulgu düzeltme) `Core` ile başlar, ardından bitiş noktası katmanını aşama.

❌ **Yavaş önerici başvuru:** Herşeyi yapı bloğa inşa etmek yavaş başlangıçtır, ancak değişim süresi kısalır.

Uygulama: [src/Core/CLAUDE.md →](https://github.com/amusleh-spotware-com/cmind/blob/main/src/Core/CLAUDE.md) · [ddd-dotnet ↗ yetenek](https://example.com)
