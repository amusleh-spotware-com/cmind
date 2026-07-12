# Public-Launch Readiness — Whole-App Hardening Plan

Status: **IN EXECUTION** — Phase A started 2026-07-12. Author baseline: 2026-07-12. Owner: main branch.

## Execution log (2026-07-12)

- **Stale gaps corrected after audit:** WS-3a full-jitter backoff is **already done** (`BackoffPolicy`
  uses equal-jitter `0.5 + 0.5·rand`); WS-10 no-raw-logging is **already gated**
  (`ArchitectureGuardTests.No_direct_ILogger_calls_outside_standalone_agents`), as is no-ambient-clock.
  K8s is **already deployable** (`deploy/helm/cmind` chart + `scripts/k8s-e2e.sh` kind harness) — the gap
  is *proof/enforcement*, not infra.
- **Landed:** WS-1 UI page census gate `RouteCoverageTests` (green — every `@page` route must be in
  `PageSmokeTests.Routes()` or explicitly excluded). WS-12 CLAUDE.md mandates fused (root DoD + full
  `tests/CLAUDE.md` coverage/K8s/live-copy sections). Kind in-cluster run verified locally via
  `scripts/k8s-e2e.sh`.
- **Coverage stance (user directive: 100%, no compromise).** 100% line/branch across all three tiers is
  now the mandated target and the enforced direction (census gates + never-regress rule). Reaching a
  literal 100% number on ~740 source files is a multi-week fill campaign tracked by WS-1; the gates make
  every *new* omission fail the build so coverage only climbs.
- **More stale gaps corrected (already shipped, verified green this session):** WS-3 chaos/fault-injection
  suite exists — `CopyChaosDstTests` (10 seeds × opens/partial-close/scale-in/close/socket-flap/token-
  rotation/rejection → heal → reconcile → assert convergence + never-fault) + `CopyLeaseReclaimStressTests`
  (node-death → lease reclaim); 17 tests green. So WS-3 (resilience/recovery) is effectively complete
  bar full-jitter (also done).
- **Landed + pushed this session:** WS-1 `RouteCoverageTests`; WS-12 CLAUDE.md mandates (root + tests);
  CI coverage on all three tiers + nightly `k8s-e2e` (In-cluster Kind) job; K8s whole-app verified locally
  on Kind (`scripts/k8s-e2e.sh`, exit 0, in-cluster tests green); WS-6 `.Cancel()`→`CancelAsync()` sweep
  (4 files, 234 copy unit tests green); WS-4 `TenantIsolationTests` (cross-tenant AI-credential isolation
  over real HTTP + Postgres, green).
- **Genuinely remaining (multi-session, tracked):** live-copy full scenario matrix *executed* against real
  cluster+creds (harness + nightly wiring exist; unsafe to run unattended here) · literal-100% coverage
  backfill on existing lines (the gates now force every *new* line covered; old lines are a fill campaign)
  · full `/security-review` sweep + API-route (endpoint) census gate · white-label industry matrix · perf
  benchmarks.

**Status by workstream:** WS-1 gated ✓ · WS-2 harness ✓/live-run pending · WS-3 ✓ · WS-4 started ✓
(tenant-isolation; security-review pending) · WS-6 ✓ · WS-10 ✓ · WS-12 ✓ · WS-5/7/8/9/11 tracked.


> Umbrella plan for going public. It does **not** re-plan features already covered by the 17 plans in
> `plans/` — it **audits, closes residual gaps, proves coverage, and turns every rule into an
> enforced gate** so no future change can drift back out of compliance. Where an existing plan already
> owns a topic, this plan references it and adds only the *launch-gate* on top.

## 0. Method & guiding principle

Three passes over the whole app, each producing a checklist gate that CI enforces:

1. **Audit** — measure actual state per dimension (evidence, not assumption).
2. **Close** — fix the concrete gaps found.
3. **Fuse** — encode the rule as (a) a CLAUDE.md mandate and (b) an automated test/CI check that
   **fails the build** on drift. A rule with no gate is a future regression.

**Launch-ready = every workstream gate below is green in CI, on `main`, with zero manual waivers.**

---

## 1. Baseline that is already solid — do NOT rebuild

Verified present (2026-07-12). Future work *extends and proves* these; it must not duplicate them.

