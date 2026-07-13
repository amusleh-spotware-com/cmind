---
title: Kopyalama Ticareti Performans Ücretleri
description: Kopya sağlayıcıları için geçişli ücretler — kar paylarını ödeyici, temizlik, vergisi.
sidebar_position: 30
---

# Performans Ücretleri

Kopya profili kârlı olduğunda otomatik ücret desteği — sağlayıcılar kâr paylarını kazanırlar.

## Yapılandırma

Profil oluşturma sırasında:

```json
{
  "performanceFeePercent": 20,  // 20% of profit
  "highWaterMark": true,         // only on new highs
  "minimumProfitToCharge": 100   // don't charge on small gains
}
```

## Hesaplaması

```
Profit = Close Balance - Entry Balance
Fee = Profit × FeePercent
Trader Gets = Profit - Fee
```

Örnek:
- Giriş Bakiyesi: $10,000
- Çıkış Bakiyesi: $11,000
- Kar: $1,000
- Ücret (%20): $200
- Tüccar alır: $800

## Temizlik

Sağlayıcılar aylık temizlik raporu elde ederler — kazanmış ücretler.

Daha fazla: [Copy Provider Marketplace →](./copy-provider-marketplace.md)
