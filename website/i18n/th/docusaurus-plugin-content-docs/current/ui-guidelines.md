---
description: "ข้อบังคับสำหรับทุกส่วนใหม่หรือเปลี่ยนแปลงของ UI ในแอปพลิเคชันนี้ (หน้า Blazor, ไดอะล็อก, คอมโพเนนต์) นี่คือแหล่งข้อมูลดั้งเดิมที่อ้างอิงโดย CLAUDE.md หากกฎ…"
---

# คำแนะนำการออกแบบ UI — บังคับใช้

ข้อบังคับสำหรับ **ทุก** ส่วนใหม่หรือเปลี่ยนแปลงของ UI ในแอปพลิเคชันนี้ (หน้า Blazor, ไดอะล็อก, คอมโพเนนต์)
นี่คือแหล่งข้อมูลดั้งเดิมที่อ้างอิงโดย `CLAUDE.md` หากกฎขัดขวางคุณ ให้หยุดและถามคำถาม — อย่าส่งมอบ UI ที่ละเมิดกฎนั้น มีรากฐานมาจาก `plans/ui-overhaul.md`

## 1. Mobile-first เสมอ

- **เขียนโค้ดสำหรับโทรศัพท์ 360–430px ก่อน** จากนั้นเพิ่มเติมขึ้นไปด้วย `min-width` media queries / prop MudBlazor
  breakpoint ไม่ใช่ desktop-first โดยใช้ `max-width` overrides
- **ไม่มีการเลื่อนแนวนอนในทุกความกว้าง 320–1920px** หากเนื้อหากว้างกว่า viewport จะถือว่าเป็นข้อบกพร่อง
- เป้าหมายการสัมผัส ≥ **44px** (`var(--app-touch-target)`) ฟอนต์ข้อความอินพุต ≥ 16px (ป้องกัน iOS zoom-on-focus)
- เคารพ notches: ใช้ `env(safe-area-inset-*)`; viewport ได้ตั้งค่า `viewport-fit=cover` แล้ว
- เคารพ `prefers-reduced-motion` — ไม่มีข้อมูลที่จำเป็นที่ถ่ายทำโดยแอนิเมชันเท่านั้น

## 2. Design tokens — ไม่มีค่าคงที่ยากเข้า