| Dimension | Mechanism in place | Location |
|---|---|---|
| Versioning | `VersionPrefix 1.0.0` single source; `Core.VersionInfo`; SemVer + Keep-a-Changelog | `Directory.Build.props`, `CHANGELOG.md` |
| SDK pin | `global.json` 10.0.100, `rollForward: latestFeature`, no prerelease | `global.json` |
| Package mgmt | Central Package Management (`ManagePackageVersionsCentrally`), 53 pinned versions | `Directory.Packages.props` |
| Warnings gate | `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-default`, no `NoWarn` | `Directory.Build.props` |
| HTTP resilience | `AddStandardResilienceHandler` (default), per-client Polly on node-agent + AI | `Web/HostDefaults.cs`, `Nodes/NodeHttpClients.cs`, `Infrastructure/Ai/AiHttpClientRegistration.cs` |
| Rate limiting | `AddRateLimiter` fixed-window; per-message-type Open API client limiter | `Web/Program.cs` |
| Audit trail | `AuditLog` entity + `AuditStampingInterceptor` + `IAuditTrailVerifier` (tamper-evident) | `Infrastructure/Persistence/` |
| Health checks | `/health`, `/alive` endpoints | `Web/HostDefaults.cs` |
| Observability | `trace_id`/`span_id` log correlation, OTel metrics/traces, Azure AI + AWS ADOT export | `Infrastructure` (see `cloud-observability-native`) |
| External-svc recovery | socket reconnect/backoff, maintenance handling, resync-from-broker, circuit breaker, token rotation | `CTraderOpenApi/`, `CopyEngine/`, see `resilience-external-services.md` |
| Distributed nodes | lease claim/renew/release `FOR UPDATE SKIP LOCKED`, dead-host watchdog, heartbeat, protocol-version gate | `Nodes/CopyTrading/CopyEngineSupervisor.cs`, `POST /api/nodes/register` |
| Copy fidelity | `FakeTradingSession` + deterministic stress suite (DST) + K8s live-E2E harness | `tests/StressTests`, `tests/E2ETests/CopyLive` |
| CI mandates | analyzer sweep, Playwright tiers, arch-guard tests, CodeQL, nightly | `.github/workflows/{ci,codeql,nightly}.yml` |
| White-label sync | `WhiteLabelCatalog` + `WhiteLabelCatalogParityTests` (build fails on un-catalogued option) | `Core/WhiteLabel/` |
| i18n gate | `NoHardcodedUiTextTests`, `ResourceParityTests` (build fails on hardcoded/blank string) | `tests/` |

**Conclusion of audit:** this is a mature codebase with strong infra. Residual launch risk is not
*missing infra* — it is **unproven completeness**: coverage that is assumed not measured, live paths
that are gated-out not run, and rules that live in prose not in gates. The workstreams below target
exactly that.

---

## 2. Workstreams

Each: **Goal → Evidence/current state → Gaps → Tasks → Coverage → Enforcement gate (new)**.
`P` = priority (P0 blocks launch, P1 strongly wanted, P2 post-launch-fast-follow).

### WS-1 · Test-coverage completeness — "every bit covered" (P0)

- **Goal.** Prove (not assume) unit + integration + E2E cover every feature, endpoint, UI control.
- **Current.** 154 unit / 107 integration / 91 E2E files; `PageSmokeTests`, arch-guard tests, AI-via-fake mandate.
- **Gaps.**
  1. No **coverage floor gate** — nothing fails CI if a new endpoint/page/control ships untested.
  2. No **endpoint census test**: every minimal-API route must map to ≥1 integration/E2E test.
  3. No **UI-control census**: every interactive MudBlazor control (button/dialog/toggle) driven by ≥1 E2E.
  4. Failure-path coverage is mandated in prose but not measured.
- **Tasks.**
  - Add Coverlet + `dotnet test --collect` to CI; publish line/branch % per project; set a floor
    (start at current measured %, ratchet up — never down).
  - Add `EndpointCoverageTests`: reflect all mapped routes, assert each has a referencing test
    (attribute/registry based). Fails build on an orphan route.
  - Add `PageControlSmokeTests`: for each page, assert primary interactive controls are exercised by an
    E2E (extend `PageSmokeTests` registry with a control manifest).
  - Add a failure-path checklist column to each feature doc; a nightly test asserts each critical flow
    has a `*_Fails_*` sibling test.
- **Enforcement.** CI coverage-ratchet + `EndpointCoverageTests` + `PageControlSmokeTests`. CLAUDE.md
  mandate #2 amended (see §4).

