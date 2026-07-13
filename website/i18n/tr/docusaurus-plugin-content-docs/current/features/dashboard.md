---
title: Gösterge Paneli
description: cMind gösterge paneli — cBot çalıştırmalarınız, backtest'leriniz, kaynaklarınız ve düğüm kümeniz için canlı, mobil-öncelikli bir komuta merkezi.
---

# Gösterge Paneli 📊

Oturum açtığınızda gördüğünüz ilk şey ve açıkçası tüm gün açık bırakacağınız sayfa. Açılış sayfası
(`/`, `Components/Pages/Index.razor`), oturum açmış kullanıcının cBot çalıştırmaları, backtest'ler,
kaynaklar ve (yöneticiler için) düğüm kümesindeki etkinliği için **canlı, mobil-öncelikli bir komuta
merkezidir**. Kendini yeniler, telefonda harika görünür ve sizi asla F5'e basmaya zorlamaz.

## Neyi gösterir

Yukarıdan aşağıya, bir telefon için öncelik sırasına göre (her blok mobilde tam genişlikte bir yığın
öğesi, tablet/masaüstünde duyarlı bir ızgaradır):

1. **Başlık** — başlık, canlı bir gösterge (gerçek nabız gibi atan bir nokta; `prefers-reduced-motion`
   altında statik), son güncelleme zamanı ve KPI'ları ve grafiği yönlendiren bir **dönem geçişi**
   (`1H · 24H · 7D · 30D`).
2. **Kahraman KPI'lar** — bir bakışta görülebilen dört kart, her biri büyük bir sayı + satır içi bir SVG
   sparkline ve (anlamlı olduğunda) **önceki döneme göre bir delta**:
   - **Şu an aktif** — şu anda başlayan/çalışan çalıştırmalar + backtest'ler.
   - **Başarı oranı** — dönem boyunca tamamlanan ÷ (tamamlanan + başarısız); yüzde puanı cinsinden delta.
   - **Tamamlanan** — bu dönemde biten çalıştırmalar/backtest'ler; önceki döneme göre delta.
   - **Başarısız** — bu dönemdeki başarısızlıklar; delta (daha az daha iyidir, bu yüzden bir düşüş yeşil gösterir).
3. **Etkinlik grafiği** — zaman kovası başına başlatılan / tamamlanan / başarısız için bir ApexCharts alan zaman çizelgesi.
4. **Örnek durum halkası** — çalışan / backtest'ler / bekleyen / tamamlanan / başarısız için bir çörek grafiği, toplam
   ortada.
5. **Backtest'ler** — üç kutucuklu bir anlık görüntü (çalışan / tamamlanan / başarısız), `/backtest`'e tıklama geçişi.
6. **Kopya ticaret** — canlı bir durum noktası, hedef sayısı ve çalışan profillerde bir **Live**
   rozeti ile kopya-ticaret profilleriniz; `/copy-trading`'e tıklama geçişi.
7. **AI ajanları** — çalışma durumu (arketip · durum) ve son eylem zamanı ile persona-güdümlü ticaret ajanlarınız;
   `/agent-studio`'ya tıklama geçişi.
8. **Canlı etkinlik akışı** — durum-renkli bir nokta ve göreli bir zaman damgası ile en son 20 olay
   (en yeni önce).
9. **Küme sağlığı** (yalnızca yöneticiler) — aktif-e-karşı-toplam düğümler ve kullanımda olan kapasite göstergesi.
10. **Kaynak kutucukları** — cBot'lar, ticaret hesapları, cTrader ID'leri, MCP anahtarları (sayfalarına tıklama geçişi).

## Gösterge panelinizi özelleştirin

Yukarıdaki her blok, **kontrol ettiğiniz bir widget'tır**. Herhangi bir widget'ı **gösterip/gizleyebileceğiniz** ve
yukarı/aşağı oklarla **yeniden sıralayabileceğiniz** bir iletişim kutusu açmak için (başlığın sağ üst köşesindeki)
**Customize**'a basın. **Reset to default**, katalog sırasını geri yükler. Seçiminiz **kullanıcı başına sunucu tarafında
kalıcılaştırılır**, böylece yalnızca bu sekmede değil, tarayıcılar ve cihazlar arasında sizi takip eder.

- Özellik-kapılı ve yalnızca-yönetici widget'ları (Kopya ticaret, AI ajanları, Küme sağlığı) yalnızca
  dağıtımınız/rolünüz bunları kullanabildiğinde iletişim kutusunda görünür.
- Widget kataloğu, `Core/Dashboard/DashboardWidgets.cs`'te tek bir doğruluk kaynağıdır; sunum
  (etiket + simge + kullanılabilirlik) `Components/Dashboard/DashboardWidgetMeta.cs`'te bulunur.

## Nasıl canlı kalır

Sayfa her 10 saniyede bir `GET /api/dashboard/overview?period=<1h|24h|7d|30d>`'yi yoklar ve
widget'ları yerinde yeniden oluşturur — manuel yeniden yükleme yok. Geçici bir getirme başarısızlığı yutulur ve
bir sonraki tıkta yeniden denenir; döngü, atma sırasında temiz bir şekilde durur. İlk yükleme bir iskelet gösterir;
kalıcı bir başarısızlık, **Retry** ile bir hata kartı gösterir; verisi olmayan bir kullanıcı, sıfırlanmış KPI'lar
ve boş-durum metni görür.

