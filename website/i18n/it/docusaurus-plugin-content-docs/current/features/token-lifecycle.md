---
description: "L'Open API di cTrader consente un solo valid access token per cTrader ID (cID) alla volta. Il momento in cui un nuovo token è emesso — un refresh scheduled, o una ri-authorization — il previous access token è invalidato."
---

# Open API token lifecycle

L'Open API di cTrader consente **un solo valid access token per cTrader ID (cID) alla volta**. Il momento
in cui un nuovo token è emesso — un refresh scheduled, o una ri-authorization quando l'utente collega un altro
account sullo stesso cID — il previous access token è invalidato. Un copy engine in esecuzione su un
nodo remoto sta trattenendo quel now-dead token, quindi il nuovo token deve raggiungerlo senza far cadere la
connessione live.

## Modello

- **`OpenApiAuthorization`** è l'aggregate che trattiene i token encrypted access + refresh di un cID. Un
  unique index su `(UserId, CtidUserId)` applica **esattamente una authorization per cID per user**.
- **`TokenVersion`** — un contatore monotono incrementato ogni volta che il token ruota (`Refresh()`,
  che copre anche il percorso re-auth quando un altro account è collegato sullo stesso cID). È il version
  marker per la regola single-valid-token ed è ciò che un host in esecuzione usa per rilevare un
  cambiamento anche se due stringhe di token dovessero collidere.
- I token sono crittografati at rest via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Non sono mai loggati o memorizzati in plaintext.

## Propagazione (graceful in-place swap)

1. Un token ruota → il nuovo token + `TokenVersion` bumped sono persistiti.
2. Il `CopyEngineSupervisor` sul nodo hosting re-legge il piano ogni ciclo di reconcile e
   computa una **token signature** (access tokens + versions). Un cambiamento significa una rotazione.
3. Invece di abbattere l'host e restartare (che farebbe cadere l'execution stream del master), il
   supervisor **spinge il nuovo token all'host in esecuzione**.
4. L'host ri-autentica l'account affected **sul socket esistente**
   (`ProtoOAAccountAuthReq` di nuovo) via `SwapAccessTokenAsync`, poi fa un light reconcile. Il
   vecchio token muore; lo stream copy non si ferma mai.

Questo è ciò che rende sicuro il caso cross-cID: un utente che aggiunge un secondo account dello stesso cID
mid-run invalida il vecchio token, e il copy profile in esecuzione continua sulla nuovo.

## Refresh

`OpenApiTokenRefreshService` (background) refresha proattivamente le authorization prima della scadenza;
`OpenApiAuthorization.IsExpiring(threshold, now)` la gating. cTrader ruota il **refresh** token
ogni refresh, quindi il nuovo refresh token è persistito immediatamente; una read-only cache che non può
persistere si auto-invaliderrebbe (rilevante per l'in-cluster test Job, che mounta una copia scrivibile
del secret).

### Failure escalation

Un refresh fallito non è silenzioso. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
registra `RefreshFailedAt`, incrementa `ConsecutiveRefreshFailures`, e solleva sempre
`AccessTokenRefreshFailed` (warning). Quando il token è ora entro `App:OpenApi:TokenRefreshCriticalWindow`
(default 6h) dalla scadenza e il refresh sta ancora fallendo, escala **una volta** con un
evento domain `AccessTokenRefreshCritical` + log `Critical` così l'owner può ri-authorizzare prima che
le operazioni copy/prop-firm perdano il token. Il contatore dei fallimenti e il latch di escalation
resettano al prossimo `Refresh` riuscito. Il servizio continua a ritentare ogni `TokenRefreshInterval`,
quindi un outage provider/maintenance si self-heals quando l'endpoint refresh ritorna.

## Invalidation alert & auto-recovery (M1)

Una authorization parziale/again su un cID invalida il token che un copy host in esecuzione ancora
trattiene. Quando una chiamata trading rifiuta con `OpenApiErrorKind.TokenInvalid`, l'host solleva un
distinto **`CopyTokenInvalidated`** alert (log 1078) — non un failure generico — così il canale di
notifica sa che un token necessita attenzione. Il recovery è automatico: il supervisor re-legge
l'authorization ogni ciclo e, quando il token refreshato cambia la token signature, lo spinge nell'host
in esecuzione per uno **in-place swap** — il copying riprende senza re-add manuale. Un profilo
`NotLinkable` (token/auth temporaneamente unresolvable) è similmente re-evaluato ogni ciclo supervisor
e hosted nel momento in cui il suo piano si builda di nuovo.

## Host liveness watchdog (M2)

Il supervisor osserva il run task di ogni profilo hosted. Se un host esce o va in fault mentre il suo
profilo è ancora assegnato a questo nodo, il watchdog cancella e **restart** esso next cycle (log
`CopyHostRestarted`), così un host incastrato si self-heala invece di necessitare un restart manuale —
e il fallimento di un profilo non ferma mai gli altri (isolamento per-profile).

## Test

- **Unit** — `TokenVersion` bumps on `Refresh`; l'host esegue uno in-place swap senza restart;
  cross-cID invalidation scambia source e destination token; **un invalidated destination token solleva
  `CopyTokenInvalidated` e auto-recupera sul next token push** (M1); la decisione watchdog `IsHostDead`
  restart un host completed/faulted e lascia un profilo reassigned da solo (M2).
- **Integration** — `TokenVersion` persiste + incrementa attraverso EF su Postgres reale; la token
  signature cambia su un version bump anche se la stringa è invariata.
