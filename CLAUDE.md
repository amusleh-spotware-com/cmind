# CLAUDE.md

Guidance for future Claude Code sessions on this repo.

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
    Options/AppOptions.cs    — binds "Ctw" section, incl. nested LocalNodeOptions
    Constants/AppConstants.cs — all magic strings
    Logging/LogMessages.cs   — source-generated ILogger extensions
    NodeAgent/AgentContracts.cs — DTOs for the ExternalNode HTTP API
  Infrastructure/ — EF Core, DataProtection, encryption, GHCR client.
    Persistence/DataContext.cs — soft-delete query filter + Delete→Modified SaveChanges override
    Persistence/Migrations/, DesignTimeDbContextFactory.cs
    Security/Argon2PasswordHasher.cs, DataProtectionSecretProtector.cs
    Ghcr/GhcrTagProvider.cs  — anonymous tag list, 1h MemoryCache
    Aspire/ServiceDefaultsExtensions.cs — OTel, health checks, service discovery, resilience
    DependencyInjection.cs  — AddCtwInfrastructure()
  Nodes/         — cross-node orchestration.
    NodeScheduler.cs           — picks least-loaded eligible node, honors MaxInstances
    ContainerDispatcherFactory.cs — routes to Http (remote) or Local dispatcher by Node type
    HttpContainerDispatcher.cs  — calls the ExternalNode agent's HTTP API for remote nodes
                                  (start/stop/status/report/logs/stats/clean); mints a short-lived
                                  per-node HS256 JWT signed with that node's shared secret
    LocalContainerDispatcher.cs — same ops via local `docker` process calls, for the web host's LocalNode
    NodeStatsPoller.cs          — BackgroundService, polls per AppOptions.NodeStatsPollInterval
    RunCompletionPoller.cs / BacktestCompletionPoller.cs — reconcile exited run/backtest containers
    ContainerCommandHelpers.cs  — BuildConsoleArgsList (tokens) / BuildConsoleArgs (shell string)
    Builder/CBotBuilder.cs      — sandboxed builder; `docker run` an SDK image + dotnet build
    Builder/Templates/          — embedded C#/Python starter project files
  ExternalNode/  — standalone HTTP node agent (deployed on remote servers).
    Program.cs                  — minimal API, JWT-bearer auth, image-prefix guard
    DockerService.cs            — pulls image + runs/stops/inspects containers via the docker CLI,
                                  stateless (looks containers up by `ctw.instance` label)
    NodeAgentOptions.cs, Dockerfile
  Web/           — Blazor Server SSR + Minimal API + SignalR.
    Program.cs               — binds AppOptions, cookie auth, role policies, hosted services
                               (OwnerSeeder, LocalNodeSeeder, InstanceReconciler), endpoint maps
    Auth/                    — HttpCurrentUser, OwnerSeeder, LocalNodeSeeder, InstanceReconciler,
                               CookieForwardingHandler
    Endpoints/               — Auth, CBot, Builder, ParamSet, Instance, Node, User, Ctid,
                               McpKey, Dashboard, Image
    Hubs/LogsHub.cs          — SignalR streaming of `docker logs -f`
    Components/Pages/        — CBots (list/build/run/edit), BuilderEditor (Monaco IDE), Run,
                               Backtest, ParamSets, Nodes, Accounts, Users, InstanceTable/Detail,
                               Login, Index, Mcp
    Components/Dialogs/NewProjectDialog.razor
    Components/              — MudBlazor + custom cTrader-style dark theme
  Mcp/           — MCP HTTP+SSE server.
    Auth/McpKeyAuthHandler.cs — bearer token `ctw_mcp_<hex>`, SHA-256 hashed, prefix-indexed
    Tools/                    — CBotTools, InstanceTools
tests/
  UnitTests/         — xUnit + FluentAssertions + NSubstitute
  IntegrationTests/  — Testcontainers PostgreSQL
