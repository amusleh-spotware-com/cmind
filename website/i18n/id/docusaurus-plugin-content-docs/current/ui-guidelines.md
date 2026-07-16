---
description: "Panduan wajib untuk setiap bagian UI baru atau berubah di aplikasi ini (halaman Blazor, dialog, komponen). Ini adalah sumber kebenaran yang dirujuk oleh CLAUDE.md. Jika suatu aturan menghalangi Anda, berhenti dan tanyakan — jangan kirimkan UI yang melanggarnya. Berasal dari plans/ui-overhaul.md."
---

# Pedoman Desain UI — WAJIB

Panduan **wajib** untuk **setiap** bagian UI baru atau berubah di aplikasi ini (halaman Blazor, dialog, komponen).
Ini adalah sumber kebenaran yang dirujuk oleh `CLAUDE.md`. Jika suatu aturan menghalangi Anda, berhenti dan tanyakan — jangan
kirimkan UI yang melanggarnya. Berasal dari `plans/ui-overhaul.md`.

## 1. Mobile-first, selalu

- **Buat untuk telepon 360–430px terlebih dahulu**, kemudian tingkatkan ke atas dengan media query `min-width` / properti breakpoint MudBlazor. Jangan pernah desktop-first dengan override `max-width`.
- **Tidak ada scroll horizontal pada lebar apa pun 320–1920px.** Jika konten lebih lebar dari viewport, itu adalah bug.
- Target sentuh ≥ **44px** (`var(--app-touch-target)`). Input teks ≥ 16px font (menghindari iOS zoom-on-focus).
- Hormati notch: gunakan `env(safe-area-inset-*)`; viewport sudah menetapkan `viewport-fit=cover`.
- Hormati `prefers-reduced-motion` — tidak ada informasi penting yang hanya disampaikan melalui animasi.

## 2. Token desain — tanpa nilai hard-coded

