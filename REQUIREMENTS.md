# Requirements & Progress Log

## Original spec (verbatim from user)

Create an ASP.NET Core web app with Blazor frontend that allows building, running, backtesting,
and optimizing cTrader cBots via the cTrader Console Docker image, running across multiple
nodes.

### Multi-User & roles

- **Owner** — single account created during deployment. Can do everything: add/remove users,
  add/remove nodes, update credentials.
- **Admin** — created by Owner. Can add/remove users (non-Admin/Owner).
- **User** — created by Owner/Admin. Can use the app; cannot create accounts or add nodes.
- **Viewer** — lowest role. Can view assigned (or all, if granted) cBot run/backtest/optimization
  instances. Cannot stop or remove instances.

Each user's data is isolated. All users can change their own passwords. Passwords must be
hashed.

### cTrader Accounts page

- Add cTrader cID accounts (username + password).
- Add trading accounts under each cID (account number).
- A cID can have multiple trading accounts.
- Each user only sees and manages their own cID and trading accounts.
- All cID and trading account credentials must be **encrypted at rest** using an industry
  standard solution and decrypted only when needed.

### cBots page

- Upload `.algo` files.
- Each cBot is linked to a user and only visible to its owner.
- Rename, add, list, remove.
- `.algo` files must be encrypted at rest.

### cBot Parameter Set files

- Per-cBot JSON parameter sets, persisted in DB.
- Rename, add, list, remove.

### Run page

To run a cBot the user must select: cBot, trading account, symbol, timeframe, parameter set.
User can list running instances, see live stats, stop, duplicate, remove.

### Backtest page

Same selectors as Run plus backtest settings (date range, etc.). User sees per-backtest stats
and can navigate to a completed backtest to see stats with charts. The Console container must
be run with `--data-dir` mounted to a persistent location on the node.

### Nodes

- Only Owner/Admin can add nodes; users cannot pick a node.
- A node is a cloud server reachable via SSH that runs the cTrader Console Docker image.
- Each node has a mode: Run / Backtest / Mixed.
- Scheduler picks node by current running-instance count.
- When a node is removed, the app must stop all its cBots/backtests first.
- Until at least one node exists, users cannot run or backtest.
- For each node show CPU, memory, disk usage, backtest data usage, running cBot count,
  backtest count, and an option to clean backtest data.

### cTrader Console Docker image

Let users pick the image tag (default `latest`).

### Stack

- PostgreSQL + Entity Framework Core
- .NET Aspire
- .NET 10
- Minimal APIs
- Blazor + Tailwind  *(later changed to MudBlazor at user request)*

### UI

cTrader-style colors. Mobile-first.

### Optimization

Not available in cTrader Console yet — keep design open for it.

### Building cBot (in-browser)

- Create a cTrader cBot .NET project.
- Code editor (Monaco) for editing source.
- Edit project file.
- Choose language: C# or Python.
- Build via standard `dotnet build`; save the resulting `.algo` as part of user's cBots.
  Each build updates the linked cBot row and bumps version.
- Quick run.
- Show code/build errors in the editor and on a Build Output tab.

### MCP server

Provide an MCP server URL so AI models can access user cBots, run/backtest them, etc.,
authenticated with per-user credentials.

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

1. Use the **Options pattern** with strongly-typed immutable records and `IOptionsMonitor`.
2. Add **health checks**.
3. Remove the `Ctw.ServiceDefaults` project; merge its content into `Infrastructure`.
4. Remove the `Ctw.` prefix from project names, file names, and namespaces.
5. Use `LoggerMessage` source-generated logging delegates instead of inline templated logs.
6. Use NuGet **Central Package Management** (`Directory.Packages.props`).
7. Write a `README.md` mentioning this is an experiment built entirely by Claude Code.
8. Write `CLAUDE.md` describing structure.
9. Drop `NoWarn` from `Directory.Build.props` and fix the underlying warnings.
10. Use DDD principles — strong-typed IDs instead of primitives, value objects for emails,
    symbols, etc.
11. Use soft delete.
12. No hard-coded strings; collect them as `const`s in dedicated classes.
13. Write `REQUIREMENTS.md` describing the spec and progress.
14. Initialize a Git repo and push to a private GitHub repo.

## What has been built so far

### Solution skeleton

- `AppHost`, `Core`, `Infrastructure`, `Nodes`, `Web`, `Mcp` projects + `UnitTests` and
  `IntegrationTests`.
- Central Package Management via `Directory.Packages.props`.
- `Directory.Build.props` with strict settings (`TreatWarningsAsErrors=true`, no `NoWarn`).
- `.editorconfig`, `.gitignore`, `global.json` pinning .NET 10.

