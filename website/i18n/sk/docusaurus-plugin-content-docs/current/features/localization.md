# Localization (i18n)

cMind je plne localizable a ships v **rovnakých 23 jazykoch, ktoré samotný cTrader podporuje**, takže obchodník
používa platformu — a číta tieto dokumenty — vo svojom vlastnom jazyku. Angličtina je fallback; akýkoľvek chýbajúci
translation degraduje pekne na angličtinu namiesto zobrazenia blanku alebo raw key.

## Supported languages

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

Jeden zdroj pravdy je `Core.Constants.SupportedCultures` — request-culture middleware, language
switcher, resource-parity test a no-hardcoded-string gate všetci čítajú z toho. Pridávanie jazyka je
one-line zmena tam plus jeho resource files.

## Ako to funguje (Blazor Server)

- **Resources.** UI strings žijú v `src/Web/Resources/Ui.resx` (English base) plus jeden
  `Ui.<culture>.resx` per language. Components čítajú cez `IStringLocalizer<Ui>` — `@L["key"]`,
  nikdy literal. `.resx` files sú generated z `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` picks culture z `.AspNetCore.Culture`
  cookie prvý, potom browser `Accept-Language`, potom English.
- **Switching.** App-bar language switcher (a **Settings → Language** section) naviguje na
  `GET /set-culture` endpoint — full-reload vonku Blazor circuit, pretože circuit nemôže
  zmeniť culture live. Piše cookie a, pre signed-in user, persist voľba na svoj
  profile (`UserProfile.Locale`); reload boots fresh circuit v chosen language.
- **Persistence & login.** Saved profile locale je written späť do culture cookie pri sign-in,
  takže user lands vo svojom jazyku na every device.
- **Right-to-left.** Arabic (a akýkoľvek budúci RTL language) sets `<html dir="rtl">` a wraps layout v
  MudBlazor `MudRTLProvider`, mirroring whole shell.
- **ICU.** Web host beží s ICU enabled (`InvariantGlobalization=false`); wire/parse code zostáva na
  `CultureInfo.InvariantCulture`, takže len per-culture UI formatting je affected — nikdy backtest alebo CSV.

## Gate — žádny hard-coded UI text

Nový user-facing strings **nemôžu** byť merged un-localized v covered scope:

- Build-failing arch-guard test (`NoHardcodedUiTextTests`) skenuje migrated `.razor` files a fails na
  akýkoľvek literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`), ktorý nie je `@L["…"]` lookup.
- Resource-parity test (`ResourceParityTests`) fails build ak akýkoľvek language schýba kľúč alebo ships
  blank value — každý language vždy má každý kľúč.

## Pridávanie alebo zmena string

1. Add/edit kľúč v `tools/i18n/ui-translations.json` pre **každú** kultúru.
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference to v component s `@L["your.key"]`.
4. `dotnet test` — parity a hardcoded-text gates vás drž čestný.

## Docs localization

Tieto dokumenty sú lokalizované aj. Docusaurus i18n je configured pre všetkých 23 locales (`website/i18n/`), s
locale dropdown v navbar a RTL pre Arabic. Scaffold locale translation files s
`npm run write-translations -- --locale <code>` a translate pod `website/i18n/<code>/`. Per
localization mandate, **pridávanie alebo zmena akéhokoľvek doc znamená updating každej locale v rovnakej zmene.**
