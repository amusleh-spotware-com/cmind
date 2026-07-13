---
description: "Binding for every new or changed piece of UI in this app (Blazor pages, dialogs, components). This is the source of truth referenced by CLAUDE.md. If a…"
---

# UI Design Guidelines — MANDATORY

Binding for **every** new or changed piece of UI in this app (Blazor pages, dialogs, components).
This is the source of truth referenced by `CLAUDE.md`. If a rule blocks you, stop and ask — don't
ship UI that violates it. Rooted in `plans/ui-overhaul.md`.

## 1. Mobile-first, always

- **Author for a 360–430px phone first**, then enhance upward with `min-width` media queries / MudBlazor
  breakpoint props. Never desktop-first with `max-width` overrides.
- **No horizontal scroll at any width 320–1920px.** If content is wider than the viewport, it's a bug.
- Touch targets ≥ **44px** (`var(--app-touch-target)`). Text inputs ≥ 16px font (stops iOS zoom-on-focus).
- Respect notches: use `env(safe-area-inset-*)`; the viewport already sets `viewport-fit=cover`.
- Honour `prefers-reduced-motion` — no essential info conveyed only by animation.

## 2. Design tokens — no hard-coded values

- All colour/radius/spacing come from **design tokens**: MudBlazor theme (`Web/Components/Theme.cs`) +
  the CSS custom properties emitted by `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Never hard-code a hex colour, radius, or brand string in a component or CSS rule.** Read a token.
  Tokens flow from white-label `BrandingOptions`, so a reseller's palette must reach your UI for free.
- New brand-affecting value → add a token + branding field; don't inline it.

## 3. Responsive layout & data

- **Tables collapse to cards on phones.** Every `MudTable` sets `Breakpoint="Breakpoint.Sm"` and every
  `MudTd` has a `DataLabel`. No raw wide table on mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — full-width on phone, multi-column upward.
- Forms single-column on mobile; large tap targets; `inputmode`/`autocomplete` on inputs; numeric/decimal
  inputmode for money/percent.
- Provide **loading, empty, and error** states on every list/detail — sized for mobile.
- The mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) is the primary phone nav; the
  grouped drawer is the full menu. Add high-traffic destinations there; keep it ≤5 items.

## 4. Dialogs (create/edit)

- All add/create/edit/new actions use a **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), never
  an inline page form. Dialogs live in `Web/Components/Dialogs/`, expose `[Parameter]`s, return a nested
  `public sealed record …Result(...)`. List row actions (start/stop/delete) stay inline as icon buttons.
- On phones, dialogs should be **full-screen / full-width** and keyboard-aware.

## 5. Inline help — every control

- Every non-obvious option, select, switch, or action gets a **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover on desktop, **tap on mobile**. Source the text from `docs/` so
  guidance stays in sync with behaviour; update both in the same commit.

## 6. White-label

- Product name, logo, description, support/company, colours, favicon all come from `BrandingOptions`.
  Reference them (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), never literal "cMind" or a
  brand colour. The PWA manifest, icons, theme-color, and login hero are all branded.

## 7. PWA

- The app is installable. Keep the manifest endpoint (`/manifest.webmanifest`) branded, icons present
  (192/512/maskable + apple-touch), the service worker app-shell-only (never touching the Blazor
  circuit/`_framework`/hubs), and the offline page working. New static route → keep manifest `scope`.
- Blazor Server needs a live SignalR circuit → **installable + app-shell**, not full offline. Don't
  promise offline interactivity.

## 8. Accessibility

- Labels on inputs, `aria-*` on custom controls, visible focus, logical focus order. Because the theme is
  white-labelable, verify **contrast** against the active theme, not a fixed palette.

## 9. E2E — no UI ships untested (blocking)

Every user-facing change ships Playwright E2E in `tests/E2ETests`, driven like a real user, **on mobile
device emulation** plus desktop:

- New route → add it to `PageSmokeTests` **and** `MobileLayoutTests` (renders, bottom nav, no error UI).
- Convert a table/page → add its route to the mobile **no-overflow** set.
- New flow → a realistic mobile journey (create/edit/save round-trip) **and** an unhappy path
  (invalid input, empty list, permission-denied per role).
- New help tip → assert it opens on tap (`HelpTipTests` pattern).
- Use `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation).
- `dotnet test` green before "done". Emulated WebKit ≠ mobile Safari — real-device gating is a separate
  release step.

## 10. Definition of done (UI)

- [ ] Mobile-first; no horizontal overflow 320–1920px; touch targets ≥44px.
- [ ] Only design tokens — zero hard-coded colours/radii/brand strings.
- [ ] Tables → cards on phone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error states present.
- [ ] Create/edit via dialog; full-screen on mobile.
- [ ] Every control has a `HelpTip` sourced from docs.
- [ ] White-label + PWA respected.
- [ ] Mobile + desktop E2E added (smoke, no-overflow, journey, unhappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean on touched files.

<!-- [ZH-HANS] Translation needed -->
