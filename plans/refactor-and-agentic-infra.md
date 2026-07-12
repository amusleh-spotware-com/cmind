# Refactor, Optimization, Docs & Agentic-Infra Plan

> Repo-wide audit → actionable backlog. Covers five axes the user asked for:
> **(1) refactor · (2) optimize · (3) docs · (4) agentic infra for Claude Code · (5) test gaps.**
> Scope snapshot at authoring: 418 `.cs` files, 697 tracked, 4 test tiers, Docusaurus site, Aspire stack.
>
> Rules still bind: strict DDD, TimeProvider, zero warnings, docs-in-same-commit, three test tiers.
> Nothing here weakens a test or the simulator. Each item has **Why / Where / Done-when**. Ordered by
> value-per-risk. Treat sections as independently shippable slices (small commits, direct to `main`).

---

## Progress — overnight autonomous run (2026-07-12)

Shipped to `main` this run (each an atomic, always-green commit):

- ✅ **§4 agentic infra** — CI restructured into jobs (build 0-warn · analyzer-sweep on *changed*
  files · unit/integration/e2e tiers with Playwright browser install + trx/coverage artifacts · PR
  docs link-check) + nightly DST workflow + NuGet cache; `scripts/` task runner (sweep/test/migration/
  site + pre-commit format hook + hook installer); `.claude/commands/` slash commands (`/sweep`
  `/test-tier` `/migration` `/site` `/done`); `.mcp.json`; `CODEOWNERS`; `.devcontainer/`.
  (`.gitignore` now tracks `.claude/commands` only.)
- ✅ **§5.4 architecture-guard tests** — `ArchitectureGuardTests` fails the build on a Core infra dep,
  an ambient-clock read, or a direct `ILogger.Log*` outside the standalone agents.
- ✅ **§1.4/§2.2 analyzer cleanup (safe subset)** — CA1806 unchecked `TryParse` (latent bug), CA1865
  `EndsWith(char)`, CA1859 concrete return; `.editorconfig` marks EF migrations generated + drops
  CA1861/CA1825 there.
- ✅ **§3.1/§3.2 docs** — `website/docs/architecture.md` (module map + mermaid diagram + request flows)
  and six ADRs promoting the non-inferable decisions; wired into the sidebar.
- ✅ **docs link-gate real** — Docusaurus `onBrokenLinks` flipped `warn`→`throw` (site verified
  link-clean), so the CI docs job now fails on a dangling link; deprecated `onBrokenMarkdownLinks`
  migrated to `markdown.hooks`.
- ✅ **§5.2 failure-path map** — `website/docs/testing/failure-paths.md` maps every mandated failure
  scenario to the test(s) that exercise it; flags three thin spots to verify (MCP auth reject, CBot
  build-failure surfacing, gated live execution).

Audited and found already-optimal (no change needed, evidence for the record):

- **§2.1 EF hot path** — `DashboardQuery` (the most-polled read) already uses `AsNoTracking` +
  server-side projections/aggregates; the one full-row materialize is a documented TPH constraint.
  No low-hanging `AsNoTracking`/projection wins in the hot path — the data layer is disciplined.

Verified already-satisfied (no action needed):

- **§5.3 route smoke** — every static page route is already in `PageSmokeTests` (only dynamic
  `/builder/{id}`, `/instance/{id}` and anonymous `/login` excluded, correctly).
- **§3.4 docs consolidation** — top-level `docs/` removed and brand assets moved to root `design/` in
  commit `f3d3140` (concurrent working-tree process); `ReadmeScreenshotsTests` path already updated.

**Deferred — needs a human in the loop, NOT done unattended:**

- ⛔ **§1.2 CopyEngineHost decomposition** — the 1222-line trade-mirroring core. Its "self-contained"
  pieces (health/circuit-breaker, position book) are woven into `logger`, `_notifications`, `plan`,
  and `timeProvider`, so extraction is a design change, not a pure move. Green DST is **insufficient
  assurance** for a rushed extraction of the money path; the downside (a subtle double/miss-copy on
  real capital) is severe and asymmetric. Do this in a focused, supervised session.
