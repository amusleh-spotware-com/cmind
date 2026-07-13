---
description: "A kötés az alkalmazásban az új vagy megváltozott UI-darabokra (Blazor lapok, párbeszédablakok, összetevők). Ez az igazság forrása, amelyet a CLAUDE.md hivatkozik. Ha a szabály blokkolja, állítsa meg és kérdezzen — ne szállítson UI-t, amely megsérüli."
---

# UI Design Guidelines — KÖTELEZŐ

Kötés az **minden** új vagy megváltozott UI-darabra az alkalmazásban (Blazor lapok, párbeszédablakok, összetevők). Ez az igazság forrása, amelyet a `CLAUDE.md` hivatkozik. Ha a szabály blokkolja, állítsa meg és kérdezzen — ne szállítson UI-t, amely megsérüli. A `plans/ui-overhaul.md`-ben gyökerezik.

## 1. Mobil-első, mindig

- **Szerzőjenek egy 360–430px telefon számára először**, majd javítson felfelé a `min-width` média lekérdezésekkel / MudBlazor szünet pontok paraméterekkel. Soha ne az asztali-először az `max-width` felülbírálások.
- **Nincs vízszintes görgetés semmilyen szélesség 320–1920px-nél.** Ha a tartalom szélesebb, mint a viewport, ez egy hiba.
- Érintett célok ≥ **44px** (`var(--app-touch-target)`). Szöveges bemenetek ≥ 16px betűtípus (megállítja az iOS fókusz-nagyítást).
- Respektálja a bevágásokat: használja az `env(safe-area-inset-*)`; a viewport már beállítja az `viewport-fit=cover`.
- Tiszteletben tartsa az `prefers-reduced-motion` — nincs lényeges információ csak animáció által közvetített.

## 2. Tervezési tokenek — nincs kemény kódolt értékek

