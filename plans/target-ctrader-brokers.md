# Target cTrader Brokers — broker allowlist, broker-facing docs, white-label AI creds

**Goal:** make cTrader **brokers** first-class customers of a white-label cMind deployment, pitch
**cloud/VPS providers** on offering managed cMind hosting, and pitch **traders** on self-hosting cMind
— all tied together by a persona hub. Five independent deliverables:

1. **Broker allowlist.** A white-label deployment can restrict which brokers' trading accounts its
   users may add. When the allowlist is set, every account-add path (Open API OAuth link **and**
   manual cID user/pass) is checked against it; a disallowed broker is rejected with a clear
   notification. Open API already returns the broker name; manual cID accounts are **verified** by
   running a tiny sample cBot through the cTrader CLI that prints `Account.BrokerName`. When the
   allowlist is empty (default) **no verification runs and every broker is allowed** — zero behavior
   change for stock deployments.
2. **Broker-facing docs.** An eye-catching "For Brokers" section on the docs site + landing page:
   why a cTrader broker gains an edge by running a cMind white-label for its own users (AI features,
   copy trading, prop-firm challenges, etc.).
3. **White-label default AI credentials.** A deployment can ship a default AI provider key/creds so
   all its users get every AI feature with no per-user setup, while still letting a user add their
   own provider creds. Builds on `plans/multi-provider-ai.md` (the provider-agnostic AI layer).
4. **Cloud & VPS provider docs.** A separate "For Cloud & VPS Providers" docs section: why a hosting
   company should offer **managed cMind** as a product, and concrete **monetization** models — plus
   the light product hooks (one-click deploy, per-tenant provisioning, usage signals) that make it
   sellable. Docs-led; reuses existing deployment/IaC/scaling/feature-toggle surfaces.
5. **Trader docs + persona hub.** A separate "For Traders" docs section (why a cTrader trader benefits
   from **self-hosting** cMind), plus a revamped **main audience hub page** that links all three
   persona pages (Brokers, Cloud/VPS Providers, Traders) with short SEO-optimized blurbs, explains the
   benefit per persona, and points each to how they can **contribute**. Fully SEO-optimized,
   localized, professional. Docs-only.

**House bars (CLAUDE.md, non-negotiable):** strict DDD (rich aggregates, VOs, Core has zero infra
deps, invariants throw `DomainException`), three test tiers every change (unit + integration + E2E,
failure paths, missing creds ⇒ skip-clean live test, never skip a tier), zero warnings + analyzer
sweep clean, no `DateTime.UtcNow` (`TimeProvider`), no secrets/magic-strings/raw-logging, MudBlazor
dialogs + mobile-first + branded + **localized** UI (`IStringLocalizer`, add keys for all cultures),
docs in the same commit, EF migration if schema changes, modern C# 14.

---

## Current state (what the code already gives us)

- **White-label config** = `App:Branding` (`BrandingOptions`) + `App:Features` (`FeaturesOptions`,
  `FeatureFlag`). Both bound from `AppOptions`. No broker concept today.
- **Account aggregate:** `CTraderIdAccount` (aggregate root) owns `TradingAccount` children.
  - Manual cID: `CtidEndpoints` `POST /api/ctids/{id}/accounts` → `CTraderIdAccount.AddTradingAccount(
    accountNumber, broker, isLive, label)`. **The `broker` string is typed by the user — untrusted.**
    Dialog: `Components/Dialogs/NewTradingAccountDialog.razor` (free-text "Broker" field).
  - Open API: `Web/OpenApi/OpenApiAccountLinker.LinkAsync` → `CTraderIdAccount.LinkOpenApiAccount(...)`.
    **Broker name is authoritative here** — `grant.Accounts[].Broker` comes from the cTrader Open API
    (`ProtoOATrader.BrokerName`, field 16). No CLI probe needed for this path.
- **cTrader CLI run flow** (for the manual-cID probe): `Nodes/ContainerCommandHelpers.BuildConsoleArgsList`
  builds `run <algo> [params] --ctid <user> --pwd-file <file> --account <n> [--symbol --period]`.
  Dispatch via `IContainerDispatcherFactory.For(node/instance)` → `IContainerDispatcher.StartAsync` +
  `TailLogsAsync` (stream stdout) + `StopAsync`. Nodes picked by `INodeScheduler.PickNodeAsync`.
  cBots build in a sandbox SDK container on the web host (`CBotBuilder`); run containers run on nodes.
- **AI key today:** single-key `IAiKeyStore`/`AiKeyStore` — DB-stored runtime key (encrypted,
  `EncryptionPurposes.AiApiKey`) falling back to `App:Ai:ApiKey` config. Endpoints in `AiEndpoints`
  (`/api/ai/key`, Owner-only). `plans/multi-provider-ai.md` replaces this with `AiProviderCredential`
  + `AiProviderStore` (N stored, one active) — deliverable 3 extends that.
