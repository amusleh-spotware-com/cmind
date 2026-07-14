# AI Features — Real-User End-to-End QA Suite

**Status:** IN PROGRESS — increment 1 shipped & verified.

**Increment 1 (shipped).** `tests/E2ETests/AiFeatureRealUserTests.cs` (collection `ai-local`, fake LLM) —
real-user, start-to-finish coverage for the body-only AI features that were previously weak:
- **F14 copy-recommend** — driven through the real `/copy-trading` UI (type risk profile → "AI suggest" →
  the AI recommendation renders the canned reply; no longer only the "not configured" gate). ✅
- **F13 curate** — endpoint drive, canned reply asserted. ✅
- **F12 vision** — asserts the correct real-user contract: the text-only fake provider degrades cleanly to
  the typed "does not support image input" capability message (mandate 11), never a crash. ✅
And `tests/E2ETests/OnnxRealUserTests.cs` (collection `ai-onnx`) — **real-model** proof through the shipped
built-in **ONNX** provider (Phi-3-mini, in-process, no external server), driving **debate** through the UI
against the actual model. Verified locally against the 2.6 GB Phi-3 model (1 min real inference, green).
Self-gates via `AI_ONNX_MODEL` (runs locally, skips the CI PR lane). CPU inference is token-bound, so the
lane covers the bounded-output features (review is already in `OnnxE2ETests`, + debate here); unbounded
ones (codegen, sentiment) stay on the deterministic fake lane.

**Increment 2 (shipped).** The deterministic seeding seam (§2) + data-dependent feature coverage:
- `src/Web/Endpoints/TestSeedEndpoints.cs` — a **dev-only, config-gated** (`App:TestSeed:Enabled` AND
  `IsDevelopment()`, fail-fast if the flag is set outside Development) `POST /api/testseed/ai-portfolio`
  that seeds a cBot + param set + a completed backtest (with a report) + a running instance for the
  current user, using the same domain factories/transitions the integration tests use — no Docker/broker.
- `tests/E2ETests/AiFeatureDataE2ETests.cs` (collection `ai-local`) — real-user, data-backed coverage,
  **6/6 green**: F6 digest (UI) and F7 exposure (UI) render the AI output over seeded instances; F10
  analyze-backtest, F8 tune/decay, F9 optimize-params, F11 post-mortem each produce AI output over the
  seeded report/cBot (canned reply asserted on the fake). Previously all six were "enabled + no-crash".

**Increment 3 (shipped).** F16 Agent-Studio research-desk **debate** (`AiFeatureRealUserTests`): create an
agent → `POST /api/agent-studio/{id}/debate` → the desk runs 4 analyst calls + a reviewer synthesis, and
every opinion + the synthesis carry the AI reply (canned reply asserted on the fake). ✅

**Increment 4 (shipped).** F19 MCP AnalyzeBacktest — `tests/IntegrationTests/McpAnalyzeBacktestLocalLlmTests.cs`:
seeds a completed backtest (with report) for a user in a real Testcontainers DB, points the MCP `AiTools`
at the fake local LLM with an authenticated caller, and asserts the tool returns the model reply. MCP AI
tools now covered 4/5 (generate/review/sentiment via `McpAiToolsLocalLlmTests` + analyze-backtest here). ✅

**Increment 5 (shipped).** F18 market-watch alerts driven through the REAL background worker
(`AlertEvaluator`). New `AiWorkersFixture` (collection `ai-workers`) enables the worker with a 2s poll;
`FakeLocalLlmServer` now returns a valid alert-assessment JSON (alert raised, canned reply embedded in the
message) for the alerting prompt. `AlertsWorkerE2ETests`: create a market-watch rule → the worker
evaluates it → an `AlertEvent` with the AI-authored message appears (canned reply asserted). ✅ The
worker-driven pattern (short poll + marker-keyed fake JSON + isolated collection) generalises to F15/F17.

**Remaining** (increments 6+): F17 prop-guard + F15 portfolio agent — same worker pattern, but each needs
more seeded state (running instances / an agent mandate + accounts) and a `FakeLocalLlmServer` marker
returning valid risk-action / agent-action JSON, plus an observable assertion surface (audit record /
decision record). F19 currency-strength MCP tool needs a seeded currency snapshot. Live-account variants
follow the onboarding pattern.

**Goal.** Today every AI feature has *an* E2E test, but for the data-dependent features the test only
asserts a weak contract: "AI is configured → the button is enabled → clicking it does not crash the
circuit." That proves wiring, not the feature. This plan extends the AI E2E suite so each feature is
driven **like a real user, start to finish**, with the **real deterministic canned AI output actually
rendered and asserted** — so a green run means the feature works 100%, not just that it is reachable.