### WS-2 · Copy-trading full **live** E2E (P0 — explicit user demand)

- **Goal.** Every trading operation verified by a live E2E that runs real cTrader Open API + real node
  cluster, on happy AND adverse conditions.
- **Current.** `FakeTradingSession` (faithful) + DST stress + K8s harness + `CopyLive/Onboarding`
  exist; live **order-execution** run is still gated (needs creds + cluster) per `copy-trading-oauth-shipped`.
- **Gaps.** The live tier exists as a *skip-clean* harness; it is **not actually executed** end-to-end
  against a live broker in CI/nightly. That is the single biggest launch risk for a money-moving app.
- **Tasks.**
  - Stand up a persistent **demo/sandbox cID** + a small kind/helm node cluster reachable from nightly.
  - Onboard tokens via the existing `CMIND_ONBOARD=1` path (self-serviceable — see
    `live-testing-self-serviceable`); store as CI secrets.
  - Author `CopyLive` scenarios covering **every** op: market open/close, all pending types + expiry,
    partial close, SL/TP amend, trailing, partial-fill true-up, multi-follower fan-out, and the adverse
    set — order rejection, socket drop mid-order, token invalidation mid-flight, node death + lease
    reclaim, desync/resync convergence.
  - Assert the invariant after each: **follower state re-converges to broker truth, zero lost intent,
    zero duplicated side effect.**
  - Wire into `nightly.yml`; skip-clean when secrets absent (keep the fake path for PR CI).
- **Enforcement.** Nightly live-copy job required-green before a release tag. CLAUDE.md mandate #3
  amended to require a live scenario for every new copy op.

### WS-3 · Resilience & long-running recovery — external services + distributed nodes (P0)

- **Goal.** Every external dep treated as hostile; every long run survives crash/reboot/partition and
  self-heals to a valid state, operator-free.
- **Current.** Strong (see §1). Owned by `resilience-external-services.md` + `resilience-web-app-and-database.md`.
- **Gaps (from those plans + this audit).**
  1. Reconnect backoff is exponential without **full jitter** (thundering-herd on venue recovery).
  2. Resync is the recovery primitive for copy but **not universally** applied to runs/backtests/prop-firm.
  3. Ambiguous-timeout order sends not yet **idempotency-keyed** end-to-end (dedup on resync partial).
  4. `.Cancel()` used in async shutdown paths where `CancelAsync()` is the .NET-10 idiom (WS-6).
  5. No **chaos/fault-injection** test tier proving recovery under random partition/kill.
- **Tasks.**
  - Add full jitter to `BackoffPolicy`; boundary test.
  - Generalize "reconcile-from-source-of-truth on reconnect" to runs/backtests (pollers already
    reconcile self-exited containers — formalize as the universal primitive) and prop-firm tracking.
  - Deterministic idempotency key (`label`/client-msg-id) on every order send; resync dedup test.
  - Extend DST/stress with a **chaos harness**: seeded random node-kill, socket-drop, DB-blip,
    clock-skew; assert convergence invariant holds every seed.
  - Node protocol: add explicit **version negotiation + reject** on `register` (protocol-version gate
    exists — make mismatch a first-class, logged, surfaced rejection, not a silent drop).
- **Enforcement.** Chaos suite required-green in nightly; CLAUDE.md resilience clause (see §4).

### WS-4 · Security — zero acceptable flaw (P0)

- **Goal.** No exploitable flaw in auth, secrets, transport, multi-tenant isolation, node trust.
- **Current.** `ISecretProtector` encryption, DataProtection, short-lived HS256 node JWTs, 2FA/TOTP,
  CodeQL, rate limiter, no-shell node exec via `ArgumentList`.
