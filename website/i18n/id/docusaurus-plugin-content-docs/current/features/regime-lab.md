---
description: "Regime Lab — identifikasi dan trading perubahan kondisi pasar (trending, ranging, volatile)."
---

# Regime Lab

Regime Lab — identifikasi dan trading perubahan kondisi pasar (trending, ranging, volatile).

## Apa itu Market Regime?

Market regime adalah kondisi pasar yang mendasari:

| Regime | Karakteristik |
|--------|---------------|
| **Trending** | Price движется определенном направлении, higher highs/lows |
| **Ranging** | Price bergerak sideways, oscillates antara support/resistance |
| **Volatile** | High volatility, whipsaw, news-driven |
| **Quiet** | Low volatility, low volume, consolidation |

Strategi yang berbeda bekerja di regime berbeda:

```
Trending → Momentum strategies work
Ranging → Mean reversion strategies work
Volatile → Lower position sizes, wider stops
Quiet → Breakout strategies work
```

## Regime Detection

### Method 1: Trend Strength

```csharp
public Regime DetectRegime(OHLCV[] candles)
{
    var adx = CalculateADX(candles); // Average Directional Index

    if (adx > 25 && IsTrendingUp())
        return Regime.Trending;

    if (adx < 20)
        return Regime.Ranging;

    return Regime.Quiet;
}
```

### Method 2: Volatility-Based

```csharp
public Regime DetectRegime(OHLCV[] candles)
{
    var atr = CalculateATR(candles, 14);
    var sma = CalculateSMA(candles, 50);
    var atrPercent = atr / sma * 100;

    if (atrPercent > 2.0)
        return Regime.Volatile;

    if (atrPercent > 0.5)
        return Regime.Normal;

    return Regime.Quiet;
}
```

### Method 3: Market State

```csharp
public Regime DetectRegime(OHLCV[] candles)
{
    var HH = CountHigherHighs(candles);
    var LL = CountLowerLows(candles);
    var slope = CalculateLinearRegressionSlope(candles);

    if (HH > 2 && slope > 0)
        return Regime.TrendingUp;

    if (LL > 2 && slope < 0)
        return Regime.TrendingDown;

    return Regime.Ranging;
}
```

## Regime Trading

### Adaptive Strategy

Switch strategi berdasarkan regime:

```csharp
public class RegimeAdaptiveStrategy
{
    public async Task<Decision> Decide(OHLCV[] candles)
    {
        var regime = _regimeDetector.DetectRegime(candles);

        return regime switch
        {
            Regime.Trending => await TrendStrategy(candles),
            Regime.Ranging => await MeanReversionStrategy(candles),
            Regime.Volatile => await LowRiskStrategy(candles),
            _ => Decision.NoTrade()
        };
    }
}
```

### Position Sizing by Regime

Sesuaikan position sizing dengan regime:

```csharp
public decimal AdjustPositionSize(Regime regime, decimal baseSize)
{
    return regime switch
    {
        Regime.Trending => baseSize * 1.5,    // Bigger in trends
        Regime.Ranging => baseSize * 0.8,     // Smaller in range
        Regime.Volatile => baseSize * 0.5,    // Much smaller in volatile
        Regime.Quiet => baseSize * 0.7,       // Moderate in quiet
        _ => baseSize
    };
}
```

## Dashboard

### Regime Monitor

Menampilkan regime saat ini untuk setiap simbol:

| Symbol | Current Regime | Confidence | Trend Strength |
|--------|---------------|------------|----------------|
| EURUSD | Trending | 85% | 32 |
| GBPUSD | Ranging | 72% | 18 |
| XAUUSD | Volatile | 91% | 45 |

### Historical Regimes

Chart showing past regime changes:

- Color-coded background (green=trending, gray=ranging, red=volatile).
- Regime transition markers.
- Strategy performance per regime.

### Regime Forecast

AI-powered forecast:

```
Tomorrow's EURUSD regime: 60% Trending, 30% Ranging, 10% Volatile
```

## Backtesting with Regimes

### Regime-Aware Backtest

```csharp
public BacktestResult BacktestWithRegimes(Strategy s, OHLCV[] data)
{
    var results = new List<Trade>();
    var currentRegime = Regime.Unknown;

    foreach (var candle in data)
    {
        var newRegime = _detector.DetectRegime(GetRecentCandles(candle));
        if (newRegime != currentRegime)
        {
            currentRegime = newRegime;
            results.Add(RegimeChangeEvent(newRegime));
        }

        var decision = s.TradeInRegime(candle, currentRegime);
        results.Add(decision);
    }

    return CalculateResults(results);
}
```

### Regime Filter

Tambahkan filter regime ke strategi:

```json
{
  "regimeFilter": {
    "allowedRegimes": ["Trending", "Ranging"],
    "blockedRegimes": ["Volatile"],
    "minConfidence": 0.7
  }
}
```

## Best Practices

1. **Use multiple indicators** — tidak ada single indicator perfect.
2. **Consider timeframe** — regime berbeda per timeframe.
3. **Update regularly** — regime berubah, strategy harus adaptif.
4. **Combine with other tools** — regime + momentum + volume.
5. **Backtest thoroughly** — test strategy di semua regime.
