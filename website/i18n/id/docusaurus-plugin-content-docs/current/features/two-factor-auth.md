---
description: "Otentikasi dua faktor TOTP opsional dengan enrollment aplikasi authenticator, single-use backup codes, dan white-label switch untuk membuatnya wajib untuk semua pengguna."
---

# Otentikasi dua faktor (2FA)

Akun dapat dilindungi dengan **time-based one-time password (TOTP)** otentikasi dua faktor di atas
kata sandi. Ini **opt-in** dari profil pengguna secara default, dan deployment white-label dapat membuatnya
**mandatory** untuk semua orang. Aplikasi authenticator RFC 6238 apa pun bekerja — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — karena implementasinya adalah standard (SHA-1, 6 digits, 30-second
step); tidak ada komponen server proprietary yang terlibat.

## Bagaimana cara kerjanya

- **Domain.** MFA tinggal di aggregate `AppUser` (Access context). Pengguna didaftarkan melalui
  intention-revealing methods — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — jadi invariants (secret harus dikonfirmasi sebelum mengaktifkan;
  backup code adalah single-use) diterapkan di satu tempat.
- **TOTP.** Generation dan verification duduk di belakang interface Core `ITotpAuthenticator`, diimplementasikan dalam
  Infrastructure dengan library **Otp.NET**. Verification tolerates ±1 time-step dari clock skew.
- **Secret at rest.** Secret authenticator disimpan **terenkripsi** via `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — tidak pernah dalam plaintext.
- **Backup codes.** Sepuluh single-use recovery codes diissue pada enrollment, ditampilkan **sekali**, dan disimpan hanya
  sebagai SHA-256 hashes (`MfaBackupCodes`). Masing-masing bekerja tepat sekali; spent code ditolak setelahnya.

## Mengaktifkannya (profile)

Pada halaman **Account** (`/account`) bagian *Two-factor authentication* menampilkan status saat ini:

1. **Enable two-factor** membuka MudBlazor dialog dengan **QR code** (rendered server-side sebagai SVG via
   `Net.Codecrete.QrCodeGenerator`) ditambah manual setup key.
2. Scan itu, masukkan 6-digit code untuk confirm — ini verifies pending secret sebelum mengaktifkan.
3. Dialog kemudian menampilkan **backup codes**; simpan mereka. 2FA sekarang aktif.

Section yang sama membiarkan enrolled user **regenerate backup codes** atau **turn off** 2FA — keduanya memerlukan
account password untuk confirm.

## Masuk dengan 2FA

Login adalah **two-step** flow sekali 2FA diaktifkan:

1. **Password step** (`POST /api/auth/login`). Pada success auth cookie adalah **tidak** diissue yet; sebaliknya short-lived (5-minute), encrypted *pending* cookie diatur dan pengguna dikirim ke `/login/2fa`.
2. **Challenge step** (`POST /api/auth/login/verify-2fa`). Pengguna memasukkan TOTP code **atau** unused backup code apa pun. Pada success pending cookie dijatuhkan dan real auth cookie diissue.

Failed second-factor attempts dihitung terhadap existing account **lockout** (`AuthLockout`), dan auth
endpoints adalah rate-limited.

## Mandatory 2FA untuk deployment white-label

Reseller yang diatur dapat memerlukan 2FA untuk **setiap** akun:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Ketika `RequireMfa` aktif dan pengguna tanpa 2FA masuk, password step melaporkan
`mfaSetupRequired` dan `MfaEnforcementMiddleware` redirects navigasi halaman mereka ke `/account` hingga mereka
selesai enrollment. Defaults ke `false`, jadi unconfigured deployment menjaga 2FA optional. Lihat
[White-label](white-label.md).

## Endpoints

| Method & route | Purpose |
| --- | --- |
| `POST /api/auth/login` | Password step; returns `mfaRequired` (challenge) atau signs in |
| `POST /api/auth/login/verify-2fa` | Second-factor step (TOTP atau backup code) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, remaining backup-code count |
| `POST /api/auth/mfa/setup` | Begin enrollment — returns secret, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Confirm code, activate, return backup codes |
| `POST /api/auth/mfa/disable` | Turn off (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Issue fresh set (password-confirmed) |

## Tests

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vectors),
  `AppUserMfaTests.cs` (enrollment/transition/single-use invariants), `MfaBackupCodesTests.cs`.
- **Integration** — `IntegrationTests/MfaPersistenceTests.cs` (enroll → confirm → consume, cascade delete)
  dan `MfaFlowTests.cs` (full HTTP two-step login dengan TOTP + backup code, dan mandatory-enrollment gate).
- **E2E** — `E2ETests/MfaFlowTests.cs`: enable dari profile (QR + confirm + backup codes) dan complete challenged
  sign-in, pada desktop dan mobile viewports.
