# CLAUDE.md

Rules for agents working in this repo. Read once; obey. Layer-specific rules live in nested
`CLAUDE.md` files (`src/Core`, `src/Infrastructure`, `src/Web`, `tests`) — they auto-load when you
touch that tree. Full DDD playbook is the **`ddd-dotnet`** skill. Human/PR process:
[CONTRIBUTING.md](CONTRIBUTING.md). Tool-agnostic agent entry: [AGENTS.md](AGENTS.md).

> **This app moves real money.** An untested bug costs users capital; a stale doc misleads the next
> agent. The mandates below are law, not style — no "small change" or "quicker this way" exemption.
> If a rule seems wrong, **stop and ask the user** — don't break it silently.

## Commands

```bash
dotnet restore
dotnet build                       # TreatWarningsAsErrors=true — must be 0 warnings, 0 errors
dotnet test                        # unit + integration (+ E2E) — must be green before "done"
dotnet run --project src/AppHost   # full stack via Aspire (Postgres, Web, MCP, pgAdmin)
dotnet run --project src/Web       # Web only
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
dotnet ef database update         -p src/Infrastructure -s src/Infrastructure
# Analyzer sweep (surfaces info-level CA/IDE rules dotnet build hides) — run on projects you touched:
dotnet format analyzers <proj>.csproj --verify-no-changes --severity info
```

### Docs site (`website/` — Docusaurus, the CANONICAL docs)

The public docs + landing page live in `website/` (a Docusaurus app) and publish to GitHub Pages at
**https://amusleh-spotware-com.github.io/cmind**. The Markdown under `website/docs/` is now the
**only** copy of the documentation — the old top-level `docs/` folder has been removed; brand assets
and screenshots now live in the root `design/` folder. Node 20+ (22 recommended); no .NET needed.

```bash
cd website
npm install                 # first time only
npm start                   # hot-reload dev server → http://localhost:3000/cmind/
npm run build               # production build to website/build (also reports broken links — run before PR)
npm run serve               # preview the production build → http://localhost:3000/cmind/
```

- Landing page: `website/src/pages/index.tsx`; brand tokens: `website/src/css/custom.css`;
  sidebar: `website/sidebars.ts`; config/SEO: `website/docusaurus.config.ts`.
- New page → add the file under `website/docs/` **and** its id to `website/sidebars.ts`.
- Screenshots are captured by the E2E test: `CAPTURE_SCREENSHOTS=1 dotnet test tests/E2ETests
  --filter ReadmeScreenshotsTests` → `design/screenshots/`, then copy into
  `website/static/img/screenshots/`.
- Deploy is automatic on push to `main` touching `website/**` via `.github/workflows/docs.yml`.
  Requires *Settings → Pages → Source: GitHub Actions*. Pages on a **private** repo needs a paid
  plan; on the free plan the repo must be **public**.

## What this repo is

Multi-tenant Blazor Server + Minimal API platform for cTrader. Builds/runs/backtests cBots (C# +
Python, `dotnet build` in a sandboxed container) via `ghcr.io/spotware/ctrader-console`, scheduled
across remote nodes (`CtraderCliNode` HTTP agents) and/or the local web host. Also mirrors trades
across accounts (copy trading) over the cTrader Open API. Stack: **.NET 10 / C# 14**, EF Core +
PostgreSQL, .NET Aspire, MCP server, AI via the Anthropic API. Architecture: **strict DDD**.

## Module map

```
src/Core            — pure domain: entities, aggregates, value objects, strong IDs, domain events,
                      Core-side interfaces. ZERO infra deps (no EF/HttpClient/Docker/ASP.NET).
src/Infrastructure  — EF Core, DataProtection, encryption, GHCR, Anthropic client, observability.
src/Nodes           — cross-node orchestration: scheduling, dispatch, pollers, background services.
src/CtraderCliNode  — standalone HTTP node agent (remote hosts; JWT-auth). Runs/backtests cBots via the
                      cTrader CLI in a docker container (optimize too, once the cTrader CLI supports it).
src/Web             — Blazor Server SSR + Minimal API + SignalR + MudBlazor UI.
src/Mcp             — MCP HTTP+SSE server (tools for AI clients).
src/AppHost         — .NET Aspire orchestrator.
tests/UnitTests · IntegrationTests (Testcontainers PG) · E2ETests (Playwright) · StressTests (DST).
website/docs/       — CANONICAL docs (Docusaurus site): features/, deployment/, operations/,
                      testing/, ui-guidelines.md. Published to https://amusleh-spotware-com.github.io/cmind.
design/             — brand assets: logo/banner SVGs, brand brief, app screenshots (root folder).
```

