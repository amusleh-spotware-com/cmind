# cMind

[![CI](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml)
[![CodeQL](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)

> **Experimental — entirely built by [Claude Code](https://claude.com/claude-code).**
> No human-written code. Spec, scaffolding, refactors, migrations, DI wiring, docs — all
> AI-generated through an interactive session. Reference experiment, not production-ready.

Multi-tenant Blazor Server + Minimal API platform to build, run, and backtest cTrader cBots
across remote nodes and/or the local host — with an in-browser Monaco IDE, an MCP server for
AI integrations, an **AI-first assistant layer** (Claude-powered cBot codegen, backtest
analysis, review, optimization, sentiment, and a background risk guard), and .NET Aspire
orchestration.

## ✨ Features

### 🤖 AI-first (Claude-powered)
- **💬 Strategy Builder** — plain-English prompt → runnable cBot, with a **codegen → build → self-repair** loop
- **⚙️ Optimization loop** — AI proposes param sets, each persisted + backtested across nodes, closed-loop
- **🧠 Autonomous portfolio agent** — mandate-driven proposals + a full decision journal
- **🛡️ Acting risk guard** — background agent AI-assesses running bots, opt-in **auto-stop** on critical risk
- **🚨 Prop-firm exposure guardian** — drawdown/exposure limits with **auto-flatten**
- **📰 Live exposure check** — news-driven de-risk on held symbols (web-search grounded)
- **🗣️ Pre-deploy strategy debate** — bull / bear / risk perspectives before you go live
- **📊 Portfolio digest** — cross-instance AI review
- **🎯 Tune Advisor** — backtest-decay detection
- **🔔 AI market alerts** engine
- **🔍 Plus** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation

### 🏗️ Platform
- **🖥️ In-browser Monaco IDE** — build C# **and** Python cBots via sandboxed `dotnet build`
- **🌐 Auto-discovering node fleet** — agents self-register + heartbeat; scheduler picks least-loaded node
- **📡 MCP server** — HTTP + SSE, per-user API keys, exposes cBot / instance / AI tools
- **📈 Run & backtest** across remote nodes and/or local host, live logs over SignalR, equity-curve charts
- **☸️ Deploy anywhere** — Docker Compose, Helm/K8s, Azure Bicep, AWS Terraform
- **🔒 Hardened** — Argon2id hashing, cert-protected Data Protection key ring, per-node HS256 JWT, rate limiting, security headers
- **🧭 Strict Domain-Driven Design** — rich aggregates, value objects, domain events
- **📟 Observability** — Serilog structured JSON + OpenTelemetry (metrics, traces, logs; OTLP export)

**Contents:** [Stack](#stack) · [Solution layout](#solution-layout) · [Prerequisites](#prerequisites)
· [Configuration](#configuration) · [External nodes](#external-nodes) · [Build & run](#build--run)
· [Deployment](#deployment) · [Step-by-step guide](#step-by-step-guide) · [Tests](#tests) · [Contributing](#contributing)
· [Security](#security) · [License](#license--disclaimer)

ASP.NET Core + Blazor Server app to build, run, backtest (later optimize) cTrader cBots via the
official [cTrader Console Docker image](https://github.com/spotware/ctrader-console-docker),
scheduled across remote nodes (each running the `ExternalNode` HTTP agent) and/or the local web host.

## Stack

- .NET 10, C# (latest)
- ASP.NET Core Minimal APIs
- Blazor Server with SSR
- MudBlazor + custom cTrader-style dark theme
- BlazorMonaco for the in-browser cBot code editor
- Blazor-ApexCharts for backtest charts
- EF Core 10 + Npgsql + PostgreSQL
- ASP.NET Core Data Protection (X.509 cert-protected key ring persisted in PostgreSQL)
- Argon2id password hashing (Konscious)
- HTTP + per-node HS256 JWT for external node connectivity
- ModelContextProtocol .NET SDK for the MCP server
- .NET Aspire 9 for orchestration
- Serilog structured logging (compact JSON) + OpenTelemetry (metrics + traces + logs, OTLP export)
- Self-registering node auto-discovery (agent heartbeat); Docker Compose, Helm, Bicep & Terraform deploy

## Solution layout

| Project              | Description                                                                    |
| -------------------- | ------------------------------------------------------------------------------- |
| `src/AppHost`        | .NET Aspire orchestrator (Postgres, Web, MCP, pgAdmin)                          |
| `src/Core`           | Domain entities, value objects, strong-typed IDs, options, log delegates        |
| `src/Infrastructure` | EF Core (`DataContext`), encryption, Argon2, GHCR client, OTel/health defaults  |
| `src/Nodes`          | Node scheduler, HTTP + local container dispatchers, stats poller, completion pollers |
| `src/ExternalNode`   | Standalone HTTP node agent (JWT auth) that pulls images and runs cBot containers on a remote server |
| `src/Web`            | Blazor Server SSR + Minimal API + SignalR LogsHub (cBots, builder IDE, run/backtest, param sets, nodes, accounts, AI Assistant + `/api/ai/*`) |
| `src/Mcp`            | MCP server (HTTP + SSE) for AI integrations — `CBotTools`, `InstanceTools`, `AiTools` |
| `tests/UnitTests`    | xUnit unit tests                                                                |
| `tests/IntegrationTests` | xUnit + Testcontainers integration tests                                    |

## Prerequisites

- .NET 10 SDK
- Docker (engine reachable by the Web host — used by the builder)
- PostgreSQL (auto-provisioned by Aspire in development)

## Configuration

Settings live under the `App` section, bound to a strongly-typed
[`AppOptions`](src/Core/Options/AppOptions.cs) record via `IOptionsMonitor<AppOptions>`.

```jsonc
{
  "App": {
    "OwnerEmail": "owner@example.com",
    "OwnerPassword": "set-via-secret",
    "DataProtectionCertBase64": "<base64 PFX>",
    "DataProtectionCertPassword": "<pfx password>",
    "DefaultDockerImage": "ghcr.io/spotware/ctrader-console",
    "DefaultDockerTag": "latest",
    "BuildWorkRoot": "/var/app/builds",
    "BuildImage": "mcr.microsoft.com/dotnet/sdk:9.0",
    "LocalNode": { "Enabled": true, "WorkRoot": "/var/app/local", "MaxInstances": 5 },
    "Ai": {
      "ApiKey": "<Anthropic API key>",       // enables all AI features when set
      "Model": "claude-opus-4-8",
      "RiskGuardEnabled": false,              // background risk agent over running bots
      "RiskGuardInterval": "00:05:00"
    }
  }
}
```

`LocalNode` (off by default) schedules run/backtest containers on the web host itself via
`LocalContainerDispatcher` instead of a remote agent.

`Ai` (off unless `ApiKey` is set) powers the AI-first features — natural-language cBot
codegen, backtest analysis, cBot review, parameter optimization, post-mortems, market
sentiment (web-search grounded), chart-vision strategy design, and marketplace curation —
surfaced on the **AI Assistant** page, the `/api/ai/*` endpoints, and the MCP `AiTools`. Calls
go directly to the Anthropic Messages API; when unset, every feature returns a friendly
"not configured" result and the app runs unchanged.

## External nodes

Remote nodes run the **`src/ExternalNode`** agent — an HTTP API the main node calls to pull
images and run cBot containers. No SSH/shell access: main node sends the image, tokenized
`run`/`backtest` command, and files (algo, `params.cbotset`, `ctid.pwd`); the agent runs the
container locally and reports status/report/logs/stats back.

Each request carries a short-lived HS256 JWT (`iss=app-main`, `aud=app-node`, 5-min expiry)
signed with a **per-node shared secret**. Agent only runs images matching `AllowedImagePrefix`
(default `ghcr.io/spotware/`) and finds containers by the `app.instance` label → stateless,
restart-safe.

Agent config (`NodeAgent` section / `NodeAgent__*` env vars):

```jsonc
{
  "NodeAgent": {
    "JwtSecret": "<shared secret, >= 32 chars>",   // must match the value stored for the node
    "DataRoot": "/var/app/data",
    "AllowedImagePrefix": "ghcr.io/spotware/"
  }
}
```

Build the image from `src/ExternalNode/Dockerfile`. It starts its own daemon, so run it `--privileged`:

```bash
docker build -f src/ExternalNode/Dockerfile -t node-agent .
docker run -d --privileged -p 8080:8080 \
  -e NodeAgent__JwtSecret="<shared secret>" \
  --name node-agent node-agent
```

Then in the Web UI (**Nodes → Add node**) register it with its **base URL** (`http://<host>:8080`)
and the same **API secret**. In production, terminate TLS in front of the agent and keep it on a
private network.

### Auto-discovery (no manual add)

Instead of registering each node by hand, agents can **self-register + heartbeat**. Set
`App:Discovery:Enabled=true` and a shared `App:Discovery:JoinToken` (≥ 32 chars) on the main node,
then start agents with `NodeAgent:MainUrl`, `NodeAgent:AdvertiseUrl`, and `NodeAgent:JwtSecret` =
the join token. Agents appear on the **Nodes** page within one heartbeat interval and are marked
unreachable automatically when heartbeats stop. Full details:
[docs/operations/node-discovery.md](docs/operations/node-discovery.md).

## Build & run

```bash
dotnet restore && dotnet build
dotnet run --project src/AppHost   # Aspire: Postgres + pgAdmin + Web + MCP, with a live dashboard
dotnet run --project src/Web       # Web app only — needs connection string "appdb" and App:* config
                                    # (user-secrets, App__OwnerEmail-style env vars, or appsettings.Development.json)
```

Migrations live in `src/Infrastructure/Persistence/Migrations`, apply automatically on startup
via `OwnerSeeder`. Regenerate:

```bash
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
```

## Deployment

| Target | Artifacts | Guide |
| ------ | --------- | ----- |
| **Local** | `docker-compose.yml` + `.env.example` (or Aspire) | [docs/deployment/local.md](docs/deployment/local.md) |
| **Kubernetes** | `deploy/helm/cmind` + `Dockerfile.{web,mcp,node-agent}` | [docs/deployment/kubernetes.md](docs/deployment/kubernetes.md) |
| **Azure** | `deploy/azure/main.bicep` (Container Apps + Postgres Flexible) | [docs/deployment/cloud-azure.md](docs/deployment/cloud-azure.md) |
| **AWS** | `deploy/aws` Terraform (ECS Fargate + RDS + ALB) | [docs/deployment/cloud-aws.md](docs/deployment/cloud-aws.md) |

The fastest local start is Docker Compose — `cp .env.example .env && docker compose up --build`
brings up Postgres + Web + MCP with the schema auto-migrated. Kubernetes and cloud deploys use the
self-registering node agents, so worker capacity scales by adding agent replicas.

## Step-by-step guide

Clean checkout → running a cBot on both the local host and a remote external node.

### 1. Configure and start the app

1. Install prerequisites (.NET 10 SDK, Docker, a demo cTrader ID).
2. Set owner credentials (seed the first admin on first run). Dev: user-secrets on `src/Web` or env vars:

   ```bash
   dotnet user-secrets --project src/Web set "App:OwnerEmail" "owner@example.com"
   dotnet user-secrets --project src/Web set "App:OwnerPassword" "ChangeMe!123"
   ```

   (Data-protection cert values may be empty in dev — keys then stored unencrypted.)
3. Start everything with Aspire (Postgres, Web, MCP, pgAdmin):

   ```bash
   dotnet run --project src/AppHost
   ```

   Open the Web URL from the Aspire dashboard.

### 2. First login

1. Sign in with the owner email/password.
2. Forced password change on first login — set a new one.

### 3. Create a cBot

Build in-browser or upload a compiled `.algo`:

- **Build in-browser** — **cBots → New project**, pick C# or Python, edit in the Monaco editor,
  **Build**. Build runs in a sandboxed container; on success produces a runnable cBot.
- **Upload** — **cBots → Upload**, select an existing `.algo`.

### 4. Add a cTrader ID and trading account

1. Go to **Accounts**.
2. Add a **cTrader ID** (cTID username + password) — encrypted at rest.
3. Under it, add a **trading account** (account number + broker, e.g. a Spotware demo account).
   Use the cTrader CLI `accounts` command to look up the number.

### 5. Create a parameter set

**Param sets → New**, pick the cBot, enter parameters as JSON whose keys match the cBot's
`[Parameter]` names, e.g. `{"Periods": 20}`. Empty parameter sets are rejected; a cBot with no
parameters gets none passed.

### 6. Run or backtest locally

1. Enable the local node: **Nodes**, toggle **local** **Enabled** (or set `App:LocalNode:Enabled=true` before startup).
2. **Backtest** — **Backtest**, choose cBot, trading account, symbol, timeframe, date range,
   image tag (e.g. `5.7.10`), parameter set, **Start backtest**. On finish, open it in the
   instances table for report + equity curve.
3. **Run** — **Run**, same inputs, **Start**. Watch live logs; **Stop** when done.

### 7. Add and use an external (remote) node

1. **Pick a shared secret** (≥ 32 chars).
2. **Build and run the agent** on the remote host (starts its own docker daemon → `--privileged`):

   ```bash
   docker build -f src/ExternalNode/Dockerfile -t node-agent .
   docker run -d --privileged -p 8080:8080 \
     -e NodeAgent__JwtSecret="<your shared secret>" \
     --name node-agent node-agent
   ```

   Confirm: `curl http://<host>:8080/health` returns `Healthy`.
3. **Register the node**: **Nodes → Add node** → set **name**, **base URL** (`http://<host>:8080`),
   same **API secret**, a **mode** (Run / Backtest / Mixed), **data dir**, **max instances**. Save.
4. (Optional) Disable the local node to prefer the remote one, or leave both — scheduler picks
   the least-loaded eligible node.
5. **Dispatch as usual** (step 6). Main node mints a short-lived JWT, sends image tag + command +
   files; agent pulls the image and runs the container on the remote host. Status, logs, reports,
   stop, and stats flow back over HTTP; the Nodes page shows the remote node's live CPU/mem/disk.

Production: put the agent behind TLS on a private network; rotate a node's secret by updating
both the agent's `NodeAgent:JwtSecret` and the node's stored API secret (re-add it).

## Tests

```bash
dotnet test
```

Integration tests use Testcontainers for a real PostgreSQL container — Docker must be available.

## Health checks

- `GET /health` — readiness (includes PostgreSQL connectivity)
- `GET /alive` — liveness
- `GET /version` — product + protocol version (also MCP liveness/readiness)

Mapped in **all** environments so container/Kubernetes probes work in production. Put a reverse
proxy / network policy in front if you don't want them publicly reachable. Logging & observability:
[docs/operations/logging.md](docs/operations/logging.md).

## MCP server

Hosted separately under `/mcp`, authenticated via per-user API keys from the Web UI
(`mcpk_<hex>`). Tokens SHA-256 hashed in DB; raw value shown once.

## Multi-region / multi-node notes

When nodes span regions, keep these in mind:

- **Clock sync**: the main node authenticates each agent call with a short-lived HS256 JWT
  (5-min lifetime, 30s allowed skew). Keep every host **NTP-synced** or requests will be
  rejected as expired / not-yet-valid.
- **Latency & retries**: all outbound HTTP uses a standard resilience handler (retry +
  timeout + circuit breaker via `ConfigureHttpClientDefaults`). Prefer placing a node's
  data dir on fast local storage; cross-region calls only carry commands and small files.
- **Timestamps** are stored and compared in **UTC** throughout.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for setup, coding
conventions, and the PR checklist. By participating you agree to the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Found a vulnerability? **Do not open a public issue.** See [SECURITY.md](SECURITY.md) for
private reporting and the production hardening checklist.

## License & disclaimer

Licensed under the [MIT License](LICENSE).



cTrader and the cTrader Console image are property of Spotware Systems Ltd. This project is an
unaffiliated experiment, not endorsed by Spotware.

Provided as-is for educational/experimental purposes. AI-generated — expect rough edges, design
inconsistencies, and code that should be reviewed before deployment.
