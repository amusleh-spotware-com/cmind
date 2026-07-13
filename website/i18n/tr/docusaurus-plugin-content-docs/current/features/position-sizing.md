---
title: Pozisyon Boyutlandırması
description: Risk temelli boyutlandırma — hesap havalanması, maksimum kayıp, pozisyon boyutu.
sidebar_position: 21
---

# Akıllı Pozisyon Boyutlandırması

Riske dayalı boyutlandırma — hesap boyutu, riskli yüzde, maksimum kayıp.

## Formüller

```
Position Size = Account Balance × Risk % / (Entry - Stop Loss)
```

Örnek:
- Hesap: $10,000
- Risk: %2
- Stop: 100 pips
- Boyut = 10000 × 0.02 / 100 = $20 risk

## cBot Entegrasyonu

```csharp
var positionSize = CalculatePositionSize(
  accountBalance: Account.Balance,
  riskPercent: 2.0,
  stopLossPips: 100
);
PlaceOrder(positionSize);
```

Daha fazla: [Strateji Sağlığı →](./strategy-health.md)
