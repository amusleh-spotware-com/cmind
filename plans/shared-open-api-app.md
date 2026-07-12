# Shared (pre-configured) Open API application for white-label deployments

**Goal:** let a white-label operator (cTrader broker / reseller) **ship the app with one cTrader Open
API application** so that **every user authorizes their accounts through that single, pre-configured
Open API app** — no per-user Open API application setup. When a shared app is configured:

- Users get **no option** to add/edit/delete their own Open API application; the "Add Application"
  surface is replaced by a **read-only "managed by your provider"** state.
- **Authorize / invite / callback / token-refresh / copy** all use the shared app's `ClientId` +
  `ClientSecret`.
- The operator can provide the shared app **two ways** (mirroring the AI-key pattern): via
  **deployment config** (`App:OpenApi:SharedApp`, appsettings/env — seeded idempotently on startup)
  **and/or** at **runtime from Owner account settings** (encrypted in DB). Runtime (owner) row wins.

When **no** shared app is configured, behavior is **exactly today's** — each user configures their own
Open API application. Zero behavior change for stock deployments.

**House bars (CLAUDE.md, non-negotiable):** strict DDD (rich aggregates, VOs, `Core` zero infra deps,
invariants throw `DomainException`, one aggregate per `SaveChanges`, cross-aggregate refs by strong
ID); three test tiers every change (unit + integration + E2E, failure paths, missing-creds ⇒
skip-clean live test, never skip a tier); zero warnings + analyzer sweep clean; no `DateTime.UtcNow`
(`TimeProvider`); no secrets/magic-strings/raw-logging (`ISecretProtector`/`EncryptionPurposes`,
`Core/Constants`, source-gen `LogMessages`, `IOptionsMonitor<AppOptions>`); MudBlazor dialogs +
mobile-first + branded + **localized** UI (`IStringLocalizer<Ui>`, keys for all
`SupportedCultures`); docs in the same commit (+ every `website/i18n/` locale); EF migration on schema
change; modern C# 14.

---

## Current state (what the code already gives us)

- **Aggregate:** `Core/CopyTrading/OpenApiEntities.cs` — `OpenApiApplication : AuditedEntity<
  OpenApiApplicationId>` with `UserId` (owner), `Name`, `ClientId`, `EncryptedClientSecret`,
  `RedirectUri`. Factory `Create(userId, name, OpenApiClientId, encryptedSecret, OpenApiRedirectUri)`
  + `UpdateCredentials(...)`. VOs in `OpenApiValueObjects.cs` (`OpenApiClientId`, `OpenApiRedirectUri`).
- **Authorization aggregate:** `OpenApiAuthorization` references the app by **strong id**
  `ApplicationId` (+ its own `UserId`, `CtidUserId`, encrypted tokens). This is the key seam: **every
  consumer resolves the app from `authorization.ApplicationId`, not from the user.**
- **Repository:** `IOpenApiApplicationRepository` (`Core/Domain/Repositories.cs`) +
  `OpenApiApplicationRepository` (`Infrastructure/Persistence/Repositories.cs`):
  - `GetByIdAsync(id, UserId owner, ct)` — **owner-filtered**.
  - `GetByUserAsync(UserId owner, ct)` — one app per user (single-app-per-user model).
  - `AddAsync` / `RemoveAsync` / `SaveChangesAsync`.
  - EF: `DataContext.cs` unique index `IX_OpenApiApplications_UserId_Name`, FK
    `OpenApiAuthorizations.ApplicationId → OpenApiApplications` (cascade). Migration
    `20260709165039_SingleOpenApiAppPerUser`.
- **Web endpoints:** `Web/Endpoints/OpenApiEndpoints.cs` (`/api/openapi/*`, `RequireFeature(OpenApi)`):
  - `GET/PUT/DELETE /application` — per-user CRUD via `GetByUserAsync(uid)`.
  - `GET /authorize` — builds cTrader authorize URL from the user's app.
  - `POST /application/invite` + `GET /openapi/invite/{state}` (anon) — invite links.
  - `GET /openapi/callback` (anon) — exchanges code, **validates `application.UserId == state.UserId`**,
    calls `OpenApiAccountLinker.LinkAsync(userId, application, grant, tokens)`.
