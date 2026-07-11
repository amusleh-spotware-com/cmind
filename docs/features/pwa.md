# Installable app (PWA)

cMind installs to a phone or desktop like a native app — home-screen icon, standalone window, splash,
and a friendly offline page. It is **mobile-first** and fully responsive; see
[ui-guidelines.md](../ui-guidelines.md).

## What "installable" means here — and the honest limit

Blazor **Server** renders through a live SignalR circuit, so the app cannot run fully offline. What the
PWA delivers:

- **Installable** — valid web manifest + icons, so browsers offer *Install* / *Add to Home Screen*.
- **App-shell cached** — the service worker caches static assets (CSS, icons, manifest) and shows an
  **offline page** when the network drops, instead of a browser error.
- **Native feel** — standalone display, branded theme-color/status bar, app icon, iOS home-screen icon.

It does **not** provide offline interactivity — that would require Blazor WebAssembly (a separate future
track). Don't promise offline use of live features.

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

Served dynamically from `BrandingOptions` so a reseller's product name, colours and icons carry into the
installed app: `name`/`short_name` from `ProductName`, `description`, `theme_color` from `AppBarColor`,
`background_color` from `BackgroundColor`, `display: standalone`, and the icon set (incl. a **maskable**
512 for a clean Android icon). Anonymous — the install prompt must work before sign-in.

### Service worker

App-shell only. It **never** intercepts the Blazor circuit (`/_blazor`), framework (`/_framework`), or
SignalR hubs (`/hubs`) — those are always network. Navigations are network-first with the offline page
as fallback; static assets (`/css`, `/icons`, `/_content`) are cache-first with background revalidate.
Registered with `updateViaCache: 'none'` so worker updates apply reliably. Caches are versioned
(`cmind-shell-v<n>`) — bump on shell changes.

### iOS

iOS ignores manifest icons/splash, so `App.razor` also emits `apple-touch-icon` and
`apple-mobile-web-app-*` meta tags. iOS has no `beforeinstallprompt`; users install via Safari's *Add to
Home Screen*. `beforeinstallprompt` is captured into `window.deferredInstallPrompt` on Chromium/Android
for a custom install affordance.

## Tests

- **E2E** — `E2ETests/PwaTests.cs`: manifest served with `application/manifest+json`, non-empty icons incl.
  a maskable one, `display: standalone`, `apple-touch-icon` linked, and the service worker registers +
  activates. `MobileLayoutTests` / `MobileDialogTests` cover the mobile shell the PWA installs.
