# cTrader Algo Web

> **Experimental project — entirely built by [Claude Code](https://claude.com/claude-code).**
> No line of code in this repository was hand-written by a human. Spec, scaffolding, refactors,
> migrations, dependency wiring, and documentation were all produced by an AI agent through an
> interactive coding session. Treat the code as a reference experiment, not production-ready.

ASP.NET Core + Blazor Server app for building, running, backtesting, and (later) optimizing
cTrader cBots through the official [cTrader Console Docker image](https://github.com/spotware/ctrader-console-docker),
scheduled across remote nodes (each running the `ExternalNode` HTTP agent) and/or the local web host.

## Stack

- .NET 10, C# (latest)
- ASP.NET Core Minimal APIs
- Blazor Server with Server-Side Rendering
- MudBlazor + custom cTrader-style dark theme
- BlazorMonaco for the in-browser cBot code editor
- Blazor-ApexCharts for backtest charts
- EF Core 10 + Npgsql + PostgreSQL
- ASP.NET Core Data Protection (X.509 cert-protected key ring persisted in PostgreSQL)
- Argon2id password hashing (Konscious)
- HTTP + per-node HS256 JWT for external node connectivity
- ModelContextProtocol .NET SDK for the MCP server
- .NET Aspire 9 for orchestration
- OpenTelemetry (metrics + traces + logs)

## Solution layout

| Project              | Description                                                                    |
| -------------------- | ------------------------------------------------------------------------------- |
| `src/AppHost`        | .NET Aspire orchestrator (Postgres, Web, MCP, pgAdmin)                          |
| `src/Core`           | Domain entities, value objects, strong-typed IDs, options, log delegates        |
| `src/Infrastructure` | EF Core (`DataContext`), encryption, Argon2, GHCR client, OTel/health defaults  |
| `src/Nodes`          | Node scheduler, HTTP + local container dispatchers, stats poller, completion pollers |
| `src/ExternalNode`   | Standalone HTTP node agent (JWT auth) that pulls images and runs cBot containers on a remote server |
| `src/Web`            | Blazor Server SSR + Minimal API + SignalR LogsHub (cBots, builder IDE, run/backtest, param sets, nodes, accounts) |
| `src/Mcp`            | MCP server (HTTP + SSE) for AI integrations                                     |
| `tests/UnitTests`    | xUnit unit tests                                                                |
| `tests/IntegrationTests` | xUnit + Testcontainers integration tests                                    |

## Prerequisites

- .NET 10 SDK
- Docker (engine reachable by the Web container/host — used by the builder)
- PostgreSQL (provisioned automatically by Aspire in development)

## Configuration

All settings live under the `Ctw` section and are bound to a strongly-typed
[`AppOptions`](src/Core/Options/AppOptions.cs) record consumed via `IOptionsMonitor<AppOptions>`.

```jsonc
{
  "Ctw": {
    "OwnerEmail": "owner@example.com",
    "OwnerPassword": "set-via-secret",
    "DataProtectionCertBase64": "<base64 PFX>",
    "DataProtectionCertPassword": "<pfx password>",
    "DefaultDockerImage": "ghcr.io/spotware/ctrader-console",
    "DefaultDockerTag": "latest",
    "BuildWorkRoot": "/var/ctw/builds",
    "BuildImage": "mcr.microsoft.com/dotnet/sdk:9.0",
    "LocalNode": { "Enabled": true, "WorkRoot": "/var/ctw/local", "MaxInstances": 5 }
  }
}
```

`LocalNode` (disabled by default) lets run/backtest containers be scheduled on the web
host itself, dispatched via `LocalContainerDispatcher` instead of a remote agent.

## External nodes

Remote nodes run the **`src/ExternalNode`** agent — an HTTP API the main node calls to pull
images and run cBot containers. There is no SSH/shell access: the main node sends the image,
the tokenized `run`/`backtest` command, and the required files (algo, `params.cbotset`,
`ctid.pwd`), and the agent runs the container locally and reports status/report/logs/stats back.

Each request is authenticated with a short-lived HS256 JWT (`iss=ctw-main`, `aud=ctw-node`,
5-minute expiry) signed with a **per-node shared secret**. The agent only runs images matching
`AllowedImagePrefix` (default `ghcr.io/spotware/`) and finds containers by the `ctw.instance`
label, so it is stateless and restart-safe.

Agent configuration (`NodeAgent` section / `NodeAgent__*` env vars):

```jsonc
{
  "NodeAgent": {
    "JwtSecret": "<shared secret, >= 32 chars>",   // must match the value stored for the node
    "DataRoot": "/var/ctw/data",
    "AllowedImagePrefix": "ghcr.io/spotware/"
  }
}
```

Run the agent (build the image from `src/ExternalNode/Dockerfile`), giving it a docker daemon.
It starts its own daemon, so run it `--privileged`:

```bash
docker build -f src/ExternalNode/Dockerfile -t ctw-node-agent .
docker run -d --privileged -p 8080:8080 \
  -e NodeAgent__JwtSecret="<shared secret>" \
  --name ctw-node-agent ctw-node-agent
```

Then in the Web UI (**Nodes → Add node**) register it with its **base URL**
(`http://<host>:8080`) and the same **API secret**. In production, terminate TLS in front of
the agent and keep it on a private network.

## Build & run

```bash
dotnet restore && dotnet build
dotnet run --project src/AppHost   # Aspire: Postgres + pgAdmin + Web + MCP, with a live dashboard
dotnet run --project src/Web       # Web app only — needs connection string "ctwdb" and Ctw:* config
                                    # (user-secrets, Ctw__OwnerEmail-style env vars, or appsettings.Development.json)
```

Migrations live in `src/Infrastructure/Persistence/Migrations` and apply automatically on
startup via `OwnerSeeder`. To regenerate:

```bash
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
```

## Step-by-step guide

End-to-end walkthrough: from a clean checkout to running a cBot on both the local host
and a remote external node.

### 1. Configure and start the app

1. Install the prerequisites (.NET 10 SDK, Docker, a demo cTrader ID).
2. Set the owner credentials (used to seed the first admin account on first run). For
   development, user-secrets on `src/Web` or env vars work:

   ```bash
   dotnet user-secrets --project src/Web set "Ctw:OwnerEmail" "owner@example.com"
   dotnet user-secrets --project src/Web set "Ctw:OwnerPassword" "ChangeMe!123"
   ```

   (Data-protection cert values may be left empty in development — keys are then stored
   unencrypted in the DB.)
3. Start everything with Aspire (brings up Postgres, the Web app, MCP, and pgAdmin):

   ```bash
   dotnet run --project src/AppHost
   ```

   Open the Web URL shown in the Aspire dashboard.

### 2. First login

1. Sign in with the owner email/password from step 1.
2. You are forced to change the password on first login — set a new one.

### 3. Create a cBot

Either build one in-browser or upload a compiled `.algo`:

- **Build in-browser** — go to **cBots → New project**, pick C# or Python, edit the code in
  the Monaco editor, then **Build**. The build runs in a sandboxed container and, on success,
  produces a runnable cBot.
- **Upload** — go to **cBots → Upload** and select an existing `.algo` file.

### 4. Add a cTrader ID and trading account

1. Go to **Accounts**.
2. Add a **cTrader ID** (your cTID username + password) — the password is encrypted at rest.
3. Under that cTID, add a **trading account** (account number + broker, e.g. a Spotware demo
   account). Use the cTrader CLI `accounts` command if you need to look up the number.

### 5. Create a parameter set

Go to **Param sets → New**, pick the cBot, and enter the parameters as JSON whose keys match
the cBot's `[Parameter]` names, e.g. `{"Periods": 20}`. (An empty parameter set is rejected;
if a cBot has no parameters, none are passed.)

### 6. Run or backtest locally

1. Enable the local node: **Nodes**, toggle the **local** node **Enabled**. (Or set
   `Ctw:LocalNode:Enabled=true` before startup.)
2. **Backtest** — go to **Backtest**, choose the cBot, trading account, symbol, timeframe,
   date range, image tag (e.g. `5.7.10`), and parameter set, then **Start backtest**. When it
   finishes, open it in the instances table to see the report and equity curve.
3. **Run** — go to **Run**, choose the same inputs, and **Start**. Watch live logs, and
   **Stop** it when done.

### 7. Add and use an external (remote) node

Run the agent on the remote server and register it so the scheduler can dispatch to it.

1. **Pick a shared secret** (≥ 32 chars) for this node.
2. **Build and run the agent** on the remote host (it starts its own docker daemon, so run it
   `--privileged`):

   ```bash
   docker build -f src/ExternalNode/Dockerfile -t ctw-node-agent .
   docker run -d --privileged -p 8080:8080 \
     -e NodeAgent__JwtSecret="<your shared secret>" \
     --name ctw-node-agent ctw-node-agent
   ```

   Confirm it is up: `curl http://<host>:8080/health` returns `Healthy`.
3. **Register the node** in the Web UI: **Nodes → Add node** → set the **name**, **base URL**
   (`http://<host>:8080`), the same **API secret**, a **mode** (Run / Backtest / Mixed), the
   **data dir**, and **max instances**. Save.
4. (Optional) Disable the local node so scheduling prefers the remote one, or leave both —
   the scheduler picks the least-loaded eligible node.
5. **Dispatch as usual** — start a run or backtest exactly as in step 6. The main node mints a
   short-lived JWT, sends the image tag, command, and files to the agent, and the agent pulls
   the image and runs the container on the remote host. Status, logs, reports, stop, and node
   stats all flow back over HTTP; the Nodes page shows the remote node's live CPU/mem/disk.

For production: put the agent behind TLS on a private network, and rotate a node's secret by
updating both the agent's `NodeAgent:JwtSecret` and the node's stored API secret (re-add it).

## Tests

```bash
dotnet test
```

Integration tests use Testcontainers for a real PostgreSQL container — Docker must be available.

## Health checks

- `GET /health` — readiness (includes PostgreSQL connectivity)
- `GET /alive` — liveness

Both endpoints are only mapped in Development; in production they live behind authentication
or a private network as configured by the deployment.

## MCP server

The MCP server is hosted separately under `/mcp` and authenticates via per-user API keys
issued from the Web UI (`ctw_mcp_<hex>`). Tokens are SHA-256 hashed in the database; the
raw value is shown to the user exactly once.

## License & disclaimer

cTrader and the cTrader Console image are property of Spotware Systems Ltd. This project is
an unaffiliated experiment and not endorsed by Spotware.

The entire codebase is provided as-is for educational and experimental purposes. Because it
was generated by an AI, expect rough edges, design inconsistencies, and code that should be
reviewed before deployment.
