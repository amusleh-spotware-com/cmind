# Plan — Secure User Registration (Self-Service + API), White-Label Gated

Status: **PLAN ONLY — not implemented.** Author target: cMind main.
New feature flag: `Registration` (see §7). New config section: `App:Registration` (`RegistrationOptions`).
Disabled by default. White-label controls enablement, collected attributes, gating mode, and default role.

---

## 0. One-paragraph summary

Today the Owner/Admin adds users by hand (`POST /api/users`, `NewUserDialog`). For white-label resellers
we add a **secure, self-service registration path** with two entry points that share one domain flow:

1. **On-app registration page** (`/register`) — a branded, mobile-first pre-auth page (same shell as
   `/login`), disabled by default, enabled and customized via white-label config.
2. **Registration API** (`POST /api/register`) — for other services to provision accounts into the app
   (integration mode), authenticated by a per-deployment provisioning secret.

Both funnel into a new `AppUser.SelfRegister(...)` factory that creates a **RegularUser** (role
configurable, never Owner/Admin), captures a **white-label-configurable set of user attributes**
(name/country/phone/company/…), records **legal-document consent** (reusing the existing
`ConsentRecord`/`LegalDocument` compliance aggregate), and — depending on configured gating mode — leaves
the account `PendingEmailVerification` or `PendingApproval` before it becomes `Active`. Every attribute the
business records is opt-in per the white-label `AttributePolicy` (Off / Optional / Required), designed
around **data minimization** and the fact that this SaaS does **not custody funds** (KYC/AML is the
broker's obligation, not the app's — see §1.3). Security follows industry standard: rate limiting, generic
anti-enumeration responses, password strength policy, optional CAPTCHA + disposable-email blocking, audited.

**Reuse, don't reinvent:** `AppUser` aggregate + role hierarchy, `Email` value object, `IPasswordHasher`,
`RateLimitPolicies.Auth`, `RequireFeature`/`FeatureFlag` gate, `BrandingOptions`/theme, `EmptyLayout`
(login shell), `LegalDocument`/`ConsentRecord` compliance, `Anonymize()` GDPR erasure. New machinery:
`RegistrationOptions`, `UserProfile` value object, registration state on `AppUser`, `IEmailSender`
abstraction (the one genuinely new cross-cutting piece — see §5), optional `ICaptchaValidator`.

---

## 1. Research findings — how others do it, what the law requires

### 1.1 Self-registration industry standard (Auth0 / Okta / AWS Cognito / Keycloak / OWASP ASVS)

The consensus flow for SaaS self sign-up:

1. **Collect minimum:** email + password (+ only the attributes the business truly needs).
2. **Account starts inactive** until verified — one of:
   - **Double opt-in email verification** (most common): send a one-time, single-use, expiring token link;
     account activates on click. Prevents typo/throwaway/someone-else's-email signups.
   - **Admin approval**: account queued `Pending`, an Owner/Admin approves. Common for B2B / closed
     platforms and when no mail infra exists.
3. **Anti-abuse at the endpoint:** rate limiting per IP + per email, CAPTCHA / proof-of-work for public
   forms, disposable-email domain blocking, bot detection.
4. **Anti-enumeration:** never reveal whether an email already exists. Return the **same** "check your
   inbox" response for new and duplicate emails; for duplicates, send a "you already have an account"
   mail instead of erroring (OWASP WSTG-IDNT / ASVS 2.2.x). The synchronous API path returns a neutral
   `202 Accepted`.
5. **Password policy = NIST SP 800-63B:** min length ≥ 8 (recommend 12), allow all chars incl. spaces,
   **no forced composition rules, no periodic rotation**, screen against known-breached/common-password
   lists. (We already hash via `IPasswordHasher`; add a strength/҂breach check, drop complexity theater.)
6. **Consent at point of collection:** explicit, unbundled checkboxes for Terms + Privacy (+ Risk
   Disclosure for a trading product), version-stamped, timestamp + IP retained.
7. **Transport + storage:** HTTPS only, hashed passwords (already Argon-class via `IPasswordHasher`),
   verification tokens stored hashed, single-use, short TTL.

### 1.2 Provisioning-by-API standard (integration mode)

For "let another service create users in the app": expose a server-to-server endpoint authenticated by a
**provisioning secret** (a per-deployment API key, or OAuth2 client-credentials). This path may **skip**
email verification (the calling service vouches for the user) and set the account `Active` directly, or
issue a `MustChangePassword` invite. Mirrors SCIM-style user provisioning and Cognito `AdminCreateUser`.
Key differences from the public form: no CAPTCHA, higher rate limit, caller identity is audited, and the
initial password can be a server-generated temporary one.

