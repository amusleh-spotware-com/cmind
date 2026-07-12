# Requirements & Progress Log

## Original spec (verbatim from user)

Create ASP.NET Core web app, Blazor frontend. Build, run, backtest, optimize cTrader cBots via cTrader Console Docker image, across many nodes.

### Multi-User & roles

- **Owner** — single account made at deploy. Do all: add/remove users, add/remove nodes, update creds.
- **Admin** — made by Owner. Add/remove users (non-Admin/Owner).
- **User** — made by Owner/Admin. Use app; no account creation, no node add.
- **Viewer** — lowest role. View assigned (or all, if granted) cBot run/backtest/optimization instances. No stop, no remove.

Each user data isolated. All users change own password. Passwords hashed.

### cTrader Accounts page

- Add cTrader cID accounts (username + password).
- Add trading accounts under each cID (account number). One cID → many trading accounts.
- Each user see/manage only own cID + trading accounts.
- All cID + trading account creds **encrypted at rest** (industry standard), decrypt only when needed.

### cBots page

- Upload `.algo` files. Each linked to user, visible only to owner.
- Rename, add, list, remove. `.algo` encrypted at rest.

### cBot Parameter Set files

- Per-cBot JSON parameter sets, persist in DB. Rename, add, list, remove.

### Run page

Select: cBot, trading account, symbol, timeframe, parameter set. List running instances, live stats, stop, duplicate, remove.

### Backtest page

Same selectors as Run plus backtest settings (date range, etc.). Per-backtest stats; navigate to completed backtest for stats + charts. Console container run with `--data-dir` mounted to persistent location on node.

### Nodes

- Only Owner/Admin add nodes; users cannot pick node.
- Node = cloud server running cTrader Console Docker image.
- Node mode: Run / Backtest / Mixed.
- Scheduler picks node by current running-instance count.
- On node removal, stop all its cBots/backtests first.
- Until ≥1 node exists, users cannot run or backtest.
- Per node show CPU, memory, disk, backtest data usage, running cBot count, backtest count, + clean-backtest-data option.

### cTrader Console Docker image

Users pick image tag (default `latest`).

### Stack

- PostgreSQL + Entity Framework Core
- .NET Aspire
- .NET 10
- Minimal APIs
- Blazor + Tailwind  *(later changed to MudBlazor at user request)*

### UI

cTrader-style colors. Mobile-first.

### Optimization

Not in cTrader Console yet — keep design open.

### Building cBot (in-browser)

- Create cTrader cBot .NET project.
- Monaco editor for source. Edit project file. Language: C# or Python.
- Build via `dotnet build`; save resulting `.algo` into user's cBots. Each build updates linked cBot row + bumps version.
- Quick run.
- Show code/build errors in editor + Build Output tab.

### MCP server

Provide MCP server URL so AI models access user cBots, run/backtest them, etc., auth via per-user creds.

## Clarifications received during the session

| Q | A |
| --- | --- |
| Blazor Server vs WASM | Blazor Server **with SSR** |
| Cloud target | Cloud-agnostic (deployable to Azure, AWS, ...) |
| Python cBot support | Yes — Python cBots also .NET projects, build with `dotnet build` |
| cTrader Console image | `ghcr.io/spotware/ctrader-console` (from GitHub) |
| Email provider | None — owner/admin resets passwords manually; user forced to change on login |
| Blazor component library | MudBlazor |
| Background worker | In-process `BackgroundService`s on web host |
| Per-user quotas | Out of scope |
| Builder location | Runs on web app server, not remote nodes |

## Architecture changes requested mid-session

1. **Options pattern** with strongly-typed immutable records and `IOptionsMonitor`.
2. Add **health checks**.
3. Remove `App.ServiceDefaults` project; merge into `Infrastructure`.
4. Remove `App.` prefix from project/file names + namespaces.
5. `LoggerMessage` source-generated logging delegates instead of inline templated logs.
6. NuGet **Central Package Management** (`Directory.Packages.props`).
7. `README.md` noting this = experiment built entirely by Claude Code.
8. `CLAUDE.md` describing structure.
9. Drop `NoWarn` from `Directory.Build.props`; fix underlying warnings.
10. DDD — strong-typed IDs instead of primitives, value objects for emails, symbols, etc.
11. Soft delete.
12. No hard-coded strings; `const`s in dedicated classes.
13. `REQUIREMENTS.md` describing spec + progress.
14. Init Git repo, push to private GitHub repo.

## What has been built so far

### Solution skeleton

