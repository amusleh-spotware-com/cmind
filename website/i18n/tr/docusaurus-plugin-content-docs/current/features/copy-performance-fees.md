---
description: "Bir yüksek-su-işareti üzerinde para-yöneticisi performans ücretleri, standart kopya-işlem modeli (cTrader Copy, Darwinex, ZuluTrade kâr-payı): bir sağlayıcı…"
---

# Kopya performans ücretleri (Faz 4)

Bir yüksek-su-işareti üzerinde **para-yöneticisi performans ücretleri**, standart kopya-işlem modeli
(cTrader Copy, Darwinex, ZuluTrade kâr-payı): bir sağlayıcı, her takipçinin zirve özkaynağının üzerindeki
*yeni* kârın bir yüzdesini alır — asla açılış bakiyesi üzerinden ve asla zaten geri kazanılmış zemin için
iki kez. `App:Copy:FeesEnabled` ile **isteğe bağlı** (varsayılan kapalı).

## Model (yüksek-su-işareti)

Hedef (takipçi hesabı) başına, her yerleşimde:

1. **İlk yerleşim** yüksek-su-işaretini (HWM) mevcut özkaynakta tohumlar → ücret yok (bir takipçi asla
   mevduatı üzerinden faturalandırılmaz).
2. **Yeni zirve** (özkaynak > HWM): `ücret = performanceFeePercent × (özkaynak − HWM)`, sonra `HWM ← özkaynak`.
3. **Zirvede veya altında**: ücret yok, HWM değişmez — takipçi önce eski zirveyi geçmek zorundadır, böylece
   aynı kazançlar için asla iki kez ücretlendirilmez.

Ücret aritmetiği `CopyDestination.SettleFee(equity)` üzerinde bir alan değişmezidir — toplam ona sahiptir;
yerleşim servisi yalnızca yoklanan özkaynağı sağlar ve döndürülen tutarı kaydeder. `PerformanceFee`, bir
yanlış yapılandırmanın bir takipçinin tüm kazancını ücret olarak alamaması için %50 ile sınırlanmış bir
değer nesnesidir.

## Nasıl yerleşir

```
CopyFeeSettlementService (BackgroundService, yalnızca FeesEnabled iken)
   │  her App:Copy:FeeSettlementInterval
   ├─ ücret-yapılandırılmış hedefi olan çalışan profilleri yükle
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader bir oturum açar,
   │                                               bakiye + dalgalı K&Z hesaplar (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← toplam üzerindeki HWM mantığı
   └─ ilerleyen HWM'yi kalıcılaştır + CopyFeeAccrual ekle (yalnızca yeni zirvede)
```

- `ICopyEquityReader` bir Core soyutlamasıdır; canlı uygulama (`OpenApiCopyEquityReader`) tek altyapı
  parçasıdır — böylece yerleşim + HWM mantığı testlerde sahte bir okuyucuyla, canlı broker olmadan yürütülür.
- `CopyFeeAccrual`, yalnızca-ekleme günlüğüdür (HWM-öncesi, özkaynak, ücret %, ücret tutarı, yerleşilme
  zamanı) — ücret raporu ve faturalama için bir gerçek günlüğü, toplam değil.

## Yapılandırma ve API

| `App:Copy` ayarı | Varsayılan | Etki |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Yerleşim servisini çalıştır. |
| `FeeSettlementInterval` | `1h` | Özkaynağın ne sıklıkta yoklandığı ve ücretlerin yerleştiği. |

Hedef başına: `PerformanceFeePercent` (0–50) hedefte ayarlanır (hedef ekleme/düzenleme isteği).

- `GET /api/copy/profiles/{id}/fees` — profilin ücret tahakkukları + toplam alınan.

## Testler

- **Birim** (`CopyPerformanceFeeTests`) — HWM değişmezi: ilk yerleşim tohumlar + hiçbir şey almaz; yeni bir
  zirve yalnızca zirvenin üzerindeki kazancı alır; zirvede/altında hiçbir şey almaz ve zirve asla gerilemez;
  bir düşüşten sonra yalnızca eski zirveyi geçen kurtarma alınır; %0 asla almaz; VO aralık-dışı yüzdeleri reddeder.
- **Entegrasyon** (`CopyFeeSettlementTests`, gerçek Postgres, sahte özkaynak okuyucu) — tohum→10k (ücret yok,
  tohumlanmış işaretle), 12k (400 alır, işaret ilerler), 11k (ücret yok, işaret tutulur); tahakkuk doğru
  sahip/tutarla kalıcılaştırılır.

Kopya host'u ücretlerden etkilenmez (yerleşim ayrı bir DB işidir), böylece kopya DST stres paketi etkilenmez (23/23).
