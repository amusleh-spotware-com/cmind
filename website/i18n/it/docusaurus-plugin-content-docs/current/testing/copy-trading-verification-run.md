---
description: "Verifica completa del lavoro copy-trading rimanente — tutto sotto effettivamente eseguito, non solo authored."
---

# Copy-trading verification run (2026-07-10)

Verifica completa del lavoro copy-trading rimanente — tutto sotto **effettivamente eseguito**, non solo authored.

## Live (real cTrader demo accounts) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Aggiunti scenari live `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: primo nodo reclama tutti i profili in esecuzione, il secondo reclama **0** (no double-copy); pause rilascia + reclaim.
- `TokenRotationSignatureTests` — la signature cambia solo su reale token rotation.

## In-cluster (kind + Helm) — pass
Installato `kind`/`kubectl`/`helm`, eseguito `scripts/k8s-e2e.sh` contro reale kind cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copia Secret → emptyDir scrivibile, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bug trovati durante la verifica (fixati + ri-verificati)
- **Pending events**: cTrader attacha *non-open Position placeholder* a resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` ora classifica placement/cancel come order event prima del branch position, ma fa passare limit/stop *fill* (es. stop-loss-triggered close) attraverso al percorso close.
- **Single-use refresh tokens**: cTrader ruota refresh token ogni refresh. Cache read-only che non può persistere si auto-invalida. Live K8s Job quindi copia Secret in **scrivibile** emptyDir; Job default a suite deterministica. `SaveTokens` ora best-effort. Live symbols forzati a FX (BTCUSD trailing amends broker-rejected).
- Image naming dello script fixato per matchare Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + programma di scaling (2026-07-10) — tier deterministici pass

Il programma di follow-up aggiunge order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy toggle, graceful in-place token swap (single valid token per cID),
cTrader-faithful simulator, self-healing node lease, unified dev-credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). Nuova copertura copy: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (no restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integration (real Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests` (atomic claim,
  no double-copy, pause release, **expired-lease reclaim by another node**),
  `TokenRotationSignatureTests` (signature changes on token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persists + increments on refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip ora assert order-type filter,
  copy-expiry, copy-slippage alongside full lifecycle.
- **Build**: clean sotto `TreatWarningsAsErrors`; Rider `get_file_problems` clean su file changed.

Live scenarios (real cTrader demo accounts) per pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored contro stesso engine; eseguiti con unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Follow-up noto
Live in-cluster run ha rotato single-use token; rigenera cache locale con
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader ha throttled la sua OAuth page subito dopo il run — retry quando si libera).