- Semua warna/radius/spacing berasal dari **token desain**: tema MudBlazor (`Web/Components/Theme.cs`) +
  properti custom CSS yang dipancarkan oleh `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Jangan pernah hard-code warna hex, radius, atau string brand di komponen atau aturan CSS.** Baca token.
  Token mengalir dari `BrandingOptions` white-label, jadi palet reseller harus mencapai UI Anda secara gratis.
- Nilai baru yang mempengaruhi brand → tambahkan token + bidang branding; jangan inline-kan.

## 3. Layout responsif & data

- **Tabel runtuh menjadi kartu di ponsel.** Setiap `MudTable` menetapkan `Breakpoint="Breakpoint.Sm"` dan setiap
  `MudTd` memiliki `DataLabel`. Tidak ada tabel lebar mentah di perangkat mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grid: `MudItem xs="12" sm="6" md="4"` — lebar penuh di ponsel, multi-kolom ke atas.
- Formulir single-column di mobile; target tap besar; `inputmode`/`autocomplete` pada input; inputmode numeric/decimal
  untuk uang/persen.
- **Kontrol yang tepat untuk input terstruktur — jangan pernah kotak teks mentah untuk angka atau daftar.** Kumpulkan angka,
  uang, persentase, tanggal, enum dan data multi-nilai apa pun dengan kontrol yang tepat (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, daftar baris yang dapat diedit dengan tipe add/remove, atau tabel), setiap bidang
  divalidasi secara individual. Satu `MudTextField` teks bebas yang harus diketik pengguna dengan blob yang dipisahkan koma/spasi/baris baru
  — yang kemudian Anda parse — adalah **terlarang**: itu mudah terjadi kesalahan, tidak divalidasi, dan bermusuhan
  di ponsel. **Tidak ada yang ingin mengetik blob.** Input multi-nilai adalah daftar baris yang dapat diedit dari tipe (add/remove),
  atau dimuat dari data domain yang ada (misalnya jalankan pemeriksaan langsung dari backtest yang selesai
  daripada memasukkan kembali angkanya). `MudTextField` biasa hanya untuk teks bebas asli — nama, catatan,
  pencarian, deskripsi.
- Sediakan state **loading, empty, dan error** pada setiap daftar/detail — berukuran untuk mobile.
- **Bottom navigation** ponsel (`Components/Layout/BottomNav.razor`) adalah navigasi telepon utama; drawer
  yang dikelompokkan adalah menu lengkap. Tambahkan tujuan lalu lintas tinggi di sana; pertahankan ≤5 item.

## 4. Dialog (create/edit)

- Semua tindakan add/create/edit/new menggunakan **dialog MudBlazor** (`IDialogService.ShowAsync<TDialog>`), bukan
  formulir halaman inline. Dialog hidup di `Web/Components/Dialogs/`, mengekspos `[Parameter]`s, mengembalikan nested
  `public sealed record …Result(...)`. Tindakan baris daftar (start/stop/delete) tetap inline sebagai tombol ikon.
- Di ponsel, dialog harus **full-screen / full-width** dan aware keyboard.

## 5. Bantuan inline — setiap kontrol

- Setiap opsi, pilih, switch, atau tindakan yang tidak jelas mendapatkan **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover di desktop, **tap di mobile**. Sumber teks dari `docs/` jadi
  panduan tetap sinkron dengan perilaku; perbarui keduanya dalam commit yang sama.

## 6. White-label

- Nama produk, logo, deskripsi, dukungan/perusahaan, warna, favicon semua berasal dari `BrandingOptions`.
  Referensikan mereka (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), bukan literal "cMind" atau
  warna brand. Manifes PWA, ikon, theme-color, dan hero login semuanya branded.

## 7. PWA

- Aplikasi dapat diinstal. Jaga endpoint manifes (`/manifest.webmanifest`) branded, ikon ada
  (192/512/maskable + apple-touch), service worker app-shell-only (tidak pernah menyentuh sirkuit Blazor/`_framework`/hubs), dan halaman offline bekerja. Rute statis baru → pertahankan manifes `scope`.
- Blazor Server membutuhkan sirkuit SignalR langsung → **dapat diinstal + app-shell**, bukan offline penuh. Jangan
  janjikan interaktivitas offline.

## 8. Aksesibilitas

- Label pada input, `aria-*` pada kontrol khusus, fokus terlihat, urutan fokus logis. Karena tema adalah
  white-labelable, verifikasi **kontras** terhadap tema aktif, bukan palet tetap.

## 9. E2E — tidak ada UI yang dikirim tanpa pengujian (blocking)

Setiap perubahan yang dihadapi pengguna mengirim Playwright E2E di `tests/E2ETests`, didorong seperti pengguna nyata, **pada emulasi
perangkat mobile** plus desktop:

- Rute baru → tambahkan ke `PageSmokeTests` **dan** `MobileLayoutTests` (render, bottom nav, tidak ada UI error).
- Konversi tabel/halaman → tambahkan rutnya ke set **no-overflow** mobile.
- Alur baru → perjalanan mobile yang realistis (putaran create/edit/save) **dan** jalur yang tidak bahagia
  (input tidak valid, daftar kosong, izin-ditolak per peran).
- Tip bantuan baru → pastikan itu terbuka saat mengetuk (`HelpTipTests` pattern).
- Gunakan `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulasi perangkat).
- `dotnet test` green sebelum "done". WebKit yang diemulasi ≠ mobile Safari — gating perangkat nyata adalah langkah
  rilis terpisah.

## 10. Definisi selesai (UI)

- [ ] Mobile-first; tidak ada overflow horizontal 320–1920px; target sentuh ≥44px.
- [ ] Hanya token desain — nol warna hard-coded/radii/string brand.
- [ ] Tabel → kartu di ponsel (`DataLabel` + `Breakpoint.Sm`); loading/empty/error state hadir.
- [ ] Input terstruktur menggunakan kontrol yang divalidasi dengan benar (numeric/date/select/editable row list) — tidak ada kotak
      teks mentah yang diketik pengguna dengan blob nilai/angka yang dibatasi.
- [ ] Create/edit melalui dialog; full-screen di mobile.
- [ ] Setiap kontrol memiliki `HelpTip` bersumber dari docs.
- [ ] White-label + PWA dihormati.
- [ ] E2E mobile + desktop ditambahkan (smoke, no-overflow, journey, unhappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` bersih pada file yang disentuh.
