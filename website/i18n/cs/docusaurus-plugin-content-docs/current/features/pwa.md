---
description: "cMind se instaluje do telefonu nebo desktopu jako nativní aplikace — domovská obrazovka ikona, standalone okno, splash, a přátelská offline stránka. Je to mobile-first a…"
---

# Instalovatelná aplikace (PWA)

cMind se instaluje do telefonu nebo desktopu jako nativní aplikace — domovská obrazovka ikona, standalone okno, splash, a přátelská offline stránka. Je to **mobile-first** a plně responsivní; viz [ui-guidelines.md](../ui-guidelines.md).

## Co "instalovatelné" znamená zde — a čestný limit

Blazor **Server** renderuje přes live SignalR obvod, takže aplikace nemůže běžet úplně offline. Co PWA dodává:

- **Instalovatelné** — platný web manifest + ikony, takže prohlížeče nabízejí *Install* / *Přidat na Home Screen*.
- **App-shell cached** — service worker ukládá statické assety (CSS, ikony, manifest) a ukazuje **offline stránka** když síť vypadne, místo chyby prohlížeče.
- **Nativní pocit** — standalone displej, značená theme-color/status bar, app ikona, iOS home-screen ikona.

To **ne** poskytuje offline interaktivitu — to by vyžadovalo Blazor WebAssembly (samostatná budoucí stopa). Neslibujte offline použití live vlastností.

## Kousky

| Kousek | Kde |
|-------|-------|
| Manifest (dynamický, značený) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymní) |
| Ikony (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback stránka | `Web/wwwroot/offline.html` |
| Registrace + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route konstanty | `Core.Constants.PwaRoutes` |

### Manifest

Servírován dynamicky z `BrandingOptions` takže rezeller's product jméno, barvy a ikony nesen do instalované aplikace: `name`/`short_name` z `ProductName`, `description`, `theme_color` z `AppBarColor`, `background_color` z `BackgroundColor`, `display: standalone`, a sada ikon (incl. **maskable** 512 pro čistou Android ikonu). Anonymní — install prompt musí pracovat před sign-in.

### Service worker

App-shell pouze. To **nikdy** zachycuje Blazor obvod (`/_blazor`), framework (`/_framework`), nebo SignalR huby (`/hubs`) — ty jsou vždy síť. Navigace jsou network-first s offline stránkou jako fallback; statické assety (`/css`, `/icons`, `/_content`) jsou cache-first s background revalidate. Registrován s `updateViaCache: 'none'` takže worker aktualizace aplikuje spolehlivě. Caches jsou verzované (`cmind-shell-v<n>`) — bump na shell změny.

### iOS

iOS ignoruje manifest ikony/splash, takže `App.razor` také emituje `apple-touch-icon` a `apple-mobile-web-app-*` meta tags. iOS nemá `beforeinstallprompt`; uživatelé instalují přes Safari's *Add to Home Screen*. `beforeinstallprompt` je zachycen do `window.deferredInstallPrompt` na Chromium/Android pro vlastní install affordance.

## Testy

- **E2E** — `E2ETests/PwaTests.cs`: manifest servírován s `application/manifest+json`, non-empty ikony incl. maskable jeden, `display: standalone`, `apple-touch-icon` linked, a service worker zaregistruje + aktivuje. `MobileLayoutTests` / `MobileDialogTests` kryt mobilní shell PWA instaluje.
