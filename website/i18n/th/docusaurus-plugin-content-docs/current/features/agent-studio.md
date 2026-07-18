---
description: "Agent Studio — สร้างเอเจนต์เทรดที่ขับเคลื่อนด้วยบุคลิกแบบ no-code พร้อมอักขระและอาร์ไทป์ที่จัดการบัญชีเพื่อบรรลุเป้าหมายของคุณภายใต้ Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, versioned disclaimer consent)"
---

# Agent Studio

Agent Studio ช่วยให้คุณสร้าง **เอเจนต์เทรดที่มีบุคลิก** — ไม่ต้องเขียนโค้ด — และมอบหมายให้มันจัดการบัญชีของคุณเพื่อบรรลุเป้าหมายที่วัดผลได้ เอเจนต์เปรียบเสมือน cBot ที่ขับเคลื่อนด้วยบุคลิก: คุณเลือกอาร์ไทป์และทัศนคติ ตั้งค่าขอบเขต และมันทำงานภายใต้ **Autonomy & Safety Kernel**

เปิด **AI → Agent Studio** (`/agent-studio`)

## สร้างเอเจนต์

**New agent** dialog รวบรวมข้อมูลแบบ no-code:

- **Name** และ **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion หรือ Breakout/Momentum แต่ละ preset กำหนดจังหวะและท่าทีที่เหมาะสม
- **Attitude** — สไลเดอร์ความก้าวร้าว ความอดทน และการตามเทรนด์
- **Managed account(s)** — **อย่างน้อยหนึ่งบัญชีจำเป็นต้องมีในการสร้างเอเจนต์** (เอเจนต์ที่ไม่มีบัญชีอาจไม่เคยเริ่มได้ ดังนั้น *Create* จึงยังคงปิดใช้งานจนกว่าคุณจะเลือกหนึ่ง) หากคุณยังไม่ได้ลิงก์บัญชีเทรดแล้ว dialog จะบอกดังกล่าวและชี้นำให้คุณลิงก์ก่อน
- **Autonomy level** — **Advisory** (เสนอเท่านั้น) หรือ **Approval-gated** (ดำเนินการหลังจากคุณอนุมัติต่อการกระทำ) **Full Auto** (ไม่มีการอนุมัติต่อการเทรด) ต้องการ **risk envelope** และยอมรับข้อสงวนสิทธิ์ก่อนจึงจะ arm ได้

บุคลิกภาพคอมไพล์ **แบบ deterministic** เป็น system prompt ของเอเจนต์ (ไม่มี LLM เขียนมัน) ดังนั้นการกำหนดค่าเดียวกันเสมอสร้างผลลัพธ์เดียวกัน — สามารถทำซ้ำได้และตรวจสอบได้

## รายการควบคุม

ทุกเอเจนต์แสดงในตารางควบคุม: **เอเจนต์ไหน ประเภทอะไร จัดการกี่บัญชี เป้าหมายอะไร สถานะการทำงาน และการกระทำล่าสุด** พร้อมปุ่มควบคุม **Start / Stop / Kill** Kill switch หยุดเอเจนต์ที่ทำงานอยู่ทันที

## ความปลอดภัยคือ domain invariant ไม่ใช่การตั้งค่า

ทุกอย่างที่เกี่ยวกับเงินต้องผ่าน **Autonomy & Safety Kernel**:

- **Risk envelope** — ขีดจำกัดต่อ order ที่เคร่งครัด (max daily loss, open exposure, position size, leverage,
  consecutive losses, orders/hour, allowed symbols) ทุก order ถูกตรวจสอบกับ envelope ก่อนส่ง
  การละเมิดจะถูกปฏิเสธไม่ใช่ถูก clamp ต้องการก่อนที่เอเจนต์จะเข้าถึง Full Auto
- **Circuit breaker** — หยุด risk ใหม่แบบ deterministic เมื่อ loss streak, daily-loss breach, **hard
  performance-goal breach** หรือ **AI-provider unavailability** (model ล่มหรือ hallucinating จะไม่เปิด position ใหม่)
- **Versioned disclaimer consent** — ต้องยอมรับแบบครั้งเดียวพร้อมเวอร์ชันก่อน arm Full Auto
  (ความยินยอมตามกฎหมาย ไม่ใช่การอนุมัติต่อการเทรด) การเปลี่ยน disclaimer จะบังคับให้ยอมรับใหม่
- **Kill switch** — การหยุดฉุกเฉิน idempotent บนทุกเอเจนต์ที่ทำงานอยู่

## เป้าหมาย

