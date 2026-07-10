# Contributing to cMind

Thanks for your interest! cMind is a multi-tenant trading operations platform for cTrader.
Contributions, bug reports, and ideas are welcome.

> The codebase follows strict Domain-Driven Design and ships every feature with unit,
> integration, and E2E tests plus a matching doc under `docs/features/`. See
> [CLAUDE.md](CLAUDE.md) for the conventions before opening a PR. When in doubt, open an
> issue first.

## Ground rules

- Be respectful — see the [Code of Conduct](CODE_OF_CONDUCT.md).
- Never commit secrets (`.pfx`, `.key`, JWT secrets, connection strings, cTrader
  credentials). They are git-ignored; keep them that way.
- Report security issues privately — see [SECURITY.md](SECURITY.md). Do **not** open a
  public issue for vulnerabilities.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) (pinned via `global.json`)
- Docker (engine reachable by the Web host — used by the builder and integration tests)
- PostgreSQL (auto-provisioned by Aspire in development)

## Getting started

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AppHost   # Aspire orchestration
```

## Development workflow

1. **Branch** off `main` (`feat/…`, `fix/…`, `docs/…`).
2. **Make focused changes** — keep PRs small and single-purpose.
3. **Build clean**: `dotnet build` must pass. `TreatWarningsAsErrors=true` — no new
   warnings, no `NoWarn`.
4. **Test**: `dotnet test` green, including pre-existing tests. Add unit tests for new
   classes, mirroring the source path under `tests/UnitTests/`.
5. **Open a PR** using the template. Link related issues.

## Coding conventions

Enforced by `.editorconfig` and `Directory.Build.props`. Highlights:

- File-scoped namespaces; `sealed` by default; `private readonly` injected fields.
- Config via `IOptionsMonitor<AppOptions>` — no `cfg["Key"]` in business code.
- Log via source-generated `LogMessages` extensions — never `ILogger.LogInformation(...)` directly.
- No magic strings — use `Core/Constants/`.
- No comments except `TODO`/`FIXME`.
- Early returns over nested `if`. Spell identifiers in full.

See [`CLAUDE.md`](CLAUDE.md) for the full architecture tour and design decisions.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`,
e.g. `feat(web): add rate limiting to login`.

## Reporting bugs / requesting features

Use the issue templates. Include repro steps, expected vs actual, and environment
(.NET SDK, OS, Docker version).
