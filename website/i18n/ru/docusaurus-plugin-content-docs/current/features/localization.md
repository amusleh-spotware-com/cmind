# Локализация (i18n)

cMind полностью локализируемо и поставляется в **том же 23 языках cTrader себя поддерживает**, поэтому trader использует платформу — и читает эти docs — на своем собственном языке. English является fallback; любой отсутствующий перевод gracefully деградирует в English вместо показа blank или raw key.

## Поддерживаемые языки

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian, Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian, Spanish, Thai, Turkish, Vietnamese.

Единственный источник истины это `Core.Constants.SupportedCultures` — request-culture middleware, language switcher, resource-parity тест и no-hardcoded-string gate все читают из этого. Добавление языка это one-line изменение там плюс его resource файлы.

## Как это работает (Blazor Server)

- **Resources.** UI strings живут в `src/Web/Resources/Ui.resx` (English base) плюс один `Ui.<culture>.resx` per язык. Components читают их через `IStringLocalizer<Ui>` — `@L["key"]`, никогда literal. `.resx` файлы генерируются из `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`), translator-friendly источник истины.
- **Culture разрешение.** `RequestLocalizationMiddleware` выбирает culture из `.AspNetCore.Culture` cookie первый, затем browser's `Accept-Language`, затем English.
- **Переключение.** App-bar language switcher (и **Settings → Language** секция) navigates к `GET /set-culture` endpoint — full-reload вне Blazor circuit, потому что circuit не может менять culture live. Это пишет cookie и, для signed-in пользователя, persists выбор их profile (`UserProfile.Locale`); reload boots свежий circuit в выбранном языке.
- **Persistence & login.** Сохраненный profile locale написано назад в culture cookie на sign-in, поэтому пользователь lands в его языке на каждом устройстве.
- **Right-to-left.** Arabic (и любой будущий RTL язык) устанавливает `<html dir="rtl">` и оборачивает layout в MudBlazor's `MudRTLProvider`, mirroring весь shell.
- **ICU.** Web host работает с ICU enabled (`InvariantGlobalization=false`); wire/parse код остается на `CultureInfo.InvariantCulture`, поэтому только per-culture UI форматирование затронуто — никогда backtest или CSV.

## Gate — нет hard-coded UI текста

Новые user-facing строки **не могут** быть merged un-localized в covered scope:

- Build-failing arch-guard тест (`NoHardcodedUiTextTests`) сканирует migrated `.razor` файлы и fails на любом literal, text-bearing атрибуте (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) что не является `@L["…"]` lookup.
- Resource-parity тест (`ResourceParityTests`) fails build если любой язык отсутствует key или ships blank значение — каждый язык всегда имеет каждый key.

## Добавление или изменение строки

1. Add/edit key в `tools/i18n/ui-translations.json` для **каждого** culture.
2. Переразработать `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Reference это в component с `@L["your.key"]`.
4. `dotnet test` — parity и hardcoded-text gates сохраняют вас честным.

## Docs локализация

Эти docs локализированы тоже. Docusaurus i18n конфигурирован для все 23 locales (`website/i18n/`), с locale dropdown в navbar и RTL для Arabic. Scaffold locale's translation файлы с `npm run write-translations -- --locale <code>` и переводить под `website/i18n/<code>/`. Per локализация mandate, **добавление или изменение любого doc означает обновление каждого locale в том же изменении.**
