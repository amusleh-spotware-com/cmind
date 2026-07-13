---
description: "Full verify remaining copy-trading work — wszystko poniżej **actually executed**, nie tylko authored."
---

# Copy-trading verification run (2026-07-10)

Full verify remaining copy-trading work — wszystko poniżej **actually executed**, nie tylko authored.

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Added live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: pierwszy node claims wszystkie running profiles,
  drugi claims **0** (brak double-copy); pause releases + reclaim.
- `TokenRotationSignatureTests` — signature zmienia tylko na real token rotation.

## In-cluster (kind + Helm) — pass
Installed `kind`/`kubectl`/`helm`, ran `scripts/k8s-e2e.sh` przeciwko real kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` kopie Secret → writable emptyDir, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bugs found podczas verifying (fixed + re-verified)
- **Pending events**: cTrader attaches *non-open Position placeholder* do resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`.
  `SourceExecutionsAsync` teraz classifies placement/cancel jako order event zanim position branch, ale lets
  limit/stop *fill* (e.g. stop-loss-triggered close) fall przez do close path.
- **Single-use refresh tokens**: cTrader rotates refresh token każdy refresh. Read-only cache że nie może
  persist self-invalidates. Live K8s Job dlatego kopie Secret do **writable** emptyDir; Job defaults do
  deterministic suite. `SaveTokens` teraz best-effort. Live symbols forced do FX (BTCUSD trailing amends
  broker-rejected).
- Script image naming fixed aby match Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Follow-up program adds order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy toggles, graceful in-place token swap (single valid
token per cID), cTrader-faithful simulator, self-healing node lease, unified dev-
credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). Nowe copy coverage: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (brak restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integration (real Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim,
  brak double-copy, pause release, **expired-lease reclaim przez inny node**),
  `TokenRotationSignatureTests` (signature zmienia na token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persists + increments na refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip teraz asserts order-type filter,
  copy-expiry, copy-slippage alongside pełny lifecycle.
- **Build**: clean pod `TreatWarningsAsErrors`; Rider `get_file_problems` clean na changed files.

Live scenarios (real cTrader demo accounts) dla pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored przeciwko ten sam engine; run z unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Known follow-up
In-cluster live run rotated single-use token; regenerate local cache z
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader throttled jej OAuth strona right po run — retry gdy clears).
