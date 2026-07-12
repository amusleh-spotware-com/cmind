# White-label options in Owner settings — every deployment knob, owner-tunable, always in sync

**Goal:** the **app owner** (single seeded owner account) can see and change **every white-label
option** from **in-app Owner settings** — enable/disable/tune each exactly as a white-label
*deployment* does through `appsettings`/env — **without a redeploy**. And the two surfaces
(deployment config ⇄ owner settings) are **kept in lock-step forever**: adding a new white-label
option to config **must** surface it in Owner settings, enforced by a build-failing parity gate and a
new CLAUDE.md mandate.

Today only **one** white-label family is owner-tunable at runtime — **feature flags**
(`IFeatureGate` overlays `App:Features` with owner `AppSetting` rows). Every other white-label family
(`App:Branding`, `App:Registration`, `App:Accounts`, `App:Email`, the white-label knobs on
`App:OpenApi`, `App:Ai` built-in gate, `App:PropFirm` thresholds) is **config-only** — invisible to
the owner and only changeable by editing config and restarting. This plan generalizes the proven
feature-gate override pattern to **all** white-label options and adds the sync guarantee.

> **Coordinate with [`plans/shared-open-api-app.md`](shared-open-api-app.md) (in flight).** That plan
> already introduces owner-runtime provisioning for the shared Open API app + per-category rate limits
> from `/settings/openapi`. This plan **does not touch the shared-app secret/credential path** — it
> owns the **non-secret white-label toggles/values**, and it treats the shared-app owner surface as an
> already-owned peer (registered in the catalog as "managed on the OpenApi settings section", not
> duplicated). See §7 Interlock.

**House bars (CLAUDE.md, non-negotiable):** strict DDD (rich aggregates/VOs, `Core` zero infra deps,
invariants throw `DomainException`, one aggregate per `SaveChanges`, cross-aggregate refs by strong
ID); three test tiers every change (unit + integration + E2E, failure paths, skip-clean live test,
never skip a tier); zero warnings + analyzer sweep clean; no `DateTime.UtcNow` (`TimeProvider`); no
secrets/magic-strings/raw-logging (`ISecretProtector`/`EncryptionPurposes`, `Core/Constants`,
source-gen `LogMessages`, `IOptionsMonitor<AppOptions>`); MudBlazor dialogs + mobile-first + branded +
**localized** UI (`IStringLocalizer<Ui>`, keys for every `SupportedCultures`); docs in the same commit
(+ every `website/i18n/` locale); EF migration on schema change; modern C# 14.

---

## Current state (verified in code)

### The white-label option records (deployment config = the source truth today)

| Record | Config section | Kind of knobs | Consumed by |
|---|---|---|---|
| `Core/Options/BrandingOptions.cs` | `App:Branding` | `ProductName`, `CompanyName`, `SupportUrl`, `Description`, `LogoUrl`, `FaviconUrl`, 10 theme colours, `CustomCss`, `ShowSiteLink` (bool), `RequireMfa` (bool), `AllowBuiltInAi` (bool), `AllowLocalProviders` (bool), `AllowedAiProviderKinds` (string list), `EnableEconomicCalendar` (bool) | `Web` theme factory, `Branding/BrandingCss.cs`, MFA gate, AI provider gates, calendar hard-gate |
| `Core/Options/FeaturesOptions.cs` | `App:Features` | 16 bool feature flags (`Authoring`…`EconomicCalendar`, `Registration`) | **already owner-tunable** via `IFeatureGate` + `AppSetting` `feature.<flag>` rows |
| `Core/Options/RegistrationOptions.cs` (+ `Captcha`/`Attributes`/`Api` nested) | `App:Registration` | `Enabled`, `Mode` (enum), `DefaultRole`, `RequireTermsAcceptance`, `AllowedEmailDomains` (list), `BlockDisposableEmail`, `TokenLifetime`, CAPTCHA (`Enabled`+`VerifyUrl`+`SiteKey`+**`Secret`**), 8 `AttributePolicy` enums, API (`Enabled`+**`Secret`**+`ActivateImmediately`+`InviteMustChangePassword`) | registration page/endpoints, provisioning API |
| `Core/Options/AccountsOptions.cs` | `App:Accounts` | `AllowedBrokers` (list), `BrokerProbeTimeout`, `BrokerProbeAlgoPath` | broker allowlist verifier |
| `Core/Options/AppOptions.cs → EmailOptions` | `App:Email` | `Host`, `Port`, `UseStartTls`, `Username`, **`Password`**, `FromAddress`, `FromName` | outbound mail sender |
| `AppOptions.cs → OpenApiOptions` | `App:OpenApi` | white-label subset: `PublicBaseUrl`, `SharedApp` (secret path — **owned by shared-open-api-app plan**), `RateLimits` map (**owned by shared-open-api plan**) | Open API auth/copy |
| `AppOptions.cs → AiOptions.BuiltIn` (`AiBuiltInOptions`) | `App:Ai:BuiltIn` | `Enabled` (combined with `Branding.AllowBuiltInAi`) | built-in ONNX LLM gate |
| `AppOptions.cs → PropFirmOptions` | `App:PropFirm` | `DrawdownWarnThresholdPercent` (white-label-ish threshold) | prop-firm tracker alerts |

