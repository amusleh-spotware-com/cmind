# Plan — Multi-Model AI + Model Browsing + Async AI Tasks

## Goals (from request)

1. **Add & use any model.** User adds a provider, then **browses the models the endpoint exposes**
   (LM Studio / Ollama / vLLM / llama.cpp / LocalAI all serve `GET /v1/models`) and **selects one**
   instead of hand-typing a model id.
2. **Swap the built-in local model** Phi-3-mini-4k → **Phi-3.5-mini-instruct** (ONNX GenAI), and make
   switching to other local models easy (browse + one-click).
3. **Multiple models active at once — per AI feature.** Kill the single global `IsActive`-drives-everything
   model. Each AI feature can be bound to a different model; different features run different models
   concurrently.
4. **Async AI tasks.** Replace the single synchronous "Build cBot" screen with a **task**: user enters the
   prompt, selects the model(s), submits; the task keeps running in the background; the user navigates
   elsewhere and returns to see progress / result. Generalise to the other long-running AI features.

## Current architecture (seams that matter)

- `Core/Ai/AiProviderKind.cs` — wire families; every OpenAI-compatible local runtime = `OpenAiCompatible`
  + different base URL (already true).
- `Infrastructure/Ai/AiProviderStore.cs` — CRUD over `AiProviderCredential`; **one `IsActive` per scope**;
  `Active` cached with short TTL.
- `Infrastructure/Ai/RoutingAiClient.cs` — resolves `_store.Active`, injects into `AiProviderRequest`,
  routes to the `IAiProvider` adapter by `Kind`. **This is the single seam to make model-aware.**
- `Infrastructure/Ai/AiFeatureService.cs` + `Core/Ai/AiContracts.cs` (`IAiFeatureService`) — ~20 feature
  methods, all `client.CompleteAsync(...)`.
- `Web/Endpoints/AiEndpoints.cs` — feature endpoints; `/build-strategy`, `/generate-project`,
  `/optimize-run` run the **inline synchronous** build/self-repair loop.
- `Infrastructure/Ai/BuiltInModelInstaller.cs` + `AiConstants` — fixed single built-in model download.
- `Web/Components/Pages/Ai/AiBuild.razor` — synchronous build UI (blocks until done).

## Target architecture

### A. Per-feature model binding (multiple models active)

Keep `AiProviderCredential` as the **credential store**. Introduce an explicit **feature → provider**
binding and thread a **provider selector** through the request path so `RoutingAiClient` resolves the
chosen credential instead of the lone `Active`.

- New enum `Core/Ai/AiFeature.cs` — one value per `IAiFeatureService` method (`BuildCBot`, `ReviewCBot`,
  `FixCBot`, `ProposeParamSetSuite`, `AnalyzeBacktest`, `Debate`, `Sentiment`, `Vision`, `Curate`,
  `Optimize`, `Digest`, `Exposure`, `Tune`, `PostMortem`, `RiskGuard`, `Agent`, `Alert`, `CopyProfile`,
  `CurrencyForward`, `CurrencyExplain`). Census-gated (see Tests).
- New aggregate `AiFeatureBinding` (Core) + table: `(OwnerUserId?, AiFeature, AiProviderCredentialId)`,
  unique on `(OwnerUserId, AiFeature)`. Resolution order for a feature: **explicit request credential →
  user feature binding → deployment feature binding → scope `Active` (fallback default) → fail.**
  `IsActive` stays as the *default* provider used when a feature has no binding — backward compatible.
- Extend the seam:
  - `Core/Ai/AiContracts.cs`: `IAiClient.CompleteAsync(AiTextRequest, AiProviderSelection?, ct)` where
    `AiProviderSelection` = `{ AiProviderCredentialId? CredentialId; AiFeature? Feature; }`.
  - `IAiFeatureService` methods gain an optional `AiProviderSelection? provider = null` (default keeps
    current behaviour) so a task/endpoint can force a specific model; each method passes its own
    `AiFeature` so the binding applies when no explicit credential is given.
  - `IAiProviderStore`: add `ResolveFor(AiProviderSelection, UserId?)` returning `ActiveAiProvider?`
    (moves the fallback chain into the store; `RoutingAiClient` calls it instead of `.Active`).
  - `AiProviderStore`: add binding CRUD (`ListBindingsAsync`, `SetBindingAsync`, `ClearBindingAsync`)
    per scope + `ResolveById`. Invalidate binding cache alongside the active cache.