- ⛔ **§1.1 Entities.cs split · §1.3 endpoint splits** — safe in principle but *multi-step*: they pass
  through intermediate non-compiling states. A `git add -A` auto-committer is active on this tree, so
  an intermediate state could be pushed broken. Do these when commits are controlled end-to-end.

**Remaining (safe, not yet done):** §2.1 EF query audit · §2.2 remaining CA1873 (8 sites, per-site
judgement) + CA1822 statics on injected services · §5.2 failure-path scenario→test map in
`tests/CLAUDE.md` · §3.3 AGENTS.md audit · flip Docusaurus `onBrokenLinks` to `throw` once the tree
is link-clean (currently `warn`, so the CI docs job catches build errors but not dangling links).

---

## Executive summary — top 10 by value/risk

| # | Item | Axis | Effort | Risk |
|---|------|------|--------|------|
| 1 | CI: add analyzer sweep + Playwright browser install + tier split | infra/test | M | low |
| 2 | Split `src/Core/Entities.cs` (1567 LOC, 46 types) per-aggregate | refactor | M | low |
| 3 | Decompose `CopyEngineHost.cs` (1222 LOC) into collaborators | refactor | L | med |
| 4 | `.claude/commands/` slash commands (build, sweep, test-tier, migration, docs) | infra | S | none |
| 5 | Repo `.mcp.json` + `scripts/` task runner (analyzer sweep, tiered test) | infra | S | none |
| 6 | CI: NuGet cache + test-results/coverage artifacts + `website` link-check | infra | S | low |
| 7 | Endpoint-file trims: `CopyEndpoints` (533), `AiEndpoints` (454) → feature groups | refactor | M | low |
| 8 | Test gaps: E2E-in-CI, failure-path integration, missing route smoke | test | M | low |
| 9 | Docs: architecture overview, ADRs, "agent onboarding", diagram, dead-stub sweep | docs | M | none |
| 10 | Perf: EF query audit (AsNoTracking/projection/split-query), allocation hot paths | optimize | M | med |

---

## 1. Refactoring

