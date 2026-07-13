---
description: "cMind s'installe sur un téléphone ou ordinateur de bureau comme une app native — icône de l'écran d'accueil, fenêtre autonome, écran de démarrage, et une page hors ligne conviviale. Elle est mobile-first et…"
---

# App installable (PWA)

cMind s'installe sur un téléphone ou ordinateur de bureau comme une app native — icône de l'écran d'accueil, fenêtre autonome, écran de démarrage,
et une page hors ligne conviviale. Elle est **mobile-first** et entièrement responsive ; voir
[ui-guidelines.md](../ui-guidelines.md).

## Ce que « installable » signifie ici — et la limite honnête

Blazor **Server** rend via un circuit SignalR en direct, donc l'app ne peut pas s'exécuter entièrement hors ligne. Ce que le
PWA livre :

- **Installable** — manifest web valide + icônes, donc les navigateurs offrent l'option *Install* / *Add to Home Screen*.
- **App-shell en cache** — le service worker met en cache les ressources statiques (CSS, icônes, manifest) et affiche une
  **page hors ligne** quand le réseau tombe, au lieu d'une erreur de navigateur.
- **Sensation native** — affichage autonome, thème-couleur/barre d'état marqué, icône d'app, icône d'écran d'accueil iOS.

Elle ne fournit **pas** l'interactivité hors ligne — cela nécessiterait Blazor WebAssembly (une piste future séparée). Ne promettez pas
l'utilisation hors ligne des features en direct.

## Pièces

| Pièce | Où |
|-------|-------|
| Manifest (dynamique, marqué) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonyme) |
| Icônes (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Page de secours hors ligne | `Web/wwwroot/offline.html` |
| Enregistrement + tags iOS + capture de message d'installation | `Web/Components/App.razor` |
| Constantes d'itinéraires | `Core.Constants.PwaRoutes` |

### Manifest

Servi dynamiquement à partir de `BrandingOptions` de sorte qu'un revendeur's produit nom, couleurs et icônes portent dans l'app
installée : `name`/`short_name` de `ProductName`, `description`, `theme_color` de `AppBarColor`,
`background_color` de `BackgroundColor`, `display: standalone`, et l'ensemble d'icônes (incl. un **maskable**
512 pour une icône Android propre). Anonyme — le message d'installation doit fonctionner avant la connexion.

### Service worker

App-shell uniquement. Il ne ** jamais n'intercept** le circuit Blazor (`/_blazor`), le framework (`/_framework`), ou
les hubs SignalR (`/hubs`) — ceux-ci sont toujours le réseau. Les navigations sont network-first avec la page hors ligne
en secours ; les ressources statiques (`/css`, `/icons`, `/_content`) sont cache-first avec revalidation en arrière-plan.
Enregistré avec `updateViaCache: 'none'` de sorte que les mises à jour du worker s'appliquent de manière fiable. Les caches
sont versionnés (`cmind-shell-v<n>`) — bump sur les changements de shell.

### iOS

iOS ignore les icônes manifest/splash, donc `App.razor` émet aussi les tags meta `apple-touch-icon` et
`apple-mobile-web-app-*`. iOS n'a pas `beforeinstallprompt` ; les utilisateurs installent via *Add to
Home Screen* de Safari. `beforeinstallprompt` est capturé dans `window.deferredInstallPrompt` sur Chromium/Android
pour une affordance d'installation personnalisée.

## Tests

- **E2E** — `E2ETests/PwaTests.cs` : manifest servi avec `application/manifest+json`, icônes non-vides incl.
  une maskable, `display: standalone`, `apple-touch-icon` lié, et le service worker enregistre +
  s'active. `MobileLayoutTests` / `MobileDialogTests` couvrent le shell mobile que le PWA installe.
