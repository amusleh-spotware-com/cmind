---
description: "Závazné pravidlo pro každou novou nebo změněnou část UI v této aplikaci (stránky Blazor, dialogy, komponenty). Toto je zdrojová pravda odkazovaná v CLAUDE.md. Pokud vás pravidlo blokuje, zastavte se a zeptejte se — nepublikujte UI, které by ho porušovalo."
---

# Pokyny k návrhu UI — POVINNÉ

Závazné pravidlo pro **každou** novou nebo změněnou část UI v této aplikaci (stránky Blazor, dialogy, komponenty).
Toto je zdrojová pravda odkazovaná v `CLAUDE.md`. Pokud vás pravidlo blokuje, zastavte se a zeptejte se — nepublikujte
UI, které by ho porušovalo. Vychází z `plans/ui-overhaul.md`.

## 1. Mobilní přístup, vždy

- **Autor pro telefon o rozměrech 360–430px nejdřív**, pak vylepšete směrem nahoru pomocí `min-width` media queries / vlastností breakpointu MudBlazor. Nikdy ne desktop-first s `max-width` přepsáními.
- **Bez horizontálního posuvu na žádné šířce 320–1920px.** Pokud je obsah širší než viewport, jedná se o chybu.
- Plochy dotyku ≥ **44px** (`var(--app-touch-target)`). Textové vstupy ≥ 16px písmo (zabraňuje zvětšení zaostření iOS).
- Respektujte zářezy: použijte `env(safe-area-inset-*)`; viewport již nastavuje `viewport-fit=cover`.
- Dodržujte `prefers-reduced-motion` — žádné podstatné informace přenášené pouze animací.

## 2. Design tokeny — bez pevných hodnot