กำหนด **วัตถุประสงค์ที่วัดผลได้** ให้เอเจนต์ — เช่น *รักษา max drawdown ต่ำกว่า 4%*, *profit factor อย่างน้อย
1.5*, *win rate ≥ 55%* แต่ละเป้าหมายเป็น **Hard** (guardrail — การละเมิดทำให้ circuit breaker ทำงาน) หรือ
**Soft** (มีอิทธิพลต่อการให้เหตุผลเท่านั้น) ประเมินเป็น On-track / At-risk / Breached

## ไพพ์ไลน์ตัดสินใจ

เมื่อเริ่ม เอเจนต์ทำงาน **24/7 supervised loop** (`AgentRuntimeService`) แต่ละ tick สำหรับทุกบัญชีที่จัดการ
มัน: อ่าน **deterministic account state** (ground truth ไม่เคยเป็น memory ของ model)
ถาม decision engine หา move ส่งผ่าน **safety gate** (`AgentDecisionProcessor`) —
autonomy level → circuit breaker → risk envelope เขียน append-only **`AgentDecisionRecord`**
และหยุดหรือดำเนินการตามที่ gate กำหนด loop เป็น **fault-isolated** (เอเจนต์หนึ่งล้มไม่กระทบอีกตัวหรือ host)
และ **safe by default**: มัน inert นอกจาก AI ถูก configure *และ*
`App:Ai:AgentRuntimeEnabled` ถูกตั้ง และมันไม่เคยเปิด risk ใหม่ขณะที่ AI provider ไม่พร้อมใช้งาน

- **Approval gate** — order ที่เอเจนต์ **Approval-gated** เสนอจะถูกบันทึกเป็น **Pending** และไม่ทำอะไร
  จนกว่าเจ้าของจะอนุมัติ (`POST /api/agent-studio/{id}/decisions/{seq}/approve` หรือ
  `/reject`) **Full Auto** ผ่าน envelope โดยไม่มีการอนุมัติต่อการเทรด **Advisory** เสนอเท่านั้น
- **Audit ledger** — ทุกการตัดสินใจเล่นซ้ำได้: reasoning (XAI), หลักฐานที่อ้าง, gate verdict,
  order intent และว่าดำเนินการหรือไม่ ที่ `GET /api/agent-studio/{id}/decisions`
- **Research desk** — การโต้แย้ง multi-agent ตามคำขอ: Alpha/Sentiment/Technical/Risk analysts แต่ละคนให้มุมมอง
  และ Reviewer สังเคราะห์ข้อเสนอ (`POST /api/agent-studio/{id}/debate`)
- **Memory** — เอเจนต์จำทุกการตัดสินใจและ recall memory ล่าสุดเข้าสู่ prompt ถัดไปเพื่อต่อเนื่อง
  (`GET /api/agent-studio/{id}/memory`)

รายละเอียดของแต่ละแถวในรายการเปิด decision feed ของเอเจนต์ (พร้อม Approve/Reject บน pending orders),
memory และแท็บ Run-debate

## ขอบเขต

ที่ส่งมอบแล้ว: lifecycle ของเอเจนต์เต็มรูปแบบ, deterministic safety gate, 24/7 runtime,
human-in-the-loop approval gate, audit ledger และ **live cTrader Open API integration** —
account-state store (อ่าน balance, positions และ open exposure เป็น lots จริง) และ order executor
(วาง market orders จริง, lots→volume ผ่าน symbol lot size) ทั้งคู่ resolve OAuth credentials
ของทุกบัญชีที่จัดการและ degrade อย่างปลอดภัยเมื่อบัญชีไม่ได้ลิงก์ **ต้องการ Anthropic API key**
เพื่อให้ model สร้าง orders (จนกว่าจะถึงตอนนั้น engine จะค้าง) สิ่งที่จะตามมาคือ
multi-agent debate roles และ layered memory/reflection Runtime ปิดจนกว่า `App:Ai:AgentRuntimeEnabled`
จะถูกตั้ง ดังนั้น live trading เกิดขึ้นเมื่อ opt-in ที่ยินยอมอย่างชัดเจนเท่านั้น

## Managed accounts and editing

เมื่อสร้างเอเจนต์ คุณเลือกบัญชีเทรด **อย่างน้อยหนึ่งบัญชีจำเป็นต้องมีตั้งแต่ตอนสร้าง** (ปุ่ม *Create* ถูกปิดใช้งานจนกว่าจะเลือกหนึ่งบัญชี และ endpoint การสร้างปฏิเสธการเลือกแบบว่าง) ทุกเอเจนต์สามารถ **แก้ไข** ได้ในภายหลัง (ชื่อ ลักษณะนิสัย ระดับความเป็นอิสระ และบัญชีที่จัดการ) จากไอคอนดินสอบนแถวรายการ Lifecycle controls (details, edit, start, stop, kill) เป็นไอคอนปุ่ม แต่ละปุ่มถูกปิดใช้งานในสถานะที่ไม่สามารถใช้การกระทำได้
