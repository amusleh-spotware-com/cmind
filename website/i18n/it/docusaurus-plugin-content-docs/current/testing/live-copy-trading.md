---
description: "Suite di test copy-trading completa e riproducibile. Due livelli:"
---

# Suite di test copy-trading (deterministica + live)

Suite di test copy-trading completa e riproducibile. Due livelli:

1. **Test deterministici** (xUnit, senza rete) — logica matematica e di copy engine. Veloci, CI, senza secrets. Coprono ogni modalita di gestione del denaro, ogni filtro/opzione, la resilienza del motore.
2. **Test E2E live** (account demo cTrader reali) — `CopyEngineHost` produce e copia ordini reali tra account reali. Completamente automatizzati, rieseguibili come test unit: leggono le credenziali cached da file locali gitignored, ricaricano autonomamente l'access token, saltano in modo pulito quando i secrets sono assenti (CI resta verde).

Non gira mai su account live con fondi reali — ogni account e **demo**, ogni test live chiude le posizioni che ha aperto.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — ogni modalita di sizing + arrotondamento + lotto min/max
  CopyDecisionEngineTests.cs     — direzione/reverse/slippage/delay/filtro simbolo/size-zero
  CopyEngineHostTests.cs         — logica copy dell'host contro sessione fake in-memory
  FakeTradingSession.cs          — IOpenApiTradingSession deterministica (registra ordini/chiude/emenda)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fault fatale (resilienza)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — carica i secrets gitignored, salva i token ricaricati
  LiveTokenBootstrapTests.cs     — one-shot: decripta token dal DB dell'app nel token cache
  LiveCopyFixture.cs             — ruota l'access token, espone la lista account demo
  LiveCopyScenario.cs            — esegue uno scenario copy reale end-to-end (open → copy → verify → cleanup)
  CopyTradingLiveTests.cs        — gli scenari live (1:1, 1:many, reverse, ...)
```

## Secrets (locali, gitignored — mai committati)

Tutte le credenziali sono sotto `<repo>/secrets/` (gia in `.gitignore`). Il dev scrive **solo i primi due file**; il terzo (token) viene prodotto automaticamente dall'onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — credenziali login cID da autorizzare (uno o piu):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **scritto dall'onboarding**, multi-cID, ricaricato ogni esecuzione:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Il refresh token **non scade mai**, quindi dopo l'onboarding one-time i test live funzionano indefinitamente: ogni esecuzione scambia il refresh token di ogni cID per un fresh access token (rotazione) — nessun browser, nessun prompt.

## Onboarding one-time (completamente automatizzato — nessuna interazione dev oltre al salvataggio credenziali)

L'onboarding guida il login cTrader ID reale in browser headless da credenziali cID salvate, cattura OAuth callback su listener HTTPS locale all'indirizzo redirect registrato dall'app (`https://localhost:7080/openapi/callback`), scambia il codice per i token, carica la lista account, scrive il token cache multi-cID. Eseguire una volta per macchina (o quando si aggiunge un cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autorizza ogni cID in `openapi-cids.local.json`, scrive `openapi-tokens.local.json`. Dopo di che i test copy live non hanno bisogno di altro. (L'account cID del cTrader deve non avere 2FA/captcha sul login affinche l'automazione possa completarsi.)