- **Consumers that resolve the app by `authorization.ApplicationId`:**
  - `Nodes/CopyTrading/OpenApiTokenRefreshService` — `applications.GetByIdAsync(auth.ApplicationId,
    auth.UserId, ct)` ← **owner-filtered; breaks under a shared app owned by someone else.**
  - `Web/OpenApi/OpenApiAccountLinker.LinkAsync` — receives the app instance (fine).
  - `Web/Endpoints/CopyEndpoints` `/accounts/{id}/symbols` — `db.OpenApiApplications.FirstOrDefault(a =>
    a.Id == auth.ApplicationId)` (**no owner filter — already shared-safe**).
  - `Nodes/CopyTrading/CopyEngineSupervisor` (~L269) + `CopyEndpoints` execution — read `ClientId` +
    decrypted secret from the app tied to the profile's source authorization.
- **Options:** `Core/Options/AppOptions.cs` → `OpenApiOptions` (`Enabled`, hosts, token-refresh
  timers). No shared-app concept yet. **AI is the template**: `AiOptions.Providers` +
  deployment-seeded providers imported idempotently on startup; owner runtime credentials in DB.
- **White-label gating template:** `Core/Options/BrandingOptions.cs` (`AllowBuiltInAi`,
  `AllowedAiProviderKinds`, `RequireMfa`, …). Owner-only settings pages exist
  (`FeatureSettings.razor` `@attribute [Authorize(Policy = "Owner")]`, `AiSettings.razor`).
- **Owner:** `Web/Auth/OwnerSeeder.cs` seeds the single owner account. `AuthPolicies` (`AppConstants`)
  has `Owner` / `UserOrAbove`.
- **Settings page:** `Web/Components/Pages/OpenApiApplications.razor` (`/settings/openapi`,
  `UserOrAbove`) + dialog `Components/Dialogs/OpenApiAppDialog.razor`.
- **Tests:** `UnitTests/CopyTrading/OpenApiAuthDomainTests`, `IntegrationTests/
  OpenApiAuthorizationPersistenceTests`, live `IntegrationTests/CopyLive/*`,
  `IntegrationTests/OpenApiLiveConnectionTests`. Onboarding E2E `CMIND_ONBOARD` pattern.
- **Docs:** `website/docs/features/` (copy-trading / open-api doc) + `website/i18n/**`.

**Design north star:** the shared app is a **real persisted `OpenApiApplication` row** so the existing
strong-id seam (`authorization.ApplicationId → app`) keeps every downstream consumer working
untouched. We add **(a)** a *deployment scope* to the aggregate, **(b)** a **resolver** that decides
per-request which app a user authorizes against, **(c)** owner + config provisioning, **(d)** UI gating.

---

## Deliverable 1 — Domain: a deployment-scoped (shared) Open API application

### 1.1 Aggregate change

Add a **shared** flavour to `OpenApiApplication` without breaking the per-user model. Keep `UserId`
**non-null** and owned by the **owner account** (from `OwnerSeeder`) — avoids a nullable-FK migration
ripple and keeps the existing index valid — and add an explicit marker:

- New property `public bool IsShared { get; private set; }` (default `false`).
- New factory:
  ```csharp
  public static OpenApiApplication CreateShared(
      UserId ownerId, string name, OpenApiClientId clientId,
      byte[] encryptedClientSecret, OpenApiRedirectUri redirectUri)
  ```
  identical to `Create` but sets `IsShared = true`. Invariant: exactly **one** shared row may exist
  (enforced by a filtered unique index + repo guard, see 1.3/1.4).
- `UpdateCredentials(...)` unchanged (works for both flavours; `IsShared` is immutable after create).
- Ubiquitous language: call it the **shared Open API application** / *deployment* application in code,
  docs, and UI. No "global app" / "tenant app" synonyms.

Unit tests (`OpenApiAuthDomainTests`): `CreateShared` sets `IsShared`, still enforces name/client-id/
secret invariants (blank ⇒ `DomainException` with the existing `DomainErrors` codes).

### 1.2 Value objects / constants

