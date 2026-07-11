# Copy trading — remaining work (implementation plan)

## Already shipped (context for the next session)

All on `main`. Copy trading live-verified end to end against real cTrader **demo** accounts:
- **OAuth fixed + UX**: callback state in HttpOnly cookie (cTrader never echo `state`); one Open API app per user; add/edit via MudBlazor dialog; success page auto-redirect (authorized→/accounts, invite→/); warn on app change when accounts authorized.
- **Automated OAuth onboarding** (`tests/E2ETests/CopyLive/OpenApiOnboarding.cs` + `OnboardingTests`, `CMIND_ONBOARD=1`): headless cTrader ID login from saved cID creds → capture code on local HTTPS listener → write **multi-cID** token cache. Refresh tokens never expire → live tests self-refresh, zero interaction. Both cIDs onboarded (amusleh demo, afhacker has some LIVE accounts).
- **Live copy verified** (`CopyTradingLiveTests`): 1:1, 1:many, reverse, **cross-cID** all place real master order, assert engine mirrors onto slave(s), then clean up. **Demo-only safety**: `LiveCopyFixture` filters to `IsLive==false` + demo gateway, so no order hits funded account.
- **Deterministic coverage** (175+ unit tests): full sizing matrix (all money-management modes, leverage/balance/currency scaling, min/max/step), decision filters (direction/reverse/slippage/delay/symbol filter/size-zero), `CopyEngineHostTests` via `FakeTradingSession` (open/side, reverse+SL/TP swap, symbol map, **per-slave order-failure isolation**, close mirror, reconnect resync, audit log).
- **Engine hardening**: `CopyEngineHost` now takes `IOpenApiTradingSessionFactory` (testable); per-slave try/catch so one slave failure never blocks others.
- **Audit logging**: every copy op = structured event (`LogMessages` 1046–1055), Serilog JSON, per-category configurable.
- **Secrets** (gitignored, local): `openapi-test-app.local.json` (client id/secret), `openapi-cids.local.json` (cID logins), `openapi-tokens.local.json` (multi-cID token cache, from onboarding). Alt bootstrap decrypts tokens from app's Postgres volume (`LiveTokenBootstrapTests`, `CMIND_VOLUME_CONN`).

**Left = three items below.** Not started; each real engine/infra work.

---

Plan for three remaining items. Each standalone; implement by value: **1 → 2 → 3**. Follow repo rules: invoke `ddd-dotnet` skill before touching `src/`, run Rider `get_file_problems` on every changed `.cs`/`.razor`, keep `dotnet build` at 0 warnings/0 errors, **verify live against demo accounts** (never live/funded — fixture filters to demo).

Read first: `docs/testing/live-copy-trading.md` (how deterministic + live suites work, secrets, onboarding). Key production files:
- `src/CopyEngine/CopyEngineHost.cs` — copy loop (open → mirror, close → close, resync).
- `src/CopyEngine/CopyDecisionEngine.cs`, `src/Core/CopyTrading/CopySizingCalculator.cs` — pure sizing/decision.
- `src/CTraderOpenApi/Client/OpenApiTradingSession.cs` — wire API (`IOpenApiTradingSession` + `IOpenApiTradingSessionFactory`); `ExecutionEvent`, `OpenPositionSnapshot`, `SymbolDetails`.
- `src/Core/CopyTrading/CopyEntities.cs` (`CopyDestination`), `CopyValueObjects.cs` (enums/VOs).
- `src/Nodes/CopyTrading/CopyEngineSupervisor.cs`, `OpenApiTokenRefreshService.cs`; `src/CopyAgent/`.
Tests: `tests/UnitTests/CopyTrading/*` (`FakeTradingSession`, `CopyEngineHostTests`), `tests/IntegrationTests/CopyLive/*` (`LiveCopyFixture`, `LiveCopyScenario`, `CopyTradingLiveTests`).

---

## 1. Partial-close mirroring · non-market order types · SL-trailing

Today `CopyEngineHost` mirrors only **market opens** and **full closes**:
- `HandleOpenAsync` dedups by `_openSourcePositions.Add(positionId)`, so second event on same source position (partial close, volume change, trailing SL move) dropped.
- `SourceExecutionsAsync` (in `OpenApiTradingSession`) **skips events with `Position == null`**, so pending orders (limit/stop) never reach host.
- Copies always `SendMarketOrderAsync`; closes always close **full** labelled copy.

