# Copy-trading test suite (deterministic + live)

This is the full, reproducible test suite for copy trading. It has two layers:

1. **Deterministic tests** (xUnit, no network) — the copy math and copy engine logic. Fast, run in CI,
   no secrets. Cover every money-management mode, every filter/option, and the engine's resilience
   behaviour.
2. **Live E2E tests** (real cTrader demo accounts) — the production `CopyEngineHost` placing and
   copying real orders between real accounts. Fully automated and rerunnable "like a unit test":
   they read cached credentials from local gitignored files, refresh the access token themselves, and
   skip cleanly when the secrets are absent (so CI stays green).

Nothing here ever runs against a live-funded account — every provided account is a **demo** account,
and every live test closes the positions it opened.

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

## Secrets (local, gitignored — never committed)

All credentials live under `<repo>/secrets/` (already in `.gitignore`). The **dev only writes the first
two files**; the third (tokens) is produced automatically by the onboarding.

`secrets/openapi-test-app.local.json` — the Open API application:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — the cID login credentials to authorize (one or many):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **written by the onboarding**, multi-cID, refreshed on every run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

The **refresh token does not expire**, so after the one-time onboarding the live tests keep working
indefinitely: each run exchanges each cID's refresh token for a fresh access token (rotation) — no
browser, no prompts.

## One-time onboarding (fully automated — no dev interaction beyond saving creds)

The onboarding drives the real cTrader ID login in a headless browser from the saved cID credentials,
captures the OAuth callback on a local HTTPS listener at the app's registered redirect
(`https://localhost:7080/openapi/callback`), exchanges the code for tokens, loads the account list, and
writes the multi-cID token cache. Run it once per machine (or whenever you add a cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

That authorizes every cID in `openapi-cids.local.json` and writes `openapi-tokens.local.json`. From then
on the live copy tests need nothing else. (The cID's cTrader ID account must not have 2FA/captcha on
login for the automation to complete.)

**Alternative bootstrap** (if the accounts are already authorized in a running app): decrypt the stored
tokens straight out of the app's Postgres volume instead of re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Safety — demo only

The live tests trade **only demo accounts**: the fixture filters the token cache to accounts with
`IsLive == false` and connects to the demo gateway, so an order can never be placed on a live/funded
account even if a live account is authorized. Every position a test opens is closed in cleanup.

## Running

```bash
# Deterministic copy tests only (fast, no secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests against the real demo accounts (needs the two secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Everything
dotnet test
```

Without the secrets files the live tests print a skip reason and pass as no-ops, so the suite is safe
to run anywhere.

## Coverage

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** and **down** for balance/leverage/capacity mismatch (the "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filters (deterministic — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (deterministic — `CopyEngineHostTests`, in-memory session)
Open mirrors a market order (side / volume / label) · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves the destination symbol · **order-failure on one slave still copies to the
others** · source close closes the mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (deterministic — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects and re-auths · fatal auth error faults ·
exponential backoff.

### Live, real cTrader demo accounts (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns a master buy into a slave sell · **cross-cID** copy (master under one cID mirrors to a
slave under another cID, each authenticating with its own token). Each opens a real min-lot position on
the master, waits for the engine to mirror it (matched by the source-position-id label on the slave),
asserts, and closes everything. A closed market is reported **Inconclusive** rather than failing.

## Logging & auditability

Every copy trading operation is logged through source-generated structured events
(`Core/Logging/LogMessages.cs`, event IDs 1043–1055) so the full trail is auditable:

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
| CopyPartialClose | 1056 | master partial close mirrored — proportional slice closed on a slave |
| CopyScaleIn | 1057 | master scale-in mirrored (opt-in) — added volume copied to a slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop mirrored to a slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop applied to a slave copy (opt-in) |
| CopyStopLossAmended | 1061 | a source SL move re-amended the slave copy |
| CopyHostTokenRotated | 1062 | supervisor restarted a running host after its access token rotated |

Logs are emitted as Serilog compact JSON (structured properties: `ProfileId`, `DestinationCtid`,
`SourcePositionId`, `Symbol`, `Side`, `Volume`, …) and shipped to OTLP when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set. **Fully configurable** per category via standard config — e.g. to
raise or lower copy-engine verbosity without touching code:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

The `Audit_log_records_every_trading_operation` host test asserts the trail fires for open, order,
protection, and close.

## Edge cases (validated against how real copy/MAM platforms fail)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch &
margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding, rejected orders,
direction filters, and orphan cleanup after a disconnect are all covered above. Sources:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

The host mirrors more than market open/close. Each behaviour is a per-destination opt-in flag on
`CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop`
default off), guarded by intention methods and jsonb-persisted (migration
`CopyAdvancedMirroringAndNodeAffinity`).

| Behaviour | Deterministic test (`CopyEngineHostTests`) | Live test |
|-----------|--------------------------------------------|-----------|
| Partial close → proportional slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 closes 60%) + disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (same live test — cancels on master, asserts slave cancels) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

