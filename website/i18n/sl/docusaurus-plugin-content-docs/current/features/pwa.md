---
description: "cMind se namesti na telefon ali namizje kot izvorna aplikacija — ikona domačega zaslona, samostalno okno, splash in prijazna stran brez interneta. Je mobilno prvo in ..."
---

# Namestljiva aplikacija (PWA)

cMind se namesti na telefon ali namizje kot izvorna aplikacija — ikona domačega zaslona,
samostalno okno, splash in prijazna stran brez interneta. Je **mobilno prvo** in v celoti
odzivna; poglejte [ui-guidelines.md](../ui-guidelines.md).

## Kaj "namestljivo" pomeni tukaj — in iskrena meja

Blazor **Server** prikaže prek živega vezja SignalR, zato se aplikacija ne more v celoti tečeti
brez interneta. Kaj PWA dostavi:

- **Namestljiva** — veljavna spletna manifestacija + ikone, torej brskalniki ponudijo *Namestite* /
  *Dodaj na domačo zaslon*.
- **Aplikacija-lupina predpomnjena** — delavec na storitve predpomniti statične sredstva (CSS,
  ikone, manifestacija) in prikaže **stran brez interneta**, ko omrežje pade, namesto napake
  brskalnika.
- **Izvorna počutka** — samostojna zaslonska točka, poimenovana tema-barva/vrstica stanja, ikona
  aplikacije, ikona domačega zaslona iOS.

**Ne** zagotavlja interaktivnosti brez interneta — to bi zahtevalo Blazor WebAssembly (ločena
prihodnja sled). Ne obljubljajte uporabe brez interneta žive značilnosti.

## Kosi

| Kos | Kje |
|-------|-------|
| Manifestacija (dinamična, poimenovana) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonimno) |
| Ikone (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Delavec za storitve (lupina aplikacije) | `Web/wwwroot/service-worker.js` |
| Stran brez interneta padajoče nazaj | `Web/wwwroot/offline.html` |
| Registracija + iOS oznake + zajemanje poziva za namestitev | `Web/Components/App.razor` |
| Poti konstant | `Core.Constants.PwaRoutes` |

### Manifestacija

Služeno dinamično od `BrandingOptions`, torej ime, barve in ikone ponovno prodajalca nosijo v
namestljeno aplikacijo: `name`/`short_name` iz `ProductName`, `description`, `theme_color` iz
`AppBarColor`, `background_color` iz `BackgroundColor`, `display: standalone` in nabor ikone
(vključno z **maskable** 512 za čisto Android ikono). Anonimno — poziv za namestitev mora delati
pred prijavo.

### Delavec za storitve

Lupina aplikacije samo. **Nikoli** ne preseče Blazor vezja (`/_blazor`), ogrodja (`/_framework`)
ali vozlišč SignalR (`/hubs`) — ta so vedno omrežje. Navigacije so prvo omrežje s stranjo brez
interneta, kot padajočo nazaj; statična sredstva (`/css`, `/icons`, `/_content`) so prvo
predpomnjena s ponovnim preverjanjem v ozadju. Registrirana s `updateViaCache: 'none'`, zato se
posodobitve delavca zanesljivo uporabijo. Predpomnilniki so verzionsko preverjeni
(`cmind-shell-v<n>`) — bump pri spremembah lupine.

### iOS

iOS prezira manifestacije ikone/splash, zato `App.razor` tudi oddaja meta oznake `apple-touch-icon`
in `apple-mobile-web-app-*`. iOS nima `beforeinstallprompt`; uporabniki namestijo prek Safari
*Dodaj na domačo zaslon*. `beforeinstallprompt` se zajame v `window.deferredInstallPrompt` na
Chromium/Android za prilagojeno namestitev.

## Preskusi

- **E2E** — `E2ETests/PwaTests.cs`: manifestacija služena s `application/manifest+json`, ne-prazne
  ikone vključno maskabilno, `display: standalone`, `apple-touch-icon` povezano in delavec za
  storitve registrira + aktivira. `MobileLayoutTests` / `MobileDialogTests` pokrivajo mobilno
  lupino, ki je PWA namestila.
