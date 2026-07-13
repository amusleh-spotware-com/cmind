---
description: "cMind se instalira na telefon ili desktop kao native aplikacija — ikonica na početnom ekranu, samostalni prozor, splash, i prijateljska offline stranica. Mobile-first je i potpuno responsive."
---

# Instalabilna aplikacija (PWA)

cMind se instalira na telefon ili desktop kao native aplikacija — ikonica na početnom ekranu, samostalni prozor, splash,
i prijateljska offline stranica. **Mobile-first** je i potpuno responsive; pogledajte
[ui-guidelines.md](../ui-guidelines.md).

## Šta „instalabilno" znači ovde — i iskrena ograničenja

Blazor **Server** renderuje kroz live SignalR krug, tako da aplikacija ne može u potpunosti da radi offline. Ono što
PWA isporučuje:

- **Instalabilno** — validan web manifest + ikone, tako da browser-i nude *Install* / *Add to Home Screen*.
- **App-shell keširano** — service worker kešira statičke resurse (CSS, ikone, manifest) i prikazuje
  **offline stranicu** kada mreža padne, umesto browser greške.
- **Native osećaj** — standalone display, branded theme-color/status bar, app ikonica, iOS home-screen ikona.

Ne **ne pruža** offline interaktivnost — to bi zahtevalo Blazor WebAssembly (zaseban budući track).
Ne obećavajte offline korišćenje live funkcija.

## Komponente

| Komponenta | Gde |
|-------|-------|
| Manifest (dinamički, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anoniman) |
| Ikone (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback stranica | `Web/wwwroot/offline.html` |
| Registracija + iOS tagovi + install-prompt capture | `Web/Components/App.razor` |
| Route konstante | `Core.Constants.PwaRoutes` |

### Manifest

Služi se dinamički iz `BrandingOptions` tako da reseller-ovo ime proizvoda, boje i ikone prenose u
instaliranu aplikaciju: `name`/`short_name` iz `ProductName`, `description`, `theme_color` iz `AppBarColor`,
`background_color` iz `BackgroundColor`, `display: standalone`, i set ikonica (uključujući **maskable**
512 za čistu Android ikonu). Anoniman — install prompt mora raditi pre sign-in-a.

### Service worker

Samo app-shell. **Nikad** ne presreće Blazor krug (`/_blazor`), framework (`/_framework`), ili
SignalR hubove (`/hubs`) — oni su uvek mreža. Navigacije su network-first sa offline stranicom
kao fallback; statički resursi (`/css`, `/icons`, `/_content`) su cache-first sa background revalidate.
Registruje se sa `updateViaCache: 'none'` tako da worker update-ovi pouzdano stupaju na snagu. Keševi su verzionisani
(`cmind-shell-v<n>`) — bump na shell promene.

### iOS

iOS ignoriše manifest ikone/splash, tako da `App.razor` takođe emituje `apple-touch-icon` i
`apple-mobile-web-app-*` meta tag-ove. iOS nema `beforeinstallprompt`; korisnici instaliraju preko Safari *Add to
Home Screen*. `beforeinstallprompt` se hvata u `window.deferredInstallPrompt` na Chromium/Android
za custom install affordance.

## Testovi

- **E2E** — `E2ETests/PwaTests.cs`: manifest served sa `application/manifest+json`, ne-prazne ikone uklj.
  maskable, `display: standalone`, `apple-touch-icon` linkovan, i service worker se registruje +
  aktivira. `MobileLayoutTests` / `MobileDialogTests` pokrivaju mobile shell koji PWA instalira.
