---
description: "Optional TOTP two-factor authentication with authenticator-app enrollment, single-use backup codes, and a white-label switch to make it mandatory for all users."
---

# Two-factor authentication (2FA)

Accounts can be protected with **time-based one-time password (TOTP)** two-factor authentication on top
of the password. It is **opt-in** from the user's profile by default, and a white-label deployment can make
it **mandatory** for everyone. Any RFC 6238 authenticator app works — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — because the implementation is standard (SHA-1, 6 digits, 30-second
step); no proprietary server component is involved.

## How it works

- **Domain.** MFA lives on the `AppUser` aggregate (Access context). A user is enrolled through
  intention-revealing methods — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — so the invariants (a secret must be confirmed before it activates;
  a backup code is single-use) are enforced in one place.
- **TOTP.** Generation and verification sit behind the Core `ITotpAuthenticator` interface, implemented in
  Infrastructure with the **Otp.NET** library. Verification tolerates ±1 time-step of clock skew.
- **Secret at rest.** The authenticator secret is stored **encrypted** via `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — never in plaintext.
- **Backup codes.** Ten single-use recovery codes are issued at enrollment, shown **once**, and stored only
  as SHA-256 hashes (`MfaBackupCodes`). Each works exactly once; a spent code is rejected thereafter.

## Enabling it (profile)

On the **Account** page (`/account`) the *Two-factor authentication* section shows the current status:

1. **Enable two-factor** opens a MudBlazor dialog with a **QR code** (rendered server-side as SVG via
   `Net.Codecrete.QrCodeGenerator`) plus the manual setup key.
2. Scan it, enter the 6-digit code to confirm — this verifies the pending secret before activating.
3. The dialog then shows the **backup codes**; save them. 2FA is now on.

The same section lets an enrolled user **regenerate backup codes** or **turn off** 2FA — both require the
account password to confirm.

## Signing in with 2FA

Login is a **two-step** flow once 2FA is enabled:

1. **Password step** (`POST /api/auth/login`). On success the auth cookie is **not** issued yet; instead a
   short-lived (5-minute), encrypted *pending* cookie is set and the user is sent to `/login/2fa`.
2. **Challenge step** (`POST /api/auth/login/verify-2fa`). The user enters a TOTP code **or** any unused
   backup code. On success the pending cookie is dropped and the real auth cookie is issued.

Failed second-factor attempts count toward the existing account **lockout** (`AuthLockout`), and the auth
endpoints are rate-limited.

## Mandatory 2FA for a white-label deployment

A regulated reseller can require 2FA for **every** account:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

When `RequireMfa` is on and a user without 2FA signs in, the password step reports
`mfaSetupRequired` and `MfaEnforcementMiddleware` redirects their page navigations to `/account` until they
finish enrollment. It defaults to `false`, so an unconfigured deployment keeps 2FA optional. See
[White-label](white-label.md).

## Endpoints

| Method & route | Purpose |
| --- | --- |
| `POST /api/auth/login` | Password step; returns `mfaRequired` (challenge) or signs in |
| `POST /api/auth/login/verify-2fa` | Second-factor step (TOTP or backup code) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, remaining backup-code count |
| `POST /api/auth/mfa/setup` | Begin enrollment — returns secret, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Confirm a code, activate, return backup codes |
| `POST /api/auth/mfa/disable` | Turn off (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Issue a fresh set (password-confirmed) |

## Tests

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vectors),
  `AppUserMfaTests.cs` (enrollment/transition/single-use invariants), `MfaBackupCodesTests.cs`.
- **Integration** — `IntegrationTests/MfaPersistenceTests.cs` (enroll → confirm → consume, cascade delete)
  and `MfaFlowTests.cs` (full HTTP two-step login with TOTP + backup code, and the mandatory-enrollment gate).
- **E2E** — `E2ETests/MfaFlowTests.cs`: enable from the profile (QR + confirm + backup codes) and complete a
  challenged sign-in, on desktop and mobile viewports.
