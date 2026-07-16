---
description: "Záväzná norma pre každú novú alebo upravenú časť používateľského rozhrania v tejto aplikácii (Blazor stránky, dialógy, komponenty). Toto je zdroj pravdy odkazovaný z `CLAUDE.md`. Ak…"
---

# Pokyny pre návrh používateľského rozhrania — POVINNÉ

Záväzná norma pre **každú** novú alebo upravenú časť používateľského rozhrania v tejto aplikácii (Blazor stránky, dialógy, komponenty).
Toto je zdroj pravdy odkazovaný z `CLAUDE.md`. Ak vás nejaké pravidlo zablokuje, zastavte sa a spýtajte sa — neposkytujte rozhranie, ktoré ho porušuje. Zakorienené v `plans/ui-overhaul.md`.

## 1. Mobile-first, vždy

- **Navrhujte najskôr pre telefón 360–430px**, potom rozširujte pomocou `min-width` mediálnych dotazov / vlastností MudBlazor
  breakpoints. Nikdy nejdite od desktopu s `max-width` prepísaniami.
- **Bez horizontálneho skrolovania pri žiadnej šírke 320–1920px.** Ak je obsah širší ako viewport, je to chyba.
- Ciele dotyku ≥ **44px** (`var(--app-touch-target)`). Textové vstupy ≥ 16px font (zabraňuje zväčšovaniu iOS pri fokuse).
- Rešpektujte zárezy: používajte `env(safe-area-inset-*)`; viewport už nastaví `viewport-fit=cover`.
- Dodržujte `prefers-reduced-motion` — nie sú žiadne kritické informácie prenesené iba animáciou.

## 2. Design tokeny — bez pevne zakódovaných hodnôt