- No new VOs needed (reuse `OpenApiClientId`, `OpenApiRedirectUri`).
- Add any new user-facing error/notice keys to `Core/Constants/DomainErrors.cs` only if a new
  invariant needs one (e.g. `OpenApiSharedAppExists` if the repo guard throws). Prefer returning a
  typed result over throwing for the "already exists" case in the app tier.

### 1.3 Repository additions (`IOpenApiApplicationRepository`)

- `Task<OpenApiApplication?> GetSharedAsync(CancellationToken ct)` — the single `IsShared == true` row.
- `Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, CancellationToken ct)` —
  **owner-agnostic** overload for **trusted server-side** resolution (token refresh, callback), used
  only where the caller already scoped ownership via the authorization.

Keep the existing owner-filtered `GetByIdAsync(id, owner, ct)` and `GetByUserAsync(owner, ct)` for the
per-user path.

### 1.4 EF / migration

- Map `IsShared` (bool, default `false`).
- **Filtered unique index** guaranteeing at most one shared row:
  `HasIndex(a => a.IsShared).HasFilter("\"IsShared\" = true").IsUnique()` (Postgres partial index).
- Migration `AddSharedOpenApiApplication` via the `migration` skill
  (`-o Persistence/Migrations`). No data backfill (existing rows default `IsShared=false`).

---

## Deliverable 2 — Resolver: which app does a user authorize against?

Single choke point so endpoints/UI never branch ad-hoc.

`Core`-side interface (pure) — `Core/CopyTrading/IOpenApiAppResolver.cs`:

```csharp
public sealed record OpenApiAppResolution(OpenApiApplication Application, bool IsShared);

public interface IOpenApiAppResolver
{
    // The app THIS user authorizes new accounts against. Shared row when shared-mode is on,
    // otherwise the user's own app (null when neither exists).
    Task<OpenApiApplication?> ResolveForUserAsync(UserId userId, CancellationToken ct);

    // True when a shared app exists — drives UI gating and "can this user manage their own app?".
    Task<bool> IsSharedModeAsync(CancellationToken ct);
}
```

Impl in `Infrastructure` (or `Web` app tier) over `IOpenApiApplicationRepository`:
`ResolveForUserAsync` → `GetSharedAsync() ?? GetByUserAsync(userId)`. `IsSharedModeAsync` →
`GetSharedAsync() is not null`. Register in DI (`Infrastructure/DependencyInjection.cs` or Web).