## Arka uç

- `Endpoints/DashboardEndpoints.cs`, `/overview`'i eşler (ve eski skaler `/stats`'ı korur). Bu,
  `ICurrentUser` aracılığıyla kullanıcı başına ve yönetici-kapılıdır; saat `TimeProvider`'dan gelir. Ayrıca
  `GET/PUT /api/dashboard/layout`'u da eşler — kullanıcının widget düzeni, sayfa başlangıcında yüklenir ve
  Customize iletişim kutusundan kaydedilir.
- **Düzen kalıcılığı**, `UserDashboard` toplamıdır (`Core/Dashboard/UserDashboard.cs`): kullanıcı başına bir pano
  (`UserId` üzerinde benzersiz), bir `jsonb` sütununda depolanan sıralı bir widget ayarları listesine (görünür + sıra)
  sahiptir. Sıralı liste yalnızca `Apply` / `Reset` aracılığıyla mutasyona uğratılır; bunlar her anahtarı
  `DashboardWidgets` kataloğuna karşı doğrular ve koleksiyonu eksiksiz ve tekilleştirilmiş tutar. Bilinmeyen
  anahtarlar bir `DomainException` → `400` ile reddedilir.
- `Endpoints/DashboardQuery.cs`, bileşik `DashboardOverview` okuma modelini oluşturur: her-zaman bir durum
  anlık görüntüsü (gruplanmış sayımlar), bir kez gerçekleştirilmiş pencerelenmiş bir örnek kümesi ve kaynak/düğüm
  sayımları. Örnek durumu ve terminal zaman damgaları TPH alt türlerinde bulunur (sütunlarda değil), bu nedenle
  satırlar paylaşılan `InstanceEndpoints.GetStartedAt/GetStoppedAt` yardımcıları aracılığıyla bellekte okunur. Olay zamanı =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs`, DTO'ları, dönem→(pencere, kova-sayısı) planını ve
  `DashboardMath`'ı tutar — saf, deterministik kovalama + KPI/delta matematiği (I/O yok, `now` içeri geçirilir).

KPI deltaları, geçerli pencereyi hemen önceki pencereyle karşılaştırır (sorgu bunun için çift bir
pencere getirir). **Canlı hesap K&Z akışı yoktur** — platformun yalnızca backtest'ler ve
prop-firm izleme için sermayesi vardır — bu nedenle gösterge paneli kasıtlı olarak *operasyoneldir* (etkinlik, verim, başarı oranı),
bir brokerlik bakiye şeridi değil.

## Tasarım ve token'lar

Tüm renk tasarım token'larından gelir (`var(--app-success|-warning|-error|-info|-primary|-text*)`), bu nedenle bir
white-label paleti bedavaya akar — grafik dahil, serilerin renkleri çalışma zamanında
`window.appReadTokens` aracılığıyla çözümlenmiş token'lardan okunur (SVG, CSS değişkenlerini doğrudan tüketemez). Gösterge
panelinde hiçbir yerde sabit-kodlanmış hex yoktur. Bkz. [../ui-guidelines.md](../ui-guidelines.md).

## "Powered by cMind" bağlantısı

Gösterge paneli, bu dokümantasyon sitesine işaret eden küçük, zarif bir **"Powered by cMind"** bağlantısı
gösterir. **Varsayılan olarak gösterilir** — projeyle gurur duyuyoruz ve diğer yatırımcıların onu bulmasına
yardımcı oluyor — ancak bu tamamen sizin kararınızdır. Tamamen white-label bir örnek çalıştıran satıcılar
`App:Branding:ShowSiteLink`'i `false` olarak değiştirir ve o kaybolur. Bkz.
[White-label markalama](./white-label.md#powered-by-link).

## Testler

- **Birim tarzı** (`tests/IntegrationTests/DashboardMathTests.cs`) — kovalama, başarı oranı,
  önceki-dönem deltaları, dönem ayrıştırma, boş/sınır (`now`'daki olay, sıfıra-bölme koruması).
- **Birim** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` toplamı: varsayılan
  seed, uygula sıra/görünürlük, ekle-atlanmış, yinelenen-daralt, bilinmeyen-anahtar reddi, sıfırlama.
- **Entegrasyon** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — okuma
  modeli gerçek Postgres'e karşı (durum/KPI'lar/etkinlik/kaynaklar, yönetici düğüm sağlığı, boş-kullanıcı yolu),
  yeni backtest'ler/kopya-profilleri/ajanlar bölümleri ve bir düzen **gidiş-dönüşü** (özel düzeni kaydet → yeniden yükle →
  sıra + görünürlük kalıcılaştırıldı).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — masaüstü + mobil: KPI
  kartları, grafik, halka ve akış oluşturulur; dönem geçişi aktif dönemi değiştirir ve yeniden yükler; bir KPI
  `/run`'a doğru delip geçer; **bir widget'ı gizlemek yeniden yükleme boyunca kalıcılaşır**, **Reset** onu geri getirir ve
  Customize iletişim kutusu bir telefonda yatay taşma olmadan çalışır. `/` ayrıca `PageSmokeTests`,
  `MobileLayoutTests` (kabuk + taşma-yok) ve `MobileJourneyTests`'te de yer alır.
