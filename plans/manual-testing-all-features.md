# Manual Testing Plan — Every Feature, Every Config, Every Deployment

> Purpose: a human tester's step-by-step guide to exercise **every** cMind feature by hand, across the
> different configurations that switch features on/off and across the different ways the app is deployed.
> Automated suites (unit/integration/E2E/stress/k8s) already gate CI — this plan is the manual
> counterpart for release sign-off, demos, and catching the UX bugs that only clicking finds (see
> CLAUDE.md mandate 11).

How to use: pick a **deployment** (§2), apply a **config profile** (§3), then walk the **feature
scripts** (§5) that the profile enables. Record pass/fail per step with the account/config used. A
feature that is gated OFF must be tested for its *gated* behaviour too (hidden nav or actionable notice
— never a dead control or raw error).

---

## 1. Test accounts & prerequisites

| Role | How to get it | Used for |
|---|---|---|
| **Owner** | `OWNER_EMAIL` / `OWNER_PASSWORD` (`.env` / Aspire params). First login forces password change. | Settings, deployment tuning, white-label, users |
| **Registered user** | `/register` (needs Registration feature ON) or created via `POST /api/provision` | Non-owner feature scope, RBAC checks |
| **cTrader Open API creds** | cTrader partner app (ClientId/Secret) + a demo trading account | Copy trading, prop-firm live, account link |
| **AI provider** | Optional real key; otherwise built-in ONNX / FakeLocalLlm | AI features |
| **FRED / BLS keys** | Free API keys | Economic calendar value series |

Standing tools: Docker Desktop, .NET 10 SDK, `kubectl`+`helm`+`kind`, two browsers (desktop Chrome/Edge
+ a phone or DevTools 360px viewport for mobile-first checks).

---

## 2. Deployment matrix — how to stand each one up

Test the app in **each** deployment at least once per release; features behave identically but wiring,
nodes, and scaling differ.

### 2.1 Local — .NET Aspire (dev)
```bash
dotnet run --project src/AppHost
```
Orchestrates Postgres, Web, MCP, pgAdmin; opens the Aspire dashboard. Set `OwnerEmail`/`OwnerPassword`
as Aspire params. Best for hacking + watching logs/traces live.
**Check:** all resources green in dashboard; Web reachable; MCP reachable; pgAdmin opens.

### 2.2 Local — Docker Compose (no SDK)
```bash
cp .env.example .env     # set PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```
Web → http://localhost:8080, MCP → http://localhost:8081/mcp. Web mounts the Docker socket so the
in-browser **builder** and seeded **LocalNode** build/run cTrader Console containers.
**Check:** owner login + forced password change; build a cBot (needs socket); `down` keeps data, `down -v` wipes.

### 2.3 Kubernetes — Helm on kind (in-cluster)
```bash
scripts/k8s-e2e.sh        # builds 3 images, kind load, helm install, runs tests-job
```
Or manual per `website/docs/deployment/kubernetes.md`: build `Dockerfile.web|mcp|node-agent`, `kind load`,
`helm install` from `deploy/helm/cmind`.
**Check:** all pods `Ready`; node agent **self-registers** (headless per-pod DNS) → appears in `/nodes`;
`/health`+`/version` = 200; scale a node agent to 0 → it is auto-marked unreachable in the UI; scale
Web to 2 → copy/prop-firm leases don't double-execute.

### 2.4 Cloud — AWS / Azure
Follow `website/docs/deployment/cloud-aws.md` / `cloud-azure.md`. Verify the observability sidecar path
(X-Ray/CloudWatch on AWS, App Insights on Azure) emits traces with `trace_id`/`span_id` log correlation.
**Check:** managed Postgres connection; secrets from the cloud store; scaling (§`scaling.md`) — multiple
Web replicas share leases correctly.