### B. Model browsing / discovery

- New `Core/Ai/IAiModelCatalog.cs` → `Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(AiProviderKind,
  AiEndpoint, string? apiKey, ct)`. `AiModelInfo(string Id, string? Family, long? SizeBytes)`.
- `Infrastructure/Ai/AiModelCatalog.cs`:
  - `OpenAiCompatible` / `AzureOpenAi` → `GET {baseUrl}/models`, parse `data[].id` (covers LM Studio,
    Ollama `/v1`, vLLM, llama.cpp server, LocalAI, OpenRouter, Groq, Together...).
  - `BuiltInOnnx` → enumerate model subdirs under `App:Ai:BuiltIn:ModelPath` (each dir with a
    `genai_config.json` = one selectable local model) **plus** a curated downloadable catalog
    (Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B) with per-model download via `BuiltInModelInstaller`.
  - `Anthropic` / `Gemini` → static/known-model list (their list endpoints optional, low priority).
  - Always degrade to an empty list + typed reason on failure (never throw into the UI).
- New endpoint `GET /api/ai/models?providerId=…` **and** `POST /api/ai/models/probe { kind, baseUrl,
  apiKey }` (probe an unsaved endpoint while the user is filling the dialog) — owner + per-user scoped.
- Reuse the named `"ai"` `HttpClient` resilience pipeline.

### C. Swap the built-in model + multi-built-in