### 1a. Partial-close mirroring
- **Host state:** add `Dictionary<long,long> _sourceVolumes` (sourcePositionId → last wire volume). On Open event for already-tracked id whose volume **decreased**, treat as partial close: compute `closedFraction = (old - new) / old`; per destination, reconcile labelled copy and `ClosePositionAsync` proportional slice (`round(copyVolume * closedFraction)` to dest step, min volume floor — reuse normalization ideas in `CopySizingCalculator.Normalize`). Update `_sourceVolumes`. Volume **increase** = scale-in; either mirror as extra market order sized by delta, or skip — gate on new `CopyDestination` flag (below). Default: mirror partial **closes**, ignore scale-ins (document choice).
- `ClosePositionAsync(ctid, positionId, volume, ct)` already accepts partial volume — no wire change.
- **Domain:** add `CopyDestination.MirrorPartialClose` (bool, default true) + `MirrorScaleIn` (default false) via intention methods (`SetPartialCloseMirroring(bool)`, …), `private set`, jsonb-persisted like existing copy flags; **add EF migration** (mirror `AddCopySymbolFilters`). Surface both in `AddCopyDestinationRequest` (`CopyEndpoints`) + destination UI grid in `CopyTrading.razor`.

### 1b. Non-market order types (pending limit/stop)
- **Wire:** add `SendPendingOrderAsync(ctid, symbolId, isBuy, volume, ProtoOAOrderType, double price,
  string label, ct)` to `IOpenApiTradingSession`/`OpenApiTradingSession` — same as `SendMarketOrderAsync` but `OrderType = Limit|Stop` with `LimitPrice`/`StopPrice` set (verify exact `ProtoOANewOrderReq` field names in generated protobuf under `src/CTraderOpenApi/Messages`).
- **Source events:** `SourceExecutionsAsync` must surface order events, not just positions. Extend `ExecutionEvent` (or add `OrderEvent`) carrying order type + price + status (`ProtoOAExecutionEvent.Order`, `OrderType`, `LimitPrice`/`StopPrice`, `OrderStatus`). Handle `ORDER_ACCEPTED` (pending placed) → place matching pending on dest labelled with source **order** id; `ORDER_CANCELLED` → cancel dest pending (needs `CancelOrderAsync` → `ProtoOACancelOrderReq`); `ORDER_FILLED` → existing position-open path takes over (dedupe so filled pending not double-placed — key by source order id → source position id).
- **Domain:** `CopyDestination.CopyPendingOrders` (bool, default false) — when false, keep today's behavior (copy only on fill). Migration + request + UI as in 1a.

### 1c. SL-trailing
- **Wire:** cTrader supports trailing stop on position/order. Verify fields on `ProtoOAAmendPositionSLTPReq` / `ProtoOANewOrderReq` (likely `TrailingStopLoss` bool + `StopTriggerMethod`). Extend `AmendPositionSltpAsync` (or add `SetTrailingStopAsync`) to set it.
- **Host:** in `ApplyProtectionAsync`, when source position has trailing stop and destination opts in, set trailing on copy instead of fixed SL. Also mirror **subsequent SL moves**: source Open event on tracked id with changed `StopLoss` (unchanged volume) = SL amend → re-amend dest copy. Needs same "second event on same id" handling as 1a (so wire 1a first).
- **Domain:** `CopyDestination.CopyTrailingStop` (bool, default false). Migration + request + UI.

### Tests for #1 (this is the bar — cover every path)
- **Deterministic** (`CopyEngineHostTests` + extend `FakeTradingSession` to record pending orders, partial closes with volume, trailing amends, cancels):
  - partial close mirrors **proportional** slice on each slave (e.g. source 1.0→0.4 closes 60% on copy); volume rounds to dest step; below-min behaves per policy.
  - scale-in ignored by default; mirrored when `MirrorScaleIn` on.
  - pending **limit** and **stop** orders placed on slave with correct type/price/label when `CopyPendingOrders` on; not placed when off.
  - source order **cancel** cancels dest pending.
  - filled pending does **not** double-open (order-id → position-id dedupe).
  - trailing stop applied to copy when `CopyTrailingStop` on; source SL move re-amends copy.
  - add sizing/VO tests for new flags.
- **Live** (`LiveCopyScenario` + `CopyTradingLiveTests`, demo only, clean up every position/order):
  - open on master then **partial close** → assert slave copy shrinks proportionally.
  - place **limit** order on master away from price → assert matching pending appears on slave; cancel on master → assert cancelled on slave.
  - open with **trailing stop** → assert slave copy has trailing stop.
  - Extend audit log (`Core/Logging/LogMessages.cs`, next ids 1056+) with `CopyPartialClose`, `CopyPendingOrderPlaced`, `CopyPendingOrderCancelled`, `CopyTrailingApplied`; assert they fire.

---

## 2. Token rotation propagation to running hosts (incl. external CopyAgent nodes)

