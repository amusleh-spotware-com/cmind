---
description: "Position sizing ในระดับสถาบันสำหรับ retail — volatility targeting และ fractional-Kelly exposure สำหรับ strategy เดียว, บวก inverse-volatility risk-parity allocation พร้อม correlation matrix ข้าม book ของ strategies"
---

# Position Sizing & Portfolio

"สัญญาณนี้ควรใหญ่แค่ไหน?" คือคำถามที่ตัดสินว่า edge จะ compound หรือ blow up
สถาบันตอบด้วย **volatility targeting** และ **Kelly criterion**, และพวกเขาสร้าง book ด้วย
**risk parity** มากกว่า equal dollars cMind นำทั้งสองมาสู่ retail — deterministic math บน
strategy's return series, พร้อม plain-English recommendation

เปิด **cBots → Position Sizing** (`/quant/sizing`)

## Single-strategy sizing

กำหนด strategy's returns (หรือ equity curve), target annual volatility, Kelly fraction และ
leverage cap, sizer รายงาน:

- **Realized annual volatility** — volatility ของ strategy เอง, annualized โดย square-root-of-time
  rule
- **Volatility-target sizing** — exposure ที่ทำให้ realized volatility ตรงกับ target ของคุณ
  (`target ÷ realized vol`), cap ที่ leverage limit ของคุณ strategy ที่มี volatility ต่ำกว่า
  ได้รับ size มากกว่า
- **Full Kelly** — fraction `f* = μ / σ²` ที่เป็น optimal สำหรับ growth (mean หาร variance
  ของ returns)
- **Fractional Kelly** — `f*` scale ด้วย Kelly fraction ของคุณ Half-Kelly (0.5) เป็นทางเลือก
  ที่ปลอดภัยที่ common; full Kelly เป็นที่รู้กันว่า aggressive เกินไปสำหรับ real, uncertain edges
- **Recommended exposure** — **เล็กกว่า** (ปลอดภัยกว่า) ของ volatility-target และ fractional-Kelly
  sizings, cap แล้ว strategy ที่ไม่มี positive edge (full Kelly ≤ 0) ถูก size เป็น **zero**

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allocation

ให้มันสองหรือมากกว่า strategies (aligned return series) และมันสร้าง book โดย
**inverse-volatility risk parity** — แต่ละ strategy ถูก weight โดย `1 / volatility`,
normalized — ดังนั้น risk ไม่ใช่ dollars ถูก share อย่างเท่าเทียม มันยังคืน:

- **correlation matrix** ข้าม strategies ของคุณ (หา strategies ที่เป็น bet เดียวกันอย่างลับๆ)
- **projected portfolio volatility** ที่ weights นั้น, จาก sample covariance
- **leverage** factor ที่ scale ทั้ง book ไปยัง target volatility ของคุณ (cap แล้ว)

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## ทำไมมันถึงเชื่อถือได้

ทั้งหมดเป็น pure, deterministic domain code (`Core.Portfolio`) ไม่มี infrastructure
dependency และไม่มี external calls — unit-tested สำหรับ vol-target scaling, Kelly formula,
equal-risk property ของ inverse-volatility weights และ correlation matrix Advisory โดย default:
ตัวเลขเป็น recommendation ไม่ใช่ automatic order
