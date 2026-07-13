---
description: "Popoln reproducirljiv copy-trading test suite. Dve plasti:"
---

# Copy-trading test suite (deterministični + live)

Popoln reproducirljiv copy-trading test suite. Dve plasti:

1. **Deterministični testi** (xUnit, brez omrežja) — copy matematika + logika motorja. Hitro, CI, brez skrivnosti. Pokrivaj vsak način upravljanja denarja, vsak filter/možnost, odpornost motorja.
2. **Live E2E testi** (realni cTrader demo računi) — produkcijski `CopyEngineHost` postavljajo + kopirajo realna naročila med realnimi računi. Popolnoma avtomatizirani, ponovno-zaživlivi kot unit test: bere predpomnjene poverilnice iz lokalnih gitignored datotek, osveži dostopovni žeton, preskoči gladko ko skrivnosti manjkajo (CI ostane zeleno).

Nikoli ne teče proti live-financiranemu računu — vsak račun **demo**, vsak live test zapre pozicije ki jih odpre.

## Postavitev

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — vsak dimenzioniranje način + zaokroževanje + min/max lot
  CopyDecisionEngineTests.cs     — smer/reverse/slippage/delay/filter simbolov/velikost-na-samem
  CopyEngineHostTests.cs         — host copy logika proti in-memory fake seji
  FakeTradingSession.cs          — deterministični IOpenApiTradingSession (zapisi naročil/zaprtij/sprememb)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (odpornost)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — naloži gitignored skrivnosti, shrani osvežene žetone
  LiveTokenBootstrapTests.cs     — enokratno: dešifriraj žetone iz app DB v predpomnilnik žetonov
  LiveCopyFixture.cs             — zavrti dostopovni žeton, izpostavi seznam demo računov
  LiveCopyScenario.cs            — zaženi en realen copy scenarij end to end (odpri → copy → verificiraj → očisti)
  CopyTradingLiveTests.cs        — live scenariji (1:1, 1:many, reverse, …)
```

## Skrivnosti (lokalne, gitignored — nikoli commitane)

Vse poverilnice pod `<repo>/secrets/` (že v `.gitignore`). Dev piše **prvi dve datoteki samo**; tretja (žetoni) avtomatsko proizvedena od onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID prijavne poverilnice za avtorizacijo (en ali več):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **napisano od onboarding**, več-cID, osveženo na vsakem zagonu:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh žeton **nikoli ne poteče**, torej po enokratnem onboarding live testi delujejo dokler: vsak zagon zamenja vsakega cID-ja refresh žeton za svež dostopovni žeton (rotacija) — brez brskalnika, brez pozivov.

## Enokratni onboarding (popolnoma avtomatiziran — brez interakcije dev razen shranjevanja poverilnic)

Onboarding poganja pravo cTrader ID prijavo v headless brskalniku iz shranjenih cID poverilnic, prestreže OAuth callback na lokalnem HTTPS poslušalcu ob aplikacijinem registriranem redirect (`https://localhost:7080/openapi/callback`), zamenja kodo za žetone, naloži seznam računov, piše več-cID predpomnilnik žetonov. Zaženi enkrat na stroj (ali ko dodajaš cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Avtorizira vsak cID v `openapi-cids.local.json`, piše `openapi-tokens.local.json`. Po tem live copy testi ne potrebujejo nič drugega. (cTrader ID račun cTrader mora imeti onemogočeno 2FA/captcha na prijavi za avtomatizacijo.)

## Varnost — demo samo

Live testi trgujejo **samo demo račune**: fikstura filtrira predpomnilnik žetonov na račune z `IsLive == false` in se poveže na demo gateway, torej naročilo nikoli ne pristane na live/financiranem računu celo če je live račun avtoriziran. Vsaka pozicija ki jo test odpre je zaprta v čiščenju.

## Zagon

```bash
# Samo deterministični copy testi (hitro, brez skrivnosti, CI-varno)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy testi proti realnim demo računom (potrebuje dve skrivnostni datoteki)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Vse
dotnet test
```

Brez skrivnostnih datotek live testi natisnejo razlog preskoka + poteklejo kot no-ops, torej suite je varen za zagon kjer koli.

## Pokritost

