---
description: "cTrader's Open API cho phép một valid access token per cTrader ID (cID) tại một thời điểm. The moment a new token is issued — a scheduled refresh, or a…"
---

# Open API token lifecycle

cTrader's Open API cho phép **một valid access token per cTrader ID (cID)** tại một thời điểm. The moment
a new token is issued — a scheduled refresh, hoặc a re-authorization when user links another
account on same cID — previous access token bị invalidated. A copy engine running on a
remote node đang holding that now-dead token, vì vậy new token phải reach nó without dropping
live connection.

## Model

- **`OpenApiAuthorization`** là aggregate holding a cID's encrypted access + refresh
  tokens. A unique index on `(UserId, CtidUserId)` enforces **exactly one authorization per cID
  per user**.
- **`TokenVersion`** — a monotonic counter bumped mỗi khi token rotates (`Refresh()`,
  which also covers re-auth path when another account linked on same cID). Nó là
  version marker for single-valid-token rule và là what a running host uses to detect a
  change even if two token strings happen to collide.
- Tokens are encrypted at rest via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Chúng không bao giờ logged hoặc stored in plaintext.

## Propagation (graceful in-place swap)

1. Token rotates → new token + bumped `TokenVersion` persisted.
2. `CopyEngineSupervisor` on hosting node re-reads plan each reconcile cycle và
   computes a **token signature** (access tokens + versions). A change means a rotation.
3. Instead of tearing down host và restarting (which would drop master's execution
   stream), supervisor **pushes new token to running host**.
4. Host re-authenticates affected account **on existing socket**
   (`ProtoOAAccountAuthReq` again) via `SwapAccessTokenAsync`, rồi does a light reconcile. Old token dies; copy stream never stops.

Đây là what makes cross-cID case safe: a user adding second account from same cID
mid-run invalidates old token, và running copy profile keeps going on new one.

## Refresh

`OpenApiTokenRefreshService` (background) proactively refreshes authorizations before expiry;
`OpenApiAuthorization.IsExpiring(threshold, now)` gates it. cTrader rotates **refresh** token
on every refresh, vì vậy new refresh token persisted immediately; a read-only cache that can't
persist would self-invalidate (relevant to in-cluster test Job, which mounts writable copy
of secret).

### Failure escalation

A failed refresh not silent. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
records `RefreshFailedAt`, increments `ConsecutiveRefreshFailures`, và always raises
`AccessTokenRefreshFailed` (warning). When token now within `App:OpenApi:TokenRefreshCriticalWindow`
(default 6h) of expiry và refresh still failing, it escalates **once** với an
`AccessTokenRefreshCritical` domain event + `Critical` log vì vậy owner có thể re-authorize before
copy/prop-firm operations lose token. Failure counter và escalation latch reset on next
successful `Refresh`. Service keeps retrying every `TokenRefreshInterval`, vì vậy a provider/maintenance
outage self-heals when refresh endpoint returns.

## Invalidation alert & auto-recovery (M1)

A partial/again-authorization on a cID invalidates token a running copy host still holds. When a
trading call rejects với `OpenApiErrorKind.TokenInvalid`, host raises distinct
**`CopyTokenInvalidated`** alert (log 1078) — not a generic failure — vì vậy notification channel biết a
token needs attention. Recovery automatic: supervisor re-reads authorization each cycle và,
when refreshed token changes token signature, pushes it into running host cho an **in-place
swap** — copying resumes với no manual re-add. A `NotLinkable` profile (token/auth temporarily
unresolvable) likewise re-evaluated every supervisor cycle và hosted the moment its plan builds again.

## Host liveness watchdog (M2)

Supervisor watches each hosted profile's run task. If a host exits hoặc faults while its profile is
still assigned to this node, watchdog cancels và **restarts** it next cycle (log
`CopyHostRestarted`), vì vậy a wedged host self-heals instead of needing a manual restart — và one profile's
failure never stalls others (per-profile isolation).

## Tests

- **Unit** — `TokenVersion` bumps on `Refresh`; host performs in-place swap without restart;
  cross-cID invalidation swaps source và destination tokens; **an invalidated destination token raises
  `CopyTokenInvalidated` và auto-recovers on next token push** (M1); watchdog `IsHostDead`
  decision restarts a completed/faulted host và leaves a reassigned profile alone (M2).
- **Integration** — `TokenVersion` persists + increments through EF on real Postgres; token
  signature changes on a version bump even if string unchanged.
