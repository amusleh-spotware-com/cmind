---
title: Dashboard
description: Dashboard cMind — pusat komando live, mobile-first untuk jalannya cBot, backtest, resource, dan kluster node.
---

# Dashboard 📊

Hal pertama yang Anda lihat saat masuk, dan jujur halaman yang akan Anda tinggalkan terbuka sepanjang hari. Halaman
mendarat (`/`, `Components/Pages/Index.razor`) adalah **pusat komando live, mobile-first** untuk aktivitas
pengguna yang masuk di seluruh cBot runs, backtest, resource dan (untuk admin) kluster node. Ia merefresh dirinya,
terlihat bagus di ponsel, dan tidak pernah membuat Anda menekan F5.

## Apa yang ditampilkannya

Atas ke bawah, prioritas-ordered untuk ponsel (setiap blok adalah full-width stack item pada mobile, grid responsif pada tablet/desktop):

1. **Header** — judul, indikator live (titik pulse nyata; static di bawah `prefers-reduced-motion`), waktu terakhir-diupdate,
   dan **period toggle** (`1H · 24H · 7D · 30D`) yang mendorong KPI dan chart.
2. **Hero KPIs** — empat kartu yang dapat dipandang sekilas, masing-masing angka besar + sparkline SVG inline, dan (di mana
   bermakna) **delta vs periode sebelumnya**:
   - **Active now** — runs + backtests saat ini mulai/running.
   - **Success rate** — completed ÷ (completed + failed) selama periode; delta dalam percentage points.
   - **Completed** — finished runs/backtests periode ini; delta vs periode sebelumnya.
   - **Failed** — kegagalan periode ini; delta (semakin sedikit semakin baik, jadi penurunan menunjukkan green).
3. **Activity chart** — timeline area ApexCharts dari started / completed / failed per time bucket.
4. **Instance status ring** — donut dari running / backtests / pending / completed / failed, total di
   tengah.
5. **Backtests** — snapshot tiga-tile (running / completed / failed), click-through ke `/backtest`.
6. **Copy trading** — profile copy-trading Anda dengan live status dot, destination count, dan badge **Live**
   pada running profiles; click-through ke `/copy-trading`.
7. **AI agents** — agen trading persona-driven Anda dengan run state (archetype · status) dan waktu last-action;
   click-through ke `/agent-studio`.
8. **Live activity feed** — 20 event paling recent (newest first) dengan status-coloured dot dan relative timestamp.
9. **Cluster health** (admin only) — active-vs-total nodes dan capacity-in-use gauge.
10. **Resource tiles** — cBots, trading accounts, cTrader IDs, MCP keys (click through ke halaman mereka).

## Sesuaikan dashboard Anda

Setiap blok di atas adalah **widget yang Anda kontrol**. Tekan **Customize** (top-right header) untuk membuka
dialog di mana Anda **show/hide** widget apa pun dan **reorder** mereka dengan up/down arrows. **Reset to default**
mengembalikan urutan katalog. Pilihan Anda **persisted server-side per user**, jadi mengikuti Anda di seluruh
browser dan device — bukan hanya tab ini.

- Widget gated fitur dan admin-only (Copy trading, AI agents, Cluster health) hanya muncul dalam
  dialog saat deployment/role Anda dapat menggunakannya.
- Katalog widget adalah satu sumber kebenaran dalam `Core/Dashboard/DashboardWidgets.cs`; presentasi
  (label + icon + availability) tinggal di `Components/Dashboard/DashboardWidgetMeta.cs`.

## Bagaimana ia tetap live

Halaman polls `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` setiap 10 detik dan re-renders
widgets di tempat — tidak ada reload manual. Kegagalan fetch transien ditelan dan dicoba kembali pada tick berikutnya;
loop berhenti clean pada dispose. Load pertama menampilkan skeleton; kegagalan persisten menampilkan error card dengan **Retry**;
pengguna tanpa data melihat KPI zeroed dan empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` maps `/overview` (dan keeps scalar yang lebih lama `/stats`). Ini
  per-user dan admin-gated via `ICurrentUser`; clock berasal dari `TimeProvider`. Ini juga maps
  `GET/PUT /api/dashboard/layout` — layout widget pengguna, loaded pada page start dan saved dari
  dialog Customize.
- **Layout persistence** adalah aggregate `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): satu board
  per user (unique pada `UserId`), memiliki ordered list widget settings (visible + order) stored sebagai
  kolom `jsonb`. Ordered list hanya pernah dimutasi melalui `Apply` / `Reset`, yang validate setiap
  key terhadap katalog `DashboardWidgets` dan keep collection complete dan de-duplicated. Keys yang tidak dikenal
  ditolak dengan `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` builds composite read model `DashboardOverview`: all-time status
  snapshot (grouped counts), windowed set instances materialized once, dan resource/node counts.
  Instance status dan terminal timestamps tinggal pada TPH subtypes (bukan kolom), jadi rows dibaca in memory
  via shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers. Event time =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` holds DTOs, period→(window, bucket-count) plan, dan
  `DashboardMath` — pure, deterministic bucketing + KPI/delta math (tidak ada I/O, `now` passed in).

KPI deltas membandingkan window saat ini terhadap yang langsung mendahuluinya (query mengambil double
window untuk ini). Ada **tidak ada live account P&L feed** — platform hanya memiliki equity untuk backtests dan
prop-firm tracking — jadi dashboard sengaja *operational* (activity, throughput, success rate),
bukan brokerage balance ticker.

## Design & tokens

Semua warna berasal dari design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), jadi
white-label palette mengalir melalui untuk gratis — termasuk chart, yang series colours dibaca dari
resolved tokens pada runtime via `window.appReadTokens` (SVG tidak dapat mengkonsumsi CSS variables directly). Tidak ada
hard-coded hex di mana pun dalam dashboard. Lihat [../ui-guidelines.md](../ui-guidelines.md).

## Link "Powered by cMind"

Dashboard menampilkan link kecil dan bergaya **"Powered by cMind"** yang menunjuk ke documentation
site ini. Ini **ditampilkan secara default** — kami bangga dengan proyeknya dan ini membantu trader lain menemukannya —
tetapi sepenuhnya pilihan Anda. Reseller yang menjalankan instance fully white-labeled flip
`App:Branding:ShowSiteLink` ke `false` dan itu hilang. Lihat
[White-label branding](./white-label.md#powered-by-link).

## Tests

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event pada `now`, divide-by-zero guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — aggregate `UserDashboard`: default
  seed, apply order/visibility, append-omitted, duplicate-collapse, unknown-key rejection, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read
  model terhadap real Postgres (status/KPIs/activity/resources, admin node health, empty-user path), section
  backtests/copy-profiles/agents yang baru, dan layout **round-trip** (save custom layout → reload →
  order + visibility persisted).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: KPI
  cards, chart, ring dan feed render; period toggle switches active period dan reloads; KPI
  drills through ke `/run`; **hiding widget persists across reload**, **Reset** membawanya kembali, dan
  dialog Customize works pada ponsel tanpa horizontal overflow. `/` juga dalam `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) dan `MobileJourneyTests`.
