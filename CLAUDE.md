# CLAUDE.md

Guidance for future Claude Code sessions on this repo.

> **MANDATORY — Domain-Driven Design.** This solution is developed **strictly** under DDD.
> Before you write **or modify any C# under `src/`**, invoke the **`ddd-dotnet`** skill and obey it.
> The binding rules live in [`## Domain-Driven Design — MANDATORY`](#domain-driven-design--mandatory)
> below. No feature, fix, or refactor is "done" until it passes the DDD checklist there. This
> overrides convenience — do not add anemic entities, primitive-obsessed signatures, or domain
> logic in endpoints/services because it is "quicker".

## What this repo is

Multi-tenant Blazor Server + Minimal API app. Builds, runs, backtests cTrader cBots
through `ghcr.io/spotware/ctrader-console`, scheduled across remote nodes (each running
`ExternalNode` HTTP agent) and/or local web host. In-browser Monaco editor builds cBot
projects (C# + Python, both .NET) via `dotnet build`.

Entirely Claude Code generated — no human-written code.

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
    Options/AppOptions.cs    — binds "App" section, incl. nested LocalNodeOptions + AiOptions
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
    RunCompletionPoller.cs / BacktestCompletionPoller.cs — reconcile exited run/backtest containers
    AiRiskGuard.cs              — BackgroundService; when AppOptions.Ai.RiskGuardEnabled, AI-assesses running bots
    ContainerCommandHelpers.cs  — BuildConsoleArgsList (tokens) / BuildConsoleArgs (shell string)
    Builder/CBotBuilder.cs      — sandboxed builder; `docker run` an SDK image + dotnet build
    Builder/Templates/          — embedded C#/Python starter project files
  ExternalNode/  — standalone HTTP node agent (deployed on remote servers).
    Program.cs                  — minimal API, JWT-bearer auth, image-prefix guard
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
    Components/Pages/        — CBots (list/build/run/edit), BuilderEditor (Monaco IDE), Run,
                               Backtest, ParamSets, Nodes, Accounts, Users, InstanceTable/Detail,
                               Login, Index, Mcp, Assistant (AI codegen/review/sentiment)
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
- Health checks: `AddHealthChecks().AddNpgSql(...)`, Development only.
- Web security wiring (in `Web/Program.cs`): auth cookie is `HttpOnly` + `SameSite=Lax` + `SecurePolicy=Always`; `Web/Security/SecurityHeaders.cs` adds `X-Content-Type-Options`/`X-Frame-Options`/`Referrer-Policy`/`Permissions-Policy`; login/auth group is rate-limited (`RateLimitPolicies.Auth`, fixed-window per-IP); OpenAPI is mapped in Development only.

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
- External nodes get **no** SSH/shell access. Main node talks to `ExternalNode` agent over HTTP; each `RemoteNode` stores `BaseUrl` + encrypted per-node shared secret. Every request carries short-lived HS256 JWT (`iss=app-main`, `aud=app-node`, 5-min expiry) signed with node's secret; agent validates. Agent only runs images matching `AllowedImagePrefix` (default `ghcr.io/spotware/`), execs docker via `ArgumentList` (no shell), stateless (finds containers by `app.instance` label → survives restart). Deploy with docker daemon available; run container `--privileged` (starts local dockerd) or run binary on host with docker.
- cTrader Console backtest CLI (verified live): requires `--data-mode` (default `m1`), dates `dd/MM/yyyy HH:mm`, `params.cbotset` is JSON (`{"Parameters":{...}}`) passed as positional arg; `run` rejects `--data-dir` (backtest-only). See `ContainerCommandHelpers`.
- `BacktestCompletionPoller` polls `RunningBacktestInstance` on `AppOptions.BacktestCompletionPollInterval` (backtest containers self-exit via `--exit-on-stop`). `RunCompletionPoller` does same for `RunningRunInstance`, using `IContainerDispatcher.GetExitCodeAsync` → exit 0/null = `StoppedRunInstance`, non-zero = `FailedRunInstance`. Backtest: report present → `CompletedBacktestInstance` (stores `ReportJson`); missing → `FailedBacktestInstance`.
- Equity curve for `InstanceDetail` chart parsed from `CompletedBacktestInstance.ReportJson` by `ContainerCommandHelpers.ParseEquityCurve`. Real report nests points at `equity.points[]` (`{balance,minEquity,maxEquity,timestamp}`); parser also scans root keys `equityHistory`/`equityCurve`/`history`/`equity`.
- AI layer: `IAiClient` calls the Anthropic Messages API over **raw HTTP** (typed `HttpClient`),
  not the Anthropic SDK — deliberate, to avoid a fragile new NuGet dependency + offline-restore
  risk; the app already uses `IHttpClientFactory` everywhere. All AI is gated on `AppOptions.Ai.ApiKey`:
  unset → every feature returns `AiResult.Fail(disabled)` and the app runs unchanged (no key needed
  for build/test/E2E). `AiFeatureService` is the single orchestrator shared by Web endpoints, MCP
  `AiTools`, and the `AiRiskGuard` background service. Market sentiment uses the server-side
  `web_search` tool; chart-vision passes a base64 image block. `generate-project` runs a
  generate → build (`CBotBuilder`) → AI-fix self-repair loop (≤3 attempts); `optimize-run`
  is a closed loop — AI proposes param sets, each is persisted + backtested across nodes via
  `INodeScheduler` (mirrors the `InstanceEndpoints` backtest-launch path).
- MCP server separate process → scales/redeploys independently of Web. Uses stateless HTTP transport + `AddHttpContextAccessor` so tool calls see the authenticated user.
- EF TPH gotcha: don't add `e.Property<T>(nameof(Subclass.Prop)).IsRequired(false)` from a *base* type's `EntityTypeBuilder` for a property on a *derived* TPH type — silently produces a property EF never persists (bit us on old `RemoteNode` SSH fields). TPH makes subclass-only properties nullable at column level automatically; no extra config.
- EF SQL-translation gotcha: nested `(i as T) != null ? (i as T)!.Prop : ...` chains in an `IQueryable` `.Select()` don't reliably translate (silent wrong/null values vs real Postgres). Materialize with `ToListAsync()` first, switch in C# (see `InstanceEndpoints.GetStartedAt`/`GetStoppedAt`).
- Don't project a full entity with a one-to-one nav cycle (`Node.LatestStats`/`NodeStats.Node`) into an API response — System.Text.Json has no cycle detection, serializes to `MaxDepth` and 500s. Project scalar fields.
- `Instance.IsActive`/`IsTerminal` are C#-only computed (per-subclass override), not mapped columns — filtering on them in `IQueryable` throws at translation. Materialize first.
- Instance state transitions replace the entity (TPH discriminator can't change) → the instance **id changes** starting→running→terminal. Container id is stable and carried over; the HTTP agent is keyed by container id for status/report/stop/logs.

## Known gaps / needs follow-up

- **Builder isolation** (resolved): `CBotBuilder` runs `dotnet build` inside a throwaway container (`DockerCommands.RunBuild` + `AppOptions.BuildImage`, work dir bind-mounted at `/work`) → untrusted user MSBuild targets can't reach host FS/network. Restore cached across builds via shared `app-nuget-cache` volume. Web host still needs Docker socket access.
- **Remote-node trust**: `ExternalNode` agent runs whatever image+args the JWT-authenticated main node sends, constrained only by `AllowedImagePrefix`. Agent must run on a trusted host with docker; shared secret is the sole credential — keep it ≥32 chars, TLS in production (agent behind reverse proxy), rotate by updating both node's stored secret and agent's `NodeAgent:JwtSecret`.

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

1. **Understand** — explore affected layers, read CLAUDE.md, find an analogous implementation to template.
2. **Plan** — list changes by layer (files, DI, tests); agree with user before coding.
3. **Implement** — non-trivial work: alternate writer/reviewer agents per layer (style, naming, disposal symmetry, DI, consistency with the analog); skip for mechanical changes.
4. **Track & adapt** — note plan deviations; get approval before changing approach mid-flight.
5. **Verify** — clean `dotnet build`; unit tests for every new class mirroring source path under `UnitTests/`; `dotnet test` green incl pre-existing; final reviewer pass (disposal symmetry, style, no leftover TODOs/hardcoded values).
6. **Summarize** — changes, deviations + why, test results, known limits.

Checklist: tests written · `dotnet test` passes · no new warnings.

## Domain-Driven Design — MANDATORY

This is the binding contract. The `ddd-dotnet` skill is the long-form playbook; this section is
the law. When the two agree, follow either. If you think a rule should be broken, **stop and ask
the user** — do not break it silently.

### Ubiquitous language

- Names in code == names the domain uses: `CBot`, `SourceProject`, `ParamSet`, `Instance`
  (Run/Backtest), `Node`, `AgentMandate`, `AgentProposal`, `AlertRule`, `TradingAccount`, `Ctid`.
- No technical synonyms for domain concepts (`InstanceRecord`, `CBotDto`, `NodeManager` where a
  domain term exists). DTOs at the edge are fine but must not rename the concept.
- A backtest is never a "job"; a node is never a "server"; a param set is never "config". Keep the
  language stable across Core, Web, Mcp, and tests.

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

- **Domain logic never lives in an endpoint, MCP tool, Razor component, or a `BackgroundService`.**
  Those are application/presentation layers: they orchestrate, they do not decide. Any `if` that
  encodes a business rule belongs on an aggregate or a domain service.
- Core stays pure. If you need infra in the domain, you modeled it wrong — introduce an interface
  in Core and implement it outside.

### Aggregates & aggregate roots

- Every write goes through an **aggregate root**. Current roots: `AppUser`, `CTraderIdAccount`
  (owns `TradingAccount`), `CBot` (owns `ParamSet`), `CBotSourceProject`, `Instance`, `Node`
  (owns `NodeStats`), `AgentMandate` (owns `AgentProposal`), `AlertRule` (owns `AlertEvent`),
  `McpApiKey`. `AuditLog`/`AppSetting`/`InstanceLog` are append-only records, not aggregates.
- **One aggregate = one consistency boundary = one transaction.** A single `SaveChanges` mutates
  **one** aggregate instance. Need to touch two? Use a domain event + a second use-case, or a
  process manager — never a fat transaction spanning roots.
- **Reference other aggregates by strong ID only** in new code (`CBotId`, `NodeId`, …), not by
  navigation property. EF nav props that already exist on entities may stay for query projection,
  but **do not add new cross-aggregate nav props** and never mutate another aggregate through one.
- Child entities (`ParamSet`, `TradingAccount`, `AgentProposal`, `AlertEvent`, `NodeStats`) are
  reached and mutated **only through their root** (`cbot.AddParamSet(...)`, not `new ParamSet` +
  `context.Add`).

### Entities — rich, not anemic

- **No public setters on domain entities.** State changes through intention-revealing methods that
  enforce invariants (`cbot.Rename(name)`, `mandate.Enable()`, `alertRule.SetInterval(minutes)`).
  Setters become `private set` / `init`.
- Constructors/factories put the entity in a **valid** state or throw a **domain exception**
  (`DomainException` subtype in Core, not `ArgumentException` from a controller). Prefer a static
  `Create(...)` factory when construction has rules; keep a private ctor for EF.
- Invariants are checked **inside** the aggregate, once, at the point of change — not re-validated
  in every caller. Callers trust the aggregate.
- The TPH state hierarchies (`Instance`, `Node` states) are the state pattern — **good, keep it**.
  A transition is a domain operation that returns the next-state entity; centralize transition
  rules (which state may go where) in the domain, not scattered across pollers. When you add a
  state or transition, model it as a method, not an ad-hoc `new XxxInstance { … }` in a service.

### Value objects

- Model concepts, not primitives. `Email`, `Symbol`, `Timeframe`, `DockerImageTag` and all strong
  IDs are VOs — **immutable, equality by value, self-validating** (see `Core/StrongIds.cs`). Follow
  that template for new ones (money/percent/risk, drawdown, container id, url, secret material).
- **Ban primitive obsession in new/changed signatures.** No bare `string symbol`, `Guid id`,
  `double riskPercent`, `int intervalMinutes` crossing a domain boundary — wrap them. Percentages
  and risk numbers especially (`RiskPercentPerTrade`, `MaxDrawdownPercent`) deserve a VO that
  rejects out-of-range values at construction.
- VOs validate in their constructor and throw a domain exception; they never carry an invalid value.

### Domain services, factories, domain events

- **Domain service** = stateless logic that spans aggregates or doesn't belong on one entity
  (`INodeScheduler` picking a node is the canonical example). Interface in Core; keep it free of
  infra. Reach for one only when the behavior genuinely isn't a single aggregate's responsibility.
- **Domain events** signal "something happened" (`InstanceStarted`, `BacktestCompleted`,
  `AgentProposalAccepted`, `RiskThresholdBreached`). Raise them from aggregate methods; collect on
  the entity; dispatch **after** a successful `SaveChanges` (EF `SavingChanges`/interceptor or an
  outbox). Cross-aggregate reactions and integration (SignalR, AI risk actions) subscribe to these
  instead of being inlined into the mutating use-case.
- **Factories** encapsulate multi-step/invariant-heavy creation. Repositories persist and retrieve
  **whole aggregates** — never leak `IQueryable` out of Core, never expose a generic
  `Repository<T>` that lets callers dodge aggregate methods.

### Repositories & persistence

- One repository interface **per aggregate root**, defined in Core, implemented in Infrastructure.
  Methods speak the domain (`GetActiveByUserAsync`, `AddAsync`, not `Query()`).
- Read models / list projections for the UI are **separate** from the write side — query EF
  directly in a read service/endpoint (CQRS-lite), returning DTOs. Don't force reporting queries
  through aggregate repositories, and don't reshape aggregates to make a screen easier.
- Persistence concerns (EF config, converters, TPH mapping, soft delete) stay in Infrastructure.
  The domain does not know EF exists.

### Bounded contexts / modules

- Organize the domain by module, not by technical type. Current de-facto contexts:
  **Access** (users, MFA, viewer grants, MCP keys), **Authoring** (CBot, SourceProject, ParamSet,
  builder), **Execution** (Instance, Node, scheduling, dispatch), **Portfolio** (AgentMandate,
  proposals, decision journal), **Alerts**. Put new types in the module that owns the concept;
  cross-context calls go through a well-named interface, not a shared mutable entity.
- Anti-corruption layers already exist for external systems (cTrader Console CLI, GHCR, Anthropic,
  `ExternalNode` agent) — keep them: translate at the edge, never let their shapes leak into Core.

### Brownfield rule (this repo is mid-migration)

Existing entities are anemic (public setters, logic in pollers/services). You are **not** required
to boil the ocean, but:
- **New** aggregates/entities/VOs: full DDD from the start. No exceptions.
- When you **touch** an existing anemic entity for a feature/fix: encapsulate the part you touch —
  add the intention method, tighten those setters, move that rule into the aggregate. Leave it
  better than you found it; do not add new anemic surface.
- Never cite "the rest of the code does it this way" to justify new anemic code. The old way is the
  debt being paid down, not the standard.

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

## Code style

- No comments except `TODO`/`FIXME`. No hardcoded strings — use a constants class.
- File-scoped namespaces. `sealed` by default. Injected fields `private readonly`.
- Early returns over nested `if`.
- Naming: `_camelCase` private fields, `I`-prefixed interfaces, `Async`-suffixed async methods, `var` when type obvious. Spell identifiers in full (no `Tp`/`Sl`/`Tcs`/`Vm`) — established initialisms (`UI`, `DI`, `ASP`, `DOM`) fine. Wire-format string literals keep literal form.
- Explicit access modifiers always (no implicit `internal`).
- Primary constructors; params ordered: regular service deps, then factory interfaces. Body order: field assignments → property init → event subscriptions last.
- Member order: fields → properties → events → methods, `Dispose()` last.
