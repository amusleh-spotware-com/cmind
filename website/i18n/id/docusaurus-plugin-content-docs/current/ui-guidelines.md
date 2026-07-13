---
description: "Binding untuk setiap piece baru atau berubah dari UI dalam aplikasi ini (halaman Blazor, dialog, komponen). Ini adalah sumber kebenaran yang direferensikan oleh CLAUDE.md. Jika…"
---

# Pedoman Desain UI — MANDATORY

Binding untuk **setiap** piece baru atau berubah dari UI dalam aplikasi ini (halaman Blazor, dialog, komponen).
Ini adalah sumber kebenaran yang direferensikan oleh `CLAUDE.md`. Jika aturan memblokir Anda, hentikan dan tanya — jangan
kirim UI yang melanggarnya. Berakar dalam `plans/ui-overhaul.md`.

## 1. Mobile-first, selalu

- **Tulis untuk telepon 360–430px pertama**, lalu tingkatkan dengan media query `min-width` / prop breakpoint MudBlazor. Jangan pernah desktop-first dengan override `max-width`.
- **Tidak ada horizontal scroll pada lebar apa pun 320–1920px.** Jika konten lebih lebar dari viewport, itu adalah bug.
- Touch target ≥ **44px** (`var(--app-touch-target)`). Input teks ≥ 16px font (menghentikan iOS zoom-on-focus).
- Hormati notch: gunakan `env(safe-area-inset-*)`; viewport sudah menetapkan `viewport-fit=cover`.
- Hormati `prefers-reduced-motion` — tidak ada info penting yang hanya disampaikan oleh animasi.

## 2. Design token — tanpa hard-coded value

- Semua warna/radius/spacing berasal dari **design token**: tema MudBlazor (`Web/Components/Theme.cs`) +
  CSS custom property yang dipancarkan oleh `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Jangan pernah hard-code warna hex, radius, atau string brand dalam komponen atau aturan CSS.** Baca token.
  Token mengalir dari white-label `BrandingOptions`, jadi palet reseller harus menjangkau UI Anda secara gratis.
- Nilai brand-affecting baru → tambahkan token + branding field; jangan inline-kan.

## 3. Tata letak responsif & data

- **Tabel runtuh menjadi kartu di telepon.** Setiap `MudTable` menetapkan `Breakpoint="Breakpoint.Sm"` dan setiap
  `MudTd` memiliki `DataLabel`. Tidak ada tabel lebar mentah di mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grid: `MudItem xs="12" sm="6" md="4"` — full-width di telepon, multi-kolom ke atas.
- Form single-column di mobile; target tap besar; `inputmode`/`autocomplete` pada input; numeric/decimal
  inputmode untuk money/percent.
- Sediakan **loading, empty, dan error** state pada setiap list/detail — berukuran untuk mobile.
- **Bottom navigation** mobile (`Components/Layout/BottomNav.razor`) adalah nav telepon utama; drawer grouped
  adalah menu penuh. Tambahkan high-traffic destination di sana; simpan ≤5 item.

## 4. Dialog (create/edit)

- Semua add/create/edit/new action menggunakan **dialog MudBlazor** (`IDialogService.ShowAsync<TDialog>`), jangan
  form halaman inline. Dialog hidup di `Web/Components/Dialogs/`, expose `[Parameter]`s, kembalikan nested
  `public sealed record …Result(...)`. List row action (start/stop/delete) tetap inline sebagai icon button.
- Di telepon, dialog harus **full-screen / full-width** dan keyboard-aware.

## 5. Inline help — setiap control

- Setiap opsi non-obvious, select, switch, atau action mendapat **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover di desktop, **tap di mobile**. Source teks dari `docs/` sehingga
  guidance tetap sinkron dengan perilaku; update keduanya dalam commit yang sama.

## 6. White-label

- Nama produk, logo, deskripsi, support/company, warna, favicon semuanya berasal dari `BrandingOptions`.
  Referensi mereka (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), jangan pernah literal "cMind" atau
  warna brand. Manifest PWA, icon, theme-color, dan login hero semuanya bermerek.

## 7. PWA

- Aplikasi dapat diinstal. Jaga endpoint manifest (`/manifest.webmanifest`) bermerek, icon hadir
  (192/512/maskable + apple-touch), service worker app-shell-only (tidak pernah menyentuh circuit Blazor/
  `_framework`/hubs), dan halaman offline bekerja. Rute statis baru → jaga scope manifest.
- Blazor Server memerlukan circuit SignalR live → **installable + app-shell**, bukan offline penuh. Jangan
  janji interaktivitas offline.

## 8. Aksesibilitas

- Label pada input, `aria-*` pada kontrol kustom, focus terlihat, urutan focus logis. Karena tema
  dapat di-white-label, verifikasi **kontras** terhadap tema aktif, bukan palet fixed.

## 9. E2E — tidak ada UI yang dikirim tanpa diuji (blocking)

Setiap perubahan user-facing dikirim Playwright E2E di `tests/E2ETests`, didorong seperti pengguna nyata, **pada
emulasi device mobile** plus desktop:

- Rute baru → tambahkan ke `PageSmokeTests` **dan** `MobileLayoutTests` (renders, bottom nav, tidak ada error UI).
- Konversi tabel/halaman → tambahkan rute-nya ke set **no-overflow** mobile.
- Aliran baru → perjalanan mobile realistis (create/edit/save round-trip) **dan** unhappy path
  (input tidak valid, list kosong, permission-denied per role).
- Help tip baru → assert itu terbuka on tap (`HelpTipTests` pattern).
- Gunakan `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation).
- `dotnet test` hijau sebelum "done". Emulated WebKit ≠ mobile Safari — real-device gating adalah
  langkah rilis terpisah.

## 10. Definisi Done (UI)

- [ ] Mobile-first; tidak ada horizontal overflow 320–1920px; touch target ≥44px.
- [ ] Hanya design token — nol hard-coded warna/radii/string brand.
- [ ] Tabel → card di telepon (`DataLabel` + `Breakpoint.Sm`); loading/empty/error state hadir.
- [ ] Create/edit melalui dialog; full-screen di mobile.
- [ ] Setiap kontrol memiliki `HelpTip` yang sourced dari docs.
- [ ] White-label + PWA dihormati.
- [ ] Mobile + desktop E2E ditambahkan (smoke, no-overflow, journey, unhappy path); `dotnet test` hijau.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` bersih pada file yang disentuh.
