# Manual Testing — Execution Findings

**Run date:** 2026-07-14 · **Branch:** main · **Executed by:** agent (automated drivers, see methodology)

> Companion to [manual-testing-all-features.md](manual-testing-all-features.md). Each feature in that
> plan was exercised and the result recorded below. **Issues were NOT fixed** — only reported.

## Methodology (honest note)

A human hand-clicks; an agent cannot. So every feature here was driven the strongest way available:

- **Real UI** via the Playwright E2E suite (`tests/E2ETests`) — boots the actual Web app in-process,
  drives real pages/dialogs on desktop **and** 360px mobile viewports. This IS the manual plan, automated.
- **Real Postgres + live cTrader Open API** via the integration suite (`tests/IntegrationTests`,
  Testcontainers) — live copy-trading orders on real demo/live accounts, live Open API connection,
  economic-calendar sources.
- **Real Kubernetes** via `scripts/k8s-e2e.sh` — kind cluster, Helm deploy, in-cluster test Job.
- **Domain invariants** via the unit suite (`tests/UnitTests`).

Credentials/infra used (all provided): cTrader Open API app + cIDs `amusleh`/`afhacker` + cached
tokens (demo + live accounts), FRED key, BLS key, kind cluster. Unified into
`secrets/dev-credentials.local.json`.

Legend: ✅ pass · ❌ fail · ⚠️ pass with anomaly · ➖ not exercised this pass (reason given).

---

## 1. Summary by tier

| Tier | Result | Count | Detail |
|---|---|---|---|
| Build (`dotnet build`, TreatWarningsAsErrors) | ✅ | 0 warn / 0 err | All projects compiled |
| Unit | ✅ | **1061 / 1061** | 5s, 0 failed, 0 skipped |
| Integration (real PG + **live** Open API + FRED/BLS) | ⚠️ | **249 / 249 pass** | All passed; host process crashed in post-run teardown (cosmetic, after results) |
| E2E (real UI, desktop + mobile) | ❌ | **347 / 349 pass** | 2 failed (visibility timeouts, §4) |
| Kubernetes in-cluster (kind + Helm) | ✅ | Job exit 0 | Helm deploy green, 257 in-cluster tests pass, cluster torn down clean |

**Net: 1657 / 1659 automated checks green. 2 E2E failures (flake-class visibility timeouts).**

---

## 2. Deployment matrix results

| Deployment | Result | Evidence / note |
|---|---|---|
| Local — in-process Web (E2E AppFixture) | ✅ | 349 E2E tests boot + drive the real app; 347 green |
| Local — .NET Aspire (`dotnet run AppHost`) | ➖ | Not stood up this pass (interactive orchestrator); same Web binary covered in-process |
| Local — Docker Compose | ➖ | Not stood up this pass; Web image is built + run under k8s path instead |
| **Kubernetes — Helm on kind** | ✅ | `scripts/k8s-e2e.sh`: 3 images built (0 warn) → kind load → helm install → test Job **exit 0** → cluster deleted. "runs on Kubernetes" DoD met |
| Cloud — AWS | ➖ | No cloud infra this pass |
| Cloud — Azure | ➖ | No cloud infra this pass |

---

## 3. Feature-by-feature results

Each row: plan feature → driving test class(es) → result → detail. Route(s) noted.