- Všetky farby/polomery/rozostúpenie pochádzajú z **design tokenov**: MudBlazor téma (`Web/Components/Theme.cs`) +
  vlastnosti CSS vydávané `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikdy nezakódujte pevne hex farbu, polomer alebo značkovací reťazec v komponente alebo CSS pravidle.** Prečítajte si token.
  Tokeny tečú z `BrandingOptions` v bielom štítku, takže paleta predajcu musí dosiahnuť vaše rozhranie bezplatne.
- Nová hodnota ovplyvňujúca značku → pridajte token + pole značky; nezaraďujte ho na riadku.

## 3. Responzívne rozloženie a údaje

- **Tabuľky sa zbalili na karty na telefónoch.** Každá `MudTable` nastaví `Breakpoint="Breakpoint.Sm"` a každá
  `MudTd` má `DataLabel`. Žiadna surová širká tabuľka na mobiloch. (Šablóna: `Components/Pages/Nodes.razor`.)
- Mreže: `MudItem xs="12" sm="6" md="4"` — plná šírka na telefóne, viacstĺpcovo hore.
- Formuláre jedného stĺpca na mobiloch; veľké ciele dotyku; `inputmode`/`autocomplete` na vstupoch; číselný/desatinný
  inputmode pre peniaze/procenta.
- **Správne ovládacie prvky pre štruktúrovaný vstup — nikdy surová textová políčka pre čísla alebo zoznamy.** Zbierajte čísla,
  peniaze, percentá, dátumy, enumerácie a všetky viachodnoty údaje so správnym ovládacím prvkom (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, upraviteľný zoznam riadkov s typizovanými poľami alebo tabuľka), každé pole
  jednotlivo validované. Jeden voľný text `MudTextField`, ktorý musí používateľ zadať blob oddelený čiarkou/medzerou/nový riadok
  — ktorý potom spracujete — je **zakázaný**: je chybový, nevalidovaný a nepriateľský
  na telefóne. **Nikto nechce zadávať blob.** Viacchodnotový vstup je upraviteľný zoznam typizovaných riadkov (pridať /
  odstrániť), alebo sa načíta z existujúcich doménových údajov (napr. spustiť kontrolu priamo z dokončeného backtesta
  namiesto opätovného zadávania jeho čísel). Jednoduchý `MudTextField` je iba pre skutočný voľný text — mená, poznámky,
  vyhľadávanie, popisy.
- Poskytujte **načítavanie, prázdne a chybové** stavy na každom zozname/detailoch — určené pre mobil.
- Mobilná **spodná navigácia** (`Components/Layout/BottomNav.razor`) je primárna navigácia telefónu; zoskupený
  zásuvka je úplné menu. Pridajte tam vysoký dopyt; udržiavajte to ≤5 položkami.

## 4. Dialógy (vytvoriť/upraviť)

- Všetky akcie pridania/vytvorenia/úpravy/nového používajú **MudBlazor dialóg** (`IDialogService.ShowAsync<TDialog>`), nikdy
  vložený formulár stránky. Dialógy sú v `Web/Components/Dialogs/`, vystavujú `[Parameter]`s, vrátia vnorené
  `public sealed record …Result(...)`. Akcie riadka zoznamu (spustenie/zastavenie/odstránenie) zostávajú vložené ako tlačidlá ikon.
- Na telefónoch by mali byť dialógy **na celú obrazovku / celú šírku** a vedomé klávesnice.

## 5. Vložená pomoc — každý ovládací prvok

- Každá neopačná možnosť, výber, prepínač alebo akcia dostane **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — najetie na plochu, **dotykom na mobilech**. Zdroj textu z `docs/` tak
  návod zostáva v sync s správaním; aktualizujte obe v rovnakom commite.

## 6. Biely štítok

- Názov produktu, logo, popis, podpora/spoločnosť, farby, favicon všetko pochádza z `BrandingOptions`.
  Odkazujte na ne (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikdy doslova "cMind" alebo
  farba značky. Manifest PWA, ikony, téma-farba a login hero sú všetky označené.

## 7. PWA

- Aplikácia je inštalovateľná. Udržiavajte koncový bod manifestu (`/manifest.webmanifest`) označený, ikony prítomné
  (192/512/maskable + apple-touch), service worker len s app-shell (nikdy nedotýka sa Blazor
  circuit/`_framework`/hubs), a offline stránka funguje. Nová statická trasa → udržiavajte manifest `scope`.
- Blazor Server potrebuje živý circuit SignalR → **inštalovateľný + app-shell**, nie úplne offline. Nesľubujte
  offline interaktivitu.

## 8. Dostupnosť

- Štítky na vstupoch, `aria-*` na vlastných ovládacích prvkoch, viditeľný fokus, logické poradie fokusu. Pretože téma je
  biely štítok, overte **kontrast** voči aktívnej téme, nie pevnej palete.

## 9. E2E — žiadne rozhranie sa neposúva neotestované (blokuje)

Každá zmena orientovaná na používateľa sa dodáva s Playwright E2E v `tests/E2ETests`, riadená ako skutočný používateľ, **na mobilnom
emulácii zariadenia** plus plocha:

- Nová trasa → pridajte ju do `PageSmokeTests` **a** `MobileLayoutTests` (vykresľuje, spodná nav, bez chybového rozhrania).
- Konverzia tabuľky/stránky → pridajte jej trasu do mobilného **bez-pretečenia** súboru.
- Nový tok → realistická mobilná cesta (kolo vytvoriť/upraviť/uložiť) **a** nešťastná cesta
  (neplatný vstup, prázdny zoznam, prístup zamietnutý podľa roly).
- Nový tip pomoci → tvrdí, že sa otvorí dotykom (`HelpTipTests` vzorec).
- Používajte `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulácia zariadenia).
- `dotnet test` zelená pred "hotovosťou". Emulované WebKit ≠ mobilný Safari — vrátenie sa na skutočné zariadenie je oddelený
  krok vydania.

## 10. Definícia hotovosti (UI)

- [ ] Mobile-first; bez horizontálneho pretečenia 320–1920px; ciele dotyku ≥44px.
- [ ] Iba design tokeny — nula pevne zakódovaných farieb/polomerov/značkovacích reťazcov.
- [ ] Tabuľky → karty na telefóne (`DataLabel` + `Breakpoint.Sm`); prítomné stavy načítavania/prázdna/chyba.
- [ ] Štruktúrovaný vstup používa správne validované ovládacie prvky (číselný/dátum/výber/upraviteľný riadok zoznamu) — bez surového
      textového poľa, ktoré používateľ zadá oddelený blob čísla/hodnoty do.
- [ ] Vytvoriť/upraviť cez dialóg; celá obrazovka na mobiloch.
- [ ] Každý ovládací prvok má `HelpTip` zdroj z dokumentov.
- [ ] Biely štítok + PWA rešpektované.
- [ ] Mobilný + desktopový E2E pridaný (dym, bez-pretečenia, cesta, nešťastná cesta); `dotnet test` zelená.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` čisté na dotknutých súboroch.