- `AiConstants`: `BuiltInModelDownloadBaseUrl` → Phi-3.5-mini-instruct ONNX repo
  (`microsoft/Phi-3.5-mini-instruct-onnx`, cpu-int4 folder), `BuiltInModelDownloadFiles` → its file set
  (probe the HF repo tree for exact names before coding — don't guess). `BuiltInModel` label → e.g.
  `phi-3.5-mini-instruct`.
- `BuiltInModelInstaller`: generalise from one fixed dir to **install-by-model-key** (dir per model under
  `models/onnx/<key>/`), `IsInstalled(key)`, `EnsureInstalling(key)`, `State(key)`, so several local
  models can coexist and be selected (feeds Model browsing for `BuiltInOnnx`). Keep the default
  auto-download (now Phi-3.5) for out-of-box.
- Update `AiProviderCapabilities.DefaultFor(BuiltInOnnx)` if Phi-3.5 changes support (still text-only, no
  tools/vision → unchanged; document it).

### D. Async AI tasks (the "create cBot task" model)

New aggregate + background worker + task UI. Mirrors the existing instance/copy-host patterns
(lease-claimed background work, `LogsHub` live logs, terminal states, lineage-stable id where needed).

- **Core aggregate `AiTask`** (`Core/Ai/AiTask.cs`), strong id `AiTaskId` (add to `StrongIds.cs` +
  `StrongIdConverter` in `DataContext`):
  - Fields: `UserId`, `AiFeature`, input payload (JSON: prompt/description, language, name, feature args),
    `AiProviderCredentialId` (the selected model), `AiTaskStatus` (`Queued`→`Running`→`Succeeded`/`Failed`/
    `Cancelled`), `CreatedAt`/`StartedAt`/`FinishedAt` (all via `TimeProvider`), `ResultText`,
    `ResultRefsJson` (e.g. produced `CBotId`/`ProjectId`), `Error`, `Attempts`, plus a claim lease
    (`ClaimedBy`, `LeaseExpiresAt`) for multi-replica safety.
  - Intention-revealing transitions guarding invariants: `Claim(node, now, lease)`, `MarkRunning(now)`,
    `Succeed(text, refs, now)`, `Fail(reason, now)`, `Cancel(now)` (only from Queued/Running),
    `RenewLease(now)`. Invariant violations throw `DomainException`. No public setters.
  - Live log: append-only `AiTaskLog` lines (or stream via `LogsHub` and persist a capped tail) — a line
    per state change / attempt / self-repair fix / failure (mandate 11: user-started background work has
    live logs + full activity).
- **Worker `AiTaskRunner`** (`BackgroundService`): claims `Queued` tasks by lease, dispatches by
  `AiFeature`. `BuildCBot`/`GenerateProject`/`OptimizeRun` **must run on the Web host** (Docker socket, as
  `CBotBuilder` does) → host the runner in `src/Web` (or gate those features to the web-host node). Emits
  `LogsHub` lines keyed by `AiTaskId`; renews lease; writes terminal result in one `SaveChanges`.
  Reconciles orphaned `Running` tasks whose lease expired (fail or requeue), like the instance pollers.
- **Refactor the build pipeline out of the endpoint** into a reusable
  `Infrastructure/Ai/CBotBuildFlow` (the generate→build→self-repair→create-CBot loop currently inline in
  `AiEndpoints`), callable by both the worker and (optionally) a sync path. DDD: orchestration stays in
  the flow/worker; domain decisions on the aggregates.
- **Endpoints** (`AiEndpoints`):
  - `POST /api/ai/tasks` `{ feature, payload, providerIds[] }` → creates **one `AiTask` per selected
    model** (this delivers "use multiple models at the same time" — fan-out to compare). Returns task ids
    immediately (non-blocking).
  - `GET /api/ai/tasks` (list, user-scoped), `GET /api/ai/tasks/{id}` (detail + result + log tail),
    `POST /api/ai/tasks/{id}/cancel`, `DELETE /api/ai/tasks/{id}`.
  - Keep the current synchronous endpoints working (thin wrapper → create task + not required to await),
    or deprecate after UI migration. Short features (sentiment/status) may stay synchronous.
- **UI**:
  - **Create task dialog** (`AiTaskCreateDialog.razor`, mandate 7 = dialog): prompt (real free-text →
    `MudTextField` OK), language `MudSelect`, name, **model multi-select** (`MudSelect` multi, from the
    user's providers + browse) → `POST /api/ai/tasks`. Disabled with tooltip when the user has **no
    provider configured** (mandate 11 dependency-gating).
  - **AI Tasks page** (`/ai/tasks`, add to `PageSmokeTests.Routes()` → `RouteCoverageTests`): rows show
    **feature + model name + status + created** (no raw GUIDs — map credential id → model/provider name),
    icon buttons: live-log (over `LogsHub`, enabled only while `Running`), view result (dialog), cancel
    (disabled on terminal), delete (disabled while active). Detail = dialog; renders for terminal/failed
    without crashing the circuit.
  - Result dialog: output text/code + **Run it / Open in editor** links (as `AiBuild` today) when the task
    produced a cBot.
  - Rework `AiBuild.razor` → thin launcher that opens the create-task dialog (or redirect to `/ai/tasks`).
  - **Settings → AI**: per-feature model binding UI (feature list → `MudSelect` of providers), the model
    **browse** button in `AiProviderDialog`, and the built-in model catalog (download Phi-3.5 / others).

## Phases (ship in order; each independently green)

- **P1 — Built-in swap + capabilities.** Phi-3.5-mini-instruct constants + installer default; update
  `OnnxLocalModelTests`, `BuiltInModelInstallerTests`, docs. Lowest risk, immediate value. *(Probe the HF
  repo file list first.)*
- **P2 — Model discovery.** `IAiModelCatalog` + `AiModelCatalog` + `GET/POST /api/ai/models[/probe]` +
  browse button in `AiProviderDialog`. Unit (parse `/v1/models`), integration (endpoint), E2E (browse
  against FakeLocalLlmServer serving `/v1/models`).
- **P3 — Multi-built-in installer.** `BuiltInModelInstaller` install-by-key + built-in catalog surfaces in
  discovery. Tests for multiple model dirs.
- **P4 — Per-feature model binding.** `AiFeature` enum + `AiFeatureBinding` aggregate + migration +
  `ResolveFor` in store + thread `AiProviderSelection` through `IAiClient`/`IAiFeatureService`/
  `RoutingAiClient`; Settings binding UI. Unit (routing resolution order), integration (binding
  persistence), E2E (bind two features to two providers, both work). Census gate
  `AiFeatureBindingParityTests` (every `IAiFeatureService` method ↔ `AiFeature`).
- **P5 — Async task engine.** `AiTask` aggregate + `AiTaskId` strong id + migration + `AiTaskRunner`
  worker + `CBotBuildFlow` extraction + task endpoints + `LogsHub` wiring. Unit (task transitions, lease,
  orphan reclaim), integration (create→worker runs→terminal via fake build / FakeLocalLlm), E2E per below.
- **P6 — Task UI.** Create-task dialog, `/ai/tasks` page, result dialog, rework `AiBuild`. Route coverage,
  mandate-11 E2E (disabled/empty + working, terminal detail renders, no GUID, live-log icon state).
- **P7 — Docs + localization.** `website/docs/features/ai-*.md` updates + **all 22 locales** via
  `translate-localization` skill (batch once at the end); new UI strings in `tools/i18n/ui-translations.json`
  all locales; regenerate resx.

## Tests (mandatory, all tiers)

- **Unit**: `AiTask` state machine + guards + lease/orphan; `AiFeatureBinding` resolution order in
  `AiProviderStore.ResolveFor`; `RoutingAiClient` picks the bound/explicit provider not just `Active`;
  `AiModelCatalog` parses `/v1/models` + built-in dir enumeration; capabilities for Phi-3.5.
- **Integration** (Testcontainers PG): binding + task persistence and CRUD; worker claim/lease under
  concurrency; `/api/ai/models` + `/api/ai/tasks` endpoints; built-in installer install-by-key.
- **E2E** (Playwright, **FakeLocalLlmServer** — mandatory for AI features; the fake also serves
  `/v1/models`):
  - Create a "Build cBot" **task**, assert it's `Queued/Running`, **navigate to another page**, return,
    assert it reaches `Succeeded` and the result (canned code) renders + Run/Open links.
  - **Model browse**: open provider dialog, browse, pick a model from the fake's `/v1/models`.
  - **Per-feature binding**: bind two features to two providers; both features run.
  - **Multi-model fan-out**: submit one task with two models → two task rows.
  - Mandate 11: create-task disabled + notice when no provider; task list live-log icon enabled only
    while running; cancel/delete disabled in invalid states; terminal task detail dialog renders; no raw
    GUID shown.
  - Keep the keyless "AI not configured" gate E2E; MCP AI tools via fake unchanged.
- **Census gates**: `AiFeatureBindingParityTests` (feature↔method), `RouteCoverageTests` (new `/ai/tasks`),
  `NoHardcodedUiTextTests`/`ResourceParityTests` (new strings), strong-id + arch guards for `AiTaskId`.

## DDD / mandate checklist

- `AiTask`, `AiFeatureBinding` = rich aggregates, strong ids, no public setters, `DomainException` on
  invariant breach, one `SaveChanges` per aggregate, cross-refs by strong id.
- All timestamps via injected `TimeProvider`; tests use `FakeTimeProvider`.
- New EF migration (canonical layout) for `AiTask`(+log), `AiFeatureBinding`; register `StrongIdConverter`.
- Zero warnings; analyzer sweep on `Core`/`Infrastructure`/`Web`/`Nodes`; `get_file_problems` clean.
- Every user-facing string localized (all 22 locales) via `translate-localization`; RTL renders.
- Docs updated same commit + localized.
- White-label: model-browse/binding under the existing `FeatureFlag.Ai`; built-in allowance still gated by
  `Branding.AllowBuiltInAi`.
- Kubernetes: worker + endpoints green under the in-cluster suite.

## Decisions (locked)

1. **Multiple models per feature = fan-out.** One bound default model per feature **+** a task may target
   multiple selected models → **one `AiTask` row per model**, compared side by side. Full ensemble (one
   task, N results, pick-winner) is explicitly deferred.
2. **Async scope = long-running only.** `BuildCBot` / `GenerateProject` / `OptimizeRun` become tasks; short
   features (sentiment/review/digest/etc.) stay synchronous request/response.
3. **Worker host** — `AiTaskRunner` runs in the **Web host** (Docker socket for builds); one worker
   location for MVP.
4. **Built-in model catalog** — curated fixed list (Phi-3.5-mini default + Qwen2.5-3B / Llama-3.2-3B
   optional) + the default auto-download; no free HF-repo entry in MVP.

## Risks / traps

- **HF file names** for Phi-3.5 ONNX differ from Phi-3 — probe the repo tree, don't guess (repo trap:
  don't guess external file lists).
- **Build must stay on the web host** (Docker socket) — don't schedule build tasks to CLI nodes.
- **E2E `--no-build` stale `Web.dll`** — rebuild `tests/E2ETests` after Web edits.
- **Lease/claim race** — model the reclaim path + test it (orphaned `Running` task after lease expiry),
  like the copy-host resync and instance pollers.
- **Don't add an `AiTaskStatus` value that ripples through unrelated filters** — mirror the copy-profile
  "Starting" lesson: derive display state where possible.
- **Threading `AiProviderSelection`** touches every `IAiFeatureService` call site — default the param to
  keep existing behaviour and migrate call sites deliberately.
