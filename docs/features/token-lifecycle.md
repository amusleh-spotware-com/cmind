# Open API token lifecycle

cTrader's Open API allows **one valid access token per cTrader ID (cID) at a time**. The moment
a new token is issued — a scheduled refresh, or a re-authorization when the user links another
account on the same cID — the previous access token is invalidated. A copy engine running on a
remote node is holding that now-dead token, so the new token must reach it without dropping the
live connection.

## Model

- **`OpenApiAuthorization`** is the aggregate that holds a cID's encrypted access + refresh
  tokens. A unique index on `(UserId, CtidUserId)` enforces **exactly one authorization per cID
  per user**.
- **`TokenVersion`** — a monotonic counter bumped every time the token rotates (`Refresh()`,
  which also covers the re-auth path when another account is linked on the same cID). It is the
  version marker for the single-valid-token rule and is what a running host uses to detect a
  change even if two token strings happen to collide.
- Tokens are encrypted at rest via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). They are never logged or stored in plaintext.

## Propagation (graceful in-place swap)

1. A token rotates → the new token + bumped `TokenVersion` are persisted.
2. The `CopyEngineSupervisor` on the hosting node re-reads the plan each reconcile cycle and
   computes a **token signature** (access tokens + versions). A change means a rotation.
3. Instead of tearing down the host and restarting (which would drop the master's execution
   stream), the supervisor **pushes the new token to the running host**.
4. The host re-authenticates the affected account **on the existing socket**
   (`ProtoOAAccountAuthReq` again) via `SwapAccessTokenAsync`, then does a light reconcile. The
   old token dies; the copy stream never stops.

This is what makes the cross-cID case safe: a user adding a second account from the same cID
mid-run invalidates the old token, and the running copy profile keeps going on the new one.

## Refresh

`OpenApiTokenRefreshService` (background) proactively refreshes authorizations before expiry;
`OpenApiAuthorization.IsExpiring(threshold, now)` gates it. cTrader rotates the **refresh** token
on every refresh, so the new refresh token is persisted immediately; a read-only cache that can't
persist would self-invalidate (relevant to the in-cluster test Job, which mounts a writable copy
of the secret).

## Tests

- **Unit** — `TokenVersion` bumps on `Refresh`; host performs an in-place swap without restart;
  cross-cID invalidation swaps source and destination tokens.
- **Integration** — `TokenVersion` persists + increments through EF on real Postgres; the token
  signature changes on a version bump even if the string is unchanged.
