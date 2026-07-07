# Requirements & Progress Log

## Original spec (verbatim from user)

Create an ASP.NET Core web app with Blazor frontend that allows building, running, backtesting,
and optimizing cTrader cBots via the cTrader Console Docker image, running across multiple
nodes.

### Multi-User & roles

- **Owner** — single account created during deployment. Does everything: add/remove users, add/remove nodes, update credentials.
- **Admin** — created by Owner. Adds/removes users (non-Admin/Owner).
- **User** — created by Owner/Admin. Uses the app; cannot create accounts or add nodes.
- **Viewer** — lowest role. Views assigned (or all, if granted) cBot run/backtest/optimization instances. Cannot stop or remove.

Each user's data isolated. All users change their own passwords. Passwords hashed.

### cTrader Accounts page

- Add cTrader cID accounts (username + password).
- Add trading accounts under each cID (account number). One cID → many trading accounts.
- Each user sees/manages only their own cID + trading accounts.
- All cID + trading account credentials **encrypted at rest** (industry standard), decrypted only when needed.

### cBots page

- Upload `.algo` files. Each linked to a user, visible only to owner.
- Rename, add, list, remove. `.algo` encrypted at rest.

### cBot Parameter Set files

- Per-cBot JSON parameter sets, persisted in DB. Rename, add, list, remove.

### Run page

Select: cBot, trading account, symbol, timeframe, parameter set. List running instances, live
stats, stop, duplicate, remove.

### Backtest page

Same selectors as Run plus backtest settings (date range, etc.). Per-backtest stats; navigate
to a completed backtest for stats + charts. Console container run with `--data-dir` mounted to a
persistent location on the node.

### Nodes

- Only Owner/Admin add nodes; users cannot pick a node.
- A node is a cloud server running the cTrader Console Docker image.
- Node mode: Run / Backtest / Mixed.
- Scheduler picks node by current running-instance count.
- On node removal, stop all its cBots/backtests first.
- Until ≥1 node exists, users cannot run or backtest.
- Per node show CPU, memory, disk, backtest data usage, running cBot count, backtest count, + clean-backtest-data option.

### cTrader Console Docker image

Users pick the image tag (default `latest`).

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

- Create a cTrader cBot .NET project.
- Monaco editor for source. Edit project file. Language: C# or Python.
- Build via `dotnet build`; save resulting `.algo` into user's cBots. Each build updates the linked cBot row + bumps version.
- Quick run.
- Show code/build errors in editor + Build Output tab.

### MCP server

Provide an MCP server URL so AI models access user cBots, run/backtest them, etc., auth via
per-user credentials.

## Clarifications received during the session

| Q | A |
| --- | --- |
| Blazor Server vs WASM | Blazor Server **with SSR** |
| Cloud target | Cloud-agnostic (deployable to Azure, AWS, ...) |
| Python cBot support | Yes — Python cBots are also .NET projects and build with `dotnet build` |
| cTrader Console image | `ghcr.io/spotware/ctrader-console` (from GitHub) |
| Email provider | None — owner/admin resets passwords manually; user forced to change on login |
| Blazor component library | MudBlazor |
| Background worker | In-process `BackgroundService`s on the web host |
| Per-user quotas | Out of scope |
| Builder location | Runs on the web app server, not on remote nodes |

## Architecture changes requested mid-session

1. **Options pattern** with strongly-typed immutable records and `IOptionsMonitor`.
2. Add **health checks**.
3. Remove `Ctw.ServiceDefaults` project; merge into `Infrastructure`.
4. Remove `Ctw.` prefix from project/file names + namespaces.
5. `LoggerMessage` source-generated logging delegates instead of inline templated logs.
6. NuGet **Central Package Management** (`Directory.Packages.props`).
7. `README.md` mentioning this is an experiment built entirely by Claude Code.
8. `CLAUDE.md` describing structure.
9. Drop `NoWarn` from `Directory.Build.props`; fix underlying warnings.
10. DDD — strong-typed IDs instead of primitives, value objects for emails, symbols, etc.
11. Soft delete.
12. No hard-coded strings; `const`s in dedicated classes.
13. `REQUIREMENTS.md` describing spec + progress.
14. Init Git repo, push to a private GitHub repo.

## What has been built so far

### Solution skeleton

- `AppHost`, `Core`, `Infrastructure`, `Nodes`, `ExternalNode`, `Web`, `Mcp` + `UnitTests`, `IntegrationTests`.
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
- MCP API key auth — `ctw_mcp_<hex>` token, SHA-256 hashed in DB, shown once.
- Password lockout, `MustChangePassword` flag.

### Nodes / orchestration

- `INodeScheduler` picks least-loaded node honoring `NodeMode` + `MaxInstances`.
- `HttpContainerDispatcher` calls the `ExternalNode` agent HTTP API (start/stop/status/report/logs/stats/clean) with a short-lived per-node HS256 JWT. `LocalContainerDispatcher` for the web host's own `LocalNode`.
- `ExternalNode` agent pulls the image + runs the cBot container via docker CLI (`--ctid --pwd-file --account --symbol --period --data-dir --start --end --data-mode ...`), image-prefix guarded, stateless by `ctw.instance` label.
- `NodeStatsPoller` collects CPU/mem/disk/backtest-data stats.
- `InstanceReconciler` marks stale Starting instances Failed; `RunCompletionPoller`/`BacktestCompletionPoller` reconcile exited containers.

### Builder (in-browser editor → `.algo`)

- C# + Python cBot starter templates (both .NET projects).
- `CBotBuilder` runs `dotnet build` in a throwaway container (SDK image, work dir bind-mounted at `/work`, shared `ctw-nuget-cache` volume) on the web host. Reads `out/*.algo`, encrypts, upserts the linked `CBot` row, bumps version.
- Quick run dispatches the produced `.algo` to a `Run` node via the scheduler.

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

## Open follow-ups

- Plug in optimization once the upstream image supports it.
- Add Azure Key Vault / AWS KMS adapters for Data Protection key encryption.
- Add in-app notifications (instance crashed, backtest done).
- Add per-cBot parameter form auto-generation from `.algo` metadata.
