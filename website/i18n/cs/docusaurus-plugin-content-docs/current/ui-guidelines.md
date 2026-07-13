---
description: "Závazné pro každý nový nebo změněný kus UI v této aplikaci (Blazor pages, dialogy, komponenty). Zdroj pravdy referenced by CLAUDE.md."
---

# UI Design Guidelines — ZÁVAZNÉ

Závazné pro **každý** nový nebo změněný kus UI v této aplikaci (Blazor pages, dialogy, komponenty).
Toto je zdroj pravdy referenced by `CLAUDE.md`. Pokud pravidlo blokuje, zastavte se a zeptejte se — nešijte
UI které ho porušuje. Rooteds in `plans/ui-overhaul.md`.

## 1. Mobile-first, vždy

- **Autor pro 360–430px telefon nejdříve**, pak vylepšujte nahoru s `min-width` media queries / MudBlazor
  breakpoint props. Nikdy desktop-first s `max-width` přepsáními.
- **Žádný horizontální scroll na jakékoliv šířce 320–1920px.** Pokud je obsah širší než viewport, je to bug.
- Touch targets ≥ **44px** (`var(--app-touch-target)`). Textové vstupy ≥ 16px font (zastaví iOS zoom-on-focus).
- Respektujte notches: použijte `env(safe-area-inset-*)`; viewport už nastavuje `viewport-fit=cover`.
- Ctěte `prefers-reduced-motion` — žádná esenciální informace sdělovaná pouze animací.

## 2. Design tokens — žádné hard-coded hodnoty

- Veškerá barva/radius/spacing přicházejí z **design tokens**: MudBlazor téma (`Web/Components/Theme.cs`) +
  CSS custom properties emitované `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikdy nepište hard-coded hex barvu, radius nebo brand string do komponenty nebo CSS pravidla.** Čtěte token.
  Tokeny proudí z white-label `BrandingOptions`, takže paleta reseller musí dosáhnout vašeho UI zadarmo.
- Nová hodnota ovlivňující brand → přidejte token + branding field; neinlineujte to.

## 3. Responzivní layout & data

- **Tabulky se kolapsují na karty na telefonech.** Každý `MudTable` nastaví `Breakpoint="Breakpoint.Sm"` a každý
  `MudTd` má `DataLabel`. Žádný raw wide table na mobile. (Šablona: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — full-width na telefonu, multi-column nahoru.
- Formuláře single-column na mobile; velké tap targets; `inputmode`/`autocomplete` na vstupy; numeric/decimal
  inputmode pro peníze/procenta.
- Poskytujte **loading, empty a error** stavy na každém listu/detailu — sized for mobile.
- Mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) je primary phone nav; grouped drawer je full menu.
  Přidejte tam vysoko-frekventované destinace; držte to ≤5 položek.

## 4. Dialogy (create/edit)

- Všechny add/create/edit/new akce používají **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), nikdy
  inline page form. Dialogy žijí v `Web/Components/Dialogs/`, vystavují `[Parameter]`s, vrací nested
  `public sealed record …Result(...)`. List row akce (start/stop/delete) zůstávají inline jako ikonová tlačítka.
- Na telefonech by měly být dialogy **full-screen / full-width** a vědět o klávesnici.

## 5. Inline help — každý ovládací prvek

- Každá ne-zřejmá volba, select, switch nebo akce dostane **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover na desktopu, **tap na mobile**. Zdroj textu z `docs/`, takže
  guidance zůstává synchronizovaná s chováním; aktualizujte obě ve stejném commitu.

## 6. White-label

- Název produktu, logo, popis, support/společnost, barvy, favicon vše přicházejí z `BrandingOptions`.
  Reference je (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikdy literal "cMind" nebo
  brand barva. PWA manifest, ikony, theme-color a login hero jsou všechny branded.

## 7. PWA

- Aplikace je instalovatelná. Držte manifest endpoint (`/manifest.webmanifest`) branded, ikony přítomné
  (192/512/maskable + apple-touch), service worker pouze app-shell (nikdy se nedotýká Blazor
  circuit/`_framework`/hubs) a offline stránka funguje. Nová statická route → držte manifest `scope`.
- Blazor Server potřebuje live SignalR circuit → **installable + app-shell**, ne plný offline.
  Neslibujte offline interaktivitu.

## 8. Accessibility

- Popisky na vstupy, `aria-*` na vlastních ovládacích prvcích, viditelný focus, logické focus order.
  Protože téma je white-labelable, ověřujte **contrast** proti aktivnímu tématu, ne fixní paletě.

## 9. E2E — žádné UI neexpeduje netestované (blokující)

Každá user-facing změna expeduje Playwright E2E v `tests/E2ETests`, řízeno jako skutečný uživatel, **na mobile
device emulaci** plus desktop:

- Nová route → přidejte ji do `PageSmokeTests` **a** `MobileLayoutTests` (renderuje se, bottom nav, žádný error UI).
- Konvertujete table/page → přidejte její route do mobile **no-overflow** sady.
- Nový flow → realistická mobile journey (create/edit/save round-trip) **a** nehappy path
  (invalid input, empty list, permission-denied per role).
- Nový help tip → assert že se otevírá na tap (`HelpTipTests` pattern).
- Použijte `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulace).
- `dotnet test` green před "done". Emulovaný WebKit ≠ mobile Safari — real-device gating je separátní
  release step.

## 10. Definition of done (UI)

- [ ] Mobile-first; žádný horizontální overflow 320–1920px; touch targets ≥44px.
- [ ] Pouze design tokens — žádné hard-coded barvy/radii/brand strings.
- [ ] Tabulky → karty na telefonu (`DataLabel` + `Breakpoint.Sm`); loading/empty/error stavy přítomny.
- [ ] Create/edit přes dialog; full-screen na mobile.
- [ ] Každý ovládací prvek má `HelpTip` sourced z docs.
- [ ] White-label + PWA respektovány.
- [ ] Mobile + desktop E2E přidány (smoke, no-overflow, journey, nehappy path); `dotnet test` green.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean na touched souborech.
