# AGENTS.md

Machine-readable guide for **AI coding agents** (Claude Code, Cursor, Copilot, Aider, Windsurf, and
friends) contributing to cMind. Humans: see [CONTRIBUTING.md](CONTRIBUTING.md). Agents: read this
first, then [CLAUDE.md](CLAUDE.md) for the full ruleset.

cMind welcomes AI-assisted contributions — the repo is built to be agent-friendly. Follow the rules
below and your PR will pass review on the first try.

## Read these before editing

1. **[CLAUDE.md](CLAUDE.md)** — the authoritative ruleset: architecture, strict DDD law, testing/time
   mandates, coding style, and hard-won gotchas. Non-negotiable.
2. **[CONTRIBUTING.md](CONTRIBUTING.md)** — PR/issue standards, what's accepted vs rejected, dos/don'ts.
3. **The relevant `docs/features/*.md`** for the area you're touching.

## What cMind is

Multi-tenant Blazor Server + Minimal API platform for cTrader: build/backtest/run cBots across a node
fleet, and copy-trade across accounts over the cTrader Open API. .NET 10, EF Core + PostgreSQL, .NET
Aspire, MCP server, AI features via the Anthropic API. Architecture: **strict Domain-Driven Design**.

## Repository map

```
src/Core            — pure domain (entities, aggregates, value objects, strong IDs). ZERO infra deps.
src/Infrastructure  — EF Core, encryption, GHCR, Anthropic client, observability.
src/Nodes           — cross-node orchestration, scheduling, dispatch, background services.
src/ExternalNode    — standalone HTTP node agent (runs on remote hosts).
src/Web             — Blazor Server SSR + Minimal API + SignalR.
src/Mcp             — MCP HTTP+SSE server (tools for AI clients).
src/AppHost         — .NET Aspire orchestrator.
tests/UnitTests         — xUnit + FluentAssertions + NSubstitute.
tests/IntegrationTests  — Testcontainers PostgreSQL.
tests/StressTests       — deterministic-simulation copy-trading stress suite.
docs/               — features/, deployment/, operations/, testing/.
```

## Hard rules (do not violate)

- **Strict DDD.** New domain logic goes on an aggregate / value object / domain service — never in an
  endpoint, MCP tool, Razor component, or hosted service. No new public setters on domain entities;
  state changes through intention-revealing methods that guard invariants. Reference other aggregates
  by **strong ID**, not navigation property. One `SaveChanges` mutates one aggregate. Wrap primitives
  crossing a domain boundary in a value object. `src/Core` must compile with **zero** infra
  dependencies. Full checklist: [CLAUDE.md → Domain-Driven Design](CLAUDE.md#domain-driven-design--mandatory).
- **Test every tier the change can reach** — unit **and** integration **and** E2E, including failure
  paths (disconnect, order rejection, desync, token rotation, node death + lease reclaim). Bug fixes
  ship a regression test. `dotnet test` must be green.
- **Never weaken a test or `FakeTradingSession` to pass CI.** The simulator stays cTrader-faithful;
  extend it, don't dumb it down. (`tests/UnitTests/CopyTrading/FakeTradingSession.cs`)
- **Never `DateTime.UtcNow`/`DateTime.Now`** in production code — inject `TimeProvider`
  (`GetUtcNow()`). Tests hardcode timestamps or use `FakeTimeProvider`.
- **Zero warnings.** `TreatWarningsAsErrors=true`, no `NoWarn`. Fix analyzer and `.razor` inspection
  findings too, not just build breaks.
- **No secrets** in code, tests, fixtures, logs, or prompts. Use `ISecretProtector`; strings live in
  `Core/Constants/`; log via source-generated `LogMessages`.
- **UI:** every add/create/edit action uses a MudBlazor dialog, never an inline page form.
- **Docs in the same PR.** Update `docs/features/*.md` when behavior changes.

## Definition of done (self-check before opening a PR)

- [ ] `dotnet build` — clean, zero new warnings.
- [ ] `dotnet test` — green, including pre-existing tests.
- [ ] New behavior covered by unit + integration/E2E tests; bug fix has a regression test.
- [ ] DDD checklist passes; `src/Core` has no infra deps.
- [ ] No `DateTime.UtcNow`/`Now`; no secrets; no magic strings; no direct `ILogger.Log*`.
- [ ] Docs updated; EF migration added if schema changed.
- [ ] Conventional Commit title; PR linked to an issue.

## Recommended agent workflow

1. Read `AGENTS.md` + `CLAUDE.md` + the relevant `docs/features/*.md`.
2. Take one scoped task tied to a single issue.
3. **Plan by layer** (Core / Infrastructure / Nodes / Web / Mcp / tests) before editing; confirm it
   fits the DDD checklist.
4. Implement → `dotnet build` → `dotnet test` → fix every analyzer/`.razor` problem → repeat until
   clean.
5. Write the tests (unit + integration/E2E + failure paths; extend `FakeTradingSession` for new
   cTrader behavior).
6. Update docs, add EF migration if needed.
7. Open a focused PR; disclose AI assistance in the description.

## Common commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AppHost   # full stack via Aspire
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
```

## Accountability

A human contributor owns every AI-assisted PR: review every line, understand it, and stand behind it.
"The AI wrote it" is never an excuse for a bug, a leaked secret, a weakened test, or a DDD violation.
Do **not** point autonomous agents at the issue tracker to mass-file low-quality issues/PRs — quality
over volume; spam is closed.
