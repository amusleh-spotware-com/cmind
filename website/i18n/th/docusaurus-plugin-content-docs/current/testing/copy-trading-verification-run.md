---
description: "Full verify of remaining copy-trading work — all below actually executed, not just authored."
---

# Copy-trading verification run (2026-07-10)

full verify ของ remaining copy-trading work — ทั้งหมด ด้านล่าง **actually executed** ไม่ใช่ เพียง authored

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh
added live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync` `OpenPositionSnapshot.StopLoss/TrailingStopLoss`)

## Integration (real Postgres Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: first node claims ทั้งหมด running profiles second claims **0** (ไม่มี double-copy); pause releases + reclaim
- `TokenRotationSignatureTests` — signature changes เฉพาะ on real token rotation

## In-cluster (kind + helm) — pass
installed `kind`/`kubectl`/`helm` ran `scripts/k8s-e2e.sh` against real kind cluster:
- **Deterministic job: 101 passed** in-cluster
- **Live job: 8 passed** in-cluster (init-container `seed-secrets` copies secret → writable emptydir real demo accounts)
- job `complete 1/1` script exit 0

## Bugs found ขณะ verifying (fixed + re-verified)
- **Pending events**: cTrader attaches *non-open position placeholder* ไป resting limit/stop `ORDER_ACCEPTED`/`CANCELLED` `SourceExecutionsAsync` now classifies placement/cancel เป็น order event ก่อน position branch แต่ lets limit/stop *fill* (เช่น stop-loss-triggered close) fall through ไป close path
- **Single-use refresh tokens**: cTrader rotates refresh token ทุก refresh read-only cache ที่ can't persist self-invalidates live k8s job therefore copies secret ไป **writable** emptydir; job defaults ไป deterministic suite `SaveTokens` now best-effort live symbols forced ไป FX (BTCUSD trailing amends broker-rejected)
- script image naming fixed ไป match helm `registry/repository` split + `pullPolicy=Never`

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Follow-up program adds order-type filtering pending-order expiry copying market-range /
stop-limit slippage mirroring SL/TP copy toggles graceful in-place token swap (single valid
token per cID) cTrader-faithful simulator self-healing node lease unified dev-
credentials file

- **Unit — 210 passed** (`dotnet test tests/UnitTests`) new copy coverage: order-type filter
  (open + pending) market-range slippage mirror + base price expiry copy on/off stop-limit
  slippage pending amend start-with-master-open disconnect→master-traded→reconnect resync
  (open missing + close orphan) in-place token swap (ไม่มี restart) cross-cID invalidation
  domain invariants lease ownership token-version bump
- **Integration (real postgres testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim
  ไม่มี double-copy pause release **expired-lease reclaim โดย another node**)
  `TokenRotationSignatureTests` (signature changes on token-version bump)
  `OpenApiAuthorizationPersistenceTests` (tokenversion persists + increments on refresh)
- **E2E** (`tests/E2ETests`): destination-option round-trip now asserts order-type filter
  copy-expiry copy-slippage alongside full lifecycle
- **Build**: clean ภายใต้ `TreatWarningsAsErrors`; rider `get_file_problems` clean on changed files

live scenarios (real ctradera demo accounts) สำหรับ pending-stop market-range expiry start-with-open
mid-run token rotation authored against same engine; run ด้วย unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md)

## Known follow-up
In-cluster live run rotated single-use token; regenerate local cache ด้วย
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(ctrader throttled oauth page ของมัน right หลัง run — retry เมื่อ clears)
