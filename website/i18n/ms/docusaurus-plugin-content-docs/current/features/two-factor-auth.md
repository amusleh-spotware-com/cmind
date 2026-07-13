---
description: "Pengesahan dua-faktor TOTP pilihan dengan pendaftaran apl pengesah, kod sandaran guna tunggal, dan suis label-putih untuk menjadikannya wajib untuk semua pengguna."
---

# Pengesahan dua-faktor (2FA)

Akaun boleh dilindungi dengan pengesahan dua-faktor **kata laluan sekali masa berasaskan masa (TOTP)** di atas kata laluan. Ia adalah **pilihan** daripada profil pengguna secara lalai, dan penempatan label-putih boleh menjadikannya **wajib** untuk semua orang. Mana-mana apl pengesah RFC 6238 berfungsi — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — kerana pelaksanaan adalah standard (SHA-1, 6 digit, langkah 30 saat); tiada komponen pelayan proprietari terlibat.

## Bagaimana ia berfungsi

- **Domain.** MFA hidup di agregat `AppUser` (Konteks Akses). Pengguna didaftarkan melalui kaedah yang menunjukkan niat — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — jadi invarian (rahsia mesti disahkan sebelum diaktifkan; kod sandaran adalah guna tunggal) dikuatkuasakan di satu tempat.
- **TOTP.** Penjanaan dan pengesahan duduk di sebalik antara muka Teras `ITotpAuthenticator`, dilaksanakan dalam Infrastruktur dengan perpustakaan **Otp.NET**. Pengesahan bertolak ansur ±1 langkah masa melencong jam.
- **Rahsia dalam keadaan rehat.** Rahsia pengesah disimpan **dienkripsi** melalui `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — tidak pernah dalam keadaan biasa.
- **Kod sandaran.** Sepuluh kod pemulihan guna tunggal dikeluarkan pada pendaftaran, ditunjukkan **sekali**, dan disimpan hanya sebagai cincangan SHA-256 (`MfaBackupCodes`). Setiap berfungsi tepat sekali; kod yang dihabiskan ditolak selepas itu.

## Membolehkannya (profil)

Di halaman **Akaun** (`/account`) bahagian *Pengesahan dua-faktor* menunjukkan status semasa:

1. **Dayakan dua-faktor** membuka dialog MudBlazor dengan **kod QR** (dirender sisi pelayan sebagai SVG melalui `Net.Codecrete.QrCodeGenerator`) tambah kunci persediaan manual.
2. Pindai itu, masukkan kod 6 digit untuk mengesahkan — ini mengesahkan rahsia yang tertunda sebelum diaktifkan.
3. Dialog kemudian menunjukkan **kod sandaran**; simpannya. 2FA sekarang hidup.

Bahagian yang sama membiarkan pengguna yang terdaftar **menjana semula kod sandaran** atau **matikan** 2FA — kedua-duanya memerlukan kata laluan akaun untuk mengesahkan.

## Menyusun masuk dengan 2FA

Log masuk adalah aliran **dua langkah** apabila 2FA diaktifkan:

1. **Langkah kata laluan** (`POST /api/auth/login`). Atas kejayaan kuki auth adalah **bukan** dikeluarkan lagi; sebaliknya kuki *tertunda* yang singkat (5-minit), dienkripsi ditetapkan dan pengguna dihantar ke `/login/2fa`.
2. **Langkah cabaran** (`POST /api/auth/login/verify-2fa`). Pengguna memasukkan kod TOTP **atau** mana-mana kod sandaran yang tidak digunakan. Atas kejayaan kuki tertunda dilepaskan dan kuki auth sebenar dikeluarkan.

Percubaan faktor kedua yang gagal dikira ke dalam akaun **lockout** sedia ada (`AuthLockout`), dan titik akhir auth adalah had kadar.

## 2FA wajib untuk penempatan label-putih
