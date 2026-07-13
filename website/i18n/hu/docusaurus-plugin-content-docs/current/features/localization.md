# Lokalizáció (i18n)

A cMind teljes mértékben lokalizálható, és az **ugyanez a 23 nyelv szállítva, amit a cTrader saját támogat**, így egy kereskedő használja a platformot — és olvassa ezeket a dokumentumokat — saját nyelvén. Az angol a visszalépés; bármilyen hiányzó fordítás kecskesen csökken az angol-ra ahelyett, hogy egy üres vagy egy nyers kulcsot mutatna.

## Támogatott nyelvek

Arab (RTL), kínai (egyszerűsített), cseh, angol, francia, német, görög, magyar, indonéz, olasz, japán, koreai, maláj, lengyel, portugál (Brazília), orosz, szerb, szlovák, szlovén, spanyol, tájlandai, török, vietnami.

Az egy igazság forrása az `Core.Constants.SupportedCultures` — a kérelem-kultúra middleware, a nyelvváltó, a erőforrás-paritás teszt, és a nem-kódolt-karakterlánc kapu mind ebből olvasnak. Egy nyelv hozzáadása egy egy-sor változtatás ott plusz az erőforrás fájlok.

## Hogyan működik (Blazor Server)

- **Erőforrások.** Az UI karakterláncok az `src/Web/Resources/Ui.resx` (angol alap) plusz egy `Ui.<culture>.resx` nyelvként élnek. Az összetevők az `IStringLocalizer<Ui>` — `@L["key"]` segítségével olvassák őket, soha egy szó. A `.resx` fájlok az `tools/i18n/ui-translations.json`-ből generálódnak (`pwsh tools/i18n/gen-resx.ps1`), a fordító-baráti igazság forrása.
- **Kultúra feloldás.** Az `RequestLocalizationMiddleware` feloldja a kultúrát az `.AspNetCore.Culture` cookiéből először, majd a böngésző `Accept-Language`-jéből, majd angol.
- **Váltás.** Az alkalmazás-sáv nyelvváltó (és az **Beállítások → Nyelv** szakasz) navigál a `GET /set-culture` végpontra — teljes újratöltés a Blazor áramkörön kívül, mivel az áramkör nem tudja megváltoztatni az élő kultúrát. Írja a sülit és egy bejelentkezet felhasználó számára megmarad az választás az profiljába (`UserProfile.Locale`); az újratöltés felindít egy friss áramköri a választott nyelvben.
- **Kitartás és bejelentkezés.** A mentett profil területi beállítása írva vissza a kultúra sütibe a bejelentkezés alatt, így egy felhasználó landol az nyelvén minden eszköz.
- **Jobbról balra.** Arab (és bármilyen jövőbeli RTL nyelv) beállít `<html dir="rtl">` és összecsukódva a kapcsolat MudBlazor `MudRTLProvider`-ben, tükrözve az egész héj.
- **ICU.** A Web gazdagép az ICU engedélyezve futtatva (`InvariantGlobalization=false`); drót/elemzés kód marad `CultureInfo.InvariantCulture` alatt, így csak az per-kultúra UI formázás érintett — soha egy backtest vagy CSV.

## A kapu — nincs kódolt UI szöveg

Az új felhasználó-felé nézett karakterláncok **nem** lehet összefésült nem-lokalizálva az fedezett hatókörben:

- Egy build-hibás arch-őr teszt (`NoHardcodedUiTextTests`) pásztáz áttelepített `.razor` fájlokat és nem teljesít nyers, szöveg-hordozó attribútumok (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) amely nincs egy `@L["…"]` felhallgatás.
- Egy erőforrás-paritás teszt (`ResourceParityTests`) nem teljesít az build ha bármilyen nyelv hiányzik egy kulcs vagy szállít egy üres érték — minden nyelv mindig az összes kulcs.

## Karakterlánc hozzáadása vagy módosítása

1. Hozzáadás/szerkesztés a kulcs `tools/i18n/ui-translations.json`-ben az **mindegyik** kultúra számára.
2. Újrageneráld az `.resx`-et: `pwsh tools/i18n/gen-resx.ps1`.
3. Hivatkozza meg az összetevőben `@L["your.key"]`-vel.
4. `dotnet test` — a paritás és kódolt-szöveg kapu őrségek tisztességes.

## Dokumentumok lokalizáció

Ezek a dokumentumok is lokalizáltak. Az Docusaurus i18n konfigurálva az összes 23 helyre (`website/i18n/`), a helyi legördülő a navbar-ban és RTL arab számára. Az egy helyre fordítás fájljaival állványzzon `npm run write-translations -- --locale <code>` és fordítson alatt `website/i18n/<code>/`. Az lokalizáció meghatalmazás szerint, **bármilyen dokumentum hozzáadása vagy módosítása azt jelenti minden helyre frissítés az ugyanazon az változás alatt.**
