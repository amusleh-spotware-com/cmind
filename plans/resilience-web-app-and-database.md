# Resilience Plan — Main Web App & Database (reliability, scalability, testability)

Status: PLAN ONLY — no implementation. Author baseline: 2026-07-11.

## 0. Goal

Make the Blazor Server + Minimal API web tier and its PostgreSQL database reliable, horizontally
scalable, and self-healing under the failures that hit apps of this shape — **and make every app
feature, option, and UI surface automatically covered by unit + integration + E2E (with UI) tests**,
enforced by a strict, mandatory rule in `CLAUDE.md`.

Stack context (from `CLAUDE.md`): .NET 10, Blazor Server SSR + SignalR + Minimal API, EF Core +
Npgsql, Aspire orchestration (`AddNpgsqlDbContext`), multi-tenant, Data Protection key ring in DB,
soft-delete + audit-chain interceptors, health checks `/health` + `/alive`, Serilog + OTel.

---

## 1. Baseline — what already exists (don't rebuild)

- **DB registration:** Web uses Aspire `builder.AddNpgsqlDbContext<DataContext>(...)` → pooling +
  health check + tracing + **default connection retry** out of the box.
- **Health probes:** `/health` (readiness, incl. `AddNpgSql`) + `/alive` (liveness) mapped in all
  environments for K8s/cloud.
- **Security:** auth cookie `HttpOnly`+`SameSite=Lax`+`SecurePolicy=Always`; `SecurityHeaders.cs`;
  auth endpoints rate-limited (`RateLimitPolicies.Auth`, fixed-window per-IP).
- **Migrations:** `OwnerSeeder` runs `db.Database.MigrateAsync` at startup.
- **Observability:** Serilog compact-JSON + OTel traces/metrics/logs, `trace_id`/`span_id`
  enrichment, OTLP + Azure Monitor + AWS X-Ray/CloudWatch.
- **Audit:** tamper-evident `AuditChainInterceptor`, `AuditStampingInterceptor`, soft-delete filter.
- **Tests:** UnitTests / IntegrationTests (Testcontainers) / E2ETests (Playwright `AppFixture`,
  `PageSmokeTests`) / StressTests.

## 2. Gaps found in the current code

| # | Gap | Evidence | Risk |
|---|---|---|---|
| G1 | `CopyAgent` registers DbContext with plain `AddDbContext(UseNpgsql(...))` — **no `EnableRetryOnFailure`** | `src/CopyAgent/Program.cs:22` | Transient DB blip kills copy-agent operations |
| G2 | No explicit, uniform DB resiliency config (retry count, `CommandTimeout`, pool size) — relies on Aspire defaults, undocumented, inconsistent across Web/Mcp/CopyAgent | DI + Program files | Inconsistent behavior, no tuning |
| G3 | **Startup `MigrateAsync` runs on every replica** — concurrent migration on scale-out / rolling deploy | `Web/Auth/OwnerSeeder.cs:23` | Migration races, deploy failures |
| G4 | **Blazor Server multi-replica has no SignalR backplane / sticky-session strategy documented** | no backplane in `Web/Program.cs` | Circuit loss on scale-out; can't scale web tier |
| G5 | No app-wide graceful-degradation when DB is briefly unavailable (retry vs fail-fast per endpoint) | — | 500s instead of transient recovery |
| G6 | No documented connection-pool / `Maximum Pool Size` / PgBouncer strategy for scale | — | Pool exhaustion under load |
| G7 | No backup / PITR / restore-drill documentation or automation for the DB | `docs/deployment/*` | Data-loss exposure (trading/financial data) |
| G8 | Test coverage is not *enforced* — no strict rule that every feature/option/UI gets all 3 tiers | `CLAUDE.md` has a mandate but not a per-change gate | Coverage drift |

---

## 3. Research — what goes wrong for Blazor-Server + EF/Postgres apps, and the fix

1. **Transient DB disconnects / failovers** (managed Postgres patching, RDS/Flexible-Server failover,
   network blips). Fix: `EnableRetryOnFailure` (execution strategy) **everywhere**, idempotent
   transactions, `CommandTimeout`, and awareness that manual transactions need the retrying strategy
   wrapper. → WS-A.
