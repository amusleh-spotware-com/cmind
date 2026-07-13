---
description: "Зеркальное отражение основной учетной записи cTrader на одну+ рабских учетных записей — кросс-брокер, кросс-cID — с контролем на назначение + reconciliation денежного класса."
---

# Copy trading

Зеркальное отражение **основной** учетной записи cTrader на одну+ **рабских** учетных записей — кросс-брокер, кросс-cID — с контролем на назначение + reconciliation денежного класса.

## Концепции

- **Профиль копирования** — одна основная (`SourceAccountId`) + одна+ **назначения**. Жизненный цикл: `Draft → Running → Paused → Stopped` (`Error` на отказ). Корневой агрегат: `CopyProfile` (владеет `CopyDestination`).
- **Назначение** — одна рабская учетная запись + полный набор правил для того как основная копируется на нее. Вся конфигурация на назначение, поэтому одна основная питает консервативные + агрессивные рабские одновременно.
- **Хост copy engine** — работающий работник для профиля (`CopyEngineHost`). Подписывается на поток выполнения основной, применяет каждое событие к каждому назначению.
- **Supervisor** — `CopyEngineSupervisor`, фоновый сервис на каждом узле. Размещает назначенные профили, самоисцеляется через кластер (см. [масштабирование](../deployment/scaling.md)).

## Что отражается

| Событие основной | Действие рабской |
|--------------|--------------|
| Открытие позиции Market / market-range | Открыть размер copy (помечена с id исходной позиции) |
| Pending order Limit / stop / stop-limit | Разместить соответствующий pending order |
| Изменение pending order | Изменить зеркальный pending order на месте |
| Отмена pending order / expiry | Отменить зеркальный pending order |
| Partial close | Закрыть ту же пропорцию позиции рабской |
| Scale-in (увеличение объема) | Открыть добавленный объем (opt-in) |
| Stop-loss / trailing-stop изменение | Изменить защиту позиции рабской |
| Full close | Закрыть copy рабской |

Каждый copy **помечен с id исходной позиции/order**. После переподключения хост перестраивает состояние из reconcile: открывает copies основная держит но рабская отсутствует, закрывает "orphans" рабской основная больше не держит — **без дублирования trades**.

## Создание профиля

Диалог **New Profile** на страницах Copy Trading собирает все заранее: имя профиля, source (основная) учетная запись, destination (рабские) учетные записи (multi-select с кнопкой **Select all**; выбранная основная исключена из списка рабских), + полный набор опций на назначение ниже. Все входы **валидированы перед сохранением** — отсутствующее имя/source/destination, non-positive sizing параметр, negative/inconsistent lot bounds, out-of-range drawdown %, no order type enabled, empty symbol filter, или malformed symbol-map пары выводят как список ошибок + блокируют сохранение. На подтверждение профиль создан + каждая выбранная рабская добавлена с выбранными параметрами.

Действия строки уважают жизненный цикл: **Start** включена только когда не работает, **Stop** + **Pause** только когда работает, **Delete** отключена во время работы + просит подтверждение перед удалением профиля + назначений.

## Опции на назначение