Detailed per-file annotations are inferable from the code — read the tree, don't ask for a manual.

## Hard mandates

Each is binding. Nested `CLAUDE.md` files and the `ddd-dotnet` skill carry the long form.

1. **Strict DDD.** Domain logic lives on an aggregate / value object / domain service — **never** in
   an endpoint, MCP tool, Razor component, or `BackgroundService` (those orchestrate, they don't
   decide). No new public setters — state changes through intention-revealing methods that guard
   invariants. Reference other aggregates by **strong ID**, not navigation property. One
   `SaveChanges` mutates **one** aggregate; cross-aggregate flows use domain events. Wrap primitives
   crossing a domain boundary in a value object. `src/Core` compiles with **zero** infra deps.
   Invariant violations throw a Core `DomainException`, not a framework exception. → `ddd-dotnet`
   skill + `src/Core/CLAUDE.md`.
2. **Three test tiers, every change.** Unit **and** integration **and** E2E — whichever the change
   can be exercised at, it ships, in the same commit. Unit asserts invariants/transitions (not
   getters); integration hits real Postgres (Testcontainers); E2E drives the real UI (Playwright,
   mobile + desktop) or an authenticated API call. **Failure paths count** — connection drop, order
   rejection, desync/resync, token rotation, node death + lease reclaim. New route → add to
   `PageSmokeTests`. → `tests/CLAUDE.md`.
   **Missing credentials/API keys is NEVER a reason to skip a test tier, and never a reason to ask.**
   Always write every applicable tier — unit, integration, **and E2E** — even when the AI key, Open API
   creds, or a live cluster are absent. Test the parts that don't need the secret directly (UI renders,
   the disabled/degraded path, gated 404s, the deterministic core via a fake `IAiClient`/
   `FakeTradingSession`/seeded state); for the part that genuinely needs the secret, add a live test
   that **skips cleanly when the secret is absent** (see the `CopyLive`/onboarding pattern) — do not
   defer it, do not leave a tier unwritten, do not ask the user whether to add it. Just add it.
   **AI features are E2E-tested through the fake local LLM — MANDATORY, no exception.** Every AI
   feature (existing or new) is driven end-to-end through the **real UI** (and MCP surface) against a
   configured provider: by default the in-process **`FakeLocalLlmServer`** (an OpenAI-compatible
   endpoint returning a deterministic canned reply — zero external deps, wire-identical to Ollama/LM
   Studio/vLLM), or a **real provider when `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY`/`AI_E2E_KIND`/
   `AI_E2E_MODEL`) is set** — real creds win, otherwise the fake. Adding or changing ANY AI feature
   (endpoint, page, MCP tool) REQUIRES a Playwright E2E test that boots the AI-configured fixture
   (`AiLocalFixture`, collection `ai-local`), exercises the feature through the UI, and asserts the AI
   output renders (the canned reply when on the fake). MCP AI tools get the same via the fake LLM
   (`McpAiToolsLocalLlmTests`). The keyless "not configured" gate E2E stays too. → `tests/CLAUDE.md`.
3. **`FakeTradingSession` stays cTrader-faithful.** Extend it for new cTrader behavior; never weaken
   the simulator or a test to make CI pass. Fix the code. (`tests/UnitTests/CopyTrading/`)
4. **Never `DateTime.UtcNow`/`.Now`/`DateTimeOffset.UtcNow`.** Inject `TimeProvider`, read
   `GetUtcNow()`; domain methods take a `DateTimeOffset now` param from the caller. Tests use
   `FakeTimeProvider` or hardcoded timestamps — never the real clock. Touch time-dependent code →
   migrate that call site and add a boundary test.
5. **Zero warnings.** `TreatWarningsAsErrors=true`, no `NoWarn`. Fix analyzer + `.razor` inspection
   findings too — a green build is not enough; run the analyzer sweep on touched projects.
6. **No secrets, no magic strings, no raw logging.** Encrypt via `ISecretProtector`
   (`EncryptionPurposes`); strings live in `Core/Constants/`; config via
   `IOptionsMonitor<AppOptions>` (never `cfg["Key"]`); log via source-generated `LogMessages` (never
   `ILogger.LogInformation(...)`). Never commit/log/store a secret in plaintext.
7. **UI = dialogs, mobile-first, branded.** Every add/create/edit action opens a MudBlazor dialog —
   never an inline page form. Author for a 360px phone; no horizontal scroll 320–1920px; design
   tokens only. → `src/Web/CLAUDE.md` + [website/website/docs/ui-guidelines.md](website/website/docs/ui-guidelines.md).
