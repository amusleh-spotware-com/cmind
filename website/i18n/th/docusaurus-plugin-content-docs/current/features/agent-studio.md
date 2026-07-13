---
description: "Agent Studio — สร้าง persona-driven, no-code trading agents ที่มีตัวละครและ archetype ที่จัดการบัญชีตามเป้าหมายของคุณภายใต้ Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, versioned disclaimer consent)"
---

# Agent Studio

Agent Studio ช่วยให้คุณสร้าง **trading agent ที่มีตัวละคร** — ไม่มีโค้ด — และให้การจัดการบัญชีของคุณตามเป้าหมายที่วัดได้ เอเจนต์เป็นเหมือน cBot ที่ขับเคลื่อนด้วย persona: คุณเลือก archetype และทัศนคติ ตั้งค่า guardrails และมันจะทำงานภายใต้ **Autonomy & Safety Kernel**

เปิด **AI → Agent Studio** (`/agent-studio`)

## สร้างเอเจนต์

กล่องโต้ตอบ **New agent** รวบรวม no-code:

- **Name** และ **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader, Contrarian, Mean Reversion หรือ Breakout/Momentum แต่ละ preset ยึดติด cadence และ posture ที่เข้าใจได้
- **Attitude** — sliders aggressiveness, patience และ trend-following
- **Autonomy level** — **Advisory** (เสนอเท่านั้น) หรือ **Approval-gated** (ทำการกระทำเท่านั้นหลังจากการอนุมัติต่อการกระทำของคุณ) **Full Auto** (ไม่มีการอนุมัติต่อการค้นหา) นอกจากนี้ยังต้องมี **risk envelope** และการยอมรับข้อปฏิเสธความเสี่ยงก่อนที่มันจะสามารถติดอาวุธได้

persona คอมไพล์ **deterministically** เป็นระบบ prompt ของเอเจนต์ (ไม่มี LLM ผู้แต่ง) ดังนั้นการกำหนดค่าเดียวกันจะให้คำแนะนำเดียวกัน — reproducible และ auditable

## หน้ารายการ

ทุกเอเจนต์แสดงในตาราห้องควบคุม: **agent ที่อยู่, type ของมัน, จำนวนบัญชีที่จัดการ เป้าหมาย run status และการกระทำครั้งล่าสุด** พร้อมกับ **Start / Stop / Kill** controls Kill switch หยุด agent ที่กำลังทำงาน

## ความปลอดภัยเป็นค่าไม่แปรเปลี่ยนของโดเมน ไม่ใช่การตั้งค่า

สิ่งทั้งหมดที่เกี่ยวข้องกับเงินจะไปผ่าน **Autonomy & Safety Kernel**:

- **Risk envelope** — hard per-order limits (max daily loss, open exposure, position size, leverage, consecutive losses, orders/hour, allowed symbols) ทุกคำสั่งได้รับการตรวจสอบกับมันก่อน dispatch; breach จะถูกปฏิเสธ ไม่ใช่ clamped จำเป็นก่อนที่เอเจนต์จะสามารถถึง Full Auto
- **Circuit breaker** — halts deterministically new risk บน loss streak, daily-loss breach, **hard performance-goal breach** หรือ **AI-provider unavailability** (down หรือ hallucinating model ไม่เคยเปิด fresh positions)
- **Versioned disclaimer consent** — การยอมรับ one-time, versioned จำเป็นต้องติดอาวุธ Full Auto (legally-required consent ไม่ใช่ per-trade approval); bumping disclaimer forces re-consent
- **Kill switch** — halts ฉุกเฉินที่เป็น idempotent บน agent ที่ทำงานทุกตัว

## เป้าหมาย

ให้เอเจนต์ **measurable objectives** — เช่น *ให้ max drawdown ด้านล่าง 4%*, *profit factor อย่างน้อย 1.5*, *win rate ≥ 55%* แต่ละเป้าหมายคือ **Hard** (guardrail — breach trips circuit breaker) หรือ **Soft** (steers reasoning เท่านั้น) ประเมินเป็น On-track / At-risk / Breached

## pipeline การตัดสินใจ

เมื่อเริ่มต้น เอเจนต์จะเรียกใช้ **24/7 supervised loop** (`AgentRuntimeService`) ในแต่ละ tick สำหรับทุกบัญชีที่จัดการ มันจะ: อ่าน **deterministic account state** (ground truth ไม่เคยเป็นหน่วยความจำของแบบจำลอง); ขอ decision engine สำหรับการย้าย; ส่งผ่าน **safety gate** (`AgentDecisionProcessor`) — autonomy level → circuit breaker → risk envelope; เขียน append-only **`AgentDecisionRecord`**; และหยุดหรือทำการใช้งานตามที่ gate ขอ loop คือ **fault-isolated** (failure ของเอเจนต์หนึ่งไม่เคยสัมผัสเอเจนต์อื่นหรือโฮสต์) และ **safe by default**: มันเป็นอเนกประสงค์เว้นแต่ AI ได้รับการกำหนดค่า *และ* `App:Ai:AgentRuntimeEnabled` ถูกตั้งค่า และไม่เคยเปิด fresh risk ในขณะที่ AI provider ไม่พร้อมใช้งาน

- **Approval gate** — agent **Approval-gated** ของ proposed order จะถูกบันทึกเป็น **Pending** และไม่ทำอะไรจนกว่าเจ้าของจะอนุมัติ (`POST /api/agent-studio/{id}/decisions/{seq}/approve` หรือ `/reject`); **Full Auto** clears ผ่าน envelope โดยไม่มี per-trade approval; **Advisory** เสนอเท่านั้น
- **Audit ledger** — ทุกการตัดสินใจสามารถ replayable: reasoning (XAI), evidence ที่อ้างถึง gate verdict order intent และว่ามันดำเนิน at `GET /api/agent-studio/{id}/decisions`
- **Research desk** — multi-agent debate on-demand: Alpha/Sentiment/Technical/Risk analysts แต่ละคนให้มุมมองและ Reviewer synthesises proposal (`POST /api/agent-studio/{id}/debate`)
- **Memory** — agent จำแต่ละการตัดสินใจและจำความจำล่าสุดเข้าไป prompt ถัดไปของมันสำหรับความต่อเนื่อง (`GET /api/agent-studio/{id}/memory`)

ทุกรายการ roster row ของ **Details** เปิด decision feed ของเอเจนต์ (พร้อม Approve/Reject บน pending orders) memory ของมัน และแท็บ Run-debate

## ขอบเขต

Shipped: agent lifecycle ฉบับเต็ม deterministic safety gate 24/7 runtime human-in-the-loop approval gate audit ledger และ **live cTrader Open API integration** — account-state store (reads real balance, positions และ open exposure in lots) และ order executor (places real market orders, lots→volume ผ่าน symbol lot size) ทั้งสอง resolving managed account ของแต่ละ OAuth credentials และ degrading safely เมื่อบัญชีไม่ได้ linked **ต้องใช้ Anthropic API key** สำหรับแบบจำลอง generate orders (จนกว่า engine จะเก็บ); ยังคงต้อง multi-agent debate roles และ layered memory/reflection runtime ปิดเว้นแต่ `App:Ai:AgentRuntimeEnabled` ถูกตั้งค่า ดังนั้นการค้นหาแบบ live เกิดขึ้นเฉพาะในการออปต์อิน fully-consented ที่ชัดแจ้ง
