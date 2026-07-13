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