### Deployment sign-off table
| Deployment | Owner login | Build cBot | Backtest | Node self-register | Copy lease | Observability |
|---|---|---|---|---|---|---|
| Aspire | | | | n/a (local) | | dashboard |
| Compose | | | | LocalNode | | — |
| kind/Helm | | | | ✅ agent | scale-2 | tests-job |
| AWS | | | | | | X-Ray |
| Azure | | | | | | App Insights |

---

## 3. Config profiles — what toggles what

Every feature is gated by a `Features.*` flag and/or a secret. Test each profile; the **gated-off**
path is a first-class test (hidden nav or actionable notice, disabled button + tooltip — never a silent
no-op or raw error).

| Profile | Config set | Expect ON | Expect gated |
|---|---|---|---|
| **P0 Bare** | No AI key, no Open API, no FRED/BLS, Registration off | Build/backtest, dashboard, MCP, journal, quant (deterministic) | AI notice, copy hidden/notice, calendar keyless-only, no `/register` |
| **P1 AI** | Built-in ONNX (default) **or** `AI_E2E_BASEURL` / real key | All `/ai/*`, agent, alerts, prop-guard, quant AI, MCP AI tools | — |
| **P2 Trading** | `App:OpenApi` creds + demo account linked; `App:Copy:Enabled`, `App:PropFirm:Enabled` | Account link, copy trading live, prop-firm live tracking | — |
| **P3 Calendar** | `App:Calendar` FRED+BLS keys; `WebhooksEnabled` | Full calendar values, series browser, alerts, webhooks | keyless = schedule-only |
| **P4 Registration** | `App:Registration` on + feature flag on | `/register`, email verify (if SMTP) | manual `MustChangePassword` if no SMTP |
| **P5 White-label** | Branding/Features overrides via `/settings/deployment` | Rebrand, feature subsets, `NodesUi` modes | disabled features hidden everywhere |
| **P6 Shared Open API** | `App:OpenApi:SharedApp` creds | Single app; per-user app option disappears | — |

Feature flags (`FeaturesOptions`): Authoring, Backtesting, Execution, CopyTrading, Ai, PortfolioAgent,
AgentStudio, Alerts, PropGuard, PropFirm, Accounts, OpenApi, Mcp, Compliance, EconomicCalendar,
Registration. **For each flag: flip OFF → confirm nav item gone + direct-URL route gated (not a crash);
flip ON → feature works.** (`GatingParityTests`/`RouteExistenceTests` cover this automatically; verify by hand too.)

Secret-driven gates: AI (`Ai.ApiKey` / provider / built-in), Copy (`Copy.Enabled` + Open API token),
PropFirm (`PropFirm.Enabled` + account), Calendar values (FRED/BLS), Registration email (`Email`),
Discovery (`Discovery.Enabled` + `JoinToken`), Alerts/PropGuard/Agent background workers (`*.Enabled`).

---

## 4. Cross-cutting checks (run once per deployment, on every page)

- **Mobile-first:** 360px viewport, no horizontal scroll 320–1920px. Every page.
- **Dialogs:** every add/create/edit opens a MudBlazor **dialog**, never an inline page form.
- **Localization:** switch culture via `/set-culture`; UI text translates; pick an **RTL** locale
  (Arabic/Hebrew/Farsi/Urdu) → `<html dir=rtl>` + layout mirrors. No raw literal strings.
- **No raw GUIDs:** user-facing columns show account **number**/name, never a strong-ID Guid.
- **Lifecycle controls:** per-row start/stop/delete/kill/pause are **icon buttons**, disabled in
  invalid states (Stop on terminal, Delete on active, Start on running).
- **Detail pages never crash the circuit:** open detail/view for terminal/failed/missing/gated entity → renders.
- **PWA:** installable, offline shell, manifest/icons (`website/docs/features/pwa.md`).
- **2FA:** if enrolled, login routes through `/login/2fa`.
- **Auth/RBAC:** non-owner cannot reach owner-only settings/users; lockout after `LockoutThreshold` (5) bad logins.

---

