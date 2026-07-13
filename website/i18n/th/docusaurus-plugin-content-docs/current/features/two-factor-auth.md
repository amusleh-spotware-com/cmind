---
description: "Optional TOTP two-factor authentication ด้วย authenticator-app enrollment single-use backup codes และ white-label switch เพื่อ ทำให้มันได้บังคับ สำหรับ ทั้งหมด users"
---

# Two-factor authentication (2FA)

Accounts สามารถ protected ด้วย **time-based one-time password (TOTP)** two-factor authentication บน top ของ password มัน **opt-in** จาก user's profile โดย default และ white-label deployment สามารถ ทำให้มัน **mandatory** สำหรับ ทั้งหมด any RFC 6238 authenticator app works — google authenticator microsoft authenticator authy aegis freeotp — เพราะ implementation เป็น standard (SHA-1 6 digits 30-second step); ไม่มี proprietary server component involved

## วิธีการ works

- **Domain** MFA อยู่บน `AppUser` aggregate (access context) user enrolled ผ่าน intention-revealing methods — `BeginMfaEnrollment` `ConfirmMfaEnrollment` `ConsumeBackupCode` `RegenerateBackupCodes` `DisableMfa` — ดังนั้น invariants (secret ต้อง confirmed ก่อน มันกระตุ้น; backup code เป็น single-use) enforced ใน สถานที่เดียว
- **TOTP** generation และ verification sit ด้านหลัง core `ITotpAuthenticator` interface implemented ใน infrastructure ด้วย **Otp.NET** library verification tolerates ±1 time-step ของ clock skew
- **Secret ที่ rest** authenticator secret stored **encrypted** ผ่าน `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — ไม่เคย ใน plaintext
- **Backup codes** สิบ single-use recovery codes issued ที่ enrollment shown **เมื่อ** และ stored เพียง เป็น SHA-256 hashes (`MfaBackupCodes`) ทุก ๆ works ตรง เมื่อ; spent code rejected thereafter

## Enabling มัน (profile)

บน **Account** page (`/account`) the *Two-factor authentication* section แสดง current status:

1. **Enable two-factor** opens mudblazor dialog ด้วย **QR code** (rendered server-side เป็น SVG ผ่าน `Net.Codecrete.QrCodeGenerator`) บวก manual setup key
2. scan มัน enter 6-digit code เพื่อ confirm — นี้ verifies pending secret ก่อน activating
3. dialog แล้ว แสดง **backup codes**; save พวกเขา 2FA ตอนนี้ on

same section let enrolled user **regenerate backup codes** หรือ **turn off** 2FA — ทั้งสอง require account password เพื่อ confirm

## Signing ใน ด้วย 2FA

login เป็น **two-step** flow เมื่อ 2FA enabled:

1. **Password step** (`POST /api/auth/login`) on success auth cookie เป็น **ไม่** issued yet; แทน short-lived (5-minute) encrypted *pending* cookie ตั้ง และ user sent เป็น `/login/2fa`
2. **Challenge step** (`POST /api/auth/login/verify-2fa`) user enters TOTP code **หรือ** any unused backup code on success pending cookie dropped และ real auth cookie issued

failed second-factor attempts count ต้านแบบ existing account **lockout** (`AuthLockout`) และ auth endpoints rate-limited

## Mandatory 2FA สำหรับ white-label deployment

regulated reseller สามารถ require 2FA สำหรับ **every** account:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

เมื่อ `RequireMfa` on และ user โดยไม่มี 2FA signs ใน password step reports `mfaSetupRequired` และ `MfaEnforcementMiddleware` redirects page navigations ของพวกเขา เป็น `/account` จนกว่า พวกเขา finish enrollment มัน defaults เป็น `false` ดังนั้น unconfigured deployment keeps 2FA optional ดู [White-label](white-label.md)

## Endpoints

| Method & route | Purpose |
| --- | --- |
| `POST /api/auth/login` | password step; returns `mfaRequired` (challenge) หรือ signs in |
| `POST /api/auth/login/verify-2fa` | second-factor step (TOTP หรือ backup code) |
| `GET /api/auth/mfa/status` | `MfaEnabled` pending remaining backup-code count |
| `POST /api/auth/mfa/setup` | begin enrollment — returns secret `otpauth://` URI QR SVG |
| `POST /api/auth/mfa/confirm` | confirm code activate return backup codes |
| `POST /api/auth/mfa/disable` | turn off (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | issue fresh set (password-confirmed) |

## Tests

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vectors) `AppUserMfaTests.cs` (enrollment/transition/single-use invariants) `MfaBackupCodesTests.cs`
- **Integration** — `IntegrationTests/MfaPersistenceTests.cs` (enroll → confirm → consume cascade delete) และ `MfaFlowTests.cs` (full HTTP two-step login ด้วย TOTP + backup code และ mandatory-enrollment gate)
- **E2E** — `E2ETests/MfaFlowTests.cs`: enable จาก profile (QR + confirm + backup codes) และ complete challenged sign-in บน desktop และ mobile viewports