### 1.3 Legal / regulatory — which attributes MUST be recorded

**Critical framing:** cMind is a **tooling/automation SaaS** (build/run/backtest cBots, copy trades over
a user's *own* cTrader Open API credentials). It **does not open trading accounts, custody client money,
or execute as principal** — the regulated broker does. Therefore:

- **KYC / AML customer due diligence does NOT bind this app.** Identity verification (passport, national
  ID, proof of address, source of funds) is the **broker's** obligation under AML directives. The app must
  **not** collect that data by default — collecting regulated PII you don't need creates liability, not
  compliance. (If a reseller *is* itself a regulated entity and wants KYC, that's a separate, explicitly
  enabled, high-sensitivity module — out of scope here; §11 open question.)
- **GDPR / UK-GDPR (data-minimization, Art. 5(1)(c)):** collect only data adequate, relevant, and limited
  to what's necessary. Lawful basis for the collected attributes = **contract** (Art. 6(1)(b), to provide
  the service) + **consent** for anything optional. Users get access/rectification/erasure — `Anonymize()`
  already implements erasure; registration must extend it to scrub the new profile attributes too.
- **ePrivacy / consent logging:** record which legal-document version was accepted, when, and from which IP
  — the existing `ConsentRecord` aggregate already does exactly this (MiFID/ESMA-grade record-keeping). We
  reuse it; no new consent store.
- **CFD/FX risk disclosure:** a trading-adjacent product should surface a risk warning + require
  acknowledgment at signup. `LegalDocumentType.RiskDisclosure` already exists — wire it into the consent
  gate.
- **CAN-SPAM / marketing:** marketing-email opt-in must be **separate and unticked by default**; never
  bundled with the mandatory terms consent. Model as its own optional attribute.
- **Age:** if a deployment operates where a minimum age applies, an age-confirmation checkbox (not DOB
  storage) is the minimal approach. Optional attribute, off by default.

**Conclusion on attributes:** the only attribute the app *needs* is **email**. Everything else
(display/full name, country, phone, company, locale, marketing opt-in, age confirmation) is **business
preference**, each independently switchable Off / Optional / Required by white-label. Default profile =
email only. We deliberately ship **no** national-ID/DOB/address fields; a reseller that needs regulated
KYC must enable a separate module with its own lawful basis (not in this plan).

---

## 2. Domain model changes (`src/Core`)

### 2.1 `UserProfile` value object

A new immutable value object (owned by `AppUser`) holding the optional, business-configurable attributes.
All fields nullable; each wrapped/validated to its format:

- `FullName?` (given + family, or a single display string) — trimmed, length-bounded.
- `DisplayName?`
- `CountryCode?` — ISO 3166-1 alpha-2, validated against a known set.
- `PhoneNumber?` — E.164 format value object, validated.
- `Company?`
- `Locale?` / `TimeZoneId?` — for UI personalization (validated against known IDs).
- `MarketingOptIn` (bool, default false) — separate from consent.
- `AgeConfirmed` (bool, default false).

Guarded construction throws `DomainException` with new `DomainErrors` on malformed country/phone/etc.
Empty profile (all null, opt-ins false) is valid — that's the default deployment.

### 2.2 `AppUser` registration state

Add a `RegistrationStatus` concept so a self-registered account isn't usable until it clears its gate:

- `enum UserActivationState { Active, PendingEmailVerification, PendingApproval }`.
- `AppUser.ActivationState` (private set; default `Active` for owner-/admin-created users — unchanged
  behavior).
- `AppUser.Profile` (the `UserProfile` VO; default empty).
- New intention-revealing methods (no public setters):
  - `AppUser.SelfRegister(...)` factory on `RegularUser` (and honoring a configurable default role, but
    **hard-refusing Owner/Admin**): sets a caller-supplied password hash, `MustChangePassword = false`
    (they chose it), attaches `UserProfile`, sets initial `ActivationState` per gating mode, raises
    `UserRegisteredDomainEvent`.
  - `ConfirmEmail(now)` → `PendingEmailVerification` → `Active` (or → `PendingApproval` if both gates on),
    raises `UserEmailConfirmedDomainEvent`; idempotent/guarded.
  - `Approve(approvedBy, now)` → `PendingApproval` → `Active`, raises `UserApprovedDomainEvent`.
  - `UpdateProfile(newProfile)` for self-service profile edits later.