- **Docs:** Docusaurus in `website/`. Getting-started sidebar already has `white-label-for-business`.
  Landing page `website/src/pages/index.tsx`; sidebar `website/sidebars.ts`.

---

# Deliverable 1 — Broker allowlist + verification

## 1.1 Design north star

Two enforcement points, one rule:

- **Open API link path** — broker name is already known and trusted (from `ProtoOATrader.BrokerName`).
  Just check it against the allowlist before `LinkOpenApiAccount`; reject the disallowed account
  (skip it / surface an error) without any container work.
- **Manual cID path** — the user-typed broker string is **not** trustworthy, so we don't check the
  typed string. We **verify** by connecting the real credentials: run the sample probe cBot via the
  cTrader CLI, read `Account.BrokerName` from its stdout, and check *that* against the allowlist. The
  typed broker field becomes optional/ignored — the verified name is authoritative and is what we
  persist.
- **Gate:** the whole thing is **inert unless the allowlist is non-empty**. Empty allowlist ⇒ no
  Open API check, no probe container, all brokers allowed. This keeps stock/dev deployments unchanged
  and every existing test green.

Broker-name comparison is **case-insensitive, trimmed** (a VO owns the normalization + match rule).

## 1.2 Domain model (`src/Core` — DDD, zero infra deps)

- **`BrokerName` value object** (`src/Core/Accounts/BrokerName.cs`): wraps the broker string.
  Non-empty (or an explicit `Unknown`), trimmed, `MaxLength 128` to match `TradingAccount.Broker`.
  Equality by normalized (case-insensitive) value. Used across allowlist + verification result +
  account creation so there is one spelling of "broker" in the domain.
- **`BrokerAllowlist` value object** (`src/Core/Accounts/BrokerAllowlist.cs`): immutable set of
  allowed `BrokerName`. `IsRestricted => Count > 0`. `Allows(BrokerName) => !IsRestricted ||
  set.Contains(normalized)`. Built from config (below). This is the single place the rule lives —
  endpoints/services only ask `Allows(...)`.
- **Enforce the invariant on the aggregate.** `CTraderIdAccount.AddTradingAccount` and
  `LinkOpenApiAccount` take the resolved `BrokerName` and an injected/passed `BrokerAllowlist`, and
  throw `DomainException(DomainErrors.BrokerNotAllowed)` when `!allowlist.Allows(broker)`. The rule is
  a domain invariant of the account aggregate, not endpoint glue. (Callers resolve the allowlist from
  options and, for manual cID, the verified name — see 1.4/1.5.)
- **`DomainErrors.BrokerNotAllowed`** constant added to `Core/Constants/DomainErrors.cs`
  (message references the broker; UI localizes the user-facing notification separately).
- **Verification port (interface in Core, impl in Web/Nodes):**
  `src/Core/Accounts/IBrokerVerifier.cs`:
  ```csharp
  public interface IBrokerVerifier
  {
      // Runs the probe against the cID credentials + account, returns the broker name the platform
      // reports, or a typed failure. Only called when the allowlist is restricted.
      Task<BrokerVerificationResult> VerifyBrokerAsync(
          BrokerProbeRequest request, CancellationToken ct);
  }
  ```
  `BrokerProbeRequest` (VO/record): ctid username, decrypted password (or a protected handle),
  account number, live/demo. `BrokerVerificationResult`: `Success`, `BrokerName?`, `Error?`
  (typed reasons: `LoginFailed`, `Timeout`, `NoNodeAvailable`, `ProbeFailed`). Pure Core contract;
  no HttpClient/Docker here.

## 1.3 White-label config for the allowlist

Add to white-label config a broker allowlist. **Recommended:** a new `AccountsOptions` record on
`AppOptions` (keeps `BrandingOptions` about visuals), bound from `App:Accounts`:
```jsonc
"App": { "Accounts": { "AllowedBrokers": ["Pepperstone", "IC Markets"] } }
```
- `AccountsOptions.AllowedBrokers : IReadOnlyList<string>` (default `[]`).
- A small factory (`BrokerAllowlist.FromOptions` or a validator) maps the strings → `BrokerAllowlist`
  of `BrokerName`, trimming/deduping. Startup validation rejects blank/whitespace entries (fail-fast,
  same spirit as `BrandingOptionsValidator`).
- Env form: `App__Accounts__AllowedBrokers__0=Pepperstone`.

> Alternative considered: put `AllowedBrokers` on `BrandingOptions`. Rejected — branding is visuals;
> account policy is its own concern and pairs naturally with future account rules. Flag if you prefer
> it on Branding to keep all white-label knobs in one section.

## 1.4 Open API path enforcement (`OpenApiAccountLinker`)

