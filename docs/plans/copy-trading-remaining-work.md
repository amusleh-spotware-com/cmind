# Copy trading — remaining work (implementation plan)

## Already shipped (context for the next session)

All on `main`. Copy trading is live-verified end to end against real cTrader **demo** accounts:
- **OAuth fixed + UX**: callback state carried in an HttpOnly cookie (cTrader never echoes `state`);
  single Open API app per user; add/edit via MudBlazor dialog; success page auto-redirects
  (authorized→/accounts, invite→/); warn on app change when accounts are authorized.
- **Automated OAuth onboarding** (`tests/E2ETests/CopyLive/OpenApiOnboarding.cs` + `OnboardingTests`,
  `CMIND_ONBOARD=1`): headless cTrader ID login from saved cID creds → captures the code on a local
  HTTPS listener → writes a **multi-cID** token cache. Refresh tokens don't expire → live tests
  self-refresh, zero interaction. Both cIDs onboarded (amusleh demo, afhacker has some LIVE accounts).
- **Live copy verified** (`CopyTradingLiveTests`): 1:1, 1:many, reverse, and **cross-cID** all place a
  real master order and assert the engine mirrors it onto the slave(s), then clean up. **Demo-only
  safety**: `LiveCopyFixture` filters to `IsLive==false` + demo gateway, so no order can hit a funded
  account.
- **Deterministic coverage** (175+ unit tests): full sizing matrix (all money-management modes,
  leverage/balance/currency scaling, min/max/step), decision filters (direction/reverse/slippage/delay/
  symbol filter/size-zero), and `CopyEngineHostTests` via `FakeTradingSession` (open/side, reverse+SL/TP
  swap, symbol map, **per-slave order-failure isolation**, close mirror, reconnect resync, audit log).
- **Engine hardening**: `CopyEngineHost` now takes `IOpenApiTradingSessionFactory` (testable); per-slave
  try/catch so one slave's failure never blocks the others.
- **Audit logging**: every copy op is a structured event (`LogMessages` 1046–1055), Serilog JSON,
  per-category configurable.
- **Secrets** (gitignored, local): `openapi-test-app.local.json` (client id/secret),
  `openapi-cids.local.json` (cID logins), `openapi-tokens.local.json` (multi-cID token cache, produced
  by onboarding). Alternative bootstrap decrypts tokens from the app's Postgres volume
  (`LiveTokenBootstrapTests`, `CMIND_VOLUME_CONN`).

**What's left = the three items below.** Not started; each is real engine/infra work.

---

Plan for three remaining items. Each is standalone; implement in order of value: **1 → 2 → 3**.
Follow the repo rules: invoke the `ddd-dotnet` skill before touching `src/`, run Rider
`get_file_problems` on every changed `.cs`/`.razor`, keep `dotnet build` at 0 warnings/0 errors, and
**verify live against demo accounts** (never live/funded — the fixture already filters to demo).

Read first: `docs/testing/live-copy-trading.md` (how the deterministic + live suites work, secrets,
onboarding). Key production files:
- `src/CopyEngine/CopyEngineHost.cs` — the copy loop (open → mirror, close → close, resync).
- `src/CopyEngine/CopyDecisionEngine.cs`, `src/Core/CopyTrading/CopySizingCalculator.cs` — pure sizing/decision.
- `src/CTraderOpenApi/Client/OpenApiTradingSession.cs` — the wire API (`IOpenApiTradingSession` +
  `IOpenApiTradingSessionFactory`); `ExecutionEvent`, `OpenPositionSnapshot`, `SymbolDetails`.
- `src/Core/CopyTrading/CopyEntities.cs` (`CopyDestination`), `CopyValueObjects.cs` (enums/VOs).
- `src/Nodes/CopyTrading/CopyEngineSupervisor.cs`, `OpenApiTokenRefreshService.cs`; `src/CopyAgent/`.
Tests: `tests/UnitTests/CopyTrading/*` (`FakeTradingSession`, `CopyEngineHostTests`),
`tests/IntegrationTests/CopyLive/*` (`LiveCopyFixture`, `LiveCopyScenario`, `CopyTradingLiveTests`).

---

## 1. Partial-close mirroring · non-market order types · SL-trailing

Today `CopyEngineHost` only mirrors **market opens** and **full closes**:
- `HandleOpenAsync` dedups by `_openSourcePositions.Add(positionId)`, so a second event on the same
  source position (partial close, volume change, trailing SL move) is dropped.
- `SourceExecutionsAsync` (in `OpenApiTradingSession`) **skips events with `Position == null`**, so
  pending orders (limit/stop) never reach the host.
- Copies are always `SendMarketOrderAsync`; closes always close the **full** labelled copy.

