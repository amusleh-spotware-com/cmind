---
description: "Pendaftaran pengguna tunai selamat, label-putih-pintu — halaman daftar pada apl dan API peruntukan pelayan-ke-pelayan, dengan atribut pengguna yang boleh dikonfigur, gating persetujuan pentadbir atau pengesahan e-mel, dan pengawal anti-penyalahgunaan. Dilumpuhkan secara lalai."
---

# Pendaftaran pengguna

Secara lalai **pemilik/pentadbir menambah pengguna secara manual** (halaman Pengguna → *Pengguna Baharu*). Untuk penempatan label-putih yang perlu untuk mengendalikan pengguna pada skala — atau integrasikan apl dengan perkhidmatan lain — cMind juga menghantar laluan **pendaftaran diri selamat**. Ia adalah **dilumpuhkan secara lalai**: penempatan stok tidak berubah dan halaman dan API kedua-duanya kembalikan 404 sehingga penempatan memilih.

Terdapat dua titik masuk berkongsi satu aliran domain:

1. **Halaman pada apl** (`/register`) — halaman daftar berjenama, mudah alih-pertama dalam cangkul yang sama seperti `/login`.
2. **API Peruntukan** (`POST /api/provision`) — titik akhir pelayan-ke-pelayan untuk perkhidmatan mengintegrasikan untuk mencipta akaun, disahkan oleh rahsia peruntukan setiap penempatan.

## Apa yang direkodkan — pengurangkan data

cMind adalah perdagangan **alatan**: ia membina/menjalankan/ujian belakang cBots dan mencerminkan dagangan ke atas kredensial API Terbuka cTrader setiap pengguna *mereka sendiri*. Ia **tidak membuka akaun perdagangan atau penjagaan wang klien**, jadi pengesahan identiti KYC/AML adalah kewajipan **broker**, bukan platform ini. Borang pendaftaran oleh itu merekodkan **hanya e-mel secara lalai** — minimum yang diperlukan untuk memberikan perkhidmatan (GDPR Art. 5(1)(c) pengurangkan data; asas undang-undang = kontrak). cMind sengaja menghantar **tiada** ID nasional / tarikh lahir / medan alamat.

Setiap atribut lain adalah **pilihan setiap penempatan** melalui `App:Registration:Attributes`, setiap secara bebas `Off` / `Optional` / `Required`:

| Atribut | Catatan |
|---|---|
| `FullName`, `DisplayName`, `Company` | Teks bebas, panjang-terikat. |
| `Country` | ISO 3166-1 alfa-2, disahkan terhadap set kod tetap. |
| `Phone` | Format E.164 (`+14155552671`). |
| `Locale` | Bentuk BCP-47 (`en-US`), ternormal. |
| `MarketingOptIn` | Berasingan, kotak **tidak ditandakan** — tidak pernah dibundel dengan persetujuan wajib (CAN-SPAM). |
| `AgeConfirmation` | Kotak sahaja; **tiada** tarikh lahir disimpan. |

Atribut hidup dalam objek nilai `UserProfile` yang dimiliki oleh agregat `AppUser`, disahkan pada pembinaan. **Penghapusan GDPR** (`AppUser.Anonymize()`) mengerik profil dan mana-mana token pengesahan.

**Persetujuan.** Apabila `RequireTermsAcceptance` hidup, pengguna mesti terima dokumen undang-undang yang diterbitkan (Terma, Privasi, Pendedahan Risiko). Penerimaan direkodkan melalui agregat `ConsentRecord` sedia ada — versi-cap, cap masa, dengan IP asal — stor yang sama digunakan di tempat lain untuk penyimpanan gred MiFID/ESMA.

## Mod gating

Akaun yang didaftarkan sendiri tidak boleh menyusun masuk sehingga ia melepasi gawanya (`App:Registration:Mode`):

- **`AdminApproval`** (lalai) — akaun antrian; seorang pemilik/pentadbir meluluskannya di halaman **Pengguna** (bahagian *Menunggu persetujuan*). Memerlukan infrastruktur mel tiada.
- **`EmailVerification`** — pautan pengesahan tunggal-guna, tamat tempoh e-melkan; akaun mengaktifkan apabila pautan dibuka. Memerlukan pengangkutan e-mel (`App:Email`). **Jika tiada pengangkutan dikonfigurasi, mod ini secara automatik menurun kepada `AdminApproval`** pada permulaan, jadi membolehkan pendaftaran tidak pernah senyap melecehkan.**
- **`Open`** — akaun aktif dengan serta-merta (dipercayai/dev sahaja).

Pengguna yang didaftarkan sendiri sentiasa dibuat sebagai **`User`** (atau `Viewer` jika dikonfigurasi) — domain **keras-menolak** mencetak Pemilik/Admin melalui pendaftaran diri.
