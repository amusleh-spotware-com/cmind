---
title: Lokalizacija (i18n)
description: "cMind je v celoti lokaliziran in je na voljo v istih 23 jezikih, ki jih podpira cTrader — trgovalec tako uporablja platformo in bere dokumentacijo v svojem lastnem jeziku."
---

# Lokalizacija (i18n)

cMind je v celoti lokaliziran in je na voljo v **istih 23 jezikih, ki jih podpira cTrader** — trgovalec tako uporablja platformo in bere to dokumentacijo v svojem lastnem jeziku. Angleščina je nadomestni jezik; katera koli manjkajoča prevod degradira v angleščino, namesto da bi prikazala prazno ali surove ključe.

## Podprti jeziki

arabščina (RTL), kitajščina (poenostavljena), češčina, angleščina, francoščina, nemščina, grščina, madžarščina, indonezijščina, italijanščina, japonščina, korejščina, malajščina, poljščina, portugalščina (Brazilija), ruščina, srbščina, slovaščina, **slovenščina**, španščina, tajščina, turščina, vietnamščina.

En sam vir resnice je `Core.Constants.SupportedCultures` — vmesna programska oprema za kulturo zahteve, preklopnik jezika, test parity virov in vrata brez trdo kodiranega besedila vse berejo iz njega. Dodajanje jezika je sprememba enega dela tukaj, poleg njegovih datotek z viri.

## Kako deluje (Blazor Server)

- **Viri.** Besedila uporabniškega vmesnika so v `src/Web/Resources/Ui.resx` (angleška osnova) plus ena
  `Ui.<culture>.resx` na jezik. Komponente jih berejo prek `IStringLocalizer<Ui>` — `@L["key"]`,
  nikoli dobesedno. Datoteke `.resx` se ustvarijo iz `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), prijazen vir za prevajalce.
- **Razreševanje kulture.** `RequestLocalizationMiddleware` izbere kulturo najprej iz piškotka `.AspNetCore.Culture`,
  nato iz brskalnikove `Accept-Language`, nato iz angleščine.
- **Preklapljanje.** Preklopnik jezika v app baru (in odsek **Settings → Language**) navigira na
  `GET /set-culture` — polno osvežitev zunaj Blazor vezja, ker vezje ne more spremeniti kulture v živo. Zapiše piškotek in za prijavljenega uporabnika vztraja izbiro v njihovem profilu (`UserProfile.Locale`); osvežitev zažene novo vezje v izbranem jeziku.
- **Vztrajnost in prijava.** Shranjena lokacija profila se ob prijavi zapiše nazaj v piškotek kulture,
  tako da uporabnik pristane v svojem jeziku na vsaki napravi.
- **Od desne proti levi.** Arabščina (in kateri koli prihodnji jezik RTL) nastavi `<html dir="rtl">` in ovije postavitev v
  `MudRTLProvider` od MudBlazor, zrcali celotno lupino.
- **ICU.** Spletni gostitelj teče z omogočenim ICU (`InvariantGlobalization=false`); koda za žice/razčlenjevanje ostane na
  `CultureInfo.InvariantCulture`, torej je samo oblikovanje UI na kulturo prizadeto — nikoli backtest ali CSV.

## Vrata — brez trdo kodiranega besedila UI

Nova besedila za uporabnike **ne morejo** biti združena nelokalizirana v zajetem obsegu:

- Test arhitekturnih vrat, ki propade med gradnjo (`NoHardcodedUiTextTests`), pregleduje prestavljene `.razor` datoteke in propade na
  kateri koli dobesedni, besedilni atribut (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`), ki ni `@L["…"]` iskanje.
- Test parity virov (`ResourceParityTests`) propade gradnjo, če kateri koli jezik manjka ključ ali pošilja
  prazno vrednost — vsak jezik ima vedno vsak ključ.

## Dodajanje ali spreminjanje niza

1. Dodaj/uredi ključ v `tools/i18n/ui-translations.json` za **vsako** kulturo.
2. Regeneriraj `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Sklicuj se nanj v komponenti z `@L["your.key"]`.
4. `dotnet test` — vrata parity in trdo kodiranega besedila te držijo poštenega.

## Lokalizacija dokumentacije

Ti dokumenti so prav tako lokalizirani. Docusaurus i18n je konfiguriran za vseh 23 lokov (`website/i18n/`), z
spustnim seznamom lokacij v navigacijski vrstici in RTL za arabščino. Ogrodje prevajalnih datotek lokacije z
`npm run write-translations -- --locale <code>` in prevajaj pod `website/i18n/<code>/`. V skladu z
mandatom lokalizacije **dodajanje ali spreminjanje katere koli dokumentacije pomeni posodobitev vsake lokacije v isti spremembi.**
