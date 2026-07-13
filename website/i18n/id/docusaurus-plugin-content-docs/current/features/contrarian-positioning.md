---
description: "Strategi contrarian — membuka posisi berlawanan dengan sentimen pasar umum."
---

# Contrarian Positioning

Strategi contrarian — membuka posisi berlawanan dengan sentimen pasar umum.

## Konsep

Trading contrarian berdasarkan prinsip bahwa sentimen pasar sering kali terlalu ekstrem. Ketika
semua orang terlalu bullish atau terlalu bearish, ini bisa menjadi sinyal pembalikan. cMind menyediakan
tool untuk mengidentifikasi kondisi ini dan menjalankan strategi contrarian secara sistematis.

## Indikator Sentimen

### Commitment of Traders (COT)

Analisis laporan COT untuk melihat posisi smart money:

- **Commercial traders** — hedger profesional.
- **Non-commercial traders** — spekulan.
- **Retail traders** — trader kecil (seringkali salah arah).

### Sentiment Overlays

- **Fear & Greed Index** — indikator agregat sentimen pasar.
- **Put/Call Ratio** — rasio option volume.
- **VIX** — volatilitas sebagai ukuran ketakutan.

## Implementasi

### Setup

```csharp
// Inisialisasi strategi
var strategy = new ContrarianStrategy(
    sentimentProvider: _sentimentClient,
    entryThreshold: 0.3m,    // Deviasi sentimen untuk entry
    exitThreshold: 0.0m     // Deviasi untuk exit
);

// Konfigurasi
strategy.MaxPositionSize = 2.0;      // Lot maksimal
strategy.StopLossPips = 50;          // Stop loss dalam pips
strategy.TakeProfitPips = 100;       // Take profit dalam pips
```

### Entry Logic

Posisi dibuka ketika sentimen mencapai ekstrem:

```csharp
// Entry long ketika sentimen sangat bearish
if (sentiment < -entryThreshold && !HasPosition())
{
    await OpenPositionAsync(TradeDirection.Buy, CalculateLotSize());
}

// Entry short ketika sentimen sangat bullish
if (sentiment > entryThreshold && !HasPosition())
{
    await OpenPositionAsync(TradeDirection.Sell, CalculateLotSize());
}
```

### Exit Logic

```csharp
// Exit ketika sentimen kembali ke netral
if (Math.Abs(sentiment) < exitThreshold)
{
    await ClosePositionAsync();
}
```

## Risk Management

- **Position sizing** — berdasarkan confidence sentimen dan volatility.
- **Stop loss** — selalu required untuk semua posisi contrarian.
- **Max drawdown** — strategi dihentikan jika drawdown melebihi batas.
- **Correlation check** — tidak buka posisi yang korelasi tinggi dengan yang ada.

## Dashboard

Halaman **Contrarian** menampilkan:

- **Sentiment gauge** — visualisasi sentimen saat ini.
- **Historical sentiment** — chart sentimen historis.
- **Active positions** — daftar posisi contrarian aktif.
- **P&L** — profit/loss dari semua posisi.
- **Statistics** — win rate, avg trade duration, dll.

## Best Practices

1. **Gunakan sebagai overlay** — jangan jadi strategi utama.
2. **Kombinasikan dengan analisis teknis** — konfirmasi dengan price action.
3. **Perhatikan news** — sentimen bisa berubah cepat.
4. **Backtest dulu** — selalu uji strategi sebelum live.
5. **Money management** — jangan risk lebih dari 1-2% per trade.