**Precedence rule (documented):** shared row present ⇒ shared-mode for **everyone** (including the
owner's own trading accounts). Any pre-existing per-user apps are **ignored for new authorizations**
while shared-mode is on, but existing `OpenApiAuthorization` rows that reference an old per-user app
keep working (resolved by `authorization.ApplicationId`). No destructive migration of user apps.

---

## Deliverable 2.5 — The canonical redirect URL (single, app-provided)

**One redirect URL for the whole deployment**, identical for the shared app **and** any per-user app,
so the operator registers exactly one value in the cTrader partner portal:

```
{deployment public base URL}/openapi/callback        # e.g. https://cmind.mybroker.com/openapi/callback
```

`/openapi/callback` = `Core.Constants.OpenApiEndpoints.CallbackPath`.

- **App-provided / displayed, never guessed by the operator.** The Owner settings card and the
  per-user card both render the redirect URL **read-only with a copy button** so anyone creating a
  cTrader Open API app knows precisely what to paste. This is the primary onboarding artifact.
- **Composed from ONE configured public base URL, not the request host.** cTrader requires the
  `redirect_uri` sent in the authorize + token-exchange calls to **exactly match** the registered
  value; behind a reverse proxy/CDN the inbound `Host` header is unreliable. So a canonical base URL
  is the source of truth for **both** (a) what the app displays, (b) the stored
  `OpenApiApplication.RedirectUri`, and (c) the `redirect_uri` sent to cTrader.
  - **Reuse an existing canonical public-URL option if one exists** (verify:
    `search_in_files_by_text "PublicBaseUrl" / "PublicUrl" / "BaseUrl"` in options + Aspire/env);
    otherwise add `App:OpenApi:PublicBaseUrl` (or a deployment-wide `App:PublicBaseUrl`).
  - Add a small `IRedirectUrlProvider` (or a helper on the resolver) that returns
    `{PublicBaseUrl}{CallbackPath}`, with a **fallback to `CallbackUrl(ctx)`** (request host) only when
    no base URL is configured — preserving today's zero-config behavior for stock single-host
    deployments while making white-label deployments deterministic.
- **Same URL for personal and shared apps** ⇒ switching a deployment from per-user to shared mode
  needs no redirect-URL change; the operator's registered value keeps working.

**Post-authorization redirect (what the user sees):**
- **Authorize flow** (signed-in user linking their own cIDs): back into the app → **accounts list**
  (`AuthorizedRedirectPath = /accounts`) — unchanged.
- **Invite flow** (a cID onboarded via an operator/inviter's link — the person may not have an app
  session): show the **success page only** — "Your accounts were added" — with **no redirect into an
  accounts list they cannot see**. Adjust `InviteRedirectPath` handling so the invite branch ends on
  the success message (drop the `/` auto-redirect for invites, or point it at a neutral confirmation).

---

## Deliverable 3 — Provisioning path A: deployment config (seed on startup)

Mirror `AiOptions` deployment-seeded providers.

- Extend `OpenApiOptions` (`AppOptions.cs`):
  ```csharp
  public SharedOpenApiAppOptions? SharedApp { get; init; }
  ...
  public sealed record SharedOpenApiAppOptions
  {
      public bool Enabled { get; init; }
      public string Name { get; init; } = "Shared Open API Application";
      public string ClientId { get; init; } = string.Empty;
      public string ClientSecret { get; init; } = string.Empty; // plaintext in config; encrypted at seed
  }
  ```
- **Idempotent seeder** (startup hosted service or the existing startup seeding path next to the AI
  provider seed): when `SharedApp.Enabled` and `ClientId`/`ClientSecret` non-blank **and no shared row
  exists**, create it via `OpenApiApplication.CreateShared(ownerId, Name, new OpenApiClientId(ClientId),
  protector.Protect(secretBytes, EncryptionPurposes.OpenApiClientSecret), redirectUri)`.
  - `ownerId` = the seeded owner (resolve owner `AppUser` by `App:OwnerEmail`).
  - `redirectUri` — the callback path is host-derived at request time today (`CallbackUrl(ctx)`), but
    the row needs a stored `RedirectUri`. Add `App:OpenApi:PublicBaseUrl` (or reuse an existing public
    base-url option if present) to compose `"{PublicBaseUrl}{OpenApiEndpoints.CallbackPath}"` at seed
    time. **Confirm** whether a canonical public-URL option already exists before adding one
    (`search_in_files_by_text "PublicBaseUrl"` / `"BaseUrl"` in options) — reuse it if so.
  - Re-seed is a no-op if the row exists; **config never overwrites an owner-edited runtime row**
    (owner wins) — only creates when absent.
- Secret stays encrypted; never logged (`LogMessages` for "seeded shared app", no secret in the log).

---

## Deliverable 4 — Provisioning path B: Owner runtime settings (DB, encrypted)

The operator manages the shared app from **Owner settings** without redeploying.

### 4.1 Endpoints (`OpenApiEndpoints.cs`, new Owner-gated sub-group)

`var owner = app.MapGroup("/api/openapi/shared").RequireAuthorization(AuthPolicies.Owner)
.RequireFeature(FeatureFlag.OpenApi);`

- `GET /api/openapi/shared` → `{ configured, name, clientId, redirectUri, source: "config"|"owner"|
  "none", authorizedAccountCount }` (never returns the secret).
- `PUT /api/openapi/shared` (`SaveOpenApiAppRequest`-shaped) → create or `UpdateCredentials` the shared
  row (owner-owned). Secret optional on edit (keep existing when blank, same pattern as per-user PUT).
  Redirect URI = the **canonical** URL from Deliverable 2.5 (`{PublicBaseUrl}{CallbackPath}`), **not**
  the raw request host.
- `DELETE /api/openapi/shared` → remove the shared row ⇒ deployment reverts to per-user mode. Warn in
  UI that existing authorizations keep working but new links revert to per-user.

Owner mutates **one aggregate per `SaveChanges`**. Reuse `ISecretProtector` +
`EncryptionPurposes.OpenApiClientSecret`.

### 4.2 Owner settings UI

Two clean options — **recommend integrating into the existing `/settings/openapi` page**, role-aware,
rather than a separate route (fewer routes, one mental model):

- **Owner, shared-mode configurable section** (new): a card "Deployment Open API application (shared)"
  with Configured/Not-configured state, `Add/Edit/Delete` via a **MudBlazor dialog** (reuse/extend
  `OpenApiAppDialog`), showing Client ID + Redirect URI, hitting `/api/openapi/shared`.
- The existing per-user card renders **only when shared-mode is off** (see Deliverable 5).

New page route must be added to `PageSmokeTests`; here we reuse `/settings/openapi` (already smoke
tested) — verify it still passes for both roles. All strings via `@L["key"]` (add to
`tools/i18n/ui-translations.json` for every `SupportedCultures`, run `gen-resx.ps1`).

---

## Deliverable 5 — UI gating: hide per-user app when shared-mode is on

`OpenApiApplications.razor` (`/settings/openapi`):

- On load, call a new `GET /api/openapi/mode` (or fold into the existing `GET /application` response)
  returning `{ sharedMode: bool, isOwner: bool }`. Fold into `IsSharedModeAsync` server-side.
- **Shared-mode ON, normal user:** hide "Add Application"/Edit/Delete entirely; show a read-only
  **"Open API is managed by your provider"** panel + the **"Authorize accounts"** button (which now
  drives the shared app) + the Redirect URI (informational). Copy-invite still available (uses shared
  app).
- **Shared-mode ON, owner:** show the **Deployment shared application** management card (Deliverable 4)
  instead of a personal app card.
- **Shared-mode OFF:** unchanged — every user manages their own app (today's behavior).

### 5.1 Endpoint rewiring to the resolver

Replace `apps.GetByUserAsync(uid, ct)` with `resolver.ResolveForUserAsync(uid, ct)` in:

- `GET /authorize` (build authorize URL from the resolved app; keep state keyed by `application.Id`).
- `POST /application/invite` (invite against the resolved app).
- `GET /openapi/invite/{state}` + `GET /openapi/callback`: **relax the `application.UserId ==
  state.UserId` check** — for a shared app the authorizing user ≠ app owner. New rule: load the app by
  `state.ApplicationId` (owner-agnostic `GetByIdAsync(id, ct)`); it is valid if it is the shared row
  **or** it is owned by `state.UserId`. The **authorization** is still created for `state.UserId`
  (`OpenApiAccountLinker.LinkAsync(state.UserId, application, …)` already keys the authorization + all
  child accounts to the authorizing user — shared app, per-user tokens/accounts).
- Per-user `GET/PUT/DELETE /application`: **reject with 409/"managed by provider"** when shared-mode is
  on (defense-in-depth behind the hidden UI), so a normal user can't create a personal app that would
  be ignored anyway.

### 5.2 Token-refresh consumer fix

`OpenApiTokenRefreshService.RefreshCycleAsync`: change
`applications.GetByIdAsync(auth.ApplicationId, auth.UserId, ct)` → owner-agnostic
`applications.GetByIdAsync(auth.ApplicationId, ct)`. The authorization already scopes the owner; the
app is a trusted server-side lookup. This makes refresh work when the app (shared) is owned by the
operator while the authorization belongs to a normal user. (Copy symbol lookup + supervisor already
resolve the app id owner-agnostically — no change.)

---

## Deliverable 6 — Open API client rate limiting (queue + pace, adjustable)

**Context (verified in code):** there is **no client-side rate limiter today.**
`OpenApiConnection.SendPumpAsync` drains the unbounded outbound `Channel` and calls
`transport.SendAsync` as fast as messages arrive; cTrader enforces its per-connection message limit
(~50 msg/s, plus stricter caps on historical-data requests) **server-side**, so a burst can get the
app **rate-limited / blocked** — the app only reacts *after* rejection via `BackoffPolicy`.

**We add one — per message type, matching the cTrader Open API docs.** cTrader does not have a single
cap; it publishes **different limits per message class** (documented, per-connection): a general
message cap (~50/s), a stricter **historical-data** request cap (~5/s for `ProtoOAGetTrendbarsReq` /
`ProtoOAGetTickDataReq`), etc. So the pacer is a **set of token buckets keyed by message-type
category**, and the white-label / owner configuration is **also per category**. Messages **queue** and
drain at the available per-type limit so we never trip a block.

### 6.1 Message-type categories (single source of truth)

- Add an `OpenApiRateCategory` enum in `CTraderOpenApi` + a pure classifier
  `OpenApiRateLimits.Classify(payloadType) → OpenApiRateCategory` that maps `ProtoOAPayloadType`
  values to a category, exactly per the Open API docs. Initial categories:
  - `General` — default for all trading/read messages (order/amend/cancel, symbol/account queries…).
  - `HistoricalData` — `ProtoOAGetTrendbarsReq`, `ProtoOAGetTickDataReq` (the doc's stricter bucket).
  - `Exempt` — heartbeat + app-auth/account-auth handshake (never paced; keep-alive/reconnect must
    never wait behind a trading backlog).
  - (Extensible: add categories as the docs distinguish more, without touching call sites.)
- The classifier is the **one place** the doc's mapping lives — config, buckets, and tests all reference
  the same enum, so there are no magic payload-type lists scattered around.

### 6.2 Per-category pacer in the Open API client (default ON)

- `OpenApiConnectionOptions.RateLimits` = a map `OpenApiRateCategory → int` (messages/sec). `> 0` caps
  that category; **`0` = unlimited** for it. `Exempt` is always unlimited.
- One **token bucket per category** (`TimeProvider`-driven; fractional continuous refill → smooth flow
  at the cap, no bursty stalls), consulted in `SendPumpAsync` **before** `transport.SendAsync`:
  1. classify the outbound message → category,
  2. `Exempt` ⇒ send immediately,
  3. else `await` a token from **that category's** bucket, then send.
  - cTrader's general cap applies to *all* messages, so a `HistoricalData` message draws from **both**
    the `HistoricalData` bucket **and** the `General` bucket (acquire both tokens) — mirrors the docs
    where a historical request also counts against the overall connection rate.
  - Messages already sit in the outbound `Channel` → we **queue, not drop**; awaited `SendAsync`
    responses still return. Single-reader drain keeps **FIFO** so order execution stays in submission
    order (critical for copy trading).
- **Resilience:** `DrainOutbound` on disconnect + buckets reset on reconnect (no post-reconnect burst
  that re-trips the limit). The existing `BackoffPolicy` stays the backstop for any residual
  server-side rejection — the pacer makes that path rare, not the sole defense.

### 6.3 Config wiring — white-label **and** Owner settings, **per category**

Both adjustment surfaces are per-category:

- **Deployment config:** `OpenApiOptions.RateLimits` on `AppOptions` — a per-category map bound from
  `App:OpenApi:RateLimits:{General,HistoricalData}` (int msgs/sec; omit ⇒ safe default; `0` ⇒
  unlimited). Passed through `OpenApiConnectionFactory.Create` into `OpenApiConnectionOptions`.
- **Owner runtime settings:** on the `/settings/openapi` Owner section (Deliverable 4.2), **one number
  field per category** (label + "0 = unlimited" hint), e.g. "General (msgs/sec)", "Historical data
  (msgs/sec)". Persist a per-category owner override (small owner-settings rows / a JSON column;
  plaintext — not a secret) that **wins over config** per category. `OpenApiConnectionFactory` composes
  the effective per-category map (`owner-override[cat] ?? config[cat] ?? default[cat]`) each time it
  builds a connection (`IOptionsMonitor` + override store) → applies to **new** connections without
  redeploy; in-flight connections pick it up on reconnect.
- **Defaults:** **ON at the cTrader-documented safe values** (`General ≈ 45`, `HistoricalData ≈ 5` —
  match/stay just under the published caps). A broker with a negotiated higher limit raises the relevant
  category; an unmetered agreement sets it `0`. Deliberate, documented improvement over today's unpaced
  send.

### 6.4 Tests (all three tiers — mandatory, resilient)

- **Unit (`tests/UnitTests`):** with `FakeTimeProvider` + `FakeOpenApiTransport` (count/timestamp sends):
  - each category paces to its own cap; a `HistoricalData` burst is capped by the historical rate **and**
    still counts against `General`;
  - `General` traffic is **not** slowed by the historical bucket and vice-versa (independent buckets);
  - `0` for a category ⇒ that category drains immediately;
  - `Exempt` (heartbeat/handshake) sent promptly under a saturated trading backlog;
  - FIFO order preserved; buckets reset on reconnect (no burst);
  - classifier maps each documented payload type to the right category (table test).
  Extend `OpenApiConnectionTests` / `OpenApiResilienceTests`.
- **Integration (`tests/IntegrationTests`, Testcontainers PG):** owner sets per-category limits via the
  endpoint; round-trip; effective map = per-category `override ?? config ?? default`; `0` persists as
  unlimited; config-only path (no overrides) yields config values.
- **E2E (`tests/E2ETests`, Playwright, mobile + desktop):** owner opens `/settings/openapi`, edits the
  per-category rate fields, saves, reload shows new values; "0 = unlimited" hint renders; fields are
  Owner-only (hidden/forbidden for a normal user). Live Open API path unaffected (server-side).

---

## Testing (all tiers — mandatory)

**Unit (`tests/UnitTests`):**
- `OpenApiApplication.CreateShared` sets `IsShared`, enforces invariants (blank name/clientId/secret ⇒
  `DomainException`).
- Resolver logic via a fake repo: shared present ⇒ returns shared for any user + `IsSharedMode==true`;
  shared absent ⇒ returns per-user app + `IsSharedMode==false`.

**Integration (`tests/IntegrationTests`, Testcontainers PG):**
- Filtered-unique-index: a second `CreateShared` insert throws `DbUpdateException`.
- Deployment-config seeder: idempotent (seed twice ⇒ one row), owner-owned, secret round-trips through
  `ISecretProtector`; seeder no-ops when an owner runtime row already exists.
- `ResolveForUserAsync` returns the shared row for a **different** user than the owner.
- Token-refresh: an `OpenApiAuthorization` owned by user B, referencing the shared app owned by owner
  A, refreshes successfully (regression for the owner-filter bug).
- Owner endpoints: `PUT`/`GET`/`DELETE /api/openapi/shared` create/update/remove the shared row; secret
  never returned; blank-secret edit preserves the stored secret.
- Per-user `POST/PUT /application` returns 409 when shared-mode is on.

**E2E (`tests/E2ETests`, Playwright, mobile + desktop):**
- **Shared-mode ON, normal user:** `/settings/openapi` shows the read-only "managed by your provider"
  panel, **no** Add form; "Authorize accounts" present. (Boot fixture with `App:OpenApi:SharedApp`
  seeded, or owner-set via API.)
- **Owner:** `/settings/openapi` shows the Deployment shared-application card; owner sets creds via the
  dialog; page reflects "configured".
- **Shared-mode OFF (default):** unchanged per-user Add flow still renders (existing test stays green).
- **Live, skip-clean:** a `CMIND_ONBOARD`/`CopyLive`-style test that, when real Open API creds +
  `SharedApp` config are present, drives the shared-app authorize/callback against a real cID and
  asserts the account links; **skips cleanly** when creds absent (never skip the tier, never ask).
- Add/confirm `/settings/openapi` in `PageSmokeTests` for both roles.

`FakeTradingSession` stays cTrader-faithful; no simulator/test weakening.

---

## Docs & localization (same commit)

- Update `website/docs/features/` Open-API / copy-trading doc: new **"Shared Open API application
  (white-label)"** section — what it is, the two provisioning paths (config keys
  `App:OpenApi:SharedApp:{Enabled,Name,ClientId,ClientSecret}` + `PublicBaseUrl`, and Owner settings),
  the user experience under shared-mode, and the precedence/fallback rules.
- **Redirect URL — document how to form it (single URL).** A dedicated doc block: the redirect URL a
  broker registers in the cTrader partner portal is **one value** =
  `{your deployment URL}/openapi/callback` (path = `OpenApiEndpoints.CallbackPath`), e.g.
  `https://cmind.yourbroker.com/openapi/callback`. Explain: it is the **same single URL** for the
  shared app and any per-user app; the app **displays the exact value** (Owner settings + per-user
  card, copy button) so nobody hand-types it; and the invite vs normal-user destinations differ only in
  where the app sends the user *after* the callback (accounts list vs "accounts added" success) — the
  registered redirect URL itself is unchanged. Show the `App:OpenApi:PublicBaseUrl` config key used to
  make the value deterministic behind a proxy/CDN.
- **Rate limiting doc (per message type):** document the `App:OpenApi:RateLimits:{General,
  HistoricalData}` map — that limits are **per cTrader message category** (mirroring the Open API docs'
  general vs historical-data caps), the safe defaults (`General ≈ 45`, `HistoricalData ≈ 5`), that
  **`0` disables a category (unlimited)**, that historical requests count against both their own and the
  general bucket, and when a broker should raise a category (negotiated higher) or disable it
  (unmetered). Show the Owner-settings per-category fields too.
- Update `website/docs/for-brokers` (persona hub) to mention "ship one Open API app for all your users"
  + "tune the per-message-type client rate limits if you've negotiated higher cTrader limits".
- Update `website/docs/deployment` config reference with all new options.
- **Localize:** mirror every doc change into all `website/i18n/**` locales; add all new UI keys to
  `tools/i18n/ui-translations.json` for every `Core.Constants.SupportedCultures` language, run
  `pwsh tools/i18n/gen-resx.ps1`. Hardcoded-string + resource-parity gates must stay green.

---

## Definition of done

- [ ] `dotnet build` clean (0 warnings); analyzer sweep clean on touched projects (`Core`,
      `Infrastructure`, `Nodes`, `Web`); Rider `get_file_problems` clean on every touched `.cs`/`.razor`.
- [ ] Unit + integration + E2E green (incl. failure paths + skip-clean live test); `/settings/openapi`
      in `PageSmokeTests` for both roles.
- [ ] DDD: shared flavour is a rich factory + invariant; `Core` has no infra deps; one aggregate per
      `SaveChanges`; consumers resolve by strong id.
- [ ] No `DateTime.UtcNow`; secret encrypted via `ISecretProtector`; no magic strings (constants);
      config via `IOptionsMonitor<AppOptions>`; source-gen logging.
- [ ] No hard-coded user-facing text; keys in all locales; parity + hardcoded gates green; RTL renders.
- [ ] EF migration `AddSharedOpenApiApplication` added; docs updated in all `website/i18n/` locales.
- [ ] Rate-limit pacer is **per message-type category** (classifier = single source of truth, matches
      Open API docs); default ON at safe caps (`General`/`HistoricalData`); `0` = unlimited per category;
      queues (never drops), FIFO preserved, heartbeat/handshake exempt, resets on reconnect; adjustable
      via `App:OpenApi:RateLimits` **and** per-category Owner-settings fields; token-buckets unit-tested
      with `FakeTimeProvider` + integration + E2E.
- [ ] Single redirect URL documented (formation from deployment URL) + shown in-app with copy button.
- [ ] Stock deployment (no shared app) behaves exactly as today (pacer default decision honored).

---

## Open questions / decisions to confirm before coding

1. **Public base URL for the seeded RedirectUri** — reuse an existing option if one exists; otherwise
   add `App:OpenApi:PublicBaseUrl`. (Runtime owner-set path derives it from the request host, so this
   only matters for the config-seed path.)
2. **UI placement** — recommend folding owner shared-app management + normal-user gating into the
   existing `/settings/openapi` page (role-aware) rather than a new Owner-only route. Confirm.
3. **Existing per-user apps when an operator later enables shared-mode** — recommend: keep them
   (existing authorizations keep working), ignore them for new links, hide the per-user UI. No
   destructive migration. Confirm.
**Decided (per your direction):**
- **Single redirect URL** (not two) — invite vs user differ only in the post-callback destination,
  driven by the validated `state.IsInvite`; the registered URL is one value, documented + shown in-app.
- **Rate limiting: implement it, per message type** (per cTrader-doc category — general vs
  historical-data), queue + pace at the available per-type limit, default ON at safe caps,
  **adjustable per category** via both white-label config **and** Owner settings (`0` = unlimited),
  resilient, tested at all three tiers, documented. (Deliverable 6.)
