---
description: "Regime Lab — ติด label return series เป็น Calm / Normal / Turbulent volatility regimes และรายงาน per-regime performance, บวก Hurst exponent (trend-persistence vs mean-reversion) ดีเทอร์มินิสติก"
---

# Regime Lab

Sharpe ratio เดียวซ่อนความจริงที่ว่า edges ส่วนใหญ่เป็น conditional: ดีเยี่ยมในตลาด
ที่ calm, trending และตายใน turbulence (หรือกลับกัน) Regime Lab ทำลาย history ของ
strategy เป็น volatility regimes และแสดงว่ามันทำได้ดีแค่ไหนในแต่ละ regime — ดังนั้นคุณรู้
*เมื่อไหร่* edge ของคุณทำงานจริงๆ

เปิด **cBots → Regime Lab** (`/quant/regimes`)

## สิ่งที่มันทำ

กำหนด return series (หรือ equity curve, เก่าสุดก่อน), มัน:

- compute **trailing realized volatility** ที่แต่ละจุดและแยก history เป็น **Calm**,
  **Normal** และ **Turbulent** regimes โดย terciles ของ volatility นั้น
- รายงาน **per-regime performance** — observations, mean return, volatility และ Sharpe —
  ดังนั้นคุณเห็นว่า edge อยู่ที่ไหน
- ประมาณ **Hurst exponent** ผ่าน rescaled-range (R/S) analysis: เหนือ ~0.55 series
  เป็น **trending / persistent**, ต่ำกว่า ~0.45 มันเป็น **mean-reverting**, และรอบๆ 0.5
  มันใกล้ random walk

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // หรือ { "equity": [...] }
```

## ทำไมมันถึงเชื่อถือได้

มันเป็น pure, deterministic domain code (`Core.Regimes`) ไม่มี infrastructure
dependency และไม่มี external calls — unit-tested สำหรับ regime separation (calm vs
turbulent volatility) และสำหรับ Hurst direction (anti-persistent series score ต่ำกว่า 0.5,
persistent trend scores เหนือ) regime signal เดียวกัน feed autonomous agents' reflection
loop ดังนั้น agent สามารถ lean เข้าสู่ regimes ที่ edge ของมันเป็นจริง
