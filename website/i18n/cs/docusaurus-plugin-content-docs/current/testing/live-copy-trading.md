---
description: "Plná reprodukovatelná copy-trading test suite. Dvě vrstvy:"
---

# Copy-trading test suite (deterministic + live)

Plná reprodukovatelná copy-trading test suite. Dvě vrstvy:

1. **Deterministic testy** (xUnit, žádná síť) — copy math + engine logic. Rychlé, CI, žádná tajemství. Cover every money-management mode, every filter/option, engine resilience.
2. **Live E2E testy** (reálné cTrader demo účty) — production `CopyEngineHost` placing + copying real orders between real accounts. Plně automatizované, rerunnable jako unit test: read cached creds z local gitignored files, self-refresh access token, skip clean when secrets absent (CI stays green).

Nikdy neběží proti live-funded účtu — každý účet **demo**, každý live test closes positions it opened.

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
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens from app DB into the token cache
  LiveCopyFixture.cs             — rotates the access token, exposes the demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — the live scenarios (1:1, 1:many, reverse, …)
```

## Tajemství (lokální, gitignored — nikdy necommitovaná)

All creds under `<repo>/secrets/` (already in `.gitignore`). Dev writes **první dva soubory pouze**; třetí (tokens) auto-produced by onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds to authorize (one or many):

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

Refresh token **nikdy nevyprší**, takže after one-time onboarding live tests work indefinitely: each run exchanges each cID's refresh token for fresh access token (rotation) — no browser, no prompts.

## One-time onboarding (plně automatizovaný — žádná dev interakce kromě ukládání creds)

Onboarding drives real cTrader ID login in headless browser from saved cID creds, captures OAuth callback on local HTTPS listener at app's registered redirect (`https://localhost:7080/openapi/callback`), exchanges code for tokens, loads account list, writes multi-cID token cache. Run once per machine (or when adding cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Authorizes every cID in `openapi-cids.local.json`, writes `openapi-tokens.local.json`. Po tom live copy testy nepotřebují nic jiného. (cID's cTrader ID účet musí mít žádné 2FA/captcha na login pro dokončení automatizace.)

**Alternativní bootstrap** (pokud účty už autorizovány v běžící app): decrypt stored tokens straight out of app's Postgres volume instead of re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Bezpečnost — pouze demo

Live testy tradují **pouze demo účty**: fixture filtruje token cache na účty s `IsLive == false` a connectuje to demo gateway, takže order can never land on live/funded account even if live account authorized. Každá pozice kterou test otevře se v cleanup uzavře.

## Spuštění

```bash
# Deterministic copy tests only (fast, no secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests against real demo accounts (potřebuje dva secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Všechno
dotnet test
```

Bez secrets files live testy print skip reason + pass as no-ops, takže suite safe to run anywhere.

