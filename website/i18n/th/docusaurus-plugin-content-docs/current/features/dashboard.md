---
title: Dashboard
description: cMind dashboard — live mobile-first command center สำหรับ cBot runs backtests resources และ node cluster ของคุณ
---

# Dashboard 📊

สิ่งแรก ที่ คุณเห็นเมื่อ sign ใน และ honestly page ที่ คุณ leave open ทั้งวัน landing page (`/` `Components/Pages/Index.razor`) เป็น **live mobile-first command center** สำหรับ signed-in user's activity ข้ามบน cBot runs backtests resources และ (สำหรับ admins) node cluster มันรีเฟรช ตัวเอง ดู great บน phone และ ไม่เคย make คุณ hit F5

## สิ่งที่มันแสดง

top เป็น bottom priority-ordered สำหรับ phone (ทุก ๆ block เป็น full-width stack item บน mobile responsive grid บน tablet/desktop):

1. **Header** — title live indicator (real pulsing dot; static ภายใต้ `prefers-reduced-motion`) last-updated time และ a **period toggle** (`1H · 24H · 7D · 30D`) ที่ drives KPIs และ chart
2. **Hero KPIs** — four glanceable cards ทุก ๆ big number + inline SVG sparkline และ (where meaningful) a **delta vs previous period**:
   - **Active ตอนนี้** — runs + backtests currently starting/running
   - **Success rate** — completed ÷ (completed + failed) ผ่านบน period; delta ใน percentage points
   - **Completed** — finished runs/backtests period นี้; delta vs previous period
   - **Failed** — failures period นี้; delta (fewer เป็น better ดังนั้น drop shows green)
3. **Activity chart** — ApexCharts area timeline ของ started / completed / failed per time bucket
4. **Instance status ring** — donut ของ running / backtests / pending / completed / failed total ใน centre
5. **Backtests** — three-tile snapshot (running / completed / failed) click-through เป็น `/backtest`
6. **Copy trading** — copy-trading profiles ของคุณ ด้วย live status dot destination count และ a **Live** badge บน running profiles; click-through เป็น `/copy-trading`
7. **AI agents** — persona-driven trading agents ของคุณ ด้วย run state (archetype · status) และ last-action time; click-through เป็น `/agent-studio`
8. **Live activity feed** — 20 most recent events (newest first) ด้วย status-coloured dot และ relative timestamp
9. **Cluster health** (admins เพียง) — active-vs-total nodes และ capacity-in-use gauge
10. **Resource tiles** — cBots trading accounts cTrader IDs MCP keys (click ผ่านไป their pages)

## Customize dashboard ของคุณ

ทุก ๆ block ด้านบน เป็น **widget ที่ คุณควบคุม** ทำให้ **Customize** (top-right ของ header) เพื่อ open dialog ที่ **show/hide** any widget และ **reorder** พวกเขา ด้วย up/down arrows **Reset เป็น default** restores catalog order choice ของคุณ **persisted server-side per user** ดังนั้นมันตามคุณ ข้าม browsers และ devices — ไม่ just tab นี้

- Feature-gated และ admin-only widgets (copy trading AI agents cluster health) เพียง ปรากฏใน dialog เมื่อ deployment/role ของคุณ สามารถ use พวกเขา
- widget catalog เป็น single source truth ใน `Core/Dashboard/DashboardWidgets.cs`; presentation (label + icon + availability) อยู่ใน `Components/Dashboard/DashboardWidgetMeta.cs`

## วิธี มันอยู่ live

page polls `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` ทุก ๆ 10 วินาที และ re-renders widgets in place — ไม่มี manual reload transient fetch failure swallowed และ retried บน next tick; loop stops cleanly บน dispose first load แสดง skeleton; persistent failure แสดง error card ด้วย **Retry**; user ด้วย no data sees zeroed KPIs และ empty-state copy

## Backend

- `Endpoints/DashboardEndpoints.cs` maps `/overview` (และ keeps older scalar `/stats`) มัน per-user และ admin-gated ผ่าน `ICurrentUser`; clock มาจาก `TimeProvider` มันด้วย maps `GET/PUT /api/dashboard/layout` — user's widget layout loaded บน page start และ saved จาก customize dialog
- **Layout persistence** เป็น `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`): one board per user (unique บน `UserId`) owning ordered list ของ widget settings (visible + order) stored เป็น `jsonb` column ordered list only ever mutated ผ่าน `Apply` / `Reset` ซึ่ง validate ทุก ๆ key ต้านแบบ `DashboardWidgets` catalog และ keep collection complete และ de-duplicated unknown keys rejected ด้วย `DomainException` → `400`
- `Endpoints/DashboardQuery.cs` builds composite `DashboardOverview` read model: all-time status snapshot (grouped counts) windowed set ของ instances materialized once และ resource/node counts instance status และ terminal timestamps live บน TPH subtypes (ไม่ columns) ดังนั้น rows read ใน memory ผ่าน shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers event time = `stopped ?? started ?? created`
- `Endpoints/DashboardModels.cs` holds DTOs period→(window bucket-count) plan และ `DashboardMath` — pure deterministic bucketing + KPI/delta math (no I/O `now` passed ใน)

KPI deltas compare current window ต้านแบบ immediately preceding one (query fetches double window สำหรับ นี้) มี **no live account P&L feed** — platform เพียง มี equity สำหรับ backtests และ prop-firm tracking — ดังนั้น dashboard deliberately *operational* (activity throughput success rate) ไม่ brokerage balance ticker

## Design & tokens

ทั้งหมด colour มาจาก design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`) ดังนั้น white-label palette ไหล ผ่านสำหรับ free — รวมถึง chart whose series colours read จาก resolved tokens ที่ runtime ผ่าน `window.appReadTokens` (SVG ไม่สามารถ consume CSS variables directly) ไม่มี hard-coded hex ที่ใด ๆ ใน dashboard ดู [../ui-guidelines.md](../ui-guidelines.md)

## "Powered by cMind" link

dashboard แสดง small tasteful **"Powered by cMind"** link ที่ points ไป documentation site นี้ มัน **shown โดย default** — เรา proud ของ project และ มันช่วย other traders หา — แต่ มัน entirely call ของคุณ resellers running fully white-labeled instance flip `App:Branding:ShowSiteLink` เป็น `false` และ มันหายไป ดู [White-label branding](./white-label.md#powered-by-link)

## Tests

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing success-rate previous-period deltas period parsing empty/boundary (event ที่ `now` divide-by-zero guard)
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: default seed apply order/visibility append-omitted duplicate-collapse unknown-key rejection reset
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs` `DashboardLayoutTests.cs`) — read model ต้านแบบ real Postgres (status/KPIs/activity/resources admin node health empty-user path) new backtests/copy-profiles/agents sections และ layout **round-trip** (save custom layout → reload → order + visibility persisted)
- **E2E** (`tests/E2ETests/DashboardTests.cs` `DashboardCustomizeTests.cs`) — desktop + mobile: KPI cards chart ring และ feed render; period toggle switches active period และ reloads; KPI drills ผ่านไป `/run`; **hiding widget persists ข้าม reload** **Reset** brings มันกลับ และ customize dialog ทำงาน บน phone ที่ no horizontal overflow `/` ด้วย ใน `PageSmokeTests` `MobileLayoutTests` (shell + no-overflow) และ `MobileJourneyTests`