### The one existing owner-runtime pattern (the template to generalize)

- `Core/Features/IFeatureGate.cs` — `IsEnabled(flag)`, `Snapshot()`, `SetOverrideAsync(flag, bool?, ct)`.
- `Infrastructure/Features/FeatureGate.cs` — overlays `options.CurrentValue.Features` baseline with
  `AppSetting` rows keyed `feature.<flag>`, `IMemoryCache` (10s TTL), `TimeProvider`, one aggregate per
  `SaveChanges`, clears cache on write.
- `Core/Constants/AppConstants.cs → FeatureSettings` — `OverrideKeyPrefix="feature."`,
  `OverrideCacheKey`, `OverrideCacheTtl`, `OverrideKey(flag)`.
- `Web/Endpoints/FeatureEndpoints.cs` — `/api/features` (`GET` snapshot, `PUT /{flag}`), Owner-gated,
  **not** itself feature-gated (owner must always reach it).
- `Web/Components/Pages/FeatureSettings.razor` — Owner-only toggle grid; a section in
  `Components/Dialogs/SettingsDialog.razor` (the full-screen Owner settings hub: OpenApi / Ai /
  Features / Language / Legal).
- `AppSetting` aggregate (`Core/Entities.cs`) — append/update key→value with `Create`/`SetValue` +
  `TimeProvider`; the generic runtime-override store.

**Design north star:** introduce **one white-label option catalog** (the single registry of every
white-label knob: config path, type, default, category, secret-ness, owner-editability) and **one
effective-value provider** that overlays owner `AppSetting` overrides on the `IOptionsMonitor`
baseline — exactly as `FeatureGate` does for bools, but generalized to bool/string/int/enum/list/
colour/secret. Every consumer that reads a white-label value reads it **through the provider**; the
Owner UI renders **from the catalog**; a reflection parity test asserts the catalog covers **every**
white-label option property. New option ⇒ compile-time-adjacent test failure until it is registered.

---

## Deliverable 1 — The white-label option catalog (single source of truth)

`Core/WhiteLabel/` (pure Core, zero infra deps).

### 1.1 The option descriptor

```csharp
public enum WhiteLabelValueKind { Bool, String, MultilineString, Int, TimeSpan, Enum, StringList, Color, Secret }

public sealed record WhiteLabelOption
{
    public required string Key { get; init; }            // stable id, e.g. "branding.requireMfa" (== config path tail, dotted)
    public required string ConfigPath { get; init; }     // "App:Branding:RequireMfa" — where the deployment sets it
    public required WhiteLabelValueKind Kind { get; init; }
    public required WhiteLabelCategory Category { get; init; }  // Branding | Theme | Features | Registration | Accounts | Email | Ai | OpenApi | PropFirm
    public bool OwnerEditable { get; init; } = true;     // false ⇒ shown read-only (or delegated elsewhere)
    public bool IsSecret { get; init; }                  // encrypt via ISecretProtector; never returned to UI
    public string? EnumTypeName { get; init; }           // for Enum kind
    public string LocalizationKey { get; init; } = "";   // @L[...] label/help key stem
    // Min/max/step for Int/TimeSpan; delegate marker for options owned by another surface (shared Open API app).
    public string? DelegatedToSection { get; init; }     // e.g. "openapi" ⇒ "managed on the Open API settings section"
}
```

