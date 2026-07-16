---
description: "Kötelező minden új vagy megváltozott felhasználói felület elemhez ebben az alkalmazásban (Blazor lapok, dialógusok, komponensek). Ez az a forrás, amit a CLAUDE.md hivatkozik. Ha egy szabály blokkal…"
---

# UI Design Guidelines — KÖTELEZŐ

Kötelező minden új vagy megváltozott felhasználói felület elemhez ebben az alkalmazásban (Blazor lapok, dialógusok, komponensek).
Ez az a forrás, amit a `CLAUDE.md` hivatkozik. Ha egy szabály blokkal, állj meg és kérdezz – ne szállíts olyan felhasználói felületet, amely megsérti. A `plans/ui-overhaul.md` alapján.

## 1. Mobilra tervezett, mindig

- **Végigmegyél a 360–430px-es telefonnál, majd javítasz felfelé `min-width` médiaqueriekkel / MudBlazor
  breakpoint propertikkel. Soha ne asztali-elsőre tervezzél `max-width` felülírásokkal.**
- **Nincs vízszintes görgetés semmilyen szélességben 320–1920px között.** Ha a tartalom szélesebb, mint az ablak, az egy hiba.
- Érintési célpontok ≥ **44px** (`var(--app-touch-target)`). Szöveges beviteli mezők ≥ 16px betűméret (megakadályozza az iOS fókuszbeállítási nagyítást).
- Tisztelj meg bevágásokat: használj `env(safe-area-inset-*)`; az ablak már beállította a `viewport-fit=cover` értéket.
- Tisztelj meg `prefers-reduced-motion` – ne közvetítj alapvető információt csak az animációval.

## 2. Design tokenek – nincs hardkódolt érték