**Provider.** Keep the existing rule (`AiLocalFixture`): the in-process **`FakeLocalLlmServer`**
(OpenAI-wire, deterministic canned reply) by default; a **real provider** when `AI_E2E_BASEURL`
(+ `AI_E2E_API_KEY`/`AI_E2E_KIND`/`AI_E2E_MODEL`) is set. For the "real model" proof we use the app's
own **built-in ONNX** provider (`Microsoft.ML.OnnxRuntimeGenAI`, `BuiltInOnnx`) — the existing
`OnnxAppFixture` (`AI_ONNX_MODEL`) — **not** an external Ollama server. This runs a real model in-process
with zero external services, and the model can auto-download. Real-model runs assert only that *some*
non-empty AI output renders (non-deterministic); the fake asserts the canned marker.

**Trading creds.** Features that need a live account (live exposure, portfolio agent acting on a real
account, copy-recommend from a real strategy) use the existing **`CopyLive`/`CMIND_ONBOARD` onboarding
pattern** — a live test that **skips cleanly when creds are absent**, never a hard dependency. The
deterministic core of each is still tested through seeded in-DB state + the fake LLM.

---

## 1. AI feature inventory (what must be covered)

Mapped from `src/Web/Endpoints/AiEndpoints.cs`, `src/Mcp/Tools/AiTools.cs`,
`src/Infrastructure/Ai/AiFeatureService.cs`, and the AI consumers in `src/Nodes` / `src/Core/Agent`.

| # | Feature | Route / surface | Service call | Needs real data? | Current E2E | Gap |
|---|---------|-----------------|--------------|------------------|-------------|-----|
| F1 | cBot codegen + build | `/ai/build` | `GenerateCBotAsync` → build pipeline | Docker (build) | `Build_page…runs` asserts result panel | Build needs Docker → heavy; assert generated **source** rendered before build |
| F2 | cBot review | `/ai/review` | `ReviewCBotAsync` | no | canned reply asserted ✓ | solid — keep |
| F3 | cBot debate | `/ai/debate` | debate | no | canned reply asserted ✓ | solid — keep |
| F4 | Market sentiment | `/ai/sentiment` | `SentimentAsync` | no | canned reply asserted ✓ | solid — keep |
| F5 | Currency strength | `/ai/currency-strength` | `CurrencyStrengthRefresher` | calendar data (seedable) | refresh → narrative canned ✓ | solid — extend pair-matrix assert |
| F6 | Portfolio digest | `/ai/digest` | `SummarizePortfolioAsync` | running/completed instances | **enabled + no-crash only** | **seed instances → assert digest output** |
| F7 | Live exposure | `/ai/exposure` | `AssessLiveExposureAsync` | live account | **enabled + no-crash only** | **seed positions (fake) → assert; live variant skips w/o creds** |
| F8 | Strategy tune / decay | `/ai/tune` | `AnalyzeBacktestAsync`/decay | cBot + backtest report | **enabled only** | **seed cBot+report → assert tune output** |
| F9 | Optimize param sets | `/ai/optimize` | `ProposeParamSets(Suite)` | cBot + params | **enabled only** | **seed cBot+params → assert proposed sets rendered** |
| F10 | Backtest analysis | InstanceDetail + MCP | `AnalyzeBacktestAsync` | completed backtest | not driven via UI | **seed completed backtest → open detail → assert analysis** |
| F11 | Instance post-mortem | InstanceDetail | `PostMortemAsync` | failed instance | not driven via UI | **seed failed instance → assert post-mortem** |
| F12 | Vision (chart image) | `/api/ai/vision` | vision | image upload | none found | **add: upload image → assert output (fake supports vision path)** |
| F13 | Curate | `/api/ai/curate` | curate | data | none found | **add coverage (UI or API-through-fake)** |
| F14 | Copy-profile recommend | `/copy-trading` AI suggest | `RecommendCopyProfileAsync` | source strategy | asserts **"not configured"** only | **run under `ai-local` → assert real recommendation renders** |
| F15 | Portfolio agent | `/agent` | `PortfolioAgentService` + mandate | account + instances | create-mandate enabled only | **seed mandate → run one cycle → assert AI decision/log rendered** |
| F16 | Agent studio | `/agent-studio` | `ResearchDesk`/`AiAgentDecisionEngine` | agent config | start enabled only | **create → start → assert AI research/decision output** |
| F17 | Prop guard / risk | `/nodes` PropGuard, `AiRiskGuard` | `AssessRiskActionsAsync` | running bots | `PropGuardTests` (verify scope) | **seed running bots → assert AI risk verdict surfaces** |
| F18 | Alerts AI | `/alerts` | `AlertEvaluator` AI sentiment | alert rule + tick | overflow test only | **seed rule → trigger eval → assert AI-enriched alert** |
| F19 | MCP AI tools (5) | MCP HTTP+SSE | `AiTools` | some need data | `McpAiToolsLocalLlmTests` (integration) | **extend to all 5 through fake LLM; data-tools seed state** |

