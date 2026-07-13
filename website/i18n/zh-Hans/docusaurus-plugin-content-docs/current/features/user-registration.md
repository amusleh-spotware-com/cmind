---
description: "Secure, white-label-gated self-service user registration — an on-app sign-up page and a server-to-server provisioning API, with configurable user attributes, admin-approval or email-verification gating, and anti-abuse guards. Disabled by default."
---

# User registration

By default the **owner/admin adds users manually** (Users page → *New User*). For white-label deployments
that need to onboard users at scale — or integrate the app with another service — cMind also ships a
**secure, self-service registration** path. It is **disabled by default**: a stock deployment is unchanged
and the page and API both return 404 until a deployment opts in.

There are two entry points sharing one domain flow:

1. **On-app page** (`/register`) — a branded, mobile-first sign-up page in the same shell as `/login`.
2. **Provisioning API** (`POST /api/provision`) — a server-to-server endpoint for an integrating service to
   create accounts, authenticated by a per-deployment provisioning secret.

## What gets recorded — data minimization

cMind is trading **tooling**: it builds/runs/backtests cBots and mirrors trades over each user's *own*
cTrader Open API credentials. It **does not open trading accounts or custody client money**, so KYC/AML
identity verification is the **broker's** obligation, not this platform's. The registration form therefore
records **only an email by default** — the minimum needed to provide the service (GDPR Art. 5(1)(c) data
minimization; lawful basis = contract). cMind deliberately ships **no** national-ID / date-of-birth /
address fields.

Every other attribute is **opt-in per deployment** via `App:Registration:Attributes`, each independently
`Off` / `Optional` / `Required`:

| Attribute | Notes |
|---|---|
| `FullName`, `DisplayName`, `Company` | Free text, length-bounded. |
| `Country` | ISO 3166-1 alpha-2, validated against a fixed code set. |
| `Phone` | E.164 format (`+14155552671`). |
| `Locale` | BCP-47 shape (`en-US`), normalized. |
| `MarketingOptIn` | Separate, **unticked** checkbox — never bundled with the mandatory consent (CAN-SPAM). |
| `AgeConfirmation` | A checkbox only; **no** date of birth is stored. |

Attributes live in the `UserProfile` value object owned by the `AppUser` aggregate, validated at
construction. **GDPR erasure** (`AppUser.Anonymize()`) scrubs the profile and any verification tokens.

**Consent.** When `RequireTermsAcceptance` is on, the user must accept the published legal documents
(Terms, Privacy, Risk Disclosure). Acceptance is recorded through the existing `ConsentRecord` aggregate —
version-stamped, timestamped, with originating IP — the same store used elsewhere for MiFID/ESMA-grade
record-keeping.

## Gating modes

A self-registered account cannot sign in until it clears its gate (`App:Registration:Mode`):

- **`AdminApproval`** (default) — the account is queued; an owner/admin approves it on the **Users** page
  (*Pending approval* section). Needs no mail infrastructure.
- **`EmailVerification`** — a single-use, expiring verification link is emailed; the account activates when
  the link is opened. Requires an email transport (`App:Email`). **If no transport is configured, this mode
  automatically downgrades to `AdminApproval`** at startup, so enabling registration never silently breaks.
- **`Open`** — the account is active immediately (trusted/dev only).

Self-registered users are always created as **`User`** (or `Viewer` if configured) — the domain
**hard-refuses** minting an Owner/Admin through self-registration.

## Security & anti-abuse

- **Anti-enumeration.** A duplicate email yields the **same** neutral `202 Accepted` as a fresh sign-up and
  creates nothing — the app never discloses whether an address already has an account.
- **Rate limiting.** The public endpoints are throttled per IP (harder than the auth limiter).
- **Password policy.** Minimum length enforced; passwords are hashed (Argon2 via `IPasswordHasher`);
  verification tokens are stored only as SHA-256 hashes and are single-use + expiring.
- **Email hygiene.** Optional allow-list of email domains and a disposable-provider block-list.
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile via their shared verify contract.
- **Login gate.** A pending account is refused at login with a neutral response.

## Provisioning API (integration)

With `App:Registration:Api:Enabled` and a `Secret` set, another service can create users:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

The secret is compared in constant time. Provisioned accounts are created **active** (or invited with
`MustChangePassword`) depending on `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Enabling it

Registration requires **both** the feature flag and the master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // or EmailVerification / Open
    "DefaultRole": "User",             // never Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

The `App:Email` section (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) configures the transport used by `EmailVerification` mode; leave `Host` unset to run with no
mail (the no-op sender). See [feature toggles](./feature-toggles.md) and [white-label](./white-label.md) for
how deployments turn features on and rebrand. When registration is enabled, the login page shows a **Create
account** link.

## Tested

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use tokens, erasure),
integration (disabled-by-default 404, approval flow, email-verification downgrade, anti-enumeration, abuse
guards, required attributes, provisioning + bad secret), and E2E (default-off login has no sign-up link; the
`/register` page renders its branded closed state).

<!-- [ZH-HANS] Translation needed -->
