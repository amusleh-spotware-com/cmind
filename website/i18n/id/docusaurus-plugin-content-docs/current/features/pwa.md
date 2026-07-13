---
description: "cMind install ke ponsel atau desktop seperti aplikasi native — home-screen icon, standalone window, splash, dan friendly offline page. Ini mobile-first dan…"
---

# Aplikasi yang dapat diinstal (PWA)

cMind install ke ponsel atau desktop seperti aplikasi native — home-screen icon, standalone window, splash,
dan friendly offline page. Ini **mobile-first** dan fully responsive; lihat
[ui-guidelines.md](../ui-guidelines.md).

## Apa "dapat diinstal" berarti di sini — dan batas jujurnya

Blazor **Server** renders melalui live SignalR circuit, jadi aplikasi tidak dapat berjalan fully offline. Yang
PWA berikan:

- **Installable** — valid web manifest + icons, jadi browsers tawarkan *Install* / *Add to Home Screen*.
- **App-shell cached** — service worker caches static assets (CSS, icons, manifest) dan menampilkan
  **offline page** ketika network drops, alih-alih browser error.
- **Native feel** — standalone display, branded theme-color/status bar, app icon, iOS home-screen icon.

Ini **tidak** menyediakan offline interactivity — yang akan memerlukan Blazor WebAssembly (separate future
track). Jangan menjanjikan offline use dari live features.

## Pieces

| Piece | Di mana |
|-------|-------|
| Manifest (dynamic, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Icons (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback page | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route constants | `Core.Constants.PwaRoutes` |

### Manifest

Served secara dinamis dari `BrandingOptions` jadi product name reseller, warna dan icons terbawa ke
aplikasi yang diinstal: `name`/`short_name` dari `ProductName`, `description`, `theme_color` dari `AppBarColor`,
`background_color` dari `BackgroundColor`, `display: standalone`, dan icon set (incl. **maskable**
512 untuk clean Android icon). Anonymous — install prompt harus bekerja sebelum sign-in.

### Service worker

App-shell saja. Ia **tidak pernah** intercepts Blazor circuit (`/_blazor`), framework (`/_framework`), atau
SignalR hubs (`/hubs`) — itu selalu network. Navigations adalah network-first dengan offline page
sebagai fallback; static assets (`/css`, `/icons`, `/_content`) adalah cache-first dengan background revalidate.
Registered dengan `updateViaCache: 'none'` jadi worker updates apply reliably. Caches adalah versioned
(`cmind-shell-v<n>`) — bump pada shell changes.

### iOS

iOS ignores manifest icons/splash, jadi `App.razor` juga emits `apple-touch-icon` dan
`apple-mobile-web-app-*` meta tags. iOS tidak memiliki `beforeinstallprompt`; pengguna install via Safari's *Add to
Home Screen*. `beforeinstallprompt` adalah captured ke `window.deferredInstallPrompt` pada Chromium/Android
untuk custom install affordance.

## Tests

- **E2E** — `E2ETests/PwaTests.cs`: manifest served dengan `application/manifest+json`, non-empty icons incl.
  maskable one, `display: standalone`, `apple-touch-icon` linked, dan service worker registers +
  activates. `MobileLayoutTests` / `MobileDialogTests` cover mobile shell yang PWA install.
