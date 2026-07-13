---
title: CBotBuilder Web Ana Bilgisayarında Çalışır
---

# `CBotBuilder` Web Ana Bilgisayarında bir Sandbox'ta Çalışır

**Bağlam:** cBot oluşturmak MSBuild çalıştırmayı gerektirir, bu da dosya sistemi erişimi ve potansiyel olarak ağ erişimi talep eder. Güvenilmeyen kaynak kodu derlemek, bir VM'nin tüm dosya sistemi için bir kötü amaçlı MSBuild eklentisi ile riski ortaya koyar.

**Karar:** `CBotBuilder` aşağıdakiler içinde çalışır:

- Web ana bilgisayarında (Docker soketine doğrudan erişim içerik ihtiyacı)
- Tek kullanımlı SDK konteyneri içinde (throwaway, her derleme için yeniden)
- Bind-mounted `/work` dizini (kaynak) + paylaşılan `app-nuget-cache` hacmi
- Ev sahibi dosya sistemi / ağ erişimi yok

**Sonuçlar:**

✅ **Sandbox:** MSBuild sadece belirtilen hacimlere erişebilir.

✅ **Yalıtım:** Kötü kaynak kod ev sahibi kaynaklarını zarar veremez.

❌ **Yapı performansı:** Her derleme yeni konteyner = ısıtma süresi. Önbelleği tarafından azaltılmış (hacim).

Daha fazla: [Build & Backtest →](../features/build-and-backtest.md)
