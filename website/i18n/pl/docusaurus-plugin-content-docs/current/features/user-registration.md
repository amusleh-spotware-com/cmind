---
description: "Bezpieczna, white-label-gated self-service user registration — on-app sign-up strona i server-to-server provisioning API, z configurable user attributes, admin-approval albo email-verification gating, i anti-abuse guards. Disabled domyślnie."
---

# User registration

Domyślnie **owner/admin dodaje użytkowników ręcznie** (Users strona → *New User*). Dla white-label
deployments które potrzebują onboard użytkowników na skali — albo integrate app z inną serwisem —
cMind również wysyła **bezpieczną, self-service registration** ścieżkę. Jest to **disabled domyślnie**:
stock deployment jest bez zmian i zarówno strona i API zwracają 404 aż deployment opts in.

Są dwa entry points dzielące jeden domain flow:

1. **On-app strona** (`/register`) — branded, mobile-first sign-up strona w tej samej shell jak `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint dla integrating service aby
   tworzyć accounts, authenticated przez per-deployment provisioning secret.

## Co się nagrywamy — data minimization

cMind to trading **tooling**: to buduje/uruchamia/backtesty cBots i mirrors trades across każdego
użytkownika *własne* cTrader Open API credentials. To **nie otwiera trading accounts albo custody client
money**, więc KYC/AML identity verification to **broker'a** obowiązek, nie tej platformy. Registration
form dlatego nagrywamy **tylko email domyślnie** — minimum potrzebne aby provide service (GDPR Art.
5(1)(c) data minimization; lawful basis = contract). cMind umyślnie wysyła **brak** national-ID /
date-of-birth / address fields.

Każdy inny attribute jest **opt-in per deployment** poprzez `App:Registration:Attributes`, każdy
niezależnie `Off` / `Optional` / `Required`:

| Attribute | Notatki |
|---|---|
| `FullName`, `DisplayName`, `Company` | Free text, length-bounded. |
| `Country` | ISO 3166-1 alpha-2, validated przeciwko fixed code set. |
| `Phone` | E.164 format (`+14155552671`). |
| `Locale` | BCP-47 shape (`en-US`), normalized. |
| `MarketingOptIn` | Separate, **unticked** checkbox — nigdy nie bundled z mandatory consent (CAN-SPAM). |
| `AgeConfirmation` | Tylko checkbox; **brak** date of birth jest przechowywany. |

Attributes żyją w `UserProfile` value object owned przez `AppUser` aggregate, validated na
construction. **GDPR erasure** (`AppUser.Anonymize()`) scrubs profil i każdy verification tokens.

**Consent.** Gdy `RequireTermsAcceptance` jest on, użytkownik musi accept published legal documents
(Terms, Privacy, Risk Disclosure). Acceptance jest recorded poprzez existing `ConsentRecord` aggregate —
version-stamped, timestamped, z originating IP — ten sam store użyty gdzie indziej dla MiFID/ESMA-grade
record-keeping.

## Gating modes

Self-registered account nie może zalogować się aż clearing its gate (`App:Registration:Mode`):

- **`AdminApproval`** (domyślnie) — account jest queued; owner/admin approves to na **Users** stronie
  (*Pending approval* section). Potrzebuje brak mail infrastructure.
- **`EmailVerification`** — single-use, expiring verification link jest emailed; account actives gdy
  link jest opened. Requires email transport (`App:Email`). **Jeśli brak transport jest configured,
  ten mode automatycznie downgrades do `AdminApproval`** na startup, więc enabling registration
  nigdy silent nie breaks.
- **`Open`** — account jest active natychmiast (trusted/dev tylko).

Self-registered users są zawsze created jako **`User`** (albo `Viewer` jeśli configured) — domena
**hard-refuses** minting Owner/Admin przez self-registration.

## Security & anti-abuse

- **Anti-enumeration.** Duplicate email yields ten sam **neutral** `202 Accepted` jak fresh sign-up i
  creates nic — app nigdy nie discloses czy address już ma account.
- **Rate limiting.** Public endpoints są throttled per IP (harder niż auth limiter).
- **Password policy.** Minimum length enforced; passwords są hashed (Argon2 poprzez `IPasswordHasher`);
  verification tokens są stored tylko jako SHA-256 hashes i są single-use + expiring.
- **Email hygiene.** Optional allow-list email domains i disposable-provider block-list.
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile poprzez ich shared verify contract.
- **Login gate.** Pending account jest refused na login z neutral response.

## Provisioning API (integration)

Z `App:Registration:Api:Enabled` i `Secret` set, inny service może tworzyć users:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secret jest compared w constant time. Provisioned accounts są created **active** (albo invited z
`MustChangePassword`) zależnie od `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Enabling it

Registration requires **zarówno** feature flag i master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // albo EmailVerification / Open
    "DefaultRole": "User",             // nigdy Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Sekcja `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) configures transport używany przez `EmailVerification` mode; leave `Host` unset aby
uruchamiać z brak mail (no-op sender). Zobacz [feature toggles](./feature-toggles.md) i
[white-label](./white-label.md) dla jak deployments turn features on i rebrand. Gdy registration
jest enabled, login strona pokazuje **Create account** link.

## Tested

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use tokens,
erasure), integration (disabled-by-default 404, approval flow, email-verification downgrade,
anti-enumeration, abuse guards, required attributes, provisioning + bad secret), i E2E (default-off
login ma brak sign-up link; `/register` strona renderuje jej branded closed state).