**Bootstrap alternativo** (se gli account sono gia autorizzati nell'app in esecuzione): decriptare i token memorizzati direttamente dal volume Postgres dell'app invece di re-autorizzare:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Sicurezza — solo demo

I test live tradano **solo account demo**: il fixture filtra il token cache agli account con `IsLive == false` e si connette al gateway demo, quindi l'ordine non puo mai arrivare su account live/con fondi reali. Ogni posizione che un test apre viene chiusa nel cleanup.

## Eseguire

```bash
# Test copy deterministici solo (veloci, no secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Test copy live sugli account demo reali (servono i due file secrets)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Tutto
dotnet test
```

Senza i file secrets i test live stampano il motivo dello skip + passano come no-op, quindi la suite e sicura da eseguire ovunque.

## Copertura

### Gestione del denaro / sizing (deterministico — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / valuta) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scala **su** e **giu** per mismatch di balance/leverage/capacita (la "regola d'oro") · arrotondamento lot-step
· skip lotto-min vs force-to-min · cap lotto-max · limite piu stretto tra bound-vs-spec min & max · skip per
balance master zero.

### Filtri decisionali (deterministico — `CopyDecisionEngineTests`)
Whitelist / blacklist simboli / allow · LongOnly / ShortOnly · reverse inverte il lato effettivo ·
slippage oltre il limite skip + esattamente-al-limite permesso · skip per signal stale (max delay) · skip per size-zero ·
riconciliazione reconnect (dedup open-managing, close orphaned).

### Copy engine host (deterministico — `CopyEngineHostTests`, sessione in-memory)
Open replica un market order (lato / volume / label) · **reverse** inverte il lato e **scambia SL/TP** ·
**symbol mapping** risolve il simbolo di destinazione · **order-failure su uno slave continua comunque a copiare sugli altri** ·
source close chiude la copia mirror · reconnect resync chiude le posizioni orphaned.

### Resilienza della connessione (deterministico — `OpenApiConnectionTests`)
Raggiunge Connected dopo auth dell'app · connessione caduta reconnects e re-auths · errore auth fatale fault ·
exponential backoff.

### Live, account demo cTrader reali (`CopyTradingLiveTests`)
Token refresh + listing account · **1:1** copy esegue · **1:many** copy replica a ogni slave ·
**reverse** trasforma master buy in slave sell · **cross-cID** copy (master sotto un cID replica a slave sotto un altro, ciascuno autentica col proprio token). Ciascuno apre posizione reale lotto-min sul master, aspetta che il motore la replichi (matched tramite source-position-id label sul slave), asserts, chiude tutto. Mercato chiuso segnalato **Inconclusive**, non failing.

## Logging e auditability

Ogni operazione copy-trading loggata tramite eventi strutturati source-generated (`Core/Logging/LogMessages.cs`, ID evento 1043–1055), trail completo auditabile:

| Evento | Id | Significato |
|-------|----|-------------|
| CopyHostStarted | 1046 | il motore di un profilo e partito (source + destinazione count) |
| CopySourceOpen | 1047 | master ha aperto una posizione (symbol / side / lots) |
| CopyOrderPlaced | 1048 | ordine copy inviato a uno slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | una copy e stata saltata e perche (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applicato a una copia slave |
| CopyOpenFailed | 1051 | copy-open slave fallita (isolata — gli altri slave continuano) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master chiuso → copia slave chiusa |
| CopyCloseFailed | 1054 | copy-close slave fallita |
| CopyResync | 1055 | riconciliazione reconnect (source open count, orphans chiusi) |
| CopyPartialClose | 1056 | master partial close replicata — fetta proporzionale chiusa su uno slave |
| CopyScaleIn | 1057 | master scale-in replicata (opt-in) — volume aggiunto copiato a uno slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop replicata a uno slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source pending cancellato → slave pending cancellato |
| CopyTrailingApplied | 1060 | trailing stop applicato a una copia slave (opt-in) |
| CopyStopLossAmended | 1061 | una mossa SL source ri-emenda la copia slave |
| CopyHostTokenRotated | 1062 | il supervisor ha riavviato un host in esecuzione dopo rotazione del suo access token |

I log emessi come Serilog compact JSON (props strutturate: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), spediti a OTLP quando `OTEL_EXPORTER_OTLP_ENDPOINT` e impostato. **Completamente configurabile** per categoria tramite config standard — es. alzare/abbassare la verbosita del copy-engine senza toccare codice:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // l'audit trail del CopyEngineHost
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test assert che il trail scatta per open, order, protection, close.

## Edge cases (validati contro come i platform copy/MAM reali falliscono)

Slippage & latenza, symbol suffix/mismatch, duplicate trades su reconnect, leverage mismatch & margin-safe sizing, differenze deposit-currency/contract-size, min/max lot & arrotondamento, ordini rifiutati, direction filters, pulizia orphan dopo disconnect — tutti coperti sopra. Fonti:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Copertura mirroring avanzato (partial close · pending orders · SL-trailing)

L'host replica piu del market open/close. Ogni comportamento = opt-in per-destinazione su `CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` default off), guarded da metodi di intenzione, jsonb-persisted (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Comportamento | Test deterministico (`CopyEngineHostTests`) | Test live |
|---------------|-------------------------------------------|-----------|
| Partial close → fetta proporzionale | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 chiude 60%) + percorso disabilitato | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + percorso disabilitato | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (stesso test live — cancel sul master, assert che slave cancel) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Tutti i test live sopra **verificati verdi contro account demo cTrader reali** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Wire additions in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing flag su `AmendPositionSltpAsync`, campi order/pending su `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, usato dai test live pending/trailing per piazzare ordini resting fuori dal mercato), `StopLoss`/`TrailingStopLoss` su `OpenPositionSnapshot` (stato trailing della copia osservabile via reconcile). Le copie di destinazione restano etichettate da **source position id** (copie pending da source **order id**) quindi il reconnect reconcile resta id-based, mai duplica trade.

**cTrader event gotcha (verificato live):** l'execution event `ORDER_ACCEPTED`/`ORDER_CANCELLED` di un pending order resting porta **un `Position` placeholder non-OPEN** piu l'`Order`. Lo stream deve classificarlo come evento *order* **prima** del branch position (gated su position non `OPEN`), altrimenti il pending placement viene letto erroneamente come close di posizione. `SourceExecutionsAsync` fa questo; se manca, silenciosamente droppa tutto il pending mirroring.

## Rotazione token + affinity nodo

- **Rotazione negli host in esecuzione.** `CopyEngineSupervisor` registra la token signature su ogni host in esecuzione e, ogni ciclo di reconcile, ricostruisce il piano dal DB (appena ruotato da `OpenApiTokenRefreshService`). Signature cambiata riavvia l'host (`CopyHostTokenRotated`, 1062); il nuovo host fa `ResyncAsync` per ricostruire lo stato senza duplicare trade. Force rotation mid-run tramite `IOpenApiTokenClient.RefreshAsync` per verificare che l'host live continua a copiare.
- **Node affinity (no double-copy).** Sia il nodo locale Web che il worker `CopyAgent` eseguono un supervisor. Ogni profilo in esecuzione e reclamato da esattamente un nodo (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim keyed off `CopyOptions.NodeName`, default machine name). Il supervisor ospita solo i profili che possiede; stop/pause rilascia il claim. Copertura:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (Postgres reale, Testcontainers)**: `CopyNodeAffinityTests` guida il `ClaimUnassignedProfilesAsync` reale del supervisor — assert che il primo nodo reclama tutti e 3 i profili in esecuzione, il secondo reclama **0** (no double-host), pause→restart libera il claim per un altro nodo.
  - Rotation detection (`TokenRotationSignatureTests`): la `TokenSignature` del supervisor cambia quando il token source o destination ruota, stabile altrimenti (l'host in esecuzione riavvia solo su rotazione reale).

### Token refresh single-use (importante)

I refresh token cTrader **sono single-use** — ogni refresh restituisce un *nuovo* refresh token, invalida il vecchio. Il fixture live refresha all'avvio, persiste il token ruotato in `secrets/openapi-tokens.local.json`. Conseguenze:
- Se il refresh riesce ma **non puo persistere** il nuovo token (es. mount read-only), il token cached muore, prossima esecuzione fallisce `ACCESS_DENIED`. Rigenera con onboarding headless:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` ingoia i fallimenti di scrittura quindi il cache read-only non fa crashare l'esecuzione, ma la suite **live** in-cluster ha ancora bisogno di un **cache scrivibile** (il Job K8s copia Secret in emptyDir — vedere deployment doc).

## Eseguire la suite in un cluster Kubernetes

L'intera suite gira in-cluster contro l'app deployata su Helm, quindi la regressione viene catturata in-cluster come in locale. Vedere [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, suite deterministica (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` costruisce l'immagine runner; Helm `tests-job.yaml` (gated `tests.enabled=false`) la esegue contro Postgres e Web in-cluster. **Default = suite deterministica copy** (no secrets, no token in rotazione). Per la suite live, impostare `tests.copySecret` al Secret che contiene i `openapi-*.local.json` gitignored; l'init-container lo copia in **scrivibile** emptyDir su `/app/secrets` (richiesto — i refresh token single-use devono essere persistibili). I test copy hanno bisogno solo di Web + Postgres + token cache — nessun agent nodo privilegiato. Lo script assert che il Job exits 0 e i log contengono `Passed!`.

**Verificato qui (Docker, no cluster):** l'immagine test esegue la suite deterministica (`101 passed`) e, con `secrets/` mount scrivibile, la suite **live** completa (`8 passed`) — percorso Job esatto senza Kubernetes. `kind`/`kubectl`/`helm` non disponibili nell'ambiente di authoring, quindi l'esecuzione cluster completa con `k8s-e2e.sh` e l'unico step non eseguito qui.

## Matrice opzioni live + chaos (LiveCopyMatrix / LiveCopyChaos)

Due suite data-driven live costruite su `LiveCopyScenario` / `LiveCopyFixture`, contropartita live alla suite stress deterministica DST:

- **`LiveCopyMatrix`** — matrice opzioni `[Theory]`/`[MemberData]`: un master open reale per riga contro account demo, ciascuno con destinazione configurata diversamente, assert golden outcome. Righe: `one_to_one`, `half_multiplier`, `reverse` (lato opposto), `manage_only` (non apre nulla), `trading_hours_closed` (finestra esclude ora → no copy), `source_label_block` (filtro label → no copy), `lot_sanity_block` (tetto → no copy).
- **`LiveCopyChaos`** — copy engine contro avvio ostile: il master detiene gia una posizione prima che l'host parta, quindi la convergenza puo venire solo dallo start-up resync (`Sync-Open-on-Start`), piu il caso negativo con toggle off. Socket-flap / token-rotation / rejection chaos deterministici coperti esaustivamente dalla suite DST; questa assert il percorso resync live.

Entrambi **saltano in modo pulito** senza credenziali, riportano **Inconclusive** su mercato chiuso, ogni riga fa cleanup delle posizioni che ha aperto (solo account demo).

**Robustezza del fixture:** `LiveCopyFixture` ora degrada a skip pulito quando i token cached non possono ricaricarsi (i refresh token cTrader sono single-use — cache stale o esecuzione offline non fanno piu fallire l'intera collezione live; rieseguire OAuth onboarding per ricaricare cache).

## Eseguire le suite live + cluster da soli (NESSUNA scusa — tutto e self-service)

Tutto sotto e disponibile in questo ambiente. Non claimare "credenziali non disponibili", "token stale", o "kind/helm non installati" — configurali ed eseguile.

### 1. Ottieni un fresh Open API token (OAuth headless, nessuna interazione browser)

I refresh token cTrader sono single-use, quindi il token cached DIVENTERA stale. Ri-crealo autonomamente da username/password cID salvati (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, o unificato `secrets/dev-credentials.local.json`). Il test di onboarding guida l'OAuth Edge headless tramite Playwright, scrive `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; autorizza ogni cID, cache i fresh token. Rieseguire ogni volta che la suite live riporta fixture unavailable per refresh failure.

### 2. Eseguire le suite copy live (account demo cTrader reali)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring core (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matrice opzioni (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Piazza + cleanup ordini DEMO reali (mai account live), riporta **Inconclusive** su mercato chiuso. Verificato verde end-to-end.

### 3. Bootstrap token da un volume app in esecuzione (alternativo)

Se l'app e in esecuzione + cID collegato in-app, estrai l'ultimo refresh token dell'app direttamente dal volume Postgres `app-pg-data` invece di re-autorizzare — vedere `LiveTokenBootstrapTests`, impostare `CMIND_VOLUME_CONN`.

### 4. Kubernetes cluster E2E

`kind`, `helm`, Docker disponibili (installare kind/helm tramite `go install`/release binaries o `choco install kind kubernetes-helm` se non sul PATH). Script one-shot costruisce+e carica immagini, fa deploy della chart, esegue il Job test in-cluster, assert exit 0:

```bash
scripts/k8s-e2e.sh                                 # suite deterministica copy (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Vedere [../deployment/kubernetes.md](../deployment/kubernetes.md).
