---
description: "Full verify remaining copy-trading work — semua di bawah **actually executed**, bukan hanya authored."
---

# Copy-trading verification run (2026-07-10)

Full verify remaining copy-trading work — semua di bawah **actually executed**, bukan hanya authored.

## Live (real cTrader demo account) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Tambah live scenario `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: first node claim semua running profile, second claim **0** (tidak ada double-copy); pause release + reclaim.
- `TokenRotationSignatureTests` — signature berubah hanya pada real token rotation.

## In-cluster (kind + Helm) — pass
Installed `kind`/`kubectl`/`helm`, jalankan `scripts/k8s-e2e.sh` terhadap real kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copy Secret → writable emptyDir, real demo account).
- Job `Complete 1/1`, script exit 0.

## Bug ditemukan saat verifying (fixed + re-verified)
- **Pending event**: cTrader attach *non-open Position placeholder* ke resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` sekarang classify placement/cancel sebagai order event sebelum position branch, tetapi biarkan limit/stop *fill* (mis. stop-loss-triggered close) fall through ke close path.
- **Single-use refresh token**: cTrader rotate refresh token setiap refresh. Read-only cache yang tidak dapat persist self-invalidate. Live K8s Job oleh karena itu copy Secret ke **writable** emptyDir; Job default ke deterministic suite. `SaveTokens` sekarang best-effort. Live symbol forced ke FX (BTCUSD trailing amend broker-rejected).
- Script image naming fixed untuk match Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tier pass

Follow-up program tambah order-type filtering, pending-order expiry copy, market-range / stop-limit slippage mirroring, SL/TP copy toggle, graceful in-place token swap (single valid token per cID), cTrader-faithful simulator, self-healing node lease, unified dev-credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). New copy coverage: order-type filter (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync (open missing + close orphan), in-place token swap (tidak ada restart), cross-cID invalidation, domain invariant, lease ownership, token-version bump.
- **Integration (real Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim,
