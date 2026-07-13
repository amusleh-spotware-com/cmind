---
description: "Trading journal — catat, analisis, dan evaluasi trade dengan statistik dan visualisasi."
---

# Trading Journal

Trading journal — catat, analisis, dan evaluasi trade dengan statistik dan visualisasi.

## Overview

Trading journal menyimpan semua trade Anda dengan detail lengkap:

- **Entry/Exit** — harga, waktu, alasan.
- **P&L** — profit/loss, fees, net.
- **Metadata** — strategi, simbol, timeframe.
- **Review** — self-assessment dan perbaikan.

## Pencatatan Trade

### Manual Entry

```json
{
  "trade": {
    "symbol": "EURUSD",
    "direction": "buy",
    "entryPrice": 1.0850,
    "entryTime": "2024-01-15T10:00:00Z",
    "exitPrice": 1.0900,
    "exitTime": "2024-01-15T14:30:00Z",
    "lots": 0.5,
    "strategy": "Trend Follower",
    "timeframe": "H4",
    "notes": "Breakout di resistance 1.0830"
  }
}
```

### Auto-Capture

Trade dari platform trading langsung ditangkap:

```csharp
public async Task CaptureTradeAsync(Trade trade)
{
    trade.AccountId = _currentUser.GetAccountId();
    trade.CapturedAt = DateTime.UtcNow;
    await _db.Trades.AddAsync(trade);
}
```

## Analisis

### Performa Overview

| Metrik | Nilai |
|--------|-------|
| **Total Trades** | 150 |
| **Win Rate** | 58% |
| **Profit Factor** | 1.8 |
| **Avg Win** | $125 |
| **Avg Loss** | -$85 |
| **Net Profit** | $4,250 |
| **Max Drawdown** | 8% |
| **Sharpe Ratio** | 1.95 |

### Visualisasi

#### Equity Curve

Chart equity seiring waktu dengan drawdown overlay:

```
Equity ($)
  │
5000 ┤                    ╱╲
     │               ╱╲╱  ╲
4000 ┤          ╱╲╱      ╲
     │     ╱╲╱            ╲
3000 ┤╱╲╱                    ╲
     └──────────────────────────→ Time
```

#### Monthly Returns

Heatmap return bulanan:

| Month | 2023 | 2024 |
|-------|------|------|
| Jan | +3.2% | +2.8% |
| Feb | -1.5% | +4.1% |
| Mar | +2.1% | +1.2% |

#### Win/Loss Distribution

Histogram win dan loss:

```
Count
  │
 20 ┤  █
    │  █ █
 15 ┤  █ █ █
    │  █ █ █
 10 ┤  █ █ █ █
    │  █ █ █ █
  5 ┤  █ █ █ █ █
    │  █ █ █ █ █ █
    └──────────────────→ P&L ($)
     -100  0  100  200
```

## Statistik Lanjutan

### By Symbol

| Symbol | Trades | Win Rate | Net P&L |
|--------|--------|----------|---------|
| EURUSD | 80 | 62% | +$2,500 |
| GBPUSD | 35 | 54% | +$950 |
| XAUUSD | 25 | 48% | -$200 |
| US500 | 10 | 60% | +$1,000 |

### By Strategy

| Strategy | Trades | Win Rate | Avg P&L | Sharpe |
|----------|--------|----------|---------|--------|
| Trend Follower | 60 | 55% | +$85 | 1.8 |
| Mean Reversion | 45 | 62% | +$65 | 1.5 |
| Breakout | 30 | 50% | +$40 | 1.2 |
| Scalping | 15 | 67% | +$25 | 2.1 |

### By Timeframe

| Timeframe | Trades | Win Rate | Avg Duration | Net P&L |
|-----------|--------|----------|--------------|---------|
| M5 | 40 | 52% | 15 min | +$800 |
| H1 | 50 | 58% | 2 hours | +$2,000 |
| H4 | 40 | 65% | 8 hours | +$1,800 |
| D1 | 20 | 60% | 2 days | -$350 |

## Journal Entry Review

### Trade Review Form

Setelah setiap trade, isi review:

```json
{
  "review": {
    "tradeId": "TRD-123",
    "rating": 4,
    "whatWentWell": "Entry timing bagus, managed stop dengan baik",
    "whatCouldBeBetter": "Should have taken profit earlier at 1.0920",
    "lessonLearned": "Ada resistance di 1.0900, take profit di sana",
    "adjustmentForNextTime": "，下次在 1.0900 设置 TP"
  }
}
```

### Pattern Identification

AI mengidentifikasi pattern dari journal:

```
Pattern Detected:
"Your winning trades tend to be in the direction of the daily trend (73% win rate vs 45% when counter-trend)."
```

## Export

### CSV

```bash
curl -H "Authorization: Bearer <token>" \
  "https://api.cmind/journal/export?format=csv&from=2024-01-01&to=2024-01-31" \
  -o journal.csv
```

### JSON

```bash
curl -H "Authorization: Bearer <token>" \
  "https://api.cmind/journal/export?format=json" \
  -o journal.json
```

## Integration

### With Backtest Results

Compare live trading dengan backtest:

```json
{
  "comparison": {
    "backtestSharpe": 2.1,
    "liveSharpe": 1.8,
    "backtestWinRate": "60%",
    "liveWinRate": "58%",
    "conclusion": "Live performance close to backtest, strategy is robust"
  }
}
```

### With Strategy Health

Journal data feed ke Strategy Health metrics:

```
Strategy Health uses journal data to calculate:
- Realized win rate and profit factor
- Drawdown from closed trades
- Trade frequency and duration
```
