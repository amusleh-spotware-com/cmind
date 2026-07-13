---
title: Командна tabla
description: "cMind командна tabla — live, mobile-first команndни центар за ваше cBot покретања, backtest-ове, ресурсе и node cluster."
---

# Командна tabla 📊

Прво што видите када се пријавите, и искрено, страница коју ћете оставити отворену целог дана. Landing
страница (`/`, `Components/Pages/Index.razor`) је **live, mobile-first команndни центар** за
активност пријављеног корисника преко cBot покретања, backtest-ова, ресурса и (за админе) node
cluster-а. Освежава се аутоматски, изгледа одлично на телефону и никада вас не тера да притиснете F5.

## Шта приказује

Од врха ка дну, приоритетно-уређено за телефон (сваки блок је full-width stack item на мобилном, a
responsive grid на tablet/desktop-у):

1. **Header** — наслов, live индикатор (права пулсирајућа тачка; статична под `prefers-reduced-motion`), последње
   ажурирано време, и **period toggle** (`1H · 24H · 7D · 30D`) који управља KPI-овима и графиконом.
2. **Hero KPI-ови** — четири glanceable картице, свака са великим бројем + inline SVG sparkline, и (тамо gdzie
   има смисла) **delta у односу на претходни период**:
   - **Active now** — покретања + backtest-ови који тренутно почињу/раде.
   - **Success rate** — завршени ÷ (завршени + неуспешни) у периоду; delta у процентним поенима.
   - **Completed** — завршена покретања/backtest-ови овог периода; delta у односу на претходни период.
   - **Failed** — неуспеси овог периода; delta (мање је боље, па пад показује зелено).
3. **Activity графикон** — ApexCharts area timeline започета / завршена / неуспешна по временском bucket-у.
4. **Instance status ring** — Прстевац од running / backtest-ови / pending / completed / failed, укупан број у
   средини.
5. **Backtest-ови** — три tile-а snapshot (running / completed / failed), клик кроз на `/backtest`.
6. **Copy trading** — ваши copy-trading профили са live status тачком, бројем дестинација и **Live**
   значком на активним профилима; клик кроз на `/copy-trading`.
7. **AI агенти** — ваши persona-driven трговачки агенти са статусом покретања (archetype · status) и временом последње акције;
   клик кроз на `/agent-studio`.
8. **Live activity feed** — 20 најскоријих догађаја (најновији први) са status-обојеном тачком и
   релативним временским ознакама.
9. **Cluster health** (само админи) — active-vs-total чворова и мерач искоришћености капацитета.
10. **Resource tiles** — cBots, трговачки налози, cTrader ID-ови, MCP кључevi (клик кроз на њихове странице).

## Прилагодите своју командну таблу

Сваки горњи блок је **widget којим управљате**. Притисните **Customize** (горе-десно од header-а) да отворите
дијалог где **приказујете/сакривате** било koji widget и **преуређујете** их стрелицама горе/доле. **Reset to default**
враћа редослед из каталога. Ваш избор је **перзистovan server-side по кориснику**, тако да вас прати преко
прегледача и уређаја — не само у овој картици.

- Feature-gated и admin-only widget-ови (Copy trading, AI агенти, Cluster health) се појављују у
  дијалогу само када ваш deployment/rola може да их користи.
- Каталог widget-ова је јединствени извор истине у `Core/Dashboard/DashboardWidgets.cs`; презентација
  (label + icon + availability) живи у `Components/Dashboard/DashboardWidgetMeta.cs`.

## Како остаје live

Страница poll-ује `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` сваких 10 секунди и ререндерује
widget-ове на месту — без ручног освежавања. Транзијентни неуспех fetch-а се прогута и ретрија на следећем tick-у;
колачи се зауставља чисто на dispose. Прво учитавање показује skeleton; перзистентни неуспех показује error
картицу са **Retry**; корисник без података види нуларване KPI-ове и empty-state копију.

## Backend

- `Endpoints/DashboardEndpoints.cs` мапира `/overview` (и чува старији скалар `/stats`). Пер-корисник је и
  admin-gated преко `ICurrentUser`; сат долази од `TimeProvider`. Такође мапира
  `GET/PUT /api/dashboard/layout` — layout widget-ова корисника, учитава се на почетку странице и чува из
  Customize дијалога.
- **Layout persistence** је `UserDashboard` aggregate (`Core/Dashboard/UserDashboard.cs`): једна табла
  по кориснику (јединствено на `UserId`), са уређеном листом widget подешавања (visible + order) ускладиштених kao
  `jsonb` колона. Уређена листа се мутира само преко `Apply` / `Reset`, који валидирају сваки кључ
  против `DashboardWidgets` каталога и држе колекцију комплетном и де-дуплицираном. Непознати
  кључevi се одбијају са `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` гради композитни `DashboardOverview` read model: all-time status
  snapshot (груписани бројеви), windowed сет инстанци материјализован једном, и resource/node бројеви.
  Instance status и terminal timestamps живи на TPH подтиповима (не колонама), тако да се редови читају у меморији
  преко дељених `InstanceEndpoints.GetStartedAt/GetStoppedAt` помоћника. Време догађаја =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` држи DTO-ове, period→(window, bucket-count) план, и
  `DashboardMath` — чиста, детерминистичка bucketing + KPI/delta математика (без I/O, `now` се прослеђује).

KPI deltas упоређују тренутни window са непосредно претходним (упит добавља дупли window за ово). Не постоји
**live account P&L feed** — платформа има equity само за backtest-ове и prop-firm праћење — тако да је командна tabla
намерно *операциона* (активност, проток, success rate), не брокерски balance ticker.

## Дизајн и токени

Сва боја долази из design token-ова (`var(--app-success|-warning|-error|-info|-primary|-text*)`), тако да a
white-label палета протиче бесплатно — укључујући графикон, чије се серијске боје читају из
решених token-ова у runtime-у преко `window.appReadTokens` (SVG не може директно да конзумира CSS променљиве). No
hard-coded hex било где у dashboard-у. Види [../ui-guidelines.md](../ui-guidelines.md).

## Линк "Powered by cMind"

Командна tabla приказује мали, укусулан линк **"Powered by cMind"** koji показује на овај сајт
документације. Приказан је **по подразумевању** — поносни смо на пројекат и помаже другим трговцима да га
пронађу — али потпуно je вама на вољи. Ресelleри koji покрећу потпуно white-labeled инстанцу преврћу
`App:Branding:ShowSiteLink` на `false` и он нестаје. Види
[White-label branding](./white-label.md#powered-by-link).

## Тестови

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, success-rate,
  previous-period deltas, period parsing, empty/boundary (догађај у `now`, divide-by-zero guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — `UserDashboard` aggregate: подразумевано семе, apply редослед/видљивост,
  append-омитовани, duplicate-collapse, unknown-key одбијање, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read
  model против правог Postgres-а (status/KPI-ови/активност/ресурси, admin node health, empty-user path), нови
  backtests/copy-profiles/agents секције, и layout **round-trip** (сачувај прилагођени layout → поново учитај →
  редослед + видљивост перзистовани).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + мобилни: KPI
  картице, графикон, ring и feed рендерују; period toggle мења активни период и поново учитава; KPI
  продире до `/run`; **сакривање widget-а перзистује преко поновног учитавања**, **Reset** га враћа, и
  Customize дијалог ради на телефону без хоризонталног overflow-а. `/` je takođe u `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) и `MobileJourneyTests`.
