---
description: "Rejim Laboratuvarı — bir getiri serisini Sakin / Normal / Çalkantılı volatilite rejimlerine etiketler ve rejim başına performansı raporlar, artı Hurst üssü (trend-kalıcılığı vs ortalamaya-dönüş). Deterministik."
---

# Rejim Laboratuvarı

Tek bir Sharpe oranı, çoğu avantajın koşullu olduğu gerçeğini gizler: sakin, trend piyasalarında harika ve
çalkantıda ölü (veya tersi). Rejim Laboratuvarı, bir stratejinin geçmişini volatilite rejimlerine böler ve
her birinde nasıl performans gösterdiğini gösterir — böylece avantajınızın gerçekte *ne zaman* işe
yaradığını bilirsiniz.

**cBots → Regime Lab** (`/quant/regimes`) sayfasını açın.

## Ne yapar

Bir getiri serisi (veya özkaynak eğrisi, en eski önce) verildiğinde:

- her noktada bir **izleyen gerçekleşen volatilite** hesaplar ve geçmişi, o volatilitenin tersillerine göre
  **Sakin**, **Normal** ve **Çalkantılı** rejimlere böler;
- **rejim başına performans** raporlar — gözlemler, ortalama getiri, volatilite ve Sharpe — böylece
  avantajın nerede yaşadığını görebilirsiniz;
- yeniden-ölçeklenmiş-aralık (R/S) analizi aracılığıyla **Hurst üssünü** tahmin eder: ~0.55'in üzerinde seri
  **trend / kalıcı**, ~0.45'in altında **ortalamaya-dönen** ve 0.5 civarında rastgele bir yürüyüşe yakındır.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // veya { "equity": [...] }
```

## Neden güvenilir

Altyapı bağımlılığı ve dış çağrısı olmayan saf, deterministik alan kodu (`Core.Regimes`) — rejim ayrımı
(sakin vs çalkantılı volatilite) ve Hurst yönü (anti-kalıcı seri 0.5'in altında puan alır, kalıcı bir trend
üstünde puan alır) için birim testlidir. Aynı rejim sinyali, otonom ajanların yansıma döngüsünü besler,
böylece bir ajan avantajının gerçek olduğu rejimlere yaslanabilir.
