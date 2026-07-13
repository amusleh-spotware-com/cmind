---
description: "İşlem Maliyeti Analizi — bir emrin varış fiyatına göre yürütme kalitesini (baz puan cinsinden kayma ve uygulama açığı) ölçer; bankaların yaşadığı bileşik yürütme avantajı. Deterministik."
---

# İşlem Maliyeti Analizi (TCA)

Yürütme alfası işlem başına küçük, binlerce işlem üzerinde ise devasadır — bankaların ve prop masalarının
avantajlarını korumasının büyük bir parçasıdır. TCA, gerçekte elde ettiğiniz fiyatın, işlem yapmaya *karar
verdiğiniz* andaki fiyattan ne kadar saptığını ölçer.

**cBots → Execution Cost** (`/quant/tca`) sayfasını açın.

## Ne ölçer

**Varış (karar) fiyatı**, **yön** ve **gerçekleşmeleriniz** (fiyat × miktar) verildiğinde şunları raporlar:

- **Ortalama gerçekleşme fiyatı (VWAP)** — gerçekte elde ettiğiniz hacim ağırlıklı fiyat.
- **Kayma (bps)** — varıştan VWAP'a sapma, baz puan cinsinden, **pozitif bir sayı maliyet olacak şekilde
  işaretli** (varışın üzerinde alım veya altında satım) ve negatif bir sayı fiyat iyileşmesidir.
- **Uygulama açığı** — bu maliyetin fiyat × miktar cinsinden ifadesi: sapmanın bu emirde size maliyeti olan para.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Akıllı dilimleme (Almgren-Chriss)

Maliyeti ölçmenin ötesinde, cMind büyük bir emri onu *en aza indirmek* için planlayabilir. **cBots →
Execution Schedule** (`/quant/execution`) bir **Almgren-Chriss optimal-yürütme çizelgesi** oluşturur:
toplam miktar, dilim sayısı, risk kaçınmanız, volatilite ve geçici piyasa etkisi verildiğinde, her dilimde
işlem yapılacak boyutu döndürür. Daha yüksek risk kaçınma çizelgeyi **öne yükler** (zamanlama riskini
azaltır); sıfır risk kaçınma düz bir **TWAP**'a düzleşir. Dilimler her zaman toplama eşittir.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Neden güvenilir

Altyapı bağımlılığı ve dış çağrısı olmayan saf, deterministik alan kodu (`Core.Execution`) — alım/satım
maliyet işareti, fiyat iyileşmesi, sıfır-kayma, VWAP toplama ve girdi korumaları için birim testlidir. Bu,
yürütme kalitesinin ölçüm yarısıdır; kopya motorunun aynalanan emirlerin maliyetini yargılamak (ve akıllı
dilimlemeyle azaltmak) için kullandığı aynı açık metriğidir.