- สีทั้งหมด/radius/spacing มาจาก **design tokens**: MudBlazor theme (`Web/Components/Theme.cs`) +
  CSS custom properties ที่ปล่อยออกมาจาก `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …)
- **ไม่ใช้รหัสสีฮex, radius หรือสตริง brand ไว้ในคอมโพเนนต์หรือกฎ CSS** อ่านโทเค็น
  Tokens ไหลมาจาก white-label `BrandingOptions` ดังนั้นจานสีของผู้ขายต้องเข้าถึง UI ของคุณได้ฟรี
- ค่าใหม่ที่ส่งผลต่อแบรนด์ → เพิ่มโทเค็น + เขตข้อมูล branding; อย่าฝังมันไว้

## 3. Responsive layout & data

- **ตารางยุบเป็นการ์ดบนโทรศัพท์** ทุก `MudTable` ตั้งค่า `Breakpoint="Breakpoint.Sm"` และทุก
  `MudTd` มี `DataLabel` ไม่มีตารางกว้างดั้งเดิมบนมือถือ (แม่แบบ: `Components/Pages/Nodes.razor`)
- กริด: `MudItem xs="12" sm="6" md="4"` — ความกว้างเต็มบนโทรศัพท์, หลายคอลัมน์ขึ้น
- ฟอร์มแนวตั้งเดียวบนมือถือ; tap targets ขนาดใหญ่; `inputmode`/`autocomplete` บนอินพุต; numeric/decimal
  inputmode สำหรับเงิน/เปอร์เซ็นต์
- **ใช้คอนโทรลที่เหมาะสมสำหรับอินพุตที่มีโครงสร้าง — ไม่ใช่กล่องข้อความดั้งเดิมสำหรับตัวเลขหรือรายการ** เก็บตัวเลข,
  เงิน, เปอร์เซ็นต์, วันที่, enum และข้อมูลค่าหลายค่าใด ๆ ด้วยคอนโทรลที่ถูกต้อง (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, รายการแถวที่แก้ไขได้เพิ่ม/ลบ หรือตาราง) แต่ละเขตข้อมูล
  ได้รับการตรวจสอบความถูกต้องแยกต่างหาก `MudTextField` ฟรีข้อความเดียวที่ผู้ใช้ต้องพิมพ์ blob ที่คั่นด้วยเครื่องหมายจุลภาค/ช่องว่าง/ขึ้นบรรทัด
  จากนั้นคุณจะแยก — **ห้ามใช้**: ติดตั้งที่ไม่มีการตรวจสอบ และไม่เป็นมิตรกับโทรศัพท์ **ไม่มีใครต้องการพิมพ์ blob** อินพุตค่าหลายค่า
  คือรายการแถวแก้ไขได้ (เพิ่ม /
  ลบ) หรือโหลดจากข้อมูลโดเมนที่มีอยู่ (เช่น เรียกใช้การตรวจสอบโดยตรงจากการ backtest ที่เสร็จสิ้น
  แทนที่จะป้อนตัวเลขใหม่) `MudTextField` ธรรมชาติ ใช้เฉพาะกับข้อความฟรีแท้ — ชื่อ, หมายเหตุ,
  ค้นหา, คำอธิบาย
- จัดเตรียม **โหลด, ว่าง, และข้อผิดพลาด** สถานะในทุกรายการ/รายละเอียด — ขนาดสำหรับมือถือ
- การนำทาง **ด้านล่างของมือถือ** (`Components/Layout/BottomNav.razor`) คือการนำทางโทรศัพท์หลัก; ลิ้นชักแบบจัดกลุ่มคือเมนูเต็ม เพิ่มจุดหมายปลายทาง high-traffic ที่นั่น; เก็บไว้ ≤5 รายการ

## 4. Dialogs (create/edit)

- การดำเนินการเพิ่ม/สร้าง/แก้ไข/ใหม่ทั้งหมดใช้ **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), ไม่ใช่
  แบบฟอร์มหน้าแบบ inline Dialogs อาศัยอยู่ใน `Web/Components/Dialogs/` เปิดเผย `[Parameter]`s, กลับมา nested
  `public sealed record …Result(...)` การดำเนินการแถวรายการ (เริ่ม/หยุด/ลบ) อยู่แบบ inline เป็นปุ่มไอคอน
- บนโทรศัพท์ ไดอะล็อกควร **เต็มหน้าจอ / ความกว้างเต็ม** และรู้จักแป้นพิมพ์

## 5. Inline help — ทุกคอนโทรล

- ทุกตัวเลือก, เลือก, สวิตช์, หรือการดำเนินการที่ไม่ชัดเจนจะได้รับ **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover บน desktop, **แตะบนมือถือ** ใช้ข้อความจาก `docs/` เพื่อให้
  คำแนะนำอยู่ในการซิงค์กับพฤติกรรม; อัปเดตทั้งสองในการ commit เดียวกัน

## 6. White-label

- ชื่อผลิตภัณฑ์, โลโก้, คำอธิบาย, การสนับสนุน/บริษัท, สี, favicon ทั้งหมดมาจาก `BrandingOptions`
  อ้างอิงจากพวกมัน (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), ไม่ใช่ "cMind" ตามตัวอักษรหรือ
  สีแบรนด์ manifest PWA, ไอคอน, theme-color, และ login hero ล้วนมีแบรนด์

## 7. PWA

- แอปสามารถติดตั้งได้ เก็บปลายจุด manifest (`/manifest.webmanifest`) ไว้ที่มีแบรนด์, ไอคอนอยู่
  (192/512/maskable + apple-touch), service worker app-shell-only (ไม่มีการสัมผัส Blazor
  circuit/`_framework`/hubs), และหน้าออนไลน์ทำงาน เส้นทางแบบ static ใหม่ → ให้ manifest `scope`
- Blazor Server ต้องการวงจร SignalR ที่ใช้งานจริง → **ติดตั้งได้ + app-shell**, ไม่ใช่ offline เต็มรูปแบบ อย่า
  สัญญา interactivity offline

## 8. Accessibility

- ป้ายกำกับบนอินพุต, `aria-*` บนคอนโทรลแบบกำหนดเอง, focus ที่มองเห็นได้, ลำดับ focus ตรรกะ เพราะ theme
  ปรับแต่งได้ตามไว ตรวจสอบ **contrast** กับ theme ที่ใช้งาน ไม่ใช่จาน palette ที่คงที่

## 9. E2E — ไม่มี UI ส่งมอบโดยไม่ได้ทดสอบ (blocking)

ทุกการเปลี่ยนแปลงที่หันหน้าไปยังผู้ใช้จัดส่ง Playwright E2E ใน `tests/E2ETests`, ขับเคลื่อนเหมือนผู้ใช้จริง, **บน mobile
device emulation** บวก desktop:

- เส้นทางใหม่ → เพิ่มไปที่ `PageSmokeTests` **และ** `MobileLayoutTests` (แสดงผล, bottom nav, ไม่มี UI ข้อผิดพลาด)
- แปลงตาราง/หน้า → เพิ่มเส้นทางไปที่มือถือ **no-overflow** set
- ไหลใหม่ → การเดินทาง mobile ที่สมจริง (create/edit/save round-trip) **และ** unhappy path
  (อินพุตไม่ถูกต้อง, รายการว่าง, permission-denied ต่อบทบาท)
- ปลายเปลี่ยนความช่วยเหลือ → ยืนยันว่ามันเปิดบน tap (`HelpTipTests` pattern)
- ใช้ `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation)
- `dotnet test` สีเขียวก่อน "done" Emulated WebKit ≠ mobile Safari — real-device gating เป็นขั้นตอนการปล่อยแยกต่างหาก

## 10. Definition of done (UI)

- [ ] Mobile-first; ไม่มี overflow แนวนอน 320–1920px; touch targets ≥44px
- [ ] Design tokens เท่านั้น — ศูนย์สีคง/radii/brand strings ยากเข้า
- [ ] ตาราง → การ์ดบนโทรศัพท์ (`DataLabel` + `Breakpoint.Sm`); โหลด/ว่าง/สถานะข้อผิดพลาดอยู่
- [ ] อินพุตที่มีโครงสร้างใช้คอนโทรลที่ถูกต้องที่ตรวจสอบความถูกต้อง (numeric/date/select/editable row list) — ไม่มี raw
      text box ที่ผู้ใช้พิมพ์ตัวเลข/ค่า blob ที่คั่นไว้ในนั้น
- [ ] สร้าง/แก้ไขผ่าน dialog; เต็มหน้าจออนมือถือ
- [ ] ทุกคอนโทรลมี `HelpTip` ที่มาจากเอกสาร
- [ ] White-label + PWA ถูกเคารพ
- [ ] E2E มือถือ + desktop เพิ่ม (smoke, no-overflow, journey, unhappy path); `dotnet test` สีเขียว
- [ ] Rider `get_file_problems` + `dotnet format analyzers` สะอาดบนไฟล์ที่สัมผัส
