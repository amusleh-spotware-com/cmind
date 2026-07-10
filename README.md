# cMind

[![CI](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml)
[![CodeQL](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)

**cMind is a multi-tenant trading operations platform for cTrader.** Build, backtest, run,
and copy trading strategies at scale — with AI assistance built in — from one hardened,
self-hostable app.

---

## Why teams use cMind

- **Copy trading that holds up with real money.** Mirror a master account onto many
  accounts across brokers and cTrader IDs, with per-destination control over sizing,
  direction, symbols, order types (market / market-range / limit / stop / stop-limit),
  stop-loss/take-profit, pending-order expiry, and exact market-range slippage. Connections
  drop, orders fail, tokens rotate — cMind reconciles state without duplicating trades and
  keeps a full audit log for compliance. → [docs/features/copy-trading.md](docs/features/copy-trading.md)
- **AI that does the work, not just chat.** Plain-English strategy → runnable cBot with a
  build-and-self-repair loop; closed-loop parameter optimization; a background risk guard
  that can auto-stop bots; prop-firm exposure guarding; backtest analysis and post-mortems.
  → [docs/features/ai.md](docs/features/ai.md)
- **Scales with a click, heals itself.** Run and backtest across an auto-discovering node
  fleet; copy hosting uses a self-healing lease so a dead node's work is reclaimed
  automatically. → [docs/deployment/scaling.md](docs/deployment/scaling.md)
- **Yours to run.** Self-host on Docker, Kubernetes, Azure, or AWS. Argon2id, encrypted key
  ring, per-node signed tokens, rate limiting, structured logs + OpenTelemetry.
  → [docs/deployment/](docs/deployment/)

## What's inside

| Capability | Docs |
|------------|------|
| Copy trading (mirroring, order types, SL/TP, slippage, sync/desync) | [features/copy-trading.md](docs/features/copy-trading.md) |
| Open API token lifecycle (single valid token per cID, in-place rotation) | [features/token-lifecycle.md](docs/features/token-lifecycle.md) |
| AI assistant, agent, risk guard, alerts, prop-guard | [features/ai.md](docs/features/ai.md) |
| Build & backtest cBots (in-browser Monaco IDE, C# + Python) | [features/build-and-backtest.md](docs/features/build-and-backtest.md) |
| Node fleet & horizontal scaling | [operations/node-discovery.md](docs/operations/node-discovery.md) · [deployment/scaling.md](docs/deployment/scaling.md) |
| MCP server (HTTP + SSE tools for AI clients) | [features/mcp.md](docs/features/mcp.md) |
| Deployment (Compose, K8s/Helm, Azure, AWS) | [deployment/](docs/deployment/) |
| Testing & dev credentials | [testing/](docs/testing/) · [testing/dev-credentials.md](docs/testing/dev-credentials.md) |

## Quick start

```bash
dotnet restore
dotnet run --project src/AppHost      # full stack via .NET Aspire (Postgres, Web, MCP)
```

Then open the Web URL from the Aspire dashboard. For a Web-only run, deployment, and
step-by-step setup, see **[docs/deployment/local.md](docs/deployment/local.md)**.

## Tech

.NET 10 · ASP.NET Core Minimal APIs · Blazor Server (SSR) + MudBlazor · EF Core 10 + PostgreSQL ·
.NET Aspire · Docker · gRPC/Protobuf (cTrader Open API) · Serilog + OpenTelemetry · MCP.
Architecture follows strict Domain-Driven Design — see [CLAUDE.md](CLAUDE.md).

## Contributing · Security · License

- Development guide and conventions: **[CLAUDE.md](CLAUDE.md)** and [CONTRIBUTING.md](CONTRIBUTING.md).
- Report vulnerabilities per [SECURITY.md](SECURITY.md).
- MIT licensed — see [LICENSE](LICENSE). Built with [Claude Code](https://claude.com/claude-code).