### Upravljanje denarja / dimenzioniranje (deterministično — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (velikost pogodbe / valuta) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** in **down** za neujemanje bilance/vzvoda/kapacitete (the "golden rule") · lot-step
zaokroževanje · min-lot preskok proti force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Odločitveni filtri (deterministično — `CopyDecisionEngineTests`)
Simbol bela lista / črna lista / dovoli · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (deterministično — `CopyEngineHostTests`, in-memory seja)
Open mirrors a market order (side / volume / label) · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves the destination symbol · **order-failure on one slave still copies to the
others** · source close closes the mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (deterministično — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects and re-auths · fatal auth error faults ·
exponential backoff.

### Live, realni cTrader demo računi (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy (master under one cID mirrors to slave under another, each authenticating with own token). Each opens real min-lot position on master, waits for engine to mirror it (matched by source-position-id label on slave), asserts, closes everything. Closed market reported **Inconclusive**, ne failing.

## Beleženje in sledljivost

Vsaka copy trading operacija beležena prek vira-generiranih strukturiranih dogodkov (`Core/Logging/LogMessages.cs`, ID-ji dogodkov 1043–1055), polna sled auditable:

| Dogodek | Id | Pomen |
|---------|----|-------|
| CopyHostStarted | 1046 | profilov engine je prišel gor (vir + število ciljev) |
| CopySourceOpen | 1047 | glavni odprl pozicijo (simbol / stran / loti) |
| CopyOrderPlaced | 1048 | copy naročilo poslano na podrejenega (simbol / stran / volumen / vir id) |
| CopySkipped | 1049 | copy bil preskočen in zakaj (slippage / smer / filter_simbola / velikost_na_nič / …) |
| CopyProtectionApplied | 1050 | SL/TP uporabljen na podrejeni kopio |
| CopyOpenFailed | 1051 | podrejen copy-open failed (izoliran — drugi podrejeni nadaljujejo) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | glavni zaprl → podrejeni copy zaprt |
| CopyCloseFailed | 1054 | podrejen copy-close failed |
| CopyResync | 1055 | reconnect uskladitev (število odprtih vira, zaprti sirote) |
| CopyPartialClose | 1056 | glavni delno zaprtje zrcaljeno — sorazmerni rez zaprt na podrejenemu |
| CopyScaleIn | 1057 | glavni scale-in zrcaljen (opt-in) — dodani volumen kopiran na podrejenega |
| CopyPendingOrderPlaced | 1058 | čakajoč limit/stop zrcaljen na podrejenega (opt-in) |
| CopyPendingOrderCancelled | 1059 | vir čakajoč preklican → podrejen čakajoč preklican |
| CopyTrailingApplied | 1060 | trailing stop uporabljen na podrejeni kopio (opt-in) |
| CopyStopLossAmended | 1061 | vir SL premik znova-ustrezbil podrejeni copy |
| CopyHostTokenRotated | 1062 | supervisor ponovno zagnal tekočega gostitelja potem ko je dostopovni žeton zavrtel |

Dnevniki oddani kot Serilog kompakten JSON (strukturirane lastnine: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), ladje v OTLP ko `OTEL_EXPORTER_OTLP_ENDPOINT` nastavljen. **Popolnoma konfigurabilno** na kategorijo — npr. zvišaj/nižaj copy-engine verbosity brez dotikanja kode:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // CopyEngineHost auditna sled
  "Nodes.CopyTrading": "Information"         // supervisor / osveževanje žetona
} } }
```

`Audit_log_records_every_trading_operation` host test trdi da sled sproži za open, order, zaščito, close.

## Robni primeri (validirano proti temu kako realne copy/MAM platforme fail)

Slippage & latenca, simbol suffix/neujemanje, podvojene trgovine ob reconnect, neujemanje vzvoda & marža-varna dimenzija, razlike v valuti depozita/velikosti pogodbe, min/max lot & zaokroževanje, zavrnjena naročila, filtri smeri, čiščenje sirot po disconnect — vse pokrito zgoraj. Viri:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Napredno zrcaljenje pokritost (delno zaprtje · čakajoča naročila · SL-trailing)

Host zrcali več kot market open/close. Vsako vedenje = per-destination opt-in zastavica na `CopyDestination` (`MirrorPartialClose` privzeto on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` privzeto off), zavarovano z namenu razkrivajočimi metodami, jsonb-vztrajno (migracija `CopyAdvancedMirroringAndNodeAffinity`).