Legend: features F2–F5 already meet the bar. F6–F19 are the real work.

---

## 2. Core problem to solve: seed real portfolio state in-process

The four "enabled + no-crash" pages (digest/exposure/tune/optimize) and the agent/copy/prop features
call AI **only after** the user has real data — running/completed **Instances**, a linked
**TradingAccount**, backtest **reports**, positions. The current fixture can't produce that
deterministically (needs Docker/nodes/broker), so the tests stop at "doesn't crash."

**Fix: a deterministic seeding seam** so E2E can create that state without Docker/broker:

- **Instances / backtest reports.** Seed completed + failed `Instance` rows (TPH terminal states) with
  a canned backtest report JSON directly via a **test-only seed endpoint** or the existing repository
  seam used by integration tests. Terminal instances need no container. This unlocks F6, F8, F9, F10,
  F11, F17.
- **Trading account + positions.** For exposure/agent, seed a `TradingAccount` and a **`FakeTradingSession`**-
  style position snapshot (the copy-trading simulator already models this) so `AssessLiveExposureAsync`
  gets non-empty input. This unlocks F7, F15 deterministic path.
- **Copy source strategy.** Seed a copy source so `RecommendCopyProfileAsync` has a strategy to reason
  about (F14).

Decide seam in step 4 (prefer reusing an existing test-seed path over adding a new endpoint; if a new
one is needed it must be **test-env-gated**, never in prod routing).

---

## 3. Real model verification via built-in ONNX (the user's explicit ask)

Prove the stack works against an **actual model** using the app's shipped **built-in ONNX** provider
(`Microsoft.ML.OnnxRuntimeGenAI`, `BuiltInOnnx`) — a real model running **in-process, no external
server** (no Ollama). The seam already exists: `OnnxAppFixture` / `OnnxE2ETests` boot the app with the
built-in enabled and pointed at a real ONNX GenAI model dir via `AI_ONNX_MODEL`, and skip cleanly when
absent.

- Reuse/extend `OnnxAppFixture` (`App__Ai__BuiltIn__Enabled=true`, `App__Ai__BuiltIn__ModelPath`); the
  model can **auto-download** on startup, so the lane needs no external service.
- Assertions are **non-deterministic-safe**: output present + non-empty + no error UI + status reports
  `kind = "BuiltInOnnx"` — not a canned marker.
- Extend beyond today's review-only `OnnxE2ETests` to the other data-independent features
  (sentiment/debate/currency-strength) end-to-end against the real ONNX model — enough to prove "a real
  model actually answers through our stack."

---

## 4. CI strategy — no conflict, heavy tests gated

Constraint from the request: *"work fine inside GitHub Actions without conflict with other E2E tests;
skip heavy tests from GitHub Action."* **Gating is CI-only.** **Locally, nothing is skipped — every
test runs, including the heavy ones (Docker build, real ONNX model, live account when creds present).**
The gates below suppress heavy tests **only** in the GitHub Actions PR lane; the local `dotnet test`
run executes the full suite.

- **Default lane (required, existing `e2e` job).** All fake-LLM real-user tests that need **no Docker,
  no broker** run here: F2–F11 (seeded), F13, F14, F16, F17, F18, F19, plus vision F12. These are the
  QA bar and must stay green on every PR. Collection stays `ai-local` (single fixture, serialized) to
  avoid port/circuit contention with the other E2E collections.
- **Heavy tests — `[Trait("Category","Heavy")]`, filtered out of the PR lane by CI only.** The GitHub
  Actions PR job adds `--filter Category!=Heavy` (or runs them in a separate non-required nightly job).
  **`dotnet test` with no filter — the local default — runs them.** These are:
  - **F1 build pipeline** (needs Docker-in-Docker). Locally Docker is present → runs. CI PR lane filters
    it out; a Docker-enabled/nightly job runs it.
  - **Real ONNX model** (§3) via `AI_ONNX_MODEL` (auto-download or cached dir). Locally set/available →
    runs; in-process, no external server. CI PR lane filters it out (non-blocking nightly job runs it).
  - **Live-account variants** of F7/F15/F14 — the `CopyLive`/`CMIND_ONBOARD` onboarding pattern.
    **Locally, onboard creds first (`CMIND_ONBOARD=1`) so these actually run — do not leave them
    skipped** (per `live-testing-self-serviceable`). They skip only where creds genuinely can't exist
    (the CI PR lane).
