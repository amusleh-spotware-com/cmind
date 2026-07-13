---
description: "Daftr sendiri yang dilindungi, digerbang white-label — halaman pendaftaran dalam apl dan API peruntukan pelayan-ke-pelayan, dengan atribut pengguna boleh konfigurasi, gerbang kelulusan admin atau pengesahan e-mel, dan penjaga anti-pend滥. Dilumpuhkan secara lalai."
---

# Pendaftaran pengguna

Secara lalai **pemilik/admin menambah pengguna secara manual** (halaman Pengguna → *Pengguna Baharu*). Untuk penempatan
white-label yang perlu aboard pengguna pada skala — atau mengintegrasikan apl dengan perkhidmatan lain — cMind juga ships
**laluan pendaftaran sendiri yang dilindungi**. Ia **dilumpuhkan secara lalai**: penempatan saham tidak berubah dan halaman serta API kedua-duanya mengembalikan 404 sehingga penempatan mengktifkannya.

Terdapat dua titik masuk kongsi satu aliran domain:

1. **Halaman dalam apl** (`/register`) — halaman pendaftaran berjenama, mudah alih-pertama dalam shell yang sama seperti `/login`.
2. **API peruntukan** (`POST /api/provision`) — titik akhir pelayan-ke-pelayan untuk perkhidmatan yang mengintegrasikan untuk
   cipta akaun, disahkan oleh rahsia peruntukan setiap penempatan.

## Apa yang direkodkan — peminimuman data

cMind ialah alat perdagangan ****: nó bina/jalankan/backtest cBot dan mencerminkan perdagangan merentasi bukti cTrader Open API
setiap pengguna. nó **tidak membuka akaun perdagangan atau menyimpan wang klien**, jadi pengesahan identiti KYC/AML ialah
**keperluan broker**, bukan platform ini. Borang pendaftaran oleh itu merekodkan **hanya e-mel secara lalai** — minimum
yang diperlukan untuk membekalkan perkhidmatan (GDPR Art. 5(1)(c) minimisasi data; asas sah = kontrak). cMind secara sengaja ships **tiada** medan ID negara/tarikh lahir /
alamat.

Setiap atribut lain ialah **pilihan masuk setiap penempatan** melalui `App:Registration:Attributes`, setiap secara bebas
`Off` / `Opsyen` / `Wajib`:

| Atribut | Nota |
|---|---|
| `FullName`, `DisplayName`, `Company` | Teks bebas, had panjang. |
| `Country` | ISO 3166-1 alpha-2, disahkan terhadap set kod tetap. |
| `Phone` | Format E.164 (`+14155552671`). |
| `Locale` | Bentuk BCP-47 (`en-US`), dinormalisasi. |
| `MarketingOptIn` | Kotak semak berasingan, **tidak dicentang** — tidak pernah digabungkan dengan persetujuan mandatori (CAN-SPAM). |
| `AgeConfirmation` | Kotak semak sahaja; **tiada** tarikh lahir disimpan. |

Atribut tinggal dalam objek nilai `UserProfile` yang dimiliki oleh agregat `AppUser`, disahkan pada
pembinaan. **GDPR erasure** (`AppUser.Anonymize()`) mengikis profil dan sebarang token pengesahan.

**Persetujuan.** Apabila `RequireTermsAcceptance` aktif, pengguna harus menerima dokumen undang-undang yang diterbitkan
(Syarat, Privasi, Pendedahan Risiko). Penerimaan direkod melalui agregat `ConsentRecord` sedia ada —
bercap versi, cap waktu, dengan IP asal — kedai yang sama digunakan di tempat lain untuk rekod penyimpanan gred MiFID/ESMA.

## Mod gerbang

Akaun yang berdaftar sendiri tidak boleh daftar masuk sehingga nó Clear gerbang (`App:Registration:Mode`):

- **`AdminApproval`** (lalai) — akaun dalam barisan; pemilik/admin meluluskannya pada halaman **Pengguna**
  (*Kelulusan tunggu*). Tidak memerlukan infrastruktur mel.
- **`EmailVerification`** — pranala pengesahan sekali-guna, tamat tempoh dihantar melalui e-mel; akaun aktif apabila
  pranala dibuka. Memerlukan pengangkutan e-mel (`App:Email`). **Jika tiada pengangkutan dikonfigurasi, mod ini
  automatik menurun taraf ke `AdminApproval`** pada permulaan, jadi mengaktifkan pendaftaran tidak pernah senyap memecahkan.
- **`Open`** — akaun aktif serta-merta ( dipercayai/dev sahaja).

Pengguna yang berdaftar sendiri sentiasa dicipta sebagai **`User`** (atau `Viewer` jika dikonfigurasi) — domain
**menolak keras** pembbitan Owner/Admin melalui pendaftaran sendiri.

## Keselamatan & anti-pend滥

- **Anti-penghitungan.** E-mel pendua menghasilkan **sama** `202 Accepted` neutral seperti daftar masuk baharu dan
  tidak membuat apa-apa — apl tidak pernah mendedahkan sama ada alamat sudah mempunyai akaun.
- **Had kadar.** Titik akhir awam dikecilkan setiap IP (lebih keras daripada pengecil auth).
- **Dasar kata lalu.** Panjang minimum dikuatkuasakan; kata lalu di-hash (Argon2 melalui `IPasswordHasher`);
  token pengesahan disimpan hanya sebagai cincangan SHA-256 dan sekali-guna + tamat tempoh.
- **Kebersihan e-mel.** Senarai benarkan pilihan domain e-mel dan senarai堵阻 pembekal buangan.
- **CAPTCHA (pilihan).** reCAPTCHA / hCaptcha / Turnstile melalui kontrak pengesahan kongsi.
- **Gerbang daftar masuk.** Akaun tunggu ditolak pada daftar masuk dengan respons neutral.

## API Peruntukan (integrasi)

Dengan `App:Registration:Api:Enabled` dan `Secret` ditetapkan, perkhidmatan lain boleh cipta pengguna:

```
POST /api/provision
X-Provision-Secret: <rahsia yang dikonfigurasi>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Rahsia dibandingkan dalam masa malar. Akaun diperuntukan dicipta **aktif** (atau dijemput dengan
`MustChangePassword`) bergantung pada `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Mengaktifkannya

Pendaftaran memerlukan **kedua-duanya** bendera ciri dan suis utama:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // atau EmailVerification / Open
    "DefaultRole": "User",             // tidak pernah Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // kosong = mana-mana
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Bahagian `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) mengkonfigur pengangkutan yang digunakan oleh mod `EmailVerification`; biarkan `Host` tidak ditetapkan untuk berjalan tanpa mel
(penghantar no-op). Lihat [togol ciri](./feature-toggles.md) dan [white-label](./white-label.md) untuk
bagaimana penempatan menghidupkan ciri dan menjenamakan. Apabila pendaftaran diaktifkan, halaman daftar masuk mempamerkan pautan **Cipta akaun**.

## Diuji

Unit (pengesahan profil, penjaga peranan `SelfRegister`, penghantaran akaun, token sekali-guna, penghapusan),
integrasi (404 lalai-dimatikan, alir kelulusan, penurunan tahap pengesahan e-mel, anti-penghitungan, penjaga pend滥, atribut wajib, peruntukan + rahsia buruk), dan E2E (lalai-off daftar masuk mempunyai tiada pautan daftar masuk; halaman `/register` memapar keadaan tertutup berjenamanya).
