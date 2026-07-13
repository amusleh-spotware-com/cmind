---
title: 0006 — Copy hosting is coordinated by an atomic DB lease
description: Why copy profiles are claimed via an atomic Postgres lease instead of a dedicated coordinator, and how that prevents double-copying.
---

# 0006 — Copy hosting is coordinated by an atomic DB lease

## Context

A running copy profile must be hosted by **exactly one** node — two hosts on the same profile means
every source trade is mirrored twice (real money lost). Nodes come and go (scaling, crashes, rolling
updates), and we don't want a separate coordinator service to run and keep alive.

## Decision

Each `CopyEngineSupervisor` claims profiles with an **atomic DB lease** on the `CopyProfiles` table:

- **Claim** — an atomic `ExecuteUpdate` (or `FOR UPDATE SKIP LOCKED` when capping per-node) takes
  profiles that are unassigned *or* whose lease has lapsed. Atomicity means two racing supervisors
  never both claim the same row.
- **Renew** — a live node refreshes its lease each cycle, so it keeps its claim.
- **Reclaim** — a crashed node's lease expires, and a survivor picks the profile up on its next cycle
  (self-heal). On graceful shutdown the node **releases** its leases immediately so failover is fast.
- **Watchdog** — a host whose task has exited while the profile is still ours is restarted.
- Reconcile is jittered to avoid a thundering herd of `UPDATE`s at scale.

## Consequences

- No standalone coordinator to deploy or keep healthy — Postgres is the single source of truth.
- Double-copying is prevented by row-level atomicity, not by application-level locking.
- Failover latency is bounded by the lease TTL (minus the fast-path graceful release).
- This is the money path; it is guarded by the deterministic stress suite (DST) — never weaken a DST
  scenario to make it pass.

<!-- [ZH-HANS] Translation needed -->
