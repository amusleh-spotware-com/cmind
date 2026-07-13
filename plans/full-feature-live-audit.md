# Plan — Full-Feature Live Audit (real-user Playwright sweep → RECORD findings, do NOT fix)

## Why this plan exists

Every feature is *supposed* to be covered by unit + integration + E2E per the CLAUDE.md mandates,
yet manual clicking keeps surfacing broken behavior: dead controls, raw GUIDs, blank pages, silent
no-ops, 500s, un-gated features, circuit crashes. The existing suite passes but does not *behave like
a suspicious human*. This plan drives **every feature through Playwright as an actual user with live
credentials**, and hunts the bugs the green suite misses.

> **AUDIT-ONLY MODE (hard rule).** This is a **read-only investigation**. When a bug is found the
> subagent **does NOT fix it, does NOT edit app code, does NOT weaken or add tests to make it pass**.
> It **records the finding with full detail** in the shared findings file and **keeps testing**. The
> only files any lane writes are: (a) the shared findings markdown, and (b) evidence artifacts
> (screenshots / console+network logs) under `tmp-audit/`. No `.cs`, `.razor`, `.csproj`, resx, or
> docs edits. Fixing + regression tests are a **separate follow-up plan** authored after this audit.

The work is partitioned into independent **lanes**. The orchestrator (main agent) spawns **one
subagent per lane, in parallel**. Lanes touch disjoint feature areas, so they do not collide. Because
no lane edits code, there are no shared-infra write conflicts — the only shared write target is the
findings file, and each lane owns its own section of it (see § Findings file).

---

## Ground truth about the harness (read before doing anything)

- **App boot for E2E:** `tests/E2ETests/AppFixture.cs` boots the real Web app against a Testcontainers
  Postgres, seeds owner `owner@e2e.local` / `Owner_Pass_123!`, logs in, captures storage state. All
  page-level E2E reuse this. AI lane uses `AiLocalFixture` (collection `ai-local`) which additionally
  wires an AI provider.
- **Live credentials (real cTrader / Open API):** live copy/OpenApi tests read
  `secrets/dev-credentials.local.json` (unified) — client id/secret + cID username/passwords. The
  OAuth token cache (`secrets/openapi-tokens.local.json`) is produced once per machine by
  `CopyLive/OnboardingTests.Onboard_all_cids_and_write_token_cache` under `CMIND_ONBOARD=1`. Refresh
  tokens don't expire, so after onboarding the live lane runs headless with no interaction.
