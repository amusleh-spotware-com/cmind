---
title: Derle & Backtest
description: Monaco IDE'de C# ve Python cBot yazılı — dan kaynağına derle bir sandbox Docker konteyneri - Backtest tarihsel veriler karşı.
sidebar_position: 3
---

# cBot Derleme & Backtesting

Tarayıcıda kod yazılı, docker'da derle, gerçek veriler üzerinde backtest çalıştırın.

## Monaco IDE

- **C# & Python** kod şablonları
- **IntelliSense** - tamamlama, çabuk bilgi
- **Kaydet & Sürüm** — her sürümü tutun

## Derleme

1. "Derle" 'ye basıyor
2. `CBotBuilder` dockerfile'dan başlatır
3. Sorunu yapı → çıkış ile derle
4. Başarıyı sürünürlük çalışlasını hazırlar

## Backtest

1. Backtest parametrelerini ayarla (tarihi, sembol, kütüphane)
2. "Backtest Koş" tıklayın
3. cTrader Console konteyneri tarihsel veriler sürüyor
4. Öz eğri & kişileri gözleyin gerçek zamanlı olarak
5. PDF raporu veya CSV dışarı aktar

## Kütüphane

cMind cTrader Console ve cTrader Open API'ye erişim sağlar:

```csharp
[Parameter("Take Profit", DefaultValue = 50)]
public double TakeProfitPips { get; set; }

protected override void OnTick()
{
  var ask = Symbol.Ask;
  // Ticaret mantığı
}
```

Daha fazla: [Backtest Bütünlüğü →](./backtest-integrity.md)
