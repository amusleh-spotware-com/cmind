# Dashboard Revamp — Live, Interactive, Eye-Catching, Mobile-First

Status: **PLANNED.** Blueprint for turning the current static count-card dashboard (`src/Web/Components/Pages/Index.razor`)
into a polished, real-time, interactive command center for the platform.

Grounded in `docs/ui-guidelines.md` (mandatory), `plans/ui-overhaul.md` (design-token + mobile-first
foundation already shipped), and 2026 fintech/trading-dashboard UX research (see §9).

---

## 0. Why — the problem with today's dashboard

`Index.razor` today is:

- **Static counts only.** ~14 `DashStat` tiles showing scalar counts (running cBots, backtests, pending,
  failed, cBots, param sets, accounts, MCP keys, admin nodes/users). No trend, no history, no context.
- **Poll-based, not pushed.** A 10s `PeriodicTimer` re-fetches `/api/dashboard/stats` and re-renders the
  whole page. "Live" is a decorative pulsing chip, not real streaming.
- **No visualization.** Zero charts, sparklines, timelines, or activity feed — even though
  **Blazor-ApexCharts 5.0 is already a dependency** (used in `InstanceDetail.razor`) and a **SignalR log
  hub already exists** (`/hubs/logs`).
- **No "so what".** It answers "how many" but never "how am I doing / what changed / what needs me now".
  Research calls the KPI card's job answering *"How am I doing?"* at a glance, then letting the user dig in.
- **Token violations.** `DashStat.razor` and `Index.razor` hard-code hex colours (`#26C281`, `#3498DB`,
  `#A0A0A0`, …) — a direct breach of `docs/ui-guidelines.md` §2 (design tokens only, white-label safe).
  The revamp fixes this on the way through.

**Goal:** a dashboard that is *glanceable* (KPIs + deltas + sparklines), *live* (SignalR push, animated
transitions), *interactive* (period toggles, drill-down, filter), *visual* (charts, activity feed,
status heat), and *calm* (fintech restraint — space, hierarchy, honest colour roles), **designed for a
360px phone first** and enhanced upward, with **zero hard-coded colours** and **full three-tier tests**.

---

## 1. Non-negotiables (acceptance gates)

1. **Mobile-first.** Every widget designed for 360–430px first, enhanced upward with MudBlazor
   breakpoints. No horizontal scroll 320–1920px. Touch targets ≥44px. Honour `prefers-reduced-motion`.
   Widgets **stack in one column on phone**, grid upward. (`docs/ui-guidelines.md` §1, §3.)
2. **Design tokens only.** All colour/radius/spacing from MudBlazor theme + `BrandingCss`
   (`var(--app-primary/-success/-warning/-danger/-info/-surface/-border/-text*/-radius)`). Kill every
   hard-coded hex in `DashStat`/`Index`. Chart palettes read tokens so a reseller's white-label palette
   flows through for free. (§2, §6.)
3. **Live via push, not poll.** Replace the 10s full re-fetch with a **SignalR dashboard hub** that pushes
   deltas; fall back to poll only if the circuit drops. Animated number roll + subtle flash on change.
4. **Strict DDD backend.** New read-model queries live behind endpoints/query services, not in the
   component. No domain logic in Razor. One aggregate per transaction is irrelevant here (read-only), but
   time-series/read models are explicit DTOs, not leaked entities. (`CLAUDE.md` mandate 1, `ddd-dotnet`.)
5. **`TimeProvider` only.** All timestamps/bucketing via injected `TimeProvider`; tests use
   `FakeTimeProvider`. Never `DateTime.UtcNow`. (mandate 4.)
6. **Three test tiers.** Unit (bucketing/read-model logic), integration (endpoints hit real Postgres via
   Testcontainers), E2E (Playwright, **mobile + desktop**: renders, no overflow, live update observed,
   drill-down navigates, empty/error states). New routes → `PageSmokeTests` + `MobileLayoutTests`. (§9.)
7. **Zero warnings**, analyzer sweep clean on touched projects, `get_file_problems` clean, modern C# 14.
8. **Docs same commit.** `docs/features/dashboard.md` (new) + update `docs/ui-guidelines.md` widget notes.

---

## 2. Target layout (mobile-first, top → bottom)

