# Copy-trading verification run (2026-07-10)

Full verification of the remaining copy-trading work — everything below was **actually executed**, not just authored.

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Added live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor's real atomic claim: first node claims all running profiles, second claims **0** (no double-copy); pause releases + reclaim.
- `TokenRotationSignatureTests` — signature changes only on a real token rotation.

## In-cluster (kind + Helm) — pass
Installed `kind`/`kubectl`/`helm`, ran `scripts/k8s-e2e.sh` against a real kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copies the Secret → writable emptyDir, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bugs found while verifying (fixed + re-verified)
- **Pending events**: cTrader attaches a *non-open Position placeholder* to a resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` now classifies placement/cancel as an order event before the position branch, but lets a limit/stop *fill* (e.g. a stop-loss-triggered close) fall through to the close path.
- **Single-use refresh tokens**: cTrader rotates the refresh token on every refresh. A read-only cache that can't persist self-invalidates. The live K8s Job therefore copies the Secret into a **writable** emptyDir; the Job defaults to the deterministic suite. `SaveTokens` is now best-effort. Live symbols forced to FX (BTCUSD trailing amends are broker-rejected).
- Script image naming fixed to match the Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Follow-up program adding order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy toggles, graceful in-place token swap (single valid
token per cID), a cTrader-faithful simulator, a self-healing node lease, and a unified dev-
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
  copy-expiry, and copy-slippage alongside the full lifecycle.
- **Build**: clean under `TreatWarningsAsErrors`; Rider `get_file_problems` clean on changed files.

Live scenarios (real cTrader demo accounts) for pending-stop, market-range, expiry, start-with-open,
and mid-run token rotation are authored against the same engine; run them with the unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Known follow-up
The in-cluster live run rotated the single-use token; regenerate the local cache with
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader was throttling its OAuth page immediately after the run — retry when it clears).
