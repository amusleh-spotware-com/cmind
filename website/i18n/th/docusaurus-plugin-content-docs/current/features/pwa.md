---
description: "cMind installs เป็น phone หรือ desktop เหมือน native app — home-screen icon standalone window splash และ friendly offline page มัน mobile-first และ fully responsive; ดู ui-guidelines.md"
---

# Installable app (PWA)

cMind installs เป็น phone หรือ desktop เหมือน native app — home-screen icon standalone window splash และ friendly offline page มัน **mobile-first** และ fully responsive; ดู [ui-guidelines.md](../ui-guidelines.md)

## สิ่งที่ "installable" หมายความว่า here — และ honest limit

Blazor **Server** renders ผ่าน live SignalR circuit ดังนั้น app ไม่สามารถ run fully offline สิ่งที่ PWA delivers:

- **Installable** — valid web manifest + icons ดังนั้น browsers offer *Install* / *Add เป็น Home Screen*
- **App-shell cached** — service worker caches static assets (CSS icons manifest) และ shows **offline page** เมื่อ network drops แทน browser error
- **Native feel** — standalone display branded theme-color/status bar app icon iOS home-screen icon

มัน **ไม่** provide offline interactivity — ที่ would require Blazor WebAssembly (separate future track) ไม่ promise offline use ของ live features

## Pieces

| Piece | ที่ |
|-------|-------|
| Manifest (dynamic branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Icons (192 512 512-maskable apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback page | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route constants | `Core.Constants.PwaRoutes` |

### Manifest

served dynamically จาก `BrandingOptions` ดังนั้น reseller's product name colours และ icons carry เข้าไป installed app: `name`/`short_name` จาก `ProductName` `description` `theme_color` จาก `AppBarColor` `background_color` จาก `BackgroundColor` `display: standalone` และ icon set (incl. a **maskable** 512 สำหรับ clean Android icon) anonymous — install prompt ต้อง work ก่อน sign-in

### Service worker

app-shell เพียง มัน **ไม่เคย** intercepts Blazor circuit (`/_blazor`) framework (`/_framework`) หรือ SignalR hubs (`/hubs`) — เหล่านั้น always network navigations เป็น network-first ด้วย offline page เป็น fallback; static assets (`/css` `/icons` `/_content`) เป็น cache-first ด้วย background revalidate registered ด้วย `updateViaCache: 'none'` ดังนั้น worker updates apply reliably caches versioned (`cmind-shell-v<n>`) — bump บน shell changes

### iOS

iOS ignores manifest icons/splash ดังนั้น `App.razor` ด้วย emits `apple-touch-icon` และ `apple-mobile-web-app-*` meta tags iOS มี no `beforeinstallprompt`; users install ผ่าน Safari's *Add เป็น Home Screen* `beforeinstallprompt` captured เป็น `window.deferredInstallPrompt` บน Chromium/Android สำหรับ custom install affordance

## Tests

- **E2E** — `E2ETests/PwaTests.cs`: manifest served ด้วย `application/manifest+json` non-empty icons incl. maskable one `display: standalone` `apple-touch-icon` linked และ service worker registers + activates `MobileLayoutTests` / `MobileDialogTests` cover mobile shell PWA installs
