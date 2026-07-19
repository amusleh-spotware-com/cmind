# tests — the money-safety net

This app moves real money; an untested path can cost users capital. **Every change is covered at all
three tiers or it does not merge** — no "small change", config-only, refactor-only, or UI-only
exemption. If a tier genuinely can't apply, say so explicitly in the summary with the reason.

## The three tiers (same commit as the change)

- **UnitTests** (xUnit + FluentAssertions + NSubstitute) — assert **invariants and state transitions**
  on aggregates, not getters/setters; and any options binding. Mirror the source path under
  `UnitTests/`.
- **IntegrationTests** (Testcontainers PostgreSQL) — real DB for persistence/endpoint behavior. For a
  pure infra/background component, drive the real component deterministically (stub the external edge,
  not the component under test).
- **E2ETests** (Playwright) — drive the real UI (mobile + desktop) through `AppFixture`:
  create/edit/save round-trip + happy path + renders without the Blazor error UI. API-only/background
  feature → authenticated API-level / deterministic E2E of the observable outcome. New route → add to
  `PageSmokeTests`.
- **StressTests** — deterministic-simulation (DST) copy-trading stress suite; keep it green.

## AI features → E2E through the fake local LLM (MANDATORY)

Every AI feature is proven end-to-end through the real UI (and MCP) against a configured provider — no
"needs an API key" excuse. Use `AiLocalFixture` (collection `ai-local`): it boots the app with one
active provider — the in-process **`FakeLocalLlmServer`** (OpenAI-compatible, deterministic canned
reply, wire-identical to Ollama/LM Studio/vLLM) by default, or a **real provider** when the dev sets
`AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — real creds win.
Add/modify an AI endpoint, page, or MCP tool ⇒ add a Playwright test that drives it through the UI and
asserts the AI output renders (`fixture.UsingFakeLlm` ⇒ assert it contains `AiLocalFixture.CannedReply`;
real provider ⇒ assert non-empty). Patterns: `AiFeatureLocalTests` (UI), `McpAiToolsLocalLlmTests`
(MCP), `LocalLlmProviderTests` (adapter). Keep the keyless "not configured" gate E2E (`AiPagesTests`).

## Failure paths are not optional

A feature that can fail tests its failure branch too: connection drop, order-placement failure,
desync/resync, token rotation/invalidation, node death + lease reclaim, DB blip. Not just the happy
path.

## `FakeTradingSession` — cTrader-faithful, always

`UnitTests/CopyTrading/FakeTradingSession.cs` must mimic real cTrader (order types, expiry, slippage,
partial close, SL/TP amend, trailing, disconnect/reconnect desync, token swap, rejections). Add a
behavior → extend the simulator. **Never weaken it or a test to make CI pass** — fix the code.

## Broker-mediated behaviour is SIMULATOR-tested, NOT E2E (learn from the take-profit bug)

E2E **cannot** exercise live-broker trade mechanics — CI has no cTrader account placing orders, so a
Playwright test can never make a *source* set a stop-loss, add a take-profit, partially close, scale in,
trail, or place/amend/cancel a pending. **These behaviours are covered at the `FakeTradingSession` (unit)
tier; E2E does NOT substitute for them.** "Everything is E2E-tested" is *false confidence* for money
logic. A real shipped bug — a source **take-profit** set on an open position was never copied, because
`MirrorStopChangeAsync` passed a hardcoded `null` TP and the host tracked only the SL — passed **every**
E2E (no E2E can drive that path). It was a **unit** hole, and only a `FakeTradingSession` assertion catches
its class.

So: **every source `ExecutionEvent` branch that changes a destination MUST have a `FakeTradingSession`
assertion on the destination effect** — the full matrix, each cell a `[Fact]`:

| Source action | On open | Post-open (position update) |
|---|---|---|
| open / close | ✓ mirror order / close | — |
| stop-loss set · move · **clear** | ✓ | ✓ |
| take-profit set · move · **clear** | ✓ | ✓ |
| **stop + take-profit together** | ✓ | ✓ |
| trailing on · off | ✓ | ✓ |
| partial close / scale-in | — | ✓ |
| pending place · amend · cancel | ✓ | ✓ |

…each **× the config that transforms it**: `Reverse` (the source SL↔TP swap must hold on open **and**
post-open — the bug's blind spot), `CopyStopLoss`/`CopyTakeProfit`/`CopyTrailingStop` on/off,
`MirrorPartialClose`/`MirrorScaleIn`. The matrix lives in `CopyEngineHostTests` ("Stop-loss / take-profit
mirroring matrix"). Adding a new source action, or a new per-destination transform, ⇒ add its row/cells in
the **same commit**. A copy-engine behaviour with no `FakeTradingSession` assertion is unshipped — no
matter how many E2E tests exist. The same rule applies to any future broker-mediated surface (prop-firm
tracking, agent order execution): assert the observable effect through its simulator/fake, not through a
UI that can't reach it.

**The raw-protocol WIRE seam needs its own tests — `FakeTradingSession` bypasses it.** The fake yields
`ExecutionEvent`s directly, so it never exercises `OpenApiTradingSession.SourceExecutionsAsync`, the
`ProtoOAExecutionEvent`→`ExecutionEvent` classification (is this a pending order? a fill? a close? a
partial? which `OrderKind`?). Two live bugs hid exactly here (take-profit; the pending-order suspicion).
Any change to how a wire event is classified — or a new execution type — gets a test that pushes a **real
`ProtoOAExecutionEvent`** through the session and asserts the resulting `ExecutionEvent`
(`OpenApiTradingSessionWireTests` + `FakeOpenApiTransport.Push(...)` for the unsolicited server event).
When a "copy X doesn't work" report survives green engine tests, write the wire test to prove/disprove the
classification before re-reading the engine — the answer is usually there or in the stored per-destination
config, not in the (already-covered) engine.

## Coverage — target 100%, never regress

Coverage is **100% line/branch** across unit + integration + E2E, and it **only ratchets up**. A change
that drops a project's measured coverage does not merge. Every branch — including the failure branch —
is exercised. "Hard to test" means the design is wrong: extract the seam, inject the edge
(`TimeProvider`, `IAiClient`, `FakeTradingSession`), and cover it. Genuinely unreachable defensive code
is the only exception and must carry an inline justification.

Census gates make omissions fail the build, not depend on a reviewer:
- **Every Blazor page** is in `PageSmokeTests.Routes()` — `RouteCoverageTests` fails the build on a page
  with no smoke coverage (add the route, or exclude it with a reason in `RouteCoverageTests`).
- **Every interactive UI control** (button, dialog, toggle, form) is driven by an E2E that asserts the
  observable outcome — not just that the page rendered.
- **Every selection/list control** (dropdown, autocomplete, radio group, picker) is E2E-driven to assert
  it **presents all the options it should** (assert a count / a specific expected option is listed) **and**
  that choosing a **non-default** one takes effect — never assert only that the control is *present*.
  Back it with a unit gate on the option **source** when it is generated (e.g. `SupportedTimeZonesTests`
  asserts the zone list is the full DB, not one entry). This exists because the time-zone switcher shipped
  listing only the current zone: the E2E checked presence, not that it offered every zone.
- **Every minimal-API route** has an integration or E2E test hitting it authenticated.
- **Mandate guards** (`ArchitectureGuardTests`, `NoHardcodedUiTextTests`, `ResourceParityTests`,
  `WhiteLabelCatalogParityTests`) stay green — they are the machine-enforced CLAUDE.md.

## Full-app smoke walk — keep it in sync (MANDATORY)

`FullAppSmokeTests` is the single "real user" pass: signed in as the owner it visits **every** page,
opens and Cancels **every** dialog-launching control, and asserts the circuit never breaks — creating,
editing, deleting nothing. It follows `PageSmokeTests.Routes()` (so routes stay in sync automatically)
and dismisses dialogs by generic heuristics (`DialogOpeners` / `Destructive` / `CloseLabels`). **Any UI
change must keep this walk green and current:** a new page is picked up via `Routes()`; a new
dialog-opening button whose label isn't matched by `DialogOpeners`, a new close/cancel affordance whose
text isn't in `CloseLabels`, or a new destructive verb missing from `Destructive` → update those lists in
`FullAppSmokeTests` in the **same commit**. Never let this smoke walk drift behind the UI.

## E2E isolation & locator hygiene — session-hardened (read before writing a Playwright test)

The `AppFixture` collection **shares one app + one owner user + one Postgres** across every test in the
collection. State accumulates: accounts, profiles, cBots seeded by one test are visible to the next, and
xUnit does not guarantee order. Two traps that each shipped a red CI here:

- **Never "select-all + assert exactly N".** A test that clicks a *select-all* control (or asserts "there
  are exactly N rows") breaks the moment another test seeds more of that entity into the shared user
  (`Sequence contains more than one element` on `.Single()`). **Select the specific rows this test created,
  by their own seeded identifier / `Suffix` / name** — never rely on the shared set being only yours.
- **Scope locators and pass `Exact` for short/generic names.** `GetByRole(Button, Name="Close")` and
  `GetByLabel("Copy redirect URI")` each matched **two** elements — a `HelpTip` whose `aria-label`
  *contains* the word, and the same adornment rendered on the page **and** an open dialog. A bare
  accessible-name match is a substring match across the whole page. Fix: scope to the container first
  (`page.Locator(".mud-dialog").GetByRole(...)`) **and** pass `new() { Name = "Close", Exact = true }`.
  `:has-text('N')` is likewise a substring — fine for a distinct number, wrong for a short/near-duplicate.
- **A `MudSelect`'s `data-testid` is on its HIDDEN `<input>` — never put it in a "resolve to a visible state"
  locator.** A `WaitForAsync(Visible)` over `"[data-testid=cot-net-chart], [data-testid=cot-nodata], [data-testid=cot-market]"`
  timed out because `.First` matched the hidden `cot-market` select input (first in DOM order) and waited forever
  for it to become visible. For "the page resolved to a known state" assert on the **visible** panels/alerts only
  (a `MudPaper`/`MudAlert` testid); to drive the select, click `.mud-select:has([data-testid=…]) .mud-input-control`.
- **Any read path that lazily fetches from an external source (read-through cache, on-demand load) MUST get a
  FAKE source in integration/`WebApplicationFactory` tests — else the read hits the live network and is flaky.**
  The COT read-through decorator fetches from CFTC on a cache miss; the endpoint tests
  `ConfigureTestServices(s => { s.RemoveAll<ICotSource>(); s.AddSingleton<ICotSource>(new FakeCotSource()); })`
  and pre-seed the DB, so reads are served from Postgres, deterministically, offline. Assert the load-through
  behaviour (first call fetches + persists, second served from DB, empty market throttled to one fetch) against
  the fake with a `CallCount` — never against the real endpoint.

- **Wait for interactivity, not just `window.Blazor` — use `page.WaitForAppReadyAsync()`.** The app is
  Blazor **Server** (InteractiveServer): a freshly-loaded page shows SSR HTML *before* the circuit wires
  its `@onclick` handlers. `WaitForFunctionAsync("() => window.Blazor !== undefined")` only proves the
  framework script loaded, so a click dispatched in that gap is **silently dropped** and the dialog/effect
  never appears → a 15 s visibility timeout on whichever page hit the window (this is why a *different*
  test failed each CI run). Always await `WaitForAppReadyAsync()` (in `PageExtensions.cs`) after a
  `GotoAsync` before interacting — it waits for `window.__appInteractive` (set from
  `Routes.OnAfterRenderAsync`, a truthful "circuit is interactive" signal) and degrades to the old probe
  on timeout. Never reintroduce the bare `window.Blazor` probe.

Saturation ≠ failure: when a whole slice fast-fails in ~5–40 ms on `AppFixture.InitializeAsync` (login-nav
timeout), the machine is saturated from back-to-back runs — cool down and run a smaller `--filter`, don't
"fix" the test. A *real* failure takes seconds and reproduces in isolation. **Never run E2E test processes
concurrently** (overlapping foreground + `run_in_background` `dotnet test`, or a rebuild while one runs):
they collide on the `Web.dll` build lock (MSB3027) and leak orphaned app hosts that keep locking output and
starve the machine, producing *false* 90 s `InitializeAsync` timeouts that look like a code regression but
aren't. Run **one** E2E slice at a time; if boot times out, check for stray `dotnet …Web.dll` / `testhost`
processes and kill them before blaming your change.

## Kubernetes — the app must work in-cluster

The app is deployed to K8s (`deploy/helm/cmind`); "works locally" is not "works". Every feature is
proven **in-cluster** by the `tests-job` and verified locally on **Kind** with
`scripts/k8s-e2e.sh` (kind create → build/load images → `helm install` → run the test Job → assert
exit 0). Default filter is the deterministic copy suite (no secrets); the live copy suite runs when
`./secrets` is present (see below). Touch deployment, config surface, a new service/agent, or a
user-facing feature → run `scripts/k8s-e2e.sh` and keep it green.

## Live copy trading — real broker, every operation

Copy trading moves real money, so it is proven against a **real cTrader Open API** account, not only the
fake. `tests/E2ETests/CopyLive` reads credentials from `secrets/dev-credentials.local.json` (or the
legacy split files / env) and **skips cleanly when absent** — never delete or stub the live tier to make
CI pass. Every trading operation (market/pending/expiry, partial close, SL/TP amend, trailing,
partial-fill true-up, multi-follower fan-out) **and** its adverse path (reject, socket drop mid-order,
token invalidation, node death + lease reclaim, desync/resync) has a live scenario asserting the
invariant: **follower re-converges to broker truth — zero lost intent, zero duplicated side effect.**
The nightly live-copy Job is required-green before a release tag.

## Time

Tests **never** read the real clock — hardcode timestamps
(`new DateTimeOffset(2026, 07, 10, 12, 00, 00, TimeSpan.Zero)`) or drive `FakeTimeProvider`. A test
whose result depends on when it runs is a bug. When you touch time-dependent code, add a
`FakeTimeProvider` boundary test (e.g. lease reclaim exactly at expiry `<= now`).

Modern C# 14 per root `CLAUDE.md`.
