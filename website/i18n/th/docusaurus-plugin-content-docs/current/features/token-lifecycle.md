---
description: "cTrader's Open API allows one valid access token per cTrader ID (cID) at a time. The moment a new token is issued — a scheduled refresh, or a…"
---

# Open API token lifecycle

cTrader's Open API allows **one valid access token per cTrader ID (cID) ที่ time** moment
new token issued — scheduled refresh หรือ re-authorization เมื่อ user links another
account on same cID — previous access token invalidated copy engine running on
remote node holding now-dead token ดังนั้น new token must reach มัน ไม่มี dropping live
connection

## Model

- **`OpenApiAuthorization`** aggregate ที่ holds cID ของ encrypted access + refresh
  tokens unique index on `(UserId, CtidUserId)` enforces **exactly one authorization per cID
  per user**
- **`TokenVersion`** — monotonic counter bumped ทุก time token rotates (`Refresh()`
  which also covers re-auth path เมื่อ another account linked on same cID) มัน version
  marker สำหรับ single-valid-token rule และ คือ what running host uses ไป detect
  change even ถ้า two token strings happen ไป collide
- tokens encrypted at rest ผ่าน `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`) พวกเขา never logged หรือ stored ใน plaintext

## Propagation (graceful in-place swap)

1. token rotates → new token + bumped `TokenVersion` persisted
2. `CopyEngineSupervisor` on hosting node re-reads plan ทุก reconcile cycle และ
   computes **token signature** (access tokens + versions) change means rotation
3. แทน tearing down host และ restarting (which would drop master ของ execution
   stream) supervisor **pushes new token ไป running host**
4. host re-authenticates affected account **on existing socket**
   (`ProtoOAAccountAuthReq` again) ผ่าน `SwapAccessTokenAsync` จากนั้น does light reconcile old
   token dies; copy stream never stops

นี้ คือ what makes cross-cID case safe: user adding second account จาก same cID
mid-run invalidates old token และ running copy profile keeps going on new one

## Refresh

`OpenApiTokenRefreshService` (background) proactively refreshes authorizations ก่อน expiry;
`OpenApiAuthorization.IsExpiring(threshold, now)` gates มัน cTrader rotates **refresh** token
on ทุก refresh ดังนั้น new refresh token persisted immediately; read-only cache ที่ can't
persist would self-invalidate (relevant ไป in-cluster test Job ซึ่ง mounts writable copy
ของ secret)

### Failure escalation

failed refresh ไม่ silent `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
records `RefreshFailedAt` increments `ConsecutiveRefreshFailures` และ always raises
`AccessTokenRefreshFailed` (warning) เมื่อ token now ใน `App:OpenApi:TokenRefreshCriticalWindow`
(default 6h) ของ expiry และ refresh still failing มัน escalates **once** ด้วย
`AccessTokenRefreshCritical` domain event + `Critical` log ดังนั้น owner สามารถ re-authorize ก่อน
copy/prop-firm operations lose token failure counter และ escalation latch reset on next
successful `Refresh` service keeps retrying ทุก `TokenRefreshInterval` ดังนั้น provider/maintenance
outage self-heals เมื่อ refresh endpoint returns

## Invalidation alert & auto-recovery (M1)

partial/again-authorization on cID invalidates token running copy host still holds เมื่อ
trading call rejects ด้วย `OpenApiErrorKind.TokenInvalid` host raises distinct
**`CopyTokenInvalidated`** alert (log 1078) — ไม่ generic failure — ดังนั้น notification channel
knows token needs attention recovery automatic: supervisor re-reads authorization ทุก cycle และ
เมื่อ refreshed token changes token signature มัน pushes มัน ไป running host สำหรับ **in-place
swap** — copying resumes ด้วย ไม่มี manual re-add `NotLinkable` profile (token/auth temporarily
unresolvable) likewise re-evaluated ทุก supervisor cycle และ hosted moment plan ของมัน builds again

## Host liveness watchdog (M2)

supervisor watches ทุก hosted profile ของ run task ถ้า host exits หรือ faults ขณะที่ profile ของมัน
still assigned ไป node นี้ watchdog cancels และ **restarts** มัน next cycle (log
`CopyHostRestarted`) ดังนั้น wedged host self-heals แทน needing manual restart — และ one profile ของ
failure never stalls others (per-profile isolation)

## Tests

- **Unit** — `TokenVersion` bumps on `Refresh`; host performs in-place swap ไม่มี restart;
  cross-cID invalidation swaps source และ destination tokens; **an invalidated destination token raises
  `CopyTokenInvalidated` และ auto-recovers on next token push** (M1); watchdog `IsHostDead`
  decision restarts completed/faulted host และ leaves reassigned profile alone (M2)
- **Integration** — `TokenVersion` persists + increments ผ่าน EF on real Postgres; token
  signature changes on version bump even ถ้า string unchanged
