---
description: "Binding pre každý nový alebo zmenený kus UI v tejto aplikácii (Blazor pages, dialogs, components). Toto je zdroj pravdy odkazovaný CLAUDE.md. Ak pravidlo vás blokuje..."
---

# UI Design Guidelines — POVINNÉ

Binding pre **každý** nový alebo zmenený kus UI v tejto aplikácii (Blazor pages, dialogy, komponenty).
Toto je zdroj pravdy odkazovaný `CLAUDE.md`. Ak pravidlo vás blokuje, zastavte sa a spýtajte — nepošlite UI, ktorý to porušuje. Zakorenené v `plans/ui-overhaul.md`.

## 1. Mobile-first, vždy

- **Autor pre 360–430px telefón prvý**, potom zlepšujú s `min-width` media queries / MudBlazor
  breakpoint props. Nikdy desktop-first s `max-width` overrides.
- **Žádny horizontal scroll v ľubovoľnej šírke 320–1920px.** Ak obsah je širší ako viewport, je to bug.
- Touch targets ≥ **44px** (`var(--app-touch-target)`). Text inputs ≥ 16px font (zastaví iOS zoom-on-focus).
- Respect notches: use `env(safe-area-inset-*)`; viewport už nastaví `viewport-fit=cover`.
- Honor `prefers-reduced-motion` — žádne podstatné info prenesené iba animáciou.

## 2. Design tokens — bez hard-coded values

- Všechny color/radius/spacing pochádzajú z **design tokens**: MudBlazor theme (`Web/Components/Theme.cs`) +
  CSS custom properties emitované `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikdy hard-code hex color, radius alebo brand string v component alebo CSS rule.** Čítajte token.
  Tokens tečú z white-label `BrandingOptions`, takže reseller paleta musí dosiahnuť váš UI zadarmo.
- Nová brand-affecting value → pridajte token + branding field; nepristupujte do nej.

## 3. Responsive layout & data

- **Tabuľky sa zomknú na karty na telefóne.** Každý `MudTable` nastavuje `Breakpoint="Breakpoint.Sm"` a každá
  `MudTd` má `DataLabel`. Žádna raw wide table na mobile. (Template: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — full-width na telefóne, multi-column upward.
- Forms single-column na mobile; veľké tap targets; `inputmode`/`autocomplete` na inputs; numeric/decimal
  inputmode pre peniaze/percent.
- Poskytnite **loading, empty a error** states na každom liste/detail — sized pre mobile.
- Mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) je primárna phone nav; grouped drawer je full menu. Pridajte high-traffic destinations tam; udržujte to ≤5 items.

## 4. Dialogy (create/edit)

- Všechny add/create/edit/new akcie používajú **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), nikdy
  inline page form. Dialogy žijú v `Web/Components/Dialogs/`, vystavujú `[Parameter]`s, vraciť nested
  `public sealed record …Result(...)`. List row actions (start/stop/delete) ostanú inline ako icon buttons.
- Na telefóne, dialogy by mali byť **full-screen / full-width** a keyboard-aware.

## 5. Inline help — každý control

- Každá non-obvious option, select, switch alebo akcia dostáva **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover na desktop, **tap na mobile**. Source text z `docs/` takže
  guidance ostáva v sync s behaviour; update obojch v rovnakom commite.

## 6. White-label

- Product name, logo, description, support/company, farby, favicon všetko pochádza z `BrandingOptions`.
  Reference je (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikdy literal "cMind" alebo
  brand farba. PWA manifest, ikony, theme-color a login hero sú všetko branded.

## 7. PWA

- Aplikácia je installable. Udržujte manifest endpoint (`/manifest.webmanifest`) branded, ikony present
  (192/512/maskable + apple-touch), service worker app-shell-only (nikdy dotýkajúce sa Blazor
  circuit/`_framework`/hubs) a offline page working. Nová static route → udržujte manifest `scope`.
- Blazor Server potrebuje live SignalR circuit → **installable + app-shell**, nie full offline. Neobľubujte
  offline interaktivity.

## 8. Dostupnosť

- Labels na inputs, `aria-*` na custom controls, viditeľný focus, logický focus order. Pretože tema je
  white-labelable, verificovať **contrast** proti active theme, nie fixed paleta.

## 9. E2E — no UI ships untested (blocking)

Každá user-facing zmena ships Playwright E2E v `tests/E2ETests`, driven ako real user, **na mobile
device emulation** plus desktop:

- Nová route → pridajte ju do `PageSmokeTests` **a** `MobileLayoutTests` (renders, bottom nav, no error UI).
- Convert table/page → pridajte jej route do mobile **no-overflow** set.
- Nový flow → realistic mobile journey (create/edit/save round-trip) **a** unhappy path
  (invalid input, empty list, permission-denied per role).
- Nový help tip → assert to opens na tap (`HelpTipTests` pattern).
- Use `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation).
- `dotnet test` green pred "done". Emulated WebKit ≠ mobile Safari — real-device gating je oddelený
  release step.

## 10. Definition of done (UI)

- [ ] Mobile-first; no horizontal overflow 320–1920px; touch targets ≥44px.
- [ ] Len design tokens — nula hard-coded colours/radii/brand strings.
- [ ] Tables → cards na phone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error states present.
- [ ] Create/edit cez dialog; full-screen na mobile.
- [ ] Každý control má `HelpTip` sourced z docs.
- [ ] White-label + PWA respected.
- [ ] Mobile + desktop E2E added (smoke, no-overflow, journey, unhappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean na touched files.
