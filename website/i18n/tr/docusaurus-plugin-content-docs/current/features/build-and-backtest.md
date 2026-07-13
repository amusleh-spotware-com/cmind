---
description: "cTrader cBot'larını (C ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den oluştur, çalıştır, geri test et; resmi ghcr.io/spotware/ctrader-console görüntüsünde çalıştır."
---

# cBot'ları oluştur ve geri test et

cTrader cBot'larını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den oluştur, çalıştır, geri test et; resmi `ghcr.io/spotware/ctrader-console` görüntüsünde çalıştır.

## Oluştur

- **Oluşturucu** sayfası Monaco editörü barındırır; `CBotBuilder` projesi `dotnet build` ile derle **bir kez çıkartma konteynırında** (`AppOptions.BuildImage`, çalışma dizini bind-mount `/work` da), bu nedenle güvenilmeyen kullanıcı MSBuild hedefleri ana bilgisayara ulaşamaz. NuGet restore, paylaşılan birim aracılığıyla yapılar arasında önbelleğe alındı. Web ana bilgisayarının Docker soket erişimi gerekli.
- C# + Python başlangıç şablonları `src/Nodes/Builder/Templates/` içinde canlı.

## Çalıştır ve geri test et

- **Örnekler** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı yerine koymayı (kimlik değişikliği), konteyner kimliği taşındı.
- `NodeScheduler` en az yüklü uygun düğümü seç; `ContainerDispatcherFactory` uzak düğüm HTTP aracısına veya yerel Docker sevkiyatçısına rota.
- Tamamlama yoklama çıktı konteynerlerini mutabık kıl (geri test konteynırları `--exit-on-stop` üzerinden kendi kendini çıkar); rapor mevcut → tamamlandı (depola `ReportJson`), eksik → başarısız.
- Canlı konteyner günlükleri SignalR üzerinde tarayıcıya akış; geri test öz serileri raporu ayrıştırıldı + grafik.

## cTrader Console CLI notları

Geri testler `--data-mode` (`m1` varsayılan), `dd/MM/yyyy HH:mm` olarak tarihler ve `params.cbotset` JSON konumsal argümanı gerektirir; `run` `--data-dir` reddet (yalnızca geri test). Bkz. `ContainerCommandHelpers`.

## Düğümler ve ölçek

Yürütme kapasitesi düğüm aracılarını ekleyerek ölçekle (kendi kendini kaydet + sinyal). Bkz. [düğüm bulma](../operations/node-discovery.md) ve [ölçekleme](../deployment/scaling.md).
