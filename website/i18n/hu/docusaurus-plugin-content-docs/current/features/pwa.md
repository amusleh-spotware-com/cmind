---
description: "A cMind egy telefonra vagy asztali számítógépre telepítve, mint egy natív alkalmazás — kezdőképernyő ikon, önálló ablak, sikló és barátságos offline oldal. Ez mobil-első és teljes mértékben reagálékony; lásd a [ui-guidelines.md](../ui-guidelines.md) fájlt."
---

# Telepítendő alkalmazás (PWA)

A cMind egy telefonra vagy asztali számítógépre telepítve, mint egy natív alkalmazás — kezdőképernyő ikon, önálló ablak, sikló és barátságos offline oldal. Ez **mobil-első** és teljes mértékben reagálékony; lásd a [ui-guidelines.md](../ui-guidelines.md) fájlt.

## Mit jelent az "telepítendő" itt — és az őszinte korlát

A Blazor **szerver** egy élő SignalR áramkörön keresztül rendez, így az alkalmazás nem futhat teljesen offline. Mit a PWA szállít:

- **Telepítendő** — érvényes webes kiáltvány + ikonok, így a böngészők kínálnak *Telepítés* / *Hozzáadás a kezdőképernyőhöz*.
- **Alkalmazás-héj gyorsítótárazva** — a szolgáltatási munkavállaló a statikus eszközöket (CSS, ikonok, kiáltvány) gyorsítótárazza, és megjeleníti az **offline oldalt**, amikor a hálózat leesik, böngésző hiba helyett.
- **Natív érzés** — önálló megjelenítés, márkanem téma-szín/állapot sáv, alkalmazás ikon, iOS kezdőképernyő ikon.

Ez **nem** ad offline interaktivitást — ez Blazor WebAssembly (egy külön jövő nyomvonal) szükséglet. Ne ígérj offline használatát az élő funkciók.

## Darabok

| Darab | Hol |
|-------|-------|
| Kiáltvány (dinamikus, márkanem) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (névtelen) |
| Ikonok (192, 512, 512-maszkolható, apple-touch-180) | `Web/wwwroot/icons/` |
| Szolgáltatási munkavállaló (alkalmazás-héj) | `Web/wwwroot/service-worker.js` |
| Offline visszajelzési oldal | `Web/wwwroot/offline.html` |
| Regisztráció + iOS-jelzők + telepítési felszólítás rögzítése | `Web/Components/App.razor` |
| Útvonal-konstansok | `Core.Constants.PwaRoutes` |

### Kiáltvány

Dinamikusan kiszolgálva az `BrandingOptions`-ből, így az átjáró termékneve, színei és ikonai az telepített alkalmazásba kerülnek: `name`/`short_name` a `ProductName`-ből, `description`, `theme_color` az `AppBarColor`-ből, `background_color` a `BackgroundColor`-ből, `display: standalone` és az ikonsík (incl. egy **maszkolható** 512 egy tiszta Android-ikonhoz). Névtelen — a telepítési felszólítás az bejelentkezés előtt működnie kell.

### Szolgáltatási munkavállaló

Alkalmazás-héj csak. Ez **soha nem** szakítja meg az Blazor áramkört (`/_blazor`), keretrendszert (`/_framework`) vagy SignalR hub-okat (`/hubs`) — ezek mindig hálózat. A navigációk hálózat-első az offline oldal visszajelzésével; a statikus eszközök (`/css`, `/icons`, `/_content`) gyorsítótár-első a háttér-revalidálásával. Regisztrálva az `updateViaCache: 'none'`-vel, így a munkavállaló-frissítések megbízhatóan alkalmazódnak. A gyorsítótárak verziót kapnak (`cmind-shell-v<n>`) — szívverés a héj módosítások során.

### iOS

Az iOS figyelmen kívül hagyja a kiáltvány-ikonokat/sikló-kat, így az `App.razor` az `apple-touch-icon` és az `apple-mobile-web-app-*` meta-jelzőket is kibocsát. Az iOS nincs `beforeinstallprompt`; a felhasználók az Safari *Add to Home Screen* segítségével telepítenek. A `beforeinstallprompt` az `window.deferredInstallPrompt`-ba van rögzítve a Chromium/Android-on egy egyéni telepítési előforduláshoz.

## Tesztek

- **E2E** — `E2ETests/PwaTests.cs`: kiáltvány kiszolgálva az `application/manifest+json` értékkel, nem üres ikonok incl. a maszkolható, `display: standalone`, `apple-touch-icon` csatornára, és a szolgáltatási munkavállaló regisztrálódik + aktiválódik. A `MobileLayoutTests` / `MobileDialogTests` a mobilhéjt fedik le, amelyet a PWA telepít.
