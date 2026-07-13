---
title: Copy-trading verification run
description: "Popolna verifikacija preostalega copy-trading dela — vse spodaj dejansko izvedeno, ne samo avtorirano."
---

# Copy-trading verification run (2026-07-10)

Popolna verifikacija preostalega copy-trading dela — vse spodaj **dejansko izvedeno, ne samo avtorirano**.

## Live (realni cTrader demo računi) — 8/8 potekajo
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Dodani live scenariji `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integracija (realen Postgres, Testcontainers) — poteka
- `CopyNodeAffinityTests` — supervisor realni atomski zahtevek: prvo vozlišče zahteva vse tekoče profile, drugo zahteva **0** (brez dvojnega-copyja); pavziranje sprosti + prevzame.
- `TokenRotationSignatureTests` — podpis se spremeni samo pri realni rotaciji žetona.

## Znotraj-gručni (kind + Helm) — poteka
Nameščen `kind`/`kubectl`/`helm`, zagnan `scripts/k8s-e2e.sh` proti realni kind gruči:
- **Deterministični Job: 101 passed** znotraj-gručno.
- **Live Job: 8 passed** znotraj-gručno (init-container `seed-secrets` kopira Secret → spreminjljiv emptyDir, realni demo računi).
- Job `Complete 1/1`, skripta exit 0.

## Hrošči najdeni med verificiranjem (popravljeni + znova verificirani)
- **Pending events**: cTrader pritrdi *ne-open Position placeholder* na počivajoč limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` zdaj klasificira postavitev/preklic kot order dogodek preden branch pozicije, toda pusti limit/stop *fill* (npr. stop-loss sproženo zaprtje) pasti čez v close path.
- **Enojne-rabe refresh žetoni**: cTrader zavrti refresh žeton ob vsakem osveževanju. Predpomnilnik samo za branje ki ne more vztrajati se sam razveljavi. Live K8s Job zato kopira Secret v **spremenljiv** emptyDir; Job privzeto na deterministični suite. `SaveTokens` zdaj best-effort. Live simboli prisiljeni na FX (BTCUSD trailing spremembe broker-zavrnjene).
- Popravljeno poimenovanje slike da se ujema s Helm `registry/repository` razdelitvijo + `pullPolicy=Never`.

## Napredno zrcaljenje + življenjski cikel žetona + skaliranje program (2026-07-10) — deterministične plasti potekajo

Follow-up program dodaja filtriranje tipa naročila, kopiranje poteka čakajočega, market-range /
stop-limit slippage zrcaljenje, SL/TP kopiraj preklopnike, gladka in-place zamenjava žetona (enojni veljaven
žeton na cID), cTrader-veren simulator, samozdravljivo vozlišče lease, enotna datoteka dev-
poverilnic.

- **Enote — 210 passed** (`dotnet test tests/UnitTests`). Nova copy pokritost: filter tipa naročila
  (open + pending), market-range slippage mirror + bazna cena, copy expiry on/off, stop-limit
  slippage, pending amend, start-with-master-open, disconnect→master-traded→reconnect resync
  (open missing + close orphan), in-place token swap (no restart), cross-cID invalidation,
  domain invariants, lease ownership, token-version bump.
- **Integracija (realen Postgres, Testcontainers) — poteka**: `CopyNodeAffinityTests` (atomski zahtevek,
  brez dvojnega-copyja, pavziranje sprosti, **potekel-lease prevzemi od drugega vozlišča**),
  `TokenRotationSignatureTests` (podpis se spremeni na token-version bump),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion vztraja + increment na Refresh).
- **E2E** (`tests/E2ETests`): round-trip možnosti cilja zdaj trdi filter tipa naročila,
  copy-expiry, copy-slippage poleg polnega življenjskega cikla.
- **Build**: čist pod `TreatWarningsAsErrors`; Rider `get_file_problems` čist na spremenjenih datotekah.

Live scenariji (realni cTrader demo računi) za pending-stop, market-range, expiry, start-with-open,
mid-run token rotation avtorirani proti istemu motorju; zaženi z enotno
`secrets/dev-credentials.local.json` na [dev-credentials.md](dev-credentials.md).

## Znana nadaljevanja
Znotraj-gručni live zagon je zavrtel enojne-rabe žeton; regeneriraj lokalni predpomnilnik z
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader je ravno takrat throttlal svojo OAuth stran — ponovi ko se sprosti).
