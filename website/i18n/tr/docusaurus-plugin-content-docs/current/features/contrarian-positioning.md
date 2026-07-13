---
description: "Kontrarian Perakende Konumlanması — uzun pozisyondaki perakende yatırımcı yüzdesini kontrarian bir eğilime dönüştürür (kalabalık tek yöne aşırı yüklendiğinde tersine oyna), artı ileriye-bakış yanlılığına karşı koruyan zaman-noktası sinyal değer nesneleri."
---

# Kontrarian Perakende Konumlanması

Perakende kalabalık, FX'te gerçekten yararlı birkaç duyarlılık sinyalinden biridir — **kontrarian** bir
gösterge olarak. Perakende yatırımcıların büyük çoğunluğu uzun pozisyondayken, fiyat tarihsel olarak düşme
eğilimi göstermiştir ve tersi de geçerlidir. Bu araç kalabalık konumlanmasını eyleme geçirilebilir bir
okumaya dönüştürür.

**cBots → Contrarian Positioning** (`/quant/positioning`) sayfasını açın.

## Ne yapar

**Uzun pozisyondaki perakende yatırımcı yüzdesini** (broker'ınızın duyarlılık sayfasından veya FXSSI gibi
bir beslemeden) girin; şunları döndürür:

- **Kontrarian eğilim** — ≥ %60 uzunken **Düşüş yönlü** (kalabalık aşırı uzun), ≤ %40 uzunken **Yükseliş
  yönlü** (kalabalık aşırı kısa), %40–60 kararsızlık bandında **Nötr**;
- **Güç** — kalabalığın ne kadar tek taraflı olduğu (0 = dengeli, 1 = tamamen tek taraflı), sinyali
  ağırlıklandırmak için.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Yapı gereği zaman-noktası

Kaputun altında sinyal katmanı (`Core.Signals`), **bilinebilir olduğu anla damgalanan** ve o damga
olmadan oluşturulmayı reddeden bir `PointInTimeSignal` modeller. Bir sinyali tüketen herhangi bir backtest
veya otonom ajan `IsKnownAt(decisionTime)` kontrolü yapar — böylece gelecekteki veriler asla geçmiş bir
karara sızamaz. İleriye-bakış yanlılığı, nicel finanstaki en büyük tekrarlanabilirlik katilidir; alan
modeli bunu yapısal olarak imkânsız kılar.

## Neden güvenilir

Altyapı bağımlılığı olmayan saf, deterministik alan kodu — kontrarian eşikleri ve zaman-noktası koruması,
40/60 sınırları ve aralık-dışı reddi dahil olmak üzere birim testlidir.
