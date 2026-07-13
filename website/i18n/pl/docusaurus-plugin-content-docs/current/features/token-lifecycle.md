---
description: "cTrader's Open API allows jeden valid access token per cTrader ID (cID) na raz. Moment nowy token jest issued — scheduled refresh, lub…"
---

# Open API token lifecycle

cTrader's Open API allows **jeden valid access token per cTrader ID (cID) na raz**. Moment
nowy token jest issued — scheduled refresh, lub re-authorization gdy user links inny
account na same cID — poprzedni access token to invalidated. Copy engine running na
remote node holds ten teraz-dead token, więc nowy token musi reach to bez dropping live
connection.

## Model

- **`OpenApiAuthorization`** to aggregate które holds cID's encrypted access + refresh
  tokens. Unique index na `(UserId, CtidUserId)` enforces **dokładnie jeden authorization per cID
  per user**.
- **`TokenVersion`** — monotonic counter bumped każdy raz token rotates (`Refresh()`,
  które także covers re-auth path gdy inny account to linked na same cID). To
  version marker dla single-valid-token rule i to co running host uses do detect change
  nawet jeśli dwa token strings happen do collide.
- Tokens to encrypted na rest via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Nigdy nie logged lub stored w plaintext.

## Propagation (graceful in-place swap)

1. Token rotates → nowy token + bumped `TokenVersion` to persisted.
2. `CopyEngineSupervisor` na hosting node re-reads plan każdy reconcile cycle i
   computes **token signature** (access tokens + versions). Change means rotation.
3. Zamiast tearing down host i restarting (które by drop master's execution
   stream), supervisor **pushes nowy token do running host**.
4. Host re-authenticates affected account **na existing socket**
   (`ProtoOAAccountAuthReq` znowu) via `SwapAccessTokenAsync`, wtedy light reconcile. Old
   token dies; copy stream nigdy nie stops.

To co makes cross-cID case safe: user adding drugi account z same cID
mid-run invalidates old token, i running copy profile keeps going na nowy.

## Refresh

`OpenApiTokenRefreshService` (background) proactively refreshes authorizations przed expiry;
`OpenApiAuthorization.IsExpiring(threshold, now)` gates to. cTrader rotates **refresh** token
na każdy refresh, więc nowy refresh token to persisted immediately; read-only cache które nie może
persist by self-invalidate (relevant do in-cluster test Job, które mounts writable copy
z secret).

### Failure escalation

Failed refresh to nie silent. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
records `RefreshFailedAt`, increments `ConsecutiveRefreshFailures`, i zawsze raises
`AccessTokenRefreshFailed` (warning). Gdy token to teraz within `App:OpenApi:TokenRefreshCriticalWindow`
(default 6h) z expiry i refresh to wciąż failing, to escalates **once** z
`AccessTokenRefreshCritical` domain event + `Critical` log więc owner może re-authorize przed
copy/prop-firm operations lose token. Failure counter i escalation latch reset na next
successful `Refresh`. Service keeps retrying każdy `TokenRefreshInterval`, więc provider/maintenance
outage self-heals gdy refresh endpoint returns.

## Invalidation alert & auto-recovery (M1)

Partial/again-authorization na cID invalidates token running copy host wciąż holds. Gdy
trading call rejects z `OpenApiErrorKind.TokenInvalid`, host raises distinct
**`CopyTokenInvalidated`** alert (log 1078) — nie generic failure — więc notification channel knows
token needs attention. Recovery to automatic: supervisor re-reads authorization każdy cycle i,
gdy refreshed token changes token signature, pushes to do running host dla **in-place
swap** — copying resumes z no manual re-add. `NotLinkable` profile (token/auth temporarily
unresolvable) jest likewise re-evaluated każdy supervisor cycle i hosted moment jego plan builds znowu.

## Host liveness watchdog (M2)

Supervisor watches każdy hosted profile's run task. Jeśli host exits lub faults gdy jego profile to
wciąż assigned do ten node, watchdog cancels i **restarts** to next cycle (log
`CopyHostRestarted`), więc wedged host self-heals zamiast needing manual restart — i jeden profile's
failure nigdy nie stalls others (per-profile isolation).

## Testy

- **Unit** — `TokenVersion` bumps na `Refresh`; host performs in-place swap bez restart;
  cross-cID invalidation swaps source i destination tokens; **invalidated destination token raises
  `CopyTokenInvalidated` i auto-recovers na next token push** (M1); watchdog `IsHostDead`
  decision restarts completed/faulted host i leaves reassigned profile alone (M2).
- **Integracja** — `TokenVersion` persists + increments przez EF na real Postgres; token
  signature changes na version bump nawet jeśli string to unchanged.
