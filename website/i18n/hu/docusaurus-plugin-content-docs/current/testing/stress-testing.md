---
title: Stressz teszteles
description: "A stressz tesztcsomag 200+N párhuzamos copy-engine host-példányt futtat valódi trading session-ökkel, a limit tesztelésére és a versenyfeltételek kiszűrésére."
---

# Stressz teszteles

A stressz tesztcsomag 200+ párhuzamos copy-engine host-példányt futtat valódi trading session-ökkel, a limit tesztelésére és a versenyfeltételek kiszűrésére.

## Mit tartalmaz

```
StressTests
  CopyEngineHostStressTests
    ConcurrentReconciles_200Instances
    LeaseContention_MultipleSupervisors
    TokenRotation_Burst
    Resync_Burst
    GracefulShutdown_Burst
  ParallelCopyExecutionTests
    ParallelOrders_100Concurrent
    PartialFills_ManyConcurrent
    PositionUpdates_ManyConcurrent
```

## Futtatas

```bash
dotnet test StressTests
```

A stressz tesztek a `FakeTradingSession`-t használják, de nagyon sok párhuzamos példánnyal, a versenyfeltételek és a teljesítménybottleneckek kiszűrésére.

## Mire figyel

- **Lease versenyfeltétel:** több supervisor próbálja ugyanazt a lejárt lease-t reclaimelni → pontosan egy nyer.
- **Párhuzamos-order:** 100+ párhuzamos megbízás → mindegyik feldolgozásra kerül, nincs duplán küldés.
- **Token-rotáció burst:** egyszerre több lejáró token → sorba állítva, nincs verseny.
- **Memória/szál:** 200+ host-példány → nincs memória-szivárgás, nincs szálszivárgás.

## CI-integráció

A stressz tesztek a `tests/StressTests/` projektben vannak. A CI minden fő commit után futtatja őket, de nem minden PR-ban (túl sokáig tartanak).

## Korlátozások

- **Nem a mindennapos CI része** - csak a stressz tesztelési fázisban fut.
- **Nem determinisztikus időzítés** - a teljesítménymetrikák gépfüggőek.
- **Nagy erőforrásigény** - legalább 4 mag, 8 GB RAM ajánlott.