### 1.1 `src/Core/Entities.cs` — 1567 LOC god-file, 46 public types
**Why:** everything from `AppUser` hierarchy, `TradingAccount`, `CBot`, `ParamSet`, node TPH tree,
instance TPH tree, copy-execution/notification/fee entities, `AgentMandate/Proposal` lives in one
file. Kills navigability, blows up diffs, defeats "one aggregate per file" readability. Not a
behavior change — pure file topology.
**Where:** `src/Core/Entities.cs`.
**Plan:** move each aggregate cluster to its own file under a matching namespace folder, keeping
namespaces stable (no `move_type_to_namespace`, just physical split via Rider so usings don't churn):
- `Users/AppUser.cs` (+ Owner/Admin/Regular/Viewer)
- `Accounts/CTraderIdAccount.cs`, `Accounts/TradingAccount.cs`
- `CBots/CBot.cs`, `CBots/CBotSourceProject.cs`, `CBots/ParamSet.cs`
- `Nodes/Node.cs` + TPH subtypes, `Nodes/NodeStats.cs`
- `Instances/RunInstance.cs`, `Instances/BacktestInstance.cs` (TPH trees), `Instances/InstanceLog.cs`
- `CopyTrading/` already has `CopyEntities.cs`; move `CopyExecution/Notification/FeeAccrual/ProviderListing` there
- `Agent/AgentMandate.cs`, `Agent/AgentProposal.cs`
**Done-when:** `Entities.cs` deleted, build 0-warn, all tests green, no namespace change (zero `using` edits elsewhere).

### 1.2 `src/CopyEngine/CopyEngineHost.cs` — 1222 LOC, 30+ private methods, mixed concerns
**Why:** one class does token-update consumption, equity/day guards, flatten signals, pending-timeout
loop, open/close mirroring, partial-close/scale-in/stop-change mirroring, pending place/amend/cancel,
resync, partial-fill true-up, an in-memory position **book**, and per-destination **health/circuit**
tracking. That's ~7 responsibilities in a `BackgroundService`-style host. Hard to test in isolation,
hard to reason about ordering, and the DDD mandate says hosts orchestrate — they don't decide.
**Where:** `src/CopyEngine/CopyEngineHost.cs`.
**Plan (extract collaborators, host keeps the loop):**
- `DestinationBook` — the `_destinationBook` cache + `BookRemove/BookReduce/BookSetStop/Invalidate`.
- `DestinationHealth` — `_destinationHealth` + `RecordCopySuccess/Failure` (the circuit breaker).
- `MirrorStrategies` — `MirrorPartialClose/ScaleIn/StopChange`, `PlacePendingToDestination`,
  `CopyOpenToDestination`, `ApplyProtection` (pure-ish given session + plan).
- `EquityGuard` / `DayGuard` evaluator — `EvaluateGuards/EvaluateDayGuards/FlattenDestination`.
- `ResyncCoordinator` — `Resync` + `TrueUpPartialFills` + `DestinationPositions`.
- Any decision math still inline (sizing already in `Core.CopySizingCalculator`) → verify no domain
  rule is trapped in the host; push genuine invariants to Core value objects.
**Risk:** this is the money path guarded by DST stress suite. **Refactor under green DST** — run
`tests/StressTests` (23/23) + 196 copy unit tests before and after each extraction; no behavior change.
**Done-when:** host < ~400 LOC orchestration only; each collaborator unit-tested directly; DST + copy
unit + integration all green; circuit-breaker `fromResync` bypass semantics preserved (see memory
[[copy-overhaul-progress]]).

### 1.3 Fat endpoint files → feature route groups
**Why:** `CopyEndpoints.cs` (533), `AiEndpoints.cs` (454) mix many endpoints; Minimal API supports
`MapGroup` splits. Improves locating a route and keeps handlers thin (DDD: endpoints orchestrate).
**Where:** `src/Web/Endpoints/CopyEndpoints.cs`, `AiEndpoints.cs`.
**Plan:** split into partial files or sub-groups by concern (copy: profiles / execution / fees /
marketplace / notifications; ai: per-feature). Verify handlers delegate to services, no domain logic.
**Done-when:** files < ~250 LOC each, routes unchanged (PageSmoke/endpoint integration tests green).

### 1.4 Cross-cutting sweep (opportunistic, per touched file)
**Why:** CLAUDE.md "modernize lines you edit" + analyzer sweep. Catch on touched files:
- CA1822 static, CA1859/CA1826/CA1849 perf, redundant `.ToList()/.ToArray()`, `EndsWith(char)`.
- `== null`/`!= null` → `is null`/`is not null`; `new List<T>()` → collection expressions.
- Any lingering `DateTime.UtcNow` (mandate #4) — grep-audit as a one-off gate (see 5.4).
**Done-when:** `dotnet format analyzers <proj>.csproj --verify-no-changes --severity info` clean on
every touched project; the full superset build (`-p:AnalysisLevel=latest-all`) shows no new CA/IDE on
touched files.

### 1.5 Constants & LogMessages hygiene
**Where:** `src/Core/Constants/AppConstants.cs` (395), `src/Core/Logging/LogMessages.cs` (336).
**Why:** growing single files; consider partial-class split by domain area (Copy / Node / PropFirm /
Ai) for both — purely organizational, low risk, aids agent navigation.
**Done-when:** split by area, no id/eventId collisions, build green.

---

## 2. Optimization

### 2.1 EF Core query audit
**Why:** DDD app with many list pages + pollers; classic wins: missing `AsNoTracking` on read-only
queries, over-fetching (no projection to DTO), N+1 from lazy patterns, `AsSplitQuery` for multi-include,
pagination on unbounded lists (dashboard/activity feed, instance tables, copy execution log).
**Where:** `src/Infrastructure/Persistence/Repositories.cs`, `src/Web/Endpoints/DashboardQuery.cs`,
copy/instance/prop-firm read paths.
**Plan:** enumerate every `DataContext` read; classify tracking-needed vs read-only; add
`AsNoTracking()`, projections, `Take/Skip` where a list can grow. Use the `dotnet-data:optimizing-ef-core-queries`
skill. Add EF `.LogTo` in a diagnostic run to spot N+1.
**Done-when:** read-only queries no-track + projected; unbounded lists paged; integration tests assert
row counts / query shape where meaningful; no behavior change.

### 2.2 Allocation / async hot paths
**Why:** pollers + copy host run continuously. Use `dotnet-diag:analyzing-dotnet-performance` on
`src/Nodes/**` (pollers, `ContainerCommandHelpers`), `src/CopyEngine/**`, `src/CTraderOpenApi/**`.
Targets: string concat in loops, LINQ in hot loops, `Task` where `ValueTask` fits, sync-over-async,
`CancellationTokenSource.Cancel()` → `CancelAsync()`, buffer reuse in the TCP/SSL transport.
**Where:** `src/CTraderOpenApi/Transport/TcpSslOpenApiTransport.cs`, `OpenApiConnection.cs`, pollers.
**Done-when:** flagged anti-patterns fixed on hot paths; if a change is perf-claimed, back it with a
BenchmarkDotNet micro-bench (`dotnet-diag:microbenchmarking`) — no guessing.

### 2.3 Build/CI time
**Why:** `dotnet test` runs all four tiers serially in one CI job (see §4). Split tiers so unit
feedback is fast; cache NuGet; `--no-restore/--no-build` reuse. Optionally graph build.
**Done-when:** CI unit tier < a few min, gated before slow tiers; measured before/after.

### 2.4 Frontend/site
**Why:** `npm run build` already reports broken links; ensure it runs on PR (see §3/§4). Check
`site.css`/`service-worker.js` for cache-busting correctness (PWA). Low priority.

---

## 3. Documentation

### 3.1 Architecture overview + diagram (missing)
**Why:** module map lives only in CLAUDE.md prose. New agents/humans lack a single
"how a build/backtest/copy request flows across Web → Nodes → ExternalNode" picture.
**Add:** `website/docs/architecture.md` (canonical) with a Mermaid diagram: aggregates, dispatch
(`ContainerDispatcherFactory` → Http/Local), copy engine host, node HTTP+JWT, MCP, AI gating. Link
from `intro.md` + sidebar.
**Done-when:** page builds (Docusaurus mermaid), sidebar id added, no broken links.

### 3.2 ADRs for non-inferable decisions
**Why:** CLAUDE.md "Non-inferable design decisions" section is gold but buried. Promote each to a
short ADR so the *why* is discoverable and versioned.
**Add:** `docs/adr/` (or `website/docs/adr/`): TPH-instance-replaces-entity, external-node-no-shell-JWT,
CBotBuilder-on-web-host-sandbox, raw-HTTP-Anthropic (not SDK), one-SaveChanges-per-aggregate,
in-place-token-swap. One page each, "Context / Decision / Consequences".
**Done-when:** ADR index + entries published, cross-linked from CLAUDE.md.

### 3.3 Agent-onboarding doc
**Why:** rules are spread across root + 4 nested CLAUDE.md + AGENTS.md + skills. A single
"start here for agents" map (which skill/tool for which job, the mandate checklist, the build/sweep
commands) reduces cold-start cost.
**Add:** `AGENTS.md` already exists — audit it against current mandates; add a "common tasks →
commands/skills" table and link the new `scripts/` + slash commands from §4/§5.
**Done-when:** AGENTS.md is a complete index; no stale command paths.

### 3.4 Doc/code drift + stub sweep
**Why:** top-level `docs/` is meant to be redirect stubs except `docs/design/`; but `docs/features/`,
`docs/deployment/`, `docs/operations/`, `docs/testing/`, `docs/plans/`, `docs/ui-guidelines.md` still
hold **real** content duplicated from `website/docs/`. Two sources of truth = drift risk (mandate #8).
**Plan:** diff `docs/**` vs `website/docs/**`; where website is canonical, reduce `docs/**` to true
redirect stubs (or delete + rely on site). Confirm `docs/plans/copy-trading-remaining-work.md` vs
`plans/` isn't stale.
**Done-when:** single source of truth; `docs/` non-design content is stubs only; links resolve.

### 3.5 Per-file / module READMEs where density is high
**Why:** `src/Nodes`, `src/CopyEngine`, `src/CTraderOpenApi` have dense orchestration with no local
README. A 15-line `README.md` per hot module (responsibilities, entry points, gotchas) speeds agents.
**Done-when:** README in the 3 densest non-CLAUDE'd modules.

---

## 4. Agentic infrastructure (make Claude Code work standard & easy here)

### 4.1 CI is the biggest gap — it does NOT enforce repo mandates
Current `.github/workflows/ci.yml` = restore + build + `dotnet test`. Missing vs CLAUDE.md law:
- **No analyzer sweep** — mandate requires `dotnet format analyzers --verify-no-changes --severity info`.
  CI can pass while info-level CA/IDE debt lands.
- **No Playwright browser install** — E2E (`AppFixture` launches a real browser) runs under
  `dotnet test` with no `playwright install`. E2E tier is effectively broken/unrun in CI, violating the
  three-tier mandate. Add the install step (or `Microsoft.Playwright` CLI) before the E2E tier.
- **No tier separation** — all tiers in one serial run; no fast unit gate, no fail-fast.
- **No NuGet cache** — every run restores cold.
- **No test artifacts** — no `.trx`/coverage upload; failures are console-only.
- **No `website` build/link-check on PR** — `npm run build` (reports broken links) only runs on the
  deploy workflow (`docs.yml`, push to main). PRs touching docs aren't link-checked.
- **No `dotnet format --verify-no-changes`** style gate (whitespace/usings).
**Plan — restructure CI into jobs:**
1. `build` (restore w/ cache → build Release, 0-warn).
2. `analyzer-sweep` (the mandated sweep on all `src/**` projects; fail on new info CA/IDE).
3. `test-unit` (fast) → `test-integration` (Testcontainers, needs Docker on runner) →
   `test-e2e` (install Playwright browsers first) → optional `test-stress` (DST) nightly.
4. `docs` (PR job: `cd website && npm ci && npm run build` — link-check).
Upload `.trx` + coverage as artifacts; gate merge on unit+integration+e2e.
**Done-when:** a PR that adds an info-level analyzer violation, an unrun E2E, or a broken doc link
**fails CI**. Mandate = enforced, not just documented.

### 4.2 `.claude/commands/` slash commands (repo-scoped, checked in)
**Why:** the recurring agent chores are long multi-flag commands. Turn them into `/slash` commands so
any agent/human runs them identically.
**Add** under `.claude/commands/`:
- `/sweep` — analyzer sweep on given/changed projects, both surfacing modes from CLAUDE.md.
- `/test-tier <unit|integration|e2e|stress>` — run one tier with the right filter/browser install.
- `/migration <Name>` — the exact `dotnet ef migrations add … -o Persistence/Migrations` invocation.
- `/site` — `cd website && npm run build` (link-check) + serve.
- `/screenshots` — the `CAPTURE_SCREENSHOTS=1 dotnet test … ReadmeScreenshotsTests` + copy step.
- `/done` — run the full definition-of-done checklist (build → sweep → problems → tests → docs check).
**Done-when:** commands exist, documented in AGENTS.md, produce correct invocations on this OS (bash).

### 4.3 Repo `scripts/` task runner (CI + local + slash-command shared)
**Why:** single source for the mandated commands so CI, slash commands, and humans don't drift.
**Add:** `scripts/sweep.sh` (+ `.ps1`), `scripts/test.sh <tier>`, `scripts/migration.sh`,
`scripts/site.sh`. `scripts/k8s-e2e.*` already sets the pattern. Keep cross-platform (bash + ps1).
**Done-when:** CI §4.1 calls these scripts; slash commands call these scripts; documented.

### 4.4 `.mcp.json` (project-scoped MCP config)
**Why:** the whole workflow depends on Rider (`jetbrains`) MCP but that's user-level only. A checked-in
`.mcp.json` documents/standardizes the MCP servers the repo expects (jetbrains, air-api, youtrack) so a
fresh clone onboards agents consistently (with per-user secrets left out / via env).
**Done-when:** `.mcp.json` present with non-secret server declarations; secrets via env/`${VAR}`;
README note on setup.

### 4.5 Pre-commit / local gate + Dev Container
**Why:** catch zero-warning + sweep failures before CI. Optional but high-leverage.
**Add:** a lightweight `.git/hooks` installer script (or `dotnet format` pre-commit), and a
`.devcontainer/` (SDK 10, Node 20+, Docker-in-Docker, Playwright deps) so E2E/integration run anywhere
identically — including cloud agents.
**Done-when:** documented opt-in hook; devcontainer builds & runs all tiers.

### 4.6 CODEOWNERS + PR checklist enforcement
**Why:** `.github/PULL_REQUEST_TEMPLATE.md` exists; add `CODEOWNERS` and a CI job that asserts the DoD
checkboxes / requires the analyzer+test jobs green. Low effort, keeps agents honest.

---

## 5. Test gaps (unit · integration · E2E)

### 5.1 E2E not enforced in CI (critical)
Covered in §4.1 — the tier exists (42 files) but no browser install ⇒ not actually validating in CI.
**Done-when:** E2E runs green in CI on every PR (mobile + desktop projects per mandate #2).

### 5.2 Failure-path coverage audit (mandate #2 "failure paths count")
**Why:** mandate lists required failure scenarios: connection drop, order rejection, desync/resync,
token rotation, node death + lease reclaim. Some exist (`OpenApiResilience`, `DatabaseResilience`,
`NodeAgentHttpResilience`, `LiveCopyChaos`, `NodeInstanceReclaimer`, `CopyLeaseReclaimStress`). **Map
each required failure path → a test; fill the holes.** Suspected thin areas:
- CBot **build failure** paths (compile error surfaces to UI/instance) — verify E2E + integration.
- **MCP tool** failure/auth-reject paths (`McpKeyAuthHandler`) — integration coverage?
- **AI disabled vs error vs rate-limit** — `AiHttpResilience` exists; confirm 429/timeout/malformed.
- **PropFirm** breach → alert → node-lease-loss recovery end-to-end.
**Done-when:** a checklist table (scenario → test file) in `tests/CLAUDE.md`, no empty rows.

### 5.3 New-route smoke coverage (mandate: new route → `PageSmokeTests`)
**Why:** verify every routable page in `src/Web/Components/Pages/**` (Ai/*, Compliance, PropFirm,
PropGuard, OpenApiApplications, FeatureSettings, Alerts, Agent, Optimize…) has a `PageSmokeTests` /
`PageHealthTests` entry. Add the missing ones.
**Done-when:** count(pages) == count(smoke entries); gap = 0.

### 5.4 `DateTime.UtcNow` guard test (mandate #4)
**Why:** the ban is only enforced by review. **Add an architecture test** (unit) that scans compiled
IL / source for `DateTime.UtcNow|DateTime.Now|DateTimeOffset.UtcNow` outside test projects and fails.
Same style guard can assert `src/Core` has zero infra deps (mandate #1) and no `ILogger.Log*` direct
calls (mandate #6).
**Done-when:** architecture-guard test project/file green; introducing a violation fails it.

### 5.5 Refactor-support unit tests (from §1)
**Why:** decomposing `CopyEngineHost` (§1.2) should *add* direct unit tests for the extracted
`DestinationBook`, `DestinationHealth`, mirror strategies, guard evaluator — currently only tested
transitively via host + DST.
**Done-when:** each new collaborator has focused unit tests; DST still 23/23.

### 5.6 Coverage visibility
**Why:** no coverage collected. Add Coverlet + artifact upload (§4.1) and a coverage summary comment;
not a hard gate initially, just visibility to find blind spots.
**Done-when:** coverage report per tier in CI artifacts.

---

## Sequencing (suggested)

1. **Infra first (unblocks everything):** §4.1 CI restructure + §4.3 scripts + §4.2 slash commands +
   §5.4 architecture-guard tests. Now mandates are machine-enforced.
2. **Docs consolidation:** §3.4 stub sweep + §3.1 architecture page + §3.3 AGENTS audit.
3. **Low-risk refactors:** §1.1 Entities split, §1.5 constants split, §1.3 endpoint groups.
4. **High-value/med-risk:** §1.2 CopyEngineHost decomposition (under green DST) + §5.5 tests.
5. **Optimization pass:** §2.1 EF audit + §2.2 hot paths (measured).
6. **Test gap fill:** §5.2 failure-path map + §5.3 route smoke + §5.6 coverage.

Each numbered item = its own small commit direct to `main` (per [[commit-direct-to-main]]), docs in the
same commit (mandate #8), analyzer sweep + `get_file_problems` clean before "done".

## Explicitly out of scope
cTrader-Console optimization feature · email/SMTP · strong-typed EF ID converters · per-user quotas
(all "Deliberately not done" in CLAUDE.md). Don't add them under the banner of "refactor".
