---
description: "Strategy Health & Alpha Decay — deterministic decay detection ที่เปรียบเทียบ recent Sharpe ของ strategy กับ record ก่อนหน้าและหา mean-shift ที่ใหญ่ที่สุด (CUSUM change-point), คืน Healthy / Degrading / Decayed verdict"
---

# Strategy Health & Alpha Decay

ทุก edge decays — research ชี้ว่า half-life ของ quant strategy ลดจากปีเป็นเดือน
ดังนั้น *adaptation beats discovery* Strategy Health monitor บอกคุณจาก strategy's
return history ของตัวเองว่า edge ยังอยู่หรือไม่

เปิด **cBots → Strategy Health** (`/quant/health`)

## สิ่งที่มันทำ

กำหนด return series (หรือ equity curve, เก่าสุดก่อน), มัน:

- แยก history เป็น **earlier** และ **recent** halves และเปรียบเทียบ Sharpe ratios ของพวกเขา
- run **CUSUM change-point** scan เพื่อหาว่า observation ไหน mean เปลี่ยนชัดเจนที่สุด
  (regime break), reported เฉพาะเมื่อ deviation เป็น statistically notable
- คืน verdict:

| Verdict | ความหมาย |
|---|---|
| **Healthy** | Recent performance สอดคล้องกับ (หรือดีกว่า) record ก่อนหน้า |
| **Degrading** | Recent Sharpe อ่อนกว่า material กับ record ก่อนหน้า — watch closely |
| **Decayed** | Edge ได้หายไปอย่าง effective ใน recent window —พิจารณาหยุด |
| **Unknown** | ไม่มี history เพียงพอที่จะตัดสิน |

```http
POST /api/quant/health
{ "returns": [...] }   // หรือ { "equity": [...] }
```

## ทำไมมันถึงเชื่อถือได้

มันเป็น pure, deterministic domain code (`Core.Health`) ไม่มี infrastructure
dependency และไม่มี external calls — unit-tested สำหรับ decayed, degrading, healthy และ
too-short cases และสำหรับ change-point localization มันเป็น companion ด้วยตนเองกับ
always-on health checks ที่ back autonomous agents: statistics เดียวกันขับเคลื่อน
circuit breaker ที่ de-risks live strategy ที่ edge fading
