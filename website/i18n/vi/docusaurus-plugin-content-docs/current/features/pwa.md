---
description: "cMind cài đặt vào điện thoại hoặc máy tính để bàn như một ứng dụng native — home-screen icon, standalone window, splash, và một trang offline thân thiện. Nó là mobile-first và…"
---

# Ứng dụng có thể cài đặt (PWA)

cMind cài đặt vào điện thoại hoặc máy tính để bàn như một ứng dụng native — home-screen icon, standalone window, splash, và một trang offline thân thiện. Nó là **mobile-first** và hoàn toàn responsive; xem [ui-guidelines.md](../ui-guidelines.md).

## "Có thể cài đặt" có nghĩa gì ở đây — và giới hạn trung thực

Blazor **Server** render qua một live SignalR circuit, nên ứng dụng không thể chạy hoàn toàn offline. Những gì PWA cung cấp:

- **Có thể cài đặt** — valid web manifest + icons, nên browsers cung cấp *Install* / *Add to Home Screen*.
- **App-shell cached** — service worker caches static assets (CSS, icons, manifest) và hiển thị một **offline page** khi network drops, thay vì browser error.
- **Native feel** — standalone display, branded theme-color/status bar, app icon, iOS home-screen icon.

Nó **không** cung cấp offline interactivity — điều đó sẽ yêu cầu Blazor WebAssembly (một track tương lai riêng biệt). Không hứa sử dụng offline của live features.

## Pieces

| Piece | Where |
|-------|-------|
| Manifest (dynamic, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Icons (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback page | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route constants | `Core.Constants.PwaRoutes` |

### Manifest

Served động từ `BrandingOptions` nên product name, colours và icons của reseller carry vào installed app: `name`/`short_name` từ `ProductName`, `description`, `theme_color` từ `AppBarColor`, `background_color` từ `BackgroundColor`, `display: standalone`, và icon set (incl. một **maskable** 512 cho clean Android icon). Anonymous — install prompt phải work trước sign-in.

### Service worker

App-shell chỉ. Nó **không bao giờ** intercepts Blazor circuit (`/_blazor`), framework (`/_framework`), hoặc SignalR hubs (`/hubs`) — những cái đó luôn network. Navigations là network-first với offline page như fallback; static assets (`/css`, `/icons`, `/_content`) là cache-first với background revalidate.
Registered với `updateViaCache: 'none'` nên worker updates apply reliably. Caches được versioned (`cmind-shell-v<n>`) — bump trên shell changes.

### iOS

iOS bỏ qua manifest icons/splash, nên `App.razor` cũng emits `apple-touch-icon` và `apple-mobile-web-app-*` meta tags. iOS không có `beforeinstallprompt`; users cài đặt qua Safari's *Add to Home Screen*. `beforeinstallprompt` được capture vào `window.deferredInstallPrompt` trên Chromium/Android cho custom install affordance.

## Tests

- **E2E** — `E2ETests/PwaTests.cs`: manifest served với `application/manifest+json`, non-empty icons incl. một maskable, `display: standalone`, `apple-touch-icon` linked, và service worker registers + activates. `MobileLayoutTests` / `MobileDialogTests` cover mobile shell mà PWA cài đặt.