- Minden szín/lekerekítés/térköz a **design tokenekből** származik: MudBlazor tema (`Web/Components/Theme.cs`) +
  a CSS egyéni properties által kibocsátott `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Soha ne hardkódolj hex színt, lekerekítést vagy márkaláncot egy komponensben vagy CSS szabályban.** Olvass egy tokent.
  A tokenek a fehér címkés `BrandingOptions` alapján áramlanak, így egy viszonteladó palettájának ingyenesen el kell érnie a felhasználói felületet.
- Új márka-érintő érték → adjunk hozzá egy tokent + márkamezőt; ne szúrd be közvetlenül.

## 3. Rugalmas elrendezés és adatok

- **Táblázatok kártyákra összeomlanak telefonokon.** Minden `MudTable` beállította a `Breakpoint="Breakpoint.Sm"` értéket és minden
  `MudTd` rendelkezik egy `DataLabel` mezővel. Nincs nyers széles táblázat mobil eszközön. (Sablon: `Components/Pages/Nodes.razor`.)
- Rácshálók: `MudItem xs="12" sm="6" md="4"` – teljes szélesség telefonokon, többoszlopos felfelé.
- Űrlapok egy oszlopos mobil eszközön; nagy érintési célpontok; `inputmode`/`autocomplete` a bemeneteken; numerikus/tizedesjegyek
  inputmód pénz/százalékhoz.
- **Megfelelő vezérlőelemek a strukturált bemenethez – soha nem nyers szövegmező számokhoz vagy listákhoz.** Gyűjts számokat,
  pénzt, százalékokat, dátumokat, enumokat és minden többérték adatot a megfelelő vezérlőelemmel (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, szerkeszthető sor-hozzáadás/eltávolítás lista vagy táblázat), minden mező
  egyénileg validálva. Egy nyers szabad szövegű `MudTextField`, amelyet a felhasználó vesszővel/szóközzel/sortöréssel
  elválasztott blokkot kell beírnia – amit aztán feldolgozol – **tiltott**: hibahajlam, nem validált és ellenséges
  telefonokon. **Senki sem akar blobokat beírni.** A többérték bevitel egy szerkeszthető lista tipizált sorokból (hozzáadás /
  eltávolítás), vagy a meglévő tartomány adataiból töltődik be (pl. közvetlenül futtasd az ellenőrzést a befejezett backtestből
  ahelyett, hogy újra beírná a számokat). Sima `MudTextField` csak valódi szabad szöveghez – nevek, megjegyzések,
  keresés, leírások.
- Adj meg **betöltés, üres és hiba** állapotokat minden lista/részlet – mobil méretű.
- A mobil **alsó navigáció** (`Components/Layout/BottomNav.razor`) az elsődleges telefon navigáció; a
  csoportosított fiók a teljes menü. Adj hozzá nagy forgalmú célpontokat; tartsd ≤5 elem alatt.

## 4. Dialógusok (létrehozás/szerkesztés)

- Minden hozzáadás/létrehozás/szerkesztés/új művelet egy **MudBlazor dialógust** használ (`IDialogService.ShowAsync<TDialog>`), soha
  nem egy beágyazott oldal űrlapot. A dialógusok a `Web/Components/Dialogs/` mappában élnek, egy beágyazott
  `public sealed record …Result(...)` függvényt teszik közzé. A listasor-műveletek (start/stop/törlés) beágyazottan maradnak, mint ikon gombok.
- Telefonokon a dialógusok **teljes képernyős / teljes szélesek** és billentyűzet-tudatosak.

## 5. Beágyazott súgó – minden vezérlőelem

- Minden nem nyilvánvaló lehetőség, kiválasztás, kapcsoló vagy művelet kap egy **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) – lebeg az asztalon, **koppints mobil eszközön**. A szöveg forrásadataiból dolgozd fel a `docs/` dokumentumot, így az
  útmutatás szinkronban marad a viselkedéssel; frissítsd mindkettőt ugyanabban a commitban.

## 6. Fehér címke

- A terméknév, logó, leírás, támogatás/vállalat, színek, favicon mind a `BrandingOptions` alapján származnak.
  Hivatkozz rájuk (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), soha nem szó szerinti "cMind" vagy egy
  márka szín. A PWA manifest, ikonok, theme-szín és login hős mind márkázottak.

## 7. PWA

- Az alkalmazás telepíthető. Tartsd a manifest végpontot (`/manifest.webmanifest`) márkázottá, ikonokat megtalálva
  (192/512/maskable + apple-touch), a service worker csak az alkalmazás-héj (soha nem érintve a Blazor
  circuit-et/`_framework`/hubs), és az offline oldal működőképesen. Új statikus útvonal → tartsd a manifest `scope` értékét.
- A Blazor Server élő SignalR circuit-et igényel → **telepíthető + alkalmazás-héj**, nem teljes offline. Ne ígérj
  offline interaktivitást.

## 8. Akadálymentesítés

- Címkék a beviteleken, `aria-*` az egyéni vezérlőelemeken, látható fókusz, logikus fókusz sorrend. Mivel a tema
  fehér címkézhető, ellenőrizd a **kontraszt** az aktív téma ellen, nem egy rögzített paletta.

## 9. E2E – a felhasználói felület nem szállítódik teszteletlen (blokkoló)

Minden felhasználói felület módosulás a `tests/E2ETests` Playwright E2E szállítódik, egy valódi felhasználóként vezérelve, **mobil
eszköz emulációval** plusz asztali:

- Új útvonal → adj hozzá azt a `PageSmokeTests` **és** `MobileLayoutTests` **hez** (render, alsó nav, nincs hibás felhasználói felület).
- Tábla/oldal átalakítása → add az útvonalát a mobil **nincs-túlcsordulás** halmazhoz.
- Új folyamat → egy reális mobil utazás (létrehozás/szerkesztés/mentés körút) **és** egy boldogtalan útvonal
  (érvénytelen bemenet, üres lista, engedély-megtagadva szerepenként).
- Új súgó tipp → állítsd be, hogy megnyílik a koppintáskor (`HelpTipTests` minta).
- Használj `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (eszköz emulációval).
- `dotnet test` zöld "kész" előtt. Az emulált WebKit ≠ mobil Safari – valódi eszköz gated egy külön
  kiadási lépés.

## 10. Felkészültség definíciója (felhasználói felület)

- [ ] Mobilra tervezett; nincs vízszintes túlcsordulás 320–1920px között; érintési célpontok ≥44px.
- [ ] Csak design tokenek – nulla hardkódolt szín/lekerekítés/márka karakterlánc.
- [ ] Táblázatok → kártyák telefonokon (`DataLabel` + `Breakpoint.Sm`); betöltés/üres/hiba állapotok jelen.
- [ ] Strukturált bemenet megfelelő validált vezérlőelemeket használ (numerikus/dátum/kiválasztás/szerkeszthető sor lista) – nincs nyers
      szövegmező, amelyet a felhasználó szöveggel elválasztott szám/érték blobbot ír be.
- [ ] Létrehozás/szerkesztés dialóguson keresztül; teljes képernyős mobil eszközön.
- [ ] Minden vezérlőelemnek van egy `HelpTip`, amely a docs alapján fordul elő.
- [ ] Fehér címke + PWA tiszteletben tartva.
- [ ] Mobil + asztali E2E hozzáadva (füst, nincs-túlcsordulás, utazás, szomorú útvonal); `dotnet test` zöld.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` tiszta az érintett fájlokon.