- Login (`AuthEndpoints`) must **reject** a non-`Active` user with a specific, non-enumerating reason
  ("account not yet verified / awaiting approval"), and the `AdminOrAbove` user-list surfaces pending
  accounts for approval.

### 2.3 Email verification token (owned entity)

`EmailVerificationToken` owned by `AppUser` (mirrors `MfaBackupCode` shape): stores a **hash** of the
token, `ExpiresAt`, `UsedAt`. Domain methods `IssueEmailVerificationToken(hash, expiresAt)` and
`RedeemEmailVerificationToken(hash, now)` (single-use, expiry-checked). Plaintext token only ever leaves
in the emailed link, never stored.

### 2.4 GDPR erasure

Extend `AppUser.Anonymize()` to also null/scrub `Profile` (name, phone, company, country) and clear
verification tokens — keeps erasure complete for the new PII.

### 2.5 Domain events

`UserRegisteredDomainEvent`, `UserEmailConfirmedDomainEvent`, `UserApprovedDomainEvent` — enable side
effects (send verification/welcome mail, notify Owner of a pending approval) via the existing domain-event
dispatch without coupling the aggregate to infrastructure. One `SaveChanges` per aggregate preserved.

---

## 3. Configuration — `RegistrationOptions` (`App:Registration`)

New `sealed record RegistrationOptions` in `src/Core/Options`, added to `AppOptions`. Every value defaults
to the safe/disabled baseline so an unconfigured deployment is unchanged (registration OFF).

```
App:Registration
  Enabled                    = false          // master switch (disabled by default)
  Mode                       = EmailVerification | AdminApproval | Open
                                                // default: AdminApproval when no email configured,
                                                // else EmailVerification. Open = active immediately (dev/trusted).
  DefaultRole                = User            // never Owner/Admin; validated
  RequireTermsAcceptance     = true            // gates on ToS + Privacy (+ RiskDisclosure) consent
  AllowedEmailDomains        = []              // empty = any; else allow-list (B2B tenanting)
  BlockDisposableEmail       = true
  Captcha                    = { Enabled=false, Provider, SiteKey, SecretRef }   // off by default
  TokenLifetime              = 24h             // email-verification token TTL
  Attributes:                                  // per-attribute white-label policy
    FullName      = Optional                   // Off | Optional | Required
    DisplayName   = Off
    Country       = Off
    Phone         = Off
    Company       = Off
    Locale        = Off
    MarketingOptIn= Off                        // when on, renders an unticked opt-in checkbox
    AgeConfirm    = Off
  Api:
    Enabled       = false                      // server-to-server provisioning endpoint
    SecretRef     = <encrypted provisioning secret>   // via ISecretProtector / EncryptionPurposes
    ActivateImmediately = true                 // API-created accounts skip verification
    InviteMustChangePassword = false
```

`RegistrationOptionsValidator` (mirrors `BrandingOptionsValidator`): rejects `DefaultRole` of Owner/Admin,
validates email domains, requires a `SecretRef` when `Api.Enabled`, warns if `Mode=EmailVerification` but
no `IEmailSender` is configured (falls back to `AdminApproval`). Bound + validated at startup like the
existing options.

**White-label management:** because these live under `App:Registration` in config (same as
`App:Branding`/`App:Features`), a reseller enables/customizes registration purely by configuration — no
code change. The Attributes map is exactly "what the business wants to record and what not."

---

## 4. API surface (`src/Web/Endpoints/RegistrationEndpoints.cs`)