8. **Docs in the same commit.** Every feature has `website/website/docs/features/*.md` (canonical — the
   published Docusaurus site; the top-level `docs/` copies are redirect stubs); behavior change →
   update its doc (and `website/docs/deployment`/`operations` when relevant). Not "done" until the
   doc matches the code. **Docs are localized too:** adding or changing any doc means updating every
   locale under `website/i18n/` (all languages in `Core.Constants.SupportedCultures`) in the same
   change — never leave a locale stale.
9. **Everything user-facing is localized (no exceptions, enforced).** No literal user-facing string in a
   `.razor`, endpoint, email, or notification — inject `IStringLocalizer<Ui>` and use `@L["key"]`; add
   the key to `tools/i18n/ui-translations.json` for **every** language in `Core.Constants.SupportedCultures`
   and regenerate (`pwsh tools/i18n/gen-resx.ps1`). Domain stays key-based (`DomainErrors`). Display
   formatting uses `CurrentCulture`; wire/parse stays `CultureInfo.InvariantCulture`. RTL must render
   (`<html dir>` + MudBlazor `MudRTLProvider`). The build **fails** on a hard-coded string
   (`NoHardcodedUiTextTests`) or a missing/blank translation (`ResourceParityTests`) — a new feature is
   born fully localized or it does not merge. → `src/Web/CLAUDE.md` + `website/docs/features/localization.md`.
10. **White-label options are owner-tunable and always in sync.** Every white-label option — a property on
    a white-label options record (`BrandingOptions`, `FeaturesOptions`, `RegistrationOptions` + nested,
    `AccountsOptions`, `EmailOptions`, or the white-label subset of `OpenApiOptions`/`AiOptions`/
    `PropFirmOptions`) or any new `App:*` deployment knob — MUST, in the **same commit**: (a) be registered
    in `Core/WhiteLabel/WhiteLabelCatalog`; (b) be surfaced in the owner **Settings → Deployment** section so
    the owner can change it at runtime exactly as a deployment does via config; and (c) be reachable through
    `IWhiteLabelSettings` (overlaid on `AppOptions` by the decorated `IOptionsMonitor`, or delegated to
    `IFeatureGate` for feature flags) so the override takes effect without a redeploy — **or** be explicitly
    listed in `WhiteLabelCatalog.IntentionallyExcluded` with a reason (operational-only/restart-only). The
    build **fails** (`WhiteLabelCatalogParityTests`) if a white-label options-record property is neither
    catalogued nor excluded. Never add a config-only white-label flag. → `website/docs/features/white-label-owner-settings.md`.

## Modern C# — MANDATORY (target C# 14 / .NET 10, `LangVersion=latest`)

Write current-idiom C#. Prefer the newest form the compiler accepts; do not emit legacy syntax an
analyzer would flag. On any file you touch, modernize the lines you edit.

- **Collection expressions** `[]` / `[.. spread]` — not `new List<T>()`, `new T[] {…}`, `Array.Empty`,
  `Enumerable.Empty`. `params` takes a collection expression.
- **Primary constructors** for services/DI (deps first, factory interfaces last); `private readonly`
  fields only where the ctor can't cover it.
- **`field` keyword** for a property with backing logic instead of a hand-written backing field.
- **Target-typed `new`** when the type is on the left; **`var`** when the type is obvious on the right.
- **Pattern matching / switch expressions**; `is null` / `is not null` — never `== null` / `!= null`.
- **File-scoped namespaces**; `sealed` by default; `required`/`init` members over mutable setters.
- **Raw string literals** (`"""…"""`) for JSON/SQL/multiline; interpolation over concatenation;
  `nameof` over string literals.
- **Async:** `await using`, `await foreach`, `ValueTask`/`CancellationToken` where the analyzer asks;
  `CancellationTokenSource.CancelAsync()` not `.Cancel()` in async.
- **Perf idioms the sweep flags:** CA1822 static, CA1859/CA1826/CA1849, `EndsWith(char)`, no redundant
  `.ToList()`/`.ToArray()`, no dead code.

Genuine false positives may stay only with an inline justification. Deliberately-off repo-wide
conventions (CA2007 ConfigureAwait, CA1031, arg-null-guards in app tiers) are the project's choice —
match surrounding code, don't fight the whole codebase.

## Style

- File-scoped namespaces. `sealed` by default. Injected fields `private readonly`. Explicit access
  modifiers always. Early returns over nested `if`.
- Primary constructors; params ordered service deps → factory interfaces. Body order: field assigns →
  property init → event subscriptions last. Member order: fields → properties → events → methods,
  `Dispose()` last.
