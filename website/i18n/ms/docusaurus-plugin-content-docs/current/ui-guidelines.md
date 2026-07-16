---
description: "Ikat untuk setiap bahagian UI yang baru atau berubah dalam aplikasi ini (halaman Blazor, dialog, komponen). Ini adalah sumber kebenaran dirujuk oleh CLAUDE.md. Jika peraturan menghalang anda, berhenti dan tanya — jangan hantar UI yang melanggarnya."
---

# Garis Panduan Reka Bentuk UI — WAJIB

Ikat untuk **setiap** bahagian UI yang baru atau berubah dalam aplikasi ini (halaman Blazor, dialog, komponen).
Ini adalah sumber kebenaran dirujuk oleh `CLAUDE.md`. Jika peraturan menghalang anda, berhenti dan tanya — jangan
hantar UI yang melanggarnya. Berakar pada `plans/ui-overhaul.md`.

## 1. Mobile-first, selalu

- **Tulis untuk telefon 360–430px terlebih dahulu**, kemudian tingkatkan ke atas dengan kueri media `min-width` / sifat titik putus MudBlazor. Jangan pernah desktop-first dengan pelangkah `max-width`.
- **Tiada scroll mendatar pada lebar apa pun 320–1920px.** Jika kandungan lebih luas daripada viewport, itu adalah pepijat.
- Sasaran sentuh ≥ **44px** (`var(--app-touch-target)`). Input teks ≥ 16px fon (menghentikan zoom-on-focus iOS).
- Hormati takuk: gunakan `env(safe-area-inset-*)`; viewport sudah menetapkan `viewport-fit=cover`.
- Hormati `prefers-reduced-motion` — tiada maklumat penting yang hanya disampaikan oleh animasi.

## 2. Token reka bentuk — tiada nilai yang dikodkan keras