- **Live AI:** real provider when `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` /
  `AI_E2E_MODEL`) is set; otherwise the existing in-process fake. For **this audit** the AI lane drives
  features against whatever is wired **today** (live provider if `AI_E2E_*` set, else the current
  fake) — it does **not** change the fixture, because audit-only mode forbids code edits.
  > **Deferred to the follow-up fix plan (user directive, recorded here so it isn't lost):** replace
  > `FakeLocalLlmServer` with the **Microsoft LLM testing package** — `Microsoft.Extensions.AI`
  > (+ `Microsoft.Extensions.AI.Abstractions`) test/fake `IChatClient` — behind the same
  > `IAiClient`/`AiLocalFixture` seam, and update CLAUDE.md mandate 2 + `multi-provider-ai` docs to
  > match. The AI lane logs this as a standing item in the findings file (§ Findings file → Deferred
  > infra changes) but performs no edit during the audit.
- **Skip-clean, never skip-silent:** where a secret is genuinely absent, the live test must **skip
  cleanly** (the `CopyLive` / `CMIND_ONBOARD` pattern) — the deterministic/gated path is still tested.
  Missing creds is NEVER a reason to leave a tier unwritten (CLAUDE.md mandate 2).
- **Browser:** Edge on Windows, Chromium fallback (`e2e-playwright-edge` memory).
- **Do NOT `git add -A`.** Concurrent lanes + auto-commit tree. Each lane stages **explicit paths only**
  (its own touched files). This is a hard rule — the repo has a documented git-add hazard.

### Prerequisites the orchestrator verifies ONCE, up front (before spawning lanes)

1. `dotnet build` clean (0 warnings) — baseline green.
2. `dotnet test tests/E2ETests` compiles and the fixture boots (run one cheap test, e.g.
   `PageSmokeTests`).
3. Live creds present? Check `secrets/dev-credentials.local.json` and
   `secrets/openapi-tokens.local.json`. If app+cIDs exist but token cache missing → run the
   `CMIND_ONBOARD=1` onboarding once. If absent entirely → note it; live lanes run their skip-clean
   path and the orchestrator records "live path unverified — creds absent" in the final report (does
   NOT count as done-with-live).
4. Live AI? Check `AI_E2E_BASEURL`. If unset, AI lane runs the **`Microsoft.Extensions.AI` test
   `IChatClient`**; record it. Confirm the `Microsoft.Extensions.AI` package is referenced by the test
   project (add it if not) as part of the fixture swap.

---

## The exploratory protocol (every lane applies this to every page/flow it owns)

Existing E2E assert known-good paths. The bugs hide in the *unasserted* corners. For **each route and
each interactive control** in the lane, the subagent drives Playwright and checks:

1. **Loads without crashing** — navigate; assert HTTP 200 render, no ErrorBoundary, circuit stays up.
   Repeat for **empty state**, **populated state**, and **terminal/failed/missing entity** state.
2. **No console errors / no failed network** — capture `page.on("console")` errors and `page.on(
   "response")` ≥400. A 500 or an unhandled JS error on any interaction is a bug.
3. **No raw identifiers** — assert no `Guid`-shaped text visible to the user (mandate 11). Human values
   only (account number, name).
4. **Control state correctness** — every lifecycle icon button (start/stop/delete/kill/pause) is
   **disabled in states where the action is invalid**; enabled only when valid. A button that would
   no-op or 409 is a bug.
5. **Dependency gating** — with the dependency absent (no account / no API key / no model), the feature
   is hidden **or** shows a clear actionable notice + disabled trigger with tooltip — never a silent
   no-op, never a raw `stream connect failed`. With the dependency present, the working path runs.
6. **Dialog flows** — every add/create/edit opens a MudBlazor **dialog** (not an inline page form);
   submit, validate, cancel, re-open all work; validation errors show.
7. **Localization + RTL** — switch to a non-default locale and to Arabic (`dir=rtl`); assert no
   hard-coded English leaks, layout doesn't break.
8. **Mobile** — 360px viewport: no horizontal scroll, dialogs usable, nav works.
9. **The real feature actually works** — not just "renders": submit the form, run the bot, trigger the
   AI call, and assert the *outcome* (row appears, status transitions, AI reply renders, file changes).

When step N fails → that's a bug. Follow the finding workflow (record, don't fix).

---

## Finding workflow (identical for every lane) — RECORD, don't fix

For each confirmed bug the subagent **records and moves on** — no code change, no test change.

1. **Reproduce minimally** in Playwright; capture screenshot(s) + console/network log to
   `tmp-audit/<lane>/<bug-slug>/`. Note the exact steps so it's replayable.
2. **Root-cause read-only.** Locating the culprit code is *optional and read-only* — if done for
   C#/.NET use **Rider MCP** (`jetbrains` read tools: `get_symbol_info`, `search_symbol`,
   `get_file_text_by_path`; always pass `projectPath: C:/Users/afhac/source/cMind`). **No `Edit`, no
   `replace_text_in_file`, no `apply_patch`, no build, no test run to "confirm the fix".** Suspected
   file:line goes in the finding as a *lead*, not a patch.
3. **Append a full-detail entry to the findings file** (§ Findings file — one entry per bug, the schema
   below). Include severity, exact repro, observed vs expected, evidence paths, suspected root cause,
   and which tiers *should* have caught it (so the follow-up plan knows what to add).
4. **Continue testing.** Do not stop the lane on a bug; a broken feature still gets the rest of its
   protocol run (other controls on the page, mobile, RTL) where possible. If a bug hard-blocks further
   navigation, record that and move to the next route.

