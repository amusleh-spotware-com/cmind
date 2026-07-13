---
description: "Perakende için kurumsal pozisyon boyutlandırma — tek bir strateji için volatilite hedefleme ve kesirli-Kelly maruziyeti, artı bir strateji kitabı boyunca korelasyon matrisiyle ters-volatilite risk-paritesi tahsisi."
---

# Pozisyon Boyutlandırma ve Portföy

"Bu işlem ne kadar büyük olmalı?" sorusu, bir avantajın bileşik büyüyüp büyümeyeceğine veya patlayıp
patlamayacağına karar veren sorudur. Kurumlar buna **volatilite hedefleme** ve **Kelly kriteri** ile yanıt
verir ve eşit dolarlar yerine **risk paritesi** ile bir kitap oluştururlar. cMind her ikisini de
perakendeye getirir — bir stratejinin getiri serisi üzerinde deterministik matematik, sade İngilizce bir
öneriyle.

**cBots → Position Sizing** (`/quant/sizing`) sayfasını açın.

## Tek-strateji boyutlandırma

Bir stratejinin getirileri (veya özkaynak eğrisi), hedef yıllık volatilite, bir Kelly kesri ve bir
kaldıraç üst sınırı verildiğinde, boyutlandırıcı şunları raporlar:

- **Gerçekleşen yıllık volatilite** — stratejinin kendi volatilitesi, zamanın-karekökü kuralıyla yıllıklandırılmış.
- **Volatilite-hedefli boyutlandırma** — gerçekleşen volatiliteyi hedefinize ulaştıran maruziyet
  (`hedef ÷ gerçekleşen vol`), kaldıraç sınırınızla sınırlanmış. Daha düşük-vol stratejileri daha fazla boyut kazanır.
- **Tam Kelly** — büyüme-optimal kesir `f* = μ / σ²` (getirilerin varyansı üzerinden ortalama).
- **Kesirli Kelly** — `f*`, Kelly kesrinizle ölçeklenmiş. Yarım-Kelly (0.5) yaygın güvenli seçimdir;
  tam Kelly, gerçek, belirsiz avantajlar için ünlü biçimde fazla agresiftir.
- **Önerilen maruziyet** — volatilite-hedef ve kesirli-Kelly boyutlandırmalarının **daha küçüğü** (daha
  güvenli olanı), sınırlanmış. Pozitif avantajı olmayan bir strateji (tam Kelly ≤ 0) **sıfıra** boyutlandırılır.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portföy tahsisi

Ona iki veya daha fazla strateji (hizalanmış getiri serileri) verin ve **ters-volatilite risk paritesi**
ile bir kitap oluştursun — her strateji `1 / volatilite` ile ağırlıklandırılıp normalleştirilir — böylece
dolar değil, risk eşit paylaşılır. Ayrıca şunları döndürür:

- stratejileriniz arasındaki **korelasyon matrisi** (gizlice aynı bahis olanları tespit edin);
- örnek kovaryanstan, o ağırlıklardaki **öngörülen portföy volatilitesi**;
- tüm kitabı hedef volatilitenize doğru ölçekleyen bir **kaldıraç** faktörü (sınırlanmış).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Neden güvenilir

Hepsi altyapı bağımlılığı ve dış çağrısı olmayan saf, deterministik alan kodudur (`Core.Portfolio`) —
vol-hedef ölçekleme, Kelly formülü, ters-volatilite ağırlıklarının eşit-risk özelliği ve korelasyon
matrisi için birim testlidir. Varsayılan olarak tavsiye niteliğinde: sayılar bir öneridir, asla otomatik bir emir değildir.