- Inject the allowlist (`IOptionsMonitor<AppOptions>` → `BrokerAllowlist`) into `OpenApiAccountLinker`.
- In the per-account loop: resolve `BrokerName` from `account.Broker` (already trusted). If
  `allowlist.IsRestricted && !allowlist.Allows(broker)` → **skip that account** and record it in a
  returned result so the OAuth callback can surface "N accounts skipped: broker X not allowed". Allowed
  accounts link as today. (No exception mid-loop that would abort the whole grant — one disallowed
  account must not block allowed ones.)
- The OAuth callback endpoint returns/passes the skipped-broker summary so the UI shows a notification.

## 1.5 Manual cID path enforcement + the probe

Flow when a user adds an account under a cID (`POST /api/ctids/{id}/accounts`) **and** the allowlist is
restricted:
1. Endpoint loads the cID, decrypts the password (`ISecretProtector`, `EncryptionPurposes.CtidPassword`).
2. Calls `IBrokerVerifier.VerifyBrokerAsync(new BrokerProbeRequest(username, password, accountNumber,
   isLive), ct)`.
3. On `Success`: call `cid.AddTradingAccount(accountNumber, verifiedBrokerName, isLive, label,
   allowlist)` — the **verified** name is persisted (typed field ignored), and the aggregate invariant
   double-checks `Allows`. On disallowed → `DomainException(BrokerNotAllowed)` → endpoint maps to a
   `400/409` problem with the broker name → UI notification "Broker X accounts are not allowed".
4. On verification `Error`: endpoint returns a typed problem (`login failed` / `verification timed out,
   try again` / `no node available`) → UI notification. **Do not** add the account.
5. When the allowlist is **empty**: skip steps 1–4 entirely; add the account as today (any broker).

### The probe cBot (sample algo shipped with the app)

- **Source file** committed under `src/Web/Resources/BrokerProbe/` (e.g. `BrokerProbeBot.cs`, a
  minimal cTrader `Robot`): on `OnStart`, print a delimited line and stop, e.g.
  `Print($"##BROKER##{Account.BrokerName}##");` then `Stop();`. Keep it trivial and offline-safe.
  Ship the **built `.algo`** too, or build it once via `CBotBuilder` at first use and cache it
  (decide in Open questions — pre-built `.algo` avoids a build dependency on the probe path).
- **`BrokerVerifier` implementation** (`src/Web/Accounts/BrokerVerifier.cs` or `src/Nodes/...`):
  - Pick a node (`INodeScheduler.PickNodeAsync("Run")`); if none → `NoNodeAvailable`.
  - Materialize a throwaway **probe run instance** (reuse the `RunInstance` machinery, or a dedicated
    lightweight probe path that calls `IContainerDispatcher.StartAsync` with the probe `.algo`, the
    cID creds via `--ctid/--pwd-file`, and `--account`). No symbol/period needed.
  - `TailLogsAsync` until the `##BROKER##...##` line appears or a **bounded timeout**
    (`AppOptions.Accounts.BrokerProbeTimeout`, default ~60s) elapses; parse the broker name.
  - `StopAsync` + ensure teardown (finally) so no probe container leaks. Never `DateTime.UtcNow` —
    use injected `TimeProvider` for the deadline.
  - Map outcomes → `BrokerVerificationResult`. Log via source-generated `LogMessages` (no secrets in
    logs — never log the password; broker name + account number are fine).
  - New CLI/const strings (`##BROKER##` marker, resource path) live in `Core/Constants` / a probe
    constants group — no magic strings.
- **Security:** the probe uses the same sandbox/JWT node model as normal runs; password flows only
  as the existing encrypted `--pwd-file`, never inlined into args or logs.

## 1.6 UI (MudBlazor dialog, mobile-first, branded, localized)

