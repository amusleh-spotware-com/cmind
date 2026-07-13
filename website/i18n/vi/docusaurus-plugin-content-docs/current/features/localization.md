# Localization (i18n)

cMind hoàn toàn có thể localized và ship trong **cùng 23 languages cTrader itself supports**, vì vậy một trader
uses platform — và reads these docs — in their own language. English là fallback; any missing
translation degrades gracefully to English hơn là showing blank hoặc raw key.

## Supported languages

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

Single source of truth là `Core.Constants.SupportedCultures` — request-culture middleware, language
switcher, resource-parity test, và no-hardcoded-string gate all read from it. Adding a
language là một one-line change there plus its resource files.

## How it works (Blazor Server)

- **Resources.** UI strings live in `src/Web/Resources/Ui.resx` (English base) plus one
  `Ui.<culture>.resx` per language. Components read chúng through `IStringLocalizer<Ui>` — `@L["key"]`,
  never a literal. `.resx` files generated from `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` picks culture từ `.AspNetCore.Culture`
  cookie first, rồi browser's `Accept-Language`, rồi English.
- **Switching.** App-bar language switcher (và **Settings → Language** section) navigates to
  `GET /set-culture` endpoint — full-reload outside Blazor circuit, because a circuit cannot
  change culture live. Nó writes cookie và, for a signed-in user, persists choice to their
  profile (`UserProfile.Locale`); reload boots fresh circuit in chosen language.
- **Persistence & login.** Saved profile locale được write back vào culture cookie at sign-in,
  vì vậy user lands in their language on every device.
- **Right-to-left.** Arabic (và any future RTL language) sets `<html dir="rtl">` và wraps layout in
  MudBlazor's `MudRTLProvider`, mirroring whole shell.
- **ICU.** Web host runs với ICU enabled (`InvariantGlobalization=false`); wire/parse code stays on
  `CultureInfo.InvariantCulture`, vì vậy chỉ per-culture UI formatting affected — không bao giờ a backtest or CSV.

## The gate — no hard-coded UI text

New user-facing strings **cannot** be merged un-localized in covered scope:

- A build-failing arch-guard test (`NoHardcodedUiTextTests`) scans migrated `.razor` files và fails on
  any literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) that isn't an `@L["…"]` lookup.
- A resource-parity test (`ResourceParityTests`) fails build if any language missing a key or ships
  blank value — every language luôn có every key.

## Adding or changing a string

1. Add/edit key in `tools/i18n/ui-translations.json` for **every** culture.
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference nó in component với `@L["your.key"]`.
4. `dotnet test` — parity và hardcoded-text gates keep you honest.

## Docs localization

These docs localized too. Docusaurus i18n configured for all 23 locales (`website/i18n/`), với
locale dropdown in navbar và RTL for Arabic. Scaffold a locale's translation files with
`npm run write-translations -- --locale <code>` và translate under `website/i18n/<code>/`. Per
localization mandate, **adding or changing any doc means updating every locale in same change.**
