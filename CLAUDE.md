# CLAUDE.md

Guidance for future Claude Code sessions on this repo.

> **MANDATORY — Domain-Driven Design.** Solution developed **strictly** under DDD.
> Before you write **or modify any C# under `src/`**, invoke **`ddd-dotnet`** skill and obey it.
> Binding rules live in [`## Testing & docs — MANDATORY

Binding, no exceptions, no "small change" skips. This is a trading and financial app; an untested
bug can cost users money, and stale docs mislead the AI agents that build on this repo.

1. **Every feature or fix ships all three test tiers.** Unit **and** integration **and** E2E —
   whichever the change can be exercised at, it must be. New behavior with only one tier is not
   "done". Unit tests assert invariants/transitions (not getters); integration uses real Postgres
   (Testcontainers); E2E drives the API/UI. Copy-trading and distributed/multi-node behavior must
   be covered including failure paths (connection drop, order-placement failure, desync/resync,
   token rotation/invalidation, node death + lease reclaim). `dotnet test` green before "done".

   **Playwright E2E is MANDATORY for every user-facing feature.** Any new page, dialog, action, nav
   entry, or endpoint a page calls ships a Playwright test in `tests/E2ETests` that drives the real UI
   through `AppFixture` — create/edit/save round-trips, the happy path, and that the page renders without
   tripping the Blazor error UI. A new API-only copy feature ships an authenticated API-level E2E hitting
   its endpoint. No UI/feature is "done" without it; add it in the same commit. `PageSmokeTests` navigates
   every static route — add new routes to it.
2. **The fake trading simulator stays cTrader-faithful.** `tests/UnitTests/CopyTrading/FakeTradingSession.cs`
   must faithfully mimic real cTrader behavior (order types, expiry, slippage, partial close, SL/TP
   amend, trailing, disconnect/reconnect desync, token swap, rejections). Extend it when you add a
   behavior; do not weaken it to make a test pass.
3. **Docs stay in sync.** Each major feature has a doc under `docs/features/`. Any change to a
   feature updates its `docs/features/*.md` **in the same commit** (and `docs/` deployment/ops docs
   when behavior there changes). No feature is "done" until its doc matches the code.

## Domain-Driven Design — MANDATORY`](#domain-driven-design--mandatory)
> below. No feature/fix/refactor "done" until it passes DDD checklist there. Overrides
> convenience — no anemic entities, primitive-obsessed signatures, or domain logic in
> endpoints/services because "quicker".

## What this repo is

Multi-tenant Blazor Server + Minimal API app. Builds, runs, backtests cTrader cBots
through `ghcr.io/spotware/ctrader-console`, scheduled across remote nodes (each running
`ExternalNode` HTTP agent) and/or local web host. In-browser Monaco editor builds cBot
projects (C# + Python, both .NET) via `dotnet build`. Also mirrors trades across accounts
(copy trading) over the cTrader Open API.

## Solution structure

```
global.json                 — pins .NET 10 SDK
Directory.Build.props       — TreatWarningsAsErrors, AnalysisLevel, ImplicitUsings
Directory.Packages.props    — Central Package Management (all versions live here)
src/
  AppHost/       — Aspire orchestrator: Postgres, Web, MCP, pgAdmin.
  Core/          — pure domain, no infra deps.
    Entities.cs             — AuditedEntity/ISoftDeletable + TPH instance hierarchy
                               (Run/Backtest x Pending/Scheduled/Starting/Running/
                               Stopping/Stopped/Failed)
    Enums.cs                — UserRole, NodeMode, NodeStatus, InstanceType/Status, CBotLanguage
    StrongIds.cs             — strong ID structs + Email/Symbol/Timeframe/DockerImageTag
    Abstractions.cs          — ISecretProtector, IPasswordHasher, INodeScheduler,
                               IContainerDispatcher(Factory), IGhcrTagProvider, ICurrentUser
    Options/AppOptions.cs    — binds "App" section, incl. nested LocalNodeOptions + DiscoveryOptions + AiOptions
    Ai/AiContracts.cs        — IAiClient, IAiFeatureService + AI DTOs (AiTextRequest/Result/Image/InstanceContext)
    Constants/AppConstants.cs — all magic strings
    Logging/LogMessages.cs   — source-generated ILogger extensions
    NodeAgent/AgentContracts.cs — DTOs for the ExternalNode HTTP API
  Infrastructure/ — EF Core, DataProtection, encryption, GHCR client.
    Persistence/DataContext.cs — soft-delete query filter + Delete→Modified SaveChanges override
    Persistence/Migrations/, DesignTimeDbContextFactory.cs
    Security/Argon2PasswordHasher.cs, DataProtectionSecretProtector.cs
    Ghcr/GhcrTagProvider.cs  — anonymous tag list, 1h MemoryCache
    Ai/AnthropicAiClient.cs  — IAiClient impl, raw HTTP to Anthropic Messages API (typed HttpClient)
    Ai/AiFeatureService.cs   — IAiFeatureService: 10 AI features + AiPrompts (system prompts)
    Aspire/ServiceDefaultsExtensions.cs — OTel, health checks, service discovery, resilience
    DependencyInjection.cs  — AddInfrastructure()
  Nodes/         — cross-node orchestration.
    NodeScheduler.cs           — picks least-loaded eligible node, honors MaxInstances
    ContainerDispatcherFactory.cs — routes to Http (remote) or Local dispatcher by Node type
    HttpContainerDispatcher.cs  — calls the ExternalNode agent's HTTP API for remote nodes
                                  (start/stop/status/report/logs/stats/clean); mints a short-lived
                                  per-node HS256 JWT signed with that node's shared secret
    LocalContainerDispatcher.cs — same ops via local `docker` process calls, for the web host's LocalNode
    NodeStatsPoller.cs          — BackgroundService, polls per AppOptions.NodeStatsPollInterval
    NodeHeartbeatMonitor.cs     — BackgroundService; marks self-registered RemoteNodes unreachable when
                                  their heartbeat exceeds AppOptions.Discovery.HeartbeatTtl
    RunCompletionPoller.cs / BacktestCompletionPoller.cs — reconcile exited run/backtest containers
    AiRiskGuard.cs              — BackgroundService; when AppOptions.Ai.RiskGuardEnabled, AI-assesses running bots
    ContainerCommandHelpers.cs  — BuildConsoleArgsList (tokens) / BuildConsoleArgs (shell string)
    Builder/CBotBuilder.cs      — sandboxed builder; `docker run` an SDK image + dotnet build
    Builder/Templates/          — embedded C#/Python starter project files
  ExternalNode/  — standalone HTTP node agent (deployed on remote servers).
    Program.cs                  — minimal API, JWT-bearer auth, image-prefix guard, Serilog
    NodeRegistrationClient.cs   — BackgroundService; self-registers + heartbeats to the main node's
                                  /api/nodes/register when NodeAgent:MainUrl+AdvertiseUrl are set
    DockerService.cs            — pulls image + runs/stops/inspects containers via the docker CLI,
                                  stateless (looks containers up by `app.instance` label)
    NodeAgentOptions.cs, Dockerfile
  Web/           — Blazor Server SSR + Minimal API + SignalR.
    Program.cs               — binds AppOptions, cookie auth, role policies, hosted services
                               (OwnerSeeder, LocalNodeSeeder, InstanceReconciler), endpoint maps
    Auth/                    — HttpCurrentUser, OwnerSeeder, LocalNodeSeeder, InstanceReconciler,
                               CookieForwardingHandler
    Endpoints/               — Auth, CBot, Builder, ParamSet, Instance, Node, User, Ctid,
                               McpKey, Dashboard, Image, Ai (/api/ai/* — generate/generate-project/
                               review/analyze-backtest/optimize-params/optimize-run/post-mortem/
                               sentiment/vision/curate)
    Hubs/LogsHub.cs          — SignalR streaming of `docker logs -f`
    Components/Pages/        — CBots (list/build/run/edit; per-cBot Parameter Sets dialog),
                               BuilderEditor (Monaco IDE), Run, Backtest, Nodes, Accounts (Trading
                               Accounts), Users, InstanceTable/Detail, Login, Index (live dashboard),
                               Mcp, Assistant (AI: Build Bot/codegen/review/sentiment/...),
                               AiSettings (/settings/ai), FeatureSettings, OpenApiApplications
                               (/settings/openapi), Compliance (/settings/legal)
    Components/Dialogs/NewProjectDialog.razor
    Components/              — MudBlazor + custom cTrader-style dark theme
  Mcp/           — MCP HTTP+SSE server.
    Auth/McpKeyAuthHandler.cs — bearer token `mcpk_<hex>`, SHA-256 hashed, prefix-indexed
    Tools/                    — CBotTools, InstanceTools, AiTools (generate/review/sentiment/analyze-backtest)
tests/
  UnitTests/         — xUnit + FluentAssertions + NSubstitute
  IntegrationTests/  — Testcontainers PostgreSQL
```

## Conventions

- `TreatWarningsAsErrors=true`, no `NoWarn` — fix real warnings.
- Config via `IOptionsMonitor<AppOptions>` (binds `App` section); no `cfg["Key"]` in business code.
- Log via `LogMessages` source-generated extensions (`Core/Logging/LogMessages.cs`) — never `ILogger.LogInformation(...)` directly.
- Magic strings live in `Core/Constants/`.
- Soft delete: entities inherit `AuditedEntity`/`ISoftDeletable`; `DataContext` global query filter + converts `Deleted` → `Modified`+`IsDeleted` in `SaveChanges`.
- Strong IDs/value objects in `Core/StrongIds.cs`; entities still use `Guid` for EF mapping (value-converter migration follow-up).
- Never log/store secrets plaintext — use `ISecretProtector` with `EncryptionPurposes` strings. Data Protection key ring PFX-encrypted via base64 env var.
- Health checks: `AddHealthChecks().AddNpgSql(...)`. `/health` (readiness) + `/alive` (liveness) mapped in **all** environments (K8s/cloud probes); MCP exposes `/version`.
- Logging: Serilog (compact JSON stdout) in Web/Mcp/CopyAgent via `Infrastructure/Observability/SerilogConfigurator` or inline (ExternalNode); every event stamped with OTel resource attrs (service.name/version/namespace, deployment.environment) + `trace_id`/`span_id` (`ActivityEnricher`) for collector-less log↔trace correlation. OTLP sink when `OTEL_EXPORTER_OTLP_ENDPOINT` set. OTel metrics+traces via `Infrastructure/Observability/OpenTelemetryConfigurator.AddAppTelemetry` (Web+Mcp) — exports OTLP and/or **natively to Azure Monitor** when `APPLICATIONINSIGHTS_CONNECTION_STRING` set. Cloud IaC: Azure bicep provisions App Insights; AWS terraform runs an ADOT sidecar (X-Ray + CloudWatch EMF). Still author app logs through `LogMessages`.
- Web security (`Web/Program.cs`): auth cookie `HttpOnly` + `SameSite=Lax` + `SecurePolicy=Always`; `Web/Security/SecurityHeaders.cs` adds `X-Content-Type-Options`/`X-Frame-Options`/`Referrer-Policy`/`Permissions-Policy`; login/auth group rate-limited (`RateLimitPolicies.Auth`, fixed-window per-IP); OpenAPI mapped Development only.

## UI design guide (MANDATORY for Blazor pages)

- **All "add/create/edit/new" actions use a MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`),
  never an inline form/card embedded in the page. A page-level toolbar button (`New X`, top-right via
  `MudSpacer`) opens the dialog; the dialog owns the form + validation and returns a small result record
  (see `Components/Dialogs/*` — `NewProjectDialog`, `NewCidAccountDialog`, `OpenApiAppDialog`,
  `NewCopyProfileDialog`). The page does the HTTP call with the returned data, then reloads.
- **Do not reintroduce the old inline "card with fields + Create button at top of page" pattern.** It was
  removed from Copy Trading / Open API on purpose. New pages match the dialog convention from day one.
- Dialog files live in `Web/Components/Dialogs/`, expose `[Parameter]`s for edit/prefill state, and a
  nested `public sealed record …Result(...)` for the payload. Confirm-destructive actions may use a
  simple dialog too, but list actions (start/stop/delete on a row) stay inline as icon buttons.

## Common commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AppHost   # Aspire orchestration
dotnet run --project src/Web       # Web app only
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
dotnet ef database update    -p src/Infrastructure -s src/Infrastructure
```

## Notable design decisions

- `CBotBuilder` runs on web host, not remote nodes — Web container needs Docker socket access.
- Run/backtest containers run on nodes picked by `NodeScheduler`, dispatched via `ContainerDispatcherFactory` to `HttpContainerDispatcher` (remote, via `ExternalNode` agent HTTP API) or `LocalContainerDispatcher` (web host's own `LocalNode`, seeded by `LocalNodeSeeder`).
- External nodes get **no** SSH/shell access. Main talks to `ExternalNode` agent over HTTP; each `RemoteNode` stores `BaseUrl` + encrypted per-node shared secret. Every request carries short-lived HS256 JWT (`iss=app-main`, `aud=app-node`, 5-min expiry) signed with node's secret; agent validates. Agent only runs images matching `AllowedImagePrefix` (default `ghcr.io/spotware/`), execs docker via `ArgumentList` (no shell), stateless (finds containers by `app.instance` label → survives restart). Deploy with docker daemon available; run container `--privileged` (starts local dockerd) or run binary on host with docker.
- Node auto-discovery: agents self-register + heartbeat to main's `POST /api/nodes/register` (join-token bearer, constant-time compare, protocol-version gated). Main upserts `RemoteNode` **by name** (stable identity across IP changes); auto-registered nodes share cluster secret (`App:Discovery:JoinToken`) as dispatch secret. `RemoteNode.SelfRegister/RecordHeartbeat/MarkUnreachable/IsHeartbeatStale` are domain methods; `IsActive/AcceptsRun/AcceptsBacktest` gate on `IsReachable` (heartbeat flag, **not** TPH type change — `OfflineNode`/`DecommissioningNode` remain admin states). `NodeHeartbeatMonitor` reconciles staleness. Gated on `App:Discovery:Enabled`; manual `POST /api/nodes` still works. Docs: `docs/operations/node-discovery.md`.
- Deploy artifacts: `Dockerfile.{web,mcp,node-agent}`, root `docker-compose.yml`+`.env.example` (local), `deploy/helm/cmind` (K8s; node agents = privileged StatefulSet + headless Service for per-pod addressability), `deploy/azure/main.bicep` (Container Apps), `deploy/aws` (ECS Fargate + RDS Terraform). Fargate/Container-Apps can't run privileged node agents → agents go on AKS/EKS/EC2/VM. See `docs/deployment/`.
- cTrader Console backtest CLI (verified live): requires `--data-mode` (default `m1`), dates `dd/MM/yyyy HH:mm`, `params.cbotset` is JSON (`{"Parameters":{...}}`) passed as positional arg; `run` rejects `--data-dir` (backtest-only). See `ContainerCommandHelpers`.
- `BacktestCompletionPoller` polls `RunningBacktestInstance` on `AppOptions.BacktestCompletionPollInterval` (backtest containers self-exit via `--exit-on-stop`). `RunCompletionPoller` does same for `RunningRunInstance`, using `IContainerDispatcher.GetExitCodeAsync` → exit 0/null = `StoppedRunInstance`, non-zero = `FailedRunInstance`. Backtest: report present → `CompletedBacktestInstance` (stores `ReportJson`); missing → `FailedBacktestInstance`.
- Equity curve for `InstanceDetail` chart parsed from `CompletedBacktestInstance.ReportJson` by `ContainerCommandHelpers.ParseEquityCurve`. Real report nests points at `equity.points[]` (`{balance,minEquity,maxEquity,timestamp}`); parser also scans root keys `equityHistory`/`equityCurve`/`history`/`equity`.
- AI layer: `IAiClient` calls Anthropic Messages API over **raw HTTP** (typed `HttpClient`), not the Anthropic SDK — deliberate, avoids fragile new NuGet dep + offline-restore risk; app already uses `IHttpClientFactory` everywhere. All AI gated on `AppOptions.Ai.ApiKey`: unset → every feature returns `AiResult.Fail(disabled)`, app runs unchanged (no key needed for build/test/E2E). `AiFeatureService` = single orchestrator shared by Web endpoints, MCP `AiTools`, `AiRiskGuard` background service. Market sentiment uses server-side `web_search` tool; chart-vision passes base64 image block. `generate-project` runs generate → build (`CBotBuilder`) → AI-fix self-repair loop (≤3 attempts); `optimize-run` is closed loop — AI proposes param sets, each persisted + backtested across nodes via `INodeScheduler` (mirrors `InstanceEndpoints` backtest-launch path).
- MCP server separate process → scales/redeploys independently of Web. Stateless HTTP transport + `AddHttpContextAccessor` so tool calls see authenticated user.
- EF TPH gotcha: don't add `e.Property<T>(nameof(Subclass.Prop)).IsRequired(false)` from a *base* type's `EntityTypeBuilder` for a property on a *derived* TPH type — silently produces a property EF never persists (bit us on old `RemoteNode` SSH fields). TPH makes subclass-only properties nullable at column level automatically; no extra config.
- EF SQL-translation gotcha: nested `(i as T) != null ? (i as T)!.Prop : ...` chains in an `IQueryable` `.Select()` don't reliably translate (silent wrong/null values vs real Postgres). Materialize with `ToListAsync()` first, switch in C# (see `InstanceEndpoints.GetStartedAt`/`GetStoppedAt`).
- EF TPH `OfType<Intermediate>()` gotcha: `db.Nodes.OfType<RemoteNode>()` over the soft-delete query filter does **not** translate on Npgsql (throws at runtime, 500 — caught in K8s/Docker E2E, missed by in-memory unit tests). Query without narrowing (by unique key, then pattern-match `is RemoteNode`) or enumerate concrete leaf subtypes + `ToListAsync()` + `.Cast<>()` in memory (see `NodeEndpoints.RegisterNodeAsync`, `NodeHeartbeatMonitor`).
- Don't project a full entity with a one-to-one nav cycle (`Node.LatestStats`/`NodeStats.Node`) into an API response — System.Text.Json has no cycle detection, serializes to `MaxDepth` and 500s. Project scalar fields.
- `Instance.IsActive`/`IsTerminal` are C#-only computed (per-subclass override), not mapped columns — filtering on them in `IQueryable` throws at translation. Materialize first.
- Instance state transitions replace the entity (TPH discriminator can't change) → instance **id changes** starting→running→terminal. Container id stable and carried over; HTTP agent keyed by container id for status/report/stop/logs.

## Known gaps / needs follow-up

- **Builder isolation** (resolved): `CBotBuilder` runs `dotnet build` inside a throwaway container (`DockerCommands.RunBuild` + `AppOptions.BuildImage`, work dir bind-mounted at `/work`) → untrusted user MSBuild targets can't reach host FS/network. Restore cached across builds via shared `app-nuget-cache` volume. Web host still needs Docker socket access.
- **Remote-node trust**: `ExternalNode` agent runs whatever image+args the JWT-authenticated main node sends, constrained only by `AllowedImagePrefix`. Agent must run on trusted host with docker; shared secret is sole credential — keep ≥32 chars, TLS in production (agent behind reverse proxy), rotate by updating both node's stored secret and agent's `NodeAgent:JwtSecret`.

## Deliberately not done

No optimization (unsupported by cTrader Console) · no email/SMTP (manual reset + forced change via `MustChangePassword`) · no strong-typed EF ID converters yet · no per-user quotas.

## How to extend

1. Entity in `Core/Entities.cs` (inherit `AuditedEntity`).
2. Strong ID in `Core/StrongIds.cs`.
3. Constants in `Core/Constants/AppConstants.cs`.
4. Minimal API group in `Web/Endpoints/`.
5. Blazor page in `Web/Components/Pages/`.
6. `BackgroundService` + DI wiring if background work involved.
7. Regenerate EF migration.

## Implementation workflow

1. **Understand** — explore affected layers, read CLAUDE.md, find analogous implementation to template.
2. **Plan** — list changes by layer (files, DI, tests); agree with user before coding.
3. **Implement** — non-trivial work: alternate writer/reviewer agents per layer (style, naming, disposal symmetry, DI, consistency with analog); skip for mechanical changes.
4. **Track & adapt** — note plan deviations; get approval before changing approach mid-flight.
5. **Verify** — clean `dotnet build`; unit tests for every new class mirroring source path under `UnitTests/`; `dotnet test` green incl pre-existing; final reviewer pass (disposal symmetry, style, no leftover TODOs/hardcoded values).
6. **Summarize** — changes, deviations + why, test results, known limits.

Checklist: tests written · `dotnet test` passes · no new warnings.

## Testing & docs — MANDATORY

Binding, no exceptions, no "small change" skips. This is a trading and financial app; an untested
bug can cost users money, and stale docs mislead the AI agents that build on this repo.

1. **Every feature or fix ships all three test tiers.** Unit **and** integration **and** E2E —
   whichever the change can be exercised at, it must be. New behavior with only one tier is not
   "done". Unit tests assert invariants/transitions (not getters); integration uses real Postgres
   (Testcontainers); E2E drives the API/UI. Copy-trading and distributed/multi-node behavior must
   be covered including failure paths (connection drop, order-placement failure, desync/resync,
   token rotation/invalidation, node death + lease reclaim). `dotnet test` green before "done".

   **Playwright E2E is MANDATORY for every user-facing feature.** Any new page, dialog, action, nav
   entry, or endpoint a page calls ships a Playwright test in `tests/E2ETests` that drives the real UI
   through `AppFixture` — create/edit/save round-trips, the happy path, and that the page renders without
   tripping the Blazor error UI. A new API-only copy feature ships an authenticated API-level E2E hitting
   its endpoint. No UI/feature is "done" without it; add it in the same commit. `PageSmokeTests` navigates
   every static route — add new routes to it.
2. **The fake trading simulator stays cTrader-faithful.** `tests/UnitTests/CopyTrading/FakeTradingSession.cs`
   must faithfully mimic real cTrader behavior (order types, expiry, slippage, partial close, SL/TP
   amend, trailing, disconnect/reconnect desync, token swap, rejections). Extend it when you add a
   behavior; do not weaken it to make a test pass.
3. **Docs stay in sync.** Each major feature has a doc under `docs/features/`. Any change to a
   feature updates its `docs/features/*.md` **in the same commit** (and `docs/` deployment/ops docs
   when behavior there changes). No feature is "done" until its doc matches the code.

## Time — MANDATORY (`TimeProvider`, never `DateTime.UtcNow`)

Binding, no exceptions. Wall-clock reads through `DateTime.UtcNow`/`DateTime.Now`/`DateTimeOffset.UtcNow`
are untestable — they make "now" non-deterministic, which produces flaky time-dependent tests (lease
expiry, heartbeat staleness, token rotation, order expiry). This is a trading app; time bugs cost money.

1. **Production code never calls `DateTime.UtcNow`/`DateTime.Now`/`DateTimeOffset.UtcNow`/`Now` directly.**
   Inject `System.TimeProvider` and read `timeProvider.GetUtcNow()`. Register the singleton
   `TimeProvider.System` in DI; take `TimeProvider` as a constructor dependency (ordered with the other
   service deps per Code style). Domain methods that need "now" take a `DateTimeOffset now` (or `DateTime`)
   parameter supplied by the caller — the aggregate does not read the clock itself.
2. **Tests never use `DateTime.UtcNow`/`DateTime.Now`.** Hardcode explicit timestamps
   (`new DateTimeOffset(2026, 07, 10, 12, 00, 00, TimeSpan.Zero)`) or drive a
   `Microsoft.Extensions.Time.Testing.FakeTimeProvider` so time is fully controlled. A test that reads the
   real clock can pass or fail depending on when it runs — that is a bug, not a test.
3. **When you touch time-dependent code, migrate it.** Existing `DateTime.UtcNow` call sites are debt;
   convert the one you touch to `TimeProvider`/injected-now, and add a `FakeTimeProvider`-driven test that
   asserts the boundary (e.g. lease reclaim exactly at expiry `<= now`). Leave it better than you found it.

## Domain-Driven Design — MANDATORY

Binding contract. `ddd-dotnet` skill = long-form playbook; this section = law. When they agree,
follow either. If you think a rule should be broken, **stop and ask the user** — don't break it silently.

### Ubiquitous language

- Names in code == names domain uses: `CBot`, `SourceProject`, `ParamSet`, `Instance`
  (Run/Backtest), `Node`, `AgentMandate`, `AgentProposal`, `AlertRule`, `TradingAccount`, `Ctid`.
- No technical synonyms for domain concepts (`InstanceRecord`, `CBotDto`, `NodeManager` where a
  domain term exists). DTOs at the edge fine but must not rename the concept.
- Backtest never a "job"; node never a "server"; param set never "config". Keep language stable
  across Core, Web, Mcp, tests.

### Layering (dependency rule — already enforced by project refs; keep it)

```
Core (domain)          ← zero infra deps. No EF, no HttpClient, no Docker, no ASP.NET, no Anthropic.
  ↑                       Entities, aggregates, value objects, domain events, domain services,
  │                       repository *interfaces*, domain exceptions.
Infrastructure/Nodes   ← EF Core, encryption, GHCR, Anthropic, Docker, node HTTP. Implements Core
  ↑                       interfaces (repositories, schedulers, dispatchers). Anti-corruption lives here.
Web / Mcp / ExternalNode ← application services / use-cases (endpoints, MCP tools, hosted services).
                           Orchestrate: load aggregate → call its methods → persist → dispatch events.
```

- **Domain logic never lives in an endpoint, MCP tool, Razor component, or `BackgroundService`.**
  Those are application/presentation layers: orchestrate, don't decide. Any `if` encoding a business
  rule belongs on an aggregate or domain service.
- Core stays pure. Need infra in the domain → you modeled it wrong; introduce an interface in Core,
  implement it outside.

### Aggregates & aggregate roots

- Every write goes through an **aggregate root**. Current roots: `AppUser`, `CTraderIdAccount`
  (owns `TradingAccount`), `CBot` (owns `ParamSet`), `CBotSourceProject`, `Instance`, `Node`
  (owns `NodeStats`), `AgentMandate` (owns `AgentProposal`), `AlertRule` (owns `AlertEvent`),
  `McpApiKey`. `AuditLog`/`AppSetting`/`InstanceLog` = append-only records, not aggregates.
- **One aggregate = one consistency boundary = one transaction.** A single `SaveChanges` mutates
  **one** aggregate instance. Touch two? Use a domain event + second use-case, or a process
  manager — never a fat transaction spanning roots.
- **Reference other aggregates by strong ID only** in new code (`CBotId`, `NodeId`, …), not by
  navigation property. Existing EF nav props may stay for query projection, but **do not add new
  cross-aggregate nav props** and never mutate another aggregate through one.
- Child entities (`ParamSet`, `TradingAccount`, `AgentProposal`, `AlertEvent`, `NodeStats`) reached
  and mutated **only through their root** (`cbot.AddParamSet(...)`, not `new ParamSet` + `context.Add`).

### Entities — rich, not anemic

- **No public setters on domain entities.** State changes through intention-revealing methods that
  enforce invariants (`cbot.Rename(name)`, `mandate.Enable()`, `alertRule.SetInterval(minutes)`).
  Setters become `private set` / `init`.
- Constructors/factories put the entity in a **valid** state or throw a **domain exception**
  (`DomainException` subtype in Core, not `ArgumentException` from a controller). Prefer a static
  `Create(...)` factory when construction has rules; keep a private ctor for EF.
- Invariants checked **inside** the aggregate, once, at the point of change — not re-validated in
  every caller. Callers trust the aggregate.
- TPH state hierarchies (`Instance`, `Node` states) = state pattern — **good, keep it**. A transition
  is a domain operation returning the next-state entity; centralize transition rules (which state may
  go where) in the domain, not scattered across pollers. Add a state/transition → model it as a
  method, not ad-hoc `new XxxInstance { … }` in a service.

### Value objects

- Model concepts, not primitives. `Email`, `Symbol`, `Timeframe`, `DockerImageTag` and all strong
  IDs are VOs — **immutable, equality by value, self-validating** (see `Core/StrongIds.cs`). Follow
  that template for new ones (money/percent/risk, drawdown, container id, url, secret material).
- **Ban primitive obsession in new/changed signatures.** No bare `string symbol`, `Guid id`,
  `double riskPercent`, `int intervalMinutes` crossing a domain boundary — wrap them. Percentages and
  risk numbers especially (`RiskPercentPerTrade`, `MaxDrawdownPercent`) deserve a VO that rejects
  out-of-range values at construction.
- VOs validate in their constructor and throw a domain exception; never carry an invalid value.

### Domain services, factories, domain events

- **Domain service** = stateless logic spanning aggregates or not belonging on one entity
  (`INodeScheduler` picking a node = canonical). Interface in Core; keep it infra-free. Reach for one
  only when behavior genuinely isn't a single aggregate's responsibility.
- **Domain events** signal "something happened" (`InstanceStarted`, `BacktestCompleted`,
  `AgentProposalAccepted`, `RiskThresholdBreached`). Raise from aggregate methods; collect on the
  entity; dispatch **after** successful `SaveChanges` (EF `SavingChanges`/interceptor or outbox).
  Cross-aggregate reactions + integration (SignalR, AI risk actions) subscribe to these instead of
  being inlined into the mutating use-case.
- **Factories** encapsulate multi-step/invariant-heavy creation. Repositories persist and retrieve
  **whole aggregates** — never leak `IQueryable` out of Core, never expose a generic `Repository<T>`
  that lets callers dodge aggregate methods.

### Repositories & persistence

- One repository interface **per aggregate root**, defined in Core, implemented in Infrastructure.
  Methods speak the domain (`GetActiveByUserAsync`, `AddAsync`, not `Query()`).
- Read models / list projections for the UI are **separate** from the write side — query EF directly
  in a read service/endpoint (CQRS-lite), returning DTOs. Don't force reporting queries through
  aggregate repositories, and don't reshape aggregates to make a screen easier.
- Persistence concerns (EF config, converters, TPH mapping, soft delete) stay in Infrastructure.
  The domain does not know EF exists.

### Bounded contexts / modules

- Organize the domain by module, not technical type. Current de-facto contexts:
  **Access** (users, MFA, viewer grants, MCP keys), **Authoring** (CBot, SourceProject, ParamSet,
  builder), **Execution** (Instance, Node, scheduling, dispatch), **Portfolio** (AgentMandate,
  proposals, decision journal), **Alerts**. Put new types in the module that owns the concept;
  cross-context calls go through a well-named interface, not a shared mutable entity.
- Anti-corruption layers already exist for external systems (cTrader Console CLI, GHCR, Anthropic,
  `ExternalNode` agent) — keep them: translate at the edge, never let their shapes leak into Core.

### Brownfield rule (repo is mid-migration)

Existing entities anemic (public setters, logic in pollers/services). Not required to boil the ocean, but:
- **New** aggregates/entities/VOs: full DDD from the start. No exceptions.
- When you **touch** an existing anemic entity for a feature/fix: encapsulate the part you touch —
  add the intention method, tighten those setters, move that rule into the aggregate. Leave it better
  than you found it; add no new anemic surface.
- Never cite "the rest of the code does it this way" to justify new anemic code. The old way is debt
  being paid down, not the standard.

### DDD definition-of-done checklist (all must hold before "done")

- [ ] New behavior lives on an aggregate/VO/domain service — **not** in an endpoint, tool, or hosted service.
- [ ] No new public setters on domain entities; state changes via intention methods that guard invariants.
- [ ] No new cross-aggregate navigation; other aggregates referenced by strong ID.
- [ ] Each `SaveChanges` mutates a single aggregate; multi-aggregate flows use domain events.
- [ ] No primitive-obsessed domain signatures; new domain concepts are value objects.
- [ ] Invariant violations throw a Core `DomainException`, not framework exceptions from outer layers.
- [ ] Ubiquitous-language names; no invented synonyms.
- [ ] Core still compiles with zero infra dependencies.
- [ ] Unit tests assert **invariants and transitions** on the aggregate (not just getters/setters).
- [ ] Existing anemic code you touched is left more encapsulated than before.

## Tooling rules

- Never `find`/`grep` via Bash — use `Glob`/`Grep`.
- Prefer `Read`/`Edit`/`Write` over `cat`/`sed`/`echo` redirection.
- Use JetBrains Rider MCP tools when available for navigation, tests, inspection.

## Fix ALL errors — including non-build-breaking ones — MANDATORY

Before declaring any work done you MUST fix **every** error/warning surfaced by the IDE analyzer, not
just what breaks `dotnet build`. This explicitly includes **`.razor` files** and analyzer/inspection
errors that don't fail the build (e.g. "Dereference of a possibly null reference" in a Razor lambda,
nullable warnings, unused-symbol errors). They are real errors and must be resolved.

- Run Rider `get_file_problems` on **every** file you touched — `.cs` **and** `.razor` — and fix all
  findings. If Rider's index is stale, confirm with a clean `dotnet build` (0 warnings, 0 errors), but
  never dismiss an analyzer error as "non-blocking".
- Razor nullable lambdas: guard the parameter (`v => $"{v?.Count ?? 0}"`), don't leave the deref.
- No "looks fine", no "small change", no "build passes so ignore the inspection" — zero outstanding
  problems is the bar.

**This includes Rider code-quality INSPECTIONS / SUGGESTIONS, not just errors + warnings.** `dotnet build`
(even with `TreatWarningsAsErrors`) does **not** surface these — only Rider `get_file_problems` does, so
running it on every touched file after finishing a task is non-negotiable. Resolve every inspection, e.g.:
- `CancellationTokenSource.Cancel()` inside an `async` method → `await CancelAsync()`.
- Prefer collection expressions (`[]`), pattern matching, `is null` / `is not null`, target-typed `new`.
- Remove redundant `.ToList()`/`ToArray()`, redundant `using`s, unused parameters/fields, dead code.
- `await using` / `await foreach` / `static` where Rider flags it.
Only a genuine false positive may remain, and only with an inline justification. Zero outstanding Rider
inspections on every touched file is the definition of done.

## Code style

- No comments except `TODO`/`FIXME`. No hardcoded strings — use a constants class.
- **Never `DateTime.UtcNow`/`DateTime.Now`.** Inject `TimeProvider` (`GetUtcNow()`) in code; hardcode
  timestamps or use `FakeTimeProvider` in tests. See [`## Time — MANDATORY`](#time--mandatory-timeprovider-never-datetimeutcnow).
- File-scoped namespaces. `sealed` by default. Injected fields `private readonly`.
- Early returns over nested `if`.
- Naming: `_camelCase` private fields, `I`-prefixed interfaces, `Async`-suffixed async methods, `var` when type obvious. Spell identifiers in full (no `Tp`/`Sl`/`Tcs`/`Vm`) — established initialisms (`UI`, `DI`, `ASP`, `DOM`) fine. Wire-format string literals keep literal form.
- Explicit access modifiers always (no implicit `internal`).
- Primary constructors; params ordered: regular service deps, then factory interfaces. Body order: field assignments → property init → event subscriptions last.
- Member order: fields → properties → events → methods, `Dispose()` last.
