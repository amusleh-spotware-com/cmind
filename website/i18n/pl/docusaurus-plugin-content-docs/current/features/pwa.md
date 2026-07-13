---
description: "cMind installs do phone lub desktop jak native app — home-screen icon, standalone window, splash, i friendly offline page. To mobile-first i…"
---

# Installable app (PWA)

cMind installs do phone lub desktop jak native app — home-screen icon, standalone window, splash,
i friendly offline page. To **mobile-first** i fully responsive; see
[ui-guidelines.md](../ui-guidelines.md).

## Co "installable" means tutaj — i honest limit

Blazor **Server** renders przez live SignalR circuit, więc app nie może run fully offline. Co
PWA delivers:

- **Installable** — valid web manifest + icons, więc browsers offer *Install* / *Add to Home Screen*.
- **App-shell cached** — service worker caches static assets (CSS, icons, manifest) i shows
  **offline page** gdy network drops, zamiast browser error.
- **Native feel** — standalone display, branded theme-color/status bar, app icon, iOS home-screen icon.

To **nie** provides offline interactivity — że by require Blazor WebAssembly (separate future
track). Don't promise offline use z live features.

## Pieces

| Piece | Gdzie |
|-------|-------|
| Manifest (dynamic, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Icons (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback page | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route constants | `Core.Constants.PwaRoutes` |

### Manifest

Served dynamically z `BrandingOptions` więc reseller's product name, colours i icons carry do
installed app: `name`/`short_name` z `ProductName`, `description`, `theme_color` z `AppBarColor`,
`background_color` z `BackgroundColor`, `display: standalone`, i icon set (incl. **maskable**
512 dla clean Android icon). Anonymous — install prompt musi work przed sign-in.

### Service worker

App-shell tylko. To **nigdy** intercepts Blazor circuit (`/_blazor`), framework (`/_framework`), lub
SignalR hubs (`/hubs`) — te są zawsze network. Navigations to network-first z offline page
jako fallback; static assets (`/css`, `/icons`, `/_content`) to cache-first z background revalidate.
Registered z `updateViaCache: 'none'` więc worker updates apply reliably. Caches to versioned
(`cmind-shell-v<n>`) — bump na shell changes.

### iOS

iOS ignores manifest icons/splash, więc `App.razor` także emits `apple-touch-icon` i
`apple-mobile-web-app-*` meta tags. iOS has no `beforeinstallprompt`; users install via Safari's *Add to
Home Screen*. `beforeinstallprompt` to captured do `window.deferredInstallPrompt` na Chromium/Android
dla custom install affordance.

## Testy

- **E2E** — `E2ETests/PwaTests.cs`: manifest served z `application/manifest+json`, non-empty icons incl.
  maskable jeden, `display: standalone`, `apple-touch-icon` linked, i service worker registers +
  activates. `MobileLayoutTests` / `MobileDialogTests` cover mobile shell które PWA installs.
