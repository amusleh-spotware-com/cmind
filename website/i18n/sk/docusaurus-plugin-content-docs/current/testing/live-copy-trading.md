---
description: "Plná reprodukovateľná copy-trading testovacia sada. Dve vrstvy:"
---

# Copy-trading testovacia sada (deterministic + live)

Plná reprodukovateľná copy-trading testovacia sada. Dve vrstvy:

1. **Deterministic testy** (xUnit, bez siete) — copy matika + engine logika. Rýchle, CI, žiadne secrets. Pokrývajú každý money-management mode, každý filter/option, engine resilience.
2. **Live E2E testy** (reálne cTrader demo účty) — produkčný `CopyEngineHost` umiestňuje + kopíruje reálne objednávky medzi reálnymi účtami. Plne automatizované, znovu-spužiteľné ako jednotkový test: číta cached creds z lokálnych gitignored súborov, self-refresh access token, skip čistí keď secrets chýbajú (CI zostáva zelená).

Nikdy nebeží proti live-funded účtu — každý účet **demo**, každý live test uzatvára pozície, ktoré otvoril.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — každý sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logika proti in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (zaznamenáva orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — načítava gitignored secrets, ukladá refreshnuté tokeny
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokeny z app DB do token cache
  LiveCopyFixture.cs             — rotuje access token, vystavuje demo účet list
  LiveCopyScenario.cs            — spúšťa jeden reálny copy scenár end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — live scenáre (1:1, 1:many, reverse, …)
```

## Secrets (lokálne, gitignored — nikdy necommitnuté)

Všetky creds pod `<repo>/secrets/` (už v `.gitignore`). Dev píše **prvé dva súbory iba**; tretí (tokeny) auto-produkovaný onboardingom.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login creds na autorizáciu (jeden alebo viac):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **písaný onboardingom**, multi-cID, refreshovaný každým behom:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **nikdy neexpiruje**, takže po one-time onboarding live testy fungujú donekonečna: každý beh vymení každý cID's refresh token za čerstvý access token (rotácia) — žiadny prehliadač, žiadne výzvy.

## One-time onboarding (plne automatizovaný — žiadna dev interakcia okrem uloženia creds)

Onboarding hná reálne cTrader ID login v headless prehliadači zo saved cID creds, zachytí OAuth callback na lokálnom HTTPS listeneri na registered redirect aplikácie (`https://localhost:7080/openapi/callback`), vymení code za tokeny, načíta zoznam účtov, zapíše multi-cID token cache. Beží raz per machine (alebo pri pridaní cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autorizuje každý cID v `openapi-cids.local.json`, zapíše `openapi-tokens.local.json`. Potom live copy testy nepotrebujú nič iné. (cID's cTrader ID účet musí mať žiadne 2FA/captcha na login pre automation na dokončenie.)

**Alternatívny bootstrap** (ak sú účty už autorizované v bežiacej app): decrypt stored tokeny priamo z app's Postgres volume namiesto re-autorizácie:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Bezpečnosť — iba demo

Live testy obchodujú **iba demo účty**: fixture filtruje token cache na účty s `IsLive == false` a
pripája sa k demo gateway, takže objednávka nikdy nepristane na live/funded účte ani keď live účet
bol autorizovaný. Každá pozícia, ktorú test otvorí, je uzatvorená v cleanup.

## Bežanie

```bash
# Deterministic copy testy iba (rýchle, žiadne secrets, CI-bezpečné)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy testy proti reálnym demo účtom (potrebuje dva secrets súbory)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Všetko
dotnet test
```

Bez secrets súborov live testy vytlačia dôvod preskočenia + prejdú ako no-ops, takže suite je
bezpečné spustiť kdekoľvek.

## Pokrytie

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** a **down** pre balance/leverage/capacity mismatch (the "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Rozhodovacie filtre (deterministic — `CopyDecisionEngineTests`)
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

### Live, reálne cTrader demo účty (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors to slave under another, each authenticating with own token). Each opens real min-lot position on master, waits for engine to mirror it (matched by source-position-id label on slave), asserts, closes everything. Closed market reported **Inconclusive**, not failing.

## Logging & auditovateľnosť

Každá copy-trading operácia logged cez source-generated structured events (`Core/Logging/LogMessages.cs`, event IDs 1043–1055), full trail auditable:

| Udalosť | Id | Význam |
|-------|----|---------|
| CopyHostStarted | 1046 | profile's engine came up (source + destination count) |
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

Logs emitted as Serilog compact JSON (structured props: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), shipped to OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` set. **Plne konfigurovateľné** per category via standard config — napr. raise/lower copy-engine verbosity without touching code:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test assertuje trail fires for open, order, protection, close.

## Edge cases (validované proti tomu, ako reálne copy/MAM platformy zlyhávajú)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch & margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding, rejected orders, direction filters, orphan cleanup after disconnect — všetko pokryté vyššie. Zdroje:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Advanced mirroring coverage (partial close · pending orders · SL-trailing)

Host zrkadlí viac ako market open/close. Každé správanie = per-destination opt-in flag na `CopyDestination` (`MirrorPartialClose` predvolené on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` predvolené off), guarded by intention methods, jsonb-persisted (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Správanie | Deterministic test (`CopyEngineHostTests`) | Live test |
|-----------|--------------------------------------------|-----------|
| Partial close → proportional slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 closes 60%) + disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (same live test — cancels on master, asserts slave cancels) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Všetky live testy vyššie **verified green proti reálnym cTrader demo účtom** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire additions in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing flag on `AmendPositionSltpAsync`, order/pending fields on `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, used by live pending/trailing tests to place resting orders away from market), `StopLoss`/`TrailingStopLoss` on `OpenPositionSnapshot` (copy's trailing state observable via reconcile). Destination copies stay labelled by **source position id** (pending copies by source **order id**) takže reconnect reconcile stays id-based, never duplicates trade.

**cTrader event gotcha (verified live):** resting pending order's `ORDER_ACCEPTED`/`ORDER_CANCELLED` execution event carries **non-open `Position` placeholder** plus the `Order`. Stream must classify it as *order* event **before** position branch (gated on position not `OPEN`), else pending placement mis-read as position close. `SourceExecutionsAsync` does this; missing it silently drops all pending mirroring.

## Token rotation + node affinity

- **Rotation into running hosts.** `CopyEngineSupervisor` records token signature on each running host and, every reconcile, rebuilds plan from DB (freshly rotated by `OpenApiTokenRefreshService`). Changed signature restarts host (`CopyHostTokenRotated`, 1062); new host's `ResyncAsync` rebuilds state without duplicating trades. Force rotation mid-run via `IOpenApiTokenClient.RefreshAsync` to verify live host keeps copying.
- **Node affinity (no double-copy).** Both Web local node and `CopyAgent` worker run a supervisor. Each running profile claimed by exactly one node (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name). Supervisor hosts only profiles it owns; stop/pause releases claim. Coverage:
  - Domain (jednotka): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integrácia (reálny Postgres, Testcontainers)**: `CopyNodeAffinityTests` hná supervisor's reálny `ClaimUnassignedProfilesAsync` — assertuje prvý node claimuje všetky 3 bežiaci profily, druhý claimuje **0** (žiadne double-host), pause→restart frees claim pre iný node.
  - Rotation detection (`TokenRotationSignatureTests`): supervisor's `TokenSignature` sa zmení keď source alebo destination token rotuje, stable otherwise (bežiaci host restartuje iba na reálnu rotáciu).

### Single-use refresh tokeny (dôležité)

cTrader **refresh tokeny sú single-use** — každý refresh vracia *nový* refresh token, invaliduje starý. Live fixture refreshuje na štarte, perzistuje rotated token do `secrets/openapi-tokens.local.json`. Dôsledky:
- Ak refresh beží ale **nemôže perzistovať** nový token (napr. read-only mount), cached token mŕtvy, ďalší beh zlyhá `ACCESS_DENIED`. Regenerujte s headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` prehľadá write failures takže read-only cache nerozbije run, ale **live**
  in-cluster suite stále potrebuje **writable** cache (K8s Job kopíruje Secret do emptyDir — pozrite deployment doc).

## Bežanie sady v Kubernetes clustri

Celá sada beží in-cluster proti Helm-deployed app, takže regress catches in-cluster rovnako ako lokálne. Pozrite [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` builduje runner image; Helm `tests-job.yaml` (gated `tests.enabled=false`) ho hná proti in-cluster Postgres + Web. **Predvolené = deterministic copy suite** (žiadne secrets, žiadne rotujúce tokeny). Pre live suite, nastavte `tests.copySecret` na Secret držiaci gitignored `openapi-*.local.json`; init-container ho kopíruje do **writable** emptyDir na `/app/secrets` (required — single-use refresh tokeny musia byť perzistovateľné). Copy testy potrebujú iba Web + Postgres + token cache — žiadne privileged node agenty. Script assertuje Job exits 0 a logy obsahujú `Passed!`.

**Verified tu (Docker, no cluster):** test image beží deterministic suite (`101 passed`) a, s writable `secrets/` mount, full **live** suite (`8 passed`) — exact Job path minus Kubernetes. `kind`/`kubectl`/`helm` unavailable in authoring env, takže full `k8s-e2e.sh` cluster run je ten jeden krok nie executed here.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Dva data-driven live suites build on `LiveCopyScenario` / `LiveCopyFixture`, live counterpart to deterministic DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: one real master open per row against demo accounts, each with differently-configured destination, asserting golden outcome. Rows: `one_to_one`, `half_multiplier`, `reverse` (opposite side), `manage_only` (opens nothing), `trading_hours_closed` (window excludes now → no copy), `source_label_block` (label filter → no copy), `lot_sanity_block` (ceiling → no copy).
- **`LiveCopyChaos`** — copy engine against hostile start: master already holds position before host starts, so convergence can only come from start-up resync (`Sync-Open-on-Start`), plus negative case with toggle off. Deterministic socket-flap / token-rotation / rejection chaos covered exhaustively by DST suite; this asserts live resync path.

Both **skip cleanly** without creds, report **Inconclusive** on closed market, every row cleans up positions it opened (demo accounts only).

**Fixture robustness:** `LiveCopyFixture` now degrades to clean skip when cached tokens can't refresh (cTrader refresh tokens single-use — stale cache or offline run no longer faults whole live collection; re-run OAuth onboarding to refresh cache).

## Bežanie live + cluster sád sami (ŽIADNE výhovorky — všetko je self-service)

Všetko nižšie dostupné v tomto prostredí. **Ne** tvrdte "creds unavailable", "token stale" alebo "kind/helm not installed" — nastavte ich a bežte.

### 1. Získajte čerstvý Open API token (headless OAuth, žiadna interakcia prehliadača)

cTrader refresh tokeny sú single-use, takže cached token WILL go stale. Re-mintnúť ho z uložených cID username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, or unified `secrets/dev-credentials.local.json`). Onboarding test hná **headless Edge** OAuth cez Playwright, píše `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; autorizuje každý cID, cachuje čerstvé tokeny. Znovu spustite kedykoľvek live suite hlási fixture unavailable kvôli refresh failure.

### 2. Bežte live copy suites (reálne cTrader demo účty)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Umiestni + vyčisti reálne DEMO objednávky (nikdy live účty), report **Inconclusive** na closed market. Verified green end to end.

### 3. Bootstrap tokeny z bežiaceho app volume (alternatíva)

Ak app beží + cID prepojené in-app, extrahujte app's najnovší refresh token priamo z `app-pg-data` Postgres volume namiesto re-autorizácie — pozrite `LiveTokenBootstrapTests`, nastavte `CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker dostupné (install kind/helm cez `go install`/release binaries alebo `choco install kind kubernetes-helm` ak nie na PATH). One-shot script builduje+loaduje images, deployuje chart, hná in-cluster test Job, assertuje exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Pozrite [../deployment/kubernetes.md](../deployment/kubernetes.md).