- **Gaps.**
  1. No **own-API versioning/route-hardening review** and no formal **authZ matrix test** (per-tenant
     isolation: user A cannot read/act on user B's aggregates via any endpoint/MCP tool).
  2. No documented **threat model** or dependency-vuln gate (`NuGetAudit=false` currently).
  3. Secrets-in-logs and secrets-at-rest coverage asserted only informally.
- **Tasks.**
  - Run the `/security-review` skill over the whole app; triage findings P0/P1.
  - Add `TenantIsolationTests`: matrix over every aggregate-owning endpoint + MCP tool, assert
    cross-tenant access → 403/404, never data leak.
  - Flip `NuGetAudit=true` (or a scheduled `dotnet list package --vulnerable` nightly gate); triage.
  - Write `docs/security/threat-model.md` (STRIDE over: web, node HTTP, Open API socket, AI provider,
    docker exec, MCP). Localize per docs mandate.
  - Verify no secret reaches logs via a `NoSecretInLogsTests` (scan source-gen `LogMessages` args +
    a runtime redaction assertion).
- **Enforcement.** `TenantIsolationTests` + `NoSecretInLogsTests` required-green; nightly vuln scan;
  CLAUDE.md mandate #6 amended.

### WS-5 · Own-API & node-protocol versioning (P1)

- **Goal.** Public/REST + node protocol can evolve without breaking deployed nodes or integrators.
- **Current.** Product SemVer solid. **But** `/api/*` REST routes are **unversioned**; node protocol
  has a version field but no negotiation contract test.
- **Gaps.** A breaking change to `/api/nodes/register`, calendar API, or copy endpoints would silently
  break older agents/clients.
- **Tasks.**
  - Introduce `Asp.Versioning` (URL-segment `/api/v1/…` or header) for externally-consumed routes
    (node register/heartbeat, calendar REST, any integrator-facing endpoint). Keep v1 = current shape.
  - Add a **protocol-compat matrix test**: main vN ↔ agent vN-1/vN/vN+1 → documented accept/reject.
  - Document the deprecation policy in `CHANGELOG.md` + a new `docs/operations/api-versioning.md`.
- **Enforcement.** `ApiVersioningTests` (every externally-consumed route carries a version);
  protocol-compat test. CLAUDE.md new mandate #11 (see §4).

### WS-6 · Modern C# / new .NET API sweep (P1)

- **Goal.** No legacy syntax or superseded BCL API on any touched line; latest idioms.
- **Current.** Mandate exists; analyzer sweep in CI.
- **Gaps (concrete, found in audit).**
  - `.Cancel()` in async supervisors/hosts (`CopyEngineSupervisor`, `PropFirmTrackingSupervisor`,
    `CopyEngineHost`, `OpenApiConnection`) → `CancelAsync()` where the call site is async (CA-flagged).
  - `Task.Run(() => …, CancellationToken.None)` long-loop launches — audit for `TaskCreationOptions
    .LongRunning` vs. proper `async` background tasks; confirm not swallowing faults.
  - Full-superset analyzer pass (`-p:AnalysisLevel=latest-all`) not run repo-wide recently.
- **Tasks.**
  - Run `dotnet format analyzers` + the `latest-all` superset build across all projects; fix CA1822/
    CA1859/CA1849/CA1873/CancelAsync/collection-expression/etc. on the whole tree (not just touched).
  - Migrate the `.Cancel()` async sites to `CancelAsync()`.
- **Enforcement.** Already fused (analyzer sweep gate); add a one-time repo-wide clean baseline commit
  so the gate starts from zero.

### WS-7 · Redundancy, duplication & dead-code elimination (P1)

- **Goal.** No duplicate feature impl, duplicate white-label option, duplicate UI, or dead code.
- **Current.** No systematic dedup pass done.
- **Gaps.** Unknown until measured; candidates: overlapping AI orchestration paths, duplicate DTO/
  mapping, redundant white-label knobs, unused `ExternalNode` (0 `.cs` files — confirm intent/remove).
- **Tasks.**
  - Enable Rider/Roslyn **dead-code inspection** (IDE0051/IDE0052/CS0169 as warnings) repo-wide; remove.
  - `ExternalNode` project has 0 source files — decide: populate, or delete from `cmind.slnx`.
  - Run a duplication scan (Rider "duplicates" / `jscpd` on `.cs`); consolidate ≥ threshold clones.
  - White-label audit: cross-check `WhiteLabelCatalog` for semantically-duplicate options; merge/alias.
- **Enforcement.** Dead-code analyzer promoted to warning (fails build via TWAE); duplication threshold
  check in nightly.

### WS-8 · Performance — no slow/allocating hot paths (P2)

- **Goal.** No obvious inefficiency on hot paths (copy dispatch loop, pollers, dashboard poll, AI
  streaming, EF queries).
- **Current.** Not profiled for launch.
- **Tasks.**
  - Run the `dotnet-diag:analyzing-dotnet-performance` skill over hot paths; fix flagged async/alloc/
    LINQ/string anti-patterns.
  - EF: run `optimizing-ef-core-queries` review — no N+1, projections over full-entity loads, `AsNoTracking`
    on reads, split-query where needed.
  - Add a couple BenchmarkDotNet micro-benchmarks for the copy dispatch decision + resync diff.
- **Enforcement.** Perf-review checklist in the `done` checklist for hot-path-touching changes.

### WS-9 · Deployment simplicity for every user type (P1)

- **Goal.** Each persona deploys in one documented, tested command.
- **Current.** Aspire (`dotnet run --project src/AppHost`), docs site, K8s harness.
- **Gaps.** No single "personas × deploy path" matrix that is E2E-smoke-tested; no one-command
  container-compose for the self-hoster; white-label deploy config story per persona not proven.
- **Tasks.**
  - Define personas (local evaluator, single-tenant self-hoster, white-label operator, cloud/K8s).
  - For each: a documented one-command path + a smoke test that boots it (compose up / helm install /
    aspire run) and hits `/alive`.
  - Ship a `docker-compose.yml` + `.env.example` for the self-hoster; validate in CI (compose smoke).
- **Enforcement.** `DeploymentSmokeTests` (per persona) + docs matrix kept in sync (docs gate).

### WS-10 · Logging & audit — top-notch (P1)

- **Goal.** Every significant action is logged (source-gen), correlated, and — for money/security
  events — audit-trailed and tamper-evident.
- **Current.** Source-gen `LogMessages`, trace correlation, `AuditLog` + tamper verifier.
- **Gaps.**
  - No **audit-event census**: which domain events MUST write an `AuditLog` (login, 2FA, token
    rotation, copy start/stop, order send, white-label change, node join)? Not enumerated/tested.
  - No log-level policy doc; no assertion that no raw `ILogger.Log*` slipped in (mandate #6).
- **Tasks.**
  - Enumerate audit-required events in `Core/Compliance`; add `AuditCoverageTests` asserting each
    fires an `AuditLog`.
  - Add `NoRawLoggingTests` (source scan: only `LogMessages` calls, no inline `ILogger.LogInformation`).
  - Doc `docs/operations/logging-and-audit.md` (levels, correlation, retention, tamper-verify).
- **Enforcement.** `AuditCoverageTests` + `NoRawLoggingTests` required-green.

### WS-11 · White-label = industry standard (P1)

- **Goal.** White-labeling matches SaaS industry norms and is fully owner-tunable + in sync.
- **Current.** `WhiteLabelCatalog` + parity test + owner Settings→Deployment + `IWhiteLabelSettings`
  overlay (see `white-label-owner-settings-shipped`). Strong.
- **Gaps.** No benchmark against industry-standard white-label feature set (custom domain, theming
  tokens, email sender identity, locale defaults, feature gating, per-tenant branding assets, legal/
  compliance text, support links). Confirm coverage; fill holes.
- **Tasks.**
  - Produce a white-label capability matrix vs. industry norms; gap-fill missing knobs through the
    existing catalog+owner-settings pipeline (never config-only — mandate #10).
  - E2E: white-label operator changes brand/theme/locale/legal at runtime → renders, persists, RTL ok.
- **Enforcement.** Existing `WhiteLabelCatalogParityTests`; add a white-label E2E operator journey.

### WS-12 · Docs ⇄ code ⇄ CLAUDE.md sync + future-proof mandates (P0 — the meta ask)

- **Goal.** Docs, all i18n locales, and CLAUDE.md files are in sync now and **cannot drift** later.
- **Current.** Docs-in-same-commit mandate, i18n parity gate, per-layer CLAUDE.md.
- **Gaps.** The *new* rules this plan introduces (coverage floor, live-copy, tenant isolation, api
  versioning, audit census, deploy smoke) are not yet mandated anywhere.
- **Tasks.**
  - Amend CLAUDE.md files per §4 so every rule above is binding on future work.
  - Add a `docs-freshness` nightly: assert each feature has a doc and each doc's "last-verified"
    stamp is newer than the feature's last code change (heuristic gate).
- **Enforcement.** CLAUDE.md amendments (§4) + docs-freshness nightly.

---

## 3. Execution phases

- **Phase A (launch-blocking, P0):** WS-1 coverage gate · WS-2 live-copy · WS-3 chaos+jitter+idempotency
  · WS-4 security review+tenant isolation · WS-12 mandates. Ship each behind its CI gate.
- **Phase B (P1):** WS-5 api versioning · WS-6 modern sweep · WS-7 dedup/dead-code · WS-9 deploy smoke
  · WS-10 audit census · WS-11 white-label matrix.
- **Phase C (P2, fast-follow):** WS-8 perf/benchmarks · duplication ratchet · doc-freshness heuristic.

Each workstream ships in its own logical commit(s) on `main` with unit+integration+E2E in the same
commit (mandate #2) and docs+i18n updated (mandate #8). Tag `v1.0.0` only when all Phase-A gates green.

---

## 4. CLAUDE.md amendments — fuse the rules (draft clauses)

Proposed edits (to apply when the plan is executed, not now):

**Root `CLAUDE.md` — extend "Hard mandates":**

- **#2 (tests)** append: *"Coverage never regresses — the CI line/branch floor only ratchets up. Every
  new minimal-API route is registered in `EndpointCoverageTests`; every new interactive UI control is
  exercised by an E2E in `PageControlSmokeTests`. A route/control with no test does not merge."*
- **#3 (copy fidelity)** append: *"Every new copy-trading operation ships a `CopyLive` scenario
  (happy + at least one adverse: reject / drop / token-invalidation / node-death) asserting
  re-convergence to broker truth — in the same commit. The nightly live-copy job is required-green
  before any release tag."*
- **#6 (secrets/logging)** append: *"No cross-tenant data path may exist — every aggregate-owning
  endpoint/MCP tool is covered by `TenantIsolationTests`. Only source-gen `LogMessages` may log
  (`NoRawLoggingTests`). Every money/security event enumerated in `Core/Compliance` writes an
  `AuditLog` (`AuditCoverageTests`)."*
- **New #11 — Resilience & recovery is proven, not asserted:** *"Every external dependency is treated
  as hostile-unreliable and every long-running actor recovers to a valid state operator-free. New
  network/order/node code ships: full-jitter backoff, an idempotency key on any retry-able side
  effect, reconcile-from-source-of-truth on reconnect, and a chaos/failure-path test proving
  convergence. Externally-consumed routes and the node protocol are versioned; a breaking change bumps
  the version and adds a compat-matrix test."*
- **New #12 — Deployment stays turnkey:** *"Each supported persona (local / self-host / white-label /
  cloud) keeps a one-command deploy path with a `DeploymentSmokeTests` boot check and a docs entry;
  changing deploy shape updates both in the same commit."*

**`tests/CLAUDE.md`** — add the census/coverage-ratchet/live-copy/chaos requirements in long form.

**`src/Web/CLAUDE.md`** — add: new interactive control ⇒ `PageControlSmokeTests` manifest entry;
externally-consumed endpoint ⇒ versioned + `ApiVersioningTests`.

**`src/Core/CLAUDE.md`** — add: new money/security domain event ⇒ audit-required enumeration entry.

Every amendment lands **with its enforcing test** in the same commit — a mandate without a gate is
banned by this plan.

---

## 5. Launch-ready definition of done (single gate)

- [ ] WS-1 coverage floor set + ratcheting; `EndpointCoverageTests` + `PageControlSmokeTests` green.
- [ ] WS-2 nightly live-copy green against real sandbox cID + cluster, all ops + adverse set.
- [ ] WS-3 full-jitter + universal reconcile + idempotency keys + chaos suite green.
- [ ] WS-4 `/security-review` triaged to zero P0/P1; `TenantIsolationTests` + `NoSecretInLogsTests`
      green; vuln scan clean.
- [ ] WS-5 externally-consumed routes versioned; protocol compat-matrix green.
- [ ] WS-6 repo-wide analyzer superset clean; `.Cancel()`→`CancelAsync()` done.
- [ ] WS-7 dead-code removed; `ExternalNode` resolved; duplication under threshold.
- [ ] WS-9 per-persona `DeploymentSmokeTests` green; compose + docs shipped.
- [ ] WS-10 `AuditCoverageTests` + `NoRawLoggingTests` green; logging/audit doc shipped.
- [ ] WS-11 white-label matrix filled; operator E2E green.
- [ ] WS-12 CLAUDE.md amended (every clause paired with a live gate); docs+i18n in sync.
- [ ] `dotnet build` 0 warnings · `dotnet test` all tiers green · `v1.0.0` tagged.
