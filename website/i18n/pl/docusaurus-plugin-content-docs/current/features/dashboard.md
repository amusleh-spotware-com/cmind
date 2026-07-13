---
title: Dashboard
description: cMind dashboard — live, mobile-first command center dla Twojego cBot runs, backtests, resources, i node cluster.
---

# Dashboard 📊

Pierwsza rzecz którą widzisz gdy się loguj, i honestly strona którą będziesz mieć otwartą cały dzień. Landing
page (`/`, `Components/Pages/Index.razor`) to **live, mobile-first command center** dla signed-in user's activity
across cBot runs, backtests, resources i (dla admins) node cluster. Refreshuje się, wygląda świetnie na telefonie,
i nigdy nie musisz uderzać F5.

## Co pokazuje

Top do bottom, priority-ordered dla telefonu (każdy block to full-width stack item na mobile, responsive grid na
tablet/desktop):

1. **Header** — title, live indicator (rzeczywisty pulsing dot; static pod `prefers-reduced-motion`), ostatni
   updated time, i **period toggle** (`1H · 24H · 7D · 30D`) który drives KPIs i chart.
2. **Hero KPIs** — cztery glanceable cards, każdy big number + inline SVG sparkline, i (gdzie meaningful) **delta
   vs previous period**:
   - **Active now** — runs + backtests currently starting/running.
   - **Success rate** — completed ÷ (completed + failed) nad period; delta w percentage points.
   - **Completed** — finished runs/backtests ten period; delta vs previous period.
   - **Failed** — failures ten period; delta (mniej jest lepsze, więc drop pokazuje green).
3. **Activity chart** — ApexCharts area timeline started / completed / failed per time bucket.
4. **Instance status ring** — donut running / backtests / pending / completed / failed, total w
   centre.
5. **Backtests** — three-tile snapshot (running / completed / failed), click-through do `/backtest`.
6. **Copy trading** — Twoje copy-trading profiles z live status dot, destination count, i **Live**
   badge na running profiles; click-through do `/copy-trading`.
7. **AI agents** — Twoje persona-driven trading agents z run state (archetype · status) i last-action
   time; click-through do `/agent-studio`.
8. **Live activity feed** — 20 most recent events (newest first) z status-coloured dot i
   relative timestamp.
9. **Cluster health** (admins tylko) — active-vs-total nodes i capacity-in-use gauge.
10. **Resource tiles** — cBots, trading accounts, cTrader IDs, MCP keys (click through do ich pages).

## Customize Twój dashboard

Każdy block powyżej to **widget którym kontrolujesz**. Hit **Customize** (top-right header) do open dialog gdzie
**show/hide** każdy widget i **reorder** je z up/down arrows. **Reset do default** restores catalog order. Twój choice
jest **persisted server-side per user**, więc follows Cię across browsers i devices — nie just ten tab.

- Feature-gated i admin-only widgets (Copy trading, AI agents, Cluster health) tylko appear w
  dialog gdy Twoje deployment/role może je użyć.
- Widget catalog to single source of truth w `Core/Dashboard/DashboardWidgets.cs`; presentation
  (label + icon + availability) żyje w `Components/Dashboard/DashboardWidgetMeta.cs`.

## Jak zostaje live

Page polls `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` co 10 sekund i re-renders widgets
w place — no manual reload. Transient fetch failure jest swallowed i retried na next tick;
loop stops cleanly na dispose. First load pokazuje skeleton; persistent failure pokazuje error
card z **Retry**; user z no data widzi zeroed KPIs i empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` maps `/overview` (i keeps starsze scalar `/stats`). To
  per-user i admin-gated via `ICurrentUser`; clock comes z `TimeProvider`. To także maps
  `GET/PUT /api/dashboard/layout` — user's widget layout, loaded na page start i saved z
  Customize dialog.
- **Layout persistence** to `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`): jeden board
  per user (unique na `UserId`), owning ordered list widget settings (visible + order) stored jako
  `jsonb` column. Ordered list to tylko ever mutated przez `Apply` / `Reset`, które validate każdy
  key contra `DashboardWidgets` catalog i keep collection complete i de-duplicated. Unknown
  keys są rejected z `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` builds composite `DashboardOverview` read model: all-time status
  snapshot (grouped counts), windowed set instances materialized once, i resource/node counts.
  Instance status i terminal timestamps live na TPH subtypes (nie columns), więc rows są read w memory
  via shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers. Event time =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` holds DTOs, period→(window, bucket-count) plan, i
  `DashboardMath` — pure, deterministyczne bucketing + KPI/delta math (no I/O, `now` passed in).

KPI deltas compare current window przeciw immediately preceding one (query fetches double
window dla tego). Jest **no live account P&L feed** — platform tylko ma equity dla backtests i
prop-firm tracking — więc dashboard to deliberately *operational* (activity, throughput, success rate),
nie brokerage balance ticker.

## Design & tokens

Wszystkie colour comes z design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), więc
white-label palette flows przez za free — including chart, którego series colours są read z
resolved tokens na runtime via `window.appReadTokens` (SVG nie może consume CSS variables directly). No
hard-coded hex anywhere w dashboard. See [../ui-guidelines.md](../ui-guidelines.md).

## "Powered by cMind" link

Dashboard pokazuje small, tasteful **"Powered by cMind"** link która points do tego documentation
site. To **shown domyślnie** — jesteśmy proud tego project i helps inne traders find
to — ale to entirely Twój call. Resellers running fully white-labeled instance flip
`App:Branding:ShowSiteLink` do `false` i disappears. See
[White-label branding](./white-label.md#powered-by-link).

## Testy

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event na `now`, divide-by-zero guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: default
  seed, apply order/visibility, append-omitted, duplicate-collapse, unknown-key rejection, reset.
- **Integracja** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read
  model contra real Postgres (status/KPIs/activity/resources, admin node health, empty-user path), nowy
  backtests/copy-profiles/agents sections, i layout **round-trip** (save custom layout → reload →
  order + visibility persisted).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: KPI
  cards, chart, ring i feed render; period toggle switches active period i reloads; KPI
  drills through do `/run`; **hiding widget persists across reload**, **Reset** brings back, i
  Customize dialog works na phone z no horizontal overflow. `/` to także w `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) i `MobileJourneyTests`.
