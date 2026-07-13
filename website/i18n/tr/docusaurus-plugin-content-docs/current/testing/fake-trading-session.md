---
title: Sahte Ticaret Oturumu
description: Deterministic FakeTradingSession simülatörü — cTrader davranışını ağlaştırmış.
---

# Sahte Ticaret Oturumu

Kopyalama ticareti test etmek ve onaylamak için, `FakeTradingSession` cTrader ticaret API'sini simüle eder.

## Niyet

- **cTrader-sadık**: Gerçek davranış prototip
- **Belirleyici**: Kararlı test sonuçları, rasgele yok
- **Hızlı**: Ağ çağrı yok

## Kullanım

```csharp
var session = new FakeTradingSession();
session.PlaceOrder(new Order { Symbol = "EURUSD", Volume = 100000 });
var result = session.ExecutePartialFill(0.8);

Assert.AreEqual(80000, result.ExecutedVolume);
```

## CTrader Davranışı Protokoller

Oturum şunları taklit eder:

- Açık pozisyon (LongPosition, ShortPosition)
- Kısmi Doldurma
- Kaymış
- Gecikmeler

Daha fazla: [Stress Testing →](./stress-testing.md)
