---
description: "Trading Journal & Coach — วิเคราะห์ runs และ backtests ของคุณเองสำหรับ behavioural leaks (over-concentration, repeated failures, losing bias) และ coach คุณบน strategy ที่คุณมีอยู่แล้ว ดีเทอร์มินิสติก, พร้อม AI narrative แบบ optional"
---

# Trading Journal & Coach

หมวดใหม่ที่ genuinely-useful ของ AI-for-trading ไม่ใช่การ predict ตลาด — มันคือการวิเคราะห์
*พฤติกรรมของคุณเอง* Trading Journal เปลี่ยน history ของ runs และ backtests ของคุณเป็น
honest feedback ดังนั้นคุณสามารถปรับปรุง strategy ที่คุณมีอยู่แล้ว

เปิด **AI → Trading Journal** (`/journal`)

## สิ่งที่มันเผย

จาก instances ของคุณ (runs และ backtests) มัน compute แบบ deterministic:

- **Win / loss / failure counts และ win rate** ข้าม backtests ของคุณ
- **Behavioural insights** — leaks ที่ cost retail traders โดยเงียบ:
  - **Over-concentration** — กิจกรรมส่วนใหญ่ของคุณอยู่ในหนึ่ง symbol
  - **Repeated failures** — ส่วนแบ่งสูงของ runs failed ที่จะ build หรือ configure
  - **Losing bias** — backtests ที่เสียมากกว่าชนะ (พร้อม nudge ให้ run Integrity Lab และ
    check ว่า edge เป็นจริง)
  - สุขภาพดีเมื่อไม่มีข้างต้น apply

```http
GET /api/journal
```

## ทำไมมันถึงเชื่อถือได้

behavioural analysis เป็น pure, deterministic domain code (`Core.Journal`) ไม่มี
infrastructure dependency — unit-tested สำหรับ over-concentration, repeated failures,
losing bias, the balanced case และ empty account facts มาก่อน; AI coach (Portfolio
Digest) เป็น optional narrative layer บน, gated บน Anthropic API key, ดังนั้น journal
ทำงานเต็มที่โดยไม่มี AI configure