- Semua warna/jejari/jarak datang dari **token reka bentuk**: tema MudBlazor (`Web/Components/Theme.cs`) +
  sifat CSS kustom yang dipancarkan oleh `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Jangan pernah mengkodkan keras warna hex, jejari, atau rentetan jenama dalam komponen atau peraturan CSS.** Baca token.
  Token mengalir daripada pilihan jenama putih `BrandingOptions`, jadi palet penjual semula mesti mencapai UI anda secara percuma.
- Nilai baharu yang mempengaruhi jenama → tambah token + medan penjenamaan; jangan sisipkannya.

## 3. Tata letak responsif & data

- **Jadual runtuh menjadi kad di telefon.** Setiap `MudTable` menetapkan `Breakpoint="Breakpoint.Sm"` dan setiap
  `MudTd` mempunyai `DataLabel`. Tiada jadual lebar mentah di mobile. (Templat: `Components/Pages/Nodes.razor`.)
- Grid: `MudItem xs="12" sm="6" md="4"` — lebar penuh di telefon, berbilang lajur ke atas.
- Bentuk lajur tunggal di mobile; sasaran ketukan besar; `inputmode`/`autocomplete` pada input; inputmode angka/perpuluhan
  untuk wang/peratusan.
- **Kawalan yang sesuai untuk input berstruktur — jangan pernah kotak teks mentah untuk nombor atau senarai.** Kumpulkan nombor,
  wang, peratusan, tarikh, enum dan sebarang data pelbagai nilai dengan kawalan yang betul (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, senarai baris boleh edit jenis, atau jadual), setiap medan disahkan secara berasingan. Satu
  `MudTextField` teks bebas yang mesti ditaip pengguna dengan koma/ruang/baris baru yang dipisahkan — yang kemudian anda huraikan — adalah **dilarang**: ia adalah sangat ralat, tidak disahkan, dan bermusuhan di telefon. **Tiada siapa yang ingin menaip blob.** Input pelbagai nilai adalah senarai boleh sunting baris jenis (tambah/
  keluarkan), atau dimuatkan daripada data domain sedia ada (cth. jalankan semakan terus dari ujian belakang yang selesai
  dan bukannya memasukkan semula nombornya). Plain `MudTextField` hanya untuk teks bebas tulen — nama, nota,
  carian, penerangan.
- Sediakan **pemuatan, kosong, dan ralat** keadaan pada setiap senarai/perincian — bersaiz untuk mobile.
- **Navigasi bawah** mobile (`Components/Layout/BottomNav.razor`) adalah navigasi telefon utama; laci berkumpulan adalah menu penuh. Tambah destinasi lalu lintas tinggi di sana; pastikan ≤5 item.

## 4. Dialog (buat/sunting)

- Semua tindakan tambah/buat/sunting/baharu menggunakan **dialog MudBlazor** (`IDialogService.ShowAsync<TDialog>`), jangan pernah
  bentuk halaman sebaris. Dialog hidup dalam `Web/Components/Dialogs/`, tunjukkan `[Parameter]`s, kembalikan bersarang
  `public sealed record …Result(...)`. Tindakan baris senarai (mula/berhenti/padam) kekal sebaris sebagai butang ikon.
- Di telefon, dialog sepatutnya **skrin penuh / lebar penuh** dan kesedaran papan kunci.

## 5. Bantuan sebaris — setiap kawalan

- Setiap pilihan yang tidak jelas, pilih, suis, atau tindakan mendapat **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover di desktop, **ketuk di mobile**. Sumber teks daripada `docs/` jadi
  panduan tetap dalam sinkronisasi dengan tingkah laku; kemas kini kedua-duanya dalam komit yang sama.

## 6. Jenama putih

- Nama produk, logo, penerangan, sokongan/syarikat, warna, favicon semuanya berasal daripada `BrandingOptions`.
  Rujuk mereka (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), jangan pernah literal "cMind" atau
  warna jenama. Manifes PWA, ikon, warna tema, dan hero login semua berjenama.

## 7. PWA

- Aplikasi ini boleh dipasang. Pastikan titik akhir manifes (`/manifest.webmanifest`) berjenama, ikon hadir
  (192/512/maskable + apple-touch), pekerja perkhidmatan app-shell-hanya (tidak pernah menyentuh litar Blazor/
  `_framework`/hubs), dan halaman luar talian berfungsi. Laluan statik baharu → pastikan manifes `scope`.
- Blazor Server memerlukan litar SignalR langsung → **boleh dipasang + app-shell**, bukan luar talian penuh. Jangan
  janji interaktiviti luar talian.

## 8. Kebolehaksesan

- Label pada input, `aria-*` pada kawalan kustom, fokus yang dapat dilihat, susunan fokus yang logik. Kerana tema itu
  boleh dijenama putih, sahkan **kontras** terhadap tema aktif, bukan palet tetap.

## 9. E2E — tiada UI dihantarkan tanpa ujian (menyekat)

Setiap perubahan yang dihadapi pengguna menghantar Playwright E2E dalam `tests/E2ETests`, didorong seperti pengguna sebenar, **pada emulasi peranti mobile** tambah desktop:

- Laluan baharu → tambahkannya ke `PageSmokeTests` **dan** `MobileLayoutTests` (memberikan, navigasi bawah, tiada UI ralat).
- Tukarkan jadual/halaman → tambahkan lalaunya ke set **tiada-limpahan** mobile.
- Aliran baharu → perjalanan mobile yang realistik (buat/sunting/simpan perjalanan bulat) **dan** laluan murka
  (input tidak sah, senarai kosong, kebenaran-dinafikan per peranan).
- Petua bantuan baharu → tegaskan ia terbuka di ketukan (`HelpTipTests` corak).
- Gunakan `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulasi peranti).
- `dotnet test` hijau sebelum "selesai". WebKit teremulasi ≠ Safari mudah alih — pintu penggating peranti sebenar adalah langkah keluaran terpisah.

## 10. Definisi siap (UI)

- [ ] Mobile-first; tiada limpahan mendatar 320–1920px; sasaran sentuh ≥44px.
- [ ] Hanya token reka bentuk — warna/jejari/rentetan jenama yang dikodkan keras sifar.
- [ ] Jadual → kad di telefon (`DataLabel` + `Breakpoint.Sm`); keadaan pemuatan/kosong/ralat hadir.
- [ ] Input berstruktur menggunakan kawalan yang disahkan dengan betul (angka/tarikh/pilih/senarai baris boleh sunting) — kotak teks mentah yang pengguna taip blob nilai/nombor yang dipisahkan tiada.
- [ ] Buat/sunting melalui dialog; skrin penuh di mobile.
- [ ] Setiap kawalan mempunyai `HelpTip` yang bersumber daripada dokumentasi.
- [ ] Jenama putih + PWA dihormati.
- [ ] E2E mobile + desktop ditambah (asap, tiada-limpahan, perjalanan, laluan murka); `dotnet test` hijau.
- [ ] Penunggang `get_file_problems` + `dotnet format analyzers` bersih pada fail yang disentuh.
