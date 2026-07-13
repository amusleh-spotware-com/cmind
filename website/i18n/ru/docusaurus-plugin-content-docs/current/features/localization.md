---
description: "cMind полностью локализуем и поставляется на тех же 23 языках что и cTrader. English как fallback; любой отсутствующий перевод graceful деградирует к English."
---

# Localization (i18n)

cMind полностью локализуем и поставляется на **тех же 23 языках, которые поддерживает cTrader**, поэтому трейдер
использует платформу — и читает эти docs — на своём языке. English как fallback; любая отсутствующая
трансляция graceful деградирует к English, а не показывает пустую строку или raw key.

## Supported languages

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

Единый источник истины — `Core.Constants.SupportedCultures` — request-culture middleware, language
switcher, resource-parity test и no-hardcoded-string gate все читают из него. Добавление языка —
однострочное изменение там плюс его файлы ресурсов.

## Как это работает (Blazor Server)

- **Resources.** UI-строки живут в `src/Web/Resources/Ui.resx` (English base) плюс один
  `Ui.<culture>.resx` per язык. Компоненты читают их через `IStringLocalizer<Ui>` — `@L["key"]`,
  никогда literal. `.resx` файлы генерируются из `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly источник истины.
- **Culture resolution.** `RequestLocalizationMiddleware` picks the culture from `.AspNetCore.Culture`
  cookie first, then browser's `Accept-Language`, then English.
- **Switching.** App-bar language switcher (и **Settings → Language** section) навигирует к
  `GET /set-culture` endpoint — full-reload outside Blazor circuit, потому что circuit cannot
  change culture live. Он пишет cookie и, для signed-in user, persists выбор в их
  profile (`UserProfile.Locale`); reload загружает fresh circuit на выбранном языке.
- **Persistence & login.** Сохранённый profile locale пишется обратно в culture cookie at sign-in,
  so user lands на своём языке на каждом устройстве.
- **Right-to-left.** Arabic (и any future RTL language) sets `<html dir="rtl">` и wraps layout in
  MudBlazor's `MudRTLProvider`, mirroring whole shell.
- **ICU.** Web host runs with ICU enabled (`InvariantGlobalization=false`); wire/parse code stays on
  `CultureInfo.InvariantCulture`, so only per-culture UI formatting affected — never a backtest или CSV.

## Gate — без hard-coded UI текста

Новые user-facing строки **не могут** быть смержены не-локализованными:

- A build-failing arch-guard test (`NoHardcodedUiTextTests`) сканирует migrated `.razor` файлы и fails on
  any literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) that isn't an `@L["…"]` lookup.
- A resource-parity test (`ResourceParityTests`) fails the build if any language missing a key или ships
  blank value — every language всегда имеет every key.

## Adding или changing a string

1. Add/edit the key in `tools/i18n/ui-translations.json` for **every** culture.
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference it in the component with `@L["your.key"]`.
4. `dotnet test` — parity и hardcoded-text gates keep you honest.

## Docs localization

Эти docs тоже локализованы. Docusaurus i18n configured для всех 23 locales (`website/i18n/`), с
locale dropdown in navbar и RTL для Arabic. Scaffold a locale's translation files with
`npm run write-translations -- --locale <code>` и translate under `website/i18n/<code>/`. Per the
localization mandate, **adding или changing any doc means updating every locale in the same change**.
