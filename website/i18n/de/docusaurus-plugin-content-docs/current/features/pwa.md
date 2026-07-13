---
description: "cMind installiert auf Telefon oder Desktop wie eine native App — Home-Screen-Symbol, Fenster eigenständig, Splash und eine freundliche Offline-Seite. Es ist mobil-zuerst und…"
---

# Installierbare App (PWA)

cMind installiert auf Telefon oder Desktop wie eine native App — Home-Screen-Symbol, Fenster eigenständig, Splash und eine freundliche Offline-Seite. Es ist **mobil-zuerst** und vollständig reaktiv; siehe [ui-guidelines.md](../ui-guidelines.md).

## Was "installierbar" hier bedeutet — und die ehrliche Grenze

Blazor **Server** rendert durch eine Live SignalR Schaltung, daher kann die App nicht vollständig offline laufen. Was die PWA liefert:

- **Installierbar** — gültig Web-Manifest + Symbole, daher bieten Browser *Installieren* / *Zu Home-Screen hinzufügen*.
- **App-Shell zwischengespeichert** — der Service Worker zwischenspeichert statische Inhalte (CSS, Symbole, Manifest) und zeigt eine **Offline-Seite**, wenn das Netzwerk ausfällt, anstatt ein Browser-Fehler.
- **Natürliches Gefühl** — Fenster eigenständig, Marken-Design-Farbe/Status-Balken, App-Symbol, iOS Home-Screen-Symbol.

Es bietet **nicht** Offline-Interaktivität — das würde Blazor WebAssembly erfordern (ein separater zukünftiger Pfad). Versprechen Sie keine Offline-Nutzung von Live-Features nicht.

## Teile

| Stück | Wo |
|-------|-------|
| Manifest (dynamisch, Marke) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonym) |
| Symbole (192, 512, 512-Maskbar, Apple-Touch-180) | `Web/wwwroot/icons/` |
| Service Worker (App-Shell) | `Web/wwwroot/service-worker.js` |
| Offline Fallback-Seite | `Web/wwwroot/offline.html` |
| Registrierung + iOS Tags + Installations-Aufforderung Erfassung | `Web/Components/App.razor` |
| Route-Konstanten | `Core.Constants.PwaRoutes` |

### Manifest

Dynamisch vom `BrandingOptions` Dienst daher ein Reseller-Produkt-Name, Farben und Symbole tragen in die installierte App: `Name`/`Kurz-Name` von `Produkt-Name`, `Beschreibung`, `Design-Farbe` von `AppBar-Farbe`, `Hintergrund-Farbe` von `Hintergrund-Farbe`, `Anzeige: eigenständig`, und der Symbol-Satz (incl. ein **maskbar** 512 für ein sauberes Android-Symbol). Anonym — die Installations-Aufforderung muss vor der Anmeldung funktionieren.

### Service Worker

App-Shell nur. Es **nie** abfangen Blazor Schaltung (`/_blazor`), Rahmen (`/_framework`), oder SignalR Hubs (`/hubs`) — die sind immer Netzwerk. Navigationen sind Netzwerk-zuerst mit Offline-Seite als Fallback; statische Inhalte (`/css`, `/icons`, `/_content`) sind Cache-zuerst mit Hintergrund Revalidate. Registriert mit `updateViaCache: 'none'`, damit Worker-Aktualisierungen zuverlässig angewendet werden. Zwischenspeicher sind versioniert (`cmind-shell-v<n>`) — Bump auf Shell-Änderungen.

### iOS

iOS ignoriert Manifest-Symbole/Splash, daher `App.razor` auch emittiert `Apple-Touch-Symbol` und `Apple-Mobile-Web-App-*` Meta-Tags. iOS hat nein `beforeinstallprompt`; Benutzer installieren über Safari *Zu Home-Screen hinzufügen*. `beforeinstallprompt` wird erfasst in `Fenster.deferredInstallPrompt` auf Chromium/Android für eine benutzerdefinierte Installations-Leistung.

## Tests

- **E2E** — `E2ETests/PwaTests.cs`: Manifest serviert mit `Anwendung/Manifest+json`, nicht-leer Symbole incl. ein Maskbar, `Anzeige: eigenständig`, `Apple-Touch-Symbol` verlinkt, und der Service Worker registriert + aktiviert. `MobileLayoutTests` / `MobileDialogTests` Cover die Mobil-Shell die PWA installiert.
