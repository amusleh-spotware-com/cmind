---
description: "Пуни репродуцибилни copy-trading пакет тестова. Два слоја:"
---

# Copy-trading пакет тестова (детерминистички + live)

Пуни репродуцибилни copy-trading пакет тестова. Два слоја:

1. **Детерминистички тестови** (xUnit, без мреже) — copy математика + engine логика. Брзо, CI, без тајни. Покрива сваки режим управљања новцем, сваки филтер/опција, engine отпорност.
2. **Live E2E тестови** (прави cTrader демо налози) — производња `CopyEngineHost` постављање + копирање правих налога између правих налога. Потпуно аутоматизовано, поновно покрајуће као unit тест: читај кеширане акредитиве из локалних gitignored датотека, self-refresh приступ токена, прескочи чисто када су тајне одсутне (CI остаје зелена).

Никад не бежи против live-funded налога — сваки налог **демо**, сваки live тест затварају позиције које је отворио.

## Расподела

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — сваки режим великости + заокружи + min/max лот
  CopyDecisionEngineTests.cs     — смер/обрнуто/slippage/delay/symbol филтер/size-zero
  CopyEngineHostTests.cs         — host copy логика против in-memory fake сесије
  FakeTradingSession.cs          — детерминистички IOpenApiTradingSession (записи налози/затвара/амендс)
  OpenApiConnectionTests.cs      — повезивање / поновна веза / backoff / fatalna грешка (отпорност)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — учитава gitignored тајне, чува освежене токене
  LiveTokenBootstrapTests.cs     — one-shot: дешифрује токене из app DB у токен кеш
  LiveCopyFixture.cs             — ротира приступ токена, изложи демо налог листу
  LiveCopyScenario.cs            — покрена jedan прави copy сценарио end to end (отварање → копија → верификуј → очисти)
  CopyTradingLiveTests.cs        — the live сценарији (1:1, 1:many, обрнуто, …)
```

## Тајне (локално, gitignored — никад посвећено)

Сви акредитиви под `<repo>/secrets/` (већ у `.gitignore`). Dev пише **само прве две датотеке**; треће (токени) аутоматски-произведени од стране onboarding.

`secrets/openapi-test-app.local.json` — Open API апп:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login акредитиви за овлашћење (једна или више):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **написано од стране onboarding**, multi-cID, освежена сваки тек:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **никад истиче**, тако да по one-time onboarding live тестови раде бесконачно: сваки тек размењује сваку cID-у refresh token за fresh приступ token (ротација) — без прегледача, без позива.

## One-time onboarding (потпуно аутоматизовано — нема dev интеракције изван спашавања акредитива)

Onboarding вози прави cTrader ID логин у headless прегледачу од спашених cID акредитива, хвата OAuth callback на локалном HTTPS слушаоцу при апп-регистрованој преусмеравање (`https://localhost:7080/openapi/callback`), размењује код за токене, учитава налог листу, пишет multi-cID токен кеш. Покренуте једном по машини (или при додавању cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Овлашћује сваку cID у `openapi-cids.local.json`, пишет `openapi-tokens.local.json`. Удаљено од тога live копија тестови требају ничегов друго. (cID-у cTrader ID налог мора немати 2FA/captcha при логину за аутоматизацију да крај.)

**Алтернатива bootstrap** (ако су налози већ овлашћени у покренутој апликацији): дешифрује сачуване токене право из app-а Postgres волумена уместо поново-овлашћења:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Безбедност — демо само

Live тестови трговају **само демо налози**: фиксетуре филтера токен кеш до налога са `IsLive == false` и повезивања демо гејтвеј, тако редослед никад не слети на live/funded налог чак и ако је live налог овлашћен. Сваку позицију тест отварања затворене у очистци.

## Покренута

```bash
# Детерминистички копија тестови само (брзо, без тајни, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live копија тестови против правих демо налога (требају две тајне датотеке)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Све
dotnet test
```

Без тајне датотека live тестови штампају разлог прескока + прохода као no-ops, тако пакет safe за покренута било где.

## Покривеност

### Управљање новцем / величина (детерминистички — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / валута) · ProportionalBalance · ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage · скала **горе** и **доле** за balance/leverage/capacity недопаривање (the "golden rule") · lot-step заокружи · min-lot прескочи вс. force-to-min · max-lot капа · tight-of bound-vs-spec мин & мах · zero главни balance прескочи.

