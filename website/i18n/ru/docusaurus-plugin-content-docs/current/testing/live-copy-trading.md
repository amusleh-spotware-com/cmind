---
description: "Полный воспроизводимый набор тестов copy-trading. Два слоя: детерминированные (без сети) и live E2E (реальные cTrader демо-счета)."
---

# Copy-trading test suite (deterministic + live)

Полный воспроизводимый copy-trading test suite. Два слоя:

1. **Детерминированные тесты** (xUnit, без сети) — copy math + engine logic. Быстрые, CI, без секретов. Покрывают
   every money-management mode, every filter/option, engine resilience.
2. **Live E2E тесты** (реальные cTrader демо-счета) — production `CopyEngineHost` размещает + копирует
   реальные ордера между реальными счетами. Полностью автоматизированы, перезапускаемы как unit test:
   читают cached creds из локальных gitignored файлов, self-refresh access token, пропускают чисто когда
   секреты отсутствуют (CI остаётся зелёным).

Никогда не запускаются против live-funded счёта — каждый счёт **демо**, каждый live тест закрывает
позиции которые открыл.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — every sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic against an in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads the gitignored secrets, saves refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens from the app DB into the token cache
  LiveCopyFixture.cs             — rotates the access token, exposes the demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — the live scenarios (1:1, 1:many, reverse, …)
```

## Secrets (локальные, gitignored — никогда не коммитятся)

Все creds under `<repo>/secrets/` (already in `.gitignore`). Dev пишет **только первые два файла**; третий (tokens) auto-produced by onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds для авторизации (один или много):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **written by onboarding**, multi-cID, refreshed every run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **никогда не истекает**, поэтому после one-time onboarding live тесты работают бессрочно: каждый run
обменивает refresh token каждого cID на fresh access token (rotation) — без браузера, без промптов.

## One-time onboarding (полностью автоматизирован — без взаимодействия разработчика кроме сохранения creds)

Onboarding drives real cTrader ID login in headless browser from saved cID creds, captures OAuth
callback on local HTTPS listener at app's registered redirect (`https://localhost:7080/openapi/callback`),
exchanges code for tokens, loads account list, writes multi-cID token cache. Run once per machine (or when adding cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Авторизует каждый cID в `openapi-cids.local.json`, пишет `openapi-tokens.local.json`. После этого live copy
тесты не требуют ничего другого.

**Альтернативный bootstrap** (если аккаунты уже авторизованы в running app): decrypt stored tokens
straight out of app's Postgres volume instead of re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Безопасность — только демо

Live тесты торгуют **только демо-счета**: fixture фильтрует token cache на аккаунты с `IsLive == false` и
подключается к demo gateway, поэтому ордер никогда не попадёт на live/funded аккаунт даже если live аккаунт
авторизован. Каждая позиция которую тест открыл закрывается в cleanup.

## Запуск

```bash
# Детерминированные copy тесты только (быстрые, без секретов, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy тесты против реальных демо-счетов (нужны два secrets файла)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Всё
dotnet test
```

Без файлов секретов live тесты печатают reason skip + pass как no-ops, поэтому suite безопасно запускать где угодно.

## Покрытие

### Money management / sizing (детерминированные — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** и **down** для balance/leverage/capacity mismatch · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filters (детерминированные — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (детерминированные — `CopyEngineHostTests`, in-memory session)
Open mirrors a market order (side / volume / label) · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves the destination symbol · **order-failure on one slave still copies to the
others** · source close closes the mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (детерминированные — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects and re-auths · fatal auth error faults ·
exponential backoff.

### Live, реальные cTrader демо-счета (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors to slave
under another, each authenticating with own token). Each opens real min-lot position on master, waits
for engine to mirror it (matched by source-position-id label on slave), asserts, closes everything.
Closed market reported **Inconclusive**, not failing.

## Logging & auditability

Каждая copy trading operation логируется via source-generated structured events (`Core/Logging/LogMessages.cs`,
event IDs 1043–1055), full trail auditable:

| Event | Id | Meaning |
|-------|----|---------|
| CopyHostStarted | 1046 | a profile's engine came up (source + destination count) |
| CopySourceOpen | 1047 | master opened a position (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order sent to a slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | a copy was skipped and why (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applied to a slave copy |
| CopyOpenFailed | 1051 | a slave copy-open failed (isolated — other slaves continue) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master closed → slave copy closed |
| CopyCloseFailed | 1054 | a slave copy-close failed |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphans closed) |
| CopyPartialClose | 1056 | master partial close mirrored |
| CopyScaleIn | 1057 | master scale-in mirrored |
| CopyPendingOrderPlaced | 1058 | pending limit/stop mirrored to a slave |
| CopyPendingOrderCancelled | 1059 | source pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop applied to a slave copy |
| CopyStopLossAmended | 1061 | source SL move re-amended the slave copy |
| CopyHostTokenRotated | 1062 | supervisor restarted a running host after its access token rotated |

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

Host mirrors more than market open/close. Each behaviour = per-destination opt-in flag on `CopyDestination`.

| Behaviour | Детерминированный тест | Live тест |
|-----------|--------------------------------------------|-----------|
| Partial close → proportional slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave` ✅ | ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` ✅ | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records token signature on each running host и,
  every reconcile, rebuilds plan from DB (freshly rotated by `OpenApiTokenRefreshService`). Changed
  signature restarts host (`CopyHostTokenRotated`, 1062); new host's `ResyncAsync` rebuilds state
  without duplicating trades.
- **Node affinity (no double-copy).** Both Web local node и `CopyAgent` worker run a supervisor. Each
  running profile claimed by exactly one node (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim).
  Supervisor hosts only profiles it owns; stop/pause releases claim.

### Single-use refresh tokens (важно)

cTrader **refresh tokens are single-use** — each refresh returns *new* refresh token, invalidates old.
Live fixture refreshes on start, persists rotated token to `secrets/openapi-tokens.local.json`.

## Запуск suite в Kubernetes cluster

Whole suite runs in-cluster against Helm-deployed app. See [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```
