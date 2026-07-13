---
description: "Plná verifikácia zostávajúcej copy-trading práce — všetko nižšie skutočne vykonané, nie len autorované."
---

# Copy-trading verification run (2026-07-10)

Plná verifikácia zostávajúcej copy-trading práce — všetko nižšie **skutočne vykonané**, nie len autorované.

## Live (reálne cTrader demo účty) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Pridané live scenáre `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integrácia (reálny Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor reálny atómický claim: prvý node claimuje všetky bežiaci profily, druhý claimuje **0** (žiadne double-copy); pause uvoľní + reclaim.
- `TokenRotationSignatureTests` — signature sa mení len pri reálnej token rotácii.

## In-cluster (kind + Helm) — pass
Nainštalovaný `kind`/`kubectl`/`helm`, bežal `scripts/k8s-e2e.sh` proti reálnemu kind clusteru:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` kopíruje Secret → writable emptyDir, reálne demo účty).
- Job `Complete 1/1`, script exit 0.

## Bugs nájdené počas verifikácie (opravené + re-verifikované)
- **Pending udalosti**: cTrader pripojuje *non-open Position placeholder* k resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` teraz klasifikuje placement/cancel ako order event pred position branch, ale nechá limit/stop *fill* (napr. stop-loss-triggered close) prejsť close path.
- **Single-use refresh tokeny**: cTrader rotuje refresh token každý refresh. Read-only cache, ktorá nemôže perzistovať, sa invaliduje. Live K8s Job preto kopíruje Secret do **writable** emptyDir; Job predvolené je deterministic suite. `SaveTokens` teraz best-effort. Live symboly forced na FX (BTCUSD trailing amends broker-rejected).
- Script image naming opravený na match Helm `registry/repository` split + `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Follow-up program pridáva order-type filtering, pending-order expiry copying, market-range /
stop-limit slippage mirroring, SL/TP copy prepínatá, graceful in-place token swap (single valid
token per cID), cTrader-faithful simulátor, self-healing node lease, unified dev-
credentials file.

- **Jednotka — 210 passed** (`dotnet test tests/UnitTests`). Nové copy coverage: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (no restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integrácia (reálny Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests` (atómický claim,
  žiadne double-copy, pause release, **expired-lease reclaim by another node**),
  `TokenRotationSignatureTests` (signature sa mení na token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion perzistuje + inkrementuje na refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip teraz assertuje order-type filter,
  copy-expiry, copy-slippage popri plnom lifecycle.
- **Build**: clean under `TreatWarningsAsErrors`; Rider `get_file_problems` clean na changed files.

Live scenáre (reálne cTrader demo účty) pre pending-stop, market-range, expiry, start-with-open,
mid-run token rotation authored proti rovnakému engine; bežané s unified
`secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Known follow-up
In-cluster live run rotated single-use token; regenerate local cache with
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader throttle-oval its OAuth page right after run — retry keď sa uvoľní).
