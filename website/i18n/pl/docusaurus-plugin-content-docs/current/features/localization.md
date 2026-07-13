# Localization (i18n)

cMind to fully localizable i ships w **same 23 languages które cTrader itself supports**, więc trader
uses platform — i reads te docs — w ich own language. English to fallback; każdy missing
translation degrades gracefully do English rather niż showing blank lub raw key.

## Wspierane języki

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

One source of truth to `Core.Constants.SupportedCultures` — request-culture middleware, language
switcher, resource-parity test, i no-hardcoded-string gate wszystkie read z to. Adding language
to one-line change tam plus jego resource files.

## Jak to działa (Blazor Server)

- **Resources.** UI strings żyją w `src/Web/Resources/Ui.resx` (English base) plus jeden
  `Ui.<culture>.resx` per language. Components read je przez `IStringLocalizer<Ui>` — `@L["key"]`,
  nigdy literal. `.resx` files są generated z `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` picks culture z `.AspNetCore.Culture`
  cookie first, wtedy browser's `Accept-Language`, wtedy English.
- **Switching.** App-bar language switcher (i **Settings → Language** section) navigates do
  `GET /set-culture` endpoint — full-reload poza Blazor circuit, bo circuit nie może
  change culture live. To writes cookie i, dla signed-in user, persists choice do ich
  profile (`UserProfile.Locale`); reload boots fresh circuit w chosen language.
- **Persistence & login.** Saved profile locale to written back do culture cookie na sign-in,
  więc user lands w ich language na każdy device.
- **Right-to-left.** Arabic (i każdy future RTL language) sets `<html dir="rtl">` i wraps layout w
  MudBlazor's `MudRTLProvider`, mirroring whole shell.
- **ICU.** Web host runs z ICU enabled (`InvariantGlobalization=false`); wire/parse code stays na
  `CultureInfo.InvariantCulture`, więc tylko per-culture UI formatting to affected — nigdy backtest lub CSV.

## Gate — no hard-coded UI text

New user-facing strings **nie mogą** be merged un-localized w covered scope:

- Build-failing arch-guard test (`NoHardcodedUiTextTests`) scans migrated `.razor` files i fails na
  każdy literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) które nie to `@L["…"]` lookup.
- Resource-parity test (`ResourceParityTests`) fails build jeśli każdy language missing key lub ships
  blank value — każdy language zawsze has każdy key.

## Dodawanie lub zmiana string

1. Add/edit key w `tools/i18n/ui-translations.json` dla **każdy** culture.
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference to w component z `@L["your.key"]`.
4. `dotnet test` — parity i hardcoded-text gates keep Cię honest.

## Docs localization

Te docs są localized too. Docusaurus i18n to configured dla wszystkie 23 locales (`website/i18n/`), z
locale dropdown w navbar i RTL dla Arabic. Scaffold locale's translation files z
`npm run write-translations -- --locale <code>` i translate pod `website/i18n/<code>/`. Per localization
mandate, **adding lub changing każdy doc znaczy updating każdy locale w same change.**
