---
description: "Mirror master cTrader account na jeden+ slave accounts — cross-broker, cross-cID — z per-destination control + money-grade reconciliation."
---

# Copy trading

Mirror **master** cTrader account na jeden+ **slave** accounts — cross-broker, cross-cID — z per-destination control + money-grade reconciliation.

## Koncepty

- **Copy profile** — jeden master (`SourceAccountId`) + jeden+ **destinations**. Lifecycle: `Draft → Running → Paused → Stopped` (`Error` na failure). Aggregate root: `CopyProfile` (owns `CopyDestination`).
- **Destination** — jeden slave account + pełny rule set dla how master copied na to. Wszystko config per-destination, więc jeden master karmi conservative + aggressive slaves jednocześnie.
- **Copy engine host** — running worker dla profile (`CopyEngineHost`). Subscribes master execution stream, applies każdy event do każdy destination.
- **Supervisor** — `CopyEngineSupervisor`, background service na każdy node. Hosts assigned profiles, self-heals across cluster (see [scaling](../deployment/scaling.md)).

## Co gets mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open sized copy (labelled z source position id) |
| Limit / stop / stop-limit pending order | Place matching pending order |
| Pending order amend | Amend mirrored pending order w place |
| Pending order cancel / expiry | Cancel mirrored pending order |
| Partial close | Close same proportion slave position |
| Scale-in (volume increase) | Open added volume (opt-in) |
| Stop-loss / trailing-stop change | Amend slave position's protection |
| Full close | Close slave copy |

Każdy copy **labelled z source position/order id**. Po reconnect host rebuilds state z reconcile: opens copies master holds ale slave missing, closes slave "orphans" master już nie holds — **without duplicating trades**.

## Tworzenie profilu

**New Profile** dialog na Copy Trading page zbiera wszystko up front: profile name, source (master) account, destination (slave) accounts (multi-select z **Select all** button; chosen master excluded z slave list), + pełny per-destination option set poniżej. Wszystkie inputs **validated przed saving** — missing name/source/destination, non-positive sizing param, negative/inconsistent lot bounds, out-of-range drawdown %, no order type enabled, empty symbol filter, lub malformed symbol-map pairs surface jako error list + block save. Na confirm, profile created + każdy selected slave added z chosen settings.

Row actions respect lifecycle: **Start** enabled tylko gdy nie running, **Stop** + **Pause** tylko gdy running, **Delete** disabled gdy running + asks confirmation przed removing profile + destinations.

## Per-destination options

