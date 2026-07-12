# Changelog

All notable changes documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Copy trading** over cTrader Open API: cID OAuth onboarding, faithful order mirroring
  (market/pending types, expiry, market-range slippage, partial close, SL/TP amend, trailing,
  partial-fill true-up), distributed `CopyEngineHost` with node lease + affinity, self-healing
  lease reclaim, in-place token rotation, resync/desync recovery, circuit breaker. Provider
  marketplace + listings (`PerformanceFee` value object), performance-fee engine,
  execution-transparency ledger, copy notifications, AI copy recommender. Backed by
  cTrader-faithful `FakeTradingSession`, deterministic stress suite (`tests/StressTests`),
  K8s live-E2E harness.
- **Prop-firm challenge simulation**: live Open API equity tracking, node-leased evaluation,
  all challenge rule types (max drawdown, daily loss, profit target).
- **White-label branding**, per-deployment **feature toggles**, **compliance/legal** page.
- **Node auto-discovery**: agents self-register + heartbeat to `POST /api/nodes/register`
  (join-token, protocol-version gated); `NodeHeartbeatMonitor` reconciles staleness.
- **Cloud-native observability**: `trace_id`/`span_id` log correlation, OTel metrics/traces,
  native Azure App Insights export, AWS X-Ray/CloudWatch via ADOT sidecar.
- **Full DDD migration** — rich aggregates, value objects over primitives, one aggregate per
  transaction, cross-aggregate references by strong ID, domain events — now enforced
  standard. `TimeProvider` replaces all `DateTime.UtcNow` for deterministic time tests.
- **AI-first assistant layer** (Claude / Anthropic Messages API). `Core.Ai.IAiClient`
  abstraction with raw-HTTP `AnthropicAiClient` in Infrastructure, `IAiFeatureService`
  orchestrating ten features, `AiOptions` config block (`App:Ai`, off unless `ApiKey`
  set). Features: natural-language cBot codegen, cBot review, backtest analysis, parameter
  optimization, instance post-mortems, market sentiment (web-search grounded), chart-vision
  strategy design, marketplace curation — surfaced on new **AI Assistant** page,
  `/api/ai/*` endpoints, MCP `AiTools`. Background `AiRiskGuard` (`Nodes`) assesses
  running bots on interval when `RiskGuardEnabled`.
- AI codegen → buildable project with self-repair (`/api/ai/generate-project`): generates
  full `CBotSourceProject`, builds in sandboxed `CBotBuilder`, feeds build errors
  back to model to fix, up to 3 attempts.
- AI closed optimization loop (`/api/ai/optimize-run/{cbotId}`): model proposes parameter
  sets, persisted + backtested across nodes via scheduler — surfaced on
  AI Assistant **Optimize** tab.

- Strict versioning across all components. Single SemVer product version
  (`VersionPrefix` in `Directory.Build.props`) shipped in lockstep by every assembly,
  surfaced at runtime via `Core.VersionInfo` (Web app bar + `/version` on Web, MCP,
  node agent). Dedicated wire-contract version (`NodeAgentProtocol`) guards main
  node ↔ cTrader CLI node agent HTTP API: main node stamps every request with
  `X-Node-Protocol-Version` header, agent rejects incompatible callers with
  `426 Upgrade Required`.
- MIT `LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- GitHub issue/PR templates, CI (build + test), CodeQL analysis, Dependabot config.
- `.dockerignore`.
- Security headers middleware (`Content-Security-Policy`, `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`) on Web app.
- Rate limiting on auth endpoints (login brute-force mitigation).
- Time-based login lockout that auto-expires (`AppUser.LockoutEnd`, EF migration
  `AddUserLockoutEnd`) instead of permanent lock on failed attempts.
- `<meta name="description">` and fallback `<title>` in app shell.

### Changed

- Hardened auth cookie (`HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`).
- OpenAPI document now exposed only in Development environment.
- cTrader CLI node `StartAsync` now idempotent (short-circuits if container already
  exists) so retried request can't wipe running container's work dir.
- Dashboard instance counters collapsed from six sequential `COUNT` queries into single
  grouped aggregate query.
- `AsNoTracking()` on read-only instance list/detail queries.

<!-- Link references -->
[Unreleased]: https://github.com/amusleh-spotware-com/cmind/commits/main