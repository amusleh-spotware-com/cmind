---
title: İçgüdüsel Konumlandırma
description: Tersine ticaret stratejisi — pazar ekstremini algılamak, karşı-trend işlemler.
sidebar_position: 22
---

# İçgüdüsel Konumlandırma

Pazar ekstremini ticaret — piyasası çok kısa/uzun olduğunda, karşı ticaret.

## Algılama

AI piyasa konumlandırması analiz eder:

- Net kısa pozisyon %
- Açık faiz ekstremlemleri
- Resim duyarlılık

Ekstremum (> 85%) algılandığında:

1. Uyarı sınıflandırması
2. İçgüdüsel stratejileri tetikle
3. Boyut ayarlama (daha agresif)

## cBot Entegrasyonu

```csharp
if (await ai.IsMarketExtreme("EUR", "USD"))
  PlaceOrder(size * 1.5);  // Aggressive
```

Daha fazla: [Position Sizing →](./position-sizing.md)
