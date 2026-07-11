<p align="center">
  <img src="docs/design/banner.svg" alt="cMind — build, run &amp; backtest cTrader bots across a distributed fleet, with an AI core watching the risk." width="100%" />
</p>

<h1 align="center">cMind</h1>

[![CI](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/ci.yml)
[![CodeQL](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml/badge.svg)](https://github.com/amusleh-spotware-com/cmind/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Good first issues](https://img.shields.io/badge/good%20first%20issues-open-7057ff.svg)](https://github.com/amusleh-spotware-com/cmind/labels/good%20first%20issue)
[![AI-assisted PRs welcome](https://img.shields.io/badge/AI--assisted%20PRs-welcome-8A2BE2.svg)](AGENTS.md)
[![PWA · mobile-first](https://img.shields.io/badge/PWA-mobile--first-26C281.svg)](docs/ui-guidelines.md)

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
- **In your pocket.** A mobile-first, fully responsive UI you can **install as an app** on your
  phone (PWA) — bottom-nav navigation, card layouts, offline shell. Every surface is
  white-labelable, so resellers ship it as their own. → [docs/ui-guidelines.md](docs/ui-guidelines.md)

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
| Design system, mobile-first & PWA, white-label theming | [ui-guidelines.md](docs/ui-guidelines.md) |
| Testing & dev credentials | [testing/](docs/testing/) · [testing/dev-credentials.md](docs/testing/dev-credentials.md) |

## Quick start

```bash
dotnet restore
dotnet run --project src/AppHost      # full stack via .NET Aspire (Postgres, Web, MCP)
```

Then open the Web URL from the Aspire dashboard. For a Web-only run, deployment, and
step-by-step setup, see **[docs/deployment/local.md](docs/deployment/local.md)**.

## Tech

.NET 10 · ASP.NET Core Minimal APIs · Blazor Server (SSR) + MudBlazor · mobile-first responsive UI +
installable PWA · white-label theming · EF Core 10 + PostgreSQL · .NET Aspire · Docker ·
gRPC/Protobuf (cTrader Open API) · Serilog + OpenTelemetry · MCP · Playwright E2E (mobile + desktop).
Architecture follows strict Domain-Driven Design — see [CLAUDE.md](CLAUDE.md) and
[docs/ui-guidelines.md](docs/ui-guidelines.md).

## Contributing — we'd love your help 💛

**cMind is built by and for cTrader traders, quants, and developers.** Every bug report, doc fix, and
PR makes it better — and you don't need to be a .NET expert to start. Traders who report precise
cTrader behavior are as valuable as the people writing aggregates.

- 🐛 [Report a bug](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml)
  · 💡 [Request a feature](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml)
  · 💬 [Ask in Discussions](https://github.com/amusleh-spotware-com/cmind/discussions)
- 🚀 **New here?** Start with a [good first issue](https://github.com/amusleh-spotware-com/cmind/labels/good%20first%20issue)
  or the [10-minute first contribution](CONTRIBUTING.md#your-first-contribution-in-10-minutes).
- 🤖 **AI-assisted contributions are welcome and encouraged** — the repo is agent-ready. See
  **[AGENTS.md](AGENTS.md)** and [Contributing with agentic AI](CONTRIBUTING.md#contributing-with-agentic-ai-).
- 📋 Full guide, PR/issue standards, and what we accept: **[CONTRIBUTING.md](CONTRIBUTING.md)**.
  Architecture + conventions: **[CLAUDE.md](CLAUDE.md)**.

Every merged contributor is credited. Come build the platform you wish existed. → **[Start here](CONTRIBUTING.md)**

## Security · License

- Report vulnerabilities per [SECURITY.md](SECURITY.md).
- MIT licensed — see [LICENSE](LICENSE). Built with [Claude Code](https://claude.com/claude-code).