| Vedenje | Deterministični test (`CopyEngineHostTests`) | Live test |
|---------|--------------------------------------------|-----------|
| Delno zaprtje → sorazmerni rez | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 zapre 60%) + onemogočena pot | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Čakajoč limit/stop postavljen | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + onemogočena pot | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Preklic čakajočega | `Source_pending_cancel_cancels_the_slave_pending` | (isti live test — prekliče na glavnem, trdi da se podrejeni prekliče) ✅ |
| Napolnjen čakajoč brez dvojnega odprtja | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Vir SL premik znova-ustrezbi | `Source_stop_loss_move_re_amends_the_copy` | — |
| Auditni dogodki sproženi | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Vsi live testi zgoraj **preverjeno zeleni proti realnim cTrader demo računom** (1:1, 1:many, reverse, cross-cID, delno zaprtje, čakajoč+preklic, trailing).

Žične dodatki v `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing zastavica na `AmendPositionSltpAsync`, order/pending polja na `ExecutionEvent`, `LoadSpotPriceAsync` (spot naročnina → bid/ask, uporabljeno s strani live čakajoč/trailing testov za postavitev počivajočih naročil stran od trga), `StopLoss`/`TrailingStopLoss` na `OpenPositionSnapshot` (trailing stanje opazovano prek uskladitve). Ciljeve kopije ostanejo označene z **source position id** (čakajoče kopije z source **order id**) torej reconnect uskladitev ostane id-based, nikoli ne podvoji trade.

**cTrader event gotcha (preverjeno live):** počivajočega čakajočega `ORDER_ACCEPTED`/`ORDER_CANCELLED` execution dogodek nosi **ne-open `Position` placeholder** plus `Order`. Stream mora klasificirati kot *order* dogodek **preden** branch pozicije (pogojeno na pozicija ni `OPEN`), sicer postavitev čakajočega zmotno bere kot pozicija close. `SourceExecutionsAsync` to stori; manjkanje tega tiho izpusti vse čakajoče zrcaljenje.

## Rotacija žetona + afiniteta vozlišča

- **Rotacija v tekoče gostitelje.** `CopyEngineSupervisor` zabeleži podpis žetona na vsakem tekočem gostitelju in, vsako usklajevalno iteracijo, obnovi načrt iz DB (sveže zavrti s strani `OpenApiTokenRefreshService`). Spremenjen podpis restarta gostitelja (`CopyHostTokenRotated`, 1062); nov gostiteljev `ResyncAsync` obnovi stanje brez podvajanja trades. Prisilna rotacija sred zagona prek `IOpenApiTokenClient.RefreshAsync` za verificiranje live gostitelj nadaljuje kopiranje.
- **Afiniteta vozlišča (brez dvojnega copy-ja).** Oba Web local node in `CopyAgent` worker run a supervisor. Each running profile claimed by exactly one node (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name). Supervisor hosts only profiles it owns; stop/pause releases claim. Pokritost:
  - Domena (enote): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integracija (real Postgres, Testcontainers)**: `CopyNodeAffinityTests` poganja supervisorjev realni `ClaimUnassignedProfilesAsync` — trdi da prvo vozlišče zahteva vse 3 tekoče profile, drugo zahteva **0** (brez dvojnega gostitelja), pause→restart sprosti claim za drugo.
  - Detekcija rotacije (`TokenRotationSignatureTests`): supervisorjev `TokenSignature` se spremeni ko vir ali cilj žeton zavrti, stabilen sicer (tekoči gostitelj restarta samo pri realni rotaciji).

### Enojne-rabe refresh žetonov (pomembno)

cTrader **refresh žetoni so enojne-rabe** — vsako osveževanje vrne *nov* refresh žeton, razveljavi starega. Live fikstura osvežuje ob startu, vztraja zavrnjeni žeton v `secrets/openapi-tokens.local.json`. Posledice:
- Če osveževanje osveži vendar **ne more vztrajati** novega žetona (npr. samo-branje mount), cached žeton mrtev, naslednji zagon fail `ACCESS_DENIED`. Regeneriraj z headless onboarding:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` pogoltne napake pisanja torej samo-branje predpomnilnik ne crashira zagon, toda **live** znotraj-gručni suite še vedno potrebuje **spremenljiv** predpomnilnik (K8s Job kopira Secret v emptyDir — glej deployment doc).

## Zagon suite v Kubernetes gruči

