---
title: Dashboard
description: Dashboard cMind — живой, mobile-first командный центр для ваших запусков cBot, бэктестов, ресурсов и кластера узлов.
---

# Dashboard

Первое, что вы видите при входе, и, честно говоря, страница, которую вы оставите открытой на весь день.
Лендинг-страница (`/`, `Components/Pages/Index.razor`) — это **живой, mobile-first командный центр**
для активности пользователя across cBot runs, бэктестов, ресурсов и (для админов) кластера узлов.
Она обновляет себя, отлично выглядит на телефоне и никогда не заставляет вас нажимать F5.

## Что показывает

Сверху вниз, по приоритету для телефона (каждый блок — full-width stack item на мобильном,
responsive grid на планшете/десктопе):

1. **Header** — title, live-индикатор (реальная пульсирующая точка; статичная при `prefers-reduced-motion`),
   время последнего обновления и **переключатель периода** (`1H · 24H · 7D · 30D`), который управляет KPI и графиком.
2. **Hero KPIs** — четыре беглых карточки, каждая с большим числом + inline SVG sparkline, и (где
   уместно) **дельту vs предыдущий период**:
   - **Active now** — запуски + бэктесты, стартующие/работающие сейчас.
   - **Success rate** — завершённые ÷ (завершённые + проваленные) за период; дельта в процентных пунктах.
   - **Completed** — завершённые запуски/бэктесты за период; дельта vs предыдущий период.
   - **Failed** — провалы за период; дельта (меньше = лучше, поэтому снижение показывается зелёным).
3. **График активности** — ApexCharts area timeline запущенных / завершённых / проваленных per временной корзины.
4. **Instance status ring** — donut диаграмма running / бэктесты / pending / completed / failed, итого в центре.
5. **Бэктесты** — три тайла snapshot (running / completed / failed), click-through на `/backtest`.
6. **Copy trading** — ваши copy-trading профили с live-индикатором статуса, количеством назначений и **Live**
   badge на работающих профилях; click-through на `/copy-trading`.
7. **AI-агенты** — ваши trading-агенты с состоянием (archetype · status) и временем последнего действия; click-through на `/agent-studio`.
8. **Live-лента активности** — 20 самых недавних событий (новые первые) с цветным индикатором статуса и относительной временной меткой.
9. **Cluster health** (только админы) — active-vs-total узлов и gauge использования вместимости.
10. **Resource tiles** — cBots, trading accounts, cTrader IDs, MCP keys (click-through на их страницы).

## Настройка dashboard

Каждый блок — это **виджет под вашим контролем**. Нажмите **Customize** (вверху справа от header),
чтобы открыть диалог, где вы **показываете/скрываете** любой виджет и **переупорядочиваете** их стрелками вверх/вниз.
**Reset to default** восстанавливает каталожный порядок. Ваш выбор **персистится на сервере per пользователь**, поэтому
он следует за вами across браузеров и устройств — не только в этой вкладке.

- Виджеты с feature-gate и admin-only (Copy trading, AI agents, Cluster health) появляются в
  диалоге только когда ваш deployment/роль может их использовать.
- Каталожный источник виджетов — один: `Core/Dashboard/DashboardWidgets.cs`; презентация
  (label + icon + availability) живёт в `Components/Dashboard/DashboardWidgetMeta.cs`.

## Как остаётся живым

Страница опрашивает `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` каждые 10 секунд и рендерит
виджеты на месте — без ручной перезагрузки. Транзиентная ошибка fetch'а проглатывается и повторяется на следующем тике;
цикл чисто останавливается при dispose. Первая загрузка показывает skeleton; персистентный сбой показывает error
card с **Retry**; пользователь без данных видит нулевые KPI и empty-state copy.

## Backend

- `Endpoints/DashboardEndpoints.cs` маппит `/overview` (и сохраняет старые скалярные `/stats`). Работает
  per-пользователя и admin-gated через `ICurrentUser`; часы из `TimeProvider`. Также маппит
  `GET/PUT /api/dashboard/layout` — layout виджетов пользователя, загружаемый при старте страницы и сохраняемый из
  диалога Customize.
- **Layout persistence** — агрегат `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): одна доска
  на пользователя (уникальный по `UserId`), владеющий упорядоченным списком настроек виджетов (visible + order),
  хранящихся как `jsonb` колонка. Упорядоченный список мутирует только через `Apply` / `Reset`, которые валидируют каждый
  ключ против каталога `DashboardWidgets` и сохраняют коллекцию полной и дедуплицированной. Неизвестные
  ключи отклоняются с `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` строит композитный read model `DashboardOverview`: snapshot статуса за всё время
  (grouped counts), окно инстансов materialized один раз, и counts ресурсов/нод.
  Статус инстанса и terminal timestamps живут на TPH-подтипах (не колонках), поэтому строки читаются в памяти
  через shared helpers `InstanceEndpoints.GetStartedAt/GetStoppedAt`. Время события =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` содержит DTO, план period→(window, bucket-count) и
  `DashboardMath` — чистую, детерминированную логику бакинга + KPI/delta (без I/O, `now` передаётся).

Дельты KPI сравнивают текущее окно с непосредственно предшествующим (запрос.fetch'ит двойное окно для этого).
**Нет живого P&L фида** — платформа имеет equity только для бэктестов и prop-firm tracking — поэтому dashboard
сознательно *операционный* (активность, throughput, success rate), не брокерский баланс-тикер.

## Дизайн & токены

Все цвета из design tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), поэтому
white-label палитра течёт бесплатно — включая график, чьи серии цветов читаются из resolved tokens
в runtime через `window.appReadTokens` (SVG не может напрямую потреблять CSS-переменные). Нигде нет
hard-coded hex. См. [../ui-guidelines.md](../ui-guidelines.md).

## Ссылка "Powered by cMind"

Dashboard показывает маленькую, уместную ссылку **"Powered by cMind"**, ведущую на этот
документационный сайт. Она **показывается по умолчанию** — мы гордимся проектом и это помогает другим
трейдерам его найти — но это полностью ваше решение. Реселлеры, запускающие полностью white-labeled
инстанс, ставят `App:Branding:ShowSiteLink` в `false` и она исчезает. См.
[White-label branding](./white-label.md#powered-by-link).

## Тесты

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — бакинг, success-rate,
  дельты предыдущего периода, парсинг периода, empty/boundary (событие в `now`, guard от деления на ноль).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — агрегат `UserDashboard`: seed по умолчанию,
  apply order/visibility, append-omitted, duplicate-collapse, отклонение неизвестного ключа, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — read model
  против реального Postgres (status/KPI/activity/resources, admin node health, path для пустого пользователя), новые секции бэктестов/copy-profiles/agents, и **round-trip** layout (сохранить custom layout → перезагрузить →
  порядок + visibility персистились).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: KPI
  cards, график, ring и лента рендерятся; переключатель периода переключает активный период и перезагружает; KPI
  drill-through на `/run`; **скрытие виджета персистится при перезагрузке**, **Reset** возвращает,
  и диалог Customize работает на телефоне без горизонтального overflow. `/` также в `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) и `MobileJourneyTests`.
