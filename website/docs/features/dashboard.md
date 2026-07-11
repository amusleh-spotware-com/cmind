---
title: Dashboard
description: The cMind dashboard — a live, mobile-first command center for your cBot runs, backtests, resources, and node cluster.
---

# Dashboard 📊

The first thing you see when you sign in, and honestly the page you&apos;ll leave open all day. The
landing page (`/`, `Components/Pages/Index.razor`) is a **live, mobile-first command center** for the
signed-in user&apos;s activity across cBot runs, backtests, resources and (for admins) the node
cluster. It refreshes itself, looks great on a phone, and never makes you hit F5.

## What it shows

Top to bottom, priority-ordered for a phone (every block is a full-width stack item on mobile, a
responsive grid on tablet/desktop):

1. **Header** — title, a live indicator (a real pulsing dot; static under `prefers-reduced-motion`), the
   last-updated time, and a **period toggle** (`1H · 24H · 7D · 30D`) that drives the KPIs and chart.
2. **Hero KPIs** — four glanceable cards, each a big number + an inline SVG sparkline, and (where
   meaningful) a **delta vs the previous period**:
   - **Active now** — runs + backtests currently starting/running.
   - **Success rate** — completed ÷ (completed + failed) over the period; delta in percentage points.
   - **Completed** — finished runs/backtests this period; delta vs previous period.
   - **Failed** — failures this period; delta (fewer is better, so a drop shows green).
3. **Activity chart** — an ApexCharts area timeline of started / completed / failed per time bucket.
4. **Instance status ring** — a donut of running / backtests / pending / completed / failed, total in
   the centre.
5. **Live activity feed** — the 20 most recent events (newest first) with a status-coloured dot and a
   relative timestamp.
6. **Cluster health** (admins only) — active-vs-total nodes and a capacity-in-use gauge.
7. **Resource tiles** — cBots, parameter sets, trading accounts, cTrader IDs, MCP keys (click through to
   their pages), plus an admin nodes row.

## How it stays live

The page polls `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` every 10 seconds and re-renders the
widgets in place — no manual reload. A transient fetch failure is swallowed and retried on the next tick;
the loop stops cleanly on dispose. The first load shows a skeleton; a persistent failure shows an error
card with **Retry**; a user with no data sees zeroed KPIs and empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` maps `/overview` (and keeps the older scalar `/stats`). It is
  per-user and admin-gated via `ICurrentUser`; the clock comes from `TimeProvider`.
- `Endpoints/DashboardQuery.cs` builds the composite `DashboardOverview` read model: an all-time status
  snapshot (grouped counts), a windowed set of instances materialized once, and resource/node counts.
  Instance status and terminal timestamps live on TPH subtypes (not columns), so rows are read in memory
  via the shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers. Event time =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` holds the DTOs, the period→(window, bucket-count) plan, and
  `DashboardMath` — pure, deterministic bucketing + KPI/delta math (no I/O, `now` is passed in).

KPI deltas compare the current window against the immediately preceding one (the query fetches a double
window for this). There is **no live account P&L feed** — the platform only has equity for backtests and
prop-firm tracking — so the dashboard is deliberately *operational* (activity, throughput, success rate),
not a brokerage balance ticker.

## Design & tokens

All colour comes from design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), so a
white-label palette flows through for free — including the chart, whose series colours are read from the
resolved tokens at runtime via `window.appReadTokens` (SVG can't consume CSS variables directly). No
hard-coded hex anywhere in the dashboard. See [../ui-guidelines.md](../ui-guidelines.md).

## The "Powered by cMind" link

The dashboard shows a small, tasteful **"Powered by cMind"** link that points to this documentation
site. It&apos;s **shown by default** — we&apos;re proud of the project and it helps other traders find
it — but it&apos;s entirely your call. Resellers running a fully white-labeled instance flip
`App:Branding:ShowSiteLink` to `false` and it disappears. See
[White-label branding](./white-label.md#powered-by-link).

## Tests

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event at `now`, divide-by-zero guard).
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`) — the read model against real
  Postgres: status/KPIs/activity/resources, admin node health, and the empty-user path.
- **E2E** (`tests/E2ETests/DashboardTests.cs`) — desktop + mobile: KPI cards, chart, ring and feed
  render; the period toggle switches the active period and reloads; a KPI drills through to `/run`; no
  horizontal overflow on a phone. `/` is also in `PageSmokeTests`, `MobileLayoutTests` (shell +
  no-overflow) and `MobileJourneyTests`.
