# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **AI-first assistant layer** (Claude / Anthropic Messages API). A `Core.Ai.IAiClient`
  abstraction with a raw-HTTP `AnthropicAiClient` in Infrastructure, an `IAiFeatureService`
  orchestrating ten features, and an `AiOptions` config block (`App:Ai`, off unless `ApiKey`
  is set). Features: natural-language cBot codegen, cBot review, backtest analysis, parameter
  optimization, instance post-mortems, market sentiment (web-search grounded), chart-vision
  strategy design, and marketplace curation — surfaced on the new **AI Assistant** page,
  the `/api/ai/*` endpoints, and MCP `AiTools`. A background `AiRiskGuard` (`Nodes`) assesses
  running bots on an interval when `RiskGuardEnabled`.
- AI codegen → buildable project with self-repair (`/api/ai/generate-project`): generates a
  full `CBotSourceProject`, builds it in the sandboxed `CBotBuilder`, and feeds build errors
  back to the model to fix, up to 3 attempts.
- AI closed optimization loop (`/api/ai/optimize-run/{cbotId}`): the model proposes parameter
  sets which are persisted and backtested across nodes via the scheduler — surfaced on the
  AI Assistant **Optimize** tab.

- Strict versioning across all components. A single SemVer product version
  (`VersionPrefix` in `Directory.Build.props`) is shipped in lockstep by every assembly and
  surfaced at runtime via `Core.VersionInfo` (Web app bar + `/version` on Web, MCP, and the
  node agent). A dedicated wire-contract version (`NodeAgentProtocol`) guards the main
  node ↔ external node agent HTTP API: the main node stamps every request with an
  `X-Node-Protocol-Version` header and the agent rejects incompatible callers with
  `426 Upgrade Required`.
- MIT `LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- GitHub issue/PR templates, CI (build + test), CodeQL analysis, and Dependabot config.
- `.dockerignore`.
- Security headers middleware (`Content-Security-Policy`, `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`) on the Web app.
- Rate limiting on authentication endpoints (login brute-force mitigation).
- Time-based login lockout that auto-expires (`AppUser.LockoutEnd`, EF migration
  `AddUserLockoutEnd`) instead of a permanent lock on failed attempts.
- `<meta name="description">` and a fallback `<title>` in the app shell.

### Changed

- Hardened the auth cookie (`HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`).
- OpenAPI document is now exposed only in the Development environment.
- External node `StartAsync` is now idempotent (short-circuits if the container already
  exists) so a retried request can't wipe the running container's work dir.
- Dashboard instance counters collapsed from six sequential `COUNT` queries into a single
  grouped aggregate query.
- `AsNoTracking()` on read-only instance list/detail queries.

<!-- Link references -->
[Unreleased]: https://github.com/amusleh-spotware-com/cmind/commits/main