- `AppHost`, `Core`, `Infrastructure`, `Nodes`, `CtraderCliNode`, `Web`, `Mcp` + `UnitTests`, `IntegrationTests`.
- Central Package Management via `Directory.Packages.props`.
- `Directory.Build.props` strict (`TreatWarningsAsErrors=true`, no `NoWarn`).
- `.editorconfig`, `.gitignore`, `global.json` pinning .NET 10.

### Domain

- `AuditedEntity` base implementing `ISoftDeletable`.
- Entities: `AppUser`, `CTraderIdAccount`, `TradingAccount`, `CBot`, `CBotSourceProject`, `ParamSet`, `Node`, `NodeStats`, `Instance`, `InstanceLog`, `ViewerGrant`, `McpApiKey`, `AuditLog`, `AppSetting`.
- Enums: `UserRole`, `NodeMode`, `NodeStatus`, `InstanceType`, `InstanceStatus`, `CBotLanguage`.
- Strong-typed IDs + value objects in `src/Core/StrongIds.cs`.
- Options record in `src/Core/Options/AppOptions.cs`.
- Source-generated logger delegates in `src/Core/Logging/LogMessages.cs`.
- Constants in `src/Core/Constants/AppConstants.cs`.

### Persistence

- `DataContext` with soft-delete global query filter + `SaveChanges` override.
- `DesignTimeDbContextFactory` for `dotnet-ef`.
- EF migrations generated.
- Data Protection key ring persisted in Postgres, key-encrypted by user-supplied PFX cert.

### Security

- Argon2id password hasher (Konscious).
- `DataProtectionSecretProtector` with per-field purpose strings.
- Cookie auth + role policies (`Owner`, `AdminOrAbove`, `UserOrAbove`); API paths return 401/403 JSON.
- `OwnerSeeder` seeds Owner from options on startup.
- MCP API key auth — `mcpk_<hex>` token, SHA-256 hashed in DB, shown once.
- Password lockout, `MustChangePassword` flag.

### Nodes / orchestration

- `INodeScheduler` picks least-loaded node honoring `NodeMode` + `MaxInstances`.
- `HttpContainerDispatcher` calls `CtraderCliNode` agent HTTP API (start/stop/status/report/logs/stats/clean) with short-lived per-node HS256 JWT. `LocalContainerDispatcher` for web host's own `LocalNode`.
- `CtraderCliNode` agent pulls image + runs cBot container via docker CLI (`--ctid --pwd-file --account --symbol --period --data-dir --start --end --data-mode ...`), image-prefix guarded, stateless by `app.instance` label.
- `NodeStatsPoller` collects CPU/mem/disk/backtest-data stats.
- `InstanceReconciler` marks stale Starting instances Failed; `RunCompletionPoller`/`BacktestCompletionPoller` reconcile exited containers.

### Builder (in-browser editor → `.algo`)

- C# + Python cBot starter templates (both .NET projects).
- `CBotBuilder` runs `dotnet build` in throwaway container (SDK image, work dir bind-mounted at `/work`, shared `app-nuget-cache` volume) on web host. Reads `out/*.algo`, encrypts, upserts linked `CBot` row, bumps version.
- Quick run dispatches produced `.algo` to `Run` node via scheduler.

### Web / API

- Minimal API groups: auth, users, cTrader IDs + trading accounts, cBots, parameter sets, instances, nodes, MCP keys, builder projects, image tags.
- SignalR `LogsHub` for live `docker logs -f` streaming.
- Health checks for PostgreSQL. OpenAPI registration.

### Blazor UI

- MudBlazor app, custom dark cTrader theme (`#2962FF` accent, charcoal background).
- Pages: Dashboard, Login, Account (change password), CBots (upload/manage), Accounts (cIDs + trading accounts), ParamSets, Run, Backtest, InstanceDetail (live logs + equity chart), Nodes (CPU/mem/disk + clean), Users (admin), Mcp (key issuance), Builder (Monaco editor with build/run/output tabs).
- PWA `manifest.webmanifest`.

### MCP server

- Separate ASP.NET Core project. Bearer auth via MCP API key. Stateless HTTP transport.
- Tools: `ListCBots`, `ListParamSets`, `ListTradingAccounts`, `ListInstances`, `GetBacktestResult`.

### Aspire

- `AppHost` wires Postgres (persistent volume + pgAdmin), Web, MCP, + parameter bindings for `OwnerEmail`, `OwnerPassword`, Data Protection cert (base64 + password), Postgres password.

## Features added after the initial build

Original spec above shipped. Since then app grew several major capabilities, each documented under `website/docs/features/`, covered by unit + integration + Playwright/API E2E tests.

### Copy trading (cTrader Open API)

- Mirror trades across accounts over cTrader Open API. cID OAuth onboarding (callback with
  cookie-state), single Open API application record, dialog-driven copy-profile UI.