**Explicitly NOT done in this audit** (deferred to the follow-up fix plan): fixing code, adding/growing
regression tests, editing fixtures (incl. the `Microsoft.Extensions.AI` swap), localizing new strings,
updating docs, running `dotnet build`/`dotnet test` to green, EF migrations. The audit's only output is
findings + evidence.

---

## Findings file

**One shared file:** `plans/full-feature-live-audit-findings.md`. The orchestrator creates it with the
skeleton before spawning lanes (top matter + one `## Lane X` heading per lane + a `## Deferred infra
changes` section). Each lane appends **only under its own `## Lane X` heading** — no two lanes write the
same region, so parallel appends don't collide. IDs are namespaced per lane (`A-01`, `A-02`, `C-01`…).

Every bug is one entry with this schema (fill all fields):

```markdown
### <ID> — <short title>
- **Severity:** blocker | high | medium | low   (blocker = data loss / money-move wrong / circuit crash / security)
- **Route / feature:** /path  (control or flow)
- **State:** empty | populated | terminal/failed | gated-off | mobile | RTL | locale=<xx>
- **Live or fake:** live-creds | fake | n/a
- **Steps to reproduce:** 1… 2… 3…  (exact, replayable)
- **Observed:** what actually happened (quote exact error text / HTTP status / console message)
- **Expected:** what a correct app should do
- **Evidence:** tmp-audit/<lane>/<slug>/  (screenshot + console + network log filenames)
- **Suspected root cause (read-only lead):** file:line / symbol — best guess, may be blank
- **Which tier SHOULD catch it:** unit | integration | E2E  (+ note why the current suite missed it)
- **Regression-test sketch (for the follow-up plan):** one line — the assertion that would lock it
```

The **`## Deferred infra changes`** section holds standing non-bug items — chiefly the
`FakeLocalLlmServer` → `Microsoft.Extensions.AI` test-client swap (user directive), plus any missing-
creds "live path unverified" notes.

At the end, the orchestrator adds a **summary table** at the top: counts by severity and by lane.

---

## Lanes (spawn one subagent each, parallel)

Each lane owns a disjoint route/feature set. Lane subagent = general-purpose but runs **read-only**
(drives Playwright, reads code, writes only findings + evidence). Give each the exploratory protocol +
finding workflow above and its scope below.

### Lane A — Auth, Access & Identity
Routes/flows: `/login`, `/register`, 2FA challenge (`TwoFactorChallenge`), `/users`, `/account`,
password/`MustChangePassword`, `/set-culture` + localization + RTL across the shell.
Live: real login only (owner seed); registration is white-label-gated (`App:Registration`, flag OFF
by default) — test both gated-off (404/hidden) and enabled paths. 2FA TOTP via `ITotpAuthenticator`.
Existing E2E: `LoginTests`, `RegistrationTests`, `MfaFlowTests`, `LocalizationTests`, `SettingsTests`.

### Lane B — cBots Lifecycle (build / run / backtest / optimize / nodes)
Routes: `/cbots`, `/run`, `/backtest`, `/optimize`, `/nodes`, `InstanceDetail`, `BuilderEditor`,
`InstanceTable`, `AssistantBuildBot`. Focus: instance state TPH transitions (id changes
starting→running→terminal), per-row control enable/disable on terminal vs active, view/eye on a
**failed** instance must not crash the circuit, no raw container/instance GUIDs. Optimize is
deliberately unsupported by cTrader Console — assert it's gated/hidden, not a doomed button.
Live: real cBot build needs Docker socket on web host; run/backtest dispatched to a node —
`CBotRealRunBacktestTests`, `RunBacktestFlowTests` patterns. Skip-clean if Docker/node absent but keep
the gated + UI-state assertions.
Existing E2E: `CBotLifecycleTests`, `CBotRealRunBacktestTests`, `RunBacktestFlowTests`, `NodesUiTests`.