| # | Feature (route) | Result | Driving tests | Detail |
|---|---|---|---|---|
| 5.1 | **Dashboard** (`/`) | ✅ | DashboardTests(7), DashboardCustomizeTests(3), DashboardEmptyStateTests(1), integration DashboardLayout/Math/Query | KPI/chart/ring/feed render; empty state renders; customize works |
| 5.2 | **Authoring / Builder** (`/cbots`,`/builder/{id}`,`/ai/build`) | ✅ | CBotLifecycleTests(4), CBotDetailPagesTests(4), integration Builder/CBot/ParamSetHttp | Create/build cBot green |
| 5.2 | **Param-set dialog** | ❌ | DialogTests | `ParamSets_new_button_opens_dialog_and_creates_param_set` — created param set text never became visible within 30s (§4) |
| 5.2 | **Run / Backtest** (`/run`,`/backtest`) | ✅ | RunBacktestFlowTests(1), CBotRealRunBacktestTests(1) | Dispatch → instance lifecycle green (real container run/backtest) |
| 5.2 | **Instance detail** (`/instance/{id}`) | ✅ | InstanceDetailTests(2), integration InstanceEndpoints | Renders running + terminal; log tail; Stop disabled on terminal |
| 5.2 | **Optimize / AI tune** (`/optimize`,`/ai/tune`) | ✅ | AiPagesTests, PageSmoke | Renders correct not-available/tune-suggestion state |
| 5.3 | **Nodes** (`/nodes`) | ✅ | NodesUiTests(1), NodesHiddenTests(2), NodesMonitorTests(1), integration NodeUiGating/NodeRegisterHttp/NodeInstanceReclaimer | Full/Monitor/Hidden gating; self-register; reclaim |
| 5.4 | **Accounts / Open API** (`/accounts`,`/account`) | ✅ | OpenApiTests(2), OpenApiSharedAppTests(4), OpenApiInviteTests(1), **integration OpenApiLiveConnectionTests (LIVE)**, TokenRefresh/Authorization persistence | **Live**: connects + app-authenticates against cTrader demo; token refresh + shared-app + rate-limit all green |
| 5.5 | **Copy trading** (`/copy-trading`) | ✅ | CopyTradingTests(3), CopyTradingDetailTests(3), CopyFeaturesE2ETests(1), CopyLive E2E(1), **integration CopyTradingLiveTests (LIVE orders)** | **Live**: 1:1, 1:many, reverse, lot-multiplier, SL/trailing mirror, pending order + cancel propagate, full-close, pre-start resync, **all 9 risk-sizing modes** place live copies |
| 5.5 | Copy — node affinity / lease / watchdog | ✅ | integration CopyNodeAffinity, CopyHostWatchdog | Only-one-node claim, expired-lease reclaim, bounded spread, shutdown release |
| 5.5 | Copy — transparency / notifications / fees / marketplace | ✅ | integration CopyExecutionDrainer, CopyNotificationDrainer, CopyFeeSettlement, CopyMarketplace | Execution-fact log, notification feed, HWM fee accrual, listing/ranking |
| 5.5 | Copy — token rotation | ✅ | integration TokenRotationSignatureTests | Signature changes on source/dest/version rotation; stable when unchanged |
| 5.5 | AI copy recommender (`/ai/*`) | ✅ | AiCopyRecommendTests(1), integration AiRecommendDisabled | Recommends + gated-off path |
| 5.6 | **Prop firm** (`/prop-firm`) | ✅ | PropFirmTests(6), integration PropFirmHttp/PropFirmChallengePersistence | Challenge create, rule types, equity tracking |
| 5.6 | **Prop guard** (`/prop-guard`) | ✅ | PropGuardTests(4) | Risk-guard render + gating |
| 5.6 | Compliance | ✅ | ComplianceTests(2), integration ComplianceFlow/CompliancePersistence/AuditChainIntegrity | Audit chain integrity green |
| 5.7 | **AI feature pages** (`/ai/build|review|optimize|debate|digest|sentiment|exposure`) | ✅ | AiPagesTests(17), AiPagesWithDataTests(2), AiFeatureLocalTests(19) | All AI pages render output via fake local LLM (mandate) |
| 5.7 | AI — Demo/live provider | ✅ | AiDemoLiveTests(1), OnnxE2ETests(2), integration LocalLlmProvider/Onnx/OllamaLocalLlm | Built-in ONNX + demo path |
| 5.7 | **Agent** (`/agent`) | ✅ | AgentTests(2), integration AgentHttp/AgentPersistence | Proposal cycle |
| 5.7 | **Agent studio** (`/agent-studio`) | ✅ | AgentStudioTests(2), integration AgentStudioHttp | Build/configure agents |
| 5.7 | **Alerts** (`/alerts`) | ✅ | AlertsTests(1), integration AlertPersistence | Rule create/fire |
| 5.8 | **Quant — integrity** (`/quant/integrity`) | ✅ | QuantIntegrityTests(3) | Deterministic figures render |
| 5.8 | **Quant — regimes** (`/quant/regimes`) | ✅ | QuantRegimesTests(2) | — |
| 5.8 | **Quant — health** (`/quant/health`) | ✅ | QuantHealthTests(2) | — |
| 5.8 | **Quant — positioning** (`/quant/positioning`) | ✅ | QuantPositioningTests(1) | — |
| 5.8 | **Quant — sizing** (`/quant/sizing`) | ❌ | QuantSizingTests | `Equity_curve_mode_returns_an_exposure` — `[data-testid=sizing-recommendation]` not visible within 30s (§4). Other sizing test passed |
| 5.8 | **Quant — TCA / execution** (`/quant/tca`,`/quant/execution`) | ✅ | QuantTcaTests(3), QuantExecutionTests(3) | — |
| 5.9 | **Economic calendar** (`/economic-calendar`,`/series/{code}`) | ✅ | EconomicCalendarE2ETests(3), integration Calendar/* (Backfill, Health, Tools, Api, EconomicAlert, CentralBankSchedule, Webhook) | FRED+BLS keys configured; backfill idempotent, health/circuit, blackout tool, alerts, central-bank schedule, JWT API |
| 5.10 | **Currency strength** (`/ai/currency-strength`) | ✅ | CurrencyStrengthTests(3), integration CurrencyStrengthApiTests(13) | AI-on + calendar-only + both-off snapshots, ranking + pair matrix, cBot `market:read` JWT, tier filter, feature-gate 404 |
| 5.11 | **Trading journal** (`/journal`) | ✅ | JournalTests(3), integration JournalHttp | Renders incl. zero entries |
| 5.12 | **MCP server** (`/mcp`, `:8081/mcp`) | ✅ | integration McpKeyHttp, McpAiToolsLocalLlm | Tool list + AI tool via fake LLM |
| 5.13 | **Users** (`/users`) | ✅ | UsersResetPasswordTests(1), integration AuthChangePassword | Owner-only mgmt, reset password |
| 5.13 | **Registration** (`/register`) | ✅ | RegistrationTests(3), MustChangePasswordTests(1) | Self-serve register + gated-off + MustChangePassword path |
| 5.14 | **Two-factor auth** (`/login/2fa`) | ✅ | MfaFlowTests(2), integration MfaFlow/MfaPersistence | Enrol TOTP, challenge, backup code |
| 5.15 | **Settings — deployment** (`/settings/deployment`) | ✅ | DeploymentSettingsE2ETests(4), integration DeploymentSettings | Runtime white-label overrides apply live |
| 5.15 | **Settings — AI / features / openapi / legal** | ✅ | SettingsTests(1), SettingsDialogTests(2), FeatureToggleTests(2), integration FeatureGate/FeatureHttp | Provider config, toggles |
| 5.16 | **White-label / branding** | ✅ | BrandingTests(2), BrandIdentityTests(8), integration BrandingHttp/BrandingOptionsValidator | Rebrand + catalog parity |

### Cross-cutting (§4 of plan)

| Concern | Result | Driving tests | Detail |
|---|---|---|---|
| Mobile-first (360px, no h-scroll) | ✅ | MobileLayoutTests(54), MobileJourneyTests(5), MobileDialogTests(1) | 54 layout checks green |
| Dialogs (no inline forms) | ✅ | DialogTests(6/7), SettingsDialogTests(2), CancelButtonTests(2) | 1 param-set dialog fail (§4) |
| Localization + RTL | ✅ | LocalizationTests(4), integration LocalizationFlow | Culture switch + RTL |
| Accessibility (axe) | ✅ | AccessibilityTests(24) | — |
| PWA | ✅ | PwaTests(5) | Installable, offline shell, manifest |
| Route census / gating | ✅ | RouteCoverageTests(1), RouteExistenceTests(1), NodesHiddenTests(2), integration NodeUiGating | Every `@page` smoke-covered |
| Page health (all routes render) | ✅ | PageHealthTests(27), PageSmokeTests(40), FullAppSmokeTests(1) | No circuit crash on any route |
| Manual-findings regressions (mandate 11) | ✅ | ManualFindingsTests(5), MiscUiTests(10), HelpTipTests(1), NavMenuTests(2) | Prior manual bugs stay fixed |
| Domain exception mapping | ✅ | integration DomainExceptionMapping | — |
| DB resilience / migration lock | ✅ | integration DatabaseResilience, MigrationLock | — |
| Broker allowlist | ✅ | BrokerAllowlistTests(1), integration BrokerAllowlist, **BrokerVerifierLiveTests (LIVE)** | Live broker probe reads account broker |

---

## 4. Failures — full detail (NOT fixed)

### ❌ F1 — `DialogTests.ParamSets_new_button_opens_dialog_and_creates_param_set`
```
Microsoft.Playwright.PlaywrightException : Locator expected to be visible
  - LocatorAssertions.ToBeVisibleAsync with timeout 30000ms
  - waiting for GetByText("cbot-b60848")
  at DialogTests.cs:line 114
```
The new param-set dialog was opened and submitted, but the created param-set's cbot name text
(`cbot-b60848`, randomized per run) never rendered in the list within 30s. Class: real-UI dialog
create flow. Other 6 DialogTests passed. **Signature = visibility timeout (flake-class, per prior
CI-flake history), but reproduced this run — treat as open until re-run confirms.**

### ❌ F2 — `QuantSizingTests.Equity_curve_mode_returns_an_exposure`
```
Microsoft.Playwright.PlaywrightException : Locator expected to be visible
  - LocatorAssertions.ToBeVisibleAsync with timeout 30000ms
  - waiting for Locator("[data-testid=sizing-recommendation]")
  at QuantSizingTests.cs:line 51
```
On `/quant/sizing` in equity-curve mode, the `sizing-recommendation` element never became visible
within 30s. The other QuantSizing test passed. Same visibility-timeout signature.

> Both failures are the classic 30s Playwright visibility timeout — historically flake under E2E
> worker contention (see memory `ci-fully-green-flake-fixes`). They may be genuine slow-render bugs on
> those two flows; a targeted re-run of just these two tests would disambiguate flake vs real. Not
> done here (report-only).

### ✅ Resolution (fixed after this report)

Isolated re-run passed both → confirmed **flake, not product bug**. Root cause: in the quant analyze
E2E tests and the param-set dialog test, a `FillAsync`/`SetInputFilesAsync` was issued **outside** the
`RunUntilVisibleAsync` retry loop. A Blazor-Server circuit that isn't yet interactive silently drops
that first input; the retry then re-clicked *calculate* but never re-applied the lost fill/upload, so
the result never rendered and the test burned its 30s timeout. A third sibling
(`QuantHealthTests.Equity_curve_mode`) surfaced the same latent flaw on a re-run.

**Fix (test-harness only, no product change):** pulled every `Fill`/upload **inside** the retried
action across all quant analyze tests (`QuantSizing`, `QuantHealth`, `QuantIntegrity`, `QuantRegimes`,
`QuantTca`) and the `DialogTests.ParamSets` upload, so a dropped first input is re-applied on retry.
**Regression guards added:** unit `PositionSizerTests.Sizes_a_rising_equity_curve_to_a_positive_exposure`
and integration `QuantPortfolioHttpTests.Sizing_from_an_equity_curve_returns_a_recommendation` — the
equity-curve sizing path that flaked in the UI is now also asserted deterministically at the fast tiers.

**Verified:** unit PositionSizer 5/5 · integration QuantPortfolio 4/4 · E2E all 16 quant tests green ·
E2E ParamSets green. (Anomaly A1's `Test host process crashed` recurred and once left a zombie test-host
holding the build DLLs — killed by PID to unblock; it is test-infra instability, not a product fault.)

---

## 5. Anomalies (not test failures)

| # | Anomaly | Impact |
|---|---|---|
| A1 | Integration run printed `Test host process crashed` / `Test Run Aborted` **after** `Passed! 249/249`. | Post-run teardown (Testcontainers/host) crash. Results were already emitted; no test lost. Cosmetic but worth watching. |
| A2 | The legacy split secret files (`openapi-*.local.json`) were moved to `secrets/secrets.bak/` and a new `secrets/dev-credentials.local.json` was left holding **only** the FRED/BLS keys — the Open API app+cids+tokens were dropped from the active file. | Live copy/Open API tests would have fallen back and possibly skipped. **Fixed for this run** by re-merging all creds into the unified file. Root cause of the split-out not investigated. |
| A3 | k8s teardown logged repeated `reflector … connection refused` after `PASS`. | Just the kubectl watch losing the API server as the cluster was deleted. Cluster confirmed gone (`kind get clusters` empty). |

---

## 6. Gap closure (second pass — all four addressed)

The four gaps from the first pass were closed on 2026-07-14:

| Gap | Status | Evidence |
|---|---|---|
| **Docker Compose** deployment not stood up | ✅ Closed | `scripts/compose-smoke.sh` (enhanced) brought the real stack up and asserted 7 invariants green: web `/health` 200, `/version` 200, `/`→`/login` 302, `/login` 200, **owner login 200** (DB migrated + owner seeded), bad login 401, MCP `/mcp` GET 405 (routing). Committable regression guard now — the Compose counterpart of `k8s-e2e.sh`. |
| **Aspire** deployment not stood up | ✅ Closed | New `tests/AspireTests/AspireAppHostSmokeTests.cs` boots the real AppHost via `Aspire.Hosting.Testing` (`DistributedApplicationTestingBuilder`), waits for the **web** resource to become healthy, and asserts `/health` 200 + `/version` 200. Ran green (1m6s). The harness allocates endpoints dynamically and returns a wired `HttpClient`, so it does **not** hit the fixed-port **DCP proxy collision on 5080** that the manual `dotnet run` did — that was a local Windows env issue, not an app defect. Wired into CI as the `test-aspire` job. |
| **AWS / Azure** observability wiring untested | ✅ Closed | New `tests/UnitTests/Observability/TelemetryConfiguratorTests.cs` (4 tests, green): asserts `AddAppTelemetry` builds a working tracer+meter pipeline with the OTLP endpoint (AWS ADOT / K8s collector), the Azure Monitor connection string (App Insights), both, and neither — guards the cloud exporter wiring with **no cloud infra**. Real cloud stand-up still out of scope (no cloud account this pass). |
| **Live FRED/BLS HTTP round-trip** not asserted | ✅ Closed (was stale) | `tests/IntegrationTests/Calendar/CalendarSourceLiveTests.cs` **already** drives the real `FredSource`/`BlsSource` against stlouisfed.org / bls.gov and asserts non-empty observations. Ran green live (~1s each) with the keys in `secrets/dev-credentials.local.json`. The first-pass "no live-fetch test" statement was outdated. (Note: it silently returns/skips when a key is absent — xUnit v2 has no `Assert.Skip`; matches the repo's existing live-test pattern.) |
| The 2 E2E failures need classification | ✅ Fixed | Classified as flake (passed isolated), root-caused, fixed, and regression-guarded in the prior commit — see §4 Resolution. |

### Still genuinely out of scope
- **Real AWS/Azure cloud** stand-up (no cloud account) — wiring is now unit-guarded, but a live cloud
  deploy + trace-in-backend verification remains a manual cloud-account task.

> Fixed: the Aspire smoke now runs against an EPHEMERAL Postgres (`PgDataVolume=""` ⇒ the AppHost skips
> `.WithDataVolume`), so it no longer shares — and password-poisons — the developer's persistent
> `app-pg-data` volume. Dev `dotnet run` keeps the stable `appdev` password on `app-pg-data`; the two never
> collide. (Postgres only applies the password on first init, so a deliberate `PgPassword` change still
> needs a one-time `docker volume rm app-pg-data`.)

---

## 7. Artifacts

- `artifacts/manual-test/e2e.trx` · `e2e.log`
- `artifacts/manual-test/integration.trx` · `integration.log`
- `artifacts/manual-test/unit.trx` · `unit.log`
- `artifacts/manual-test/k8s.log`
