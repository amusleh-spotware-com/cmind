# COT cBot API

ข้อมูล Commitment of Traders จะถูกเปิดเผยต่อ cBots และไคลเอนต์ภายนอกผ่าน REST API ที่ผ่านการตรวจสอบ
เพื่อให้กลยุทธ์สามารถดึงข้อมูลการวางตำแหน่ง (ตำแหน่งสุทธิ, % ของดอกเบี้ยเปิด, ดัชนี COT) เป็นอินพุตสัญญาณ 
มันใช้ซ้ำ **เครื่องจักร JWT เดียวกันและขอบเขต `market:read`** เหมือนกับ API ตลาดสกุลเงิน — หนึ่งโทเค็น, หนึ่งโครงการ

## การตรวจสอบสิทธิ์

1. ในแอป ออกโทเค็นไคลเอนต์ข้อมูลตลาด (เจ้าของ) และให้ขอบเขต **`market:read`**
2. แลกเปลี่ยน ID/ลับของไคลเอนต์สำหรับโทเค็นตัวพกพาระยะสั้น:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   การตอบสนองมีโทเค็น `token`, `expiresAt` และขอบเขต `scopes` ที่ได้รับการอนุญาต
3. ส่งโทเค็นในทุกการโทร COT:

   ```http
   Authorization: Bearer <token>
   ```

โทเค็นที่หายไป/ไม่ถูกต้องส่งคืน `401`; โทเค็นที่ไม่มี `market:read` ส่งคืน `403`

## จุดสิ้นสุด

เส้นทางพื้นฐาน `/api/market/v1/cot` การตอบสนองทั้งหมดเป็น JSON

| วิธีการ & เส้นทาง | วัตถุประสงค์ |
|---------------|---------|
| `GET /markets` | แค็ตตาล็อกตลาดสัญญาติดตามตัวเลือก `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) และคำสำคัญ `q` |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | ภาพรวมรายสัปดาห์ล่าสุดสำหรับตลาด |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | ประวัติศาสตร์รายสัปดาห์ตลอดหน้าต่าง |

พารามิเตอร์:

- `code` — รหัสสัญญา CFTC ตลาด (เช่น `099741` สำหรับ Euro FX; รับจาก `/markets`)
- `kind` — `Legacy` (ค่าเริ่มต้น), `Disaggregated` หรือ `Tff`
- `combined` — `true` สำหรับฟิวเจอร์ส + ตัวเลือก, `false` (ค่าเริ่มต้น) เฉพาะฟิวเจอร์ส
- `asOf` (ISO-8601, ตัวเลือก) — สมอตำแหน่งเวลา: เพียงรายงานสาธารณะในช่วงเวลานั้นที่ส่งคืน
  ดังนั้นการทดสอบย้อนหลังจึงไม่เห็นการก้าวไปข้างหน้า

### ตัวอย่าง

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## เครื่องมือ MCP

โมเดลการอ่านเดียวกันมีให้ใช้กับไคลเอนต์ AI เป็นเครื่องมือ MCP: `CotMarkets`, `CotLatest`, `CotHistory`
และ `CotHealth` — แต่ละจุดเวลาที่ถูกต้องผ่าน `asOf` ที่เป็นตัวเลือก ดู
[คุณสมบัติ Commitment of Traders](./cot-report.md) สำหรับภาพรวมที่สมบูรณ์

## การล็อก

API อยู่หลังประตูสองชั้นเหมือนกับหน้า: `App:Branding:EnableCot` และ `App:Features:Cot`
ด้วยการปิดใช้งานใด ๆ ทรัพยากรทั้งหมดภายใต้ `/api/market/v1/cot` ส่งคืน `404`