## Pokrytí

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** and **down** for balance/leverage/capacity mismatch (the "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filtry (deterministic — `CopyDecisionEngineTests`)
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

### Live, real cTrader demo účty (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors to slave under another, each authenticating with own token). Each opens real min-lot position on master, waits for engine to mirror it (matched by source-position-id label on slave), asserts, closes everything. Closed market reported **Inconclusive**, not failing.

## Logging & auditovatelnost

Every copy trading operation logged via source-generated structured events (`Core/Logging/LogMessages.cs`, event IDs 1043–1055), full trail auditable:

| Událost | Id | Význam |
|-------|----|--------|
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

Logy emitted as Serilog compact JSON (structured props: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), shipped to OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` set. **Fully configurable** per category via standard config — e.g. raise/lower copy-engine verbosity without touching code:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test asserts trail fires for open, order, protection, close.

## Edge cases (validované proti tomu jak reálné copy/MAM platformy selhávají)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch & margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding, rejected orders, direction filters, orphan cleanup after disconnect — all covered above. Zdroje:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring pokrytí (partial close · pending orders · SL-trailing)

Host mirrors more than market open/close. Each behaviour = per-destination opt-in flag on `CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` default off), guarded by intention methods, jsonb-persisted (migration `CopyAdvancedMirroringAndNodeAffinity`).

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

All live tests above **verified green against real cTrader demo účty** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire additions in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing flag on `AmendPositionSltpAsync`, order/pending fields on `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, used by live pending/trailing tests to place resting orders away from market), `StopLoss`/`TrailingStopLoss` on `OpenPositionSnapshot` (copy's trailing state observable via reconcile). Destination copies stay labelled by **source position id** (pending copies by source **order id**) so reconnect reconcile stays id-based, never duplicates trade.

**cTrader event gotcha (verified live):** resting pending order's `ORDER_ACCEPTED`/`ORDER_CANCELLED` execution event carries **non-open `Position` placeholder** plus the `Order`. Stream must classify it as *order* event **before** position branch (gated on position not `OPEN`), else pending placement mis-read as position close. `SourceExecutionsAsync` does this; missing it silently drops all pending mirroring.

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records token signature on each running host and, every reconcile, rebuilds plan from DB (freshly rotated by `OpenApiTokenRefreshService`). Changed signature restarts host (`CopyHostTokenRotated`, 1062); new host's `ResyncAsync` rebuilds state without duplicating trades. Force rotation mid-run via `IOpenApiTokenClient.RefreshAsync` to verify live host keeps copying.
- **Node affinity (no double-copy).** Both Web local node and `CopyAgent` worker run a supervisor. Each running profile claimed by exactly one node (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name). Supervisor hosts only profiles it owns; stop/pause releases claim. Pokrytí:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` drives supervisor's real `ClaimUnassignedProfilesAsync` — asserts first node claims all 3 running profiles, second claims **0** (no double-host), pause→restart frees claim for another node.
  - Rotation detection (`TokenRotationSignatureTests`): supervisor's `TokenSignature` changes when source or destination token rotates, stable otherwise (running host restarts only on real rotation).

### Single-use refresh tokeny (důležité)

cTrader **refresh tokeny jsou single-use** — each refresh returns *new* refresh token, invalidates old. Live fixture refreshes on start, persists rotated token to `secrets/openapi-tokens.local.json`. Důsledky:
- If run refreshes but **cannot persist** new token (e.g. read-only mount), cached token dead, next run fails `ACCESS_DENIED`. Regenerate with headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` swallows write failures so read-only cache doesn't crash run, but **live** in-cluster suite still needs **writable** cache (K8s Job copies Secret into emptyDir — see deployment doc).

## Spuštění suite v Kubernetes clusteru

Whole suite runs in-cluster against Helm-deployed app, takže regression caught in-cluster same as locally. Viz [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builds runner image; Helm `tests-job.yaml` (gated `tests.enabled=false`) runs it against in-cluster Postgres + Web. **Default = deterministic copy suite** (no secrets, no rotating tokens). Pro live suite, set `tests.copySecret` to Secret holding gitignored `openapi-*.local.json`; init-container copies it into **writable** emptyDir at `/app/secrets` (required — single-use refresh tokeny must be persistable). Copy testy potřebují pouze Web + Postgres + token cache — žádné privilegované node agenty. Script asserts Job exits 0 and logs contain `Passed!`.

**Verified here (Docker, no cluster):** test image runs deterministic suite (`101 passed`) and, with writable `secrets/` mount, full **live** suite (`8 passed`) — exact Job path minus Kubernetes. `kind`/`kubectl`/`helm` unavailable in authoring env, takže full `k8s-e2e.sh` cluster run is the one step not executed here.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Two data-driven live suites build on `LiveCopyScenario` / `LiveCopyFixture`, live counterpart to deterministic DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: one real master open per row against demo accounts, each with differently-configured destination, asserting golden outcome. Rows: `one_to_one`, `half_multiplier`, `reverse` (opposite side), `manage_only` (opens nothing), `trading_hours_closed` (window excludes now → no copy), `source_label_block` (label filter → no copy), `lot_sanity_block` (ceiling → no copy).
- **`LiveCopyChaos`** — copy engine against hostile start: master already holds position before host starts, so convergence can only come from start-up resync (`Sync-Open-on-Start`), plus negative case with toggle off. Deterministic socket-flap / token-rotation / rejection chaos covered exhaustively by DST suite; this asserts live resync path.

Both **skip cleanly** without creds, report **Inconclusive** on closed market, every row cleans up positions it opened (demo accounts only).

**Fixture robustness:** `LiveCopyFixture` now degrades to clean skip when cached tokens can't refresh (cTrader refresh tokeny single-use — stale cache or offline run no longer faults whole live collection; re-run OAuth onboarding to refresh cache).

## Spuštění live + cluster suites samostatně (ŽÁDNÉ výmluvy — všechno je self-serviceable)

Všechno níže dostupné v tomto prostředí. **Neclaimujte "creds unavailable", "token stale", nebo "kind/helm not installed"** — nastavte je a spusťte.

### 1. Získejte fresh Open API token (headless OAuth, žádná interakce prohlížeče)

cTrader refresh tokeny single-use, takže cached token WILL go stale. Re-mint it yourself from saved cID username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, or unified `secrets/dev-credentials.local.json`). Onboarding test drives **headless Edge** OAuth via Playwright, writes `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; authorizes every cID, caches fresh tokens. Re-run whenever live suite reports fixture unavailable due to refresh failure.

### 2. Spusťte live copy suites (reálné cTrader demo účty)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Place + clean up real DEMO orders (nikdy live accounts), report **Inconclusive** on closed market. Verified green end to end.

### 3. Bootstrap tokeny z běžícího app volume (alternativa)

If app run + cID linked in-app, extract app's latest refresh token straight from `app-pg-data` Postgres volume instead of re-authorizing — viz `LiveTokenBootstrapTests`, set `CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker available (install kind/helm via `go install`/release binaries or `choco install kind kubernetes-helm` if not on PATH). One-shot script builds+loads images, deploys chart, runs in-cluster test Job, asserts exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Viz [../deployment/kubernetes.md](../deployment/kubernetes.md).