Priority order = what a user needs first on a phone. Each block is one full-width stack item on mobile;
graduates to a responsive grid on `sm`+.

```
┌─ Header ───────────────────────────────────────────────┐
│ Dashboard      ● Live (real pulse on push)   ⟳ 12:04:31 │
│ [ 1H · 24H · 7D · 30D ]  period segmented toggle        │
├─ 1. Hero KPI strip (glanceable) ───────────────────────┤
│ [Active now] [Success rate] [Failed 24h] [Avg runtime] │   ← big number + delta + sparkline
├─ 2. Activity timeline chart ───────────────────────────┤
│  started / completed / failed over selected period      │   ← ApexChart area/line, live
├─ 3. Status donut + Node health ────────────────────────┤
│  instance status breakdown  |  node capacity gauge      │
├─ 4. Live activity feed ────────────────────────────────┤
│  ● 12:04 backtest #A1 completed (+2.3%)                 │   ← streamed events, newest on top
│  ● 12:03 cBot EURUSD started on node-2                  │
├─ 5. Resource tiles (today's static counts, restyled) ──┤
│  cBots · param sets · accounts · cTIDs · MCP keys       │   ← click-through, now token-coloured
└─ 6. Admin row (admins only) ───────────────────────────┘
   users · nodes · active nodes · cluster load
```

Widgets are **components** under `src/Web/Components/Dashboard/` so each is independently testable and
reorderable. Everything below the hero strip is lazy-friendly (charts mount after first paint — research:
"limit first paint to essentials, lazy-load heavy charts").

---

## 3. Widgets — detail

### 3.1 Hero KPI cards (`DashKpiCard.razor` — evolves `DashStat`)
- Big value + **label** + **delta vs previous period** (▲ +12% / ▼ −4%, coloured by success/danger role)
  + **inline sparkline** (tiny ApexChart, last N buckets). Research: KPI card = big number + label +
  delta + mini-trend, answering "how am I doing" first.
- Metrics: **Active now** (running instances+backtests), **Success rate** (completed / (completed+failed)
  over period), **Failed (period)**, **Avg runtime** (or throughput). Admin adds **Cluster load**.
- Animated count-up on value change; brief token-tinted flash on live update. Click → drill to filtered
  list page. `HelpTip` on each explaining the metric (sourced from `docs/features/dashboard.md`).
- **Token colours only** — pass a semantic role (`Success`/`Warning`/`Danger`/`Info`/`Neutral`), map to
  `var(--app-*)`. This replaces `DashStat`'s hard-coded hex params.

### 3.2 Activity timeline (`DashActivityChart.razor`)
- ApexChart stacked area/line: started vs completed vs failed per time bucket over the selected period.
- Series colours = tokens. Smooth, `prefers-reduced-motion` disables the animation.
- Reacts to the period toggle (1H/24H/7D/30D) — re-queries a time-series endpoint.
- New points appended live from the hub without a full reload.

### 3.3 Status donut + node health (`DashStatusDonut.razor`, `DashNodeHealth.razor`)
- Donut: running / pending / failed / completed instance mix (token role colours), centre shows total.
- Node health (admin, or user with nodes): active-vs-total gauge + per-node capacity bars; degraded/offline
  nodes flagged danger. Ties into existing node status types (`ActiveRunNode`, heartbeat).

### 3.4 Live activity feed (`DashActivityFeed.razor`)
- Streamed newest-first list of domain events: instance started/completed/failed, node
  registered/offline, backtest finished (+result), copy-trade mirrored. Each row: dot (role colour),
  relative time (via `TimeProvider`), text, optional click-through. Research: activity/alert feed "keeps
  the dashboard live and actionable". Capped buffer (e.g. 50) like the existing log-tail pattern.

### 3.5 Resource + admin tiles
- The current counts, restyled as `DashKpiCard` (neutral role), click-through preserved. Grouped under
  "My resources" / "Admin" as today, but token-coloured and consistent with the hero strip.

---

## 4. Backend — read models & live push (DDD)

### 4.1 Query/read-model service (`src/Web` or a Nodes/Infrastructure query service)
- Keep the existing `/api/dashboard/stats` for the scalar snapshot (already correct, per-user, admin-gated).
- **Add** `GET /api/dashboard/timeseries?period=24h` → buckets of started/completed/failed counts. Bucketing
  boundaries computed from `TimeProvider.GetUtcNow()`. Returns an explicit `DashboardTimeSeriesDto`
  (records, `required`/`init`), never a leaked EF entity.