2. **Connection-pool exhaustion** under load / long-held scopes. Fix: right-size `Maximum Pool Size`,
   short-lived `DbContext` scopes, optional PgBouncer (transaction pooling) for horizontal scale. → WS-A/WS-D.
3. **Migration on startup with N replicas** → race/lock/deploy break. Fix: run migrations as a
   dedicated init job / leader-only, app replicas *wait* for schema, never migrate concurrently. → WS-B.
4. **Blazor Server circuit fragility**: a dropped WebSocket loses UI state; scale-out without a
   backplane + sticky sessions breaks reconnect. Fix: reconnection UX, sticky sessions (session
   affinity) + optional Redis/Azure SignalR backplane, tuned circuit retention & disconnect timeouts. → WS-C.
5. **Long/slow queries & N+1** blocking the pool. Fix: query timeouts, `AsNoTracking` for reads,
   projection to DTOs (CQRS-lite already the convention), pagination, EF query analysis. → WS-D.
6. **Thundering-herd on cache miss / cold start.** Fix: output/response caching where safe, memory
   cache already present for GHCR/tags. → WS-D.
7. **Unhandled exceptions taking down a circuit / request.** Fix: global error boundary in Blazor,
   problem-details for APIs, per-request timeout middleware. → WS-C.
8. **Backpressure / overload**: no global rate limiting beyond auth. Fix: sensible global limiter +
   concurrency limits on expensive endpoints (AI, builds, backtests). → WS-C.
9. **Data durability & recovery**: financial data needs backups + tested restore. Fix: PITR, backup
   schedule, periodic restore drills, documented RPO/RTO. → WS-E.
10. **Secrets/key-ring availability**: Data Protection keys live in DB; if DB down at startup the app
    can't decrypt. Fix: startup ordering/health-gating + clear failure mode. → WS-B.

---

## 4. Workstreams

### WS-A — Database connection resilience & tuning

**A.1 Uniform resilient DbContext config** across Web, Mcp, CopyAgent, and the design-time factory:
- `EnableRetryOnFailure(maxRetryCount, maxRetryDelay, errorCodesToAdd)` on Npgsql (fix G1/G2).
- Explicit `CommandTimeout`.
- Centralize the Npgsql options in one shared extension so all three hosts are identical (avoid the
  CopyAgent divergence). Bind tunables from `AppOptions` (no magic numbers — CLAUDE.md).
**A.2 Execution-strategy-safe transactions.** Audit every manual `BeginTransaction` / multi-SaveChanges
flow (e.g. copy lease SQL, seeders) — with a retrying strategy these must run inside
`strategy.ExecuteAsync(...)` or they throw. Fix + test.
**A.3 Pool sizing.** Set/verify `Maximum Pool Size`, `Timeout`, `Connection Idle Lifetime`; document
per-deployment. Ensure `DbContext` scopes stay request-scoped and short.
**A.4 Graceful DB-down behavior.** Readiness (`/health`) already reflects DB; ensure the app returns
retry-friendly 503 (not 500) when DB is transiently down, and background services back off instead of
tight-looping (`InstanceReconciler`/pollers already catch+delay — verify + standardize).

**Tests:** integration (Testcontainers) — kill/pause the Postgres container mid-operation, assert
retry succeeds; assert a manual-transaction flow survives a transient failure; assert `/health` flips
to unhealthy when DB paused and back when resumed. Unit — options binding. E2E — DB paused → UI shows
a graceful "temporarily unavailable" state, no Blazor error UI; recovers when DB returns.

### WS-B — Migrations & startup ordering (fix G3)

**B.1 Single-writer migrations.** Move `MigrateAsync` out of the per-replica startup path: a
dedicated migration step (Aspire/K8s init job or leader election via a Postgres advisory lock) runs
migrations once; app replicas verify schema compatibility and start read-to-serve, never migrate
concurrently.
**B.2 Startup health-gating.** App reports `not ready` until schema present + Data Protection key ring
readable; liveness stays up so K8s doesn't kill a pod merely waiting on DB.
**B.3 Seeders idempotent + safe under concurrency** (OwnerSeeder, LocalNodeSeeder) — guarded so two
replicas don't double-seed.

