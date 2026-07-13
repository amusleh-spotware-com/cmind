---
title: Dashboard
description: หน้า Dashboard ของ cMind — ศูนย์บัญชาการแบบ live และ mobile-first สำหรับ cBot runs, backtests, resources และ node cluster ของคุณ
---

# Dashboard

สิ่งแรกที่คุณเห็นเมื่อ sign in และเป็นหน้าที่คุณจะเปิดทิ้งไว้ตลอดวัน landing page
(`/`, `Components/Pages/Index.razor`) เป็น **live, mobile-first command center** สำหรับ
signed-in user ที่ทำกิจกรรมข้าม cBot runs, backtests, resources และ (สำหรับ admins) node
cluster มัน refresh ตัวเอง และดูดีบนมือถือ และไม่เคยบังคับให้คุณกด F5

## สิ่งที่มันแสดง

จากบนลงล่าง จัดลำดับความสำคัญสำหรับโทรศัพท์ (ทุก block เป็น full-width stack item บนมือถือ,
responsive grid บน tablet/desktop):

1. **Header** — title, live indicator (dot ที่เต้าจริง; static ภายใต้ `prefers-reduced-motion`),
   last-updated time และ **period toggle** (`1H · 24H · 7D · 30D`) ที่ขับเคลื่อน KPIs และ chart
2. **Hero KPIs** — สี่การ์ดที่อ่านได้ง่าย แต่ละอันเป็นตัวเลขใหญ่ + inline SVG sparkline และ
   (ที่สำคัญ) **delta vs period ก่อน**:
   - **Active now** — runs + backtests ที่กำลัง starting/running
   - **Success rate** — completed ÷ (completed + failed) ข้าม period; delta เป็น percentage points
   - **Completed** — finished runs/backtests period นี้; delta vs period ก่อน
   - **Failed** — failures period นี้; delta (น้อยกว่าดีกว่า ดังนั้น drop แสดงเป็นสีเขียว)
3. **Activity chart** — ApexCharts area timeline ของ started / completed / failed ต่อ time bucket
4. **Instance status ring** — donut ของ running / backtests / pending / completed / failed,
   ผลรวมอยู่ตรงกลาง
5. **Backtests** — สาม-tile snapshot (running / completed / failed), click-through ไปที่ `/backtest`
6. **Copy trading** — copy-trading profiles ของคุณพร้อม live status dot, destination count และ
   **Live** badge บน profiles ที่ทำงาน; click-through ไปที่ `/copy-trading`
7. **AI agents** — persona-driven trading agents ของคุณพร้อม run state (archetype · status)
   และ last-action time; click-through ไปที่ `/agent-studio`
8. **Live activity feed** — 20 events ล่าสุด (ใหม่สุดก่อน) พร้อม status-coloured dot และ
   relative timestamp
9. **Cluster health** (admins เท่านั้น) — active-vs-total nodes และ capacity-in-use gauge
10. **Resource tiles** — cBots, trading accounts, cTrader IDs, MCP keys (click through ไปยัง pages ของพวกเขา)

## Customize dashboard ของคุณ

ทุก block ข้างต้นเป็น **widget ที่คุณควบคุม** กด **Customize** (มุมขวาบนของ header)
เพื่อเปิด dialog ที่คุณ **show/hide** widget ใดก็ได้และ **reorder** ด้วย up/down arrows
**Reset to default** คืนค่า catalog order ทางเลือกของคุณ **persisted server-side ต่อ user**
ดังนั้นมันตามคุณข้าม browsers และ devices — ไม่ใช่แค่ tab นี้

- Feature-gated และ admin-only widgets (Copy trading, AI agents, Cluster health) แสดงใน dialog
  เมื่อ deployment/role ของคุณสามารถใช้ได้เท่านั้น
- Widget catalog เป็น single source of truth ใน `Core/Dashboard/DashboardWidgets.cs`;
  presentation (label + icon + availability) อยู่ใน `Components/Dashboard/DashboardWidgetMeta.cs`

## มันอัปเดตอย่างไร

