---
title: Lokalizacio (i18n)
description: A cMind teljesen lokalizalhato es 23 nyelven szallithato, ugyanazokon, amelyeiket a cTrader maga tamogatja. Az angol fallback; barmely hianyzo forditas kegyesen degradal angolra a blank vagy nyers kulcs megjelenitese helyett.
---

# Lokalizacio (i18n)

A cMind teljesen lokalizalhato es **ugyanazokon a 23 nyelven szallithato, amelyeiket a cTrader maga tamogatja**, igy egy kereskedő a platformot - es olvassa ezeket a dokumentumokat - a sajat nyelven. Az angol fallback; barmely hianyzo forditas kegyesen degradal angolra a blank vagy nyers kulcs megjelenitese helyett.

## Tamogatott nyelvek

Arab (RTL), Kinai (Egyszerusitett), Cseh, Angol, Francia, Nemet, Görög, Magyar, Indonéz, Olasz, Japan, Koreai, Malajziai, Lengyel, Portugl (Brazil), Orosz, Szerb, Szlovak, Szlovén, Spanyol, Thai, Török, Vietnámi.

Az egyetlen forrasa az igazsagnak a `Core.Constants.SupportedCultures` - a request-culture middleware, a nyelves kapcsolo, a resource-parity teszt, es a no-hardcoded-string gate mind ebből olvasnak. Egy nyelv hozzaadasa egy egysoros valtoztatas itt plusz az erőforras fajljai.

## Hogyan mukodik (Blazor Server)

- **Eroforrasok.** UI szovegek a `src/Web/Resources/Ui.resx`-ben (angol bázis) plusz egy `Ui.<culture>.resx` per nyelv. A komponensek a `IStringLocalizer<Ui>`-n at olvassak - `@L["key"]`, soha nem literal. A `.resx` fajlok a `tools/i18n/ui-translations.json`-bol vannak generalva (`pwsh tools/i18n/gen-resx.ps1`), a fordito-barát forras-igazsag.
- **Kultura feloldas.** `RequestLocalizationMiddleware` valasztja a kulturat a `.AspNetCore.Culture` cookie-bol eloszor, aztán a böngésző `Accept-Language`-ebol, aztán az angolbol.
- **Valtas.** Az alkalmazassav nyelves kapcsoloja (es a **Beallitasok → Nyelv** szekcio) a `GET /set-culture` vegpontra navigal - egy teljes újratoltes a Blazor circuit-en kivul, mivel egy circuit nem tud kulturat valtani élőben. Iroi a cookie-t es egy bejelentkezett felhasznalonal perzisztalja a valasztast a profiljara (`UserProfile.Locale`); az újratoltes egy friss circuit-et bootol a valasztott nyelvvel.
- **Perzisztencia es bejelentkezes.** A mentett profil locale vissza van irva a culture cookie-ba bejelentkezeskor, igy egy felhasznalo a sajat nyelven landol minden eszkozon.
- **Jobbrol-balra.** Az Arab (es barmely jövőbeli RTL nyelv) beallitja a `<html dir="rtl">`-t es becsomagolja az elrendezest MudBlazor `MudRTLProvider`-rel, tukrozve a teljes shell-t.
- **ICU.** A Web host ICU-val fut (`InvariantGlobalization=false`); wire/parse kod a `CultureInfo.InvariantCulture`-en marad, igy csak a per-kultura UI formazas erintett - soha egy backtest vagy CSV.

## A kapu - nincs kemenykodolt UI szoveg

Uj felhasznaloval szembeni szovegek **nem** egyesithetok lokalizálatlanul a lefedett scope-ban:

- Egy build-meghiuso arch-guard teszt (`NoHardcodedUiTextTests`) scanneli a migrált `.razor` fajlok es meghiusul minden literál, szoveget-behelo attributumon (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`), ami nem `@L["..."]` lookup.
- Egy resource-parity teszt (`ResourceParityTests`) meghiusitja a build-et, ha barmely nyelv hianyzik egy kulcsot vagy ures erteket szallit - minden nyelv mindig minden kulcsot birtokol.

## Egy szoveg hozzaadasa vagy valtoztatasa

1. Add/szerkesztd a kulcsot a `tools/i18n/ui-translations.json`-ban **minden** kultúrához.
2. Generald ujra a `.resx`-et: `pwsh tools/i18n/gen-resx.ps1`.
3. Hivatkozd a komponensben `@L["your.key"]`-kent.
4. `dotnet test` - a parity es a hardcoded-text gate-k tartanak.

## Dok lokalizacio

Ezek a dokumentumok szinten lokalizálva vannak. A Docusaurus i18n az osszes 23 locale-re konfiguralva (`website/i18n/`), a navbarban egy locale dropdown es RTL az Arab nyelvhez. Allitsd le egy locale forditasi fajljait az `npm run write-translations -- --locale <code>`-vel es forditsd a `website/i18n/<code>/`-t. A lokalizacios mandatum szerint **barmely doc hozzaadasa vagy valtoztatasa minden lokalitast jelent a valtoztatasban**.
