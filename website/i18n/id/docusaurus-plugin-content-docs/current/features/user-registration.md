---
description: "Secure, white-label-gated self-service user registration — on-app sign-up page dan server-to-server provisioning API, dengan configurable user attributes, admin-approval atau email-verification gating, dan anti-abuse guards. Disabled by default."
---

# User registration

Secara default **owner/admin menambahkan users secara manual** (Users page → *New User*). Untuk white-label deployments
yang memerlukan onboard users dalam skala — atau mengintegrasikan app dengan layanan lain — cMind juga mengirim
**secure, self-service registration** path. Ini **disabled by default**: stock deployment tidak berubah
dan page dan API keduanya return 404 hingga deployment opt in.

Ada dua entry points yang share one domain flow:

1. **On-app page** (`/register`) — branded, mobile-first sign-up page dalam shell yang sama seperti `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint untuk integrating service untuk
   membuat accounts, authenticated oleh per-deployment provisioning secret.

## Apa yang direkam — data minimization

cMind adalah trading **tooling**: builds/runs/backtests cBots dan mirrors trades di atas setiap user's *own*
cTrader Open API credentials. Ini **tidak membuka trading accounts atau custody client money**, jadi KYC/AML
identity verification adalah **broker's** obligation, bukan platform ini punya. Registration form karena itu
merekam **hanya email secara default** — minimum yang diperlukan untuk menyediakan layanan (GDPR Art. 5(1)(c) data
minimization; lawful basis = contract). cMind deliberately mengirim **no** national-ID / date-of-birth /
address fields.

Setiap other attribute adalah **opt-in per deployment** via `App:Registration:Attributes`, masing-masing independently
`Off` / `Optional` / `Required`:

| Attribute | Notes |
|---|---|
| `FullName`, `DisplayName`, `Company` | Free text, length-bounded. |
| `Country` | ISO 3166-1 alpha-2, validated terhadap fixed code set. |
| `Phone` | E.164 format (`+14155552671`). |
| `Locale` | BCP-47 shape (`en-US`), normalized. |
| `MarketingOptIn` | Separate, **unticked** checkbox — tidak pernah bundled dengan mandatory consent (CAN-SPAM). |
| `AgeConfirmation` | Checkbox hanya; **no** date of birth disimpan. |

Attributes tinggal dalam `UserProfile` value object dimiliki oleh `AppUser` aggregate, divalidasi pada
construction. **GDPR erasure** (`AppUser.Anonymize()`) scrubs profile dan any verification tokens.

**Consent.** Ketika `RequireTermsAcceptance` adalah on, user harus accept published legal documents
(Terms, Privacy, Risk Disclosure). Acceptance direkam melalui existing `ConsentRecord` aggregate — version-stamped,
timestamped, dengan originating IP — same store yang digunakan di tempat lain untuk MiFID/ESMA-grade
record-keeping.

## Gating modes

Self-registered account tidak dapat sign in hingga clears its gate (`App:Registration:Mode`):

- **`AdminApproval`** (default) — account adalah queued; owner/admin approves itu pada **Users** page
  (*Pending approval* section). Tidak memerlukan mail infrastructure.
- **`EmailVerification`** — single-use, expiring verification link adalah emailed; account activates ketika
  link dibuka. Memerlukan email transport (`App:Email`). **Jika tidak ada transport yang dikonfigurasi, mode ini
  automatically downgrades ke `AdminApproval`** pada startup, jadi enabling registration tidak pernah silently breaks.
- **`Open`** — account adalah active immediately (trusted/dev only).

Self-registered users selalu dibuat sebagai **`User`** (atau `Viewer` jika dikonfigurasi) — domain
**hard-refuses** minting Owner/Admin melalui self-registration.

## Security & anti-abuse

- **Anti-enumeration.** Duplicate email yields **same** neutral `202 Accepted` seperti fresh sign-up dan
  creates tidak ada — app tidak pernah discloses apakah address sudah memiliki account.
- **Rate limiting.** Public endpoints adalah throttled per IP (harder dari auth limiter).
- **Password policy.** Minimum length diterapkan; passwords adalah hashed (Argon2 via `IPasswordHasher`);
  verification tokens disimpan hanya sebagai SHA-256 hashes dan single-use + expiring.
- **Email hygiene.** Optional allow-list dari email domains dan disposable-provider block-list.
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile via their shared verify contract.
- **Login gate.** Pending account adalah refused pada login dengan neutral response.

## Provisioning API (integration)

Dengan `App:Registration:Api:Enabled` dan `Secret` set, layanan lain dapat membuat users:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secret adalah dibandingkan dalam constant time. Provisioned accounts adalah dibuat **active** (atau invited dengan
`MustChangePassword`) tergantung pada `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Mengaktifkannya

Registration memerlukan **keduanya** feature flag dan master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // atau EmailVerification / Open
    "DefaultRole": "User",             // tidak pernah Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // kosong = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Bagian `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) mengkonfigurasi transport yang digunakan oleh `EmailVerification` mode; tinggalkan `Host` unset untuk jalankan
tanpa mail (no-op sender). Lihat [feature toggles](./feature-toggles.md) dan [white-label](./white-label.md) untuk
bagaimana deployments turn features on dan rebrand. Ketika registration diaktifkan, login page menampilkan **Create
account** link.

## Tested

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use tokens, erasure),
integration (disabled-by-default 404, approval flow, email-verification downgrade, anti-enumeration, abuse
guards, required attributes, provisioning + bad secret), dan E2E (default-off login memiliki tidak ada sign-up link;
`/register` page renders its branded closed state).