- Všechny barvy/poloměry/mezery pocházejí z **design tokenů**: motiv MudBlazor (`Web/Components/Theme.cs`) +
  vlastnosti CSS vydané `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikdy nekódujte barvu, poloměr nebo značkovací řetězec do komponenty nebo pravidla CSS.** Přečtěte si token.
  Tokeny pocházejí z bílé značky `BrandingOptions`, takže paleta prodejce musí bez nákladů dosáhnout vašeho UI.
- Nová hodnota ovlivňující značku → přidejte token + pole branding; nevkládejte ji inline.

## 3. Responzivní rozložení a data

- **Tabulky se na telefonech sbalují na karty.** Každá `MudTable` nastavuje `Breakpoint="Breakpoint.Sm"` a každá
  `MudTd` má `DataLabel`. Bez nezpracované širší tabulky na mobilní. (Šablona: `Components/Pages/Nodes.razor`.)
- Mřížky: `MudItem xs="12" sm="6" md="4"` — plná šířka na telefonu, více sloupců směrem nahoru.
- Formuláře jednoho sloupce na mobilní; velké plochy dotyku; `inputmode`/`autocomplete` na vstupech; numeric/decimal
  inputmode pro peníze/procento.
- **Správné ovládací prvky pro strukturovaný vstup — nikdy nezpracovaný textový box pro čísla nebo seznamy.** Sbírejte čísla,
  peníze, procenta, data, výčty a jakákoli data s více hodnotami se správným ovládacím prvkem (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, upravitelný seznam řádků se zadanými poli nebo tabulka), každé pole je
  jednotlivě ověřeno. Jeden volný text `MudTextField`, který uživatel musí zadat do objektu oddeleného čárkou/mezerou/novým řádkem — který pak analyzujete — je **zakázán**: je náchylný na chyby, neověřený a nepřátelský
  na telefonu. **Nikdo nechce psát objekt.** Vstup více hodnot je upravitelný seznam zadaných řádků (přidej /
  odeber), nebo je načten z existujících domén dat (např. spustit kontrolu přímo z dokončeného backtestu
  místo opětovného zadávání jeho čísel). Běžný `MudTextField` je pouze pro opravdový volný text — jména, poznámky,
  hledání, popisy.
- Poskytujte **načítání, prázdné a chybové** stavy na každém seznamu/detailu — změněné velikostí pro mobilní.
- Mobilní **dolní navigace** (`Components/Layout/BottomNav.razor`) je primární navigace telefonu; skupinovaný zásuvník je plná nabídka. Přidejte tam cíle s vysokou návštěvností; udržujte to ≤5 položkami.

## 4. Dialogy (vytváření/úprava)

- Všechny akce přidání/vytvoření/úpravy/nová používají **dialog MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nikdy
  inline formulář stránky. Dialogy jsou umístěny v `Web/Components/Dialogs/`, odhalují `[Parameter]`s, vrátí vnořené
  `public sealed record …Result(...)`. Akce řádku seznamu (spuštění/zastavení/odstranění) zůstávají inline jako ikony.
- Na telefonech by měly být dialogy **na celou obrazovku / plná šířka** a vědomé klávesnice.

## 5. Nápověda inline — každý ovládací prvek

- Každá nesamozřejmá možnost, výběr, přepínač nebo akce dostane **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — přejetí kurzorem na ploše, **klepnutí na mobilní**. Zdroj text z `docs/` takže
  pokyny zůstávají v souladu s chováním; aktualizujte oba ve stejné potvrzení.

## 6. Bílá značka

- Název produktu, logo, popis, podpora/společnost, barvy, favicon všechno pochází z `BrandingOptions`.
  Odkazujte na ně (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikdy doslova "cMind" nebo
  barvu značky. Manifest PWA, ikony, theme-color a přihlašovací hrdina jsou všechny značkované.

## 7. PWA

- Aplikace je instalovatelná. Udržujte koncový bod manifestu (`/manifest.webmanifest`) značkovaný, ikony přítomné
  (192/512/maskable + apple-touch), service worker pouze app-shell (nikdy se nedotkne okruhu Blazor/`_framework`/hubs), a offline stránka funguje. Nová statická trasa → udržujte manifest `scope`.
- Blazor Server potřebuje živý okruh SignalR → **instalovatelný + app-shell**, ne plná offline. Nepobídejte offline interaktivitu.

## 8. Dostupnost

- Popisky na vstupech, `aria-*` na vlastních ovládacích prvcích, viditelné zaměření, logické pořadí zaostření. Protože motivů je
  bílá značka, ověřte **kontrast** aktivního motivu, ne pevné palety.

## 9. E2E — žádné UI se neposílá bez testování (blokování)

Každá změna viditelná uživatelem je odesílána Playwright E2E v `tests/E2ETests`, řízená jako skutečný uživatel, **na emulaci mobilního
zařízení** plus plocha:

- Nová trasa → přidejte ji do `PageSmokeTests` **a** `MobileLayoutTests` (vykreslí se, dolní navigace, bez chyby UI).
- Převod tabulky/stránky → přidejte její trasu do mobilní sady **bez přetečení**.
- Nový tok → realistická mobilní cesta (vytvoření/úprava/uložení obousměrné) **a** nešťastná cesta
  (neplatný vstup, prázdný seznam, přístup odepřen podle role).
- Nová nápověda → potvrďte, že se otevře na klepnutí (`HelpTipTests` vzor).
- Použijte `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulace zařízení).
- `dotnet test` zelený před "hotovo". Emulovaný WebKit ≠ mobilní Safari — gating skutečného zařízení je samostatný
  krok vydání.

## 10. Definice hotovo (UI)

- [ ] Mobilní přístup; bez horizontálního přetečení 320–1920px; dotyk cíle ≥44px.
- [ ] Pouze design tokeny — nula pevně kódovaných barev/poloměrů/značkovacích řetězců.
- [ ] Tabulky → karty na telefonu (`DataLabel` + `Breakpoint.Sm`); stavy načítání/prázdný/chyba přítomné.
- [ ] Strukturovaný vstup používá správné ověřené ovládací prvky (numeric/date/select/upravitelný řádek seznamu) — ne nezpracovaný
      textový box, do kterého uživatel zadá odděleného objekt číslo/hodnota.
- [ ] Vytváření/úprava přes dialog; celá obrazovka na mobilní.
- [ ] Každý ovládací prvek má `HelpTip` pocházející z dokumentů.
- [ ] Bílá značka + PWA respektovány.
- [ ] Mobilní + plocha E2E přidáno (kouř, bez přetečení, cesta, nešťastná cesta); `dotnet test` zelený.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` čisté na dotčených souborech.