- Minden szín/sugár/térközösítés a **tervezési token**-ból származik: MudBlazor téma (`Web/Components/Theme.cs`) + az `Web/Branding/BrandingCss.cs` által kibocsátott CSS egyéni tulajdonságok (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Soha ne egy hex-szín, sugár vagy márka-karakterlánc nem készült be az összetevőben vagy CSS-szabályban.** Olvassa el a tokent. A tokenek a fehér címke `BrandingOptions`-ből áramlanak, így egy átjáró palettája ingyen kell elérnie az UI-t.
- Új márka-érintő érték → adjon hozzá egy tokent + márkaépítési mezőt; ne soroljon fel.

## 3. Reagálékony elrendezés & adatok

- **Táblák összecsukódnak kártyákra a telefonokon.** Minden `MudTable` beállítja az `Breakpoint="Breakpoint.Sm"` és minden `MudTd` egy `DataLabel` van. Nincs nyers széles tábla mobil. (Sablon: `Components/Pages/Nodes.razor`.)
- Rácsok: `MudItem xs="12" sm="6" md="4"` — teljes szélesség a telefon, több oszlop felfelé.
- Formák egyoszlopos a mobil; nagy érintési célok; `inputmode`/`autocomplete` a bemeneteken; numerikus/tizedes inputmode a pénz/százalékra.
- Adjon meg **betöltést, üres és hiba** állapotokat minden listán/részleten — mobil méretű.
- A mobil **alsó navigáció** (`Components/Layout/BottomNav.razor`) az elsődleges telefon-nav; a csoportosított fiók a teljes menü. Adjon hozzá magas forgalmú célhelyeket; tartsa meg ≤5 elemet.

## 4. Párbeszédablakok (létrehozás/szerkesztés)

- Minden hozzáadás/létrehozás/szerkesztés/új acció egy **MudBlazor párbeszédablakot** (`IDialogService.ShowAsync<TDialog>`), soha sem egy soron belüli oldal forma. A párbeszédablakok a `Web/Components/Dialogs/` alatt élnek, kitettésbe `[Parameter]`-ek, egy beágyazott `public sealed record …Result(...)` visszatér. A lista sor-szerkesztések (indítás/leállítás/törlés) maradnak beágyazva ikon gombok.
- Telefonokon a párbeszédablakok **teljes képernyőn / teljes szélesség** és billentyűzet-tudatosak kell lenni.

## 5. Beágyazott segítség — minden kontroll

- Minden nem nyilvánvaló opció, válassza, kapcsoló vagy intézmény egy **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — lebegés az asztali, **tap a mobil**. A szöveget a `docs/` -ból forrozzuk, így az útmutatás szinkronban marad a viselkedéssel; frissítse mindkettőt ugyanabban a commitban.

## 6. Fehér címke

- Terméknév, logó, leírás, támogatás/vállalat, színek, favicon mind az `BrandingOptions`-ből származnak. Hivatkozz rájuk (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), soha nem szó szerint "cMind" vagy márka szín. A PWA kiáltvány, ikonok, téma-szín és bejelentkezés hős mind márkanem.

## 7. PWA

- Az alkalmazás telepítendő. Tartsa meg a kiáltvány végpontot (`/manifest.webmanifest`) márkanem, ikonok jelen (192/512/maszkolható + apple-touch), a szolgáltatási munkavállaló alkalmazás-héj-csak (soha nem érintsen az Blazor áramkört/`_framework`/hubok), és az offline oldal működő. Új statikus útvonal → tartsa meg a kiáltvány `scope`.
- A Blazor szerver kell egy élő SignalR áramkört → **telepítendő + alkalmazás-héj**, nem teljes offline. Ne ígérj offline interaktivitást.

## 8. Megközelíthetőség

- Feliratok a bemeneteken, `aria-*` az egyéni vezérlőkön, látható fókusz, logikai fókusz-rendezés. Mivel a téma fehér-címkézett, ellenőrizze az **kontrasztot** az aktív téma ellen, nem egy rögzített palettát.

## 9. E2E — nincs UI szállít teszteletlen (blokkolás)

Minden felhasználóval néz szél Playwright E2E a `tests/E2ETests`, valódi felhasználóként vezetett, **a mobil eszköz emulálásán** és asztali:

- Új útvonal → adjon hozzá a `PageSmokeTests` **és** `MobileLayoutTests` (megjelenik, alsó nav, nincs hiba UI).
- Konvertáljon egy tábla/oldal → adjon hozzá az útvonalat a mobil **nincs túlcsordulás** készlethez.
- Új folyamat → egy reális mobil utazás (létrehozás/szerkesztés/mentés körút) **és** egy boldogtalan útvonal (érvénytelen bemenet, üres lista, engedély-megtagadva per szerep).
- Új segítség tipp → állítsa, hogy megnyílik a tap (`HelpTipTests` minta).
- Használja az `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (eszköz emulálás).
- `dotnet test` zöld a "kész" előtt. Az emulált WebKit ≠ mobil Safari — a valódi eszköz kapu egy külön kiadás lépés.

## 10. Definíció a befejezésről (UI)

- [ ] Mobil-első; nincs vízszintes túlcsordulás 320–1920px-nél; érintési célok ≥44px.
- [ ] Csak tervezési tokenek — nulla kemény kódolt színek/sugárak/márka-karakterláncok.
- [ ] Táblák → kártyák a telefon (`DataLabel` + `Breakpoint.Sm`); betöltés/üres/hiba állapotok jelen.
- [ ] Létrehozás/szerkesztés párbeszédablak; teljes képernyő a mobil.
- [ ] Minden kontroll van egy `HelpTip` a docs-ból forrozzuk.
- [ ] Fehér címke + PWA tiszteletben tartva.
- [ ] Mobil + asztali E2E hozzáadott (füst, nincs túlcsordulás, utazás, boldogtalan útvonal); `dotnet test` zöld.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` tiszta az érintett fájlokon.