หน้า poll `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` ทุก 10 วินาทีและ re-renders
widgets ในที่ — ไม่ต้อง reload ด้วยตนเอง fetch failure ชั่วคราวจะถูก swallowed และ retried
on next tick; loop หยุดอย่างสะอาดเมื่อ dispose First load แสดง skeleton; persistent failure
แสดง error card พร้อม **Retry**; user ที่ไม่มี data เห็น zeroed KPIs และ empty-state copy

## Backend

- `Endpoints/DashboardEndpoints.cs` map `/overview` (และเก็บ scalar `/stats` เวอร์ชันเก่า)
  เป็น per-user และ admin-gated ผ่าน `ICurrentUser`; clock มาจาก `TimeProvider` มันยัง map
  `GET/PUT /api/dashboard/layout` — widget layout ของ user, loaded on page start และ saved
  จาก Customize dialog
- **Layout persistence** คือ `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`):
  หนึ่ง board ต่อ user (unique บน `UserId`), owning ordered list ของ widget settings
  (visible + order) เก็บเป็น `jsonb` column ordered list ถูก mutate ผ่าน `Apply` / `Reset`
  เท่านั้น ซึ่ง validate ทุก key กับ `DashboardWidgets` catalog และ keep collection complete
  และ de-duplicated unknown keys ถูก reject ด้วย `DomainException` → `400`
- `Endpoints/DashboardQuery.cs` build composite `DashboardOverview` read model: all-time status
  snapshot (grouped counts), windowed set ของ instances materialized once และ resource/node counts
  Instance status และ terminal timestamps อยู่บน TPH subtypes (ไม่ใช่ columns) ดังนั้น rows
  ถูก read in memory ผ่าน shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers
  Event time = `stopped ?? started ?? created`
- `Endpoints/DashboardModels.cs` ถือ DTOs, period→(window, bucket-count) plan และ
  `DashboardMath` — pure, deterministic bucketing + KPI/delta math (ไม่มี I/O, `now`
  ถูกส่งเข้ามา)

KPI deltas เปรียบเทียบ current window กับ window ก่อนหน้านั้นทันที (query fetch
double window สำหรับสิ่งนี้) ไม่มี **live account P&L feed** — แพลตฟอร์มมี equity
สำหรับ backtests และ prop-firm tracking เท่านั้น ดังนั้น dashboard เป็นแบบ *operational*
โดยเจตนา (กิจกรรม, throughput, success rate) ไม่ใช่ brokerage balance ticker

## Design & tokens

สีทั้งหมดมาจาก design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`)
ดังนั้น white-label palette ไหลผ่านฟรี — รวมถึง chart ที่ series colours ถูกอ่านจาก
resolved tokens at runtime ผ่าน `window.appReadTokens` (SVG ไม่สามารถใช้ CSS variables
โดยตรง) ไม่มี hard-coded hex ที่ไหนบน dashboard ดู [../ui-guidelines.md](../ui-guidelines.md)

## ลิงก์ "Powered by cMind"

หน้า dashboard แสดง **"Powered by cMind"** link เล็กๆ ที่ชี้ไปยัง docs site นี้
มัน **แสดงโดย default** — เราภูมิใจในโปรเจกต์และช่วยให้ traders คนอื่นค้นพบมัน
แต่เป็นทางเลือกของคุณทั้งหมด resellers ที่ run fully white-labeled instance พลิก
`App:Branding:ShowSiteLink` เป็น `false` และมันหายไป ดู
[White-label branding](./white-label.md#powered-by-link)

## Tests

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event ที่ `now`, divide-by-zero guard)
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: default
  seed, apply order/visibility, append-omitted, duplicate-collapse, unknown-key rejection, reset
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) —
  read model ต่อ real Postgres (status/KPIs/activity/resources, admin node health, empty-user path),
  new backtests/copy-profiles/agents sections และ layout **round-trip** (save custom layout →
  reload → order + visibility persisted)
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile:
  KPI cards, chart, ring และ feed render; period toggle switches active period และ reloads;
  KPI drills through ไปยัง `/run`; **hiding a widget persists across a reload**, **Reset**
  คืนค่า และ Customize dialog ทำงานบนโทรศัพท์โดยไม่มี horizontal overflow `/` อยู่ใน
  `PageSmokeTests`, `MobileLayoutTests` (shell + no-overflow) และ `MobileJourneyTests`
