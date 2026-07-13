---
description: "Binding untuk setiap UI baru atau berubah dalam aplikasi ini (halaman Blazor, dialog, komponen). Ini adalah source of truth yang direferensikan oleh CLAUDE.md. Jika rule memblok Anda, stop dan tanya — jangan kirim UI yang melanggarnya. Berakar di plans/ui-overhaul.md."
---

# Panduan Desain UI — MANDATORY

Binding untuk **setiap** UI baru atau berubah dalam aplikasi ini (halaman Blazor, dialog, komponen). Ini adalah source of truth yang direferensikan oleh `CLAUDE.md`. Jika rule memblok Anda, stop dan tanya — jangan kirim UI yang melanggarnya. Berakar di `plans/ui-overhaul.md`.

## 1. Mobile-first, selalu

- **Author untuk 360–430px phone terlebih dahulu**, kemudian enhance keatas dengan `min-width` media query / MudBlazor breakpoint prop. Tidak pernah desktop-first dengan `max-width` override.
- **Tidak ada horizontal scroll pada width apa pun 320–1920px.** Jika content lebih lebar dari viewport, itu bug.
- Touch target ≥ **44px** (`var(--app-touch-target)`). Text input ≥ 16px font (stop iOS zoom-on-focus).
- Hormati notch: gunakan `env(safe-area-inset-*)`; viewport sudah set `viewport-fit=cover`.
- Hormati `prefers-reduced-motion` — tidak ada info essential yang conveyed hanya oleh animation.

## 2. Design token — tidak ada hard-coded value

- Semua colour/radius/spacing berasal dari **design token**: MudBlazor theme (`Web/Components/Theme.cs`) + CSS custom property yang di-emit oleh `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Jangan pernah hard-code hex colour, radius, atau brand string dalam komponen atau CSS rule.** Baca token. Token mengalir dari white-label `BrandingOptions`, jadi palette reseller harus reach UI Anda secara free.
- Nilai baru yang brand-affecting → tambahkan token + branding field; jangan inline.

## 3. Responsive layout & data

- **Tabel collapse ke card di phone.** Setiap `MudTable` set `Breakpoint="Breakpoint.Sm"` dan setiap `MudTd` punya `DataLabel`. Tidak ada raw wide table di mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grid: `MudItem xs="12" sm="6" md="4"` — full-width di phone, multi-column keatas.
- Form single-column di mobile; large tap target; `inputmode`/`autocomplete` di input; numeric/decimal inputmode untuk money/percent.
- Sediakan **loading, empty, dan error** state di setiap list/detail — sized untuk mobile.
- Mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) adalah primary phone nav; grouped drawer adalah full menu. Tambahkan high-traffic destination di sana; simpan ≤5 items.

## 4. Dialog (create/edit)

- Semua add/create/edit/new action gunakan **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), tidak pernah inline page form. Dialog hidup di `Web/Components/Dialogs/`, expose `[Parameter]`, return nested `public sealed record …Result(...)`. List row action (start/stop/delete) tetap inline sebagai icon button.
- Di phone, dialog seharusnya **full-screen / full-width** dan keyboard-aware.

## 5. Inline help — setiap kontrol

- Setiap non-obvious option, select, switch, atau action mendapat **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — hover di desktop, **tap di mobile**. Source text dari `docs/` sehingga guidance tetap sync dengan behaviour; update keduanya dalam commit yang sama.

## 6. White-label

- Nama produk, logo, deskripsi, support/company, warna, favicon semua berasal dari `BrandingOptions`. Reference (IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), tidak pernah literal "cMind" atau brand colour. PWA manifest, icon, theme-color, dan login hero semua branded.

## 7. PWA

- App adalah installable. Simpan manifest endpoint (`/manifest.webmanifest`) branded, icon present (192/512/maskable + apple-touch), service worker app-shell-only (tidak pernah touching Blazor circuit/`_framework`/hub), dan offline page working. Route statis baru → simpan manifest `scope`.
- Blazor Server butuh live SignalR circuit → **installable + app-shell**, tidak full offline. Jangan janji offline interactivity.

## 8. Accessibility

- Label di input, `aria-*` di custom control, visible focus, logical focus order. Karena theme adalah white-labelable, verify **contrast** terhadap active theme, bukan fixed palette.

## 9. E2E — tidak ada UI ship untested (blocking)

Setiap user-facing change ship Playwright E2E di `tests/E2ETests`, driven seperti real user, **di mobile device emulation** plus desktop:

- Route baru → tambahkan ke `PageSmokeTests` **dan** `MobileLayoutTests` (render, bottom nav, tidak error UI).
- Convert tabel/halaman → tambahkan route-nya ke mobile **no-overflow** set.
- Alur baru → journey mobile realistic (create/edit/save round-trip) **dan** unhappy path (invalid input, empty list, permission-denied per role).
- Help tip baru → assert itu open pada tap (`HelpTipTests` pattern).
- Gunakan `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation).
- `dotnet test` green sebelum "done". Emulated WebKit ≠ mobile Safari — real-device gating adalah separate release step.

## 10. Definition of done (UI)

- [ ] Mobile-first; tidak ada horizontal overflow 320–1920px; touch target ≥44px.
- [ ] Hanya design token — zero hard-coded colour/radii/brand string.
- [ ] Tabel → card di phone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error state present.
- [ ] Create/edit via dialog; full-screen di mobile.
- [ ] Setiap kontrol punya `HelpTip` sourced dari docs.
- [ ] White-label + PWA respected.
- [ ] Mobile + desktop E2E added (smoke, no-overflow, journey, unhappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean di file yang touched.
