---
description: "Regime detection — trend vs range, momentum vs mean-reversion market environment. Live + backtest overlay."
---

# Regime Lab

Market mode (trend, range, spike, crash?) shapes strategy. Regime Lab detects live.

## Módy

- **Trending** — strong directional bias (ADX > 25)
- **Range-bound** — choppy, price respects levels (BB squeeze)
- **Spike** — extreme volatility (ATR spike)
- **Crash** — panic selling

Computed from OHLC + technical indicators over window.

## API

GET /api/quant/regime — current market regime + confidence
GET /api/quant/regime/history — time-series by symbol

Backtest report overlay — see when strategy was in its best regime.

Gated na Backtesting feature.
