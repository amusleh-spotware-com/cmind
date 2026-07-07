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