- **Mechanism, not deletion.** Gating is a **test filter / env flag**, never a removed test. A heavy
  test always exists and always runs locally; CI merely chooses not to run it on every PR.
- **Isolation.** Reuse the existing per-collection fixture pattern and the context-cap fix
  (`e2e-appfixture-context-leak`) so the new data-seeding tests don't leak circuits into later
  collections. Keep the demo/unconfigured gate tests in their own collections (already separate).

---

## 5. Work breakdown

1. **Confirm the seeding seam** (§2). Locate/choose the deterministic path to create terminal
   Instances, backtest reports, a TradingAccount + position snapshot, and a copy source. Reuse
   integration-test seeding if reachable from E2E; else a test-env-gated seed endpoint.
2. **Add data-backed assertions** to `AiFeatureLocalTests` (or a new `AiFeatureDataE2ETests` in the
   `ai-local` collection) for **F6–F11**: seed → open page → click → assert the canned reply renders in
   the feature's result panel (`data-testid`), not just "no crash." Add `data-testid`s where a result
   panel lacks one (UI change tracked separately — this plan doesn't edit code).
3. **Promote F14 copy-recommend** to also run under `ai-local` with a seeded source, asserting a real
   recommendation renders (keep the existing "not configured" gate test too).
4. **Agent F15 / F16 real-user flow:** create mandate/agent → start/run one cycle → assert the AI
   decision/research output appears in the run log/timeline UI.
5. **Prop guard F17 / Alerts F18:** seed running bots / an alert rule → trigger evaluation → assert the
   AI verdict/enriched alert surfaces in the UI.
6. **Vision F12 / Curate F13:** add the missing coverage (image upload path; curate through fake).
7. **MCP F19:** extend `McpAiToolsLocalLlmTests` to all 5 tools; data-dependent tools get seeded state.
8. **Real-model lane (§3):** extend `OnnxE2ETests`/`OnnxAppFixture` (built-in ONNX) to the
   data-independent features; non-blocking job with `AI_ONNX_MODEL`.
9. **CI wiring (§4):** default lane green; heavy + real-LLM + live lanes gated/non-required.
10. **Docs:** update `website/docs/testing/*` (+ all i18n locales per mandate 8) describing the AI QA
    suite and how to run the real-LLM/live lanes locally.

---

## 6. Definition of done

- [ ] Every AI feature F1–F19 has an E2E that drives it **as a real user** and asserts the **AI output
      renders** (canned marker on fake; non-empty on real) — no feature left at "enabled + no-crash."
- [ ] Data-dependent features seed real in-DB state deterministically (no Docker/broker) so their AI
      output is actually produced and asserted.
- [ ] Built-in **ONNX** real-model lane proves the stack works against an actual model (in-process, no
      external server) — data-independent features covered, not just review.
- [ ] Live-account variants use the onboarding pattern; **run locally after `CMIND_ONBOARD=1`**, skip
      only in the CI PR lane where creds can't exist.
- [ ] **Locally, `dotnet test` runs the FULL suite — nothing skipped**, including heavy (Docker build,
      real ONNX, live account). All green. Gating (`Category!=Heavy` filter) applies **only** to the
      GitHub Actions PR lane, which stays green and contention-free; heavy tests still run in CI via a
      separate non-required/nightly job.
- [ ] No analyzer/`get_file_problems` regressions on touched files.
- [ ] Testing docs updated in all locales.
- [ ] **Then commit + push to `main`** (per repo convention) — only after the full suite passes.

---

## 7. Risks / open questions

- **Seeding seam.** If no reusable E2E-reachable seed path exists, a test-only endpoint is needed —
  must be env-gated so it never ships in prod routing. Confirm in step 1 before writing tests.
- **Result-panel `data-testid`s.** Some AI pages may lack a stable output selector for the seeded path;
  small UI additions (test hooks) may be required — track as a separate, minimal code change.
- **Fake LLM structured replies.** Features needing structured JSON (like currency-strength already
  does) may need the fake to return feature-specific payloads (extend `FakeLocalLlmServer` per-marker,
  as done for `CurrencyGatherMarker`).
- **Real ONNX flakiness/cost.** First run may auto-download the model (slow); inference output is
  non-deterministic. Keep that lane's assertions loose (presence, not content) and its CI job
  non-blocking; cache the model dir where possible.