### Lane C — Copy Trading & Open API accounts
Routes: `/copy-trading`, `/accounts`, `TradingAccountList`, `/settings/openapi`,
`OpenApiApplications`, OAuth invite/callback. **This is the primary live-credential lane.** Onboard
cIDs (`CMIND_ONBOARD=1`) first; then drive: connect account, configure copy source→target, sizing
modes, order types, partial-fill true-up, token rotation, node affinity, desync/resync. Assert account
**number** shown (never cID GUID), dependency gate when no account connected.
Live: real Open API — the whole point. Skip-clean per cID when its creds absent.
Existing E2E: `CopyTradingTests`, `CopyFeaturesE2ETests`, `OpenApiTests`, `OpenApiSharedAppTests`,
`OpenApiInviteTests`, `CopyLive/*`.

### Lane D — Prop Firm, Prop Guard & Compliance
Routes: `/prop-firm`, `/prop-guard`, `/compliance`. Live equity tracking via Open API (shares Lane C
creds — coordinate through orchestrator on the token cache, read-only). All challenge rule types,
node lease, breach paths. Assert gating when no live account.
Existing E2E: `PropFirmTests`, `PropGuardTests`, `ComplianceTests`.

### Lane E — AI features & Agent surface
Routes: all `/ai/*` (`build`, `debate`, `digest`, `exposure`, `optimize`, `review`, `tune`),
`/agent`, `/agent-studio`, `/alerts`, `/settings/ai`, `/mcp`. Drive every AI feature against what's
wired today (live when `AI_E2E_BASEURL` set, else the current fake) and **record** any that: don't
render output, don't honor the keyless "not configured" gate, crash, or leak errors. Cover MCP AI
tools too. Multi-provider (Anthropic/OpenAI/Azure/Gemini/local/ONNX default-on). **No fixture edit** —
the `FakeLocalLlmServer` → `Microsoft.Extensions.AI` swap is logged under § Findings → Deferred infra
changes, not performed.
Existing E2E: `AiPagesTests`, `AiPagesWithDataTests`, `AiFeatureLocalTests`, `AiDemoLiveTests`,
`AgentTests`, `AgentStudioTests`, `AlertsTests`, `AiCopyRecommendTests`, `AssistantBuildBotTests`.

### Lane F — Quant / Institutional-edge suite
Routes: `/quant/execution`, `/quant/health`, `/quant/integrity`, `/quant/positioning`,
`/quant/regimes`, `/quant/sizing`, `/quant/tca`, `/journal`. Deterministic cores — assert real
computed output, empty-state, and no crash on missing input data.
Existing E2E: `Quant*Tests`, `JournalTests`.

### Lane G — Economic Calendar & Currency Strength
Routes: `/economic-calendar`, `CalendarSeries`, `/ai/currency-strength`, `/ai/sentiment`. Calendar
ingestion default-ON; needs FRED/BLS key for some sources — assert key-gated notice when absent
(`CalendarEnablement`), working path when present. Currency-strength is calendar-anchored + cBot
`market:read` JWT.
Existing E2E: `EconomicCalendarE2ETests`, `CurrencyStrengthTests`.

### Lane H — Settings, White-label & Feature toggles
Routes: `/settings/deployment`, `/settings/features`, `/settings/legal`, branding. Every white-label
option owner-tunable at runtime and reflected live (`IWhiteLabelSettings` decorator); feature flags
gate nav/pages/endpoints consistently. Assert `WhiteLabelCatalog` parity behavior in the UI, Nodes UI
Full/Monitor/Hidden modes, RTL branding.
Existing E2E: `DeploymentSettingsE2ETests`, `SettingsDialogTests`, `FeatureToggleTests`,
`BrandingTests`, `BrandIdentityTests`, `NodesHiddenTests`, `NodesMonitorTests`.

