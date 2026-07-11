# Contributing to cMind

Thanks for being here. **cMind is built by and for cTrader traders, quants, and developers** —
and it gets better every time one of you opens an issue, files a bug, or ships a PR. Whether you
trade for a living, write cBots, run a prop desk, or just like clean .NET, there's a place for your
contribution here.

> **New to the project? You're exactly who we want.** Skip to
> [Your first contribution in 10 minutes](#your-first-contribution-in-10-minutes) or the
> [Quick wins](#quick-wins--good-first-issues) list. Not sure where to start? Open a
> [Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) and say hi.

---

## Table of contents

- [Why contribute](#why-contribute)
- [Ways to contribute (not just code)](#ways-to-contribute-not-just-code)
- [Your first contribution in 10 minutes](#your-first-contribution-in-10-minutes)
- [Quick wins / good first issues](#quick-wins--good-first-issues)
- [Ground rules](#ground-rules)
- [Prerequisites & setup](#prerequisites--setup)
- [Development workflow](#development-workflow)
- [Issue standard — how to file one we can act on](#issue-standard)
- [Pull request standard — how to get merged fast](#pull-request-standard)
- [What we accept ✅ and what we don't ❌](#what-we-accept--and-what-we-dont-)
- [Dos and don'ts (edge cases)](#dos-and-donts-edge-cases)
- [Coding conventions](#coding-conventions)
- [Contributing with agentic AI 🤖](#contributing-with-agentic-ai-)
- [Commit messages](#commit-messages)
- [Review & merge process](#review--merge-process)
- [Recognition](#recognition)

---

## Why contribute

This is a **trading operations platform that touches real money**. That makes it one of the most
rewarding open-source codebases to work on — and one where your name on a merged PR actually means
something. Concretely, contributing here:

- **Sharpens skills that pay.** Strict Domain-Driven Design, .NET 10, Blazor, EF Core, distributed
  node orchestration, the cTrader Open API, MCP, and production AI integration — all in one repo.
- **Scratches your own itch.** Trade cTrader? The feature you wish existed is a PR away, and you're
  the best-placed person to design it.
- **Ships to real users.** Copy trading, prop-firm guards, and AI codegen are used against live
  accounts. Good fixes protect people's capital.
- **Builds your reputation.** Every merged contributor is credited (see [Recognition](#recognition)).
- **Is genuinely welcoming.** We review fast, explain the "why" behind conventions, and mentor first
  PRs. Ask questions early — that's a strength, not a bother.

**You don't need to be a .NET expert.** Traders who report precise cTrader behavior, testers who find
edge cases, and writers who fix docs are as valuable as the people writing aggregates.

## Ways to contribute (not just code)

| Contribution | Effort | Where to start |
|---|---|---|
| 🐛 Report a reproducible bug | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Suggest a feature or improvement | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Fix a typo, clarify a doc, add an example | 15 min | Edit any `docs/**/*.md` or `README.md` and open a PR |
| 🧪 Add a missing test or a failing repro | 30 min | `tests/UnitTests`, `tests/IntegrationTests` |
| 🎯 Report real cTrader behavior we mis-model | 20 min | Open an issue tagged `copy-trading` with the exact cTrader flow |
| 🛠️ Fix a `good first issue` | 1–2 hrs | [Quick wins](#quick-wins--good-first-issues) |
| 🚀 Build a feature | varies | [Open an issue first](#issue-standard), then a PR |
| 🌐 Improve deploy/ops docs (K8s, Azure, AWS) | varies | `website/docs/deployment/`, `website/docs/operations/` |

## Your first contribution in 10 minutes

```bash
# 1. Fork on GitHub, then clone your fork
git clone https://github.com/<you>/cmind.git
cd cmind

# 2. Build and test (proves your environment works)
dotnet restore
dotnet build
dotnet test

# 3. Branch, change one thing, commit
git checkout -b docs/fix-typo
#   ...make your edit...
git commit -am "docs: fix typo in copy-trading guide"

# 4. Push and open a PR
git push origin docs/fix-typo
```

Then open a PR against `main` using the template. That's it — you're a contributor.

## Quick wins / good first issues

- Browse issues labeled [`good first issue`](https://github.com/amusleh-spotware-com/cmind/labels/good%20first%20issue)
  and [`help wanted`](https://github.com/amusleh-spotware-com/cmind/labels/help%20wanted).
- Fix a doc that drifted from the code (compare a `website/docs/features/*.md` with its source).
- Add a unit test for an untested invariant or state transition on an aggregate.
- Extend `tests/UnitTests/CopyTrading/FakeTradingSession.cs` with a cTrader behavior it doesn't yet
  model (and a test that exercises it).

Comment on the issue to claim it so we don't double up. No response needed to start a doc fix.

## Ground rules

- **Be respectful** — see the [Code of Conduct](CODE_OF_CONDUCT.md).
- **Never commit secrets** (`.pfx`, `.key`, JWT secrets, connection strings, cTrader credentials,
  API keys). They're git-ignored; keep them that way. If you leak one, rotate it immediately and tell
  a maintainer.
- **Report security issues privately** — see [SECURITY.md](SECURITY.md). Do **not** open a public
  issue or PR for a vulnerability.
- **One logical change per PR.** Small, focused PRs get merged; sprawling ones stall.
- **When in doubt, open an issue first.** A 5-minute alignment beats a rejected 500-line PR.

## Prerequisites & setup

- [.NET 10 SDK](https://dotnet.microsoft.com/) (pinned via `global.json`)
- Docker (engine reachable by the Web host — used by the cBot builder and integration tests)
- PostgreSQL (auto-provisioned by .NET Aspire in development)

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AppHost   # full stack via Aspire (Postgres, Web, MCP)
```

For a Web-only run and step-by-step setup, see
**[website/docs/deployment/local.md](website/docs/deployment/local.md)**. AI features stay off until you set an API
key — you never need one to build, test, or run the app.

## Development workflow

1. **Branch** off `main` (`feat/…`, `fix/…`, `docs/…`, `test/…`, `refactor/…`).
2. **Read the conventions.** [CLAUDE.md](CLAUDE.md) is the architecture + rules bible. The domain
   follows **strict Domain-Driven Design** — rich aggregates, value objects over primitives, no
   domain logic in endpoints/services. See [DDD definition of done](CLAUDE.md#hard-mandates).
3. **Make one focused change.**
4. **Build clean:** `dotnet build` must pass. `TreatWarningsAsErrors=true`, no `NoWarn` — fix real
   warnings, including analyzer/`.razor` inspection warnings.
5. **Test every tier the change can reach:** unit **and** integration **and** E2E where applicable
   (see [Testing rules in CLAUDE.md](CLAUDE.md#hard-mandates)). `dotnet test` green,
   including pre-existing tests.
6. **Never call `DateTime.UtcNow`/`Now`** in production code — inject `TimeProvider`. Tests hardcode
   timestamps or use `FakeTimeProvider`.
7. **Update the docs** in the same PR. Each feature has a `website/docs/features/*.md`; a feature isn't
   "done" until its doc matches the code.
8. **Add an EF migration** if the schema changed.
9. **Open a PR** using the template. Link the related issue.

## Issue standard

A good issue is one a maintainer (or an AI agent) can act on **without asking you anything back**.

### Bug reports — required

Use the [bug template](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml).
Include **all** of:

1. **What happened** — expected vs actual, one clear sentence each.
2. **Steps to reproduce** — numbered, from a clean state. "It's broken" is not a repro.
3. **Environment** — .NET SDK version, OS, Docker version, and which component
   (Web / MCP / ExternalNode / AppHost).
4. **Logs / stack trace** — the relevant slice, in a code block. Redact secrets.
5. **Scope** — does it happen on a fresh `main`? Which commit if you can bisect.

For **copy-trading / cTrader behavior** bugs, also state the exact cTrader flow (order type, symbol,
volume, SL/TP, expiry) and what real cTrader does vs what cMind does. Precision here is gold — it's
often the whole fix.

### Feature requests — required

Use the [feature template](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml).
Lead with the **problem**, not the solution: what are you trying to do, and why does the current app
block you? Then propose a solution and note alternatives.

### Before you open one

- **Search first** — dedupe against open/closed issues.
- **Questions & half-formed ideas** go in
  [Discussions](https://github.com/amusleh-spotware-com/cmind/discussions), not Issues.
- **One problem per issue.** Split unrelated bugs.

## Pull request standard

A PR is **ready to review** when every box below is true. This is the bar we hold every PR to
(maintainers included).

### Required

- [ ] **Scoped:** one logical change. Unrelated fixes → separate PRs.
- [ ] **Builds clean:** `dotnet build` passes, zero new warnings (`TreatWarningsAsErrors=true`).
- [ ] **Tests green:** `dotnet test` passes, including pre-existing tests.
- [ ] **Tests added:** new behavior has unit tests (mirrored under `tests/UnitTests/`), plus
      integration/E2E where the change can be exercised there. Bug fixes include a regression test.
- [ ] **DDD respected:** new domain logic lives on an aggregate / value object / domain service — not
      in an endpoint, MCP tool, Razor component, or hosted service. No new public setters on
      entities; no primitive-obsessed domain signatures. See the
      [DDD checklist](CLAUDE.md#hard-mandates).
- [ ] **No secrets committed.**
- [ ] **Docs updated** in the same PR (`website/docs/features/*.md`, `README.md`, `CLAUDE.md`) if behavior or
      setup changed.
- [ ] **EF migration** added if the schema changed.
- [ ] **Conventional Commit** title (see [Commit messages](#commit-messages)).
- [ ] **Linked issue:** `Closes #123` in the description.

### Makes review faster

- A clear **Summary** answering *what* and *why*.
- Changes **grouped by layer** (Core / Infrastructure / Nodes / Web / Mcp / ExternalNode).
- Screenshots / GIFs for UI changes.
- A **Notes for reviewers** section for anything non-obvious, trade-offs, or follow-ups.
- Keep the diff reviewable — under ~400 lines where you can. Split big work into a stack of PRs.

### Draft PRs welcome

Open a **draft** early to get direction before you've polished everything. Mark it ready when the
checklist passes.

## What we accept ✅ and what we don't ❌

### We happily accept ✅

- Bug fixes **with a regression test**.
- New features that fit the trading-operations mission, **discussed in an issue first**.
- More faithful cTrader modelling in `FakeTradingSession` (and the tests that prove it).
- New/expanded tests, especially **failure-path** coverage (disconnect, order rejection, desync,
  token rotation, node death + lease reclaim).
- Docs: clarity, accuracy, examples, deployment/ops guides.
- Performance and safety improvements with before/after evidence.
- Accessibility and UX polish that follows the [UI dialog convention](website/docs/ui-guidelines.md).

### We will ask you to change, or decline ❌

- **Anemic domain code** — public setters, business rules in endpoints/services, primitive-obsessed
  signatures. This is the single most common reason a PR bounces. Read the DDD section first.
- **Untested behavior.** "It works on my machine" without tests is not done — this app moves money.
- **Weakening a test or the simulator to make CI pass.** Fix the code, not the test.
  `FakeTradingSession` must stay cTrader-faithful; don't dumb it down.
- **New warnings, `NoWarn`, or suppressed analyzers** to dodge `TreatWarningsAsErrors`.
- **`DateTime.UtcNow`/`Now`** in production code. Inject `TimeProvider`.
- **Secrets, credentials, or real account data** in code, tests, fixtures, or logs.
- **Giant unfocused PRs** mixing refactor + feature + formatting. Split them.
- **New heavy dependencies** without discussion (e.g. we deliberately call the Anthropic API over raw
  `HttpClient`, not the SDK — see [CLAUDE.md](CLAUDE.md)). Justify any new NuGet package in the issue.
- **Reintroducing removed patterns** — e.g. inline "card with fields + Create button" forms instead
  of MudBlazor dialogs.
- **Scope not in the mission:** unrelated tools, gratuitous rewrites, or "while I was in there"
  drive-by changes.
- **Formatting-only churn** across files you didn't otherwise touch (noise in the diff).
- **Features behind a paywall / license change.** This is MIT and stays MIT.

If a PR is declined, we'll explain why and — where possible — how to reshape it so we *can* take it.

## Dos and don'ts (edge cases)

**Do:**

- **Do** open an issue before a large feature — align on approach to avoid wasted work.
- **Do** put new domain concepts in the right [bounded context / module](src/Core/CLAUDE.md)
  (Access, Authoring, Execution, Portfolio, Alerts).
- **Do** reference other aggregates by **strong ID**, not navigation property, in new code.
- **Do** keep each `SaveChanges` to a **single aggregate**; use domain events for cross-aggregate flows.
- **Do** wrap primitives crossing a domain boundary in a value object (percentages, risk, symbols…).
- **Do** add a `FakeTimeProvider`-driven test when you touch time-dependent code (lease expiry,
  heartbeat staleness, token rotation, order expiry).
- **Do** run `get_file_problems` / a clean build and fix **every** analyzer finding, `.razor`
  included, before marking a PR ready.
- **Do** rebase on `main` and resolve conflicts yourself before requesting review.

**Don't:**

- **Don't** add a cross-aggregate navigation property or mutate another aggregate through one.
- **Don't** add `e.Property<T>(nameof(Subclass.Prop))` from a **base** type's builder for a derived
  TPH property — EF silently never persists it.
- **Don't** use `OfType<Intermediate>()` over the soft-delete filter on TPH — it throws on Npgsql at
  runtime (in-memory unit tests miss it). Query by key, then pattern-match.
- **Don't** project an entity with a one-to-one nav cycle (`Node.LatestStats`/`NodeStats.Node`) into
  an API response — System.Text.Json has no cycle detection → 500. Project scalars.
- **Don't** filter on C#-only computed properties (`Instance.IsActive`/`IsTerminal`) in an
  `IQueryable` — materialize first.
- **Don't** log or persist secrets in plaintext — use `ISecretProtector`.
- **Don't** hardcode strings — use `Core/Constants/`. **Don't** log via `ILogger.LogInformation(...)`
  directly — use the source-generated `LogMessages`.
- **Don't** force-push over a reviewer's in-progress review without a heads-up.

Most of these are hard-won gotchas already documented in [CLAUDE.md](CLAUDE.md#non-inferable-design-decisions)
— skim it once and you'll dodge the traps that bite everyone.

## Coding conventions

Enforced by `.editorconfig`, `Directory.Build.props`, and analyzers. Highlights:

- **Modern C# 14 / .NET 10** (`LangVersion=latest`): collection expressions `[]` (not
  `new List<T>()`/`Array.Empty`), primary constructors, `field` keyword, target-typed `new`,
  `is null`/`is not null` (never `== null`), switch expressions/pattern matching, `required`/`init`
  over setters, raw string literals for JSON/SQL. No legacy syntax an analyzer would flag.
- File-scoped namespaces; `sealed` by default; `private readonly` injected fields; explicit access
  modifiers; primary constructors (service deps first, then factories).
- Config via `IOptionsMonitor<AppOptions>` — no `cfg["Key"]` in business code.
- Log via source-generated `LogMessages` — never `ILogger.LogInformation(...)` directly.
- No magic strings — use `Core/Constants/`. No comments except `TODO`/`FIXME`.
- Early returns over nested `if`. Spell identifiers in full (no `Tp`/`Sl`/`Vm`).
- **Ubiquitous language:** use the domain's names (`CBot`, `ParamSet`, `Instance`, `Node`,
  `AgentMandate`…). No invented synonyms.

See [CLAUDE.md](CLAUDE.md) for the full architecture tour and design decisions.

## Contributing with agentic AI 🤖

**We actively encourage AI-assisted contributions.** cMind was largely built with agentic AI, ships
its own AI + MCP features, and includes machine-readable conventions so an agent can contribute
*correctly* — not just quickly. A well-driven agent produces PRs that pass our bar on the first try.

### Why this matters here

The repo is **agent-ready**:

- **[CLAUDE.md](CLAUDE.md)** — the full ruleset (architecture, DDD law, testing/time mandates,
  gotchas). Point your agent at it.
- **[AGENTS.md](AGENTS.md)** — a short entry point for AI coding agents (Claude Code, Cursor, Copilot,
  Aider, etc.) summarizing where to look and the non-negotiable rules.
- **Machine-checkable gates** — `dotnet build` (warnings = errors), `dotnet test`, and EF migrations
  give an agent a tight, deterministic feedback loop.
- **An MCP server** ships in `src/Mcp` — AI clients can drive cBot and instance tooling directly.

### How to use an agent to contribute (recommended flow)

1. **Feed it the rules.** Start your session by having the agent read `AGENTS.md` and `CLAUDE.md`, plus
   the relevant `website/docs/features/*.md`. In Claude Code these load automatically; in other tools, paste or
   `@`-reference them.
2. **Give it a scoped task** tied to one issue. Agents excel at focused work and drift on vague ones.
3. **Have it plan before coding** — list changes by layer (Core / Infra / Web / Mcp / tests), and
   confirm the approach matches the DDD checklist.
4. **Let it close the loop:** implement → `dotnet build` → `dotnet test` → fix analyzer/`.razor`
   problems → repeat until clean. Don't accept an agent PR that skips this.
5. **Make it write the tests** — unit + integration/E2E for the behavior, including failure paths, and
   extend `FakeTradingSession` when modelling new cTrader behavior.
6. **Review the diff yourself.** You are accountable for what you submit (see disclosure below). Read
   every line; agents can hallucinate APIs, weaken tests, or sneak in anemic domain code — the exact
   things we reject.

### Rules for AI-assisted PRs

- **You own it.** "The AI wrote it" is never an excuse for a bug, a leaked secret, a weakened test, or
  a DDD violation. Submit only code you understand and stand behind.
- **Disclose it.** Tick the "AI-assisted" note in the PR template (or mention it in the description).
  We don't penalize AI use — we value the transparency, and it helps reviewers.
- **Same bar applies.** AI PRs meet the exact same [PR standard](#pull-request-standard). No shortcuts.
- **No auto-filed spam.** Don't point an autonomous agent at our issue tracker to mass-open low-quality
  issues/PRs. Quality over volume — low-effort AI spam will be closed.
- **Keep secrets out of prompts.** Never paste real credentials, tokens, or account data into an AI
  tool.

Done well, an agent turns "I found a bug" into a tested, doc-updated, convention-clean PR in one
sitting. That's the workflow we want to make effortless.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`.

```
feat(web): add rate limiting to login
fix(copy-trading): reconcile partial close after reconnect desync
docs(deployment): clarify node TLS termination
test(nodes): cover lease reclaim exactly at expiry
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`. Keep the subject
imperative and ≤ ~72 chars. Explain the *why* in the body when it isn't obvious.

## Review & merge process

- CI (build + tests + CodeQL) runs on every PR; get it green.
- A maintainer reviews for correctness, tests, DDD compliance, and scope. Expect questions — they're
  how we keep the domain clean, not gatekeeping.
- Address feedback with follow-up commits (don't force-push mid-review without a heads-up); we squash
  on merge.
- Two things unblock most PRs: **tests that prove the behavior** and **domain logic in the right
  place**. Nail those and review is quick.

## Recognition

Every merged contributor is credited in the project's history and release notes. Meaningful,
sustained contributions can earn a maintainer role. We mean it when we say this project is built by
its community — thank you for being part of it. 💛
