---
title: Přehled architektuры
description: Jak je cMind složeno — moduly, jak se požadavek build/backtest/copy šíří přes webový hostitel a flotilu uzlů a neočividná designová rozhodnutí za ním.
sidebar_position: 5
---

# Přehled architektury

cMind je platforma s více tenanty **Blazor Server + Minimal API** pro cTrader, postavená na **.NET 10 /
C# 14**, EF Core + PostgreSQL a .NET Aspire, s MCP serverem a jádrem AI. Dodržuje
**přísný Domain-Driven Design**: obchodní pravidla se nacházejí na agregátech a objektech hodnot v čistém
`Core` a všechno ostatní orchestruje.

Tato stránka je mapa. Pro *proč* za konkrétními volbami viz
[Záznamy rozhodnutí o architektuře](./adr/README.md).

## Modules

| Project | Responsibility |
|---|---|
| `src/Core` | Pure domain — entities, aggregates, value objects, strong IDs, domain events, Core-side interfaces. **Zero** infra dependencies (no EF/HttpClient/Docker/ASP.NET). |
| `src/Infrastructure` | EF Core + PostgreSQL, DataProtection encryption, GHCR client, Anthropic AI client, observability. |
| `src/Nodes` | Cross-node orchestration — scheduling, dispatch, pollers, background services. |
| `src/CtraderCliNode` | Standalone HTTP node agent on remote hosts (JWT-auth, no shell). Runs and backtests cBots by driving the **cTrader CLI** inside a docker container — and will optimize too, once the cTrader CLI adds it. |
| `src/CopyEngine` | The copy-trading host: mirrors trades from a source account onto destinations. |
| `src/CTraderOpenApi` | cTrader Open API client (protobuf over TCP/SSL) — auth, trading session, equity. |
| `src/Web` | Blazor Server SSR + Minimal API + SignalR + MudBlazor UI. |
| `src/Mcp` | MCP HTTP+SSE server exposing tools to AI clients. |
| `src/AppHost` | .NET Aspire orchestrator (Postgres, Web, MCP, pgAdmin). |

## The big picture

```mermaid
flowchart TB
    subgraph Clients
        Browser["Browser / PWA"]
        AiClient["AI client (MCP)"]
    end

    subgraph WebHost["Web host (src/Web)"]
        UI["Blazor SSR + MudBlazor"]
        Api["Minimal API endpoints"]
        Builder["CBotBuilder (Docker socket)"]
        LocalNode["Local node dispatcher"]
    end

    Mcp["MCP server (src/Mcp)"]
    Core["Core domain (aggregates, value objects)"]
    Db[("PostgreSQL (EF Core)")]

    subgraph Fleet["Node fleet"]
        ExtNode["CtraderCliNode agent (HTTP + JWT)"]
        Docker["ctrader-console containers"]
    end

    Copy["CopyEngine host"]
    OpenApi["cTrader Open API"]
    Anthropic["Anthropic API"]

    Browser --> UI --> Api
    AiClient --> Mcp --> Core
    Api --> Core
    Core --> Db
    Api --> Builder --> Docker
    Api -->|NodeScheduler + ContainerDispatcherFactory| LocalNode & ExtNode
    ExtNode --> Docker
    Copy --> OpenApi
    Core -->|AiFeatureService| Anthropic
```

## Request flows

### Build & backtest

1. A user submits a cBot source project. `CBotBuilder` runs **on the web host** (it needs the Docker
   socket) inside a throwaway SDK container with a bind-mounted `/work` and a shared
   `app-nuget-cache` volume, so untrusted MSBuild can't reach the host filesystem or network.
2. Run/backtest containers execute on a node chosen by `NodeScheduler`, dispatched through
   `ContainerDispatcherFactory` → either `Http` (a remote `CtraderCliNode` agent) or `Local` (the web
   host's own node).
3. Containers run `ghcr.io/spotware/ctrader-console` with `--exit-on-stop`. Pollers
   (`RunCompletionPoller`, `BacktestCompletionPoller`) reconcile self-exited containers: exit 0/null
   ⇒ Stopped, non-zero ⇒ Failed.

Instance state is **TPH, and a transition replaces the entity** (the discriminator can't change), so
an instance **id changes** starting → running → terminal. The **container id is stable** and carried
over; the HTTP agent is keyed by container id for status/report/stop/logs.

### cTrader CLI nodes

cTrader CLI nodes get **no SSH or shell**. The main app talks to each agent over HTTP; every request
carries a short-lived HS256 **JWT** (5-minute, `iss=app-main` / `aud=app-node`) signed with that
node's secret. The agent only runs images matching `AllowedImagePrefix`, execs docker via
`ArgumentList` (never a shell), and is stateless (it finds containers by the `app.instance` label).
Agents self-register and heartbeat to `POST /api/nodes/register`; the main app upserts the
`CtraderCliNode` **by name** so it survives IP changes.

### Copy trading

`CopyEngineSupervisor` (a `BackgroundService`) reconciles running copy profiles with live
`CopyEngineHost` instances — claiming profiles via an atomic DB lease (so two nodes never
double-copy), renewing leases, and restarting dead hosts. Each `CopyEngineHost` connects to the
cTrader Open API, mirrors source executions onto destinations through the pure `CopyDecisionEngine`
(direction/latency/slippage filters + sizing), and self-heals via resync + partial-fill true-up.

### AI

AI is **fully gated on `AppOptions.Ai.ApiKey`** — unset ⇒ every feature returns `AiResult.Fail` and
the app runs unchanged (no key needed for build/test/E2E). `IAiClient` calls Anthropic over **raw
HTTP** (a typed `HttpClient`), deliberately not the SDK. `AiFeatureService` is the single
orchestrator shared by Web endpoints, the MCP `AiTools`, and `AiRiskGuard`.

## Cross-cutting rules

- **One `SaveChanges` mutates one aggregate.** Cross-aggregate flows use domain events dispatched by
  an EF interceptor.
- **Aggregates reference each other by strong ID**, never navigation property.
- **No ambient clock.** Code injects `TimeProvider`; domain methods take a `DateTimeOffset now`.
- **Secrets** are encrypted via `ISecretProtector` (`EncryptionPurposes`); **strings** live in
  `Core/Constants/`; **logs** go through source-generated `LogMessages`.

These are enforced in CI: the analyzer sweep, the zero-warning build, and
`ArchitectureGuardTests` (which fail the build on an ambient-clock read, a Core infra dependency, or
a direct `ILogger.Log*` call).