- Faithful order mirroring: market/pending order types, expiry, market-range slippage, partial
  close, SL/TP amend, trailing, position true-up on partial fills, pending-order mirroring.
- Distributed execution: `CopyEngineHost` across nodes, node lease + affinity, self-healing lease
  reclaim on node death, in-place Open API token rotation (single valid token per cID), resync
  after disconnect/desync, circuit breaker (resync heals bypass breaker).
- Provider marketplace + listings (`PerformanceFee` VO, plan status), performance-fee engine,
  execution-transparency ledger, copy notifications (host → alert bridge). AI copy recommender
  suggests providers. Docs: `copy-trading.md`, `copy-execution-transparency.md`,
  `copy-performance-fees.md`, `copy-provider-marketplace.md`, `copy-notifications.md`,
  `ai-copy-recommender.md`.
- Fidelity guaranteed by cTrader-faithful `FakeTradingSession` simulator plus deterministic
  copy-trading stress suite (`tests/StressTests`) and K8s live-E2E harness.

### AI-first layer (Claude / Anthropic Messages API)

- `Core.Ai.IAiClient` abstraction, raw-HTTP `AnthropicAiClient` (no SDK dep), `IAiFeatureService`
  orchestrating ten features, gated on `App:Ai:ApiKey` (off → every feature returns disabled, app
  runs unchanged). Runtime key encrypted at rest, editable on AI Settings page.
- Features: NL cBot codegen, buildable-project generation with build/self-repair loop, cBot review,
  backtest analysis, parameter optimization + closed optimize-run loop (propose → persist →
  backtest across nodes), instance post-mortems, web-search-grounded market sentiment,
  chart-vision strategy design, marketplace curation.
- Surfaces: **AI Assistant** page, `/api/ai/*` endpoints, MCP `AiTools`, background
  `AiRiskGuard` assessing running bots when `RiskGuardEnabled`. Agent mandates + proposals +
  alert rules/events aggregates back agent/alerts features. Docs: `ai.md`.

### Prop-firm challenge simulation

- Live Open API equity tracking, node-leased evaluation, all challenge rule types (max drawdown,
  daily loss, profit target, ...). Superseded earlier "no live equity feed" limitation for
  prop-firm context. Docs: `prop-firm.md`.

### White-label, feature toggles, compliance

- White-label branding, per-deployment feature toggles, compliance/legal settings page.
  Docs: `white-label.md`, `feature-toggles.md`, `compliance.md`.

### Platform hardening

- **Full DDD migration** — rich aggregates (no anemic entities/public setters), value objects over
  primitives, one aggregate per transaction, cross-aggregate refs by strong ID, domain events;
  strict DDD now enforced standard (see `CLAUDE.md`).
- **`TimeProvider` everywhere** — no `DateTime.UtcNow`; injected clock for deterministic time tests.
- **Node auto-discovery** — agents self-register + heartbeat to `POST /api/nodes/register`
  (join-token, protocol-version gated); `NodeHeartbeatMonitor` reconciles staleness.
- **Strict versioning** — one SemVer product version surfaced via `Core.VersionInfo` and `/version`
  on Web/MCP/node agent; `NodeAgentProtocol` wire version guards main↔agent HTTP API
  (`426 Upgrade Required` on mismatch).
- **Cloud-native observability** — Serilog compact-JSON with `trace_id`/`span_id` correlation, OTel
  metrics/traces, native Azure App Insights export, AWS X-Ray/CloudWatch via ADOT sidecar.
- **Web security** — CSP + security-headers middleware, auth-endpoint rate limiting, time-based
  auto-expiring login lockout, hardened auth cookie.
- **Deploy artifacts** — `Dockerfile.{web,mcp,node-agent}`, docker-compose, Helm chart
  (`deploy/helm/cmind`, node agents = privileged StatefulSet), Azure bicep (Container Apps),
  AWS Terraform (ECS Fargate + RDS). See `website/docs/deployment/`.
- **UI convention** — all add/create/edit actions use MudBlazor dialogs (no inline page forms);
  grouped nav with Settings section; per-cBot Parameter Sets dialog.

## Open follow-ups

- Add Azure Key Vault / AWS KMS adapters for Data Protection key encryption.
- Add per-cBot parameter form auto-generation from `.algo` metadata.
- Live copy-trading order-execution E2E against real broker credentials + node cluster.
- Cloud IaC item S5 (copy-overhaul Phase 5) still open.

## Resolved follow-ups (were open in the initial build)

- ~~Plug in optimization once upstream image supports it~~ → AI closed optimize-run loop
  (propose → persist → backtest across nodes) fills this without upstream support.
- ~~Add in-app notifications (instance crashed, backtest done)~~ → alert rules/events + copy
  notifications shipped.