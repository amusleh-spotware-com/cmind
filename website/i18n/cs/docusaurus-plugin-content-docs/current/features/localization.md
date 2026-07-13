# Lokalizace (i18n)

cMind je plně lokalizovatelný a dodává se ve **stejných 23 jazycích které podporuje samotný cTrader**, takže trader
používá platformu — a čte tyto docs — ve svém vlastním jazyce. Angličtina je fallback; jakákoliv chybějící
translace degradovaně přechází do angličtiny spíše než by ukázala prázdný nebo raw klíč.

## Podporované jazyky

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

Jediný zdroj pravdy je `Core.Constants.SupportedCultures` — request-culture middleware,
language switcher, resource-parity test, and the no-hardcoded-string gate all read from it. Přidání a
jazyka je one-line change tam plus its resource files.

## Jak to funguje (Blazor Server)

- **Resources.** UI strings žijí v `src/Web/Resources/Ui.resx` (English base) plus one
  `Ui.<culture>.resx` per jazyk. Komponenty je čtou přes `IStringLocalizer<Ui>` — `@L["key"]`,
  nikdy literál. `.resx` soubory jsou generovány z `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` vybírá culture z `.AspNetCore.Culture`
  cookie first, then browser's `Accept-Language`, then English.
- **Switching.** App-bar language switcher (a **Settings → Language** section) naviguje na
  `GET /set-culture` endpoint — a full-reload outside Blazor circuit, because a circuit cannot
  change culture live. Zapisuje cookie and, for a signed-in user, persists choice to their
  profile (`UserProfile.Locale`); reload boots a fresh circuit in chosen language.
- **Persistence & login.** Saved profile locale is written back into culture cookie at sign-in,
  so a user lands in their language on every device.
- **Right-to-left.** Arabic (and any future RTL language) sets `<html dir="rtl">` and wraps the layout in
  MudBlazor's `MudRTLProvider`, mirroring the whole shell.
- **ICU.** Web host běží with ICU enabled (`InvariantGlobalization=false`); wire/parse code stays on
  `CultureInfo.InvariantCulture`, takže only per-culture UI formatting is affected — nikdy backtest nebo CSV.

## Brána — žádný hard-coded UI text

Nové user-facing strings **nemohou** být mergeovány un-localized in the covered scope:

- A build-failing arch-guard test (`NoHardcodedUiTextTests`) scans migrated `.razor` files and fails on
  any literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) that isn't an `@L["…"]` lookup.
- A resource-parity test (`ResourceParityTests`) fails the build if any language is missing a key or ships
  a blank value — every language always has every key.

## Přidání nebo změna stringu

1. Přidejte/upravte klíč v `tools/i18n/ui-translations.json` pro **každý** kultur.
2. Regenerujte `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference it in component with `@L["your.key"]`.
4. `dotnet test` — parity and hardcoded-text gates keep you honest.

## Docs lokalizace

Tyto docs jsou také lokalizovány. Docusaurus i18n je nakonfigurován pro všech 23 locale (`website/i18n/`), s a
locale dropdown in navbar and RTL for Arabic. Scaffold a locale's translation files with
`npm run write-translations -- --locale <code>` and translate under `website/i18n/<code>/`. Per the
localization mandate, **adding or changing any doc znamená aktualizovat každý locale ve stejné změně.**