- Naming: `_camelCase` private fields, `I`-prefixed interfaces, `Async`-suffixed async methods. Spell
  identifiers in full — no `Tp`/`Sl`/`Tcs`/`Vm` (established `UI`/`DI`/`ASP`/`DOM` fine). Wire-format
  string literals keep literal form.
- No comments except `TODO`/`FIXME`.
- **Ubiquitous language** — use the domain's names (`CBot`, `SourceProject`, `ParamSet`, `Instance`,
  `Node`, `AgentMandate`, `TradingAccount`, `Ctid`). No invented synonyms (`InstanceRecord`,
  `NodeManager`). Backtest ≠ "job"; node ≠ "server"; param set ≠ "config".

## Definition of done

- [ ] `dotnet build` clean (0 warnings) · analyzer sweep clean on touched projects · Rider
      `get_file_problems` clean on every `.cs`/`.razor` you touched.
- [ ] `dotnet test` green (incl. pre-existing); new behavior covered unit + integration + E2E, failure
      paths included; bug fix has a regression test; new route added to `PageSmokeTests`.
- [ ] DDD checklist passes; `src/Core` has no infra deps; touched anemic code left more encapsulated.
- [ ] No `DateTime.UtcNow`/`.Now`; no secrets; no magic strings; no direct `ILogger.Log*`; modern C#.
- [ ] No hard-coded user-facing text — every string via `@L["key"]`, present in all locales (hardcoded +
      parity gates green); new RTL renders correctly.
- [ ] Docs updated in the same commit (and every `website/i18n/` locale); EF migration added if schema changed.
- [ ] **Full coverage, no regression.** New behavior is covered at **all three tiers** — unit **and**
      integration **and** E2E-with-UI. The target is **100%** line/branch and coverage **never drops**:
      a change that lowers a project's measured coverage does not merge. Every new Blazor page is in
      `PageSmokeTests.Routes()` (enforced by `RouteCoverageTests`); every new interactive UI control is
      driven by an E2E; every new minimal-API route has an integration/E2E test. Failure paths count.
- [ ] **Runs on Kubernetes.** Any change touching deployment, config surface, a new service/agent, or a
      new user-facing feature keeps the app green under the in-cluster suite — verified locally on Kind
      via `scripts/k8s-e2e.sh` and covered by an in-cluster test in `deploy/helm/cmind` `tests-job`.
      "Works on my machine" is not done; "works on the cluster" is.

## Non-inferable design decisions

Architecture facts you can't read off the code — the rest lives in nested `CLAUDE.md` + `docs/`.

- `CBotBuilder` runs on the **web host** (needs Docker socket), inside a throwaway SDK container
  (bind-mount `/work`, shared `app-nuget-cache` volume) so untrusted MSBuild can't reach host FS/net.
  Run/backtest containers run on nodes picked by `NodeScheduler`, dispatched via
  `ContainerDispatcherFactory` → `Http` (remote agent) or `Local` (web host's own node).
- **cTrader CLI nodes get no SSH/shell.** Main→agent over HTTP; each request carries a short-lived HS256
  JWT (5-min, `iss=app-main`/`aud=app-node`) signed with the node's secret. Agent only runs images
  matching `AllowedImagePrefix`, execs docker via `ArgumentList` (no shell), stateless (finds
  containers by `app.instance` label). Auto-discovery: agents self-register + heartbeat to
  `POST /api/nodes/register`; upsert `CtraderCliNode` **by name** (stable across IP changes).
- **Instance state = TPH; a transition replaces the entity** (discriminator can't change) → instance
  **id changes** starting→running→terminal. Container id is stable and carried over; the HTTP agent is
  keyed by container id for status/report/stop/logs.
- cTrader Console backtest CLI (verified): requires `--data-mode` (default `m1`), dates
  `dd/MM/yyyy HH:mm`, `params.cbotset` is JSON passed positionally; `run` rejects `--data-dir`. Pollers
  reconcile self-exited containers (`--exit-on-stop`); exit 0/null = Stopped, non-zero = Failed. See
  `ContainerCommandHelpers`.
- **AI is fully gated on `AppOptions.Ai.ApiKey`** — unset → every feature returns `AiResult.Fail`, app
  runs unchanged (no key needed for build/test/E2E). `IAiClient` calls Anthropic over **raw HTTP**
  (typed `HttpClient`), deliberately not the SDK. `AiFeatureService` is the single orchestrator shared
  by Web endpoints, MCP `AiTools`, and `AiRiskGuard`.

## Deliberately not done

No optimization (unsupported by cTrader Console) · email/SMTP is **opt-in only** (`App:Email`) for
registration email-verification — unset ⇒ no-op sender and password reset stays manual via
`MustChangePassword` · no strong-typed EF ID converters yet · no per-user quotas.
