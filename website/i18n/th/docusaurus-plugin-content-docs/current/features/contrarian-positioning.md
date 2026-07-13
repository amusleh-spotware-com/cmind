---
description: "Contrarian Retail Positioning — turns the % of retail traders long into a contrarian bias (fade the crowd when it is lopsided), plus point-in-time signal value objects that guard against look-ahead bias."
---

# Contrarian Retail Positioning

retail crowd one ของ few genuinely useful sentiment signals ใน FX — เช่น **contrarian**
indicator เมื่อ great majority ของ retail traders long price historically tended ไป fall และ vice-versa tool นี้ turns crowd positioning ไป actionable read

open **cbots → contrarian positioning** (`/quant/positioning`)

## What มันdoes

enter **% ของ retail traders long** (จาก broker ของคุณ ของ sentiment page หรือ feed เช่น fxssi) และ
มัน returns:

- **contrarian bias** — **bearish** เมื่อ ≥ 60% long (crowd too long) **bullish** เมื่อ ≤ 40% long
  (crowd too short) **neutral** ใน 40–60% indecision band;
- **strength** — how lopsided crowd (0 = balanced 1 = fully one-sided) ไป weight signal

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time by construction

under hood signal layer (`Core.Signals`) models `PointInTimeSignal` ที่ **stamped ด้วย moment มัน
was knowable** และ refuses ไป constructed ไม่มี มัน any backtest หรือ autonomous agent ที่
consumes signal checks `IsKnownAt(decisionTime)` — ดังนั้น future data can never leak ไป historical
decision look-ahead bias top reproducibility killer ใน quant finance; domain model makes มัน
structurally impossible

## Why มันreliable

pure deterministic domain code ด้วย no infrastructure dependency — contrarian thresholds และ
point-in-time guard unit-tested รวม 40/60 boundaries และ out-of-range rejection
