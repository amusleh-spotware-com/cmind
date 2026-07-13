---
description: "cMind si installa su un telefono o desktop come un'app nativa — icona della schermata iniziale, finestra autonoma, splash, e una pagina offline amichevole. È mobile-first e…"
---

# App installabile (PWA)

cMind si installa su un telefono o desktop come un'app nativa — icona della schermata iniziale, finestra autonoma, splash,
e una pagina offline amichevole. È **mobile-first** e completamente responsive; vedi
[ui-guidelines.md](../ui-guidelines.md).

## Cosa significa "installabile" qui — e il limite onesto

Blazor **Server** renderizza attraverso un circuito SignalR live, quindi l'app non può funzionare completamente offline. Quello che
il PWA consegna:

- **Installabile** — manifesto web valido + icone, quindi i browser offrono *Installa* / *Aggiungi alla schermata iniziale*.
- **App-shell cachata** — il service worker mette in cache le risorse statiche (CSS, icone, manifesto) e mostra una
  **pagina offline** quando la rete cade, invece di un errore del browser.
- **Sensazione nativa** — display autonomo, theme-color/barra di stato branded, icona app, icona schermata iniziale iOS.

**Non** fornisce interattività offline — che richiederebbe Blazor WebAssembly (una traccia futura separata). Non promettere l'uso offline di funzionalità live.

## Pezzi

| Pezzo | Dove |
|-------|-------|
| Manifesto (dinamico, branded) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonimo) |
| Icone (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Pagina fallback offline | `Web/wwwroot/offline.html` |
| Registrazione + tag iOS + cattura install-prompt | `Web/Components/App.razor` |
| Costanti di rotta | `Core.Constants.PwaRoutes` |

### Manifesto

Servito dinamicamente da `BrandingOptions` così il nome del prodotto del rivenditore, i colori e le icone portano nell'app
installata: `name`/`short_name` da `ProductName`, `description`, `theme_color` da `AppBarColor`,
`background_color` da `BackgroundColor`, `display: standalone`, e il set di icone (incl. un **maskable**
512 per un'icona Android pulita). Anonimo — il prompt di installazione deve funzionare prima dell'accesso.

### Service worker

Solo app-shell. **Non** intercetta mai il circuito Blazor (`/_blazor`), framework (`/_framework`), o
hub SignalR (`/hubs`) — quelli sono sempre rete. Le navigazioni sono network-first con la pagina offline
come fallback; gli asset statici (`/css`, `/icons`, `/_content`) sono cache-first con revalidazione di sfondo.
Registrato con `updateViaCache: 'none'` quindi gli aggiornamenti del worker si applicano in modo affidabile. Le cache sono versionate
(`cmind-shell-v<n>`) — bump sui cambiamenti della shell.

### iOS

iOS ignora le icone e il splash del manifesto, quindi `App.razor` emette anche tag meta `apple-touch-icon` e
`apple-mobile-web-app-*`. iOS non ha `beforeinstallprompt`; gli utenti installano tramite *Aggiungi alla
schermata iniziale* di Safari. `beforeinstallprompt` è catturato in `window.deferredInstallPrompt` su Chromium/Android
per un'affordance di installazione personalizzata.

## Test

- **E2E** — `E2ETests/PwaTests.cs`: manifesto servito con `application/manifest+json`, icone non vuote incl.
  una maskable, `display: standalone`, `apple-touch-icon` collegato, e il service worker si registra +
  si attiva. `MobileLayoutTests` / `MobileDialogTests` coprono la shell mobile che il PWA installa.