```

## Conventions

- `TreatWarningsAsErrors=true`, no `NoWarn` — fix real warnings.
- Config via `IOptionsMonitor<AppOptions>` (binds `Ctw` section); no `cfg["Key"]` in business code.
- Log via `LogMessages` source-generated extensions (`Core/Logging/LogMessages.cs`) — never `ILogger.LogInformation(...)` directly.
- Magic strings live in `Core/Constants/`.
- Soft delete: entities inherit `AuditedEntity`/`ISoftDeletable`; `DataContext` global query filter + converts `Deleted` → `Modified`+`IsDeleted` in `SaveChanges`.
- Strong IDs/value objects in `Core/StrongIds.cs`; entities still use `Guid` for EF mapping (value-converter migration follow-up).
- Never log/store secrets plaintext — use `ISecretProtector` with `EncryptionPurposes` strings. Data Protection key ring PFX-encrypted via base64 env var.
- Health checks: `AddHealthChecks().AddNpgSql(...)`, Development only.

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
- External nodes get **no** SSH/shell access. Main node talks to `ExternalNode` agent over HTTP; each `RemoteNode` stores `BaseUrl` + encrypted per-node shared secret. Every request carries short-lived HS256 JWT (`iss=ctw-main`, `aud=ctw-node`, 5-min expiry) signed with node's secret; agent validates. Agent only runs images matching `AllowedImagePrefix` (default `ghcr.io/spotware/`), execs docker via `ArgumentList` (no shell), stateless (finds containers by `ctw.instance` label → survives restart). Deploy with docker daemon available; run container `--privileged` (starts local dockerd) or run binary on host with docker.
- cTrader Console backtest CLI (verified live): requires `--data-mode` (default `m1`), dates `dd/MM/yyyy HH:mm`, `params.cbotset` is JSON (`{"Parameters":{...}}`) passed as positional arg; `run` rejects `--data-dir` (backtest-only). See `ContainerCommandHelpers`.
- `BacktestCompletionPoller` polls `RunningBacktestInstance` on `AppOptions.BacktestCompletionPollInterval` (backtest containers self-exit via `--exit-on-stop`). `RunCompletionPoller` does same for `RunningRunInstance`, using `IContainerDispatcher.GetExitCodeAsync` → exit 0/null = `StoppedRunInstance`, non-zero = `FailedRunInstance`. Backtest: report present → `CompletedBacktestInstance` (stores `ReportJson`); missing → `FailedBacktestInstance`.
- Equity curve for `InstanceDetail` chart parsed from `CompletedBacktestInstance.ReportJson` by `ContainerCommandHelpers.ParseEquityCurve`. Real report nests points at `equity.points[]` (`{balance,minEquity,maxEquity,timestamp}`); parser also scans root keys `equityHistory`/`equityCurve`/`history`/`equity`.
- MCP server separate process → scales/redeploys independently of Web. Uses stateless HTTP transport + `AddHttpContextAccessor` so tool calls see the authenticated user.
- EF TPH gotcha: don't add `e.Property<T>(nameof(Subclass.Prop)).IsRequired(false)` from a *base* type's `EntityTypeBuilder` for a property on a *derived* TPH type — silently produces a property EF never persists (bit us on old `RemoteNode` SSH fields). TPH makes subclass-only properties nullable at column level automatically; no extra config.
- EF SQL-translation gotcha: nested `(i as T) != null ? (i as T)!.Prop : ...` chains in an `IQueryable` `.Select()` don't reliably translate (silent wrong/null values vs real Postgres). Materialize with `ToListAsync()` first, switch in C# (see `InstanceEndpoints.GetStartedAt`/`GetStoppedAt`).
- Don't project a full entity with a one-to-one nav cycle (`Node.LatestStats`/`NodeStats.Node`) into an API response — System.Text.Json has no cycle detection, serializes to `MaxDepth` and 500s. Project scalar fields.
- `Instance.IsActive`/`IsTerminal` are C#-only computed (per-subclass override), not mapped columns — filtering on them in `IQueryable` throws at translation. Materialize first.
- Instance state transitions replace the entity (TPH discriminator can't change) → the instance **id changes** starting→running→terminal. Container id is stable and carried over; the HTTP agent is keyed by container id for status/report/stop/logs.

## Known gaps / needs follow-up

- **Builder isolation** (resolved): `CBotBuilder` runs `dotnet build` inside a throwaway container (`DockerCommands.RunBuild` + `AppOptions.BuildImage`, work dir bind-mounted at `/work`) → untrusted user MSBuild targets can't reach host FS/network. Restore cached across builds via shared `ctw-nuget-cache` volume. Web host still needs Docker socket access.
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