**Tests:** integration — two app instances against one fresh DB start concurrently → migrations run
exactly once, both become healthy, no duplicate owner/local-node. E2E — rolling restart keeps the app
serving.

### WS-C — Web tier reliability & horizontal scale

**C.1 Blazor Server scale-out strategy (fix G4).** Decide + document + implement: session affinity
(sticky) at the ingress + a SignalR backplane (Redis or Azure SignalR Service) so circuits survive
multi-replica. Tune `CircuitOptions` (DisconnectedCircuitRetentionPeriod, MaxRetained) and the
JS-side reconnect UI so a transient WS drop reconnects gracefully.
**C.2 Global error boundary** around the Blazor root layout so a component exception shows a friendly
error region, not a dead circuit; Problem-Details for Minimal APIs.
**C.3 Request/circuit timeouts + concurrency limits** on expensive endpoints (AI generate/vision,
builder, backtest launch) to protect the pool and node cluster; extend rate limiting beyond auth to a
sensible global policy.
**C.4 Graceful shutdown** (drain SignalR circuits + in-flight requests on SIGTERM) for clean rolling
deploys.

**Tests:** unit — options/limiter config. Integration — limiter returns 429 past threshold; timeout
middleware aborts a slow request. E2E (Playwright) — kill the SignalR connection mid-session → UI
auto-reconnects and state is intact; a component that throws shows the error boundary, not the Blazor
error UI; expensive endpoint under concurrency returns graceful backpressure.

### WS-D — Query performance & load resilience

**D.1 Read-path hygiene:** `AsNoTracking` + DTO projection for all list/reporting endpoints
(CQRS-lite is already the convention — audit & fill gaps), pagination on unbounded lists, query
timeouts. Fix known EF/Npgsql gotchas already documented in CLAUDE.md (TPH `OfType`, nav-cycle
serialization, computed-property translation) — add regression tests.
**D.2 Caching** where safe (output caching for static-ish pages, existing `MemoryCache`), with correct
per-tenant/per-user keys.
**D.3 Load test** the web tier (k6/Crank or a StressTests HTTP driver): sustained + spike load →
assert p99 latency, no pool exhaustion, no error-rate blowup.

**Tests:** integration — projection endpoints don't trip the documented EF gotchas (real Postgres).
Stress — HTTP load profile reproducible by seed/config; asserts SLO thresholds.

### WS-E — Data durability, backup & recovery (fix G7)

**E.1 Backups + PITR** documented and (where infra allows) automated in the deploy IaC
(`deploy/azure` bicep Flexible Server, `deploy/aws` RDS Terraform, Helm/compose for self-host).
Define **RPO/RTO** for this trading/financial data.
**E.2 Restore drill** runbook + a periodic automated restore-verification (restore into a scratch DB,
run schema+smoke checks).
**E.3 Data Protection key-ring durability** — keys persist in DB (already), protected by cert; ensure
backups include them and document key rotation.

**Tests:** integration — restore a seeded backup into a Testcontainer and assert schema + audit-chain
verify passes (`IAuditTrailVerifier`). Document manual DR steps; automate the verify in CI where feasible.

### WS-F — Mandatory test-coverage rule (fix G8) + full-surface coverage

**F.1 Amend `CLAUDE.md`** with a strict, enforced rule (proposed text below) making all-three-tier
coverage a hard gate for **every** change — new page/dialog/option/endpoint/config toggle.
**F.2 Backfill coverage** so *every* existing app feature, option, and UI surface has:
- Unit tests for domain invariants/transitions and option-binding.
- Integration tests (Testcontainers) for persistence + endpoint behavior.
- E2E (Playwright) driving the real UI for every page/dialog/action + `PageSmokeTests` for every route.
Produce a coverage matrix (feature × tier) and close every gap.

**Proposed `CLAUDE.md` rule text (to add under "Testing & docs — MANDATORY"):**

