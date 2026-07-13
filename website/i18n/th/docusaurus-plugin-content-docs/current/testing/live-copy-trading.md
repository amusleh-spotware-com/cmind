---
description: "Full reproducible copy-trading test suite สอง layers:"
---

# Copy-trading test suite (deterministic + live)

Full reproducible copy-trading test suite สอง layers:

1. **Deterministic tests** (xUnit, ไม่มี network) — copy math + engine logic Fast, CI, ไม่มี
   secrets Cover ทุก money-management mode, ทุก filter/option, engine resilience
2. **Live E2E tests** (real cTrader demo accounts) — production `CopyEngineHost` placing +
   copying real orders ระหว่าง real accounts Fully automated, rerunnable เหมือน unit test:
   read cached creds จาก local gitignored files, self-refresh access token, skip clean เมื่อ
   secrets ไม่มี (CI stays green)

ไม่เคย run ต่อ live-funded account — ทุก account **demo**, ทุก live test closes positions
ที่มันเปิด

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — ทุก sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic ต่อ in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads the gitignored secrets, saves refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens from app DB เป็น token cache
  LiveCopyFixture.cs             — rotates access token, exposes demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — live scenarios (1:1, 1:many, reverse, …)
```

## Secrets (local, gitignored — ไม่เคย committed)

ทั้งหมด under `<repo>/secrets/` (อยู่ใน `.gitignore` แล้ว) Dev เขียน **เฉพาะสองไฟล์แรก**;
สาม (tokens) ถูก produce โดย onboarding

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds เพื่อ authorize (หนึ่งหรือหลาย):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **เขียนโดย onboarding**, multi-cID, ถูก refresh ทุก run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **ไม่มีวันหมดอายุ** ดังนั้นหลัง one-time onboarding live tests ทำงานได้อย่างไม่มีกำหนด:
แต่ละ run exchange ทุก cID's refresh token สำหรับ fresh access token (rotation) — ไม่มี
browser, ไม่มี prompts

## One-time onboarding (fully automated — ไม่มี dev interaction นอกจาก saving creds)

Onboarding drives real cTrader ID login ใน headless browser จาก saved cID creds, captures
OAuth callback บน local HTTPS listener ที่ app's registered redirect
(`https://localhost:7080/openapi/callback`), exchanges code สำหรับ tokens, loads account list,
writes multi-cID token cache Run ครั้งเดียวต่อ machine (หรือเมื่อเพิ่ม cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Authorize ทุก cID ใน `openapi-cids.local.json`, เขียน `openapi-tokens.local.json` หลังจาก
นั้น live copy tests ไม่ต้องการอะไรอีก (cID's cTrader ID account ต้องไม่มี 2FA/captcha บน
login เพื่อให้ automation สมบูรณ์)

**Alternative bootstrap** (ถ้า accounts authorized ใน app ที่รันอยู่): decrypt stored tokens
ตรงๆ จาก app's Postgres volume แทน re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Safety — demo เท่านั้น

Live tests เทรด **เฉพาะ demo accounts**: fixture filter token cache ไปยัง accounts ที่มี
`IsLive == false` และ connects to demo gateway, ดังนั้น order ไม่มีทางลงบน live/funded
account แม้ว่า live account authorized ทุก position ที่ test เปิด closed ใน cleanup

## Running

```bash
# Deterministic copy tests เท่านั้น (fast, ไม่มี secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests ต่อ real demo accounts (ต้องการ two secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# ทุกอย่าง
dotnet test
```

โดยไม่มี secrets files live tests print skip reason + pass เป็น no-ops, ดังนั้น suite safe
ที่จะ run ที่ไหน

## Coverage

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** และ **down** สำหรับ balance/leverage/capacity mismatch (the "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip

### Decision filters (deterministic — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned)

### Copy engine host (deterministic — `CopyEngineHostTests`, in-memory session)
Open mirrors market order (side / volume / label) · **reverse** flips side และ **swaps SL/TP** ·
**symbol mapping** resolves destination symbol · **order-failure on one slave still copies to others**
· source close closes mirrored copy · reconnect resync closes orphaned copies

### Connection resilience (deterministic — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects และ re-auths · fatal auth error faults ·
exponential backoff

### Live, real cTrader demo accounts (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy เป็น slave sell · **cross-cID** copy (master under one cID mirrors to
slave under another, แต่ละ authenticate ด้วย own token) แต่ละ opens real min-lot position on
master, waits for engine to mirror it (matched โดย source-position-id label บน slave), asserts,
closes everything Closed market reported **Inconclusive**, not failing

## Logging & auditability

ทุก copy trading operation logged ผ่าน source-generated structured events
(`Core/Logging/LogMessages.cs`, event IDs 1043–1055), full trail auditable:

| Event | Id | ความหมาย |
|-------|----|---------|
| CopyHostStarted | 1046 | profile's engine came up (source + destination count) |
| CopySourceOpen | 1047 | master opened position (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order sent to slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | copy ถูก skip และเหตุผล (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applied to slave copy |
| CopyOpenFailed | 1051 | slave copy-open failed (isolated — other slaves continue) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master closed → slave copy closed |
| CopyCloseFailed | 1054 | slave copy-close failed |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphans closed) |
| CopyPartialClose | 1056 | master partial close mirrored — proportional slice closed on slave |
| CopyScaleIn | 1057 | master scale-in mirrored (opt-in) — added volume copied to slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop mirrored to slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop applied to slave copy (opt-in) |
| CopyStopLossAmended | 1061 | source SL move re-amended slave copy |
| CopyHostTokenRotated | 1062 | supervisor restarted running host after access token rotated |

Logs emit เป็น Serilog compact JSON (structured props: `ProfileId`, `DestinationCtid`,
`SourcePositionId`, `Symbol`, `Side`, `Volume`, …), shipped to OTLP เมื่อ
`OTEL_EXPORTER_OTLP_ENDPOINT` ถูกตั้ง **Fully configurable** ต่อ category ผ่าน standard config
— เช่น raise/lower copy-engine verbosity โดยไม่แตะโค้ด:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test assert trail fires for open, order,
protection, close

## Edge cases (validated ต่อวิธีที่ real copy/MAM platforms fail)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch
& margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding,
rejected orders, direction filters, orphan cleanup after disconnect — ทั้งหมด covered ข้างบน
Sources:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/)

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

Host mirrors มากกว่า market open/close แต่ละ behaviour = per-destination opt-in flag บน
`CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/
`CopyTrailingStop` default off), guarded โดย intention methods, jsonb-persisted (migration
`CopyAdvancedMirroringAndNodeAffinity`)

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

ทั้งหมด live tests ข้างบน **verified green ต่อ real cTrader demo accounts** (1:1, 1:many,
reverse, cross-cID, partial close, pending+cancel, trailing)

Wire additions ใน `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`,
`ReconcilePendingOrdersAsync`, trailing flag บน `AmendPositionSltpAsync`, order/pending fields
บน `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, used by live
pending/trailing tests เพื่อ place resting orders away from market), `StopLoss`/`TrailingStopLoss`
บน `OpenPositionSnapshot` (copy's trailing state observable ผ่าน reconcile) Destination
copies stay labelled โดย **source position id** (pending copies โดย source **order id**) ดังนั้น
reconnect reconcile stays id-based, ไม่เคย duplicate trade

**cTrader event gotcha (verified live):** resting pending order's
`ORDER_ACCEPTED`/`ORDER_CANCELLED` execution event carries **non-open `Position` placeholder**
บวก `Order` Stream ต้อง classify มันเป็น *order* event **ก่อน** position branch (gated บน
position ไม่ใช่ `OPEN`) ไม่งั้น pending placement mis-read เป็น position close
`SourceExecutionsAsync` ทำสิ่งนี้; missing it silently drops all pending mirroring

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records token signature บนแต่ละ running
  host และทุก reconcile, rebuilds plan จาก DB (freshly rotated โดย
  `OpenApiTokenRefreshService`) Changed signature restarts host (`CopyHostTokenRotated`, 1062);
  new host's `ResyncAsync` rebuilds state โดยไม่ duplicate trades Force rotation mid-run ผ่าน
  `IOpenApiTokenClient.RefreshAsync` เพื่อ verify live host keeps copying
- **Node affinity (ไม่มี double-copy).** ทั้ง Web local node และ `CopyAgent` worker run
  supervisor แต่ละ running profile claimed โดย exactly หนึ่ง node
  (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`,
  default machine name) Supervisor hosts เฉพาะ profiles ที่มันเป็นเจ้าของ; stop/pause
  releases claim Coverage:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`
  - **Integration (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` drives supervisor's
    real `ClaimUnassignedProfilesAsync` — asserts first node claims all 3 running profiles,
    second claims **0** (ไม่มี double-host), pause→restart frees claim สำหรับ node อื่น
  - Rotation detection (`TokenRotationSignatureTests`): supervisor's `TokenSignature` changes
    เมื่อ source หรือ destination token rotates, stable ไม่เปลี่ยน (running host restarts
    เฉพาะบน real rotation)

### Single-use refresh tokens (สำคัญ)

cTrader **refresh tokens เป็น single-use** — แต่ละ refresh returns *new* refresh token,
invalidates old Live fixture refreshes on start, persists rotated token ไปยัง
`secrets/openapi-tokens.local.json` ผลที่ตามมา:
- ถ้า run refreshes แต่ **ไม่สามารถ persist** new token (เช่น read-only mount), cached token
  dead, run ถัดไป fails `ACCESS_DENIED` Regenerate ด้วย headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
- `LiveCopySecrets.SaveTokens` swallows write failures ดังนั้น read-only cache ไม่ crash run,
  แต่ **live** in-cluster suite ยังต้องการ **writable** cache (K8s Job copies Secret
  into emptyDir — ดู deployment doc)

## Running the suite in a Kubernetes cluster

ทั้ง suite รัน in-cluster ต่อ Helm-deployed app, ดังนั้น regression caught in-cluster
เหมือน local ดู [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite)

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (ไม่มี secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builds runner image; Helm `tests-job.yaml` (gated `tests.enabled=false`)
runs it ต่อ in-cluster Postgres + Web **Default = deterministic copy suite** (ไม่มี secrets,
ไม่มี rotating tokens) สำหรับ live suite, ตั้ง `tests.copySecret` เป็น Secret holding
gitignored `openapi-*.local.json`; init-container copies it into **writable** emptyDir ที่
`/app/secrets` (ต้องการ — single-use refresh tokens ต้อง persistable) Copy tests ต้องการ
เฉพาะ Web + Postgres + token cache — ไม่มี privileged node agents Script asserts Job
exits 0 และ logs contain `Passed!`

**Verified ที่นี่ (Docker, ไม่มี cluster):** test image runs deterministic suite
(`101 passed`) และด้วย writable `secrets/` mount, full **live** suite (`8 passed`) —
exact Job path minus Kubernetes `kind`/`kubectl`/`helm` unavailable ใน authoring env,
ดังนั้น full `k8s-e2e.sh` cluster run คือขั้นตอนเดียวที่ไม่ได้ execute ที่นี่

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

สอง data-driven live suites build on `LiveCopyScenario` / `LiveCopyFixture`, live counterpart
ไปยัง deterministic DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: หนึ่ง real master open ต่อ row
  ต่อ demo accounts, แต่ละอันมี differently-configured destination, asserting golden outcome
  Rows: `one_to_one`, `half_multiplier`, `reverse` (opposite side), `manage_only` (opens nothing),
  `trading_hours_closed` (window excludes now → no copy), `source_label_block` (label filter →
  no copy), `lot_sanity_block` (ceiling → no copy)
- **`LiveCopyChaos`** — copy engine ต่อ hostile start: master already holds position ก่อน host
  starts, ดังนั้น convergence สามารถมาจาก start-up resync (`Sync-Open-on-Start`) บวก
  negative case ด้วย toggle off Deterministic socket-flap / token-rotation / rejection chaos
  covered exhaustively โดย DST suite; นี่ asserts live resync path

ทั้งสอง **skip cleanly** โดยไม่มี creds, report **Inconclusive** บน closed market,
ทุก row cleans up positions ที่มันเปิด (demo accounts เท่านั้น)

**Fixture robustness:** `LiveCopyFixture` ตอนนี้ degrades ไปยัง clean skip เมื่อ cached
tokens can't refresh (cTrader refresh tokens single-use — stale cache หรือ offline run ไม่
faults whole live collection; re-run OAuth onboarding เพื่อ refresh cache)

## Running the live + cluster suites yourself (NO excuses — ทุกอย่าง self-serviceable)

ทุกอย่างข้างล่าง available ใน environment นี้ ไม่ claim ว่า "creds unavailable", "token
stale" หรือ "kind/helm not installed" — set พวกมันขึ้นและ run พวกมัน

### 1. Get a fresh Open API token (headless OAuth, ไม่มี browser interaction)

cTrader refresh tokens เป็น single-use, ดังนั้น cached token จะ stale ทำให้
Re-mint จาก saved cID username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`,
หรือ unified `secrets/dev-credentials.local.json`) Onboarding test drives **headless Edge**
OAuth via Playwright, เขียน `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; authorizes ทุก cID, caches fresh tokens Re-run เมื่อ live suite reports fixture
unavailable เนื่องจาก refresh failure

### 2. Run the live copy suites (real cTrader demo accounts)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Place + clean up real DEMO orders (ไม่เคย live accounts), report **Inconclusive** บน
closed market Verified green end to end

### 3. Bootstrap tokens from a running app volume (alternative)

ถ้า app run + cID linked in-app, extract app's latest refresh token ตรงๆ จาก
`app-pg-data` Postgres volume แทน re-authorizing — ดู `LiveTokenBootstrapTests`, ตั้ง
`CMIND_VOLUME_CONN`

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker available (install kind/helm ผ่าน `go install`/release binaries หรือ
`choco install kind kubernetes-helm` ถ้าไม่อยู่บน PATH) One-shot script builds+loads images,
deploys chart, runs in-cluster test Job, asserts exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (ไม่มี secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

ดู [../deployment/kubernetes.md](../deployment/kubernetes.md)
