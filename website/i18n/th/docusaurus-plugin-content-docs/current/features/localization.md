# Localization (i18n)

cMind สามารถ localize ได้เต็มรูปแบบและ ship ใน **23 languages เดียวกันกับที่ cTrader รองรับ**
ดังนั้น trader ใช้แพลตฟอร์ม — และอ่าน docs เหล่านี้ — ในภาษาของเขา English เป็น fallback;
translation ที่ขาดจะ degrade gracefully ไปยัง English มากกว่าแสดง blank หรือ raw key

## ภาษาที่รองรับ

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese

แหล่งข้อมูลจริงเพียงแห่งเดียวคือ `Core.Constants.SupportedCultures` — request-culture
middleware, language switcher, resource-parity test และ no-hardcoded-string gate ทั้งหมดอ่าน
จากมัน การเพิ่มภาษาคือ one-line change ที่นั่นบวก resource files

## มันทำงานอย่างไร (Blazor Server)

- **Resources.** UI strings อยู่ใน `src/Web/Resources/Ui.resx` (English base) บวกหนึ่ง
  `Ui.<culture>.resx` ต่อภาษา Components อ่านผ่าน `IStringLocalizer<Ui>` — `@L["key"]`,
  ไม่ใช่ literal `.resx` files ถูก generate จาก `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), แหล่งข้อมูลจริงที่ translator-friendly
- **Culture resolution.** `RequestLocalizationMiddleware` เลือก culture จาก `.AspNetCore.Culture`
  cookie ก่อน, แล้ว browser's `Accept-Language`, แล้ว English
- **Switching.** App-bar language switcher (และ **Settings → Language** section) navigate ไปยัง
  `GET /set-culture` endpoint — full-reload นอก Blazor circuit, เพราะ circuit ไม่สามารถ
  change culture live มันเขียน cookie และสำหรับ signed-in user, persist ทางเลือกไปยัง
  profile ของเขา (`UserProfile.Locale`); reload boot circuit ใหม่ในภาษาที่เลือก
- **Persistence & login.** Saved profile locale เขียนกลับเข้า culture cookie ตอน sign-in,
  ดังนั้น user ลงจอดในภาษาของเขาบนทุก device
- **Right-to-left.** Arabic (และภาษา RTL ใดๆ ในอนาคต) ตั้ง `<html dir="rtl">` และ wrap
  layout ใน MudBlazor's `MudRTLProvider`, mirror entire shell
- **ICU.** Web host ทำงานพร้อม ICU enabled (`InvariantGlobalization=false`); wire/parse code
  stays on `CultureInfo.InvariantCulture`, ดังนั้นเฉพาะ per-culture UI formatting ที่ได้รับผลกระทบ
  — ไม่เคย backtest หรือ CSV

## The gate — ไม่มี hard-coded UI text

strings ที่เป็น user-facing ใหม่ **ไม่สามารถ** merge un-localized ใน covered scope:

- ทดสอบ build-failing arch-guard (`NoHardcodedUiTextTests`) scan migrated `.razor` files และ fail
  บน literal, text-bearing attribute ใดๆ (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) ที่ไม่ใช่ `@L["…"]` lookup
- ทดสอบ resource-parity (`ResourceParityTests`) fail build ถ้าภาษาใดขาด key หรือส่งค่าว่าง
  — ทุกภาษาเสมอมีทุก key

## การเพิ่มหรือเปลี่ยน string

1. เพิ่ม/edit key ใน `tools/i18n/ui-translations.json` สำหรับ **ทุก** culture
2. Regenerate `.resx`: `pwsh tools/i18n/gen-resx.ps1`
3. อ้างอิงใน component ด้วย `@L["your.key"]`
4. `dotnet test` — parity และ hardcoded-text gates ทำให้คุณซื่อสัตย์

## Docs localization

docs เหล่านี้ localize ด้วย Docusaurus i18n ถูก configure สำหรับทั้ง 23 locales
(`website/i18n/`), พร้อม locale dropdown ใน navbar และ RTL สำหรับ Arabic scaffold locale's
translation files ด้วย `npm run write-translations -- --locale <code>` และ translate ภายใต้
`website/i18n/<code>/` ตาม localization mandate, **การเพิ่มหรือเปลี่ยน doc ใดหมายถึง
การอัปเดตทุก locale ในการเปลี่ยนเดียวกัน**