### Филтери одлуке (детерминистички — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / дозволити · LongOnly / ShortOnly · обрнуто флипс ефективна страна · slippage преко лимита прескочи + тачно-on-limit дозвољено · stale-signal (max delay) прескочи · size-zero прескочи · reconnect умирити (open-missing dedup, close-orphaned).

### Copy engine host (детерминистички — `CopyEngineHostTests`, in-memory сесија)
Open огледала тржишни налог (страна / волумен / етикета) · **обрнуто** флипс страна и **размени SL/TP** · **symbol пресликавање** решава одредишни симбол · **налог-неуспех на једну slave јако копирање на друге** · source затвори затвори огледало копија · reconnect resync затварају сирот копија.

### Веза отпорност (детерминистички — `OpenApiConnectionTests`)
Достиже Connected после app аутентификације · прекинута веза ponovoljava i re-auths · fatal auth грешка неуспеси · exponential backoff.

### Live, прави cTrader демо налози (`CopyTradingLiveTests`)
Token освежи + налог листање · **1:1** копија извршава · **1:many** копија огледала за сваку slave · **обрнуто** окрене главни куповати у slave продају · **cross-cID** копија (главни под једна cID огледала за slave под друга, свака аутентификација са сопственим токеном). Сваки отвара прави min-lot позицију на главна, чека engine да је огледала (подударно по source-position-id етикета на slave), потврди, затвори све. Затворено тржишту пријављена **Inconclusive**, не неуспевајава.

## Логовање & аудитабилност

Сваке copy трговање операција пријављена via source-generated структурирани догађаји (`Core/Logging/LogMessages.cs`, event IDs 1043–1055), пуна стаза аудитабилна:

