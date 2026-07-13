---
description: "Full verify of remaining copy-trading work — all below actually executed, not just authored."
---

# Copy-trading verification run (2026-07-10)

Full verify of remaining copy-trading work — all below **actually executed**, not just authored.

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Added live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: first node claims all running profiles, second claims **0** (no double-copy); pause releases + reclaim.
- `TokenRotationSignatureTests` — signature changes only on real token rotation.

## In-cluster (kind + Helm) — pass
Installed `kind`/`kubectl`/`helm`, ran `scripts/k8s-e2e.sh` against real kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copies Secret → writable emptyDir, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bugs found while verifying (fixed + re-verified)
- **Pending events**: cTrader attaches *non-open Position placeholder* to resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` now classifies placement/cancel as order event before position branch, but lets limit/stop *fill* (e.g. stop-loss-triggered close) fall through to close path.
- **Single-use refresh tokens**: cTrader rotates refresh token every refresh. Read-only cache that can't persist self-invalidates. Live K8s Job therefore copies Secret into **writable** emptyDir; Job defaults to deterministic suite. `SaveTokens` now best-effort. Live symbols forced to FX (BTCUSD trailing amends broker-rejected).
- Script image naming fixed to match Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Follow-up program adds order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy toggles, graceful in-place token swap (single valid
token per cID), cTrader-faithful simulator, self-healing node lease, unified dev-
credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). New copy coverage: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (no restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integration (real Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim,
  no double-copy, pause release, **expired-lease reclaim by another node**),
  `TokenRotationSignatureTests` (signature changes on token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persists + increments on refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip now asserts order-type filter,
  copy-expiry, copy-slippage alongside full lifecycle.
- **Build**: clean under `TreatWarningsAsErrors`; Rider `get_file_problems` clean on changed files.

Live scenarios (real cTrader demo accounts) for pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored against same engine; run with unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Known follow-up
In-cluster live run rotated single-use token; regenerate local cache with
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader throttled its OAuth page right after run — retry when clears).
<!-- [ZH-HANS] Translation needed -->