`OpenApiTokenRefreshService` (`src/Nodes/CopyTrading`) refreshes each `OpenApiAuthorization` near expiry, persists new token via `authorization.Refresh(...)`. `CopyEngineSupervisor.BuildPlanAsync` **reads token from DB each reconcile cycle**, so *newly hosted* profile uses fresh token. **Gap:** profile **already running** holds old token inside its `OpenApiConnection` (set once at `AttachAccount`); on reconnect it re-auths with stale token. If old access token revoked on rotation, running host breaks.

### Implementation
- **Detect rotation for running hosts.** In `CopyEngineSupervisor.ReconcileAsync`, alongside running set, compare token(s) in each running host's plan against current DB token; if changed, **cancel + restart** that host with fresh plan (cleanest, reuses `BuildPlanAsync`). Store token (or hash) on `HostHandle` to compare. Restart must be seamless: new host's `ResyncAsync` rebuilds state from reconcile (open-missing / close-orphaned) without duplicating trades — already how reconnect works, so restart safe.
- **Alternative (lower churn):** add way to push new token into live `OpenApiConnection` (`AttachAccount` already replaces token in `_accounts`; add `RefreshAccountToken` that also triggers re-auth or controlled reconnect). Restart simpler and safer — prefer it unless churn matters.
- **External nodes / no double-copy.** `CopyAgent` worker runs own `CopyEngineSupervisor`, so does Web local node — **both currently host every Running profile → double execution.** Before (or with) this work, add **node affinity**: assign each running profile to exactly one node (`CopyProfile.AssignedNodeId` or claim row), each supervisor only hosts profiles assigned to its node id (env/`AppOptions` node identity). Without this, rotation tests ambiguous and prod double-copies. Prerequisite — call it out.

### Tests for #2
- **Deterministic/integration:** fake `IOpenApiTokenClient` returns rotated token; drive supervisor reconcile (or host directly) and assert running host restarts / re-auths with **new** token and keeps copying (extend `FakeTradingSession` to record token seen at `AttachAccount`).
- **Node affinity:** integration test that two supervisors with different node ids do **not** both host same profile (only assigned one does).
- **Live (optional, hard):** access tokens last ~30 days, so real expiry untestable in CI. Instead call `IOpenApiTokenClient.RefreshAsync` mid-run to force new access token, persist it, assert live host keeps copying after forced rotation.

---

## 3. In-cluster (K8s) test suite run

Goal: run **live copy-trading suite** (and unit/integration) inside Kubernetes cluster against Helm-deployed app, reproducibly, so regression caught in-cluster same as locally.

### Implementation
- **Test image:** add `Dockerfile.tests` that builds solution and sets `ENTRYPOINT` to `dotnet test tests/IntegrationTests --filter CopyTradingLiveTests` (and variant for full suite). Include Playwright/Edge only if onboarding runs in-cluster; else mount pre-made token cache.
- **Secrets:** create K8s `Secret` from gitignored `secrets/openapi-*.local.json` (`kubectl create secret generic cmind-copy-secrets --from-file=secrets/`), mounted into test Job at path `LiveCopySecrets` searches (or point there via env). Never bake secrets into image.
- **Helm:** add `deploy/helm/cmind/templates/tests-job.yaml` (a `Job`, gated by `values.tests.enabled=false` by default) that runs test image, mounts secret, talks to in-cluster Postgres + Web services. Add `values.tests.*`.
- **Runner script:** `scripts/k8s-e2e.sh` — `kind create cluster` (or minikube) → `helm install cmind
  deploy/helm/cmind` → create secret → `helm upgrade --set tests.enabled=true` (or `kubectl apply` the Job) → wait completion → `kubectl logs job/cmind-tests` → assert exit 0 → tear down. Idempotent and CI-friendly.
- **Docs/CI:** document in `docs/deployment/` + add CI workflow (optional, gated) running script on kind cluster. Note node agents need `--privileged` (already documented) — **copy** tests don't need node agents, only Web + Postgres + token cache, so keep test Job minimal.

### Tests / verification for #3
- Job itself is the test. Success = live copy suite passes inside cluster and script exits 0. Add smoke assertion that Job logs contain expected `copied=True` lines.

---

## Definition of done (all three)
- New engine behavior lives on aggregate/host per DDD; no new public setters; new flags intention-method-guarded and migration-backed; audit-logged.
- Deterministic tests cover every new branch (fast, no network); live tests verify on **demo** accounts with full cleanup; `dotnet test` green; `get_file_problems` clean on every changed file.
- `docs/testing/live-copy-trading.md` updated (coverage matrix + new scenarios); "Known gaps" section shrinks as each item lands.