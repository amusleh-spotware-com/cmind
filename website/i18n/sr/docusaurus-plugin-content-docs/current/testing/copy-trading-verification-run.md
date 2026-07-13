---
description: "Пуна провера преостатка copy-trading работе — све доле је заиста извршена, не само написана."
---

# Copy-trading провера извршавања (2026-07-10)

Пуна провера преостатка copy-trading работе — све доле је **заиста извршена**, не само написана.

## Live (прави cTrader демо налози) — 8/8 пролаз
1:1 · 1:many · обрнуто · cross-cID · делимично-затворено · **pending limit + cancel** · **trailing stop** · refresh токена.
Додани live сценарији `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Интеграција (прави Postgres, Testcontainers) — пролаз
- `CopyNodeAffinityTests` — надзорник прави атомски захтев: прва чвор захтева све текуће профиле, друга захтева **0** (без двоструке копије); паузирање ослобађа + поновни захтев.
- `TokenRotationSignatureTests` — потпис се мења само на правој ротацији токена.

## У кластеру (kind + Helm) — пролаз
Инсталирана `kind`/`kubectl`/`helm`, покренута `scripts/k8s-e2e.sh` против правог kind кластера:
- **Детерминистички Job: 101 пролаз** у кластеру.
- **Live Job: 8 пролаз** у кластеру (init-container `seed-secrets` копира Secret → писљиви emptyDir, прави демо налози).
- Job `Complete 1/1`, скрипта излаз 0.

## Грешке пронађене док се верификује (исправљено + поново верификовано)
- **Pending догађаји**: cTrader прилаже *не-отворено Position заместиље* на мировању limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` сада класификује јављање/отказ као редослед догађаја пре гране позиције, али дозвољава limit/stop *пуњење* (нпр. stop-loss-triggered затворено) да пада кроз затворену путању.
- **Једнократно коришћени refresh токени**: cTrader ротира refresh токен при сваком рефреш. Кеш само за читање који не може да настави сам себе инвалидира. Live K8s Job према томе копира Secret у **писљиви** emptyDir; Job подразумева детерминистички пакет. `SaveTokens` сада је best-effort. Live симболи су принудно FX (BTCUSD trailing амендс брокер-одбијени).
- Скрипта име слике исправљено да се подудара са Helm `registry/repository` поделом + `pullPolicy=Never`.

## Напредна слика + token-lifecycle + програм за масштабирање (2026-07-10) — детерминистички нивои пролаз

Програм наставка додаје филтрирање типа налога, копирање рока истека pending налога, опсег тржишта / заустављање-лимита слипаж огледалење, SL/TP копирање праћења, грациозна in-place замена токена (једна важећа токена по cID), cTrader-верна симулација, самоподршана node lease, уједињена датотека акредитива развоја.

- **Unit — 210 пролаз** (`dotnet test tests/UnitTests`). Нова copy покривеност: филтрирање типа налога (отворено + pending), огледалење слипаж опсега тржишта + базна цена, копирање рока истека укл/искл, stop-limit слипаж, pending амендс, старт-са-главним-отворени, прекид→главни-трговани→поновна веза ресинк (отворено недостаје + затворено сироче), in-place замена токена (без рестарта), cross-cID инвалидација, домена инваријанти, власништво lease-а, bump верзије токена.
- **Интеграција (прави Postgres, Testcontainers) — пролаз**: `CopyNodeAffinityTests` (атомски захтев, без двоструке копије, паузирање ослобађа, **истекао-lease поновни захтев друге чвора**), `TokenRotationSignatureTests` (потпис се мења на bump верзије токена), `OpenApiAuthorizationPersistenceTests` (TokenVersion упорно + приращава се при рефреш).
- **E2E** (`tests/E2ETests`): опција одредишта round-trip сада потврђује филтрирање типа налога, copy-expiry, copy-slippage у целој животној вези.
- **Build**: чисто под `TreatWarningsAsErrors`; Rider `get_file_problems` чисто на измењеним датотекама.

Live сценарији (прави cTrader демо налози) за pending-stop, market-range, expiry, start-with-open, mid-run token rotation написани против исте машине; покренуће са уједињеним `secrets/dev-credentials.local.json` per [dev-credentials.md](dev-credentials.md).

## Познато наставак
In-cluster live извршавање је ротирало једнократно коришћени токен; регенеришите локалну кеш са `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests` (cTrader га је гасила своју OAuth страну одмах после извршавања — покушајте поново када се очисти).