- `NewTradingAccountDialog.razor`: when the deployment is restricted, either (a) turn the free-text
  Broker field into a **read-only "verified on add"** hint, or (b) keep it but label it advisory. On
  submit, the page calls the endpoint; a `BrokerNotAllowed`/verification error surfaces via
  `ISnackbar` with a localized message ("Accounts from {broker} aren't allowed here." / "Couldn't
  verify the broker — check the credentials and try again."). Add a `<HelpTip>` explaining the policy.
- Open API OAuth return page: show a localized notification listing skipped brokers.
- All new strings → `tools/i18n/ui-translations.json` for **every** culture in `SupportedCultures`,
  then `pwsh tools/i18n/gen-resx.ps1` (`ResourceParityTests` + `NoHardcodedUiTextTests` enforce this).

## 1.7 Tests (all three tiers — Deliverable 1)

- **Unit:**
  - `BrokerName` VO (normalize/equality/guard), `BrokerAllowlist` (`IsRestricted`, `Allows`
    case/whitespace, empty ⇒ allows all).
  - `CTraderIdAccount.AddTradingAccount`/`LinkOpenApiAccount` throw `BrokerNotAllowed` when restricted
    + disallowed; allow when unrestricted or allowed. Verified-name-wins on manual path.
  - `AccountsOptions` → `BrokerAllowlist` mapping (trim/dedupe/blank rejection).
  - `BrokerVerifier` with a **fake `IContainerDispatcher`** whose `TailLogsAsync` yields a canned
    `##BROKER##Pepperstone##` line → asserts parse; login-fail / timeout / no-node → typed failures.
- **Integration (Testcontainers PG):**
  - Manual-cID endpoint: restricted allowlist + fake `IBrokerVerifier` returning allowed/disallowed →
    account persisted / rejected with the right status; unrestricted → persisted with any broker.
  - `OpenApiAccountLinker` with restricted allowlist skips disallowed grant accounts, links allowed
    ones, returns the skipped summary.
- **E2E (Playwright, mobile + desktop):**
  - Deployment **without** allowlist: add-account dialog works for any broker (existing behavior).
  - Deployment **with** allowlist (fixture sets `App:Accounts:AllowedBrokers`) + a **fake/seam**
    `IBrokerVerifier` (no live broker in CI): adding an allowed broker succeeds; a disallowed one
    shows the rejection notification and no account row appears.
  - **Live probe test that skips cleanly when creds absent** (the `CopyLive`/onboarding pattern):
    when real cID creds are provided, run the actual probe end-to-end and assert the real
    `Account.BrokerName` is read. Skipped (not failed) without creds — never left unwritten.
  - New route/dialog states covered in `PageSmokeTests`/`MobileLayoutTests` as applicable.

---

# Deliverable 2 — "For Brokers" docs (eye-catching)

Goal: a broker landing/marketing surface explaining the edge of running a cMind white-label for their
own users.

- **New docs page** `website/docs/for-brokers.md` (slug `/for-brokers`), added to `sidebars.ts`
  Getting-started category next to `white-label-for-business`. Eye-catching, benefit-led:
  - *Why a cTrader broker should run cMind:* differentiate vs other brokers — give your users AI cBot
    generation/review/optimization, copy trading + provider marketplace + performance fees, prop-firm
    challenges with live equity tracking, backtesting on a managed cluster, a branded installable PWA
    — all under **your** brand, your domain, your colors.
  - *Retention & stickiness:* users build/run strategies inside your platform.
  - *New revenue:* performance fees, prop-firm challenge fees, premium AI.
  - *Broker allowlist tie-in:* restrict accounts to **your** brokerage so the deployment only serves
    your book (link to the white-label + allowlist reference). Show the `App:Accounts:AllowedBrokers`
    snippet.
  - *AI with zero user friction:* deployment-provided AI key (Deliverable 3) means users get AI
    features immediately, no signup elsewhere.
  - Strong CTA linking to `white-label-for-business`, `features/white-label`, `features/ai`,
    `features/copy-trading`, `features/prop-firm`.
- **Landing page** (`website/src/pages/index.tsx`): add a compact "For brokers" highlight
  card/section linking to the new page (mirrors the existing audience/white-label callouts). Keep it
  visually consistent with brand tokens in `website/src/css/custom.css`.
- **Cross-link** from `white-label-for-business.md` ("Are you a broker? →") and mention the allowlist.
- `npm run build` must report **zero broken links** before PR; update `sidebars.ts` id.
- (Docs-only; no unit/integration/E2E, but the broker-allowlist doc content ships in the same commit
  as Deliverable 1 per the "docs in the same commit" mandate.)

---

# Deliverable 3 — White-label default AI credentials

Goal: a deployment ships default AI provider creds so **all its users** use every AI feature with no
setup, while a user may still add **their own** provider creds.

**This rides on `plans/multi-provider-ai.md`** (provider-agnostic `AiProviderCredential` +
`AiProviderStore`, N stored / one active). Two ways to frame it — pick per that plan's state:

- **If multi-provider is implemented:** the deployment-seeded providers (`App:Ai:Providers[]`,
  imported into the store on startup) already give a "default provider for everyone." This deliverable
  = make that seed the **shared default** and let a user layer a **personal** provider on top.
- **If shipping standalone first (Anthropic-only today):** extend the existing key model minimally
  (below), then fold into multi-provider later.

## 3.1 Two scopes of AI credential: deployment-default vs per-user

- **Deployment default (white-label):** `App:Ai:ApiKey` (+ model/base URL, or `App:Ai:Providers[]`
  once multi-provider lands). Already the fallback in `AiKeyStore.CurrentKey`. Semantics to make
  explicit: when a deployment default is present, **every user** can use all AI features against it
  with **no per-user config and no per-user limit** (matching the task). This is the broker's own
  key funding its users' AI usage.
- **Per-user override (new):** a user may store **their own** provider creds; when present, their AI
  features use *their* creds instead of the deployment default. Modeled as an **owner/user-scoped
  `AiProviderCredential`** (per the multi-provider aggregate, which is currently owner-scoped —
  broaden its scope key to `UserId` for per-user, keeping a deployment/global scope for the default).

### Resolution order (single rule, in the store/routing client)
`effective creds = user's active personal credential (if any) → deployment default provider (if any)
→ none (feature returns AiResult.Fail)`. Implement in `AiProviderStore.ActiveCredentialFor(userId)`
(or, pre-multi-provider, extend `AiKeyStore` with a per-user lookup that falls back to config/global).

**Degrade-not-break stays law:** no personal creds and no deployment default ⇒ every AI feature
returns `AiResult.Fail`, app runs unchanged (existing gate).

## 3.2 Domain / store changes

- **Multi-provider aggregate** `AiProviderCredential`: add an **owner scope** dimension — a `Scope`
  (`Deployment` vs `User`) + optional `UserId`, or two query paths in the store. Invariant "one active
  per scope." Deployment-scope credential is managed by Owner (or seeded from config); user-scope by
  the user themselves.
- Encryption unchanged (`ISecretProtector`, `EncryptionPurposes.AiApiKey`/`AiProviderKey`).
- Config seeding: deployment default imported on startup if absent (idempotent) — a broker sets it in
  appsettings/env and never touches the UI.

## 3.3 Web surface (dialogs, mobile-first, branded, localized)

- **Settings → AI:** show whether AI is powered by the **deployment default** ("AI is provided by
  {ProductName} — no setup needed") vs the user's **own** creds. Let a user **add their own provider**
  via the existing/new provider dialog (from multi-provider P5), and **revert to the default**
  (delete personal creds).
- Owner-only management of the deployment-default provider stays under the existing Owner-gated
  endpoints (`/api/ai/key` today → `/api/ai/providers` in multi-provider).
- `AiFeatureNotice`/gate reads the **effective** status for the current user (personal-or-default).
- Localize all new strings across every culture; add the route/dialog to `PageSmokeTests`.

## 3.4 Tests (all three tiers — Deliverable 3)

- **Unit:** resolution order (user creds win over default; default used when no personal; none ⇒
  fail). Scope invariant on the aggregate. `AiProviderStore.ActiveCredentialFor(userId)` selection.
- **Integration (PG):** seed deployment default → user with no personal creds resolves to default;
  user adds personal creds → resolves to personal; user deletes → back to default. Encrypted
  round-trip per scope.
- **E2E:** deployment-default fixture (fake AI endpoint from multi-provider's `FakeLocalLlmServer`):
  a fresh user runs an AI feature (e.g. Review) with **no setup** and gets output; then adds personal
  creds and still works; keyless deployment (no default) shows the disabled notice. Skip-clean live
  Anthropic/provider test when real key absent.

---

# Deliverable 4 — Cloud & VPS providers (managed-service docs + monetization)

Goal: convince a **cloud/VPS/hosting provider** (DigitalOcean-style VPS shops, regional clouds,
managed-hosting resellers, MSPs) to offer **managed cMind** to their customers, and show exactly how
to make money doing it. cMind is already built for this: stateless Web/MCP tier scales on any
container platform, Postgres is a managed DB, node agents run on VMs/K8s, branding + feature toggles
are pure config, and self-serve registration/provisioning already exists. This deliverable packages
that into a provider-facing pitch + the few product hooks that make it turnkey.

## 4.1 Docs (primary deliverable — eye-catching, benefit + money led)

- **New docs page** `website/docs/for-cloud-providers.md` (slug `/for-cloud-providers`), added to the
  Getting-started category in `sidebars.ts` alongside `for-brokers` and `white-label-for-business`.
  Structure:
  - *Why offer managed cMind:* a ready-made, differentiated product for the algo-trading/prop-firm
    niche — no build cost, open source, .NET/Postgres/containers you already host. Land a high-value,
    sticky, compute-hungry workload (backtests + live nodes burn CPU = billable usage).
  - *Who buys it from you:* brokers, prop firms, copy-trading businesses, trading communities, and
    individual quants who want it hosted (the "not for you" self-host caveat in `audience.md` becomes
    *your* upsell — "don't want to be the ops team? your provider runs it").
  - *What "managed cMind" means:* you run the stateless tier + Postgres + node fleet; the customer
    gets a branded URL. Point to the real deployment guides so it's obviously achievable:
    [cloud](./deployment/cloud.md), [Azure](./deployment/cloud-azure.md),
    [AWS](./deployment/cloud-aws.md), [Kubernetes](./deployment/kubernetes.md),
    [scaling](./deployment/scaling.md). Call out the **privileged-Docker node** constraint up front
    (VMs/K8s, not serverless) so providers scope it right.
  - **Monetization models (the core of the page):**
    - *Managed hosting subscription* — monthly per-deployment plans (Starter/Team/Business) sized by
      node fleet + backtest concurrency + DB tier.
    - *Usage/compute metering* — bill backtest-hours / live-node-hours / storage; naturally metered by
      the container fleet you already run.
    - *White-label reseller tiers* — charge more for full rebrand (logo/colors/PWA/`ShowSiteLink=false`)
      and for enabling premium features via [feature toggles](./features/feature-toggles.md).
    - *Managed AI* — bundle a provider key (Deliverable 3) and mark up AI usage, or offer BYO-key.
    - *Prop-firm / copy-trading revenue share* — host firms running challenges + performance fees
      ([prop-firm](./features/prop-firm.md), [performance fees](./features/copy-performance-fees.md),
      [marketplace](./features/copy-provider-marketplace.md)) and take a platform cut.
    - *Setup / onboarding / support / SLA* — professional-services and premium-support attach.
  - *Multi-tenant patterns:* deployment-per-tenant (strong isolation, per-tenant branding/DB — the
    recommended model, matches the app's `IOptionsMonitor` per-instance branding) vs shared control
    plane driving many instances. Cross-link `white-label-for-business.md` (per-customer branding) and
    [node-discovery](./operations/node-discovery.md) (join-token per tenant).
  - *Operational cost levers:* scale nodes to demand, share Postgres tiers, autoscale the stateless
    tier; point at [scaling](./deployment/scaling.md) + [logging](./operations/logging.md) +
    [backup/recovery](./operations/backup-recovery.md) so margins are predictable.
  - Strong CTA → deployment guides + `white-label-for-business` + `for-brokers`.
- **Landing page** (`website/src/pages/index.tsx`): add a "For cloud & VPS providers" highlight
  card next to the "For brokers" one (same brand tokens), linking to the new page.
- **Cross-links:** add a "🖥️ Cloud & VPS providers" audience block to `audience.md`; a "Want it
  hosted? Providers can run it for you →" note where the self-host caveat lives; and a mention from
  `white-label-for-business.md`.
- `npm run build` = **zero broken links** before PR; update `sidebars.ts` id.

## 4.2 Product hooks that make managed hosting turnkey (optional, phased after docs)

These lower the friction the docs promise; each is small and reuses existing surfaces. Ship docs
first (they stand alone), then add hooks as follow-ups.

- **One-click / templated deploy:** publish a provider-oriented compose/IaC bundle (extend the
  existing Bicep/Terraform/Helm) parameterized by tenant branding + join token + DB — so a provider
  spins a new managed instance from a template. Doc: a "provision a new tenant" runbook.
- **Tenant provisioning API/CLI:** the self-serve `POST /api/provision` + `App:Registration` already
  exist (owner/registration). Document (and, if needed, extend) a provider-facing provisioning path to
  create an owner + seed branding/features for a fresh tenant deployment programmatically.
- **Usage signals for metering:** expose the data a provider needs to bill — backtest-hours,
  live-node-hours, active users, storage. Likely a read-only owner/admin **usage endpoint** sourced
  from existing `Instance`/`Node`/`NodeStats`/audit data (no new domain, just a projection). This is
  the one piece that may touch code (Web endpoint + read model + tests all tiers); keep it a **later
  phase**, docs don't depend on it.
- **Health/SLA surface:** existing probes + [logging](./operations/logging.md) +
  observability already support SLA monitoring — document how a provider wires alerting.

## 4.3 Tests

- **Docs-only for 4.1:** no unit/integration/E2E; `npm run build` link-check gates it.
- **If the usage-metering endpoint (4.2) is built:** full three tiers — unit (projection math on
  seeded instances/nodes with `FakeTimeProvider`), integration (endpoint against Postgres with seeded
  data, owner-gated 403 for non-owners), E2E (owner views a usage/metering page or the endpoint
  returns expected shape), plus the route in `PageSmokeTests`. Localize any new UI strings.

---

# Deliverable 5 — Trader docs + persona hub

Goal: (a) a dedicated "For Traders" page selling **self-hosted** cMind to an individual cTrader
trader, and (b) a single **audience hub** that unifies all three personas — Brokers, Cloud/VPS
Providers, Traders — with SEO-optimized blurbs, per-persona benefit, and a contribution path. This is
the navigational + SEO capstone over Deliverables 2 and 4.

## 5.1 "For Traders" page (`website/docs/for-traders.md`, slug `/for-traders`)

Benefit-led, honest, developer-tone (matches `audience.md` voice). Why a cTrader trader self-hosts:
- *Own your stack & data:* your strategies, credentials, and equity history live on **your** box — no
  third party, no lock-in. Open source, C# 14 / .NET 10, hackable.
- *One console, no tab-juggling:* author (Monaco IDE, C#+Python), backtest across a fleet, run live,
  monitor — same app, real-time equity/logs. Link [build-and-backtest](./features/build-and-backtest.md),
  [copy-trading](./features/copy-trading.md), [dashboard](./features/dashboard.md).
- *AI on your terms:* bring your own AI key (any provider per multi-provider), generate/review/optimize
  cBots, post-mortems — no per-feature paywall. Link [ai](./features/ai.md).
- *Institutional-grade tooling for one:* backtest integrity, position sizing, strategy health, regime
  lab, execution TCA, trading journal, agent studio — link the institutional-edge feature set.
- *Runs where you do:* laptop, a cheap VPS, or a home server; installable **PWA** for phone. Link
  [run locally](./deployment/local.md) + [PWA](./features/pwa.md). "Don't want to run it yourself? A
  provider can host it →" cross-link to `for-cloud-providers`.
- *MCP:* drive it from your own AI client. Link [mcp](./features/mcp.md).
- *How you can contribute:* it's open source — file issues, add cBot templates, improve docs, send
  PRs (three test tiers, strict DDD). Link [contributing](./contributing.md).

## 5.2 Persona hub (revamp `audience.md` → the main audience page)

Repurpose the existing `audience.md` ("Who is cMind for?") into the **hub** that routes each visitor
to their persona page. It already has three persona blocks (traders, quant devs, prop firms) — extend
to a clean three-card layout mapping to the new pages:
- **📈 Traders** → short SEO blurb + benefit sentence → `for-traders.md`.
- **🏢 Brokers** → short blurb ("run a white-label for your own users; AI + copy + prop-firm edge") →
  `for-brokers.md`.
- **🖥️ Cloud & VPS Providers** → short blurb ("offer managed cMind, monetize hosting") →
  `for-cloud-providers.md`.
- Keep the **quant-developer / open-source** angle as a fourth "Contributors" block → `contributing.md`
  (satisfies "how they can contribute to improve it"): report bugs, add templates/providers/locales,
  send PRs under the DDD + three-tier bars.
- Each card: one-line value prop + 2–4 word CTA link. Mobile-first (cards stack), brand tokens.
- Decide slug: keep `audience.md` as `/audience` (existing inbound links + sidebar id stay valid) and
  make it the hub, rather than adding a second competing page. Update its `description` for SEO.

## 5.3 SEO + localization (applies to all new docs pages — Deliverables 2, 4, 5)

- **Front matter per page:** unique `title`, keyword-rich `description` (≤160 chars), stable `slug`.
  Add `keywords` array where the theme supports it (cTrader, algorithmic trading, white-label, prop
  firm, managed hosting, self-hosted, copy trading, AI trading bots). One `<h1>` per page (the title).
- **Internal linking:** hub ↔ persona pages ↔ relevant feature/deployment docs (topic cluster — hub
  is the pillar, persona pages are spokes). No orphan pages; every new page reachable from the sidebar
  **and** the hub.
- **Docusaurus SEO already configured** (`docusaurus.config.ts` — canonical URL, OG/Twitter, sitemap);
  new pages inherit it. Verify OG image/description resolve; add per-page `image` front matter if a
  persona-specific social card is wanted.
- **Localization:** docs are authored in English; the site runs **Docusaurus i18n** (per
  `localization-i18n-shipped`). Add the three new pages + hub edits to the default locale; translated
  locales pick them up via the standard `i18n/<locale>/docusaurus-plugin-content-docs` workflow
  (translations can follow — content ships in the same commit as English). Keep slugs stable across
  locales for clean localized URLs.
- **Landing page** (`website/src/pages/index.tsx`): the three persona cards (Brokers, Providers,
  Traders) point at the three pages; ensure the hub is linked from the nav/footer.
- `npm run build` = **zero broken links** and a valid sitemap before PR.

## 5.4 Tests

- Docs-only: `npm run build` link-check + sitemap gate it. No unit/integration/E2E. (No app routes
  added — these are Docusaurus pages, not Blazor pages, so `PageSmokeTests` is not involved.)

---

## Phasing (each phase = green build + its tests + docs in the same commit)

- **P1 — Broker allowlist domain + Open API path.** `BrokerName`, `BrokerAllowlist`, `AccountsOptions`,
  `DomainErrors.BrokerNotAllowed`, aggregate invariant, `OpenApiAccountLinker` skip-disallowed,
  startup validation. Unit + integration + E2E (unrestricted unchanged; Open API restricted).
- **P2 — Manual-cID verification.** `IBrokerVerifier` + probe cBot resource + `BrokerVerifier` impl
  (dispatch + tail-logs + timeout + teardown), endpoint wiring, dialog/notification UX, localization.
  Unit (fake dispatcher) + integration (fake verifier) + E2E (fake verifier) + skip-clean live probe.
- **P3 — Broker docs + landing.** `for-brokers.md`, sidebar, landing card, cross-links, allowlist doc
  (paired with P1/P2). `npm run build` zero broken links.
- **P4 — White-label AI creds.** Deployment-default vs per-user resolution (on top of multi-provider,
  or minimal standalone), settings UI, tests all tiers, docs (`features/ai.md`,
  `white-label`/`for-brokers` mention).
- **P5 — Persona docs + hub (Deliverables 2, 4, 5 docs).** `for-brokers.md`, `for-cloud-providers.md`,
  `for-traders.md`; revamp `audience.md` into the persona hub (3 persona cards + Contributors block);
  landing-page persona cards; SEO front matter + internal-link cluster + sitemap; sidebar ids; i18n
  wiring. `npm run build` zero broken links. Docs-only. (Broker-allowlist reference doc ships with
  P1/P2 per the same-commit mandate; the marketing `for-brokers.md` lands here.)
- **P6 — Provider hooks (optional follow-up).** Templated tenant deploy bundle + provisioning runbook;
  owner/admin **usage-metering endpoint** (projection over `Instance`/`Node`/`NodeStats`) with all
  three test tiers + `PageSmokeTests`. Docs from P5 stand without this.

## Touch map (files)

- **New (Core):** `Accounts/BrokerName.cs`, `Accounts/BrokerAllowlist.cs`, `Accounts/IBrokerVerifier.cs`
  (+ `BrokerProbeRequest`/`BrokerVerificationResult`), `Options/AccountsOptions.cs`; edits to
  `Options/AppOptions.cs`, `Constants/DomainErrors.cs`, `Entities.cs` (`CTraderIdAccount` methods take
  allowlist/verified broker). Deliverable 3: `AiProviderCredential` scope (per multi-provider plan).
- **New (Web/Nodes/Infra):** `Accounts/BrokerVerifier.cs`, `Resources/BrokerProbe/BrokerProbeBot.cs`
  (+ prebuilt `.algo`), allowlist wiring in `OpenApiAccountLinker.cs`, edits to `CtidEndpoints.cs`,
  DI registration, source-gen `LogMessages`, probe constants.
- **Modified (Web UI):** `NewTradingAccountDialog.razor`, OAuth-return notification, Settings→AI
  (Deliverable 3), i18n resources.
- **Docs:** `website/docs/for-brokers.md`, `website/docs/for-cloud-providers.md`,
  `website/docs/for-traders.md`, revamp `website/docs/audience.md` (persona hub), `sidebars.ts`,
  `website/src/pages/index.tsx` (persona cards), `docusaurus.config.ts` (verify SEO/sitemap),
  `white-label-for-business.md`, `features/white-label.md` (allowlist), `features/ai.md` (default
  creds); i18n locale files for translated locales.
- **Provider hooks (P6, optional):** templated deploy bundle (extend Bicep/Terraform/Helm),
  provisioning runbook, owner/admin usage-metering endpoint + read model + its tests.
- **Tests:** unit (`Accounts/`, `Ai/`), integration (`IntegrationTests`), E2E (`E2ETests`), plus
  `PageSmokeTests`/`MobileLayoutTests` routes.

## Open decisions (defaults chosen; flag to change)

1. **Allowlist config location:** new `App:Accounts:AllowedBrokers` (default — separates policy from
   visuals) vs on `App:Branding`. *Default: `App:Accounts`.*
2. **Probe `.algo`:** ship a prebuilt `.algo` in the repo (default — no build on the probe path) vs
   build the probe source via `CBotBuilder` once and cache. *Default: prebuilt, with source committed
   for provenance.*
3. **Probe dispatch:** reuse the full `RunInstance` machinery vs a dedicated lightweight probe path on
   `IContainerDispatcher`. *Default: dedicated lightweight probe (no persisted `Instance`, less state
   churn), reusing `BuildConsoleArgsList` semantics.*
4. **Disallowed Open API accounts:** skip-and-report (default — allowed accounts still link) vs reject
   the whole grant. *Default: skip-and-report.*
5. **Deliverable 3 base:** implement on top of `multi-provider-ai.md` (default — one credential model)
   vs a minimal standalone default-vs-personal key first. *Default: on top of multi-provider; if that
   plan isn't scheduled first, do the minimal `AiKeyStore` per-user fallback and migrate later.*
6. **Per-user AI creds scope:** broaden `AiProviderCredential` to `User` scope now (default) vs keep
   AI owner-only and only add the deployment-default semantics. *Default: add user scope (the task
   explicitly wants "users add their own creds").*
7. **Cloud-provider deliverable scope:** docs-only now (default — P5), add the usage-metering
   endpoint + templated deploy hooks as a later follow-up (P6) vs build hooks alongside the docs.
   *Default: docs-first (P5), hooks P6.*
8. **Multi-tenancy model pitched to providers:** deployment-per-tenant (default — strong isolation,
   matches per-instance `IOptionsMonitor` branding) vs a shared control plane driving many instances.
   *Default: deployment-per-tenant, mention control-plane as an advanced pattern.*
9. **Audience hub:** repurpose existing `audience.md` as the persona hub (default — keeps `/audience`
   inbound links + sidebar id, avoids a duplicate competing page) vs a brand-new hub page. *Default:
   revamp `audience.md`.*
