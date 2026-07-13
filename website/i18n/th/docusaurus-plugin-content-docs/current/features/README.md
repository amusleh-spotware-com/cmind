---
slug: /features
title: ฟีเจอร์ — ツアรฉบับเต็ม
description: ทุกสิ่งที่ cMind สามารถทำได้ — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP และอื่น ๆ
sidebar_label: ภาพรวม
---

# ฟีเจอร์ — ツアรฉบับเต็ม 🧭

ยินดีต้อนรับสู่ツารฉบับเต็ม cMind บรรจุ *มาก* ในแอปเดียว ดังนั้นนี่คือแผนที่ ความสามารถแต่ละอย่างมีเอกสารเจาะลึกของตัวเอง — คลิกผ่านไปยังสิ่งที่คุณขูดขึ้น

## 🔁 การคัดลอกการค้นหา

เพชรมงกุฎ สะท้อนบัญชีอาจารย์ลงไปหลายโหนด และให้พวกเขาซิงค์แม้เมื่ออินเทอร์เน็ตทำให้เกิดอุปสรรค

- **[Copy trading](./copy-trading.md)** — แกน: mirroring, order types, SL/TP, slippage, desync/resync
- **[Execution transparency](./copy-execution-transparency.md)** — ดู exactly สิ่งที่ถูกคัดลอก เมื่อ และทำไม
- **[Performance fees](./copy-performance-fees.md)** — คิดค่าสำหรับสัญญาณของคุณ สไตล์ high-water-mark
- **[Provider marketplace](./copy-provider-marketplace.md)** — ให้ผู้ค้นหาค้นพบและติดตามผู้ให้บริการ
- **[Notifications](./copy-notifications.md)** — บอกเมื่อสิ่งใดเป็นสิ่งที่คุณต้องการ
- **[AI copy recommender](./ai-copy-recommender.md)** — ให้ AI แนะนำว่าใครจะติดตาม
- **[Open API token lifecycle](./token-lifecycle.md)** — วิธีที่ cMind ทำให้โทเค็นที่ถูกต้องเพียงอันเดียวต่อ cID

## 📊 ฐานแบบหน้าแรกของคุณ

- **[Dashboard](./dashboard.md)** — ศูนย์การควบคุมแบบเรียลไทม์มือถือก่อน: KPIs พร้อมเสาบาง แผนภูมิกิจกรรม วงแหวนสถานะ ฟีดสด และ (สำหรับผู้ดูแลระบบ) สุขภาพคลัสเตอร์ มันรีเฟรชตัวเอง

## 🧠 ไอ AI

ไม่ใช่กล่องแชทที่ปลายที่ไม่ถูกต้อง — AI ที่ทำให้ *งานจริง*

- **[AI assistant, agent, risk guard & alerts](./ai.md)** — การสร้างกลยุทธ์ การสร้างการซ่อมแซมตนเอง ยาม background ความเสี่ยงที่สามารถหยุด bots โดยอัตโนมัติ และการแจ้งเตือนอัจฉริยะ

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — ไอดี Monaco ในเบราว์เซอร์ แม่แบบ C#/Python สร้างแซนด์บ็อก และเส้นโค้งอิควิตี้สด
- **[MCP server](./mcp.md)** — เปิดเผยเครื่องมือของ cMind ผ่าน HTTP + SSE เพื่อให้ไคลเอ็นต์ AI สามารถควบคุมได้

## 🏢 รันมันเป็นธุรกิจ

- **[White-label / branding](./white-label.md)** — ปรับแต่งทุกพื้นผิวผ่านการกำหนดค่า
- **[Prop-firm challenge simulation](./prop-firm.md)** — บังคับใช้ daily-loss, drawdown และ target กฎด้วยอิควิตี้สด
- **[Feature toggles](./feature-toggles.md)** — ตัดสินใจว่าการจัดเตรียมแต่ละครั้ง/ผู้เช่าเห็นอะไร
- **[Compliance / legal](./compliance.md)** — การตรวจสอบและพื้นผิวทางกฎหมาย

## 📱 ประสบการณ์

- **[Installable app (PWA)](./pwa.md)** — มือถือก่อน เปลือกออฟไลน์ เพิ่มไปยังหน้าแรก
- **[UI design system & mobile-first](../ui-guidelines.md)** — โทเค็นการออกแบบและกฎเบื้องหลังลักษณะ

## ⚙️ ภายใต้ฝาครอบ

บิตปฏิบัติการที่ทำให้มันทั้งหมดทำงาน:

- **[Node fleet & discovery](../operations/node-discovery.md)** — วิธีการที่โหนดลงทะเบียนตนเองและหายไป
- **[Horizontal scaling](../deployment/scaling.md)** — เพิ่มจำนวนจำลอง ไม่จำเป็นต้องมีผู้ประสานงานภายนอก
- **[Logging & audit](../operations/logging.md)** — บันทึกโครงสร้าง + OpenTelemetry
- **[Deployment](../deployment/local.md)** — รันให้ทำงานทุกที่

:::note การเก็บเอกสารให้ซื่อสัตย์
ทุกเอกสารฟีเจอร์ได้รับการเก็บไว้ในลูกศรกับโค้ด — เปลี่ยนพฤติกรรม อัปเดตเอกสาร การ commit เดียวกัน หากคุณเคยเห็นการดริฟท์ นั่นคือบั๊ก: โปรดเปิด [ปัญหา](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) หรือส่ง PR 🙏
:::