### 1.2 The catalog

`WhiteLabelCatalog` — a `static IReadOnlyList<WhiteLabelOption> All` enumerating **every** white-label
knob from the table above, grouped by `WhiteLabelCategory`. Ubiquitous language: call these
**white-label options** / **deployment settings** in code, docs, UI — no "config flags" synonym.

- **Secrets** (`Email.Password`, `Registration.Captcha.Secret`, `Registration.Api.Secret`) are
  `IsSecret=true`, `Kind=Secret` — write-only from UI (blank = keep existing), encrypted at rest.
- **Delegated** options (Open API `SharedApp` creds + `RateLimits`) are listed with
  `DelegatedToSection="openapi"` and `OwnerEditable=false` **in this catalog** so the parity test knows
  they are covered, but the actual editor is the shared-open-api plan's `/settings/openapi` card (no
  duplicate control). See §7.
- Colour options (`Kind=Color`) drive a colour picker; `CustomCss`/`Description` are `MultilineString`.

### 1.3 Why a catalog and not hand-wired forms

The catalog is the seam the **sync guarantee** hangs on (Deliverable 5): a reflection test walks every
property of every white-label option record and asserts a matching `WhiteLabelOption.ConfigPath`
exists. Hand-wired forms cannot be parity-checked; the catalog can.

---

## Deliverable 2 — Effective-value provider (config baseline ⊕ owner override)

Generalize `IFeatureGate` to all kinds.

`Core/WhiteLabel/IWhiteLabelSettings.cs` (pure):

```csharp
public interface IWhiteLabelSettings
{
    // Effective value for an option: owner override if set, else the App:* config baseline, else the default.
    string? GetRaw(string key);                          // raw string form (decrypted for secrets = never exposed to UI)
    bool GetBool(string key);
    // typed helpers: GetInt / GetTimeSpan / GetEnum<T> / GetStringList / GetColor …
    IReadOnlyList<WhiteLabelEffectiveValue> Snapshot();  // catalog entry + effective value + source(Config|Owner|Default) + HasOverride
    Task SetOverrideAsync(string key, string? rawValue, CancellationToken ct);  // null clears → revert to config
}
```

`Infrastructure/WhiteLabel/WhiteLabelSettings.cs`:

- Baseline read from `IOptionsMonitor<AppOptions>.CurrentValue` resolved **through the catalog's
  `ConfigPath`** (a small typed accessor per category — no reflection on the hot path; reflection only
  in the parity test).
- Override rows: `AppSetting` keyed `whitelabel.<Key>` (namespace constant in
  `AppConstants → WhiteLabelSettingsKeys`, mirroring `FeatureSettings`), `IMemoryCache` short TTL,
  `TimeProvider`, cache-clear on write, **one aggregate per `SaveChanges`**.
- **Secrets:** override value encrypted via `ISecretProtector` + a new
  `EncryptionPurposes.WhiteLabelSecret`; `GetRaw` decrypts for server consumers; `Snapshot()` returns
  `HasValue`/masked, **never** the plaintext. Setting blank leaves the stored secret intact.
- **Feature flags stay on `IFeatureGate`** — `IWhiteLabelSettings` **delegates** the `Features.*`
  category to the existing gate (no double store, no migration of `feature.*` rows). The catalog lists
  features with `Category=Features`; the provider routes them to `IFeatureGate` so the Owner UI shows
  them uniformly while the persistence stays where it is. (Alternatively the Features section keeps its
  own page and the catalog marks them `DelegatedToSection="features"` — pick one in §Open questions.)

