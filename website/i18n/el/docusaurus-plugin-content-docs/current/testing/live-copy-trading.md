---
description: "Πλήρες αναπαραγώγιμο copy-trading test suite. Δύο στρώσεις: deterministic tests (xUnit, χωρίς δίκτυο) και live E2E tests (πραγματικοί cTrader demo λογαριασμοί)."
---

# Copy-trading test suite (deterministic + live)

Πλήρες αναπαραγώγιμο copy-trading test suite. Δύο στρώσεις:

1. **Deterministic tests** (xUnit, χωρίς δίκτυο) — copy math + engine logic. Γρήγορο, CI,
   χωρίς secrets. Καλύπτει κάθε money-management mode, κάθε filter/option, engine resilience.
2. **Live E2E tests** (real cTrader demo accounts) — production `CopyEngineHost` placing +
   copying real orders μεταξύ real accounts. Πλήρως αυτοματοποιημένο, επανεκτελέσιμο όπως
   unit test: διαβάζει cached creds από local gitignored files, self-refreshes access token,
   skip cleanly όταν secrets απουσιάζουν (CI stays green).

Δεν τρέχει ποτέ ενάντια σε live-funded account — κάθε account **demo**, κάθε live test
κλείνει τις θέσεις που άνοιξε.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — κάθε sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic ενάντια σε ένα in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — φορτώνει τα gitignored secrets, σώζει refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens από την app DB στο token cache
  LiveCopyFixture.cs             — περιστρέφει το access token, εκθέτει τη demo account list
  LiveCopyScenario.cs            — εκτελεί ένα real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — τα live scenarios (1:1, 1:many, reverse, …)