### 1a. Partial-close mirroring
- **Host state:** add `Dictionary<long,long> _sourceVolumes` (sourcePositionId → last wire volume).
  On an Open event for an already-tracked id whose volume **decreased**, treat it as a partial close:
  compute `closedFraction = (old - new) / old`; for each destination, reconcile the labelled copy and
  `ClosePositionAsync` a proportional slice (`round(copyVolume * closedFraction)` to the dest step, min
  volume floor — reuse the normalization ideas in `CopySizingCalculator.Normalize`). Update
  `_sourceVolumes`. A volume **increase** = scale-in; either mirror as an additional market order sized
  by the delta, or skip — gate on a new `CopyDestination` flag (see below). Default: mirror partial
  **closes**, ignore scale-ins (document the choice).
- `ClosePositionAsync(ctid, positionId, volume, ct)` already accepts a partial volume — no wire change.
- **Domain:** add `CopyDestination.MirrorPartialClose` (bool, default true) + `MirrorScaleIn` (default
  false) via intention methods (`SetPartialCloseMirroring(bool)`, …), `private set`, jsonb-persisted
  like the existing copy flags; **add an EF migration** (mirror `AddCopySymbolFilters`). Surface both in
  `AddCopyDestinationRequest` (`CopyEndpoints`) + the destination UI grid in `CopyTrading.razor`.

### 1b. Non-market order types (pending limit/stop)
- **Wire:** add `SendPendingOrderAsync(ctid, symbolId, isBuy, volume, ProtoOAOrderType, double price,
  string label, ct)` to `IOpenApiTradingSession`/`OpenApiTradingSession` — same as `SendMarketOrderAsync`
  but `OrderType = Limit|Stop` with `LimitPrice`/`StopPrice` set (verify the exact `ProtoOANewOrderReq`
  field names in the generated protobuf under `src/CTraderOpenApi/Messages`).
- **Source events:** `SourceExecutionsAsync` must surface order events, not just positions. Extend
  `ExecutionEvent` (or add an `OrderEvent`) carrying order type + price + status
  (`ProtoOAExecutionEvent.Order`, `OrderType`, `LimitPrice`/`StopPrice`, `OrderStatus`). Handle
  `ORDER_ACCEPTED` (pending placed) → place matching pending on dest labelled with the source **order**
  id; `ORDER_CANCELLED` → cancel the dest pending (needs a `CancelOrderAsync` → `ProtoOACancelOrderReq`);
  `ORDER_FILLED` → the existing position-open path takes over (dedupe so a filled pending isn't double
  placed — key by source order id → source position id).
- **Domain:** `CopyDestination.CopyPendingOrders` (bool, default false) — when false, keep today's
  behavior (copy only on fill). Migration + request + UI as in 1a.

### 1c. SL-trailing
- **Wire:** cTrader supports a trailing stop on the position/order. Verify fields on
  `ProtoOAAmendPositionSLTPReq` / `ProtoOANewOrderReq` (likely `TrailingStopLoss` bool +
  `StopTriggerMethod`). Extend `AmendPositionSltpAsync` (or add `SetTrailingStopAsync`) to set it.
- **Host:** in `ApplyProtectionAsync`, when the source position has a trailing stop and the destination
  opts in, set trailing on the copy instead of a fixed SL. Also mirror **subsequent SL moves**: a source
  Open event on a tracked id with a changed `StopLoss` (and unchanged volume) = SL amend → re-amend the
  dest copy. This needs the same "second event on same id" handling as 1a (so wire 1a first).
- **Domain:** `CopyDestination.CopyTrailingStop` (bool, default false). Migration + request + UI.

### Tests for #1 (this is the bar — cover every path)
- **Deterministic** (`CopyEngineHostTests` + extend `FakeTradingSession` to record pending orders,
  partial closes with volume, trailing amends, cancels):
  - partial close mirrors a **proportional** slice on each slave (e.g. source 1.0→0.4 closes 60% on the
    copy); volume rounds to the dest step; below-min behaves per policy.
  - scale-in ignored by default; mirrored when `MirrorScaleIn` on.
  - pending **limit** and **stop** orders placed on the slave with correct type/price/label when
    `CopyPendingOrders` on; not placed when off.
  - source order **cancel** cancels the dest pending.
  - filled pending does **not** double-open (order-id → position-id dedupe).
  - trailing stop applied to the copy when `CopyTrailingStop` on; a source SL move re-amends the copy.
  - add sizing/VO tests for the new flags.
- **Live** (`LiveCopyScenario` + `CopyTradingLiveTests`, demo only, clean up every position/order):
  - open on master then **partial close** → assert the slave copy shrinks proportionally.
  - place a **limit** order on master away from price → assert a matching pending appears on the slave;
    cancel on master → assert it's cancelled on the slave.
  - open with a **trailing stop** → assert the slave copy has a trailing stop.
  - Extend the audit log (`Core/Logging/LogMessages.cs`, next ids 1056+) with `CopyPartialClose`,
    `CopyPendingOrderPlaced`, `CopyPendingOrderCancelled`, `CopyTrailingApplied`; assert they fire.

---

## 2. Token rotation propagation to running hosts (incl. external CopyAgent nodes)

