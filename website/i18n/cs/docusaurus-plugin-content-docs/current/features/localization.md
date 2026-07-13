# Lokalizace (i18n)

cMind je plně lokalizovatelný a dodává se ve **stejných 23 jazycích, které cTrader sám podporuje**, aby obchodník používal platformu — a četl tyto dokumenty — ve svém vlastním jazyce. Angličtina je fallback; jakýkoli chybějící překlad se elegantně degraduje na angličtinu spíše než zobrazuje prázdné nebo surové klíče.

## Podporované jazyky

Arabština (RTL), Čínština (zjednodušená), Čeština, Angličtina, Francouzština, Němčina, Řečtina, Maďarština, Indonéština, Italština, Japonština, Korejština, Malajština, Polština, Portugalština (Brazílie), Ruština, Srbština, Slovenština, Slovenština, Španělština, Thajština, Turečtina, Vietnamština.

Jediným zdrojem pravdy je `Core.Constants.SupportedCultures` — middleware žádosti kultury, přepínač jazyku, test parity zdrojů a brána bez pevně zakódovaných řetězců si z něj čtou. Přidání jazyka je jednořádkovou změnou tam plus jeho soubory zdrojů.

## Jak to funguje (Blazor Server)

- **Zdroje.** Řetězce uživatelského rozhraní existují v `src/Web/Resources/Ui.resx` (základní angličtina) plus jeden `Ui.<culture>.resx` na jazyk. Komponenty je čtou přes `IStringLocalizer<Ui>` — `@L["key"]`, nikdy doslova. Soubory `.resx` se generují z `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`), přátelský zdroj pravdy překladatele.
- **Rozlišení kultury.** `RequestLocalizationMiddleware` nejprve vybere kulturu z souboru cookie `.AspNetCore.Culture`, poté z `Accept-Language` prohlížeče, poté z angličtiny.
- **Přepínání.** Přepínač jazyku na pruhu aplikace (a část **Nastavení → Jazyk**) přejde na koncový bod `GET /set-culture` — úplné znovunačtení mimo obvod Blazor, protože obvod nemůže live změnit kulturu. Zapíše soubor cookie a pro přihlášeného uživatele připraví volbu do jeho profilu (`UserProfile.Locale`); znovunačtení zavádí nový obvod ve zvoleném jazyce.
- **Perzistence & přihlášení.** Uložená lokalizace profilu se při přihlášení zapíše zpět do souboru cookie kultury, takže se uživatel dostane do svého jazyka na každém zařízení.
- **Zprava doleva.** Arabština (a všechny budoucí jazyky RTL) nastavuje `<html dir="rtl">` a zabaluje rozvržení do MudBlazorovny `MudRTLProvider`, zrcadlící celé prostředí.
- **ICU.** Web host běží s ICU povoleným (`InvariantGlobalization=false`); kód v drátě/parseuje zůstane na `CultureInfo.InvariantCulture`, takže je ovlivněno pouze formátování uživatelského rozhraní na kulturu — nikdy backtest nebo CSV.

## Brána — bez pevně zakódovaného textu uživatelského rozhraní

Nové řetězce viditelné uživatelem **nemohou** být sloučeny nelokalizované v krytém rozsahu:

- Test arch-guard selhávající při sestavě (`NoHardcodedUiTextTests`) prohledá migrované soubory `.razor` a selže na jakémkoli literálu, atributu s textem (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`), který není `@L["…"]` vyhledáním.
- Test parity zdrojů (`ResourceParityTests`) selhává v případě, že jakýkoli jazyk chybí klíč nebo dodá prázdnou hodnotu — každý jazyk vždy má každý klíč.

## Přidání nebo změna řetězce

1. Přidejte/upravte klíč v `tools/i18n/ui-translations.json` pro **každou** kulturu.
2. Regenerujte `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Odkažte na něj v součásti s `@L["your.key"]`.
4. `dotnet test` — brány parity a pevně zakódovaného textu vás drží upřímní.

## Lokalizace dokumentů

Tyto dokumenty jsou také lokalizovány. Docusaurus i18n je konfigurován pro všechny 23 lokality (`website/i18n/`), s rozevírací nabídkou lokality v navigačním panelu a RTL pro arabštinu. Vygenerujte soubory překladu lokality pomocí `npm run write-translations -- --locale <code>` a překládejte pod `website/i18n/<code>/`. Dle mandátu lokalizace **přidání nebo změna jakéhokoli dokumentu znamená aktualizaci každé lokality ve stejné změně.**
