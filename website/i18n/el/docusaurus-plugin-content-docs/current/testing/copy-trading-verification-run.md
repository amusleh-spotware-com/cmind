---
description: "Πλήρης επαλήθευση του copy-trading work — όλα τα παρακάτω πραγματικά εκτελέστηκαν, δεν γράφτηκαν απλά."
---

# Copy-trading verification run (2026-07-10)

Πλήρης επαλήθευση του copy-trading work — όλα τα παρακάτω **πραγματικά εκτελέστηκαν**, δεν
γράφτηκαν απλά.

## Live (πραγματικοί cTrader demo λογαριασμοί) — 8/8 pass
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** ·
**trailing stop** · token-refresh.
Προστέθηκαν live scenarios `RunPendingAsync` / `RunTrailingAsync`
(+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (πραγματικό Postgres, Testcontainers) — pass
- `CopyNodeAffinityTests` — supervisor real atomic claim: πρώτο node διεκδικεί όλα τα running
  profiles, δεύτερο διεκδικεί **0** (κανένα double-copy)· pause releases + reclaim.
- `TokenRotationSignatureTests` — η υπογραφή αλλάζει μόνο σε πραγματικό token rotation.

## In-cluster (kind + Helm) — pass
Εγκαταστάθηκε `kind`/`kubectl`/`helm`, εκτελέστηκε `scripts/k8s-e2e.sh` ενάντια σε real kind
cluster:
- **Deterministic Job: 101 passed** in-cluster.
- **Live Job: 8 passed** in-cluster (init-container `seed-secrets` copies Secret → writable
  emptyDir, real demo accounts).
- Job `Complete 1/1`, script exit 0.

## Bugs βρέθηκαν κατά την επαλήθευση (διορθώθηκαν + επαληθεύτηκαν)
- **Pending events**: ο cTrader επισυνάπτει *non-open Position placeholder* σε resting
  limit/stop `ORDER_ACCEPTED`/`CANCELLED`. Το `SourceExecutionsAsync` τώρα ταξινομεί
  placement/cancel ως order event πριν το position branch, αλλά αφήνει limit/stop *fill*
  (π.χ. stop-loss-triggered close) να περάσει στο close path.
- **Single-use refresh tokens**: ο cTrader περιστρέφει το refresh token κάθε refresh.
  Το read-only cache που δεν μπορεί να επιμένει self-invalidates. Το live K8s Job επομένως
  αντιγράφει το Secret σε **writable** emptyDir· το Job default στο deterministic suite.
  Το `SaveTokens` τώρα best-effort. Live symbols forced to FX (BTCUSD trailing amends
  broker-rejected).
- Το script image naming διορθώθηκε ώστε να ταιριάζει το Helm `registry/repository` split +
  `pullPolicy=Never`.

## Advanced mirroring + token-lifecycle + scaling program (2026-07-10) — deterministic tiers pass

Το follow-up πρόγραμμα προσθέτει order-type filtering, pending-order expiry copying,
market-range / stop-limit slippage mirroring, SL/TP copy toggles, graceful in-place token
swap (ένα έγκυρο token ανά cID), cTrader-faithful simulator, self-healing node lease,
unified dev-credentials file.

- **Unit — 210 passed** (`dotnet test tests/UnitTests`). Νέα copy coverage: order-type filter
  (open + pending), market-range slippage mirror + base price, expiry copy on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (χωρίς restart), cross-cID
  invalidation, domain invariants, lease ownership, token-version bump.
- **Integration (πραγματικό Postgres, Testcontainers) — pass**: `CopyNodeAffinityTests`
  (atomic claim, κανένα double-copy, pause release, **expired-lease reclaim by another node**),
  `TokenRotationSignatureTests` (η υπογραφή αλλάζει σε token-version bump),
  `OpenApiAuthorizationPersistenceTests` (το TokenVersion επιμένει + increments on refresh).
- **E2E** (`tests/E2ETests`): destination-option round-trip τώρα asserts order-type filter,
  copy-expiry, copy-slippage μαζί με full lifecycle.
- **Build**: clean under `TreatWarningsAsErrors`· Rider `get_file_problems` clean σε changed
  files.

Live scenarios (πραγματικοί cTrader demo λογαριασμοί) για pending-stop, market-range, expiry,
start-with-open, mid-run token rotation γράφτηκαν ενάντια στον ίδιο engine· εκτελέστηκαν με
unified `secrets/dev-credentials.local.json` ανά [dev-credentials.md](dev-credentials.md).

## Γνωστό follow-up
Το in-cluster live run περιέστρεψε single-use token· αναγεννήστε το τοπικό cache με
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(ο cTrader throttled το OAuth page αμέσως μετά το run — retry όταν καθαρίσει).