`OpenApiTokenRefreshService` (`src/Nodes/CopyTrading`) refreshes each `OpenApiAuthorization` near expiry
and persists the new token via `authorization.Refresh(...)`. `CopyEngineSupervisor.BuildPlanAsync`
**reads the token from the DB each reconcile cycle**, so a *newly hosted* profile uses the fresh token.
**Gap:** a profile **already running** holds the old token inside its `OpenApiConnection` (set once at
`AttachAccount`); on reconnect it re-auths with that stale token. If the old access token is revoked on
rotation, the running host breaks.

### Implementation
- **Detect rotation for running hosts.** In `CopyEngineSupervisor.ReconcileAsync`, alongside the running
  set, compare the token(s) in each running host's plan against the current DB token; if changed,
  **cancel + restart** that host with a fresh plan (cleanest, reuses `BuildPlanAsync`). Store the token
  (or a hash) on the `HostHandle` to compare. Restart must be seamless: the new host's `ResyncAsync`
  rebuilds state from reconcile (open-missing / close-orphaned) without duplicating trades — this is
  already how reconnect works, so a restart is safe.
- **Alternative (lower churn):** add a way to push a new token into a live `OpenApiConnection`
  (`AttachAccount` already replaces the token in `_accounts`; add a `RefreshAccountToken` that also
  triggers a re-auth or a controlled reconnect). Restart is simpler and safer — prefer it unless churn
  matters.
- **External nodes / no double-copy.** The `CopyAgent` worker runs its own `CopyEngineSupervisor`, and
  so does the Web local node — **both currently host every Running profile → double execution.** Before
  (or with) this work, add **node affinity**: assign each running profile to exactly one node
  (`CopyProfile.AssignedNodeId` or a claim row), and have each supervisor only host profiles assigned to
  its node id (env/`AppOptions` node identity). Without this, rotation tests are ambiguous and prod
  double-copies. This is a prerequisite — call it out.

### Tests for #2
- **Deterministic/integration:** a fake `IOpenApiTokenClient` returns a rotated token; drive a supervisor
  reconcile (or the host directly) and assert the running host is restarted / re-auths with the **new**
  token and continues copying (extend `FakeTradingSession` to record the token seen at `AttachAccount`).
- **Node affinity:** integration test that two supervisors with different node ids do **not** both host
  the same profile (only the assigned one does).
- **Live (optional, hard):** access tokens last ~30 days, so real expiry isn't testable in CI. Instead
  call `IOpenApiTokenClient.RefreshAsync` mid-run to force a new access token, persist it, and assert the
  live host keeps copying after the forced rotation.

---

## 3. In-cluster (K8s) test suite run

Goal: run the **live copy-trading suite** (and unit/integration) inside a Kubernetes cluster against the
Helm-deployed app, reproducibly, so a regression is caught in-cluster the same as locally.

### Implementation
- **Test image:** add `Dockerfile.tests` that builds the solution and sets `ENTRYPOINT` to
  `dotnet test tests/IntegrationTests --filter CopyTradingLiveTests` (and a variant for the full suite).
  Include Playwright/Edge only if onboarding runs in-cluster; otherwise mount a pre-made token cache.
- **Secrets:** create a K8s `Secret` from the gitignored `secrets/openapi-*.local.json`
  (`kubectl create secret generic cmind-copy-secrets --from-file=secrets/`), mounted into the test Job at
  the path `LiveCopySecrets` searches (or point it there via env). Never bake secrets into the image.
- **Helm:** add `deploy/helm/cmind/templates/tests-job.yaml` (a `Job`, gated by
  `values.tests.enabled=false` by default) that runs the test image, mounts the secret, and talks to the
  in-cluster Postgres + Web services. Add `values.tests.*`.
- **Runner script:** `scripts/k8s-e2e.sh` — `kind create cluster` (or minikube) → `helm install cmind
  deploy/helm/cmind` → create the secret → `helm upgrade --set tests.enabled=true` (or `kubectl apply`
  the Job) → wait for completion → `kubectl logs job/cmind-tests` → assert exit 0 → tear down. Make it
  idempotent and CI-friendly.
- **Docs/CI:** document in `docs/deployment/` + add a CI workflow (optional, gated) that runs the script
  on a kind cluster. Note that the node agents need `--privileged` (already documented) — the **copy**
  tests don't need node agents, only Web + Postgres + the token cache, so keep the test Job minimal.

### Tests / verification for #3
- The Job itself is the test. Success = the live copy suite passes inside the cluster and the script
  exits 0. Add a smoke assertion that the Job's logs contain the expected `copied=True` lines.

---

## Definition of done (all three)
- New engine behavior lives on the aggregate/host per DDD; no new public setters; new flags are
  intention-method-guarded and migration-backed; audit-logged.
- Deterministic tests cover every new branch (fast, no network); live tests verify on **demo** accounts
  with full cleanup; `dotnet test` green; `get_file_problems` clean on every changed file.
- `docs/testing/live-copy-trading.md` updated (coverage matrix + new scenarios); the "Known gaps"
  section shrinks as each item lands.
