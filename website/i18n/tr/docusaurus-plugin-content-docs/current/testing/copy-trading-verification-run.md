---
title: Kopyalama Ticareti Doğrulama Çalışması
description: Kod değişikliklerini kopyalama ticareti karşı doğrula — DST suite, senaryo kapsamı.
---

# Kopyalama Ticareti Doğrulama Çalışması

Kopyalama ticareti kodunu değiştirdikten sonra, DST suite tüm senaryolara karşı çalıştırın.

## Senaryolar

23 test senaryosu:

1. Basit Kopyalama
2. Kısmi Dolgu Gerçeğe Uyarlanması
3. Düşen Bağlantı
4. Token Rotasyonu
5. ... (daha 18 senaryo)

## Çalıştırma

```bash
dotnet test tests/StressTests -v normal --logger "console;verbosity=minimal"
```

Sonuç:

```
23/23 PASSED ✓
Duration: 45 seconds
```

## Başarısızlık Tanısı

Başarısız senaryo, tam backtrace + log özeti:

```
Scenario: Kısmi Dolgu + Resync
Beklenen: 1000 lot final
Gerçek:   999 lot
Neden:    Resync çıkış işlemini kaçırdı
```

Daha fazla: [Stress Testing →](./stress-testing.md)