### Lane I — Dashboard, Shell, PWA, Mobile & Accessibility
Routes: `/` dashboard + customize, nav menu, `/mcp` landing, PWA install, help tips. Cross-cutting:
mobile journeys 320–1920px, accessibility (axe), page-health sweep. This lane also runs the
**full `PageSmokeTests` + `PageHealthTests` + `RouteCoverageTests`** as the safety net that every route
the other lanes touch is registered.
Existing E2E: `DashboardTests`, `DashboardCustomizeTests`, `NavMenuTests`, `PwaTests`,
`MobileJourneyTests`, `MobileLayoutTests`, `MobileDialogTests`, `AccessibilityTests`, `HelpTipTests`,
`PageHealthTests`, `PageSmokeTests`, `MiscUiTests`.

---

## Coordination (orchestrator responsibilities)

- **No code edits, so no shared-infra write conflicts.** The only shared write target is
  `plans/full-feature-live-audit-findings.md`, and each lane appends only under its own `## Lane X`
  heading. Evidence goes to per-lane `tmp-audit/<lane>/` dirs — disjoint by construction.
- **Findings-file appends only.** A lane never rewrites another lane's section and never edits the
  top matter / summary table (orchestrator-owned). To dodge any last-write-wins race on the single
  file, a lane MAY instead write its section to `tmp-audit/<lane>/findings.md` and hand it to the
  orchestrator to merge — orchestrator's call at spawn time.
- **Live-cred token cache is shared, read-only.** Lanes C and D both consume
  `secrets/openapi-tokens.local.json`. Onboarding runs **once** (orchestrator, before spawning);
  lanes only read it, never write it.
- **Sub-parallelism inside a lane.** A heavy lane (B, C, E) may fan out **read-only** investigation to
  `cavecrew-investigator`. It never spawns `cavecrew-builder` — no edits happen in this audit.

---

## Per-lane definition of done (subagent reports this back)

A lane is done when, for its scope:

- [ ] Every route/control in scope driven through Playwright per the exploratory protocol — evidence
      (screenshots/logs) in `tmp-audit/<lane>/`.
- [ ] Every bug found is **recorded** as a full-schema entry under the lane's heading in the findings
      file. **Zero code/test edits made** (audit-only).
- [ ] Live path exercised with real creds where available; where genuinely absent, logged as "live path
      unverified — creds absent" (never silently skipped).
- [ ] AI features (if in scope) driven against the wired provider (live or current fake); the
      `Microsoft.Extensions.AI` swap logged under Deferred infra changes, not performed.
- [ ] A one-line lane summary returned to the orchestrator: #routes covered, #bugs by severity, any
      route that could not be reached and why.

## Global exit criteria (orchestrator, after all lanes return)

- [ ] All lanes report done; every bug is a full-schema entry in the findings file with evidence.
- [ ] **Working tree still clean of source edits** — `git status` shows only
      `plans/full-feature-live-audit-findings.md` (and this plan). No `.cs`/`.razor`/`.csproj`/resx/docs
      changes. If any appeared, revert them — this audit does not modify the app.
- [ ] Summary table (counts by severity × lane) filled at the top of the findings file; Deferred infra
      changes section populated (incl. the Microsoft LLM swap).
- [ ] Every route from all lanes appears in the findings file's coverage list (even if "no issues").
- [ ] Findings file committed to `main` (explicit path only: `git add plans/…-findings.md`;
      commit-direct-to-main). A **separate follow-up fix plan** is authored from the findings — fixing
      + regression tests happen there, not here.

---

## Execution order

1. Orchestrator runs the prerequisite checks (§ Prerequisites), onboards live creds if possible, and
   creates `plans/full-feature-live-audit-findings.md` with the skeleton (top matter + per-lane
   headings + Deferred infra changes).
2. Orchestrator spawns Lanes A–I as parallel subagents with this plan + their scope, stressing
   **audit-only: record, do not fix**.
3. Lanes execute exploratory protocol → finding workflow (record + continue) → per-lane DoD; return a
   summary + their appended findings section.
4. Orchestrator merges any per-lane findings, fills the summary table + Deferred section, and verifies
   the working tree has **no source edits** (reverts any that slipped in).
5. Orchestrator commits/pushes the findings file, then drafts the follow-up **fix plan** (per-bug: root
   cause → fix → unit+integration+E2E regression test) as a new file under `plans/`.
