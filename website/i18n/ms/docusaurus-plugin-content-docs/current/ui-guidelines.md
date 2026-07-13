---
description: "Ikatan untuk setiap bahagian UI baharu atau yang ditukar dalam apl ini (halaman Blazor, dialog, komponen). Ini ialah sumber kebenaran yang dirujuk oleh CLAUDE.md. Jika peraturan menyekat anda, berhenti dan tanya — jangan penghantaran UI yang melanggarnya."
---

# Garis Panduan Reka Bentuk UI — MANDatori

Ikatan untuk **setiap** bahagian UI baharu atau yang ditukar dalam apl ini (halaman Blazor, dialog, komponen).
Ini ialah sumber kebenaran yang dirujuk oleh `CLAUDE.md`. Jika peraturan menyekat anda, berhenti dan tanya — jangan
hantar UI yang melanggarnya. Berakar daripada `plans/ui-overhaul.md`.

## 1. Mudah alih-pertama, sentiasa

- **Writ untuk telefon 360–430px dulu**, kemudian tingkatkan ke atas dengan `min-width` media queries / breakpoint MudBlazor
  props. Tidak pernah desktop-pertama dengan `max-width` overrides.
- **Tiada scolition mendatar pada mana-mana lebar 320–1920px.** Jika kandungan lebih lebar dari viewport, itu pepijat.
- Target sentuh ≥ **44px** (`var(--app-touch-target)`). Input teks ≥ 16px fon (menghentikan iOS zoom-on-focus).
- Hormati takik: gunakan `env(safe-area-inset-*)`; viewport sudah tetapkan `viewport-fit=cover`.
- Hormati `prefers-reduced-motion` — tiada maklumat penting yang dibawa oleh animasi sahaja.

## 2. Token reka — tiada nilai yang di-hardcode

- Semua warna/jejari/ruang berasal dari **token reka**: tema MudBlazor (`Web/Components/Theme.cs`) +
  hartanah tersuai CSS yang dipancarkan oleh `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Tidak pernah hard-code warna hex, jejar, atau rentetan jenama dalam komponen atau aturan CSS.** Baca token.
  Token mengalir dari `BrandingOptions` white-label, jadi palet penjual semula harus sampai ke UI anda secara percuma.
- Hartanah nilai yang mempengaruhi jenama baharu → tambah token + medan branding; jangan inline nó.

## 3. Reka letak responsif & data

- **Jadual runtuh kepada kad pada telefon.** Setiap `MudTable` tetapkan `Breakpoint="Breakpoint.Sm"` dan setiap
  `MudTd` ada `DataLabel`. Tiada jadual mentah lebar pada mudah alih. (Templat: `Components/Pages/Nodes.razor`.)
- Grid: `MudItem xs="12" sm="6" md="4"` — lebar penuh pada telefon, multi-lajur ke atas.
- Borang satu-lajur pada mudah alih; target sentuh besar; `inputmode`/`autocomplete` pada input; inputmode numerik/perpuluhan untuk wang/peratus.
- Sediakan keadaan **memuat, kosong, dan ralat** pada setiap senarai/butiran — bersaiz untuk mudah alih.
- **Navigasi bawah** mudah alih (`Components/Layout/BottomNav.razor`) ialah nav telefon utama; laci berkumpulan ialah menu penuh. Tambah destinasi berkelajuan tinggi di sana; kekalkannya ≤5 item.

## 4. Dialog (cipta/edit)

- Semua tindakan tambah/cipta/edit/baru menggunakan **dialog MudBlazor** (`IDialogService.ShowAsync<TDialog>`), tidak pernah
  borang halaman sebaris. Dialog tinggal dalam `Web/Components/Dialogs/`, dedahkan `[Parameter]`s, kembalikan `public sealed record …Result(...)`. Tindakan baris(jalan/mula/padam) kekal sebaris sebagai butang ikon.
- Pada telefon, dialog harus **skrin penuh / lebar penuh** dan sedar keyboard.

## 5. Bantuan sebaris — setiap kawalan

- Setiap pilihan, select, suis, atau tindakan yang tidak jelas mendapat **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover pada desktop, **tekan pada mudah alih**. Sumber teks daripada `docs/` jadi
  panduan kekal sepadan dengan kelakuan; kemas kini kedua-dua dalam komit yang sama.

## 6. White-label

- Nama produk, logo, penerangan, sokongan/syarikat, warna, favicon semua berasal dari `BrandingOptions`.
  Rujukannya (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), tidak pernah literal "cMind" atau
  warna jenama. Manifest PWA, ikon, warna-tema, dan hero daftar masuk semuanya berjenama.

## 7. PWA

- Apl boleh pasang. Kekalkan titik akhir manifest (`/manifest.webmanifest`) berjenama, ikon sedia
  (192/512/maskable + apple-touch), service worker cangkang sahaja (tidak pernah menyentuh litar Blazor
  / `_framework` / hub), dan halaman offline berfungsi. Laluan statik baharu → kekalkan `scope` manifest.
- Blazor Server memerlukan litar SignalR hidup → **boleh pasang + cangkang**, bukan offline sepenuhnya. Jangan
  promising offline interaktiviti.

## 8. Kebolehaksesan

- Label pada input, `aria-*` pada kawalan tersuai, fokus yang kelihatan, urutan fokus logik. Kerana tema adalah
  white-labelable, sahkan **kontras** terhadap tema aktif, bukan palet tetap.

## 9. E2E — tiada UI dihantar tanpa ujian (memblokir)

Setiap perubahan yang menghadap pengguna ships Playwright E2E dalam `tests/E2ETests`, dikendalikan seperti pengguna sebenar, **pada emulasi peranti mudah alih** serta desktop:

- Laluan baharu → tambahkan nó ke `PageSmokeTests` **dan** `MobileLayoutTests` (dipapar, nav bawah, tiada ralat UI).
- Convert jadual/halaman → tambahkan laluannya ke set **tiada limpahan** mudah alih.
- Aliran baharu → perjalanan mudah alih yang realistik (cipta/edit/simpan pusingan) **dan** laluan sedih
  (input tidak sah, senarai kosong, kebenaran ditolak setiap peranan).
- Help tip baharu → assert nó terbuka pada tekan (`HelpTipTests` pattern).
- Gunakan `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulasi peranti).
- `dotnet test` hijau sebelum "selesai". WebKit terenkulasi ≠ Safari Mudah Alih — gerbang peranti sebenar ialah langkah pengeluaran berasingan.

## 10. Definisi selesai (UI)

- [ ] Mudah alih-pertama; tiada limpahan mendatar 320–1920px; target sentuh ≥44px.
- [ ] Hanya token reka — sifar warna/ruang/rentetan jenama keras.
- [ ] Jadual → kad pada telefon (`DataLabel` + `Breakpoint.Sm`); keadaan memuat/kosong/ralat sedia ada.
- [ ] Cipta/edit melalui dialog; skrin penuh pada mudah alih.
- [ ] Setiap kawalan ada `HelpTip` bersumber daripada docs.
- [ ] White-label + PWA dihormati.
- [ ] E2E mudah alih + desktop ditambah (smoke, tiada limpahan, perjalanan, laluan sedih); `dotnet test` hijau.
- [ ] `get_file_problems` + `dotnet format analyzers` bersih pada fail disentuh.
