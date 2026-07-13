---
description: "Marketplace provider copy-trading — temukan, evaluasi, dan subscribe ke provider."
---

# Copy Trading Marketplace

Marketplace provider copy-trading — temukan, evaluasi, dan subscribe ke provider.

## Overview

Marketplace memungkinkan follower menemukan dan menyalin trader berpengalaman (provider).
Setiap provider ditampilkan dengan statistik performa transparan untuk membantu follower
membuat keputusan informasi.

## Menemukan Provider

### Browse

Halaman **Marketplace** menampilkan semua provider yang tersedia:

| Kolom | Deskripsi |
|-------|-----------|
| Provider | Nama dan avatar |
| Strategy | Deskripsi singkat strategi |
| Win Rate | Persentase trade profitable |
| Sharpe | Risk-adjusted return |
| Max DD | Drawdown maksimum |
| Investors | Jumlah follower |
| AUM | Assets under management |

### Filter

```json
{
  "filters": {
    "minWinRate": 55,
    "maxDrawdown": 20,
    "minSharpe": 1.0,
    "strategy": ["trend", "mean-reversion"],
    "instruments": ["forex", "indices"],
    "minAge": 6
  }
}
```

### Search

Pencarian teks pada nama provider dan deskripsi.

## Provider Profile

### Stats Overview

- **Total Return** — return keseluruhan sejak start.
- **YTD Return** — return year-to-date.
- **Monthly Returns** — return per bulan (chart).
- **Sharpe Ratio** — risk-adjusted return.
- **Sortino Ratio** — downside risk-adjusted return.
- **Max Drawdown** — drawdown maksimum.
- **Win Rate** — persentase trade profitable.
- **Avg Trade** — rata-rata profit per trade.
- **Avg Duration** — rata-rata lama posisi.

### Risk Metrics

- **Volatility** — standar deviasi return.
- **Value at Risk (VaR)** — potential loss pada confidence level.
- **Exposure** — rata-rata exposure per trade.
- **Correlation** — korelasi dengan indeks.

### Trading Style

- **Strategy Type** — trend, mean-reversion, scalping, dll.
- **Holding Period** — rata-rata lama posisi.
- **Risk Per Trade** — risk per trade sebagai % balance.
- **Max Positions** — posisi simultan maksimum.

## Subscribe

### Pilih Plan

| Plan | Durasi | Fee | Features |
|------|--------|-----|----------|
| Monthly | 30 hari | Per fee | Full access |
| Quarterly | 90 hari | Per fee + diskon | Full access |
| Annual | 365 hari | Per fee + diskon | Full access + priority |

### Konfigurasi Copy

```json
{
  "copySettings": {
    "allocation": 500.00,          // Amount to allocate
    "copyRatio": 1.0,               // Ratio to master (0.1 - 2.0)
    "maxSlippage": 5,               // Pips
    "stopLoss": 2.0,                // % of allocation
    "copyStopLoss": true,
    "copyTakeProfit": true,
    "pendingOrders": true           // Copy pending orders
  }
}
```

### Auto-Copy

Opsi untuk auto-copy semua trade baru:

```json
{
  "autoCopy": {
    "enabled": true,
    "minTradeSize": 0.1,           // Minimum lot to copy
    "maxTradeSize": 2.0,           // Maximum lot to copy
    "excludeNews": true            // Skip trades around news
  }
}
```

## Rating dan Review

### Rating

Follower dapat memberikan rating 1-5 bintang:

| Rating | Arti |
|--------|------|
| 5 | Excellent |
| 4 | Good |
| 3 | Average |
| 2 | Below Average |
| 1 | Poor |

### Review

Review teks juga dapat diberikan:

```json
{
  "review": {
    "rating": 5,
    "comment": "Strategi yang sangat konsisten. Drawdown rendah dan win rate tinggi.",
    "pros": ["Consistent returns", "Low drawdown"],
    "cons": ["Sometimes slow to adapt"]
  }
}
```

## Payout Provider

Provider earn money melalui:

1. **Performance Fee** — % dari profit yang dihasilkan.
2. **Subscription Fee** — biaya tetap per follower.
3. **Signal Fee** — biaya per signal yang dicopy.

Dashboard provider menampilkan semua earnings dan withdrawal options.
