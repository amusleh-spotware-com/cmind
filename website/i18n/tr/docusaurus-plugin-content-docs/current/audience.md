---
slug: /audience
title: cMind kimin için?
description: cMind, cTrader tüccarları, brokerleri ve bulut/VPS sağlayıcıları içindir. Sizin yolunuzu bulun — bir tüccar olarak kendi sunucunuzda çalıştırın, bir broker olarak beyaz etikette sunun veya yönetilen bir barındırma sağlayıcı olarak teklifte bulunun — artı katkıda bulunmak için nasıl yapılır.
keywords:
  - cTrader platformu
  - algoritmik ticaret
  - beyaz etiket broker
  - yönetilen ticaret barındırması
  - açık kaynak ticaret
sidebar_position: 2
---

# cMind kimin için?

Kısa sürüm: **paranızı riske atan ve kontrolde olmaktan hoşlanan insanlar.** cMind ciddi bir
algoritmik ticaret operasyon konsolu — bir tüketici fintech oyuncağı değil. İster ticaret yap, ister
broker ol, ister barındırma sağla, hepiniz için bir yol vardır.

## 📈 Tüccarlar — tüm masanızı kendi sunucunuzda çalıştırın

cTrader'da ticaret yapıyorsunuz ve bir editörü, backtester'ı, VPS'i ve üç tarayıcı sekmesini
birbirine yapıştırmaktan bıktınız. cMind'ı kendi sunucunuzda çalıştırın ve yazarlık, backtesting,
canlı yürütme, kopya ticareti ve izlemeyi tek bir AI destekli konsolda edinin — kendi kutunuzda, verileriniz
asla bırakmıyor.

**Okuyun:** [cTrader tüccarları için cMind →](./for-traders.md)

## 🏦 Brokerler — müşterileriniz için beyaz etiket olarak sunun

Bir cTrader aracı kuruluşu işletiyorsunuz ve rakiplerin terminal dışında hiçbir şey sunmadığından
daha iyi bir avantaj istiyorsunuz. cMind'ı kendi ürünü olarak gönderin: müşterilerinize AI, kopya
ticareti ve prop-firm zorlukları kendi markanız altında verin, hesapları kitabınız ile sınırlayın ve
yeni gelir hatları açın.

**Okuyun:** [cTrader brokerleri için cMind →](./for-brokers.md) ·
[İşletmeler için beyaz etiket →](./white-label-for-business.md)

## 🖥️ Bulut ve VPS sağlayıcıları — yönetilen barındırma olarak satın

İşlem gücü kiralıyorsunuz. Yönetilen cMind barındırması sunun ve yüksek değerli, işlem gücü açısından
açlık çeken, yapışkan bir iş yükü edinin — ve aboneliği, ölçülü işlem gücünü, beyaz etiketi ve AI'yı
para haline dönüştürün.

**Okuyun:** [Bulut ve VPS sağlayıcıları için cMind →](./for-cloud-providers.md)

## 🧑‍💻 Katkıda bulunanlar — bunu daha iyi hale getirmeye yardımcı olun

cMind açık kaynaktır (MIT) ve topluluk tarafından şekillendirilmiştir: C# 14 / .NET 10, katı DDD,
EF Core + PostgreSQL, .NET Aspire, bir MCP sunucusu ve üç test katmanı. Yukarıdaki her kişi yapabilir
— ve yapmalıdır — bunu ileriye taşı:

- **Tüccarlar:** cBot şablonları ekleyin, özellik isteyin, hataları bildirin.
- **Brokerler:** masanızın ihtiyaç duyduğu entegrasyonları ve kontrolleri katkıda bulunun.
- **Sağlayıcılar:** operasyonel düzeltmelerinizi ve IaC iyileştirmelerinizi yayında sunun.

Sorunları açın, UI çevirilerini veya AI sağlayıcı adaptörlerini ekleyin ve PR gönderin —
[Katkılama rehberi](./contributing.md) ve [MCP sunucu dokümanları](./features/mcp.md) sizi başlatacak.

## cMind *sizin için* değil mi?

Kendinize dürüst olun. cMind abartılı olabilir eğer:

- Hiç bir terminal'e dokunmadınız ve başlamak istemiyorsunuz — ancak bir
  [bulut/VPS sağlayıcısı bunu sizin için barındırabilir](./for-cloud-providers.md).
- Sadece manuel olarak ticaret yapıyorsunuz ve botlar, backtestler veya kopya ticareti umursamıyorsunuz.
- Bir destek hattı olan barındırılan SaaS istiyorsunuz ve kimsenin bunu kendi sunucusunda çalıştırmasını
  istemiyorsunuz — cMind tasarımına göre kendi sunucusunda barındırılır (sadece bunu acısız hale getirmeyi
  çok zor deneyin).

Hala başıyla onay vermekle devam mı ediyorsunuz? Hoşgeldiniz. → [Yerel olarak çalıştırın](./deployment/local.md)
