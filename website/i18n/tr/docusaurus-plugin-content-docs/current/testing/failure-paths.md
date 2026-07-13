---
title: Hata Yolu Testi
description: Rağmen başarısızlık — bağlantı kaybı, token rotasyonu, zaman uyumsuzluğu, düğüm ölümü.
---

# Hata Yolu Testi

Her özellik başarısızlık senaryoları kapsamaktadır — ağ bozulmaları, kimlik bilgisi sorunları, ölü düğümler.

## Senaryolar

| Senaryo | Nasıl Sınana |
|---|---|
| Bağlantı Kaybı | Ağ'yi simüle dönem değiştir |
| Token Süresi Dolu | Token süresi simüle |
| Düğüm Ölümü | Düğüm durdurmak |
| Kısmi Dolgu | Emir özü karşılanır |
| Zaman Uyumsuzluğu | Saat farkı |

## Test Yazma

```csharp
[TestMethod]
public async Task FailureScenario_ConnectionLost_Resync()
{
  var session = NewFakeTradingSession();
  session.PlaceOrder(order1);
  
  session.SimulateConnectionLoss();
  
  var result = await copyEngine.Resync();
  Assert.AreEqual(ResyncStatus.Success, result.Status);
}
```

Daha fazla: [Stress Testing →](./stress-testing.md)
