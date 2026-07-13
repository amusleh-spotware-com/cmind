---
description: "Teljes reprodukálható copy-trading tesztcsomag. Két réteg:"
---

# Copy-trading tesztcsomag (determinisztikus + live)

Teljes reprodukálható copy-trading tesztcsomag. Két réteg:

1. **Determinisztikus tesztek** (xUnit, no network) — copy matematika + engine logika. Gyors, CI, nincs secrets. Minden money-management mód, minden filter/opció, engine reziliencia lefedve.
2. **Live E2E tesztek** (valódi cTrader demo számlák) — produktív `CopyEngineHost` valódi ordres + copy műveletek valódi számlák között. Teljesen automatizált, újrafuttatható mint unit teszt: gitignolt fájlokból cache-elt creds-et olvas, önfrissíti az access tokent, clean skip-el, ha nincs secrets (CI zölden marad).

Sosem fut live-funded számla ellen — minden számla **demo**, minden live teszt bezárja a megnyitott pozíciókat.

## Elrendezés

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — minden sizing mód + kerekítés + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logika in-memory fake session ellen
  FakeTradingSession.cs          — determinisztikus IOpenApiTradingSession (rögzíti orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (reziliencia)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — betölti a gitignored secrets-eket, menti a frissített tokeneket
  LiveTokenBootstrapTests.cs     — one-shot: visszafejti a tokeneket az app DB-ből a token cache-be
  LiveCopyFixture.cs             — rotálja az access tokent, kiadja a demo számla listát
  LiveCopyScenario.cs            — futtat egy valódi copy scenáriót end to end (open → copy → verify → cleanup)
  CopyTradingLiveTests.cs        — a live scenáriók (1:1, 1:many, reverse, …)
```

## Secrets (local, gitignored — sosem commit-olva)

Minden cred `<repo>/secrets/` alatt (már `.gitignore`-ban). A dev csak az **első két fájlt** írja; a harmadik (tokenek) auto-produced az onboarding által.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds az engedélyezéshez (egy vagy több):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **az onboarding írja**, multi-cID, minden futáskor frissül:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

A refresh token **sosem jár le**, így az one-time onboarding után a live copy tesztek határozatlan ideig működnek: minden futás exchange-eli minden cID refresh tokenjét friss access tokenre (rotáció) — nincs browser, nincs prompt.

## One-time onboarding (teljesen automatizált — nincs dev interakció a creds mentésen túli)

Az onboarding headless browser-ben viszi a valódi cTrader ID bejelentkezést a mentett cID creds-ből, capture-olja az OAuth callback-et egy local HTTPS listener-re az app regisztrált redirect-jén (`https://localhost:7080/openapi/callback`), exchange-eli a code-ot tokenekre, betölti a számlalistát, írja a multi-cID token cache-t. Egyszer futtatandó gépenként (vagy új cID hozzáadásakor):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Minden cID-t engedélyez a `openapi-cids.local.json`-ban, írja a `openapi-tokens.local.json`-t. Ezután a live copy tesztekhez semmi más nem kell. (A cID cTrader ID számlán nem lehet 2FA/captcha a bejelentkezéshez, különben az automation nem tudja befejezni.)

**Alternatív bootstrap** (ha a számlák már engedélyezve vannak a futó app-ban): visszafejti a tárolt tokeneket közvetlenül az app Postgres volume-jából újra-engedélyezés helyett:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Biztonság — csak demo

A live tesztek **csak demo számlákkal** kereskednek: a fixture szűri a token cache-t az `IsLive == false` számlákra és a demo gateway-hez csatlakozik, így az order sosem landolhat live/funded számlán, még ha live számla is engedélyezve van. A teszt által megnyitott minden pozíció a cleanup-ban bezárásra kerül.

## Futtatás

```bash
# Csak determinisztikus copy tesztek (gyors, nincs secrets, CI-biztos)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tesztek a valódi demo számlák ellen (kell a két secrets fájl)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Minden
dotnet test
```

Secrets fájlok nélkül a live tesztek skip okot printelnek + no-op-ként pass-olnak, így a csomag bárhol biztonságosan futtatható.

## Coverage

### Money management / sizing (determinisztikus — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **felfelé** és **lefelé** balance/leverage/capacity mismatch-re (a "golden rule") · lot-step
kerekítés · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filters (determinisztikus — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (determinisztikus — `CopyEngineHostTests`, in-memory session)
Open mirrors a market order (side / volume / label) · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves the destination symbol · **order-failure on one slave still copies to the
others** · source close closes the mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (determinisztikus — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects and re-auths · fatal auth error faults ·
exponential backoff.

### Live, valódi cTrader demo számlák (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors to slave under another, each authenticating with own token). Mindegyik valódi min-lot pozíciót nyit a master-en, vár amíg az engine tükrözi (source-position-id label alapján a slave-en), állít, bezár mindent. A closed market **Inconclusive**-ként jelentve, nem hibaként.

## Logging és auditálhatóság

Minden copy trading művelet source-generated struktúrált eseményekként logolva (`Core/Logging/LogMessages.cs`, event ID-k 1043–1055), teljes nyomvonal auditálható:

| Esemény | Id | Jelentés |
|---------|----|---------|
| CopyHostStarted | 1046 | egy profile engine-je felállt (forrás + célpontok száma) |
| CopySourceOpen | 1047 | master pozíciót nyitott (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order elküldve egy slave-nek (symbol / side / volume / source id) |
| CopySkipped | 1049 | egy másolás átugorva és miért (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP alkalmazva egy slave copy-ra |
| CopyOpenFailed | 1051 | egy slave copy-open failed (izolált — a többi slave folytatja) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master bezárta → slave copy bezárult |
| CopyCloseFailed | 1054 | egy slave copy-close failed |
| CopyResync | 1055 | reconnect reconcilation (source open count, orphans closed) |
| CopyPartialClose | 1056 | master részleges zárás tükrözve — arányos szelet zárva a slave-en |
| CopyScaleIn | 1057 | master scale-in tükrözve (opt-in) — hozzáadott volume másolva a slave-re |
| CopyPendingOrderPlaced | 1058 | pending limit/stop tükrözve a slave-re (opt-in) |
| CopyPendingOrderCancelled | 1059 | forrás pending cancelled → slave pending cancelled |
| CopyTrailingApplied | 1060 | trailing stop alkalmazva a slave copy-ra (opt-in) |
| CopyStopLossAmended | 1061 | egy forrás SL move re-amend-elte a slave copy-t |
| CopyHostTokenRotated | 1062 | supervisor újraindított egy futó hostot az access token rotációja után |

Logok Serilog compact JSON-ként emisszálva (strukturált props: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), OTLP-nek szállítva, ha `OTEL_EXPORTER_OTLP_ENDPOINT` be van állítva. **Teljesen konfigurálható** per category standard config-on keresztül — pl. fel/le tornázzák a copy-engine verbosity-t kód módosítása nélkül:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host teszt állítja, hogy a nyomvonal fire-öl open, order, protection, close-ra.

## Edge cases (validálva valódi copy/MAM platformok kudarc-módjaira)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch & margin-safe sizing, deposit-currency/contract-size különbségek, min/max lot & kerekítés, rejected orders, direction filters, orphan cleanup after disconnect — mind fedve fent. Források:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

A host többet tükröz mint market open/close. Minden viselkedés = per-destination opt-in flag a `CopyDestination`-ön (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` default off), guardolva intention methods által, jsonb-perzisztált (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Viselkedés | Deterministic test (`CopyEngineHostTests`) | Live test |
|-----------|--------------------------------------------|-----------|
| Partial close → proportional slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 closes 60%) + disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (same live test — cancels on master, asserts slave cancels) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Minden live teszt fent **verified green valódi cTrader demo számlákon** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire kiegészítések a `OpenApiTradingSession`-ben: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing flag az `AmendPositionSltpAsync`-en, order/pending mezők az `ExecutionEvent`-en, `LoadSpotPriceAsync` (spot subscribe → bid/ask, a live pending/trailing tesztek által használt, hogy a resting orders-t a piactól távolabb helyezze), `StopLoss`/`TrailingStopLoss` az `OpenPositionSnapshot`-on (a copy trailing state-je megfigyelhető a reconcile-on keresztül). A destination copy-k a **forrás pozíció id** alapján vannak címkézve (pending copy-k a forrás **order id** alapján), így a reconnect reconcile id-alapú, sosem duplikál trade-et.

**cTrader event gotcha (verified live):** a resting pending order `ORDER_ACCEPTED`/`ORDER_CANCELLED` execution event-je **nem-nyitott `Position` placeholder-t** hordoz plusz az `Order`-t. A stream-nek *order* event-ként kell klasszifikálnia **először** a position branch előtt (gated on position not `OPEN`), különben a pending placement-et pozíció zárásként olvas félre. `SourceExecutionsAsync` ezt csinálja; ennek hiánya csendben drop-ol minden pending mirroring-ot.

## Token rotáció + node affinity

- **Rotáció a futó hostokba.** `CopyEngineSupervisor` rögzíti a token signature-t minden futó hoston és minden reconcile-ciklusban újraépíti a tervet az DB-ből (frissen rotálva az `OpenApiTokenRefreshService` által). A changed signature restartolja a hostot (`CopyHostTokenRotated`, 1062); az új host `ResyncAsync`-a újraépíti az állapotot trade duplikálás nélkül. Force rotation mid-run `IOpenApiTokenClient.RefreshAsync`-en keresztül a live host copy-k maradásának verifikálásához.
- **Node affinity (no double-copy).** A Web local node és a `CopyAgent` worker is egy supervisor-t futtat. Minden futó profile pontosan egy node által igényelt (`CopyProfile.AssignedNode`, atomikus `ExecuteUpdate` claim `CopyOptions.NodeName`-ről kulcsolva, alapértelmezett gépnév). A supervisor csak az általa birtokolt profile-okat hostolja; stop/pause felszabadítja az igénylést. Coverage:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (valódi Postgres, Testcontainers)**: `CopyNodeAffinityTests` vezérli a supervisor valódi `ClaimUnassignedProfilesAsync`-jét — állítja, hogy az első node igényli mind a 3 futó profile-t, a második **0**-t (nincs double-host), pause→restart felszabadítja az igénylést másik node-nak.
  - Rotáció detektálás (`TokenRotationSignatureTests`): a supervisor `TokenSignature`-je változik, amikor a forrás vagy cél token rotál, egyébként stabil (futó host csak valódi rotációnál restartol).

### Single-use refresh tokens (fontos)

A cTrader **refresh tokenek single-use** — minden refresh *új* refresh tokent ad vissza, érvényteleníti a régit. A live fixture indításkor refresh-el, perzisztálja a rotált tokent a `secrets/openapi-tokens.local.json`-ba. Következmények:
- Ha a run refresh-el de **nem tud perzisztálni** (pl. read-only mount), a cached token halott, a következő run `ACCESS_DENIED`-t dob. Regenerálás headless onboarding-gal:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` lenyeli a write failure-okat, így a read-only cache nem crash-eli a run-t, de a **live** in-cluster suite-nak **írható** cache kell (K8s Job Secret-et másol emptyDir-be — lásd deployment doc).

## A suite futtatása Kubernetes cluster-ben

A teljes suite fut a cluster-ben a Helm-deployed app ellen, így a regresszió a cluster-ben is elkapásra kerül, ugyanúgy, mint lokálisan. Lásd [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, determinisztikus csomag (nincs secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` építi a runner image-et; Helm `tests-job.yaml` (gated `tests.enabled=false`) futtatja az in-cluster test Job-ot Postgres + Web ellen. **Alapértelmezett = determinisztikus copy csomag** (nincs secrets, nincs rotáló tokenek). A live csomaghoz állítsd be a `tests.copySecret`-et a Secret-re, amely a gitignolt `openapi-*.local.json`-okat tartalmazza; init-container bemásolja **írható** emptyDir-be `/app/secrets`-re (szükséges — single-use refresh tokeneknek perzisztálhatónak kell lenniük). A Copy tesztek csak Web + Postgres + token cache-t igénylik — nincs privileged node agents. A script assert-eli, hogy a Job exit 0 és a logok `Passed!`-et tartalmaznak.

**Verified itt (Docker, no cluster):** a test image futtatja a determinisztikus csomagot (`101 passed`) és írható `secrets/` mount-tal a teljes **live** csomagot (`8 passed`) — exact Job path mínusz Kubernetes. A `kind`/`kubectl`/`helm` nem elérhető a szerzői környezetben, így a teljes `k8s-e2e.sh` cluster run az az egy lépés, ami itt nincs végrehajtva.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Két data-driven live csomag épül a `LiveCopyScenario` / `LiveCopyFixture`-ra, live counterpart a determinisztikus DST stress suite-ra:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: egy valódi master open per sor különbözően konfigurált destination-nel, a golden outcome-t állítva. Sorok: `one_to_one`, `half_multiplier`, `reverse` (ellenkező oldal), `manage_only` (semmit nem nyit), `trading_hours_closed` (ablak kizárja most → nincs copy), `source_label_block` (label filter → nincs copy), `lot_sanity_block` (ceiling → nincs copy).
- **`LiveCopyChaos`** — copy engine hostile start ellen: a master már tartalmaz pozíciókat, mielőtt a host elindul, így a konvergencia csak a start-up resync-ből jöhet (`Sync-Open-on-Start`), plusz negatív eset toggle-offal. A determinisztikus socket-flap / token-rotation / rejection chaos exhaustive DST suite által fedett; ez a live resync utat állítja.

Mindkettő **clean skip-el** creds nélkül, **Inconclusive**-t jelent closed market-on, minden sor cleanup-olja a megnyitott pozíciókat (csak demo számlák).

**Fixture robusztusság:** a `LiveCopyFixture` most clean skip-re degradál, amikor a cached tokenek nem tudnak refresh-elni (cTrader refresh tokenek single-use — stale cache vagy offline run nem fault-olja a teljes live collection-t; re-run OAuth onboarding a cache refresh-eléséhez).

## A live + cluster suite-ok saját magad általi futtatása (NINCS kifogás — minden ön-szolgáltató)

Minden lent elérhető ebben a környezetben. Ne állítsd, hogy "creds nem elérhető", "token stale", vagy "kind/helm nincs telepítve" — állítsd be és futtasd.

### 1. Szerezz friss Open API tokent (headless OAuth, nincs böngésző interakció)

A cTrader refresh tokenek single-use, így a cached token IDŐVEL elavulttá válik. Frissítsd magad a mentett cID username/password-ból (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, vagy unified `secrets/dev-credentials.local.json`). Az Onboarding test vezet **headless Edge** OAuth-ot Playwright-on keresztül, írja a `secrets/openapi-tokens.local.json`-t:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; engedélyez minden cID-t, cache-el friss tokeneket. Futtasd újra, amikor a live suite fixture unavailable-t jelent refresh failure miatt.

### 2. Futasd a live copy csomagokat (valódi cTrader demo számlák)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Nyit + cleanup valódi DEMO order-eket (sosem live számlák), **Inconclusive**-t jelent closed market-on. Végig zölden verifikálva.

### 3. Bootstrap tokenek egy futó app volume-ból (alternatíva)

Ha az app fut + cID linkelve az app-ban, extracted az app legfrissebb refresh tokenjét közvetlenül az `app-pg-data` Postgres volume-ból újra-engedélyezés helyett — lásd `LiveTokenBootstrapTests`, állítsd be a `CMIND_VOLUME_CONN`-t.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker elérhető (telepítsd kind/helm-et `go install`/release binaries vagy `choco install kind kubernetes-helm`-mel, ha nincs a PATH-on). One-shot script épít+betölt imageket, deploy-olja a chart-ot, futtatja az in-cluster test Job-ot, assert-eli exit 0-t:

```bash
scripts/k8s-e2e.sh                                 # determinisztikus copy csomag (nincs secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Lásd [../deployment/kubernetes.md](../deployment/kubernetes.md).
