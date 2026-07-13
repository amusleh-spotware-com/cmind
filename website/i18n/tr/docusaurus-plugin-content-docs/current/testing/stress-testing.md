---
title: Stres Testi (DST)
description: Deterministic Simulation Test — kopya ticaret 23 senaryo altında ölçeklendirilir.
---

# Deterministic Stres Testi (DST)

DST suite kopya ticaretini 23 senaryo (başarı, kısmi dolgu, resync, vb.) altında test eder — hiçbir rasgele, 100% tekrar edilebilir.

## Senaryolar

- Basit kopyalama
- Kısmi doldurma gerçeğe uyarlanması
- Düşen bağlantılar
- Token rotasyonu
- Düğüm ölümü & lease yenileme
- Çok kiracı

## Çalıştırma

```bash
dotnet test tests/StressTests --configuration Release
```

Tüm senaryoları paralel çalıştırır.

## Başarısızlık Tanısı

Başarısız senaryo:

```
Scenario: Partial fill + resync
Expected: 1000 lot final
Actual:   999 lot
Delta:    -1 (orphaned fill)
```

Root cause ile yardımcı — backtrace, log özeti.

Daha fazla: [Fake Trading Session →](./fake-trading-session.md)