- **Add** `GET /api/dashboard/kpis?period=24h` → the hero metrics **with previous-period value** for delta
  (success rate, active now, failed, avg runtime). Compute deltas server-side so the client stays dumb.
- All queries per-user (`ICurrentUser`), admin fields gated, mirroring the existing endpoint's pattern.
- Efficient EF: grouped counts / date-trunc aggregation, not N+1. Watch analyzer perf rules (CA1859 etc.).

### 4.2 SignalR dashboard hub (`/hubs/dashboard`)
- New `DashboardHub`; server pushes `StatsChanged`, `TimeSeriesPoint`, and `ActivityEvent` messages to the
  connected user's group. Client subscribes on mount, updates in place, `StateHasChanged`.
- **Source of events:** the existing background pipeline already knows when instances transition and nodes
  change (pollers/`NodeScheduler`/instance state machine). Bridge those domain events → a hub notifier
  (thin app-tier adapter; domain stays pure). Reuse the resilience/notification-bridge pattern noted in
  the copy-trading work rather than inventing a parallel path.
- **Graceful degradation:** if the hub can't connect, the component keeps the existing `PeriodicTimer`
  poll as fallback (feature-gate/ErrorBoundary gotcha from prior work — guard the initial load, recover on
  reconnect; the reconnect-modal pattern already exists).
- Auth: hub `RequireAuthorization`, user-scoped groups so no cross-tenant leakage.

### 4.3 No live equity/P&L
Per prior finding (`no-live-equity-feed`, now partially reopened by prop-firm simulation): the app has **no
general live account P&L feed** outside backtests/prop-firm equity tracking. So the dashboard's "trading"
flavour is **operational** (bot/backtest/node activity, success rates, throughput) — not a brokerage
balance ticker. Where prop-firm live equity *does* exist, an optional equity mini-widget can surface it,
gated on that feature. Don't promise a P&L stream the platform can't back.

---

## 5. Visual & interaction design

- **Dark, token-driven, calm.** Deep surface background (`--app-surface`), high-contrast typography, no
  pure black, generous spacing — research: "make it calm, clean, honestly a little boring… let things
  breathe." Bold contrast reserved for the numbers that matter (active, failed, success rate).
- **Semantic colour roles**, not decoration: success/running = `--app-success`, pending/warning =
  `--app-warning`, failed = `--app-danger`, info/backtest = `--app-info`, neutral counts = `--app-text`.
- **Motion with restraint:** count-up on KPIs, fade/slide-in for new feed rows, smooth chart transitions —
  **all gated behind `prefers-reduced-motion`**. No essential info conveyed by animation alone.
- **Interactivity:** period segmented toggle (drives charts+KPIs), click-through drill-down on every KPI
  and feed row, hover/tap tooltips on chart points, `HelpTip` on every metric. Optional later: user
  reorder / show-hide widgets (persisted per user) — flagged as a stretch, not v1.
- **Skeleton loading** (not a bare spinner), explicit **empty state** ("No activity yet — start a cBot"),
  and **error state** per widget so one failing endpoint doesn't blank the page.
- **No gamification.** Research explicitly warns streaks/badges can nudge impulsive trading. Keep it
  informational.

---

## 6. Files (new / touched)

**New**
- `src/Web/Components/Dashboard/DashKpiCard.razor` — KPI card w/ delta + sparkline (role-coloured).
- `src/Web/Components/Dashboard/DashActivityChart.razor` — timeline area/line chart.
- `src/Web/Components/Dashboard/DashStatusDonut.razor` — status breakdown donut.
- `src/Web/Components/Dashboard/DashNodeHealth.razor` — node capacity/health (admin).
- `src/Web/Components/Dashboard/DashActivityFeed.razor` — streamed event feed.
- `src/Web/Hubs/DashboardHub.cs` + notifier bridge from the transition pipeline.
- `DashboardTimeSeriesDto` / `DashboardKpisDto` + query service.
- `docs/features/dashboard.md`.

