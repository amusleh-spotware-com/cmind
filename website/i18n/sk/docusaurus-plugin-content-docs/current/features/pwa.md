---
description: "cMind nainštaluje sa na telefón alebo desktop ako native app — home-screen ikona, standalone okno, splash a priateľská offline stránka. Je to mobile-first a..."
---

# Installable app (PWA)

cMind nainštaluje sa na telefón alebo desktop ako native app — home-screen ikona, standalone okno, splash
a priateľská offline stránka. Je to **mobile-first** a plne responsive; pozrite
[ui-guidelines.md](../ui-guidelines.md).

## Čo "installable" znamená tu — a čestný limit

Blazor **Server** renderuje cez live SignalR circuit, takže aplikácia nemôže bežať plne offline. Čo PWA dodáva:

- **Installable** — platný web manifest + ikony, takže prehliadače ponúkajú *Install* / *Add to Home Screen*.
- **App-shell cached** — service worker cachuje static assets (CSS, ikony, manifest) a ukazuje
  **offline page** keď sieť padne, namiesto browser error.
- **Native feel** — standalone display, branded theme-color/status bar, app icon, iOS home-screen icon.

Neposkytuje **offline interaktivity** — to by vyžadovalo Blazor WebAssembly (oddelená budúcna stopa). Neobľubujte offline použitie live features.

## Kusy

| Kus | Kde |
|-------|-------|
| Manifest (dynamic, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Icons (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback page | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route constants | `Core.Constants.PwaRoutes` |

### Manifest

Podávaný dynamicky z `BrandingOptions`, takže reseller product name, farby a ikony majú v
installed app: `name`/`short_name` z `ProductName`, `description`, `theme_color` z `AppBarColor`,
`background_color` z `BackgroundColor`, `display: standalone` a icon set (incl. **maskable**
512 pre čistý Android icon). Anonymous — install prompt musí pracovať pred sign-in.

### Service worker

App-shell len. Nikdy **neprekrýva** Blazor circuit (`/_blazor`), framework (`/_framework`) alebo
SignalR hubs (`/hubs`) — tí sú vždy network. Navigations sú network-first s offline page
ako fallback; static assets (`/css`, `/icons`, `/_content`) sú cache-first s background revalidate.
Registered s `updateViaCache: 'none'`, takže worker updates sa aplikujú spoľahlivo. Caches sú versioned
(`cmind-shell-v<n>`) — bump na shell zmeny.

### iOS

iOS ignoruje manifest icons/splash, takže `App.razor` tiež emituje `apple-touch-icon` a
`apple-mobile-web-app-*` meta tags. iOS nemá `beforeinstallprompt`; používatelia nainštalujú cez Safari *Add to
Home Screen*. `beforeinstallprompt` je captured do `window.deferredInstallPrompt` na Chromium/Android
pre custom install affordance.

## Testy

- **E2E** — `E2ETests/PwaTests.cs`: manifest podávaný s `application/manifest+json`, non-empty icons incl.
  maskable jeden, `display: standalone`, `apple-touch-icon` linked a service worker registers +
  activates. `MobileLayoutTests` / `MobileDialogTests` pokrývajú mobile shell, ktorý PWA nainštaluje.