### Domain

- `AuditedEntity` base class implementing `ISoftDeletable`.
- Entities: `AppUser`, `CTraderIdAccount`, `TradingAccount`, `CBot`, `CBotSourceProject`,
  `ParamSet`, `Node`, `NodeStats`, `Instance`, `InstanceLog`, `ViewerGrant`, `McpApiKey`,
  `AuditLog`, `AppSetting`.
- Enums: `UserRole`, `NodeMode`, `NodeStatus`, `InstanceType`, `InstanceStatus`,
  `CBotLanguage`.
- Strong-typed IDs and value objects in `src/Core/StrongIds.cs`.
- `CtwOptions` record in `src/Core/Options/CtwOptions.cs`.
- Source-generated logger delegates in `src/Core/Logging/LogMessages.cs`.
- Constants in `src/Core/Constants/AppConstants.cs`.

### Persistence

- `CtwDbContext` with soft-delete global query filter and override of `SaveChanges`.
- `DesignTimeDbContextFactory` for `dotnet-ef` tooling.
- Initial EF migration generated.
- Data Protection key ring persisted in Postgres, key-encrypted by user-supplied PFX cert.

### Security

- Argon2id password hasher (Konscious).
- `DataProtectionSecretProtector` with per-field purpose strings.
- Cookie auth + role policies (`Owner`, `AdminOrAbove`, `UserOrAbove`).
- `OwnerSeeder` seeds the Owner account from `CtwOptions` on startup.
- MCP API key auth handler — `ctw_mcp_<hex>` token, SHA-256 hashed in DB, shown to user once.
- Password lockout, `MustChangePassword` flag.

### Nodes / orchestration

- `INodeScheduler` picks least-loaded node honoring `NodeMode` + `MaxInstances`.
- `SshContainerDispatcher` drives `docker run/stop/logs/stats` over SSH; reads cTrader Console
  flags (`--ctid --pwd-file --account --symbol --period --data-dir --start --end ...`).
  Encrypted SSH private keys + optional passphrase. Path-traversal guard on cleanup.
- `NodeStatsPoller` `BackgroundService` collects CPU/mem/disk/backtest-data stats.
- `InstanceReconciler` `BackgroundService` marks stale Starting instances as Failed.

### Builder (in-browser editor → `.algo`)

- Templates for C# and Python cBot starter projects (both as .NET projects).
- `CBotBuilder` runs `docker run --rm --network=none -v <workdir>:/work
  mcr.microsoft.com/dotnet/sdk:9.0 sh -c "dotnet build -c Release -o /work/out"` locally on
  the web host. Reads `out/*.algo`, encrypts, upserts the linked `CBot` row, bumps version.
- Quick run dispatches the produced `.algo` to a `Run` node via the scheduler.

### Web / API

- Minimal API endpoint groups for: auth, users, cTrader IDs + trading accounts, cBots,
  parameter sets, instances, nodes, MCP keys, builder projects, image tags.
- SignalR `LogsHub` for live `docker logs -f` streaming.
- Health checks for PostgreSQL.
- OpenAPI registration.

### Blazor UI

- MudBlazor app with custom dark cTrader-style theme (`#2962FF` accent, charcoal background).
- Pages: Dashboard, Login, Account (change password), CBots (upload/manage), Accounts (cIDs +
  trading accounts), ParamSets, Run, Backtest, InstanceDetail (live logs + equity chart),
  Nodes (with CPU/mem/disk + clean), Users (admin), Mcp (key issuance), Builder (Monaco editor
  with build/run/output tabs).
- PWA `manifest.webmanifest`.

### MCP server

- Hosted in a separate ASP.NET Core project.
- Bearer auth via MCP API key.
- Tools: `ListCBots`, `ListParamSets`, `ListTradingAccounts`, `ListInstances`,
  `GetBacktestResult`.

### Aspire

- `AppHost` wires up Postgres (with persistent volume + pgAdmin), Web, MCP, and parameter
  bindings for `OwnerEmail`, `OwnerPassword`, Data Protection cert (base64 + password),
  Postgres password.

## Open follow-ups

- Verify the cTrader Console CLI's parameter-file consumption mechanism (the docs do not
  publicly describe a `--params` flag).
- Plug in optimization once the upstream image supports it.
- Add Azure Key Vault / AWS KMS adapters for Data Protection key encryption.
- Add notification system (instance crashed, backtest done) in-app.
- Add per-cBot parameter form auto-generation from `.algo` metadata.
