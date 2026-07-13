---
description: "Xác minh đầy đủ công việc copy-trading còn lại — tất cả bên dưới thực sự được thực thi, không chỉ được viết."
---

# Lần chạy xác minh copy-trading (2026-07-10)

Xác minh đầy đủ công việc copy-trading còn lại — tất cả bên dưới **thực sự được thực thi**, không chỉ được viết.

## Live (tài khoản demo cTrader thực) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Đã thêm live scenarios `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (Postgres thực, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: node đầu tiên claim tất cả profile đang chạy, node thứ hai claim **0** (không double-copy); pause releases + reclaim.
- `TokenRotationSignatureTests` — signature chỉ thay đổi khi token thực sự được rotate.

## In-cluster (kind + Helm) — pass
Đã cài `kind`/`kubectl`/`helm`, chạy `scripts/k8s-e2e.sh` với kind cluster thực:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copies Secret → writable emptyDir, tài khoản demo thực).
- Job `Complete 1/1`, script exit 0.

## Bugs tìm thấy khi xác minh (đã sửa + xác minh lại)
- **Pending events**: cTrader gắn *non-open Position placeholder* vào resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` giờ phân loại placement/cancel là order event trước nhánh position, nhưng để limit/stop *fill* (ví dụ stop-loss-triggered close) đi qua đến close path.
- **Single-use refresh tokens**: cTrader rotates refresh token mỗi lần refresh. Read-only cache không thể persist self-invalidates. Live K8s Job vì vậy copies Secret vào **writable** emptyDir; Job mặc định deterministic suite. `SaveTokens` giờ best-effort. Live symbols forced to FX (BTCUSD trailing amends broker-rejected).
- Script image naming đã sửa để khớp Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Chương trình tiếp theo thêm order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy toggles, graceful in-place token swap (một token hợp lệ duy nhất
per cID), cTrader-faithful simulator, self-healing node lease, unified dev-credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). Coverage mới cho copy: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (no restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integration (Postgres thực, Testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim,
  no double-copy, pause release, **expired-lease reclaim by another node**),
  `TokenRotationSignatureTests` (signature changes on token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persists + increments on refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip giờ assert order-type filter,
  copy-expiry, copy-slippage cùng với full lifecycle.
- **Build**: clean dưới `TreatWarningsAsErrors`; Rider `get_file_problems` clean trên các file đã thay đổi.

Live scenarios (tài khoản demo cTrader thực) cho pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored against same engine; chạy với unified
`secrets/dev-credentials.local.json` theo [dev-credentials.md](dev-credentials.md).

## Follow-up đã biết
In-cluster live run đã rotate single-use token; regenerate local cache với
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader throttled its OAuth page right after run — retry when clears).