| Событај | Id | Смисла |
|-------|----|---------|
| CopyHostStarted | 1046 | a профил-а engine дошло горе (source + одредишта број) |
| CopySourceOpen | 1047 | главни отворена позиција (симбол / страна / лотова) |
| CopyOrderPlaced | 1048 | копија налог послан slave (симбол / страна / волумен / source id) |
| CopySkipped | 1049 | a копија била прескочена и зашто (slippage / смер / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP примењена на slave копија |
| CopyOpenFailed | 1051 | a slave копија-отварање неуспело (изолована — остали slave-и наставити) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | главни затворена → slave копија затворена |
| CopyCloseFailed | 1054 | a slave копија-затварање неуспело |
| CopyResync | 1055 | reconnect умирити (source отварање број, сирот затворена) |
| CopyPartialClose | 1056 | главни делимично затварање огледала — proportional крајња затворена на slave |
| CopyScaleIn | 1057 | главни scale-in огледала (opt-in) — додано волумен копирано slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop огледала slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending отказан → slave pending отказан |
| CopyTrailingApplied | 1060 | trailing stop примењена на slave копија (opt-in) |
| CopyStopLossAmended | 1061 | a source SL покрет re-amended slave копија |
| CopyHostTokenRotated | 1062 | надзорник поново покренут покренута host по њеног приступ токена ротирана |

Логови емитовани као Serilog компактна JSON (структуирана пропс: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), послана OTLP када је `OTEL_EXPORTER_OTLP_ENDPOINT` постављена. **Потпуно конфигурабилно** per категорија via стандард config — нпр. подизање/снижавање копија-engine verbose без дотицања код:

```jsonc
// appsettings.json — Serilog нивој преписује
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit стаза
  "Nodes.CopyTrading": "Information"        // надзорник / token освежи
} } }
```

`Audit_log_records_every_trading_operation` host тест потврди стаза пожара за отварање, налог, заштита, затварање.

## Edge случајеви (верификовано против како прави копија/MAM платформи неуспешна)

Slippage & кашњење, симбол suffix/mismatch, дупла трговање на reconnect, leverage недопаривање & margin-safe величина, deposit-валута/contract-size разлика, min/max лот & заокружи, одбијени налози, смер филтери, сирот очистка после раскида — све покривена горе. Извори: [leverage недопаривање](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) · [cross-broker копирање](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) · [копијер замке](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) · [slippage & кашњење](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) · [зашто копирање трговање неуспева](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) · [ризик параметри](https://www.mt4copier.com/risk-parameters/).

## Напредна слика покривеност (делимично затварање · pending налози · SL-trailing)

Host огледала више него тржишни отварање/затварање. Сваки понашање = per-destination opt-in флаг на `CopyDestination` (`MirrorPartialClose` подразумевано на, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` подразумевано искл), заштићена од намере методи, jsonb-упорно (миграција `CopyAdvancedMirroringAndNodeAffinity`).

| Понашање | Детерминистички тест (`CopyEngineHostTests`) | Live тест |
|-----------|--------------------------------------------|-----------|
| Делимично затварање → proportional крајња | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 затворе 60%) + оневозмогућена путања | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop постављена | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + оневозмогућена путања | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending отказ | `Source_pending_cancel_cancels_the_slave_pending` | (исти live тест — отказ на главна, потврде slave отказ) ✅ |
| Пуњена pending без double-open | `Filled_pending_does_not_double_open` (налог-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL преместити re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit eventi паљбе | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Сви live тестови горе **верификовано зелена против прави cTrader демо налога** (1:1, 1:many, обрнуто, cross-cID, делимично затварање, pending+cancel, trailing).

Wire додаци у `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing флаг на `AmendPositionSltpAsync`, налог/pending поља на `ExecutionEvent`, `LoadSpotPriceAsync` (spot претплатити → bid/ask, коришћена од стране live pending/trailing тестови за место мировања налози далеко од тржишта), `StopLoss`/`TrailingStopLoss` на `OpenPositionSnapshot` (копија-и trailing стање посматрачно via умирити). Одредишта копија остају етикетиране од **source position id** (pending копија од source **налог id**) тако reconnect умирити остајау id-заснована, никад дупла трговање.

**cTraderEvent готча (верификовано live):** мировање pending налог-а `ORDER_ACCEPTED`/`ORDER_CANCELLED` извршење dogodaj носи **non-open `Position` заместиље** плус `Order`. Ток мора класифику то као *налог* dogodaj **пре** position гране (врата на position не `OPEN`), иначе pending јављање мис-читање као положај затварање. `SourceExecutionsAsync` ради ово; недостаје то тихо капе све pending огледала.

## Token ротација + node аљинејаност

- **Ротација у покренута хостова.** `CopyEngineSupervisor` записи token потпис на сваки покренута host и, сваки умирити, преградити план од DB (свеже ротирана од `OpenApiTokenRefreshService`). Промењена потпис рестарт host (`CopyHostTokenRotated`, 1062); нови host-а `ResyncAsync` преградити стање без дупла трговање. Force ротација mid-run via `IOpenApiTokenClient.RefreshAsync` верифику live host чува копија.
- **Node аљинејаност (без double-copy).** Обоје Web локално node и `CopyAgent` радник покренута надзорник. Сваки покренута профил захтевала тачно једна node (`CopyProfile.AssignedNode`, атомски `ExecuteUpdate` захтев од ключне `CopyOptions.NodeName`, подразумевано машина име). Надзорник хостова само профила то оснований; стоп/паузирање ослобађа захтев. Покривеност:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`, `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Интеграција (прави Postgres, Testcontainers)**: `CopyNodeAffinityTests` вози надзорник-а прави `ClaimUnassignedProfilesAsync` — потврди прва node захтева све 3 покренута профила, друга захтева **0** (без double-host), паузирање→рестарт ослобађа захтев за другог node.
  - Ротација откривање (`TokenRotationSignatureTests`): надзорник-а `TokenSignature` мења када source или одредишта token ротирана, стабилна иначе (покренута host рестарт само на правој ротацији).

### Једнократно коришћени refresh токени (важно)

cTrader **refresh токени су једнократно коришћена** — сваки освежи враћања *нови* refresh token, инвалидира старо. Live фиксетуре освежи на почетак, упорно ротирана token `secrets/openapi-tokens.local.json`. Последице:
- Ако тек освежи али **не може упорно** нови token (нпр. read-only монт), кеширана token мртва, следећи тек неуспева `ACCESS_DENIED`. Регенеришите са headless onboarding: `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` гутање пишемо неуспеси тако read-only кеш не крахира тек, али **live** in-cluster пакет јако требају **писљиви** кеш (K8s Job копира Secret у emptyDir — видите deployment док).

## Покренута пакет у Kubernetes кластеру

Цела пакет покрена in-cluster против Helm-deployed апликација, тако регресија ухваћена in-cluster исто као локално. Видите [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind кластер, детерминистички пакет (без тајни)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` граде runner слику; Helm `tests-job.yaml` (врата `tests.enabled=false`) покрена то против in-cluster Postgres + Web. **Default = детерминистички копија пакет** (без тајни, без ротирања токена). За live пакет, поставити `tests.copySecret` до Secret држава gitignored `openapi-*.local.json`; init-container копира то у **писљиви** emptyDir на `/app/secrets` (потребно — једнократно коришћена refresh токени мора бити упорно). Copy тестови требају само Web + Postgres + token кеш — без привилегова node агента. Скрипта потврде Job излаз 0 и логови садржи `Passed!`.

**Верификовано овде (Docker, без кластера):** тест слика покрена детерминистички пакет (`101 passed`) и, са писљивим `secrets/` монт, пуна **live** пакет (`8 passed`) — тачна Job путања минус Kubernetes. `kind`/`kubectl`/`helm` недостаје у окружење аутора, тако пуна `k8s-e2e.sh` кластер тек је онај корак не извршена овде.

## Live опција матрица + chaos (LiveCopyMatrix / LiveCopyChaos)

Два data-driven live пакети изградити на `LiveCopyScenario` / `LiveCopyFixture`, live паралела да детерминистички DST stress пакет:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` опција матрица: један прави главни отварање per ред против демо налога, свака са различно-конфигуирана одредишта, потврђујућа golde исход. Редови: `one_to_one`, `half_multiplier`, `reverse` (супротна страна), `manage_only` (отворе ништа), `trading_hours_closed` (прозор искључи сада → без копија), `source_label_block` (етикета филтер → без копија), `lot_sanity_block` (стопа → без копија).
- **`LiveCopyChaos`** — копија engine против враћа почетак: главни већ држите позицију пре host почињу, тако конвергенција може само доћи од start-up resync (`Sync-Open-on-Start`), плус негативан случај са прекидањем искл. Детерминистички socket-flap / token-ротација / одбити chaos покривена исцрпно од DST пакет; ово потврди live resync путања.

Обоје **прескочи чисто** без акредитива, извештај **Inconclusive** на затворено тржишту, сваки ред чисти позиције то отворена (демо налози само).

**Фиксетуре отпорност:** `LiveCopyFixture` сада деградира чисто прескочи када кеширана токени не могу освежи (cTrader refresh токени једнократно коришћена — stale кеш или offline тек не фалт цела live колекција; re-run OAuth onboarding до освежи кеш).

## Покренута the live + кластер пакети себе (NO изговорима — све је self-serviceable)

Све доле доступна у ово окружење. Ради **не** потврди "creds недостаје", "token stale", или "kind/helm не инсталирана" — подеси их и покренута их.

### 1. Узмите fresh Open API token (headless OAuth, без прегледача интеракције)

cTrader refresh токени једнократно коришћена, тако кеширана token ЈЕ отпада stale. Re-mint то себе од спашена cID кориснико име/лозинка (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, или уједињена `secrets/dev-credentials.local.json`). Onboarding тест вози **headless Edge** OAuth via Playwright, пишет `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; овлашћује сваку cID, кешира fresh токени. Re-run кад год live пакет извештаји фиксетуре недостаје због освежи неуспеха.

### 2. Покренута live копија пакети (прави cTrader демо налози)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core огледала (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # опција матрица (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Место + очисти прави DEMO налози (никад live налози), извештај **Inconclusive** на затворено тржишту. Верификовано зелена end до конца.

### 3. Bootstrap токени од покренута апликација волумена (алтернатива)

Ако апп тек + cID повезано у-апп, извлачи апп-а најнови refresh token право од `app-pg-data` Postgres волумена уместо поново-овлашћење — видите `LiveTokenBootstrapTests`, поставити `CMIND_VOLUME_CONN`.

### 4. Kubernetes кластер E2E

`kind`, `helm`, Docker доступна (инсталирајте kind/helm via `go install`/отпуст бинари или `choco install kind kubernetes-helm` ако не на PATH). One-shot скрипта граде+учитава слике, распоредимо граф, покрена in-cluster тест Job, потврде излаз 0:

```bash
scripts/k8s-e2e.sh                                 # детерминистички копија пакет (без тајни)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Видите [../deployment/kubernetes.md](../deployment/kubernetes.md).
