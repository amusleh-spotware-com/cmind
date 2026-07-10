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

## Known follow-up
The in-cluster live run rotated the single-use token; regenerate the local cache with
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader was throttling its OAuth page immediately after the run — retry when it clears).
