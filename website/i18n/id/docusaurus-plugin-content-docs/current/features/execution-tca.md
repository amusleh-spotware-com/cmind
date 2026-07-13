---
description: "Transaction Cost Analysis (TCA) — ukur dan analisis biaya eksekusi trading."
---

# Execution TCA

Transaction Cost Analysis (TCA) — ukur dan analisis biaya eksekusi trading.

## Apa itu TCA?

TCA mengukur biaya yang Anda bayar untuk mengeksekusi trade, selain dari spread. Ini termasuk:

- **Slippage** — perbedaan antara harga yang diharapkan dan harga aktual.
- **Market impact** — bagaimana trade Anda mempengaruhi harga.
- **Timing cost** — biaya tertunda eksekusi.
- **Commission** — biaya broker/exchange.

## Mengapa TCA Penting?

Even dengan spread ketat, biaya tersembunyi bisa makan profit:

```
Total Cost = Spread + Slippage + Market Impact + Commission
           = 1 pip + 0.5 pip + 0.3 pip + 0.2 pip
           = 2.0 pips
```

Untuk strategi high-frequency, ini perbedaan antara profit dan loss.

## Metrics

### Pre-Trade

| Metric | Deskripsi |
|--------|-----------|
| **Estimated Cost** | Estimasi biaya sebelum eksekusi |
| **Market Impact** | Estimasi dampak pasar |
| **Liquidity Score** | Kedalaman liquiditas |

### Post-Trade

| Metric | Deskripsi |
|--------|-----------|
| **Realized Slippage** | Slippage aktual |
| **Implementation Shortfall** | Cost dari price movement selama eksekusi |
| **VWAP Diff** | Deviasi dari volume-weighted average price |
| **Arrival Price** | Harga saat order submitted |

### Aggregate

| Metric | Deskripsi |
|--------|-----------|
| **Avg Slippage** | Rata-rata slippage per trade |
| **Cost per Pip** | Biaya per pip |
| **Cost as % of Value** | Biaya sebagai % dari nilai trade |
| **Fill Rate** | % order yang terisi |

## Dashboard

### Overview

- **Total costs** — akumulasi biaya semua trade.
- **Cost breakdown** — pie chart komponen biaya.
- **Trend** — biaya seiring waktu.
- **Benchmark** — dibandingkan dengan VWAP market.

### Per-Instrument

- **EURUSD** — biaya terendah.
- **XAUUSD** — biaya tertinggi karena volatilitas.
- **US500** — biaya متوسط.

### Per-Broker

- **Broker A** — avg slippage 0.3 pips.
- **Broker B** — avg slippage 0.5 pips.

## Best Practices

1. **Monitor terus** — TCA bukan one-time analysis.
2. **Compare brokers** — gunakan broker dengan biaya rendah.
3. **Optimize timing** — hindari waktu volatilitas tinggi.
4. **Review regularly** — periksa TCA mingguan.

## API

```http
GET /api/tca/report?from=2024-01-01&to=2024-01-31
```

```json
{
  "totalCost": 1250.00,
  "costPerTrade": 2.50,
  "slippage": {
    "avg": 0.3,
    "max": 1.5,
    "min": 0.0
  },
  "implementationShortfall": 0.8,
  "breakdown": {
    "spread": 800.00,
    "slippage": 300.00,
    "commission": 150.00
  }
}
```