Set w New Profile dialog, na Copy Trading page's per-destination panel, lub via `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Plus min/max lot bounds + force-min-lot. **Risk-from-stop** sizes destination więc risks configured percent z *its own* balance, derived z **master's stop-loss distance** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master open **without** stop-loss ma no distance do size against → uses configured **max-risk fallback lot** (M7) jeśli set, else skipped (`no_stop_loss`) nie guessed. Proportional-**equity**/**free-margin** size off real account **equity** (`balance + Σ floating P&L`, derived per cTrader Open API które doesn't deliver equity), nie plain balance — więc master sitting na open profit/loss sizes copies right. Used margin nie exposed przez reconcile API, więc free-margin treated jako equity (honest available-funds proxy); inne modes read balance + skip extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip side (+ swap SL↔TP) dla contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes, partial closes + protection changes na already-copied positions, ale open **no** new positions/pending orders (skipped `manage_only`). Use do wind destination down bez cutting existing copies.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): na profile's **first** resync, czy do open copies dla master's pre-existing positions, + czy do close copies master closed gdy profile stopped. Oba apply tylko na start — mid-run reconnect zawsze reconciles fully więc desync recovers regardless.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Każdy symbol-map entry carries optional **per-symbol volume multiplier** (cMAM per-symbol override) scaling copy size dla tego symbol na top z destination's sizing (1 = no change). Cały map imports/exports jako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; columns `Source,Destination,VolumeMultiplier`) — każdy row validated przez domain value objects, więc malformed file nie może produce invalid map.
- **Trading-hours window** (C18) — per-destination daily UTC window (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day). Nowe opens poza window skipped (`trading_hours`); window z `start > end` wraps past midnight (np. 22:00–06:00). Istniejące positions stay managed.
- **Source-label filter** (C18, cTrader equivalent z MT magic-number filter) — gdy set, copy tylko master trades których label matches **exactly** (np. jeden bot's trades, lub manual-only label); else skipped (`source_label`). Puste = copy all. Carried na `ExecutionEvent.SourceLabel` z master position/order's `TradeData.Label`, honored na resync too.
- **Account protection** (ZuluGuard / Global Account Protection) — watch destination's **live equity** (`balance + Σ floating P&L`, polled każdy `CopyDefaults.EquityGuardInterval`) contra `StopEquity` floor i/lub optional `TakeEquity` ceiling. Na breach, apply mode: **CloseOnly** (stop new copies, keep managing existing), **Frozen** (stop opening), **SellOut** (close **every** copy na destination immediately). Raz fired, destination latched — no new opens dopóki host restarts — + `CopyAccountProtectionTriggered` alert raised. `SellOut` requires `StopEquity`; `TakeEquity` must sit above `StopEquity`. **No-guarantee caveat:** sell-out uses market execution — jak każdy competitor's equivalent, nie może guarantee fill price w fast/gapped market.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` immediately closes **every** copied position na każdy destination + locks przeciw new opens. Routed cross-process: API sets flag, supervisor delivers do running host (reusing token-rotation channel), który flattens w place; flag cleared więc fires dokładnie raz (`CopyFlattenAll` alert). User wtedy pauses/stops profile.
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier users ask dla. Per destination, **daily-loss cap** (loss z day's opening equity) i/lub **trailing-drawdown** limit (loss z running peak equity), oba w deposit currency. Na breach destination **auto-flattened** (każdy copy closed) + **locked out** rest z UTC day (new opens skipped `prop_lockout`); `CopyPropRuleBreached` alert fires. Lockout clears gdy UTC day rolls over (fresh baseline/peak taken). Shares same live-equity poll jako account protection.
- **Execution jitter** (C11, off domyślnie) — random `0..N` ms delay przed placing każdy copy, do de-correlate near-identical order timestamps across user's **own** accounts. **Compliance caveat:** aid dla prop firms które *permit* copying — **nie** tool do evade firm które forbids to; staying within Twojej firm's rules to Twoja responsibility.
- **Config lock** (C9) — freeze destination's settings dla period (`POST …/destinations/{id}/lock` z minutes). Gdy locked, destination nie może być removed (aggregate rejects z `CopyDestinationConfigLocked`) — deliberate guard przeciw impulsive changes podczas drawdown. Lock expires automatically na jego timestamp.
- **Consistency pre-alert** (C10) — warn (raz per UTC day) gdy destination's **daily profit** reaches configured percent z day's opening equity (`CopyConsistencyThresholdApproaching`), więc prop-firm consistency rule respected *przed* to trips. Profit-side, independent z loss-side lockout; runs off same day baseline jako prop-rule guard.
- **Order-type filter** — choose dokładnie które master order types do copy: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit, lub manage protection independently.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — każdy independently toggleable.
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (default on) — dla market-range + stop-limit orders, place slave order z master's dokładny slippage-in-points (base price taken z slave's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy jeśli slave price moved beyond N pips z master entry). **Max copy delay** measured przeciw master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) via injected `TimeProvider`: signal older niż configured max-lag skipped, więc stale copy nigdy placed late (previously delay zawsze zero + guard dead).
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices rounded do **destination** symbol's digit precision przed amend, więc master price na finer precision (lub cross-broker digit mismatch) nigdy trips server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — destination rejecting `CopyDefaults.RejectionBudget` opens w row jest **tripped**: no new opens dla cooldown window (`CopyDestinationTripped` alert fires), stopping rejection storm z hammering (prop-firm) account. Istniejące positions wciąż managed + closed gdy tripped; breaker auto-resets po cooldown + successful copy clears counter.
- **Lot sanity ceiling** (C14) — absolute max copy size i/lub multiple-of-master cap. Computed copy exceeding absolute cap, lub exceeding `N×` master's own lot size, **hard-blocked** (surfaced jako `lot_sanity` skip, counted na `cmind.copy.skipped`) nie placed — defends przeciw catastrophic-oversize class (0.23-lot master turning do 3 lots na każdy receiver via runaway multiplier lub rounding bug). Oba dimensions default `0` (off).

## Niezawodność & edge cases

Engine built dla reality że coś może fail anytime:

- **Slave-pending fill-correlation timeout** (C13) — mirrored slave pending którego master pending vanished (ani resting ani freshly filled) cancelled po correlation timeout, więc slave copy nie może fill uncorrelated do unmanaged position (`CopyPendingTimedOut`). Resync także cleans order-id-labelled filled-pending orphan.
- **Robust close/flatten** (M8) — closing orphan na resync, lub flattening na guard breach, tolerates position broker już closed (`POSITION_NOT_FOUND`): każdy close runs independently, więc jeden stale id nigdy nie aborts resync lub leaves rest z account un-flattened.

- **Start z master już w trades** — na start host reconciles + opens copies dla master's existing positions.
- **Connection drops / desync** — na reconnect host reconciles: opens missing copies, closes orphans, re-labels pendings. Nie duplicate orders.
- **Order placement failure** — failure na jeden destination logged, nigdy blocks inne destinations.
- **Single valid token per cID** — cTrader invalidates cID's old access token moment nowy issued. cMind swaps running host's token **w place** (re-auth na live socket) więc copying continues bez dropping stream. See [token lifecycle](token-lifecycle.md).

## Auditability

Każdy action emits structured, source-generated log event (`LogMessages`) z profile id, destination cID, order/position ids, + values — order placed/skipped (z reason), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. To audit trail dla compliance + dispute resolution.

Alongside logs, engine emits **OpenTelemetry metrics** na `cMind.Copy` meter (registered w shared OTel pipeline, exported over OTLP / do Azure Monitor jak rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out do wszystkie destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged przez destination), `cmind.copy.skipped` (tagged przez reason), + `cmind.copy.failed`. Te make latency/slippage regression measurable, nie just visible w log line — live suite asserts je przeciw budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (z optional destination account ids).
- `GET /api/copy/profiles/{id}` — full detail incl. każdy destination option.
- `POST /api/copy/profiles/{id}/destinations` — add destination z pełny option set.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Testy

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Runs противу `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integracja** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation na real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip przez API + UI, pełny lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` do quiescence + assert convergence invariants. See [testing/stress-testing.md](../testing/stress-testing.md). Ten suite surfaced + fixed real startup race: `OnReconnected` wired przed initial reference-load + resync, więc socket flap podczas startup mógł run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync teraz run pod `_stateGate`.
- **Live** — real cTrader demo accounts; see [testing/live-copy-trading.md](../testing/live-copy-trading.md).

See [dev-credentials.md](../testing/dev-credentials.md) dla single credentials file live + E2E tiers read.
