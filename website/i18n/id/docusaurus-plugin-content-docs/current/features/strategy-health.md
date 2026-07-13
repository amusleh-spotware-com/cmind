---
description: "Strategy Health — monitor kesehatan strategi trading dengan metrik risiko dan performa."
---

# Strategy Health

Strategy Health — monitor kesehatan strategi trading dengan metrik risiko dan performa.

## Overview

Strategy Health memberikan visibility ke kesehatan trading strategies:

- **Risk metrics** — exposure, drawdown, VaR.
- **Performance metrics** — Sharpe, win rate, profit factor.
- **Behavioral metrics** — trading frequency, position duration.
- **Anomaly detection** — deteksi perilaku tidak biasa.

## Metrik Utama

### Risk Metrics

| Metrik | Deskripsi | Batas |
|--------|-----------|-------|
| **Max Drawdown** | Drawdown maksimum | < 20% |
| **Current Drawdown** | Drawdown saat ini | < 10% |
| **Daily VaR** | Value at Risk harian | < 2% |
| **Exposure** | % equity di pasar | < 30% |
| **Leverage** | Effective leverage | < 10x |

### Performance Metrics

| Metrik | Deskripsi | Target |
|--------|-----------|--------|
| **Sharpe Ratio** | Risk-adjusted return | > 1.5 |
| **Profit Factor** | Gross profit / gross loss | > 1.5 |
| **Win Rate** | % trade profitable | > 50% |
| **Avg Win/Loss** | Rata-rata win vs loss | > 1.5 |
| **Recovery Factor** | Net profit / max drawdown | > 2.0 |

### Behavioral Metrics

| Metrik | Deskripsi | Batas Normal |
|--------|-----------|--------------|
| **Trades/Day** | Rata-rata trade per hari | 1-50 |
| **Avg Duration** | Rata-rata lama posisi | Contextual |
| **Max Duration** | Posisi terlama | < 1 week |
| **Orders/Hour** | Rate order | < 100 |

## Health Score

Score keseluruhan dari 0-100:

```
Health Score = (Risk Score × 0.4) + (Performance Score × 0.4) + (Behavioral Score × 0.2)
```

### Score Ranges

| Score | Status | Arti |
|-------|--------|------|
| 80-100 | Excellent | Strategi sangat sehat |
| 60-79 | Good | Sedikit perhatian dibutuhkan |
| 40-59 | Warning | Perlu review |
| 20-39 | Poor | Risk tinggi, perlu perhatian |
| 0-19 | Critical | Hentikan atau fix segera |

## Dashboard

### Health Overview

```
Strategy: Trend Follower Pro
Health Score: 78/100 (Good)

Risk Score: 82/100
  ✓ Max Drawdown: 8.5% (limit: 20%)
  ✓ Daily VaR: 1.2% (limit: 2%)
  ⚠ Exposure: 25% (limit: 30%)

Performance Score: 85/100
  ✓ Sharpe: 1.8 (target: 1.5)
  ✓ Win Rate: 58% (target: 50%)
  ✗ Profit Factor: 1.3 (target: 1.5)

Behavioral Score: 60/100
  ✓ Trades/Day: 5 (normal: 1-50)
  ⚠ Avg Duration: 4h (normally < 2h)
  ✗ Max Duration: 6d (limit: 1 week)
```

### Alerts

| Alert | Severity | Action |
|-------|----------|--------|
| Drawdown approaching limit | Warning | Review positions |
| VaR exceeded | Critical | Reduce exposure |
| Unusual trading frequency | Warning | Check for errors |
| Extended position duration | Warning | Review strategy logic |

## Anomaly Detection

### Statistical

```csharp
public bool IsAnomaly(Trade trade, StrategyStats stats)
{
    // Z-score untuk durasi
    var durationZ = (trade.Duration - stats.AvgDuration) / stats.DurationStdDev;
    if (Math.Abs(durationZ) > 3)
        return true;

    // Outlier untuk P&L
    var pnlZ = (trade.PnL - stats.AvgPnL) / stats.PnLStdDev;
    if (Math.Abs(pnlZ) > 3)
        return true;

    return false;
}
```

### Pattern Detection

- **Sudden changes** — perubahan tiba-tiba dalam metrik.
- **Trending issues** — drawdown yang terus-menerus naik.
- **Correlation breakdown** — korelasi antar trade berubah.
- **Frequency anomalies** — trading terlalu sering atau jarang.

## Alerts Configuration

### Per-Metric Thresholds

```json
{
  "alerts": {
    "maxDrawdown": {
      "warning": 15,
      "critical": 20
    },
    "dailyVaR": {
      "warning": 1.5,
      "critical": 2.0
    },
    "exposure": {
      "warning": 25,
      "critical": 30
    }
  }
}
```

### Notification Channels

| Channel | Alert Types |
|---------|-------------|
| In-app | All |
| Email | Warning + Critical |
| Webhook | Critical only |

## Integration

### With Backtest Integrity Lab

Strategy Health membaca hasil backtest:

```csharp
var integrityResult = await _integrityLab.AnalyzeAsync(backtestId);
var health = await _strategyHealth.CheckAsync(integrityResult);
```

### With Copy Trading

Health score ditampilkan di provider profile:

```
Provider: John Trader
Health Score: 82/100
Risk: Low (Score: 90)
Performance: Good (Score: 80)
```

### With AI Agent Runtime

Agent runtime memantau health:

```csharp
if (_healthScore < 40)
{
    // Hentikan agent
    await _agentRuntime.StopAsync(agentId, "Health score critical");
}
```
