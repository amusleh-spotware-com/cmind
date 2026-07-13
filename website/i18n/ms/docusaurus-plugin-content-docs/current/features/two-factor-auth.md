---
description: "Pengesahan dua faktor TOTP pilihan dengan pendaftaran apl pengesah, kod sandaran sekali-guna, dan suis white-label untuk menjadikannya mandatori untuk semua pengguna."
---

# Pengesahan dua faktor (2FA)

Akaun boleh dilindungi dengan **kata lalu satu masa berbasis masa (TOTP)** pengesahan dua faktor di atas
kata lalu. Ia ialah **pilihan masuk** dari profil pengguna secara lalai, dan penempatan white-label boleh menjadikannya
**mandatori** untuk semua orang. Mana-mana apl pengesah RFC 6238 berfungsi — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — kerana pelaksanaan adalah standard (SHA-1, 6 digit, langkah 30 saat); tiada komponen pelayan proprietary yang terlibat.

## Cara ia berfungsi

- **Domain.** MFA tinggal pada agregat `AppUser` (konteks Access). Pengguna pendaftaran melalui
  kaedah yang mendedahkan niat — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — jadi invariant (rahsia harus disahkan sebelum aktif;
  kod sandaran sekali-guna) menguatkuasakan di satu tempat.
- **TOTP.** Penjanaan dan pengesahan di belakang antara muka Core `ITotpAuthenticator`, dilaksanakan dalam
  Infrastructure dengan perpustakaan **Otp.NET**. Pengesahan toleransi ±1 langkah masa kecurian jam.
- **Rahsia pada rehat.** Rahsia pengesah disimpan **disulit** melalui `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — tidak pernah dalam teks jelas.
- **Kod sandaran.** Sepuluh kod pemulihan sekali-guna dikeluarkan semasa pendaftaran, dipapar **sekali**, dan disimpan hanya
  sebagai cincangan SHA-256 (`MfaBackupCodes`). Setiap berfungsi tepat sekali; kod yang dibelanjakan ditolak selepas itu.

## Mengenablekannya (profil)

Pada halaman **Akaun** (`/account`) bahagian *Pengesahan dua faktor* menunjukkan status semasa:

1. **Enable two-factor** membuka dialog MudBlazor dengan **kod QR** (diserver melalui SVG) ditambah kunci penetapan manual.
2. Imbas nó, masukkan kod 6 digit untuk mengesahkan — ini mengesahkan rahsia tertunda sebelum mengaktifkan.
3. Dialog kemudian menunjukkan **kod sandaran**; simpan nó. 2FA sekarang aktif.

Bahagian yang sama membolehkan pengguna berdaftar **regenerasi kod sandaran** atau **matikan** 2FA — kedua-duanya memerlukan kata lalu akaun untuk mengesahkan.

## Daftar masuk dengan 2FA

Daftar masuk ialah alir **dua-langkah** setelah 2FA diaktifkan:

1. **Langkah kata lalu** (`POST /api/auth/login`). Pada kejayaan kuki auth **tidak** dikeluarkan lagi; sebaliknya kuki *tertunda* berenkripsi pendek hayat (5 minit) ditetapkan dan pengguna dihantar ke `/login/2fa`.
2. **Langkah cabaran** (`POST /api/auth/login/verify-2fa`). Pengguna memasukkan kod TOTP **atau** mana-mana kod sandaran yang belum digunakan. Pada kejayaan kuki tertunda didrop dan kuki auth sebenar dikeluarkan.

Percubaan faktor kedua yang gagal dikira terhadap kunci akaun sedia ada (`AuthLockout`), dan titik akhir auth dikecilkan.

## 2FA Mandatori untuk penempatan white-label

Penjual regulasi boleh mewajibkan 2FA untuk **setiap** akaun:

```jsonc
// appsettings / persekitaran
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Apabila `RequireMfa` aktif dan pengguna tanpa 2FA daftar masuk, langkah kata lalu melaporkan
`mfaSetupRequired` dan `MfaEnforcementMiddleware` mengarahkan semula navigasi halaman mereka ke `/account` sehingga mereka
selesai pendaftaran. Lalai kepada `false`, jadi penempatan yang tidak dikonfigurasi kekal 2FA pilihan. Lihat
[White-label](white-label.md).

## Titik akhir

| Kaedah & laluan | Tujuan |
| --- | --- |
| `POST /api/auth/login` | Langkah kata lalu; kembalikan `mfaRequired` (cabangan) atau daftar masuk |
| `POST /api/auth/login/verify-2fa` | Langkah faktor kedua (TOTP atau kod sandaran) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, tertunda, bilangan kod sandaran tinggal |
| `POST /api/auth/mfa/setup` | Mula pendaftaran — kembalikan rahsia, URI `otpauth://`, SVG QR |
| `POST /api/auth/mfa/confirm` | Sahkan kod, aktifkan, kembalikan kod sandaran |
| `POST /api/auth/mfa/disable` | Matikan (pengesahan kata lalu) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Keluarkan set baharu (pengesahan kata lalu) |

## Ujian

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vektor RFC 6238),
  `AppUserMfaTests.cs` (pendaftaran/penghantaran/invariants sekali-guna), `MfaBackupCodesTests.cs`.
- **Integrasi** — `IntegrationTests/MfaPersistenceTests.cs` (daftar → sahkan → guna, padam bertingkat) dan `MfaFlowTests.cs` (daftar masuk HTTP dua-langkah penuh dengan TOTP + kod sandaran, dan gerbang pendaftaran mandatori).
- **E2E** — `E2ETests/MfaFlowTests.cs`: aktifkan dari profil (QR + sahkan + kod sandaran) dan lengkapkan daftar masuk dengan cabangan, pada desktop dan paparan mudah alih.