**Touched**
- `src/Web/Components/Pages/Index.razor` — recompose into the widget layout; swap poll → hub w/ fallback.
- `src/Web/Components/DashStat.razor` — retire or fold into `DashKpiCard`; remove hard-coded hex.
- `src/Web/Endpoints/DashboardEndpoints.cs` — add `/timeseries`, `/kpis`.
- Program/DI + hub route registration; `SecurityHeaders.cs` if a new connect-src is needed (ApexCharts
  already allowed).
- `docs/ui-guidelines.md` — note the dashboard widget conventions.

---

## 7. Testing (three tiers, mobile-first)

- **Unit:** period bucketing + delta math with `FakeTimeProvider` (boundary cases: empty period, single
  bucket, previous-period zero → no divide-by-zero on success rate). Read-model DTO shaping.
- **Integration:** `/api/dashboard/timeseries` + `/kpis` against real Postgres (Testcontainers) — per-user
  isolation, admin gating, correct counts across instance TPH states, empty-data path.
- **E2E (Playwright, mobile + desktop):**
  - `PageSmokeTests` + `MobileLayoutTests`: dashboard renders, bottom nav, **no overflow 320–1920px**.
  - Live update: seed an instance transition → assert a KPI/feed row updates **without reload** (hub) and
    that the poll fallback also updates if the hub is unavailable.
  - Interaction: period toggle changes the chart; KPI click navigates to the filtered list; `HelpTip`
    opens on tap.
  - States: empty (new user → empty feed/zeroed KPIs), error (endpoint 500 → widget error card, page not
    blanked).
- `dotnet test` green before "done".

---

## 8. Phases

- **P0 — Tokenize + restructure (no new data).** Extract `DashKpiCard`, kill hard-coded hex, stack-first
  layout, skeleton/empty/error states. Ships value immediately, unblocks token compliance. E2E: layout +
  no-overflow.
- **P1 — Visuals from existing snapshot.** Status donut + resource tiles from the current `/stats`
  endpoint. ApexChart wired. E2E: renders + drill-down.
- **P2 — Time-series + KPI deltas.** New endpoints + activity timeline chart + hero KPIs w/ sparklines &
  deltas. Unit + integration for bucketing/deltas.
- **P3 — Live push.** `DashboardHub` + notifier bridge; swap poll → push w/ fallback; live activity feed.
  E2E: live-update-without-reload + fallback.
- **P4 — Node health + admin + polish.** Node capacity widget, motion polish (reduced-motion gated),
  optional prop-firm equity mini-widget. Stretch: user widget reorder/persist.

Each phase is independently shippable to `main`, docs + tests in the same commit.

## 9. Research basis (2026 fintech/trading dashboard UX)

Key patterns applied above: **token-based dark-mode design systems** for high-frequency data; **KPI cards =
big number + label + delta + sparkline** answering "how am I doing" first; **real-time push** (WebSocket-
style) over refresh; **strong visual hierarchy** via contrasting colour on the metrics that matter;
**reduced cognitive load** (compact modular widgets, let it breathe); **activity/alert feed** to stay live
and actionable; **semantic colour-role tokens, avoid pure black, validate contrast**; **lazy-load heavy
charts, essentials first**; **restraint over flash** (finance UX rewards calm); and a deliberate
**no-gamification** stance (streaks can nudge impulsive trading).

Sources:
- [Trading App Design: Complete Guide to UI/UX & System Architecture (2026) — Lollypop](https://lollypop.design/blog/2026/june/trading-app-design/)
- [Fintech Dashboard UI: KPIs, Card Patterns, Tables — uisea](https://uisea.net/fintech-dashboard-ui-kpis-card-patterns-tables-figma-guide/)
- [Fintech dashboard design — Merge Rocks](https://merge.rocks/blog/fintech-dashboard-design-or-how-to-make-data-look-pretty)
- [50 Best Dashboard Design Examples for 2026 — Muzli](https://muz.li/blog/best-dashboard-design-examples-inspirations-for-2026/)
- [How TradingView Increased Trader Efficiency 26% — RonDesignLab](https://rondesignlab.com/cases/tradingview-platform-for-traders)
- [Crypto Trading Dashboard with Smarter Visualization — MultipurposeThemes](https://multipurposethemes.com/blog/new-crypto-dashboard-trading-ui-now-enhanced-with-smarter-visualization/)
