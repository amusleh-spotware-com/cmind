---
title: Telepitheto alkalmazas (PWA)
description: "A cMind telepítheto mint PWA - mobil-elsofokus, offline shell, es hozzaadas a kezdokepernyohoz. Minden kepessege mukodik a webes feluleten."
---

# Telepitheto alkalmazas (PWA)

A cMind telepíthető mint **Progressive Web App (PWA)** - mobil-elősfókusz, offline shell, és hozzáadás a kezdőképernyőhöz. Minden képessége működik a webes felületen.

## Mi a PWA

A PWA egy webalkalmazás, amely natív app-szerű élményt nyújt:
- **Telepíthető** - hozzáadás a kezdőképernyőhöz
- **Offline** - az alkalmazás részlegesen offline működik
- **Gyors** - service worker gyorsítja a betöltést
- **Reszponzív** - mobil, tablet, desktop egyaránt

## Telepites

### Android (Chrome)

1. Nyisd meg a cMind-et Chrome-ban.
2. Kattints a **Telepítés** bannerre (alsó sáv) vagy a menü → "Hozzáadás a kezdőképernyőhöz".
3. A cMind ikon megjelenik a kezdőképernyőn.

### iOS (Safari)

1. Nyisd meg a cMind-et Safari-ban.
2. Kattints a **Megosztás** gombra → "Hozzáadás a kezdőképernyőhöz".
3. A cMind ikon megjelenik a kezdőképernyőn.

### Desktop (Chrome/Edge)

1. Kattints a telepítés ikonra a címsorban (vagy a menü → "Telepíthető alkalmazásként telepítés").
2. A cMind külön ablakban nyílik meg, asztali parancsikonnal.

## Offline kepessegek

A PWA **offline shell**-t biztosít - a UI betöltődik, de az adatok nem frissülnek:

| Működik offline | Nem működik offline |
|----------------|-------------------|
| Navigáció | Élő adatok |
| Beállítások megtekintése | Kereskedés |
| Cache-elt stratégiák | Élő grafikonok |
| Helyi beállítások | SignalR frissítések |

## Service Worker

A service worker gyorsítja a betöltést és lehetővé teszi az offline működést. A PWA a **Cache First** stratégiát használja statikus asszettekhez (JS, CSS, képek) és a **Network First** stratégiát API hívásokhoz.

## Reszponzivitats

A cMind mobil-elősfókuszos - 360px szélességre tervezve. A következő mérettartományok támogatottak:

| Tartomány | Elrendezés |
|-----------|-----------|
| 320-360px | Egyoszlopos mobil |
| 360-640px | Táblagép (mobil elrendezés) |
| 640-1024px | Tablet asztali elemekkel |
| 1024px+ | Teljes asztali elrendezés |

## Kapcsolodo

- **[UI tervezési irányelvek](../ui-guidelines.md)**
- **[White-label](./white-label.md)**
