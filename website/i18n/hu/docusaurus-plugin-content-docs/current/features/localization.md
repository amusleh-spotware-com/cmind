# Localization (i18n)

cMind is fully localizable and ships in the **same 23 languages cTrader itself supports**, so a trader
uses the platform — and reads these docs — in their own language. English is the fallback; any missing
translation degrades gracefully to English rather than showing a blank or a raw key.

## Supported languages

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

The one source of truth is `Core.Constants.SupportedCultures` — the request-culture middleware, the
language switcher, the resource-parity test, and the no-hardcoded-string gate all read from it. Adding a
language is a one-line change there plus its resource files.

## How it works (Blazor Server)

- **Resources.** UI strings live in `src/Web/Resources/Ui.resx` (English base) plus one
  `Ui.<culture>.resx` per language. Components read them through `IStringLocalizer<Ui>` — `@L["key"]`,
  never a literal. The `.resx` files are generated from `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), the translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` picks the culture from the `.AspNetCore.Culture`
  cookie first, then the browser's `Accept-Language`, then English.
- **Switching.** The app-bar language switcher (and the **Settings → Language** section) navigates to
  the `GET /set-culture` endpoint — a full-reload outside the Blazor circuit, because a circuit cannot
  change culture live. It writes the cookie and, for a signed-in user, persists the choice to their
  profile (`UserProfile.Locale`); the reload boots a fresh circuit in the chosen language.
- **Persistence & login.** The saved profile locale is written back into the culture cookie at sign-in,
  so a user lands in their language on every device.
- **Right-to-left.** Arabic (and any future RTL language) sets `<html dir="rtl">` and wraps the layout in
  MudBlazor's `MudRTLProvider`, mirroring the whole shell.
- **ICU.** The Web host runs with ICU enabled (`InvariantGlobalization=false`); wire/parse code stays on
  `CultureInfo.InvariantCulture`, so only per-culture UI formatting is affected — never a backtest or CSV.

## The gate — no hard-coded UI text

New user-facing strings **cannot** be merged un-localized in the covered scope:

- A build-failing arch-guard test (`NoHardcodedUiTextTests`) scans migrated `.razor` files and fails on
  any literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) that isn't an `@L["…"]` lookup.
- A resource-parity test (`ResourceParityTests`) fails the build if any language is missing a key or ships
  a blank value — every language always has every key.

## Adding or changing a string

1. Add/edit the key in `tools/i18n/ui-translations.json` for **every** culture.
2. Regenerate the `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference it in the component with `@L["your.key"]`.
4. `dotnet test` — the parity and hardcoded-text gates keep you honest.

## Docs localization

These docs are localized too. Docusaurus i18n is configured for all 23 locales (`website/i18n/`), with a
locale dropdown in the navbar and RTL for Arabic. Scaffold a locale's translation files with
`npm run write-translations -- --locale <code>` and translate under `website/i18n/<code>/`. Per the
localization mandate, **adding or changing any doc means updating every locale in the same change.**
