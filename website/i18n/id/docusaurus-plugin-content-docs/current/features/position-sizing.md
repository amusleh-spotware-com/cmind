---
description: "Position sizing — hitung ukuran posisi optimal berdasarkan risk tolerance."
---

# Position Sizing

Position sizing — hitung ukuran posisi optimal berdasarkan risk tolerance.

## Mengapa Position Sizing Penting?

Position sizing adalah faktor paling penting dalam risk management:

```
Risk = Position Size × Distance to Stop Loss
```

Position sizing yang tepat memastikan:

- **Konsistensi** — risiko sama per trade.
- **Survival** — akun tidak blow up dari satu trade.
- **Growth** — akun tumbuh seiring waktu.

## Metode

### 1. Fixed Lot

Ukuran lot tetap untuk setiap trade:

```csharp
public class FixedLotSizer
{
    public decimal CalculateLotSize(PositionParams p)
    {
        return 0.5m; // Selalu 0.5 lot
    }
}
```

**Pro**: Simpel, predictable.
**Con**: Tidak adaptif terhadap volatilitas atau ukuran akun.

### 2. Fixed Percentage

% tetap dari equity per trade:

```csharp
public class FixedPercentageSizer
{
    public decimal CalculateLotSize(PositionParams p)
    {
        var riskAmount = p.AccountEquity * (p.RiskPercent / 100);
        var lotSize = riskAmount / (p.StopLossPips * p.PipValue);
        return Math.Round(lotSize, 2);
    }
}
```

**Pro**: Amount risk konstan sebagai % equity.
**Con**: Lot size bervariasi, bisa terlalu besar di akun kecil.

### 3. Kelly Criterion

Optimal sizing berdasarkan edge:

```csharp
public class KellySizer
{
    public decimal CalculateLotSize(PositionParams p)
    {
        var kelly = (p.WinRate * p.AvgWin) - (p.LossRate * p.AvgLoss);
        kelly /= p.AvgWin;
        var fraction = kelly / 2; // Half-Kelly untuk conservative
        return p.MaxLot * fraction;
    }
}
```

**Pro**: Mathematically optimal untuk long-run growth.
**Con**: Volatile, bisa oversized di akun kecil.

### 4. Volatility-Based

Sesuaikan dengan ATR (Average True Range):

```csharp
public class VolatilitySizer
{
    public decimal CalculateLotSize(PositionParams p)
    {
        var atr = p.GetAtr(14);
        var riskPips = atr * 2; // 2 ATR stop
        var riskAmount = p.AccountEquity * (p.RiskPercent / 100);
        var lotSize = riskAmount / (riskPips * p.PipValue);
        return Math.Min(lotSize, p.MaxLot);
    }
}
```

**Pro**: Adaptif terhadap kondisi pasar.
**Con**: Lebih kompleks.

### 5. Monte Carlo Simulation

Simulasi untuk find optimal sizing:

```csharp
public class MonteCarloSizer
{
    public decimal CalculateLotSize(PositionParams p)
    {
        var results = new List<decimal>();
        for (int i = 0; i < 10000; i++)
        {
            results.Add(SimulateTrades(p, 100));
        }
        // Return sizing yang memaksimalkan sharpe
        return FindOptimalSizing(results);
    }
}
```

## Konfigurasi

### Global Settings

```json
{
  "positionSizing": {
    "defaultMethod": "fixed-percentage",
    "riskPercent": 1.0,
    "maxLot": 10.0,
    "minLot": 0.01
  }
}
```

### Per-Strategy

```json
{
  "strategy": {
    "name": "Trend Follower",
    "sizing": {
      "method": "volatility",
      "atrMultiplier": 2.0,
      "riskPercent": 1.5
    }
  }
}
```

## Dashboard

Halaman **Position Sizing** menampilkan:

- **Current method** — metode yang aktif.
- **Risk analysis** — risk per trade dan aggregate.
- **Size recommendations** — suggested size untuk trade saat ini.
- **Historical performance** — performa berdasarkan sizing method.
- **Monte Carlo simulation** — hasil simulasi.

## Best Practices

1. **Start conservative** — 1% risk per trade untuk mulai.
2. **Adjust based on performance** — naikkan jika win rate tinggi.
3. **Consider costs** — slippage dan spread mempengaruhi sizing optimal.
4. **Review regularly** — backtest different methods.
5. **Combine with stop loss** — sizing tanpa stop loss = disaster.
