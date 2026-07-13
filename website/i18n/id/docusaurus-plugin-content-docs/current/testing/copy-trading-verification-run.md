---
description: "Verifikasi lengkap dari remaining copy-trading work — semua di bawah ini sebenarnya dieksekusi, bukan hanya ditulis."
---

# Copy-trading verification run (2026-07-10)

Verifikasi lengkap dari remaining copy-trading work — semua di bawah ini **sebenarnya dieksekusi**, bukan hanya ditulis.

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Ditambahkan live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: node pertama mengklaim semua running profiles, node kedua mengklaim **0** (tidak ada double-copy); pause release + reclaim.
- `TokenRotationSignatureTests` — signature berubah hanya pada real token rotation.

## In-cluster (kind + Helm) — pass
Terinstall `kind`/`kubectl`/`helm`, run `scripts/k8s-e2e.sh` terhadap real kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copies Secret → writable emptyDir, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bugs found while verifying (fixed + re-verified)
- **Pending events**: cTrader attaches *non-open Position placeholder* to resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` sekarang mengklasifikasikan placement/cancel sebagai order event sebelum branch posisi, tapi biarkan limit/stop *fill* (mis. stop-loss-triggered close) jatuh ke close path.
- **Single-use refresh tokens**: cTrader rotate refresh token setiap refresh. Read-only cache yang tidak dapat persist self-invalidate. Live K8s Job therefore copies Secret into **writable** emptyDir; Job defaults to deterministic suite. `SaveTokens` sekarang best-effort. Live symbols forced to FX (BTCUSD trailing amends broker-rejected).
- Script image naming fixed untuk match Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Program follow-up menambahkan order-type filtering, pending-order expiry copying, market-range /
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
- **E2E** (`tests/E2ETests`): destination-option round-trip sekarang asserts order-type filter,
  copy-expiry, copy-slippage alongside full lifecycle.
- **Build**: clean under `TreatWarningsAsErrors`; Rider `get_file_problems` clean on changed files.

Live scenarios (real cTrader demo accounts) untuk pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored against same engine; run dengan unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Known follow-up
In-cluster live run rotated single-use token; regenerate local cache dengan
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader throttled its OAuth page right after run — retry when clears).
