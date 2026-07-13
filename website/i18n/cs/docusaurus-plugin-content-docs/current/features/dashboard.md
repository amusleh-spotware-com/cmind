---
title: Dashboard
description: cMind dashboard — živý, mobile-first velín pro vaše cBot spuštění, backtesty, zdroje a node cluster.
---

# Dashboard

První věc kterou vidíte když se přihlásíte, a upřímně stránka, kterou necháte otevřenou celý den. The
landing page (`/`, `Components/Pages/Index.razor`) je **živý, mobile-first velín** pro
přihlášeného uživatele napříč cBot spuštěními, backtesty, zdroji a (pro admina) node
clusterem. Obnovuje se sama, vypadá skvěle na telefonu a nikdy vás nenutí dát F5.

## Co ukazuje

Shora dolů, prioritně seřazeno pro telefon (každý blok je full-width stack item na mobile, a
responsive grid na tablet/desktop):

1. **Header** — titulek, živý indikátor (reálný pulsing bod; static under `prefers-reduced-motion`), čas
   poslední aktualizace, a **period toggle** (`1H · 24H · 7D · 30D`) který řídí KPI a graf.
2. **Hero KPIs** — čtyři glanceable karty, každá velké číslo + inline SVG sparkline, a (kde
   smysluplné) **delta vs předchozí období**:
   - **Active now** — spuštění + backtesty aktuálně startující/běžící.
   - **Success rate** — dokončené ÷ (dokončené + selhavší) za období; delta v procentech.
   - **Completed** — dokončené spuštění/backtesty toto období; delta vs předchozí období.
   - **Failed** — selhání toto období; delta (méně je lepší, takže pokles ukazuje zelenou).
3. **Activity chart** — ApexCharts area timeline started / completed / failed per time bucket.
4. **Instance status ring** — donut běžících / backtestů / pending / dokončených / selhavších, total in
   centre.
5. **Backtests** — three-tile snapshot (běžící / dokončené / selhavší), click-through na `/backtest`.
6. **Copy trading** — vaše copy-trading profily se živým statusovým bodem, počtem destinací, a **Live**
   badge na běžících profilech; click-through na `/copy-trading`.
7. **AI agenty** — vaši persona-driven trading agenty se stavem běhu (archetype · status) a časem poslední akce;
   click-through na `/agent-studio`.
8. **Live activity feed** — 20 nejnovějších událostí (nejnovější první) se statusově barevným bodem a
   relativním časovým razítkem.
9. **Cluster health** (pouze admin) — active-vs-total nodes a capacity-in-use gauge.
10. **Resource tiles** — cBots, trading účty, cTrader IDs, MCP klíče (click skrze na jejich stránky).

## Přizpůsobte si svůj dashboard

Každý blok výše je **widget který ovládáte**. Stiskněte **Customize** (vpravo nahoře v headeru) pro otevření
dialogu kde **zobrazíte/skryjete** jakýkoliv widget a **přeuspořádáte** je šipkami nahoru/dolů. **Reset to default**
obnoví katalogovní pořadí. Vaše volba je **persistována server-side per user**, takže vás sleduje across
prohlížečů a zařízení — ne jen tato záložka.

- Feature-gated a admin-only widgety (Copy trading, AI agenty, Cluster health) se objevují v
  dialogu pouze když vaše deployment/role může používat.
- Widget katalog je single source of truth v `Core/Dashboard/DashboardWidgets.cs`; prezentace
  (label + icon + availability) žije v `Components/Dashboard/DashboardWidgetMeta.cs`.

## Jak zůstává živý

Stránka polling `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` každých 10 sekund a re-renderuje
widgety na místě — žádné manuální reload. Transient fetch failure je spolknut a retryován na dalším tick;
loop clean zastaví na dispose. První load ukazuje skeleton; persistent failure ukazuje error
card s **Retry**; uživatel bez dat vidí zeroed KPIs a empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` mapuje `/overview` (a drží starší scalar `/stats`). Je
  per-user a admin-gated přes `ICurrentUser`; hodiny přicházejí z `TimeProvider`. Mapuje také
  `GET/PUT /api/dashboard/layout` — layout widgetů uživatele, loaded na start stránky a saved from the
  Customize dialog.
- **Layout persistence** je `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`): one board
  per user (unique on `UserId`), owning an ordered list of widget settings (visible + order) stored jako `jsonb` column. The ordered list is only ever mutated through `Apply` / `Reset`, which validate every
  key against the `DashboardWidgets` catalog and keep the collection complete and de-duplicated. Unknown
  keys jsou odmítnuty with a `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` buduje composite `DashboardOverview` read model: all-time status
  snapshot (grouped counts), a windowed set of instances materialized once, and resource/node counts.
  Instance status and terminal timestamps live on TPH subtypes (not columns), takže rows jsou čteny in memory
  přes shared `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers. Event time =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` drží DTOs, the period→(window, bucket-count) plan, and
  `DashboardMath` — pure, deterministic bucketing + KPI/delta math (no I/O, `now` je předán).

KPI delty srovnávají aktuální okno proti bezprostředně předcházejícímu (query fetches a double
window for this). Neexistuje **živý feed account P&L** — platforma má equity pouze pro backtesty a
prop-firm tracking — takže dashboard je záměrně *operační* (activity, throughput, success rate),
ne broker balance ticker.

## Design & tokens

Veškerá barva přichází z design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), takže
white-label palette proudí zdarma — včetně grafu, jehož series barvy jsou čteny z resolved
tokens at runtime via `window.appReadTokens` (SVG can't consume CSS variables directly). Žádný
hard-coded hex anywhere in the dashboard. Viz [../ui-guidelines.md](../ui-guidelines.md).

## Odkaz "Powered by cMind"

Dashboard ukazuje malý, vkusný **"Powered by cMind"** odkaz který míří na tuto dokumentační
stránku. Je **shown by default** — jsme hrdí na projekt a pomáhá to ostatním traderům ho najít
— ale je to zcela vaše rozhodnutí. Reselleři provozující plně white-labeled instanci přepnou
`App:Branding:ShowSiteLink` na `false` a zmizí. Viz
[White-label branding](./white-label.md#powered-by-link).

## Testy

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (event at `now`, divide-by-zero guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: default
  seed, apply order/visibility, append-omitted, duplicate-collapse, unknown-key rejection, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read
  model proti real Postgres (status/KPIs/activity/resources, admin node health, empty-user path), new backtests/copy-profiles/agents sections, and layout **round-trip** (save custom layout → reload →
  order + visibility persisted).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: KPI
  cards, chart, ring and feed render; period toggle přepíná active period a reloaduje; KPI
  drill through na `/run`; **skrytí widgetu přetrvává přes reload**, **Reset** ho vrátí, a
  Customize dialog funguje na telefonu bez horizontálního overflow. `/` je také v `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) a `MobileJourneyTests`.