All live tests above are **verified green against real cTrader demo accounts** (1:1, 1:many, reverse,
cross-cID, partial close, pending+cancel, trailing).

Wire additions in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`,
`ReconcilePendingOrdersAsync`, trailing flag on `AmendPositionSltpAsync`, order/pending fields on
`ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, used by the live pending/trailing
tests to place resting orders away from market), and `StopLoss`/`TrailingStopLoss` on
`OpenPositionSnapshot` (so a copy's trailing state is observable via reconcile). Destination copies stay
labelled by **source position id** (pending copies by source **order id**) so reconnect reconcile stays
id-based and never duplicates a trade.

**cTrader event gotcha (verified live):** a resting pending order's `ORDER_ACCEPTED`/`ORDER_CANCELLED`
execution event carries a **non-open `Position` placeholder** as well as the `Order`. The stream must
therefore classify it as an *order* event **before** the position branch (gated on the position not
being `OPEN`), else a pending placement is mis-read as a position close. `SourceExecutionsAsync` does
this; missing it silently drops all pending mirroring.

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records a token signature on each running host
  and, every reconcile, rebuilds the plan from the DB (freshly rotated by `OpenApiTokenRefreshService`).
  A changed signature restarts the host (`CopyHostTokenRotated`, 1062); the new host's `ResyncAsync`
  rebuilds state without duplicating trades. Force a rotation mid-run via
  `IOpenApiTokenClient.RefreshAsync` to verify the live host keeps copying.
- **Node affinity (no double-copy).** Both the Web local node and the `CopyAgent` worker run a supervisor.
  Each running profile is claimed by exactly one node (`CopyProfile.AssignedNode`, atomic
  `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name). A supervisor hosts only
  profiles it owns; stop/pause releases the claim. Coverage:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` drives the supervisor's
    real `ClaimUnassignedProfilesAsync` — asserts the first node claims all 3 running profiles and the
    second claims **0** (no double-host), and that pause→restart frees the claim for another node.
  - Rotation detection (`TokenRotationSignatureTests`): the supervisor's `TokenSignature` changes when
    the source or a destination token rotates, and is stable otherwise (so a running host restarts only
    on a real rotation).

### Single-use refresh tokens (important)

cTrader **refresh tokens are single-use** — each refresh returns a *new* refresh token and invalidates
the old one. The live fixture refreshes on start and persists the rotated token to
`secrets/openapi-tokens.local.json`. Consequences:
- If a run refreshes but **cannot persist** the new token (e.g. a read-only mount), the cached token is
  dead and the next run fails `ACCESS_DENIED`. Regenerate with the headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` swallows write failures so a read-only cache doesn't crash a run, but the
  **live** in-cluster suite still needs a **writable** cache (the K8s Job copies the Secret into an
  emptyDir — see the deployment doc).

## Running the suite in a Kubernetes cluster

The whole suite runs in-cluster against the Helm-deployed app, so a regression is caught in-cluster the
same as locally. See [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builds the runner image; the Helm `tests-job.yaml` (gated `tests.enabled=false`) runs
it against the in-cluster Postgres + Web. **Default = the deterministic copy suite** (no secrets, no
rotating tokens). For the live suite, set `tests.copySecret` to a Secret holding the gitignored
`openapi-*.local.json`; an init-container copies it into a **writable** emptyDir at `/app/secrets`
(required — single-use refresh tokens must be persistable). The copy tests need only Web + Postgres +
the token cache — no privileged node agents. The script asserts the Job exits 0 and its logs contain
`Passed!`.

**Verified here (Docker, no cluster):** the test image runs the deterministic suite (`101 passed`) and,
with a writable `secrets/` mount, the full **live** suite (`8 passed`) — i.e. the exact Job path minus
Kubernetes. `kind`/`kubectl`/`helm` were not available in the authoring environment, so the full
`k8s-e2e.sh` cluster run is the one step not executed here.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Two data-driven live suites build on `LiveCopyScenario` / `LiveCopyFixture`, the live counterpart to the
deterministic DST stress suite:

- **`LiveCopyMatrix`** — a `[Theory]`/`[MemberData]` option matrix: one real master open per row against
  demo accounts, each with a differently-configured destination, asserting the golden outcome. Rows:
  `one_to_one`, `half_multiplier`, `reverse` (copies the opposite side), `manage_only` (opens nothing),
  `trading_hours_closed` (window excludes now → no copy), `source_label_block` (label filter → no copy),
  `lot_sanity_block` (ceiling → no copy).
- **`LiveCopyChaos`** — the copy engine against a hostile start: the master already holds a position before
  the host starts, so convergence can only come from the start-up resync (`Sync-Open-on-Start`), plus the
  negative case with the toggle off. Deterministic socket-flap / token-rotation / rejection chaos is covered
  exhaustively by the DST suite; this asserts the live resync path.

Both **skip cleanly** without credentials and report **Inconclusive** on a closed market, and every row
cleans up the positions it opened (demo accounts only).

**Fixture robustness:** `LiveCopyFixture` now degrades to a clean skip when the cached tokens can't be
refreshed (cTrader refresh tokens are single-use — a stale cache or an offline run no longer faults the
whole live collection; re-run the OAuth onboarding to refresh the cache).

## Running the live + cluster suites yourself (NO excuses — everything is self-serviceable)

Everything below is available in this environment. Do **not** claim "creds unavailable", "token stale", or
"kind/helm not installed" — set them up and run them.

### 1. Get a fresh Open API token (headless OAuth, no browser interaction)

cTrader refresh tokens are single-use, so a cached token WILL go stale. Re-mint it yourself from the saved
cID username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, or the
unified `secrets/dev-credentials.local.json`). The onboarding test drives a **headless Edge** OAuth via
Playwright and writes `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; authorizes every cID and caches fresh tokens. Re-run whenever the live suite reports the fixture is
unavailable due to a refresh failure.

### 2. Run the live copy suites (real cTrader demo accounts)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

They place + clean up real DEMO orders (never live accounts), and report **Inconclusive** on a closed
market. Verified green end to end.

### 3. Bootstrap tokens from a running app volume (alternative)

If the app has been run and a cID linked in-app, extract the app's latest refresh token straight from the
`app-pg-data` Postgres volume instead of re-authorizing — see `LiveTokenBootstrapTests` and set
`CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, and Docker are available (install kind/helm via `go install`/release binaries or
`choco install kind kubernetes-helm` if not already on PATH). The one-shot script builds+loads images,
deploys the chart, runs the in-cluster test Job, and asserts exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

See [../deployment/kubernetes.md](../deployment/kubernetes.md).