All routes gated: return **404** (not 403 — don't reveal the feature exists) when
`Registration.Enabled` is false, via a small `RequireRegistrationEnabled` filter alongside the existing
`RequireFeature(FeatureFlag.Registration)`.

- `GET  /api/register/config` (anonymous) — returns the white-label attribute policy + which consents are
  required, so the page/an integrating client renders the correct fields. No secrets.
- `POST /api/register` (anonymous, `RateLimitPolicies.Registration`, antiforgery for form post) — the
  public self-service path. Validates attributes against policy, checks allowed domains + disposable
  block + CAPTCHA (if enabled), enforces password policy, creates the user via `SelfRegister`, records
  consent, issues+sends verification token (EmailVerification mode) or queues for approval. **Always**
  returns a neutral `202 Accepted` "check your inbox / pending approval" regardless of duplicate email
  (anti-enumeration). Duplicate → send "you already have an account" mail instead.
- `GET  /api/register/verify?token=…` (anonymous) — redeems the email token, activates the account,
  redirects to `/login?verified=1`. Generic failure page on bad/expired token.
- `POST /api/register/resend` (anonymous, rate-limited) — resend verification; neutral response.
- `POST /api/provision` (server-to-server, `Registration.Api.Enabled`, provisioning-secret auth via a
  header checked with `ISecretProtector`, own rate limit) — integration mode: create an active (or
  invited) user, audited with the caller identity. Returns the created `UserId`.

Admin-side (extends existing `/api/users`, `AdminOrAbove`):

- `GET  /api/users/pending` — list `PendingApproval` accounts.
- `POST /api/users/{id}/approve` — `AppUser.Approve(...)`.
- Reject/delete reuses existing `DELETE /api/users/{id}`.

New route(s) added to `PageSmokeTests` per the test mandate.

---

## 5. Email delivery — the one genuinely new cross-cutting piece

CLAUDE.md records **"no email/SMTP (manual reset via `MustChangePassword`)"** as *deliberately not done*.
Registration's EmailVerification mode needs outbound mail, so this plan **reverses that decision behind a
config flag** and adds a minimal, pluggable abstraction — flagged here for explicit sign-off:

- `IEmailSender` in `src/Core` (interface only — Core stays pure) with a `SendAsync(EmailMessage, ct)`.
- Infrastructure implementations: a **no-op/logging** default (logs via source-generated `LogMessages`,
  never sends) so the app + all tests run with **zero** mail config; a real **SMTP**/provider sender
  wired only when `App:Email` is configured. Secrets via `ISecretProtector`.
- **Graceful degradation:** if `Mode=EmailVerification` but no real sender is configured, the validator
  downgrades to `AdminApproval` at startup and logs a warning — so enabling registration never silently
  breaks. This keeps "no mail infra" a first-class, supported deployment.

If the user prefers to keep SMTP strictly out, we ship **AdminApproval + API-provision only** in phase 1
and defer EmailVerification — see §10 phasing. (Open question §11.)

---

## 6. UI (`src/Web`)

- **`/register` page** — a pre-auth **page** (not a dialog; the dialog mandate is for in-app add/edit —
  `/login` is the precedent for pre-auth pages), using `EmptyLayout` + `BrandingThemeProvider` so it
  inherits product name, logo, and the deployment's color tokens automatically. Mobile-first, 360px, no
  horizontal scroll, design tokens only.
- Renders fields dynamically from `GET /api/register/config` (only the attributes the white-label enabled;
  required vs optional per policy). Unticked, separate checkboxes for Terms/Privacy/Risk consent and (if
  enabled) marketing opt-in.
- Client + server validation; on submit shows the neutral "check your inbox / awaiting approval" state.
- **Login page** gains a "Create account" link **only when `Registration.Enabled`** (branding-aware).
- **Users admin page** gains a "Pending approval" section with approve/reject actions (MudBlazor, in-page
  list + confirm dialog per the dialog mandate for the approve action).
- Everything hidden by default (feature off) — zero UI change for a stock/owner-managed deployment.

---

## 7. Feature gating & white-label wiring

- Add `FeatureFlag.Registration` to the `FeatureFlag` enum + `FeaturesOptions` (`Registration` bool).
  **Default: `false`** (unlike other features which default true) — registration stays off unless a
  deployment opts in. Both `FeaturesOptions.Registration` *and* `RegistrationOptions.Enabled` must be true;
  `RegistrationOptions.Enabled` is the primary switch, the feature flag keeps nav/endpoint gating uniform
  with the rest of the app.
- `RegistrationEndpoints` group uses `.RequireFeature(FeatureFlag.Registration)` + the enabled filter.
- DI registration in `src/Web`/`Infrastructure` `DependencyInjection` for `IEmailSender`,
  `ICaptchaValidator` (no-op default), options binding + validator.

---

## 8. Persistence / migration (`src/Infrastructure`)

- EF config: `AppUser` owns `UserProfile` (owned type / columns), `ActivationState` (enum→int),
  `EmailVerificationToken` (owned collection like `MfaBackupCode`), indexes on token hash + activation
  state (partial index for pending-approval queries).
- One EF migration via the `migration` skill / canonical layout:
  `dotnet ef migrations add AddUserRegistration -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations`.
- New `DomainErrors` constants (invalid country/phone/role, disposable email, token expired, registration
  disabled, duplicate handled neutrally, etc.) in `Core/Constants/DomainErrors.cs`.

---

## 9. Tests (three tiers — mandatory, failure paths included)

**Unit (`tests/UnitTests`):**
- `UserProfile` validation (country/phone/locale good + bad).
- `AppUser.SelfRegister` — default role honored, **Owner/Admin refused**, initial state per mode,
  `MustChangePassword=false`, event raised.
- `ConfirmEmail` / `Approve` transitions, idempotency, guard on already-active.
- `EmailVerificationToken` single-use + expiry.
- `Anonymize()` scrubs new profile fields.
- `RegistrationOptionsValidator` — rejects Owner/Admin default role, requires API secret, downgrade logic.

**Integration (`tests/IntegrationTests`, real Postgres):**
- Full public registration → token → verify → active; approval-mode path; API-provision path.
- **Anti-enumeration:** duplicate email returns identical `202` + no new row.
- Allowed-domain reject, disposable-email reject, CAPTCHA-fail reject.
- Rate-limit trips after N attempts (per IP + per email).
- Login blocked while `PendingEmailVerification`/`PendingApproval`; allowed after activation.
- Registration endpoints return **404** when feature disabled (default).
- Provisioning endpoint rejects bad/missing secret.
- Consent rows written with correct version + IP.
- `IEmailSender` no-op path leaves app fully working; verification-mode-without-sender downgrades to
  approval.

**E2E (`tests/E2ETests`, Playwright, mobile + desktop):**
- `/register` renders only white-label-enabled fields, brand theme applied, mobile 360px clean.
- Happy-path signup → neutral confirmation; login link appears only when enabled.
- Owner approves a pending user; that user can then log in.
- New routes added to `PageSmokeTests`.

**Stress/DST:** not applicable (no concurrent trading state); rely on integration rate-limit + race tests
for duplicate-email concurrency (two simultaneous signups, same email → one row, both get neutral 202).

---

## 10. Phased rollout

- **Phase 0 — Domain + config (no endpoints):** `UserProfile`, activation state, `SelfRegister`/`Approve`,
  `RegistrationOptions` + validator, `DomainErrors`, migration, unit tests. Ships dark (flag off).
- **Phase 1 — AdminApproval + API provisioning:** `RegistrationEndpoints` (public form → pending),
  approval admin endpoints/UI, `POST /api/provision`, `/register` page. **No SMTP needed** — works with
  the no-op sender. Integration + E2E. This alone satisfies "enable registration, white-label gated" and
  respects the current no-mail stance.
- **Phase 2 — Email verification:** `IEmailSender` + SMTP impl (`App:Email`), verification token flow,
  resend, welcome/duplicate mails, downgrade logic. Gated behind mail config.
- **Phase 3 — Hardening extras:** CAPTCHA provider, disposable-email blocklist source, breached-password
  screening, marketing opt-in + age confirmation attributes.

Each phase is independently shippable and green across the tiers before "done".

---

## 11. Docs (same commit)

- `website/docs/features/user-registration.md` — canonical feature doc: enabling it, the white-label
  attribute policy, gating modes, the data-minimization/regulatory stance (§1.3), API-provisioning guide.
- `website/docs/deployment/` — `App:Registration` + `App:Email` config reference; note the default-off,
  default-approval-when-no-mail behavior.
- `website/docs/operations/` — approving pending users; anti-abuse knobs.
- Update the "Deliberately not done" note in `CLAUDE.md` (email/SMTP) to reflect the new opt-in sender.

---

## 12. Open questions (need a decision before build)

1. **Email/SMTP:** OK to add the opt-in `IEmailSender` (reversing the "no SMTP" call, config-gated), or
   ship **Phase 1 only** (AdminApproval + API provisioning, zero mail) and defer email verification?
2. **Default gating mode** when registration is enabled: AdminApproval (safest, no mail) or
   EmailVerification (needs mail)? Recommendation: **AdminApproval** default, auto-upgrade if mail
   configured.
3. **Default self-registered role:** `User` (recommended) vs `Viewer`?
4. **Regulated KYC module:** confirm it's **out of scope** (broker's duty) — app collects no
   ID/DOB/address by default. A reseller needing KYC gets a separate future module with its own lawful
   basis.
5. **API provisioning auth:** simple per-deployment provisioning secret (recommended, matches app's
   secret-handling) vs full OAuth2 client-credentials?
6. **Multi-tenant scoping:** is a self-registered user global to the deployment, or must registration be
   scoped/branded per reseller-tenant? (Affects `AllowedEmailDomains` semantics.)