```

## Secrets (local, gitignored — ποτέ committed)

Όλα τα creds under `<repo>/secrets/` (ήδη στο `.gitignore`). Ο dev γράφει **μόνο τα
πρώτα δύο αρχεία**· το τρίτο (tokens) auto-produced by onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds για authorization (ένα ή πολλά):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **γράφεται από onboarding**, multi-cID, refreshed κάθε run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Το refresh token **δεν λήγει ποτέ**, οπότε μετά από one-time onboarding τα live tests
δουλεύουν indefinitely: κάθε run ανταλλάσσει κάθε cID's refresh token για fresh access
token (rotation) — χωρίς browser, χωρίς prompts.

## One-time onboarding (fully automated — καμία dev αλληλεπίδραση beyond saving creds)

Το Onboarding οδηγεί real cTrader ID login σε headless browser από saved cID creds,
capture το OAuth callback σε local HTTPS listener στο registered redirect της app
(`https://localhost:7080/openapi/callback`), exchange code για tokens, φορτώνει account
list, γράφει multi-cID token cache. Εκτελέστε μία φορά ανά machine (ή όταν προσθέτετε
cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Authorizes κάθε cID στο `openapi-cids.local.json`, γράφει `openapi-tokens.local.json`.
Μετά τα live copy tests δεν χρειάζονται τίποτα άλλο. (Το cID's cTrader ID account
πρέπει να μην έχει 2FA/captcha στο login για να ολοκληρωθεί το automation.)

**Εναλλακτικό bootstrap** (αν accounts ήδη authorized στην running app): decrypt stored
tokens απευθείας από το app's Postgres volume αντί να επαναεξουσιοδοτηθεί:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Ασφάλεια — demo μόνο

Τα live tests διαπραγματεύονται **μόνο demo accounts**: το fixture φιλτράρει το token
cache σε accounts με `IsLive == false` και συνδέεται στο demo gateway, οπότε η εντολή
δεν μπορεί ποτέ να προσγειωθεί σε live/funded account ακόμα και αν live account
authorized. Κάθε θέση που ένα test ανοίγει κλείνει στο cleanup.

## Running

```bash
# Deterministic copy tests only (γρήγορο, χωρίς secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests ενάντια στους real demo accounts (χρειάζεται τα δύο secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Everything
dotnet test
```

Χωρίς secrets files τα live tests εκτυπώνουν skip reason + pass ως no-ops, οπότε η
suite είναι ασφαλής να τρέξει οπουδήποτε.

## Coverage

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** και **down** για balance/leverage/capacity mismatch (το "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filters (deterministic — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero
skip · reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (deterministic — `CopyEngineHostTests`, in-memory session)
Open mirrors ένα market order (side / volume / label) · **reverse** flips side και **swaps
SL/TP** · **symbol mapping** resolves το destination symbol · **order-failure on one slave still
copies to the others** · source close closes το mirrored copy · reconnect resync closes orphaned
copies.

### Connection resilience (deterministic — `OpenApiConnectionTests`)
Reaches Connected μετά από app auth · dropped connection reconnects και re-auths · fatal auth
error faults · exponential backoff.

### Live, real cTrader demo accounts (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors σε κάθε slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors
to slave under another, each authenticating with own token). Κάθε ένα opens real min-lot
position on master, περιμένει για engine να το mirror (matched by source-position-id label on
slave), asserts, closes everything. Closed market reported **Inconclusive**, not failing.

## Logging & auditability

Κάθε copy trading operation logged μέσω source-generated structured events
(`Core/Logging/LogMessages.cs`, event IDs 1043–1055), full trail auditable:

| Event | Id | Σημασία |
|-------|----|---------|
| CopyHostStarted | 1046 | ένα profile's engine came up (source + destination count) |
| CopySourceOpen | 1047 | master opened ένα position (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order sent to ένα slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | ένα copy παραλείφθηκε και γιατί (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applied to ένα slave copy |
| CopyOpenFailed | 1051 | ένα slave copy-open failed (isolated — τα άλλα slaves συνεχίζουν) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master closed → slave copy closed |
| CopyCloseFailed | 1054 | ένα slave copy-close failed |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphans closed) |
| CopyPartialClose | 1056 | master partial close mirrored — proportional slice closed on ένα slave |
| CopyScaleIn | 1057 | master scale-in mirrored (opt-in) — added volume copied to ένα slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop mirrored to ένα slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop applied to ένα slave copy (opt-in) |
| CopyStopLossAmended | 1061 | source SL move re-amended το slave copy |
| CopyHostTokenRotated | 1062 | supervisor restarted ένα running host μετά από το access token rotation |

Logs emitted ως Serilog compact JSON (structured props: `ProfileId`, `DestinationCtid`,
`SourcePositionId`, `Symbol`, `Side`, `Volume`, …), shipped to OTLP όταν
`OTEL_EXPORTER_OTLP_ENDPOINT` set. **Πλήρως διαμορφώσιμο** per category μέσω standard config
— π.χ. raise/lower copy-engine verbosity χωρίς να αγγίξετε κώδικα:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // το CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test asserts trail fires για open, order,
protection, close.

## Edge cases (validated έναντι του πώς real copy/MAM platforms αποτυγχάνουν)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch
& margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding,
rejected orders, direction filters, orphan cleanup after disconnect — όλα καλύπτονται
παραπάνω. Sources:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

Ο Host mirrors περισσότερα από market open/close. Κάθε behaviour = per-destination opt-in
flag on `CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/
`CopyPendingOrders`/`CopyTrailingStop` default off), guarded by intention methods,
jsonb-persisted (migration `CopyAdvancedMirroringAndNodeAffinity`).

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

Όλα τα live tests παραπάνω **verified green ενάντια σε real cTrader demo accounts**
(1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire additions in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`,
`ReconcilePendingOrdersAsync`, trailing flag on `AmendPositionSltpAsync`, order/pending
fields on `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, used by live
pending/trailing tests για να place resting orders away from market), `StopLoss`/
`TrailingStopLoss` on `OpenPositionSnapshot` (copy's trailing state observable via
reconcile). Destination copies stay labelled by **source position id** (pending copies by
source **order id**) οπότε reconnect reconcile stays id-based, never duplicates trade.

**cTrader event gotcha (verified live):** resting pending order's `ORDER_ACCEPTED`/
`ORDER_CANCELLED` execution event carries **non-open `Position` placeholder** plus το
`Order`. Stream must classify it as *order* event **before** position branch (gated on
position not `OPEN`), else pending placement mis-reads ως position close.
`SourceExecutionsAsync` does this; missing it silently drops all pending mirroring.

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records token signature on each
  running host και, every reconcile, rebuilds plan from DB (freshly rotated by
  `OpenApiTokenRefreshService`). Changed signature restarts host (`CopyHostTokenRotated`,
  1062)· new host's `ResyncAsync` rebuilds state χωρίς duplicating trades. Force rotation
  mid-run via `IOpenApiTokenClient.RefreshAsync` για να verify live host keeps copying.
- **Node affinity (no double-copy).** Both Web local node και `CopyAgent` worker run ένα
  supervisor. Each running profile claimed by exactly one node (`CopyProfile.AssignedNode`,
  atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name).
  Supervisor hosts only profiles it owns· stop/pause releases claim. Coverage:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` drives
    supervisor's real `ClaimUnassignedProfilesAsync` — asserts first node claims all 3
    running profiles, second claims **0** (no double-host), pause→restart frees claim for
    another node.
  - Rotation detection (`TokenRotationSignatureTests`): supervisor's `TokenSignature`
    changes όταν source or destination token rotates, stable otherwise (running host
    restarts only on real rotation).

### Single-use refresh tokens (important)

Ο cTrader **refresh tokens είναι single-use** — κάθε refresh επιστρέφει *νέο* refresh
token, invalidates old. Live fixture refreshes on start, persists rotated token to
`secrets/openapi-tokens.local.json`. Συνέπειες:
- Αν το refresh επιτύχει αλλά **δεν μπορεί να επιμένει** το new token (π.χ. read-only
  mount), cached token dead, next run fails `ACCESS_DENIED`. Αναγεννήστε με headless
  onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` swallows write failures ώστε read-only cache να μην crash
  run, αλλά **live** in-cluster suite εξακολουθεί να χρειάζεται **writable** cache (K8s
  Job copies Secret into emptyDir — δείτε deployment doc).

## Running the suite in a Kubernetes cluster

Ολόκληρη η suite τρέχει in-cluster ενάντια σε Helm-deployed app, ώστε regression να
πιάνεται in-cluster ίδιο ως locally. Δείτε
[`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (χωρίς secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builds runner image· Helm `tests-job.yaml` (gated `tests.enabled=false`)
runs it ενάντια σε in-cluster Postgres + Web. **Default = deterministic copy suite**
(χωρίς secrets, χωρίς rotating tokens). Για live suite, θέστε `tests.copySecret` σε Secret
που κρατά gitignored `openapi-*.local.json`· init-container copies it into **writable**
emptyDir at `/app/secrets` (required — single-use refresh tokens must be persistable). Τα
Copy tests χρειάζονται μόνο Web + Postgres + token cache — χωρίς privileged node agents.
Script asserts Job exits 0 και logs contain `Passed!`.

**Verified εδώ (Docker, χωρίς cluster):** test image runs deterministic suite
(`101 passed`) και, με writable `secrets/` mount, full **live** suite (`8 passed`) — exact
Job path minus Kubernetes. `kind`/`kubectl`/`helm` unavailable in authoring env, οπότε το
πλήρες `k8s-e2e.sh` cluster run είναι το ένα βήμα που δεν εκτελέστηκε εδώ.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Δύο data-driven live suites build on `LiveCopyScenario` / `LiveCopyFixture`, live
counterpart to deterministic DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: one real master open per
  row against demo accounts, each με differently-configured destination, asserting golden
  outcome. Rows: `one_to_one`, `half_multiplier`, `reverse` (opposite side),
  `manage_only` (opens nothing), `trading_hours_closed` (window excludes now → no copy),
  `source_label_block` (label filter → no copy), `lot_sanity_block` (ceiling → no copy).
- **`LiveCopyChaos`** — copy engine against hostile start: master already holds position
  before host starts, οπότε convergence can only come from start-up resync
  (`Sync-Open-on-Start`), plus negative case with toggle off. Deterministic
  socket-flap / token-rotation / rejection chaos covered exhaustively by DST suite· this
  asserts live resync path.

Και τα δύο **skip cleanly** χωρίς creds, report **Inconclusive** on closed market, κάθε
row cleans up positions it opened (demo accounts only).

**Fixture robustness:** `LiveCopyFixture` now degrades to clean skip όταν cached tokens
can't refresh (cTrader refresh tokens single-use — stale cache or offline run no longer
faults whole live collection· re-run OAuth onboarding to refresh cache).

## Running the live + cluster suites yourself (ΚΑΝΕΝΑ δικαιολογητικό — όλα self-serviceable)

Όλα τα παρακάτω είναι διαθέσιμα σε αυτό το περιβάλλον. **Μην ισχυριστείτε** "creds unavailable",
"token stale", ή "kind/helm not installed" — setup and run them.

### 1. Λάβετε ένα fresh Open API token (headless OAuth, χωρίς browser interaction)

Τα cTrader refresh tokens είναι single-use, οπότε το cached token ΘΑ γίνει stale.
Re-mint it yourself από saved cID username/password (`secrets/openapi-cids.local.json` +
`secrets/openapi-test-app.local.json`, ή unified `secrets/dev-credentials.local.json`).
Onboarding test drives **headless Edge** OAuth μέσω Playwright, γράφει
`secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s· authorizes κάθε cID, caches fresh tokens. Re-run όταν live suite reports fixture
unavailable due to refresh failure.

### 2. Εκτελέστε τις live copy suites (real cTrader demo accounts)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Places + cleans up real DEMO orders (ποτέ live accounts), reports **Inconclusive** on
closed market. Verified green end to end.

### 3. Bootstrap tokens from a running app volume (εναλλακτικό)

Αν η app τρέχει + cID linked in-app, extract app's latest refresh token απευθείας από
το `app-pg-data` Postgres volume αντί να επαναεξουσιοδοτηθεί — δείτε
`LiveTokenBootstrapTests`, θέστε `CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker διαθέσιμα (εγκατάσταση kind/helm through `go install`/release
binaries ή `choco install kind kubernetes-helm` αν όχι στο PATH). One-shot script
builds+loads images, deploys chart, runs in-cluster test Job, asserts exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (χωρίς secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Δείτε [../deployment/kubernetes.md](../deployment/kubernetes.md).
