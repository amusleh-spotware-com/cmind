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

## Time

Tests **never** read the real clock — hardcode timestamps
(`new DateTimeOffset(2026, 07, 10, 12, 00, 00, TimeSpan.Zero)`) or drive `FakeTimeProvider`. A test
whose result depends on when it runs is a bug. When you touch time-dependent code, add a
`FakeTimeProvider` boundary test (e.g. lease reclaim exactly at expiry `<= now`).

Modern C# 14 per root `CLAUDE.md`.
