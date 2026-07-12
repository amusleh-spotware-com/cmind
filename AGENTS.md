# AGENTS.md

Machine-readable guide for **AI coding agents** (Claude Code, Cursor, Copilot, Aider, Windsurf, friends) contributing to cMind. Humans: see [CONTRIBUTING.md](CONTRIBUTING.md). Agents: read this first, then [CLAUDE.md](CLAUDE.md) for full ruleset.

cMind welcome AI-assisted contribution — repo built agent-friendly. Follow rules below, PR pass review first try.

## Read these before editing

1. **[CLAUDE.md](CLAUDE.md)** — authoritative ruleset: architecture, strict DDD law, testing/time mandates, modern-C# + coding style, hard-won gotchas. Non-negotiable.
2. **Nested `CLAUDE.md`** in the tree you edit — `src/Core`, `src/Infrastructure`, `src/Web`, `tests` carry layer-specific rules and gotchas. They auto-load in Claude Code; in other tools open the one for your area.
3. **[CONTRIBUTING.md](CONTRIBUTING.md)** — PR/issue standards, accepted vs rejected, dos/don'ts.
4. **Relevant `website/website/docs/features/*.md`** for the area you touch (canonical docs; published at
   https://amusleh-spotware-com.github.io/cmind — top-level `docs/` are redirect stubs).

## What cMind is

Multi-tenant Blazor Server + Minimal API platform for cTrader: build/backtest/run cBots across node fleet, copy-trade across accounts over cTrader Open API. .NET 10, EF Core + PostgreSQL, .NET Aspire, MCP server, AI features via Anthropic API. Architecture: **strict Domain-Driven Design**.

## Repository map

```
src/Core            — pure domain (entities, aggregates, value objects, strong IDs). ZERO infra deps.
src/Infrastructure  — EF Core, encryption, GHCR, Anthropic client, observability.
src/Nodes           — cross-node orchestration, scheduling, dispatch, background services.
src/CtraderCliNode  — standalone HTTP node agent (remote hosts); runs/backtests cBots via the cTrader CLI.
src/Web             — Blazor Server SSR + Minimal API + SignalR.
src/Mcp             — MCP HTTP+SSE server (tools for AI clients).
src/AppHost         — .NET Aspire orchestrator.
tests/UnitTests         — xUnit + FluentAssertions + NSubstitute.
tests/IntegrationTests  — Testcontainers PostgreSQL.
tests/StressTests       — deterministic-simulation copy-trading stress suite.
website/docs/       — CANONICAL docs (Docusaurus site, published to GitHub Pages).
design/             — brand assets (logo/banner SVGs, brand brief, app screenshots) at repo root.
```

## Hard rules (do not violate)

- **Strict DDD.** New domain logic go on aggregate / value object / domain service — never in endpoint, MCP tool, Razor component, hosted service. No new public setters on domain entities; state change through intention-revealing methods that guard invariants. Reference other aggregates by **strong ID**, not navigation property. One `SaveChanges` mutate one aggregate. Wrap primitives crossing domain boundary in value object. `src/Core` must compile with **zero** infra deps. Full checklist: [CLAUDE.md → Domain-Driven Design](CLAUDE.md#hard-mandates).
- **Test every tier change can reach** — unit **and** integration **and** E2E, including failure paths (disconnect, order rejection, desync, token rotation, node death + lease reclaim). Bug fix ship regression test. `dotnet test` must be green.
- **Never weaken test or `FakeTradingSession` to pass CI.** Simulator stay cTrader-faithful; extend it, don't dumb down. (`tests/UnitTests/CopyTrading/FakeTradingSession.cs`)
- **Never `DateTime.UtcNow`/`DateTime.Now`** in production code — inject `TimeProvider` (`GetUtcNow()`). Tests hardcode timestamps or use `FakeTimeProvider`.
- **Zero warnings.** `TreatWarningsAsErrors=true`, no `NoWarn`. Fix analyzer and `.razor` inspection findings too, not just build breaks.
- **Modern C# 14 / .NET 10** (`LangVersion=latest`). Collection expressions `[]` (not `new List<T>()`/`Array.Empty`); primary constructors; `field` keyword; target-typed `new`; `is null`/`is not null` (never `== null`); switch expressions/pattern matching; file-scoped namespaces; `required`/`init` over setters; raw string literals for JSON/SQL. No legacy syntax an analyzer flags. Modernize the lines you touch.
- **No secrets** in code, tests, fixtures, logs, prompts. Use `ISecretProtector`; strings live in `Core/Constants/`; log via source-generated `LogMessages`.
- **UI:** every add/create/edit action use MudBlazor dialog, never inline page form.
- **Docs in same PR.** Update `website/website/docs/features/*.md` (canonical) when behavior changes.

## Definition of done (self-check before opening a PR)

- [ ] `dotnet build` — clean, zero new warnings.
- [ ] `dotnet test` — green, including pre-existing tests.
- [ ] New behavior covered by unit + integration/E2E tests; bug fix has regression test.
- [ ] DDD checklist passes; `src/Core` has no infra deps.
- [ ] No `DateTime.UtcNow`/`Now`; no secrets; no magic strings; no direct `ILogger.Log*`.
- [ ] Docs updated; EF migration added if schema changed.
- [ ] Conventional Commit title; PR linked to issue.

## Recommended agent workflow

1. Read `AGENTS.md` + `CLAUDE.md` + relevant `website/website/docs/features/*.md`.
2. Take one scoped task tied to single issue.
3. **Plan by layer** (Core / Infrastructure / Nodes / Web / Mcp / tests) before editing; confirm fit DDD checklist.
4. Implement → `dotnet build` → `dotnet test` → fix every analyzer/`.razor` problem → repeat till clean.
5. Write tests (unit + integration/E2E + failure paths; extend `FakeTradingSession` for new cTrader behavior).
6. Update docs, add EF migration if needed.
7. Open focused PR; disclose AI assistance in description.

## Common commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AppHost   # full stack via Aspire
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
```

## Accountability

Human contributor own every AI-assisted PR: review every line, understand it, stand behind it. "AI wrote it" never excuse for bug, leaked secret, weakened test, DDD violation. Do **not** point autonomous agents at issue tracker to mass-file low-quality issues/PRs — quality over volume; spam closed.