## 5. Feature test scripts (grouped by nav)

Each script: **precondition (config profile)** → **steps** → **expect** → **gated-off expect**.
Route + canonical doc noted per feature (`website/docs/features/<x>.md`).

### 5.1 Dashboard — `/` (`dashboard.md`)
- Pre: P0. Steps: log in → land on dashboard. Expect: live KPI tiles, chart, ring, activity feed
  populate (enhanced-poll). Powered-by docs link works. Gated: empty state renders, no crash with zero data.

### 5.2 Build & Backtest (`build-and-backtest.md`)
- **Authoring** `/cbots`, `/builder/{id}`, `/ai/build`: create a cBot (C# and Python). Build runs in a
  throwaway SDK container on the **web host** (Docker socket). Expect: build log streams; success/fail status.
- **Run** `/run`, **Backtest** `/backtest`: pick cBot + params + account; dispatch to a node. Backtest
  needs `--data-mode`, dd/MM/yyyy dates, `params.cbotset` JSON. Expect: instance created → running →
  terminal; equity result on backtest. Gated (Backtesting off): nav hidden / notice.
- **Instance detail** `/instance/{id}`: open for a **running** and a **terminal/failed** instance → both
  render; log tail streams (running) / final log (terminal); Stop disabled on terminal.
- **Optimize** `/optimize`, `/ai/tune`: note — optimization unsupported by cTrader Console (deliberately
  not done); page shows the correct not-available state / AI-tune suggestions only.

### 5.3 Nodes — `/nodes` (`node-discovery.md`, ops)
- Pre: kind deployment (§2.3) or LocalNode (compose). Steps: view nodes; agent self-registers by name
  (stable across IP). Scale agent to 0 → marked unreachable. Expect: stats poll every 15s. White-label
  `NodesUi` = Full/Monitor/Hidden gates nav+page+endpoints; `RestrictNodesToOwner`. Gated: Hidden → gone.

### 5.4 Accounts — `/accounts`, `/account` (`open-api-shared-app.md`, `token-lifecycle.md`)
- Pre: P2 (Open API creds). Steps: link a cTrader account via OAuth (cookie-state); demo + live host.
  Expect: account shows **number**/name (no Guid); token auto-refresh (3-day threshold); re-auth prompt
  on repeated refresh failure inside critical window. Shared-app (P6): per-user app option hidden. Gated
  (Accounts/OpenApi off): section hidden / notice.

### 5.5 Copy Trading — `/copy-trading`, `/copy-trading/{id}` (`copy-trading.md` + subdocs)
- Pre: P2, `Copy.Enabled`, ≥2 linked accounts. Steps: create a copy profile (source→destination),
  dialog-based. Start it → mirrors positions. Test failure paths: source position open during a
  **Critical calendar blackout** with `NewsPauseEnabled` → skipped; partial fill true-up; token
  rotation; **node death → lease reclaim** (scale to 2, kill one). Subfeatures to click through:
  - Execution transparency (`copy-execution-transparency.md`) — needs `TransparencyEnabled`; per-copy latency/slippage log.
  - Notifications (`copy-notifications.md`) — `NotificationsEnabled` (on by default); safety feed populates on breach/flatten.
  - Performance fees (`copy-performance-fees.md`) — `FeesEnabled`; high-water-mark accrual settles.
  - Provider marketplace (`copy-provider-marketplace.md`); AI copy recommender `/ai/*` (`ai-copy-recommender.md`).
- Expect: lifecycle icon buttons disabled in invalid states. Gated (CopyTrading off / no token): hidden or actionable notice.

### 5.6 Prop Firm — `/prop-firm`, `/prop-guard` (`prop-firm.md`, `compliance.md`)
- Pre: P2 + `PropFirm.Enabled` + account. Steps: create a challenge (all rule types); tracker claims a
  self-healing lease, recomputes equity every 5s over Open API. Cross drawdown warn threshold (80%) →
  alert. **Prop-guard** `/prop-guard`: AI risk guard (`RiskGuardEnabled`/`AutoStop`). Gated: manual-equity
  path works without live tracking; feature-off hidden/notice. Compliance page renders.

### 5.7 AI features — `/ai/*` (`ai.md` + per-feature docs)
Pre: P1 (built-in ONNX default, or real key / `AI_E2E_BASEURL`). Each renders AI output (canned reply on fake):
- `/ai/build` — cBot build assist · `/ai/review` — code review · `/ai/optimize`, `/ai/tune` — tuning
- `/ai/debate` — strategy debate · `/ai/digest` — digest · `/ai/sentiment` — sentiment
- `/ai/exposure` — exposure · `/ai/currency-strength` — see 5.11
- **Agent** `/agent` (`PortfolioAgent`, `Agent.Enabled`) — proposals per cycle (30 min).
- **Agent Studio** `/agent-studio` (`agent-studio.md`) — build/configure agents.
- **Alerts** `/alerts` (`Alerts.Enabled`, 5-min poll) — create alert rule → fires.
- Gated (AI off / no key): every AI page shows the **not-configured** notice, `AiResult.Fail`, app otherwise unchanged.

### 5.8 Quant / Institutional-edge — `/quant/*` (`institutional-edge*`, per-feature docs)
Pre: P0 deterministic core works; P1 adds AI narration. Walk each:
- `/quant/integrity` — backtest integrity (`backtest-integrity.md`)
- `/quant/regimes` — regime lab (`regime-lab.md`)
- `/quant/health` — strategy health (`strategy-health.md`)
- `/quant/positioning` — contrarian positioning (`contrarian-positioning.md`)
- `/quant/sizing` — position sizing (`position-sizing.md`)
- `/quant/tca`, `/quant/execution` — TCA / execution (`execution-tca.md`)
Expect: deterministic figures render without AI; AI narrative when P1. Gated: degrade gracefully.

### 5.9 Economic Calendar — `/economic-calendar`, `/economic-calendar/series/{code}` (`economic-calendar.md`)
Pre: P0 = keyless central-bank **schedule** only; P3 = FRED/BLS **values**. Steps: browse calendar;
open a series detail; set an alert. Blackout gate: `BlackoutFailClosed` (fail-closed default). Webhooks
(`WebhooksEnabled`) POST released events. cBot Calendar API (`calendar-cbot-api.md`) — JWT `market:read`,
15-min token. Gated (EconomicCalendar off): worker doesn't run, nav hidden/notice; missing FRED key →
value source degrades, schedule still works (no raw error).

### 5.10 Currency Strength — `/ai/currency-strength` (`currency-strength.md`)
Pre: P1+P3 (AI + calendar). Steps: view macro strength + forward pair-outlook matrix; opt-in dashboard
widget. Refresh worker every 6h (leased). Gated: degrades to no snapshot when AI/calendar absent — notice, no crash.

### 5.11 Trading Journal — `/journal` (`trading-journal.md`)
Pre: P0. Steps: view/add journal entries (dialog). Expect: renders with zero entries; no Guid columns.

### 5.12 MCP server — `/mcp` (`mcp.md`)
Pre: `Mcp` on. Steps: connect an MCP client to `:8081/mcp` (SSE+HTTP); list tools; call a read tool;
call an **AI tool** (P1) → returns via fake/real LLM. `/mcp` page documents tools. Gated (Mcp off): hidden.

### 5.13 Users & Registration — `/users`, `/register` (`user-registration.md`)
- Owner `/users`: list/manage users (dialog); non-owner blocked.
- `/register` (P4): self-serve register → `POST /api/provision`; email verify if `Email` (SMTP) set,
  else `MustChangePassword` manual path. Gated (Registration off): `/register` 404/gated, nav hidden.

### 5.14 Two-Factor Auth — `/login/2fa` (`two-factor-auth.md`)
Pre: any. Steps: enrol TOTP (QR), save backup codes; log out/in → `/login/2fa` challenge; use a backup
code. White-label `RequireMfa` forces enrolment. Gated: MFA optional path.

### 5.15 Settings (owner) — `/settings/*` (`white-label-owner-settings.md`, `feature-toggles.md`)
- `/settings/deployment` — tune **every** white-label option at runtime (overrides apply live via
  decorated `IOptionsMonitor`, no redeploy). Change branding → UI rebrands. Toggle a feature → nav/route
  updates immediately. (`WhiteLabelCatalogParityTests` guards catalog sync.)
- `/settings/ai` — AI provider config + encrypted runtime key; add provider, set active, built-in ONNX.
- `/settings/features` — feature toggles (`feature-toggles.md`).
- `/settings/openapi` — Open API app config (per-user or shared).
- `/settings/legal` — legal/compliance text.
Gated: non-owner cannot reach any `/settings/*`.

### 5.16 White-label / branding (`white-label.md`)
Apply a full rebrand (name, logo, colors, feature subset, `NodesUi` mode) via `/settings/deployment` **or**
`App:*` config. Expect: branding everywhere (login, PWA manifest, emails); disabled features vanish from
nav + routes; `IntentionallyExcluded` options are operational-only. Test both config-driven and owner-tuned.

---

## 6. Failure-path checklist (mandate 2 — failure paths count)

Run these deliberately; each must degrade cleanly, never crash the circuit or show a raw framework error:
- [ ] Open API **connection drop** mid-copy → resync, no double-fill.
- [ ] **Order rejection** → surfaced, profile continues.
- [ ] **Desync/resync** on reconnect.
- [ ] **Token rotation** — single valid token/cID; in-place swap.
- [ ] **Node death** → lease reclaim (copy, prop-firm, calendar, currency workers).
- [ ] **Orphaned Running instance** reconciled by pollers (self-exited container → Stopped/ Failed by exit code).
- [ ] Hung backtest force-stopped after `MaxBacktestDuration` (6h).
- [ ] Missing AI key / FRED key / account / model → gated notice, not a doomed action.
- [ ] Gated API route hit directly → 404/gated, page still loads (ErrorBoundary recovers on nav).
- [ ] Lockout after 5 bad logins; forced password change on first owner login.

---

## 7. Sign-off matrix (fill per release)

| Feature area | P0 | P1 | P2 | P3 | Compose | kind | Cloud | Mobile | RTL |
|---|---|---|---|---|---|---|---|---|---|
| Dashboard | | | | | | | | | |
| Build/Backtest | | | | | | | | | |
| Nodes | | | | | | | | | |
| Accounts | | | | | | | | | |
| Copy trading | | | | | | | | | |
| Prop firm / guard | | | | | | | | | |
| AI features | | | | | | | | | |
| Quant suite | | | | | | | | | |
| Economic calendar | | | | | | | | | |
| Currency strength | | | | | | | | | |
| Journal | | | | | | | | | |
| MCP | | | | | | | | | |
| Users/Registration | | | | | | | | | |
| 2FA | | | | | | | | | |
| Settings/White-label | | | | | | | | | |

Legend: ✅ pass · ⚠️ pass-with-note · ❌ fail (file issue) · — n/a for this profile.

---

## 8. Notes for the tester

- Prefer the **built-in ONNX / FakeLocalLlm** for AI unless validating a real provider — deterministic,
  zero external deps, wire-identical to Ollama/LM Studio/vLLM. Set `AI_E2E_BASEURL` (+ key/kind/model) for a real one.
- Missing credentials is **never** a reason to skip a feature — test the gated path instead.
- Every UX bug found by clicking that the suite missed → add an E2E test (mandate 11) before closing.
- Canonical feature docs: `website/docs/features/`. Deployment: `website/docs/deployment/`. Ops:
  `website/docs/operations/`. Prior manual-audit findings: `plans/full-feature-live-audit-findings.md`.
