---
slug: /features
title: Özellikler — tam tur
description: cMind'ın yapabileceği her şey — kopya ticareti, AI, derleme ve backtest, prop-firma koruyucuları, beyaz etiket, PWA, MCP ve daha fazlası.
sidebar_label: Genel Bakış
---

# Özellikler — tam tur 🧭

Büyük tura hoşgeldiniz. cMind bir uygulamaya *çok fazlasını* paketler, bu nedenle işte harita. Her yetenek
kendi derin dalış dokümanına sahip — sizin kaşındığınız şeyi tıklatın.

## 🔁 Kopya ticareti

Taç mücevheri. Bir ana hesabı birçok hesaba yansıtın ve internet ağız açmasında bile senkronizasyonda
tutun.

- **[Kopya ticareti](./copy-trading.md)** — çekirdek: yansıtma, sipariş türleri, SL/TP, kayan, desync/resync.
- **[Yürütme transparanması](./copy-execution-transparency.md)** — tam olarak ne, ne zaman ve neden kopyalandığını görün.
- **[Performans ücretleri](./copy-performance-fees.md)** — sinyaliniz için ücret alın, yüksek su işareti tarzı.
- **[Sağlayıcı pazarı](./copy-provider-marketplace.md)** — tüccarların sağlayıcıları keşfetmesine ve
  takip etmesine izin verin.
- **[Bildirimler](./copy-notifications.md)** — sizi ihtiyacınız olduğunda bilgilendir.
- **[AI kopya tavsiyesi](./ai-copy-recommender.md)** — AI'nın kimi kopyalayacağını önermasine izin verin.
- **[Open API jeton yaşam döngüsü](./token-lifecycle.md)** — cMind, per cID tam olarak bir geçerli jeton tutar.

## 📊 Sizin ana tabanınız

- **[Pano](./dashboard.md)** — canlı, mobil-ilk komuta merkezi: sparklines ile KPI'lar, bir etkinlik
  grafiği, durum halkası, canlı beslenme ve (yöneticiler için) küme sağlığı. Kendisini yeniler.

## 🧠 AI çekirdeği

Yanına yapıştırılmış bir sohbet kutusu değil — gerçekten *işi yapan* AI.

- **[AI asistanı, aracısı, risk koruması ve uyarıları](./ai.md)** — strateji oluşturma, kendi kendini
  onarma derleme, botları otomatik olarak durdtabilen arka plan risk koruması ve akıllı uyarılar.

## 🛠️ İnşa et ve çalıştır

- **[cBot'ları derle ve backtest](./build-and-backtest.md)** — tarayıcıda Monaco IDE, C#/Python şablonları,
  sandbox derleme ve canlı öz sermaye eğrileri.
- **[MCP sunucusu](./mcp.md)** — AI istemcileri bunu sürüş yapabilir böylece cMind araçlarını HTTP + SSE
  aracılığıyla ortaya koyduk.

## 🏢 Bir işletme olarak çalıştırın

- **[Beyaz etiket / markalaştırma](./white-label.md)** — yapılandırma aracılığıyla her yüzeyi yeniden
  markalaştırın.
- **[Prop-firma zorluk simülasyonu](./prop-firm.md)** — canlı öz sermaye ile günlük kayıp, geri çekilme
  ve hedef kurallarını uygulayın.
- **[Özellik geçişleri](./feature-toggles.md)** — her dağıtım/tenant'ın ne göreceğine karar verin.
- **[Uyum / yasal](./compliance.md)** — denetim izi ve yasal yüzey.

## 📱 Deneyim

- **[Yüklenebilir uygulama (PWA)](./pwa.md)** — mobil-ilk, çevrimdışı kabuk, ana ekrana ekle.
- **[UI tasarım sistemi ve mobil-ilk](../ui-guidelines.md)** — görünüş arkasındaki tasarım jetonları
  ve kurallar.

## ⚙️ Kapu altı

Tümünü çalışan tutan operasyonel bitler:

- **[Düğüm filosu ve keşfi](../operations/node-discovery.md)** — düğümler kendi kendini nasıl kaydeder
  ve iyileştirir.
- **[Yatay ölçekleme](../deployment/scaling.md)** — kopyalar ekleyin, harici koordinatör gerekmez.
- **[Günlükleme ve denetim](../operations/logging.md)** — yapılandırılmış günlükler + OpenTelemetry.
- **[Konuşlandırma](../deployment/local.md)** — herhangi bir yerde çalıştırın.

:::note Dokümanları dürüst tutmak
Her özellik dokümanı kod ile kilitli adımda tutulur — davranışı değiştir, dokümanı güncelle, aynı
commit. Eğer hiç sapma farkederseniz, bu bir hata: lütfen [bir sorun açın](https://github.com/amusleh-spotware-com/cmind/issues/new/choose)
veya PR gönderin. 🙏
:::