Установить в диалоге New Profile, на панели на назначение страницы Copy Trading, или через `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + параметр): fixed lot, lot/notional multiplier, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Плюс min/max lot bounds + force-min-lot. **Risk-from-stop** размер назначения так чтобы оно рисковало настроенный процент *своего собственного* баланса, выведено из **stop-loss расстояния основной** (`основная рисков 2% → рабская auto-рисков 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Основная открытие **без** stop-loss нет расстояния для размер против → использует настроенный **max-risk fallback lot** (M7) если установлен, иначе skipped (`no_stop_loss`) не угадано. Proportional-**equity**/**free-margin** размер от реальной учетной записи **equity** (`balance + Σ floating P&L`, выведено per cTrader Open API которая не доставляет equity), не простой balance — поэтому основная сидит на открытом profit/loss размеры copies правильно. Используемый margin не выведен по reconcile API, поэтому free-margin рассматривается как equity (честный available-funds proxy); другие режимы читают balance + пропускают extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip side (+ swap SL↔ TP) для contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): зеркальные closes, partial closes + protection изменения на уже-скопированных позициях, но открыть **никакие** новые позиции/pending orders (skipped `manage_only`). Используйте для wind down назначение без cutting существующих copies.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (по умолчанию on): на **первый** resync профиля, является ли открытие copies для существующих pre-existing позиций основной, + является ли закрытие copies основная закрыла пока профиль stopped. Оба применяются только при старте — mid-run reconnect всегда reconciles полностью поэтому desync восстанавливается regardless.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Каждый symbol-map запись носит необязательный **per-symbol volume multiplier** (cMAM per-symbol override) масштабирование copy размер для этого символа на top of sizing назначения (1 = no change). Целая карта импорты/экспорты как **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; колонки `Source,Destination,VolumeMultiplier`) — каждая строка валидирована через domain value objects, поэтому malformed файл не может produce invalid map.
- **Trading-hours window** (C18) — per-destination ежедневное UTC окно (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day). Новые opens вне окна skipped (`trading_hours`); окно с `start > end` обертывает past midnight (например 22:00–06:00). Существующие позиции остаются managed.
- **Source-label filter** (C18, cTrader equivalent of MT magic-number filter) — когда установлено, copy только основные trades чей label точно совпадает (например один bot's trades, или manual-only label); иначе skipped (`source_label`). Пусто = copy все. Носится на `ExecutionEvent.SourceLabel` из основной позиции/order's `TradeData.Label`, уважено на resync тоже.
- **Account protection** (ZuluGuard / Global Account Protection) — watch назначения's **live equity** (`balance + Σ floating P&L`, polled каждые `CopyDefaults.EquityGuardInterval`) против `StopEquity` floor и/или необязательного `TakeEquity` ceiling. На breach, apply режим: **CloseOnly** (stop новые copies, keep managing существующие), **Frozen** (stop opening), **SellOut** (close **каждый** copy на назначение immediately). Как только fired, назначение latched — нет новых opens пока хост перезагружается — + `CopyAccountProtectionTriggered` alert raised. `SellOut` требует `StopEquity`; `TakeEquity` должен sit выше `StopEquity`. **No-guarantee caveat:** sell-out использует market execution — как каждый конкурент's equivalent, не может guarantee fill цену в fast/gapped market.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` немедленно закрывает **каждый** скопированный позицию на каждом назначение + locks против новых opens. Маршрутизировано cross-process: API устанавливает флаг, supervisor доставляет на работающий хост (переиспользуя token-rotation канал), который сглаживает на месте; флаг очищен поэтому fires точно один раз (`CopyFlattenAll` alert). Пользователь затем pauses/stops профиль.
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier пользователи просят. Per назначение, **daily-loss cap** (loss из дня's opening equity) и/или **trailing-drawdown** limit (loss от running peak equity), оба в deposit currency. На breach назначение **auto-flattened** (каждый copy закрыт) + **locked out** rest of UTC day (новые opens skipped `prop_lockout`); `CopyPropRuleBreached` alert fires. Lockout очищает когда UTC день rolls over (свежая baseline/peak взята). Делит то же live-equity poll как account protection.
- **Execution jitter** (C11, off по умолчанию) — random `0..N` ms задержка перед размещением каждого copy, для de-correlate near-identical order timestamps поперек пользователя's **собственных** учетных записей. **Compliance caveat:** помощь для prop firms которые *позволяют* копирование — **не** инструмент для evade фирма которая запрещает это; staying в пределах вашего фирма's правил это ваша ответственность.
- **Config lock** (C9) — freeze назначения's параметры для периода (`POST …/destinations/{id}/lock` с minutes). Пока locked, назначение не может быть removed (агрегат отклоняет с `CopyDestinationConfigLocked`) — deliberate guard против impulsive изменения во время drawdown. Lock истекает автоматически при его timestamp.
- **Consistency pre-alert** (C10) — warn (один раз per UTC day) когда назначения's **daily profit** достигает настроенный процент дня's opening equity (`CopyConsistencyThresholdApproaching`), поэтому prop-firm consistency правило уважено *перед* it trips. Profit-side, independent от loss-side lockout; runs off то же day baseline как prop-rule guard.
- **Order-type filter** — выберите точно какие основные order types для copy: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` флаги; по умолчанию все). cMAM-style selectivity.
- **Copy SL / Copy TP** — зеркальные основные's stop-loss / take-profit, или manage protection независимо.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — каждый independently toggleable.
- **Copy pending expiry** (по умолчанию on) — зеркальные основные pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (по умолчанию on) — для market-range + stop-limit orders, разместить рабской order с основной's exact slippage-in-points (base цена взята из рабской's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy если рабской цена moved за N pips из основной entry). **Max copy delay** измерено против основной event's real сервер timestamp (`ExecutionEvent.ServerTimestamp`) через injected `TimeProvider`: signal старше чем настроенный max-lag skipped, поэтому stale copy никогда не placed late (ранее delay всегда ноль + guard мертв).
- **SL/TP precision normalization** (M6) — скопированный stop-loss/take-profit цены rounded на **назначение** symbol's digit precision перед amend, поэтому основной цена на finer precision (или cross-broker digit mismatch) никогда не trip сервер's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — назначение rejecting `CopyDefaults.RejectionBudget` opens в ряд is **tripped**: нет новых opens для cooldown окно (`CopyDestinationTripped` alert fires), stopping rejection storm от hammering (prop-firm) учетная запись. Существующие позиции все еще managed + closed пока tripped; breaker auto-resets после cooldown + успешный copy clears counter.
- **Lot sanity ceiling** (C14) — absolute макс copy размер и/или multiple-of-master cap. Computed copy превышающий absolute cap, или превышающий `N×` основной's собственной lot размер, **hard-blocked** (выведено как `lot_sanity` skip, counted на `cmind.copy.skipped`) не placed — defends против catastrophic-oversize класс (0.23-lot основной turning into 3 lots на каждый receiver через runaway multiplier или rounding bug). Оба dimensions по умолчанию `0` (off).

## Надежность & edge cases

Движок построен для реальности что anything может fail anytime:

- **Slave-pending fill-correlation timeout** (C13) — зеркальный рабской pending чья основная pending исчезла (ни resting ни свежо filled) отменена после correlation timeout, поэтому рабской copy не может fill uncorrelated в unmanaged позицию (`CopyPendingTimedOut`). Resync также очищает order-id-labelled filled-pending orphan.
- **Robust close/flatten** (M8) — closing orphan на resync, или flattening на guard breach, tolerate позиция broker уже закрыта (`POSITION_NOT_FOUND`): каждый close работает независимо, поэтому один stale id никогда не aborts resync или leaves rest of учетная запись un-flattened.

- **Start с основной уже в trades** — на start хост reconciles + открывает copies для основной's существующих позиций.
- **Connection drops / desync** — на reconnect хост reconciles: открывает missing copies, закрывает orphans, re-labels pendings. Нет duplicate orders.
- **Order placement failure** — failure на один назначение logged, никогда не blocks другие назначения.
- **Single valid token per cID** — cTrader инвалидирует cID's old access token момент новый issued. cMind swaps работающий хост's token **на месте** (re-auth на live socket) поэтому copying продолжает без dropping stream. См. [token lifecycle](token-lifecycle.md).

## Auditability

Каждое действие emits структурированный, source-generated log event (`LogMessages`) с profile id, destination cID, order/position ids, + values — order placed/skipped (с reason), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry зеркальный, market-range slippage зеркальный, token swapped, resync summary. Это audit trail для compliance + dispute resolution.

Наряду с logs, движок emits **OpenTelemetry метрики** на `cMind.Copy` meter (зарегистрирован в общем OTel pipeline, exported через OTLP / на Azure Monitor как rest): `cmind.copy.latency` (основной-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out на все назначения, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged по назначению), `cmind.copy.skipped` (tagged по reason), + `cmind.copy.failed`. Эти делают latency/slippage regression measurable, не просто видимый в log line — live suite asserts их против budget.

## API

- `GET /api/copy/profiles` — список.
- `POST /api/copy/profiles` — создать (с необязательными destination account ids).
- `GET /api/copy/profiles/{id}` — полный detail incl. каждый destination опции.
- `POST /api/copy/profiles/{id}/destinations` — добавить назначение с полным набором опций.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — удалить.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — жизненный цикл.

## Тесты

- **Unit** (`tests/UnitTests/CopyTrading`) — режимы sizing, фильтры решения, order-type фильтр, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Работает против `FakeTradingSession`, cTrader-faithful in-memory симулятор.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation на реальном Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip через API + UI, полный жизненный цикл.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` на quiescence + assert convergence invariантs. См. [testing/stress-testing.md](../testing/stress-testing.md). Этот набор выведена + fixed реальный startup race: `OnReconnected` подключена перед initial reference-load + resync, поэтому socket flap во время startup мог бы run второй resync concurrently + corrupt хост's non-concurrent state dictionaries — startup load + первый resync теперь run under `_stateGate`.
- **Live** — реальные cTrader demo учетные записи; см. [testing/live-copy-trading.md](../testing/live-copy-trading.md).

См. [dev-credentials.md](../testing/dev-credentials.md) для одного credentials файл live + E2E tiers читать.
