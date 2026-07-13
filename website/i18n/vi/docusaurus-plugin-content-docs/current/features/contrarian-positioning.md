---
description: "Contrarian Retail Positioning — turns the % of retail traders long into a contrarian bias (fade the crowd when it is lopsided), plus point-in-time signal value objects that guard against look-ahead bias."
---

# Contrarian Retail Positioning

The retail crowd is one of the few genuinely useful sentiment signals in FX — as a **contrarian**
indicator. When the great majority of retail traders are long, price has historically tended to fall,
and vice-versa. This tool turns crowd positioning into an actionable read.

Open **cBots → Contrarian Positioning** (`/quant/positioning`).

## What it does

Enter the **% of retail traders long** (from your broker's sentiment page or a feed such as FXSSI) and
it returns:

- **Contrarian bias** — **Bearish** when ≥ 60% are long (crowd too long), **Bullish** when ≤ 40% are
  long (crowd too short), **Neutral** in the 40–60% indecision band;
- **Strength** — how lopsided the crowd is (0 = balanced, 1 = fully one-sided), to weight the signal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time by construction

Under the hood the signal layer (`Core.Signals`) models a `PointInTimeSignal` that is **stamped with the
moment it was knowable** and refuses to be constructed without it. Any backtest or autonomous agent that
consumes a signal checks `IsKnownAt(decisionTime)` — so future data can never leak into a historical
decision. Look-ahead bias is the top reproducibility killer in quant finance; the domain model makes it
structurally impossible.

## Why it is reliable

Pure, deterministic domain code with no infrastructure dependency — the contrarian thresholds and the
point-in-time guard are unit-tested, including the 40/60 boundaries and out-of-range rejection.
