---
description: "Secure, white-label-gated self-service user registration — on-app sign-up page a server-to-server provisioning API, s konfigurovatelnými uživatelskými atributy, admin-approval nebo email-verification gating, a anti-abuse guards. Defaultně zakázáno."
---

# Registrace uživatele

Defaultně **owner/admin přidává uživatele manuálně** (Users page → *New User*). Pro white-label deploymenty
 které potřebují onboardovat uživatele ve velkém — nebo integrovat aplikaci s jinou službou — cMind také dodává a
**secure, self-service registration** path. Je **defaultně zakázána**: stock deployment je nezměněn
a stránka i API obě vracejí 404 dokud deployment neoptuje.

Existují dva vstupní body sdílející jeden doménový flow:

1. **On-app page** (`/register`) — branded, mobile-first sign-up page in the same shell as `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint pro integrační službu k
   vytváření účtů, authenticated by a per-deployment provisioning secret.

## Co se zaznamenává — minimalizace dat

cMind je trading **tooling**: builds/runs/backtests cBots and mirrors trades over each user's *own*
cTrader Open API credentials. **Neotevírá trading účty ani nesvěřuje client money**, takže KYC/AML
identity verification is the **broker's** obligation, not this platform's. Registrační formulář therefore
zaznamenává **pouze email defaultně** — minimum needed to provide the service (GDPR Art. 5(1)(c) data
minimization; lawful basis = contract). cMind deliberate shipuje **žádné** national-ID / date-of-birth /
address fields.

Každý jiný atribut je **opt-in per deployment** přes `App:Registration:Attributes`, each independently
`Off` / `Optional` / `Required`:

| Atribut | Poznámky |
|---|---|
| `FullName`, `DisplayName`, `Company` | Free text, length-bounded. |
| `Country` | ISO 3166-1 alpha-2, validated against fixed code set. |
| `Phone` | E.164 format (`+14155552671`). |
| `Locale` | BCP-47 shape (`en-US`), normalized. |
| `MarketingOptIn` | Separate, **unticked** checkbox — never bundled with mandatory consent (CAN-SPAM). |
| `AgeConfirmation` | A checkbox only; **no** date of birth is stored. |

Atributy žijí v `UserProfile` value object owned by `AppUser` aggregate, validated at
construction. **GDPR erasure** (`AppUser.Anonymize()`) scrubs profile and any verification tokens.

**Consent.** Když `RequireTermsAcceptance` is on, user must accept published legal documents
(Terms, Privacy, Risk Disclosure). Acceptance is recorded through existing `ConsentRecord` aggregate —
version-stamped, timestamped, with originating IP — same store used elsewhere for MiFID/ESMA-grade
record-keeping.

## Gating módy

Self-registered account cannot sign in until it clears its gate (`App:Registration:Mode`):

- **`AdminApproval`** (default) — účet je ve frontě; owner/admin ho schválí na **Users** page
  (*Pending approval* section). Potřebuje žádnou mail infrastrukturu.
- **`EmailVerification`** — a single-use, expiring verification link is emailed; účet se aktivuje když
  link is opened. Vyžaduje email transport (`App:Email`). **If no transport is configured, this mode
  automatically downgrades to `AdminApproval`** at startup, takže enabling registration never silently breaks.
- **`Open`** — účet je aktivní okamžitě (trusted/dev only).

Self-registered users are always created as **`User`** (or `Viewer` if configured) — domain
**hard-refuses** minting Owner/Admin through self-registration.

## Bezpečnost & anti-abuse

- **Anti-enumeration.** Duplikátní email vrací **stejnou** neutral `202 Accepted` as fresh sign-up and
  creates nothing — app never discloses whether an address already has an account.
- **Rate limiting.** Public endpoints are throttled per IP (harder than the auth limiter).
- **Password policy.** Minimum length enforced; passwords are hashed (Argon2 via `IPasswordHasher`);
  verification tokens are stored only as SHA-256 hashes and are single-use + expiring.
- **Email hygiene.** Optional allow-list of email domains and a disposable-provider block-list.
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile via their shared verify contract.
- **Login gate.** Pending účet je odmítnut při loginu s neutral response.

## Provisioning API (integrace)

S `App:Registration:Api:Enabled` a `Secret` set, another service can create users:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secret is compared in constant time. Provisioned accounts are created **active** (or invited with
`MustChangePassword`) depending on `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Enabling it

Registrace vyžaduje **obojí** feature flag a master switch:

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

`App:Email` section (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) konfiguruje transport used by `EmailVerification` mode; leave `Host` unset to run with no
mail (no-op sender). Viz [feature toggles](./feature-toggles.md) a [white-label](./white-label.md) for
how deployments turn features on and rebrand. When registration is enabled, login page shows a **Create
account** link.

## Testováno

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use tokens, erasure),
integration (disabled-by-default 404, approval flow, email-verification downgrade, anti-enumeration, abuse
guards, required attributes, provisioning + bad secret), and E2E (default-off login has no sign-up link; `/register` page renders its branded closed state).