### 2.1 Consumer rewiring (the breadth of this change)

Replace direct `options.CurrentValue.Branding/Registration/Accounts/Email/…` reads on the
**white-label knobs** with `IWhiteLabelSettings` so owner overrides actually take effect at runtime:

- **Branding/theme** — the theme factory + `Branding/BrandingCss.cs` + the MFA gate + AI provider
  gates (`AllowBuiltInAi`/`AllowLocalProviders`/`AllowedAiProviderKinds`) + calendar hard-gate read
  through the provider. (Theme/CSS recompute per request already; a colour/name override then shows
  live on next render.)
- **Registration** — page/endpoints read `Enabled`/`Mode`/attribute policies through the provider.
  (Note `App:Features:Registration` **and** `App:Registration:Enabled` both gate it today — keep both;
  owner can flip either.)
- **Accounts** — broker allowlist verifier reads `AllowedBrokers` through the provider.
- **Email** — the sender reads host/creds through the provider (secret via the encrypted path).
- **PropFirm** — `DrawdownWarnThresholdPercent` through the provider.

Where a value is only read at **startup** (e.g. options validators), document that the owner override
applies from the **next read**; background workers pick it up on their next cycle (they already use
`IOptionsMonitor`/scoped resolves). Anything genuinely restart-only is marked `OwnerEditable=false`
with a "takes effect on restart" note (avoid pretending a live flip works when it doesn't).

---

## Deliverable 3 — Owner endpoints

`Web/Endpoints/WhiteLabelSettingsEndpoints.cs`, Owner-gated, **not** feature-gated (owner must always
reach it — mirror `FeatureEndpoints`):

```
GET  /api/whitelabel                → catalog + effective values (Snapshot): [{ key, category, kind,
                                       value|masked, source, hasOverride, delegatedToSection }]  (secrets masked)
PUT  /api/whitelabel/{key}          → set an owner override { value }  (blank secret = keep)
DELETE /api/whitelabel/{key}        → clear the override → revert to config baseline
POST /api/whitelabel/reset          → clear ALL overrides (revert deployment to pure config)
```

- Validate `key` against the catalog (unknown ⇒ 404); validate `value` per `Kind` (bad enum/int/colour
  ⇒ 400 with a localized `DomainErrors`-style code). Colour validated by the same rule the theme
  factory uses; list values split/trimmed; `TimeSpan` parsed invariant.
- One override mutation = one `AppSetting` `SaveChanges`.
- Source-gen `LogMessages` for "owner set/cleared white-label option {key}" — **never log the value for
  a secret** (log key + "(secret)").

---

## Deliverable 4 — Owner settings UI (a new "Deployment / White-label" section)

Add a **"Deployment"** (white-label) section to `Components/Dialogs/SettingsDialog.razor`, Owner-only
(`<AuthorizeView Policy="Owner">`), rendering **from the catalog** grouped by `WhiteLabelCategory`.
The shared Open API app stays on the OpenApi section (delegated entries render a link "Managed on the
Open API settings" — no duplicate control).

### 4.1 Layout — responsive tabs (mobile-first) + windowed desktop dialog

Per the owner's UI direction, the Deployment surface is **tabbed, one tab per category** (Branding,
Theme & colours, Features, Registration, Accounts, Email, AI limits, Prop-firm), presented adaptively:

- **Desktop (≥ Md):** a **windowed MudBlazor dialog** — `ShowAsync<DeploymentSettingsDialog>` with
  `DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true }` (a real centered
  window, not full-screen). Inside, a **horizontal `MudTabs`** — one `MudTabPanel` per category; the
  selected tab shows that category's option rows. Resizable/scrollable content region so long
  categories (Theme has ~10 colours) never overflow the window.
- **Mobile (< Md):** the same component goes **full-screen** (`FullScreen = true` below the breakpoint)
  and the tabs render **mobile-first** — either `MudTabs` with `Rounded`/scrollable header (swipe
  through category tabs) or, when tab headers would crowd a 360px width, a top `MudSelect` category
  picker that switches the panel. Category tab headers use short localized labels + icons so they fit;
  no horizontal scroll of the header row beyond an intentional scrollable tab strip.
- **One component, breakpoint-driven** (`MudBreakpointProvider` / `IBrowserViewportService`) — do not
  fork two components; switch `DialogOptions` (`FullScreen` vs windowed) and the tab-header density by
  breakpoint. This keeps the tabs + content identical across form factors (single source of truth,
  single set of E2E selectors).

### 4.2 Editing within a tab

Each category tab lists its options as **rows/cards** (tables→cards on phone, every `MudTd` a
`DataLabel`): option label + `<HelpTip>` + current value + **source chip** ("Config" / "Owner
override" / "Default"). Per house rule *dialogs, never inline page forms*, an edit pencil opens a
nested **MudBlazor edit dialog** `EditWhiteLabelOptionDialog` that adapts its input to `Kind`:
switch → bool, text → string, `MudColorPicker` → colour, `MudSelect` → enum, chips → `StringList`,
password field → secret (write-only), number → int/TimeSpan. A "Revert to config" icon per row clears
that override; a tab-level (and global) **"Reset all to config"** button hits `POST /reset`.

- **Live preview for theme/branding:** after saving a colour/name/logo, the hub reloads branding so
  the change shows without restart (theme/CSS recompute per render; trigger a shell re-render).
- **Mobile-first**, 360px, tables→cards, touch targets ≥44px, no horizontal scroll 320–1920px, design
  tokens only.
- **Fully localized:** every tab label, option label/help/placeholder/aria via `@L["whitelabel.…"]`;
  add keys to `tools/i18n/ui-translations.json` for **every** `Core.Constants.SupportedCultures`, run
  `pwsh tools/i18n/gen-resx.ps1`; hardcoded + parity gates stay green. RTL: tab strip + windowed dialog
  flip correctly (logical CSS; verify `MudTabs` RTL under `MudRTLProvider`).
- New route: if a deep-linkable `/settings/deployment` page is added (recommended, §Open questions) it
  reuses the same tabbed component and goes into `PageSmokeTests` + `MobileLayoutTests` for both roles
  and the no-overflow set. The dialog-hub section is covered by the settings-dialog E2E regardless.

---

## Deliverable 5 — The sync guarantee (config ⇄ owner settings never drift)

Two mechanisms — an automated gate **and** a written mandate.

### 5.1 Reflection parity test (build-failing) — `tests/UnitTests/WhiteLabel/WhiteLabelCatalogParityTests`

- **Every white-label option property is in the catalog.** Reflect over the white-label option records
  (`BrandingOptions`, `FeaturesOptions`, `RegistrationOptions` + nested, `AccountsOptions`,
  `EmailOptions`, the white-label subset of `OpenApiOptions`, `AiBuiltInOptions`, the white-label
  `PropFirmOptions` props) and assert **each public property maps to exactly one
  `WhiteLabelCatalog.All` entry** by `ConfigPath`. A new property with no catalog entry ⇒ **red**.
  - Records/props that are **deliberately not** white-label (pure operational timers like
    `TokenRefreshInterval`, node names, lease TTLs) are declared in an explicit
    `WhiteLabelCatalog.IntentionallyExcluded` allow-list **with a reason string**; the test asserts
    every property is either cataloged **or** excluded — so excluding is a conscious, reviewed act, not
    an accident.
- **Every catalog entry is owner-reachable.** Assert each `OwnerEditable` (non-delegated) entry has a
  localization key present in **all** locales (ties into `ResourceParityTests`) and a matching
  `GET /api/whitelabel` snapshot row (integration test, §6).
- **Every catalog entry has a valid `ConfigPath`.** Assert the path resolves to a real property on
  `AppOptions` (reflection walk) — catches typos and renamed options.

### 5.2 CLAUDE.md mandate (new hard rule)

Add a mandate to the **root `CLAUDE.md`** "Hard mandates" list (and mirror the one-liner in
`src/Core/CLAUDE.md` where options live) — see Deliverable 8 for exact wording. In short: *any new or
changed white-label option (a property on a white-label options record / new `App:*` deployment knob)
MUST be added to `WhiteLabelCatalog` and surfaced in Owner settings in the same commit, or explicitly
listed in `IntentionallyExcluded` with a reason. The parity test enforces it; the doc explains why.*

---

## Deliverable 6 — Tests (all three tiers — mandatory, nothing uncovered)

### Unit (`tests/UnitTests/WhiteLabel`)
- Catalog parity (§5.1): every white-label property cataloged or explicitly excluded; every
  `ConfigPath` resolves; enum/kind metadata correct.
- `WhiteLabelSettings` overlay logic via a fake `AppSetting` store + `FakeTimeProvider`:
  override present ⇒ effective = override + `source=Owner`; absent ⇒ config baseline + `source=Config`;
  clear ⇒ reverts; each `Kind` parses/formats round-trip (bool/int/TimeSpan/enum/list/colour);
  secret override never surfaces plaintext in `Snapshot()`; invalid value rejected.
- Feature delegation: a `Features.*` catalog entry routes to `IFeatureGate` (no separate row written).

### Integration (`tests/IntegrationTests/WhiteLabel`, Testcontainers PG)
- `PUT /api/whitelabel/{key}` writes one `AppSetting` row; `GET` reflects it; `DELETE` reverts;
  `POST /reset` clears all. Secret round-trips through `ISecretProtector` and is **masked** in `GET`.
- Effective value drives a **real consumer**: override `branding.requireMfa=true` ⇒ the MFA gate now
  forces enrollment; override `accounts.allowedBrokers` ⇒ the verifier rejects a disallowed broker;
  override `registration.enabled=false` ⇒ the registration endpoint 404s. (Proves the rewiring in §2.1
  actually takes effect at runtime, not just persists.)
- Non-owner is `403` on every `/api/whitelabel` verb; the endpoint is reachable even when the related
  feature is disabled (not feature-gated).
- Every catalog `OwnerEditable` entry appears in the `GET /api/whitelabel` snapshot (closes the loop
  with the parity unit test at the HTTP layer).

### E2E (`tests/E2ETests`, Playwright, mobile + desktop) — **full coverage, nothing left out**
- **Owner opens Settings → Deployment**: sees a **tab per category**; switching each tab shows that
  category's options; every catalog option renders a row with a source chip. (Drive by iterating the
  catalog via a `data-testid` per key + per category-tab so the test asserts **coverage of every
  option and every tab**, not a hand-picked few — the "nothing uncovered" guarantee at the UI layer.)
- **Responsive shell:** desktop run asserts the Deployment surface is a **windowed dialog** (not
  full-screen) with a horizontal tab strip; mobile-emulation run asserts it is **full-screen** with the
  mobile tab/`MudSelect` category switcher, and every tab's content fits with **no horizontal scroll**
  320–1920px. Tab switching works on both (swipe/tap on mobile, click on desktop).
- **Edit round-trips per kind:** toggle a bool (e.g. `ShowSiteLink`), set a colour (`PrimaryColor`) and
  assert the theme changes live, set a string (`ProductName`) and see it in the app bar/title, set a
  list (`AllowedBrokers`), pick an enum (`Registration.Mode`), set a secret (`Email.Password`) and
  assert it saves + never renders back. Reload → values persist; source chip flips to "Owner override".
- **Revert:** clear one override → chip back to "Config"/"Default", value reverts. "Reset all" clears
  everything.
- **RBAC:** a normal user has **no** Deployment section and `GET /api/whitelabel` returns 403
  (assert via UI absence + an authed API probe).
- **Gating interplay:** disabling `EconomicCalendar` via the owner branding gate hides the calendar
  nav/route (reuse the feature-gate/ErrorBoundary guard pattern).
- **Mobile:** the Deployment section renders as cards, no horizontal scroll 320–1920px; help tips
  tap-open.
- Add the settings-hub Deployment section to the existing settings-dialog E2E and (if a standalone
  route is added) to `PageSmokeTests` + `MobileLayoutTests` for both roles.

`FakeTradingSession` stays cTrader-faithful; no simulator/test weakening. Missing creds never skip a
tier — secret-bearing options (email/captcha) are tested for the persist+mask+gate behavior without a
live send; a live email test (if any) skips clean when SMTP absent.

---

## Deliverable 7 — Interlock with `shared-open-api-app.md`

That plan is being implemented now; avoid collision:

- **This plan does NOT implement the shared Open API app creds or rate-limit editors.** Those live on
  `/settings/openapi` (owner card) per the other plan. Here they appear in `WhiteLabelCatalog` as
  **delegated** entries (`DelegatedToSection="openapi"`, `OwnerEditable=false`) purely so the parity
  test counts them as covered and the Deployment section can render a "Managed on the Open API
  settings" link — **no duplicate control, no second store.**
- **Shared override store:** both plans persist owner runtime settings as `AppSetting` rows /
  encrypted values through `ISecretProtector`. Reuse the same `AppSetting` aggregate + a **distinct key
  namespace** (`whitelabel.*` here vs the shared-app's own keys) so the two never clash. If the shared
  plan lands an `IOwnerSettingsStore` abstraction first, build `IWhiteLabelSettings` **on top of it**
  rather than a parallel store.
- **Sequencing:** land shared-open-api-app first (it is in flight). Then this plan adds the catalog +
  provider + Deployment UI + parity gate, registering the shared-app options as delegated. If this
  plan lands parts first, mark the OpenApi delegated entries `OwnerEditable=false` with a "coming from
  Open API settings" note so nothing dangles.
- **`PublicBaseUrl`** (`App:OpenApi:PublicBaseUrl`) is a genuine white-label value **not** secret and
  **not** the shared-app cred — this plan **can** own it as a normal cataloged option (or delegate it
  to the OpenApi section for locality — §Open questions).

---

## Deliverable 8 — Docs, localization, CLAUDE.md mandate (same commit)

- **New CLAUDE.md hard mandate** (root `CLAUDE.md`, "Hard mandates" list) — proposed wording:

  > **N. White-label options are owner-tunable and always in sync.** Every white-label option (a
  > property on a white-label options record — `BrandingOptions`, `FeaturesOptions`,
  > `RegistrationOptions`, `AccountsOptions`, `EmailOptions`, the white-label subset of
  > `OpenApiOptions`/`AiOptions`/`PropFirmOptions`, or any new `App:*` deployment knob) MUST, in the
  > **same commit**: (a) be registered in `Core/WhiteLabel/WhiteLabelCatalog`, (b) be surfaced in the
  > Owner **Settings → Deployment** section so the owner can change it at runtime exactly as a
  > deployment does via config, and (c) be reachable through `IWhiteLabelSettings` so the override takes
  > effect without a redeploy — **or** be explicitly listed in `WhiteLabelCatalog.IntentionallyExcluded`
  > with a reason (operational-only, restart-only, secret-managed-elsewhere). The build fails
  > (`WhiteLabelCatalogParityTests`) if a white-label option is neither cataloged nor excluded. Never
  > add a config-only white-label flag.

  Mirror a one-line pointer in `src/Core/CLAUDE.md` (options live there) and `tests/CLAUDE.md` (the gate
  lives there).

- **Feature doc:** `website/docs/features/white-label-owner-settings.md` (new) — what the Deployment
  settings section is, that it mirrors every `App:*` white-label knob, the override-beats-config
  precedence, secrets handling, revert/reset, and the interplay with feature toggles + shared Open API
  app. Cross-link `feature-toggles.md`, the branding/white-label deployment doc, and the Open API doc.
- **Deployment doc:** update the config reference to note every `App:Branding/Registration/Accounts/
  Email/…` knob is **also** owner-settable at runtime (override wins), and that owners can `Reset all`
  to fall back to pure config.
- **Localize everything:** mirror all doc changes into every `website/i18n/**` locale; add every new UI
  key to `tools/i18n/ui-translations.json` for all `SupportedCultures`, run `gen-resx.ps1`. Hardcoded +
  resource-parity gates stay green.

---

## Definition of done

- [ ] `dotnet build` clean (0 warnings); analyzer sweep clean on touched projects (`Core`,
      `Infrastructure`, `Web`, `tests`); Rider `get_file_problems` clean on every touched `.cs`/`.razor`.
- [ ] `WhiteLabelCatalog` covers **every** white-label option property (or `IntentionallyExcluded` with
      reason); `WhiteLabelCatalogParityTests` green and **fails** when a new option is unregistered.
- [ ] `IWhiteLabelSettings` overlays owner overrides on the config baseline for every `Kind`
      (bool/string/int/TimeSpan/enum/list/colour/secret); feature flags delegate to `IFeatureGate`;
      secrets encrypted via `ISecretProtector` + `EncryptionPurposes.WhiteLabelSecret`, never surfaced.
- [ ] Consumers (branding/theme, MFA gate, AI gates, registration, accounts allowlist, email,
      prop-firm threshold) read the **effective** value so owner changes take effect without redeploy;
      restart-only knobs marked and labeled honestly.
- [ ] Owner **Settings → Deployment** section renders from the catalog, MudBlazor dialogs for edits,
      source chips, revert + reset-all; Owner-only; mobile-first; no horizontal scroll; RTL renders.
- [ ] `/api/whitelabel` (`GET`/`PUT`/`DELETE`/`reset`) Owner-gated, not feature-gated; validates per
      kind; 403 for non-owners; source-gen logging (no secret values logged).
- [ ] Unit + integration + E2E green, failure paths included; E2E asserts **coverage of every catalog
      option** (per-key `data-testid`), edit-per-kind round-trips, revert/reset, RBAC, gating interplay,
      mobile. New route (if any) in `PageSmokeTests` + `MobileLayoutTests` both roles.
- [ ] No `DateTime.UtcNow`; no magic strings (`WhiteLabelSettingsKeys` constants); config via
      `IOptionsMonitor<AppOptions>`; one aggregate per `SaveChanges`.
- [ ] No hard-coded user-facing text; keys in all locales; parity + hardcoded gates green.
- [ ] New CLAUDE.md mandate added (root + Core + tests pointers); feature + deployment docs written and
      localized into every `website/i18n/` locale; EF: **no schema change** (reuses `AppSetting`) —
      confirm no migration needed.
- [ ] Interlock with `shared-open-api-app.md` honored: shared-app creds + rate limits stay on
      `/settings/openapi` (delegated in the catalog, no duplicate control, shared `AppSetting`/secret
      infra, distinct key namespace).

---

## Open questions / decisions to confirm before coding

1. **Features section: fold or keep?** Route the existing `feature.*` toggles into the new Deployment
   section (uniform UX, one place for the owner) **or** keep `FeatureSettings.razor` as its own section
   and mark features `DelegatedToSection="features"` in the catalog (less churn). *Recommend fold —
   one mental model — with the provider delegating persistence to `IFeatureGate` so no data moves.*
2. **`PublicBaseUrl` ownership:** catalog it here as a normal option, or delegate to the OpenApi
   section for locality? *Recommend catalog here (it is not a secret and not the shared-app cred).*
3. **Standalone `/settings/deployment` route vs dialog-only section:** the settings hub is a dialog
   today; a deep-linkable Owner route is nice for support ("go to /settings/deployment"). *Recommend
   add the route AND include it in the dialog hub; register in `PageSmokeTests` + `MobileLayoutTests`.*
4. **Restart-only options:** confirm which knobs cannot take effect live (startup-only validators) —
   those are `OwnerEditable=true` but labeled "applies after restart", or `OwnerEditable=false`. Audit
   each consumer's read timing during implementation.
5. **Override store abstraction:** if `shared-open-api-app.md` introduces a generic owner-settings
   store first, build `IWhiteLabelSettings` on it; otherwise this plan reuses `AppSetting` directly with
   the `whitelabel.*` namespace. Confirm ordering.
</content>
</invoke>
