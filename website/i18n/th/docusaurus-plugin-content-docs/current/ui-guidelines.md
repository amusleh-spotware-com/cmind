---
description: "Binding สำหรับ piece UI ทุก ๆ ใหม่หรือเปลี่ยน ในแอป นี้ (Blazor pages dialogs components) นี่คือ source truth referenced โดย CLAUDE.md ถ้า rule block คุณ stop และ ask — ไม่ ship UI ที่ violates มัน Rooted ใน plans/ui-overhaul.md"
---

# UI Design Guidelines — MANDATORY

Binding สำหรับ **every** ใหม่หรือเปลี่ยน piece UI ในแอป นี้ (Blazor pages dialogs components) นี่คือ source truth referenced โดย `CLAUDE.md` ถ้า rule block คุณ stop และ ask — ไม่ ship UI ที่ violates มัน Rooted ใน `plans/ui-overhaul.md`

## 1. Mobile-first ทั้งหมด

- **Author สำหรับ 360–430px phone first** แล้ว enhance ขึ้น ด้วย `min-width` media queries / MudBlazor breakpoint props ไม่เคย desktop-first ด้วย `max-width` overrides
- **ไม่มี horizontal scroll ที่ width ใด ๆ 320–1920px** ถ้า content กว้างกว่า viewport มัน bug
- Touch targets ≥ **44px** (`var(--app-touch-target)`) Text inputs ≥ 16px font (stops iOS zoom-on-focus)
- Respect notches: use `env(safe-area-inset-*)`; viewport แล้ว sets `viewport-fit=cover`
- Honour `prefers-reduced-motion` — ไม่มี essential info conveyed ด้วย animation เท่านั้น

## 2. Design tokens — ไม่มี hard-coded values

- ทั้ง colour/radius/spacing มาจาก **design tokens**: MudBlazor theme (`Web/Components/Theme.cs`) + CSS custom properties ปล่อย โดย `Web/Branding/BrandingCss.cs` (`var(--app-primary)` `--app-surface` `--app-border` `--app-text*` `--app-radius` …)
- **ไม่เคย hard-code hex colour radius หรือ brand string ในใด ๆ component หรือ CSS rule** อ่าน token Tokens ไหล จาก white-label `BrandingOptions` ดังนั้น reseller palette ต้อง reach UI ของคุณ ฟรี
- ใหม่ brand-affecting value → เพิ่ม token + branding field; ไม่ inline มัน

## 3. Responsive layout & data

- **Tables collapse เป็น cards บน phones** ทุก ๆ `MudTable` sets `Breakpoint="Breakpoint.Sm"` และ ทุก ๆ `MudTd` มี `DataLabel` ไม่มี raw wide table บน mobile (Template: `Components/Pages/Nodes.razor`)
- Grids: `MudItem xs="12" sm="6" md="4"` — full-width บน phone multi-column ขึ้น
- Forms single-column บน mobile; large tap targets; `inputmode`/`autocomplete` บน inputs; numeric/decimal inputmode สำหรับ money/percent
- Provide **loading empty และ error** states บน list/detail ทั้งหมด — sized สำหรับ mobile
- mobile **bottom navigation** (`Components/Layout/BottomNav.razor`) คือ primary phone nav; grouped drawer คือ full menu เพิ่ม high-traffic destinations ที่นั่น; keep มัน ≤5 items

## 4. Dialogs (create/edit)

- ทั้ง add/create/edit/new actions ใช้ **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`) ไม่เคย inline page form Dialogs อยู่ใน `Web/Components/Dialogs/` expose `[Parameter]`s ส่งกลับ nested `public sealed record …Result(...)` List row actions (start/stop/delete) stay inline เป็น icon buttons
- บน phones dialogs ควร **full-screen / full-width** และ keyboard-aware

## 5. Inline help — ทุก ๆ control

- ทุก ๆ non-obvious option select switch หรือ action ได้ **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — hover บน desktop **tap บน mobile** Source text จาก `docs/` ดังนั้น guidance stay ใน sync ด้วย behaviour; update ทั้งสอง ใน commit เดียว

## 6. White-label

- Product name logo description support/company colours favicon ทั้งหมด มาจาก `BrandingOptions` Reference พวกเขา (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`) ไม่เคย literal "cMind" หรือ brand colour PWA manifest icons theme-color และ login hero ทั้งหมด branded

## 7. PWA

- app ใช้ installable keep manifest endpoint (`/manifest.webmanifest`) branded icons present (192/512/maskable + apple-touch) service worker app-shell-only (ไม่เคย touching Blazor circuit/`_framework`/hubs) และ offline page working ใหม่ static route → keep manifest `scope`
- Blazor Server ต้อง live SignalR circuit → **installable + app-shell** ไม่มี full offline ไม่ promise offline interactivity

## 8. Accessibility

- Labels บน inputs `aria-*` บน custom controls visible focus logical focus order เพราะ theme white-labelable verify **contrast** ต้านแบบธรรมชาติ active theme ไม่ fixed palette

## 9. E2E — ไม่มี UI ships untested (blocking)

ทุก ๆ user-facing เปลี่ยน ships Playwright E2E ใน `tests/E2ETests` driven เหมือน real user **บน mobile device emulation** บวก desktop:

- ใหม่ route → เพิ่มมัน `PageSmokeTests` **และ** `MobileLayoutTests` (renders bottom nav ไม่มี error UI)
- Convert table/page → เพิ่ม route ของมัน เป็น mobile **no-overflow** set
- ใหม่ flow → realistic mobile journey (create/edit/save round-trip) **และ** unhappy path (invalid input empty list permission-denied per role)
- ใหม่ help tip → assert มันเปิด บน tap (`HelpTipTests` pattern)
- ใช้ `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device emulation)
- `dotnet test` green ก่อน "done" Emulated WebKit ≠ mobile Safari — real-device gating เป็น release step แยก

## 10. Definition ของ done (UI)

- [ ] Mobile-first; ไม่มี horizontal overflow 320–1920px; touch targets ≥44px
- [ ] เพียง design tokens — zero hard-coded colours/radii/brand strings
- [ ] Tables → cards บน phone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error states present
- [ ] Create/edit ผ่าน dialog; full-screen บน mobile
- [ ] ทุก ๆ control มี `HelpTip` sourced จาก docs
- [ ] White-label + PWA respected
- [ ] Mobile + desktop E2E added (smoke no-overflow journey unhappy path); `dotnet test` green
- [ ] Rider `get_file_problems` + `dotnet format analyzers` clean บน touched files
