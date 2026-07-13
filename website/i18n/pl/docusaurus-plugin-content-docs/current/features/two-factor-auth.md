---
description: "Optional TOTP two-factor authentication z authenticator-app enrollment, single-use backup codes, i white-label switch do make to mandatory dla all users."
---

# Two-factor authentication (2FA)

Accounts mogą być protected z **time-based one-time password (TOTP)** two-factor authentication na top
z password. To **opt-in** z user's profile domyślnie, i white-label deployment może make to
**mandatory** dla everyone. Każdy RFC 6238 authenticator app works — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — bo implementation to standard (SHA-1, 6 digits, 30-second
step); proprietary server component nie jest involved.

## Jak to działa

- **Domain.** MFA lives na `AppUser` aggregate (Access context). User jest enrolled przez
  intention-revealing methods — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — więc invariants (secret musi być confirmed przed to activates;
  backup code to single-use) są enforced w one place.
- **TOTP.** Generation i verification sit za Core `ITotpAuthenticator` interface, implemented w
  Infrastructure z **Otp.NET** library. Verification tolerates ±1 time-step z clock skew.
- **Secret na rest.** Authenticator secret to stored **encrypted** via `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — nigdy w plaintext.
- **Backup codes.** Dziesięć single-use recovery codes są issued na enrollment, shown **once**, i stored tylko
  jako SHA-256 hashes (`MfaBackupCodes`). Każdy works dokładnie raz; spent code to rejected thereafter.

## Enabling to (profile)

Na **Account** page (`/account`) *Two-factor authentication* section shows current status:

1. **Enable two-factor** opens MudBlazor dialog z **QR code** (rendered server-side jako SVG via
   `Net.Codecrete.QrCodeGenerator`) plus manual setup key.
2. Scan to, enter 6-digit code do confirm — to verifies pending secret przed activating.
3. Dialog wtedy shows **backup codes**; save je. 2FA to teraz on.

Same section lets enrolled user **regenerate backup codes** lub **turn off** 2FA — oba require
account password do confirm.

## Signing in z 2FA

Login to **two-step** flow raz 2FA to enabled:

1. **Password step** (`POST /api/auth/login`). Na success auth cookie to **nie** issued yet; zamiast
   short-lived (5-minute), encrypted *pending* cookie to set i user to sent do `/login/2fa`.
2. **Challenge step** (`POST /api/auth/login/verify-2fa`). User enters TOTP code **lub** każdy unused
   backup code. Na success pending cookie to dropped i real auth cookie to issued.

Failed second-factor attempts count toward existing account **lockout** (`AuthLockout`), i auth
endpoints są rate-limited.

## Mandatory 2FA dla white-label deployment

Regulated reseller może require 2FA dla **każdy** account:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Gdy `RequireMfa` to on i user bez 2FA signs in, password step reports
`mfaSetupRequired` i `MfaEnforcementMiddleware` redirects ich page navigations do `/account` dopóki
nie finish enrollment. To defaults do `false`, więc unconfigured deployment keeps 2FA optional. See
[White-label](white-label.md).

## Endpoints

| Method & route | Cel |
| --- | --- |
| `POST /api/auth/login` | Password step; returns `mfaRequired` (challenge) lub signs in |
| `POST /api/auth/login/verify-2fa` | Second-factor step (TOTP lub backup code) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, remaining backup-code count |
| `POST /api/auth/mfa/setup` | Begin enrollment — returns secret, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Confirm code, activate, return backup codes |
| `POST /api/auth/mfa/disable` | Turn off (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Issue fresh set (password-confirmed) |

## Testy

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vectors),
  `AppUserMfaTests.cs` (enrollment/transition/single-use invariants), `MfaBackupCodesTests.cs`.
- **Integracja** — `IntegrationTests/MfaPersistenceTests.cs` (enroll → confirm → consume, cascade delete)
