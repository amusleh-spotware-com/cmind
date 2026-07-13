---
description: "Full reproducible copy-trading test suite. Dwie warstwy:"
---

# Copy-trading test suite (deterministic + live)

Full reproducible copy-trading test suite. Dwie warstwy:

1. **Deterministic tests** (xUnit, brak network) — copy math + engine logic. Szybko, CI, brak sekretów. Cover każdy money-management mode, każdy filter/opcja, engine resilience.
2. **Live E2E tests** (real cTrader demo accounts) — production `CopyEngineHost` placing + copying real orders między real accounts. Pełnie automated, rerunnable jak unit test: read cached creds z local gitignored files, self-refresh access token, skip clean gdy sekrety absent (CI stays green).

Nigdy nie runs przeciwko live-funded account — każdy account **demo**, każdy live test closes positions to opened.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — każdy sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic przeciwko in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads gitignored sekrety, saves refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens z app DB do token cache
  LiveCopyFixture.cs             — rotates access token, exposes demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end do end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — live scenarios (1:1, 1:many, reverse, …)
```

## Sekrety (local, gitignored — nigdy nie committed)

Wszystkie creds pod `<repo>/secrets/` (już w `.gitignore`). Dev writes **pierwszy dwa files tylko**; trzeci (tokens) auto-produced przez onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds do authorize (jeden albo wiele):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **written przez onboarding**, multi-cID, refreshed każdy run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **nigdy nie expires**, więc po one-time onboarding live tests work indefinitely: każdy run
exchanges każdy cID'a refresh token dla fresh access token (rotation) — brak browser, brak prompts.

## One-time onboarding (fully automated — brak dev interaction beyond saving creds)

Onboarding drives real cTrader ID login w headless browser z saved cID creds, captures OAuth callback
na local HTTPS listener na app's registered redirect (`https://localhost:7080/openapi/callback`),
exchanges code dla tokens, loads account list, writes multi-cID token cache. Run raz per machine
(albo gdy adding cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Authorizes każdy cID w `openapi-cids.local.json`, writes `openapi-tokens.local.json`. Po tym live
copy tests potrzebują nic innego. (cID'a cTrader ID account musi mieć brak 2FA/captcha na login
dla automation do complete.)

**Alternative bootstrap** (jeśli accounts już authorized w running app): decrypt stored tokens
straight out z app's Postgres volume zamiast re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Safety — demo tylko

Live tests trade **tylko demo accounts**: fixture filters token cache do accounts z `IsLive == false`
i connects do demo gateway, więc order nigdy może land na live/funded account nawet jeśli live account
authorized. Każdy position test opens closed w cleanup.

## Running

```bash
# Deterministic copy tests tylko (szybko, brak sekretów, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests przeciwko real demo accounts (potrzeby dwa sekrety files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Wszystko
dotnet test
```

Bez sekrety files live tests print skip reason + pass jako no-ops, więc suite safe do run anywhere.

## Coverage

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** i **down** dla balance/leverage/capacity mismatch (golden rule) · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max ·
zero master balance skip.

### Decision filters (deterministic — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips effective side ·
slippage nad limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (deterministic — `CopyEngineHostTests`, in-memory session)
Open mirrors market order (side / volume / label) · **reverse** flips side i **swaps SL/TP** ·
**symbol mapping** resolves destination symbol · **order-failure na jeden slave ciągle copies do
others** · source close closes mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (deterministic — `OpenApiConnectionTests`)
Reaches Connected po app auth · dropped connection reconnects i re-auths · fatal auth error faults ·
exponential backoff.

### Live, real cTrader demo accounts (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors do każdy slave ·
**reverse** turns master buy do slave sell · **cross-cID** copy (master pod jeden cID mirrors do
slave pod inny, każdy authenticating z own token). Każdy opens real min-lot position na master,
waits dla engine do mirror to (matched przez source-position-id label na slave), asserts, closes
wszystko. Closed market reported **Inconclusive**, nie failing.

## Logging & auditability

Każdy copy trading operation logged poprzez source-generated structured events (`Core/Logging/LogMessages.cs`,
event IDs 1043–1055), pełny trail audytowalny:

| Event | Id | Meaning |
|-------|----|---------|
| CopyHostStarted | 1046 | profile'a engine came up (source + destination count) |
| CopySourceOpen | 1047 | master opened position (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order sent do slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | copy była skipped i dlaczego (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applied do slave copy |
| CopyOpenFailed | 1051 | slave copy-open failed (isolated — inni slaves continue) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master closed → slave copy closed |
| CopyCloseFailed | 1054 | slave copy-close failed |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphans closed) |
| CopyPartialClose | 1056 | master partial close mirrored — proportional slice closed na slave |
| CopyScaleIn | 1057 | master scale-in mirrored (opt-in) — added volume copied do slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop mirrored do slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop applied do slave copy (opt-in) |
| CopyStopLossAmended | 1061 | source SL move re-amended slave copy |
| CopyHostTokenRotated | 1062 | supervisor restarted running host po its access token rotated |

Logs emitted jako Serilog compact JSON (structured props: `ProfileId`, `DestinationCtid`, `SourcePositionId`,
`Symbol`, `Side`, `Volume`, …), shipped do OTLP gdy `OTEL_EXPORTER_OTLP_ENDPOINT` set. **Pełnie
configurable** per category poprzez standard config — np. raise/lower copy-engine verbosity bez
touching kod:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // copy engine audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test asserts trail fires dla open, order, protection, close.

## Edge cases (validated przeciwko jak real copy/MAM platforms fail)

Slippage & latency, symbol suffix/mismatch, duplicate trades na reconnect, leverage mismatch &
margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding, rejected orders,
direction filters, orphan cleanup po disconnect — wszystko covered powyżej. Źródła:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[dlaczego copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

Host mirrors więcej niż market open/close. Każdy behaviour = per-destination opt-in flag na `CopyDestination`
(`MirrorPartialClose` domyślnie on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` domyślnie off),
guarded przez intention methods, jsonb-persisted (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Behaviour | Deterministic test (`CopyEngineHostTests`) | Live test |
|-----------|--------------------------------------------|-----------|
| Partial close → proportional slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 closes 60%) + disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (ten sam live test — cancels na master, asserts slave cancels) ✅ |
| Filled pending brak double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Wszystkie live tests powyżej **zweryfikowane green przeciwko real cTrader demo accounts**
(1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire additions w `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`,
`ReconcilePendingOrdersAsync`, trailing flag na `AmendPositionSltpAsync`, order/pending fields na
`ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, używany przez live pending/trailing
testy do place resting orders away z market), `StopLoss`/`TrailingStopLoss` na `OpenPositionSnapshot`
(copy'a trailing state observable poprzez reconcile). Destination copies stay labeled przez **source
position id** (pending copies przez source **order id**) więc reconnect reconcile stays id-based,
nigdy nie duplicates trade.

**cTrader event gotcha (verified live):** resting pending order'a `ORDER_ACCEPTED`/`ORDER_CANCELLED`
execution event niesie **non-open `Position` placeholder** plus `Order`. Stream musi classify to jako
*order* event **zanim** position branch (gated na position nie `OPEN`), else pending placement mis-read
jako position close. `SourceExecutionsAsync` does to; missing to silent drops wszystkie pending mirroring.

## Token rotation + node affinity

- **Rotation do running hosts.** `CopyEngineSupervisor` records token signature na każdy running host
  i, każdy reconcile, rebuilds plan z DB (świeżo rotated przez `OpenApiTokenRefreshService`).
  Changed signature restarts host (`CopyHostTokenRotated`, 1062); new host'a `ResyncAsync` rebuilds
  state bez duplicating trades. Force rotation mid-run poprzez `IOpenApiTokenClient.RefreshAsync`
  aby verify live host keeps copying.
- **Node affinity (brak double-copy).** Zarówno Web local node i `CopyAgent` worker run supervisor.
  Każdy running profile claimed przez dokładnie jeden node (`CopyProfile.AssignedNode`, atomic
  `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, domyślnie machine name). Supervisor hosts
  tylko profiles to owns; stop/pause releases claim. Coverage:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` drives supervisor'a
    real `ClaimUnassignedProfilesAsync` — asserts pierwszy node claims wszystkie 3 running profiles,
    drugi claims **0** (brak double-host), pause→restart frees claim dla inny node.
  - Rotation detection (`TokenRotationSignatureTests`): supervisor'a `TokenSignature` zmienia gdy
    source albo destination token rotates, stable w innym wypadku (running host restarts tylko na
    real rotation).

### Single-use refresh tokens (important)

cTrader **refresh tokens są single-use** — każdy refresh zwraca *new* refresh token, invalidates old.
Live fixture refreshes na start, persists rotated token do `secrets/openapi-tokens.local.json`.
Consequences:
- Jeśli run refreshes ale **nie może persist** new token (np. read-only mount), cached token dead,
  next run fails `ACCESS_DENIED`. Regenerate z headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` swallows write failures więc read-only cache nie crash run, ale **live**
  in-cluster suite ciągle potrzebuje **writable** cache (K8s Job kopie Secret do emptyDir — zobacz
  deployment doc).

## Uruchamianie suite w Kubernetes cluster

Całej suite runs in-cluster przeciwko Helm-deployed app, więc regresja caught in-cluster jak lokalnie.
Zobacz [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (brak sekretów)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builds runner image; Helm `tests-job.yaml` (gated `tests.enabled=false`) runs to
przeciwko in-cluster Postgres + Web. **Domyślnie = deterministic copy suite** (brak sekretów, brak
rotating tokens). Dla live suite, ustaw `tests.copySecret` do Secret holding gitignored `openapi-*.local.json`;
init-container kopie to do **writable** emptyDir na `/app/secrets` (required — single-use refresh
tokens muszą być persistable). Copy tests potrzebują tylko Web + Postgres + token cache — brak
privileged node agents. Script asserts Job exits 0 i logs zawierają `Passed!`.

**Zweryfikowano tutaj (Docker, brak cluster):** test image runs deterministic suite (`101 passed`)
i, z writable `secrets/` mount, pełny **live** suite (`8 passed`) — exact Job path minus
Kubernetes. `kind`/`kubectl`/`helm` unavailable w authoring env, więc pełny `k8s-e2e.sh` cluster
run jest jeden step nie executed tutaj.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Dwa data-driven live suity build na `LiveCopyScenario` / `LiveCopyFixture`, live counterpart do
deterministic DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: jeden real master open per row
  przeciwko demo accounts, każdy z differently-configured destination, asserting golden outcome.
  Rows: `one_to_one`, `half_multiplier`, `reverse` (opposite side), `manage_only` (opens nic),
  `trading_hours_closed` (window excludes now → no copy), `source_label_block` (label filter →
  no copy), `lot_sanity_block` (ceiling → no copy).
- **`LiveCopyChaos`** — copy engine przeciwko hostile start: master już holds position zanim host
  starts, więc convergence może tylko prijść z start-up resync (`Sync-Open-on-Start`), plus
  negative case z toggle off. Deterministic socket-flap / token-rotation / rejection chaos
  covered exhaustively przez DST suite; to asserts live resync path.

Zarówno **skip cleanly** bez creds, report **Inconclusive** na closed market, każdy row cleans up
positions to opened (demo accounts tylko).

**Fixture robustness:** `LiveCopyFixture` teraz degrades do clean skip gdy cached tokens nie mogą
refresh (cTrader refresh tokens single-use — stale cache albo offline run nie longer faults całej
live collection; re-run OAuth onboarding do refresh cache).

## Uruchamianie live + cluster suiti sam (BEZ excuses — wszystko jest self-serviceable)

Wszystko poniżej available w tym środowisku. Nie claim "creds unavailable", "token stale", albo
"kind/helm nie installed" — ustaw je i run je.

### 1. Get fresh Open API token (headless OAuth, brak browser interaction)

cTrader refresh tokens single-use, więc cached token WILL go stale. Re-mint to sam z saved cID
username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, albo
unified `secrets/dev-credentials.local.json`). Onboarding test drives **headless Edge** OAuth via
Playwright, writes `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; authorizes każdy cID, caches fresh tokens. Re-run whenever live suite reports fixture unavailable
due do refresh failure.

### 2. Run live copy suites (real cTrader demo accounts)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Place + clean up real DEMO orders (nigdy live accounts), report **Inconclusive** na closed market.
Zweryfikowane green end do end.

### 3. Bootstrap tokens z running app volume (alternative)

Jeśli app run + cID linked in-app, extract app'a latest refresh token straight z `app-pg-data`
Postgres volume zamiast re-authorizing — zobacz `LiveTokenBootstrapTests`, ustaw `CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker available (install kind/helm poprzez `go install`/release binarki albo
`choco install kind kubernetes-helm` jeśli nie na PATH). One-shot script builds+loads images,
deploys chart, runs in-cluster test Job, asserts exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (brak sekretów)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Zobacz [../deployment/kubernetes.md](../deployment/kubernetes.md).