> **4. Every change is covered at all three tiers or it does not merge.** Any new or changed
> feature, page, dialog, nav entry, endpoint, config option/toggle, or UI control MUST ship, in the
> same commit: (a) unit tests for its domain invariants/transitions and any options binding; (b) an
> integration test against real Postgres (Testcontainers) for its persistence/endpoint behavior; and
> (c) a Playwright E2E that drives the real UI (create/edit/save round-trip + happy path + renders
> without the Blazor error UI), or, for an API-only feature, an authenticated API-level E2E. New
> routes are added to `PageSmokeTests`. A change exercisable at a tier but lacking that tier's test is
> not "done". No "small change", config-only, or UI-only exemptions.

---

## 5. Testing strategy (reliable, reproducible, cross-machine)

- **Unit** — options binding, domain invariants, middleware/limiter config, error-boundary logic.
  `FakeTimeProvider`, no network.
- **Integration** — Testcontainers Postgres for: retry-on-failure, transaction-under-retry,
  concurrent-migration-once, seeder idempotency, projection endpoints vs EF gotchas, backup/restore
  verify, health flip. Identical on any machine/CI via pinned container images.
- **E2E** — Playwright `AppFixture`: DB-down UX, SignalR reconnect, error boundary, backpressure,
  every page/dialog/option round-trip; `PageSmokeTests` covers every route. Edge on Windows /
  Chromium fallback (per project harness).
- **Stress/Load** — reproducible HTTP load (seeded/config-driven) asserting latency + pool + error SLOs.

Reproducibility: injected `TimeProvider`, seeded RNG, Testcontainers (no shared external DB), no
wall-clock waits — a red test is a real regression on every box.

---

## 6. Structured logging & observability (audit/regulatory grade)

- Continue authoring via `LogMessages` (source-generated) — never raw `ILogger.Log*`.
- Add structured events for: DB retry/failover-recovered, migration start/complete, circuit
  connect/disconnect/reconnect, limiter throttle, DB-unhealthy/healthy transitions, startup readiness
  gates. Each with stable `EventId` and correlation (`trace_id`/`span_id` already enriched, add
  `tenant`/`userId` where present).
- Money/decision + auth events flow through the tamper-evident `AuditChainInterceptor` for legal/audit.
- Metrics/SLOs: request latency, error rate, DB pool usage, retry count, active SignalR circuits,
  health-probe status — via existing OTel (OTLP / Azure Monitor / X-Ray).

---

## 7. Sequencing & deliverables

1. **WS-A** DB resilience (retry everywhere incl. CopyAgent, timeouts, pool, tx-under-retry) + tests. *(highest ROI, fixes G1/G2/G5/G6)*
2. **WS-B** migration/startup ordering (leader/init-job, health-gating, idempotent seeders) + tests. *(fixes G3)*
3. **WS-C** web-tier reliability + Blazor scale-out backplane/affinity + error boundary + backpressure + tests. *(fixes G4)*
4. **WS-D** query hygiene + caching + load tests.
5. **WS-E** backup/PITR/restore-drill + IaC + docs. *(fixes G7)*
6. **WS-F** amend `CLAUDE.md` coverage rule + backfill full-surface tests + coverage matrix. *(fixes G8)*
7. **Docs:** update `docs/deployment/*` (scaling, cloud-aws, cloud-azure, kubernetes, local) and
   `docs/operations/*` in the same commits (CLAUDE.md docs-in-sync mandate).

## 8. Definition of done

- Transient DB failures, failovers, and pauses recover automatically (retry) across Web/Mcp/CopyAgent
  — proven by Testcontainers fault-injection tests.
- Migrations run exactly once on scale-out; replicas start safely; seeders idempotent.
- Web tier scales horizontally (sticky + backplane), circuits reconnect gracefully, component errors
  are boundaried, expensive endpoints shed load — proven by Playwright + integration tests.
- Backups/PITR documented + automated; a restore drill verifies schema + audit chain.
- `CLAUDE.md` enforces all-three-tier coverage for every change; existing surface backfilled; coverage
  matrix has no gaps.
- Structured, correlated, audit-grade logs + SLO metrics for every reliability event.
- `dotnet test` green; DDD checklist satisfied for new domain code; docs in sync.