Cela suite teče znotraj-gručno proti Helm-nameščeni aplikaciji, torej se regresija ujame znotraj-gručno enako kot lokalno. Glej [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind gruča, deterministični suite (brez skrivnosti)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` zgradi runner sliko; Helm `tests-job.yaml` (zastaviciran `tests.enabled=false`) zažene proti znotraj-gručni Postgres + Web. **Privzeto = deterministični copy suite** (brez skrivnosti, brez rotirajočih žetonov). Za live suite, nastavite `tests.copySecret` na Secret ki drži gitignored `openapi-*.local.json`; init-container kopira v **spremenljiv** emptyDir na `/app/secrets` (zahtevano — enojne-rabe refresh žetoni morajo biti vztrajni). Copy testi potrebujejo samo Web + Postgres + predpomnilnik žetonov — ni privilegiranih agentskih vozlišč. Script trdi da Job exit 0 in dnevniki vsebujejo `Passed!`.

**Preverjeno tu (Docker, brez gruče):** testna slika zažene deterministični suite (`101 passed`) in, s spreminjljivim `secrets/` mountom, polni **live** suite (`8 passed`) — natančna Pot opravila minus Kubernetes. `kind`/`kubectl`/`helm` niso na voljo v autorskem okolju, torej polni `k8s-e2e.sh` gručni zagon je korak ki se ne izvede tukaj.

## Live matrika možnosti + chaos (LiveCopyMatrix / LiveCopyChaos)

Dve podatkovno-gonjeni live zbirki gradita na `LiveCopyScenario` / `LiveCopyFixture`, live sorodnik determinističnemu DST stress suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` matrika možnosti: en realen master open na vrstico proti demo računom, vsak z drugače konfiguriranim ciljem, trdi golden izid. Vrstice: `one_to_one`, `half_multiplier`, `reverse` (nasprotna stran), `manage_only` (odpira nič), `trading_hours_closed` (okno izključuje zdaj → ni copyja), `source_label_block` (filter oznake → ni copyja), `lot_sanity_block` (zgornja meja → ni copyja).
- **`LiveCopyChaos`** — copy engine proti sovražnemu začetku: master že drži pozicijo preden host začne, torej konvergenca lahko pride samo iz startup resync (`Sync-Open-on-Start`), plus negativni primer z izključenim. Deterministični socket-flap / rotacija žetona / zavrnitev chaos pokrit izčrpno s DST suite; to trdi live resync pot.

Oba **preskočita gladko** brez poverilnic, poročata **Inconclusive** na zaprtem trgu, vsaka vrstica očisti pozicije ki jih odpre (samo demo računi).

**Robusnost fiksture:** `LiveCopyFixture` zdaj degradira v čist preskok ko predpomnljeni žetoni ne morejo osvežiti (cTrader refresh žetoni so enojne-rabe — zastarelm predpomnilnik ali offline zagon ne faulta celotne live zbirke; znova zaženi OAuth onboarding da osveži predpomnilnik).

## Zagon live + gručne suite same (NI Excuses — vse je self-serviceable)

Spodaj je vse na voljo v tem okolju. **Ne trdite** "poverilnice niso na voljo", "žeton zastarelm", ali "kind/helm ne nameščen" — nastavite jih in jih zaženite.

### 1. Pridobite svež Open API žeton (headless OAuth, brez interakcije brskalnika)

cTrader refresh žetoni so enojne-rabe, torej predpomnljeni žeton BO šel zastarelm. Ponovno vzpostavite ga iz shranjenih cID uporabniškega imena/gesla (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, ali enotna `secrets/dev-credentials.local.json`). Onboarding test poganja **headless Edge** OAuth prek Playwright, piše `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; avtorizira vsak cID, predpomni sveže žetone. Znova zaženite kadarkoli live suite poroča da fikstura ni na voljo zaradi napake osveževanja.

### 2. Zaženi live copy suite (realni cTrader demo računi)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # jedrno zrcaljenje (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matrika možnosti (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Postavi + očisti realna DEMO naročila (nikoli live računi), poročaj **Inconclusive** na zaprtem trgu. Preverjeno zeleno end to end.

### 3. Bootstrap žetonov iz running app volume (alternativa)

Če je app tekel + cID povezan v-app, izvlecite app najnovejši refresh žeton naravnost iz `app-pg-data` Postgres volume namesto ponovne avtorizacije — glej `LiveTokenBootstrapTests`, nastavite `CMIND_VOLUME_CONN`.

### 4. Kubernetes gruča E2E

`kind`, `helm`, Docker na voljo (namestite kind/helm prek `go install`/release binarov ali `choco install kind kubernetes-helm` če ne na PATH). Enokratna skripta zgradi+nanese slike, namešči chart, zažene znotraj-gručni test Job, trdi exit 0:

```bash
scripts/k8s-e2e.sh                                 # deterministični copy suite (brez skrivnosti)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live znotraj-gručni
```

Glej [../deployment/kubernetes.md](../deployment/kubernetes.md).
