---
title: Dashboard
description: Dashboard cMind — một trung tâm chỉ huy mobile-first, live cho các lần chạy cBot, backtest, tài nguyên và node cluster của bạn.
---

# Dashboard 📊

Điều đầu tiên bạn thấy khi đăng nhập, và thành thật mà nói thì đây là trang bạn sẽ để mở cả ngày. Trang
landing (`/`, `Components/Pages/Index.razor`) là một **trung tâm chỉ huy mobile-first, live** cho hoạt động
của user đã đăng nhập qua các lần chạy cBot, backtest, tài nguyên và (cho admins) node
cluster. Nó tự làm mới, nhìn tuyệt vời trên điện thoại, và không bao giờ khiến bạn phải nhấn F5.

## Nó hiển thị gì

Từ trên xuống dưới, theo thứ tự ưu tiên cho điện thoại (mỗi block là một stack item full-width trên mobile, một
responsive grid trên tablet/desktop):

1. **Header** — tiêu đề, một live indicator (một chấm pulsing thực sự; static dưới `prefers-reduced-motion`),
   thời gian last-updated, và một **period toggle** (`1H · 24H · 7D · 30D`) điều khiển KPIs và chart.
2. **Hero KPIs** — bốn thẻ có thể nhìn nhanh, mỗi cái là một số lớn + một inline SVG sparkline, và (ở đâu
   có ý nghĩa) một **delta so với kỳ trước**:
   - **Active now** — các lần chạy + backtest đang bắt đầu/chạy.
   - **Success rate** — completed ÷ (completed + failed) trong kỳ; delta bằng điểm phần trăm.
   - **Completed** — các lần chạy/backtest đã hoàn thành trong kỳ này; delta so với kỳ trước.
   - **Failed** — các lần thất bại trong kỳ này; delta (ít hơn thì tốt hơn, nên giảm thì hiển thị màu xanh).
3. **Activity chart** — một ApexCharts area timeline của started / completed / failed per time bucket.
4. **Instance status ring** — một donut của running / backtests / pending / completed / failed, tổng ở
   centre.
5. **Backtests** — một snapshot ba-tile (running / completed / failed), click-through đến `/backtest`.
6. **Copy trading** — các profile copy-trading của bạn với một live status dot, destination count, và một **Live**
   badge trên các profile đang chạy; click-through đến `/copy-trading`.
7. **AI agents** — các trading agent theo persona của bạn với run state (archetype · status) và last-action
   time; click-through đến `/agent-studio`.
8. **Live activity feed** — 20 sự kiện gần nhất (mới nhất trước) với một status-coloured dot và một
   relative timestamp.
9. **Cluster health** (chỉ admins) — active-vs-total nodes và một capacity-in-use gauge.
10. **Resource tiles** — cBots, tài khoản giao dịch, cTrader IDs, MCP keys (click through đến các trang của chúng).

## Tùy chỉnh dashboard của bạn

Mỗi block ở trên là một **widget bạn kiểm soát**. Nhấn **Customize** (góc trên bên phải của header) để mở một
dialog nơi bạn **show/hide** bất kỳ widget nào và **reorder** chúng bằng các mũi tên lên/xuống. **Reset to default**
khôi phục thứ tự catalog. Lựa chọn của bạn **được persist server-side per user**, vì vậy nó đi theo bạn across
browsers và devices — không chỉ tab này.

- Widget feature-gated và admin-only (Copy trading, AI agents, Cluster health) chỉ xuất hiện trong
  dialog khi deployment/role của bạn có thể sử dụng chúng.
- Widget catalog là một single source of truth trong `Core/Dashboard/DashboardWidgets.cs`; presentation
  (label + icon + availability) nằm trong `Components/Dashboard/DashboardWidgetMeta.cs`.

## Cách nó stay live

Trang poll `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` mỗi 10 giây và re-renders các
widgets tại chỗ — không reload thủ công. Một transient fetch failure được swallow và retried on next tick;
loop dừng cleanly on dispose. First load hiển thị skeleton; persistent failure hiển thị error
card với **Retry**; một user không có data thấy zeroed KPIs và empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` maps `/overview` (và giữ các scalar `/stats` cũ hơn). Nó là
  per-user và admin-gated via `ICurrentUser`; clock đến từ `TimeProvider`. Nó cũng maps
  `GET/PUT /api/dashboard/layout` — widget layout của user, loaded on page start và saved from the
  Customize dialog.
- **Layout persistence** là `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`): một board
  per user (unique on `UserId`), owning một ordered list của widget settings (visible + order) stored as a
  `jsonb` column. Ordered list chỉ được mutate through `Apply` / `Reset`, which validate mọi key
  against `DashboardWidgets` catalog và keep collection complete and de-duplicated. Unknown keys bị reject với `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` builds the composite `DashboardOverview` read model: an all-time status
  snapshot (grouped counts), a windowed set của instances materialized once, và resource/node counts.
  Instance status và terminal timestamps sống trên TPH subtypes (không phải columns), vì vậy rows được read in memory
  via shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers. Event time =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` holds the DTOs, period→(window, bucket-count) plan, và
  `DashboardMath` — pure, deterministic bucketing + KPI/delta math (không I/O, `now` được pass vào).

KPI deltas so sánh current window với immediately preceding one (query fetch a double
window for this). Có **không có live account P&L feed** — platform chỉ có equity cho backtests và
prop-firm tracking — vì vậy dashboard là *operational* by design (activity, throughput, success rate),
không phải brokerage balance ticker.

## Design & tokens

Tất cả màu đến từ design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), vì vậy một
white-label palette chảy qua miễn phí — bao gồm chart, có series colours được đọc từ resolved
tokens at runtime via `window.appReadTokens` (SVG không thể consume CSS variables trực tiếp). Không có
hard-coded hex ở bất kỳ đâu trong dashboard. Xem [../ui-guidelines.md](../ui-guidelines.md).

## The "Powered by cMind" link

Dashboard hiển thị một **"Powered by cMind"** link nhỏ xinh trỏ đến documentation
site này. Nó **`true` theo mặc định** — chúng tôi tự hào về project và nó giúp các trader khác tìm thấy
nó — nhưng đó là quyết định của bạn. Các reseller chạy một instance white-labeled đầy đủ flip
`App:Branding:ShowSiteLink` thành `false` và nó biến mất. Xem
[White-label branding](./white-label.md#powered-by-link).

## Tests

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event at `now`, divide-by-zero guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: default
  seed, apply order/visibility, append-omitted, duplicate-collapse, unknown-key rejection, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read
  model against real Postgres (status/KPIs/activity/resources, admin node health, empty-user path), các section backtests/copy-profiles/agents mới, và một layout **round-trip** (save custom layout → reload →
  order + visibility persisted).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: KPI
  cards, chart, ring và feed render; period toggle switch active period và reloads; một KPI
  drills through đến `/run`; **hiding a widget persists across a reload**, **Reset** mang nó trở lại, và
  Customize dialog hoạt động trên điện thoại không có horizontal overflow. `/` cũng trong `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) và `MobileJourneyTests`.
