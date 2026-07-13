---
description: "Binding pro každý nový nebo změněný kus UI v této aplikaci (Blazor pages, dialogy, komponenty). Toto je zdroj pravdy referenced by CLAUDE.md. Pokud pravidlo blokuje, zastavte se a zeptejte se — nešijte UI které ho porušuje."
---

# UI Design Guidelines — ZÁVAZNÉ

Závazné pro **každý** nový nebo změněný kus UI v této aplikaci (Blazor pages, dialogy, komponenty).
Toto je zdroj pravdy referenced by `CLAUDE.md`. Pokud pravidlo blokuje, zastavte se a zeptejte se — nešijte
UI které ho porušuje. Rooted in `plans/ui-overhaul.md`.

## 1. Mobile-first, vždy

- **Autor pro 360–430px telefon nejdříve**, pak enhance upward with `min-width` media queries / MudBlazor
  breakpoint props. Nikdy desktop-first s `max-width` overrides.
- **Žádný horizontální scroll na jakékoliv šířce 320–1920px.** Pokud je obsah širší než viewport, je to bug.
- Touch targets ≥ **44px** (`var(--app-touch-target)`). Textové vstupy ≥ 16px font (stops iOS zoom-on-focus).
- Respektujte notches: použijte `env(safe-area-inset-*)`; viewport už nastavuje `viewport-fit=cover`.
- Ctěte `prefers-reduced-motion` — žádná essential info conveyed only by animation.

## 2. Design tokens — žádné hard-coded hodnoty

- Veškerá barva/radius/spacing přicházejí z **design tokens**: MudBlazor theme (`Web/Components/Theme.cs`) +
  CSS custom properties emitted by `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikdy nepište hard-coded hex barvu, radius, nebo brand string do komponenty nebo CSS pravidla.** Čtěte token.
  Tokens flow from white-label `BrandingOptions`, takže reseller's palette must reach your UI for free.
- New brand-affecting value → přidejte token + branding field; don't inline it.

## 3. Responsive layout & data

- **Tabulky collapse to cards on phones.** Každý `MudTable` nastaví `Breakpoint="Breakpoint.Sm"` a každý
  `MudTd` má `DataLabel`. Žádný raw wide table on mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — full-width on phone, multi-column upward.
- Formuláře single-column on mobile; large tap targets; `inputmode`/`autocomplete` on inputs; numeric/decimal
  inputmode for money/percent.
- Poskytujte **loading, empty, and error** stavy on every list/detail — sized for mobile.
- Mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) je primary phone nav; the
  grouped drawer is the full menu. Add high-traffic destinations there; keep it ≤5 items.

## 4. Dialogy (create/edit)

- Všechny add/create/edit/new akce používají **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), nikdy
  inline page form. Dialogy žijí v `Web/Components/Dialogs/`, expose `[Parameter]`s, return a nested
  `public sealed record …Result(...)`. List row actions (start/stop/delete) zůstávají inline as icon buttons.
- Na telefonech, dialogy by měly být **full-screen / full-width** and keyboard-aware.

## 5. Inline help — každý ovládací prvek

- Každý non-obvious option, select, switch, nebo action dostane **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover on desktop, **tap on mobile**. Source the text from `docs/` so
  guidance stays in sync with behaviour; update both in the same commit.

## 6. White-label

- Product name, logo, description, support/company, barvy, favicon vše přicházejí z `BrandingOptions`.
  Reference them (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikdy literal "cMind" nebo a
  brand colour. PWA manifest, icons, theme-color, and login hero are all branded.

## 7. PWA

- Aplikace je installable. Keep the manifest endpoint (`/manifest.webmanifest`) branded, icons present
  (192/512/maskable + apple-touch), the service worker app-shell-only (never touching the Blazor
  circuit/`_framework`/hubs), and the offline page working. New static route → keep manifest `scope`.
- Blazor Server potřebuje live SignalR circuit → **installable + app-shell**, not full offline. Nepříslibujte offline interaktivitu.

## 8. Accessibility

- Labels on inputs, `aria-*` on custom controls, visible focus, logical focus order. Protože theme is
  white-labelable, verify **contrast** against the active theme, not a fixed palette.

## 9. E2E — žádné UI neexpeduje netestované (blokující)

Každá user-facing změna shipuje Playwright E2E in `tests/E2ETests`, driven like a real user, **on mobile
device emulation** plus desktop:

- New route → add it to `PageSmokeTests` **and** `MobileLayoutTests` (renders, bottom nav, no error UI).
- Convert a table/page → add its route to the mobile **no-overflow** set.
- New flow → realistic mobile journey (create/edit/save round-trip) **and** an unhappy path
  (invalid input, empty list, permission-denied per role).
- New help tip → assert it opens on tap (`HelpTipTests` pattern).
- Use `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation).
- `dotnet test` green before "done". Emulated WebKit ≠ mobile Safari — real-device gating is a separate
  release step.

## 10. Definition of done (UI)

- [ ] Mobile-first; žádný horizontální overflow 320–1920px; touch targets ≥44px.
- [ ] Pouze design tokens — zero hard-coded colours/radii/brand strings.
- [ ] Tables → cards on phone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error states present.
- [ ] Create/edit přes dialog; full-screen on mobile.
- [ ] Každý ovládací prvek má `HelpTip` sourced from docs.
- [ ] White-label + PWA respected.
- [ ] Mobile + desktop E2E added (smoke, no-overflow, journey, unhappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean on touched files.
