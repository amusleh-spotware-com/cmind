# Localization (i18n)

cMind สามารถ localizable ได้อย่างเต็มที่และ ships ใน **same 23 languages cTrader itself supports** ดังนั้น trader ใช้ platform — และ reads docs เหล่านี้ — ใน language ของตนเอง English คือ fallback; ใด ๆ missing translation degrades gracefully ไป English แทนที่จะแสดง blank หรือ raw key

## Supported languages

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian, Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian, Spanish, Thai, Turkish, Vietnamese

source of truth เดียว `Core.Constants.SupportedCultures` — request-culture middleware language switcher resource-parity test และ no-hardcoded-string gate ทั้งหมด read จาก มัน Adding language คือ one-line change there บวก resource files ของมัน

## How it works (Blazor Server)

- **Resources.** UI strings live ใน `src/Web/Resources/Ui.resx` (English base) บวก `Ui.<culture>.resx` per language Components read พวกมัน ผ่าน `IStringLocalizer<Ui>` — `@L["key"]` ไม่เคย literal `.resx` files generated จาก `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`) translator-friendly source of truth
- **Culture resolution.** `RequestLocalizationMiddleware` picks culture จาก `.AspNetCore.Culture` cookie ก่อน จากนั้น browser ของ `Accept-Language` จากนั้น English
- **Switching.** app-bar language switcher (และ **Settings → Language** section) navigates ไป `GET /set-culture` endpoint — full-reload ออก Blazor circuit เพราะ circuit ไม่สามารถ change culture live มันเขียน cookie และ สำหรับ signed-in user persists choice ไปยัง profile (`UserProfile.Locale`); reload boots fresh circuit ใน chosen language
- **Persistence & login.** saved profile locale ถูกเขียน กลับเข้าไป culture cookie ที่ sign-in ดังนั้น user lands ใน language ของพวกเขา ทุก device
- **Right-to-left.** Arabic (และ ใด ๆ future RTL language) sets `<html dir="rtl">` และ wraps layout ใน MudBlazor ของ `MudRTLProvider` mirroring whole shell
- **ICU.** Web host รัน ด้วย ICU enabled (`InvariantGlobalization=false`); wire/parse code stays บน `CultureInfo.InvariantCulture` ดังนั้น เพียง per-culture UI formatting affected — ไม่เคย backtest หรือ CSV

## gate — ไม่มี hard-coded UI text

New user-facing strings **ไม่สามารถ** merged un-localized ใน covered scope:

- build-failing arch-guard test (`NoHardcodedUiTextTests`) scans migrated `.razor` files และ fails บน ใด ๆ literal text-bearing attribute (`Label` `Text` `Title` `Placeholder` `HelperText` `aria-label` `alt`) ที่ไม่ใช่ `@L["…"]` lookup
- resource-parity test (`ResourceParityTests`) fails build ถ้า ใด ๆ language หายไป key หรือ ships blank value — ทุก language เสมอ มี ทุก key

## Adding หรือ changing string

1. Add/edit key ใน `tools/i18n/ui-translations.json` สำหรับ **ทุก** culture
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`
3. Reference มัน ใน component ด้วย `@L["your.key"]`
4. `dotnet test` — parity และ hardcoded-text gates ให้คุณ honest

## Docs localization

docs เหล่านี้ localized too Docusaurus i18n configured สำหรับ ทั้งหมด 23 locales (`website/i18n/`) ด้วย locale dropdown ใน navbar และ RTL สำหรับ Arabic Scaffold locale ของ translation files ด้วย `npm run write-translations -- --locale <code>` และ translate ภายใต้ `website/i18n/<code>/` Per localization mandate **adding หรือ changing ใด ๆ doc หมายถึง updating ทุก locale ใน same change**
