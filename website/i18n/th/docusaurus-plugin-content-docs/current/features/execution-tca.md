---
description: "Transaction Cost Analysis — วัดคุณภาพการ execution (slippage เป็น basis points และ implementation shortfall) ของ order เทียบกับ arrival price, compounding execution edge ที่ banks อยู่รอด ดีเทอร์มินิสติก"
---

# Transaction Cost Analysis (TCA)

Execution alpha เล็กมากต่อการเทรดแต่มหาศาลข้ามหลายพันครั้ง — มันเป็นส่วนใหญ่ของวิธีที่
banks และ prop desks � giữ lợi thế của họ TCA วัดว่าราคาที่คุณได้จริงๆ แยกจากราคาตอนที่คุณ *ตัดสินใจ* จะเทรด

เปิด **cBots → Execution Cost** (`/quant/tca`)

## สิ่งที่มันวัด

กำหนด **arrival (decision) price**, **side** และ **fills** ของคุณ (price × quantity) มันรายงาน:

- **Average fill price (VWAP)** — ราคาเฉลี่ยถ่วงน้ำหนักด้วย volume ที่คุณได้จริง
- **Slippage (bps)** — drift จาก arrival ไปยัง VWAP เป็น basis points, **signed เพื่อให้ค่าบวกคือ
  ค่าใช้จ่าย** (ซื้อเหนือ arrival หรือขายต่ำกว่า) และค่าลบคือ price improvement
- **Implementation shortfall** — ค่าใช้จ่ายนั้นแสดงเป็น price × quantity: เงินที่ drift ทำให้คุณ
  เสียบน order นี้

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

นอกเหนือจากวัดค่าใช้จ่าย, cMind สามารถวางแผน large order เพื่อ *minimise* มัน **cBots → Execution Schedule**
(`/quant/execution`) สร้าง **Almgren-Chriss optimal-execution schedule**: กำหนด total quantity,
จำนวน slices, risk aversion, volatility และ temporary market impact มันคืนขนาดที่จะ
trade ในแต่ละ slice risk aversion ที่สูงกว่า **front-loads** schedule (ลด timing risk);
risk aversion ที่ zero แผ่ออกเป็น **TWAP** เท่ากัน slices รวมเป็น total เสมอ

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## ทำไมมันถึงเชื่อถือได้

Pure, deterministic domain code (`Core.Execution`) ไม่มี infrastructure dependency และไม่มี
external calls — unit-tested สำหรับ buy/sell cost sign, price improvement, zero-slippage, VWAP
aggregation และ input guards นี่คือครึ่งวัดของ execution quality; มันคือ shortfall
metric เดียวกันที่ copy engine ใช้ตัดสิน (และด้วย smart slicing, ลด) ค่าใช้จ่ายของ
mirrored orders
