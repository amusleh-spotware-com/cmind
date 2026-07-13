---
description: "cMind se instala en un teléfono o escritorio como una aplicación nativa — icono de pantalla de inicio, ventana independiente, pantalla de bienvenida, y página amigable sin conexión. Es optimizada para dispositivos móviles y…"
---

# Aplicación instalable (PWA)

cMind se instala en un teléfono o escritorio como una aplicación nativa — icono de pantalla de inicio, ventana independiente, pantalla de bienvenida,
y página amigable sin conexión. Es **optimizada para dispositivos móviles** y completamente responsiva; véase
[ui-guidelines.md](../ui-guidelines.md).

## Qué significa "instalable" aquí — y el límite honesto

Blazor **Server** renderiza a través de un circuito SignalR en vivo, por lo que la aplicación no puede ejecutarse completamente sin conexión. Lo que
el PWA entrega:

- **Instalable** — manifest web válido + iconos, por lo que navegadores ofrecen *Instalar* / *Agregar a pantalla de inicio*.
- **Caparazón de aplicación en caché** — el trabajador de servicio cachés activos estáticos (CSS, iconos, manifest) y muestra una
  **página sin conexión** cuando la red cae, en lugar de un error de navegador.
- **Sensación nativa** — exhibición independiente, color de tema marcado/barra de estado, icono de aplicación, icono de pantalla de inicio iOS.

**No** proporciona interactividad sin conexión — eso requeriría Blazor WebAssembly (una pista futura separada). No prometas
uso sin conexión de características en vivo.

## Piezas

| Pieza | Dónde |
|-------|-------|
| Manifest (dinámico, marcado) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anónimo) |
| Iconos (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Trabajador de servicio (caparazón de aplicación) | `Web/wwwroot/service-worker.js` |
| Página de recurso sin conexión | `Web/wwwroot/offline.html` |
| Registro + etiquetas iOS + captura de solicitud de instalación | `Web/Components/App.razor` |
| Constantes de ruta | `Core.Constants.PwaRoutes` |

### Manifest

Servido dinámicamente desde `BrandingOptions` por lo que nombre del producto revendedor, colores e iconos se llevan a la
aplicación instalada: `name`/`short_name` desde `ProductName`, `description`, `theme_color` desde `AppBarColor`,
`background_color` desde `BackgroundColor`, `display: standalone`, y conjunto de iconos (incl. un **maskable**
512 para icono Android limpio). Anónimo — la solicitud de instalación debe funcionar antes de inicio de sesión.

### Trabajador de servicio

Solo caparazón de aplicación. **Nunca** intercepta circuito Blazor (`/_blazor`), framework (`/_framework`), o
hubs SignalR (`/hubs`) — esos son siempre red. Navegaciones son red-primero con página sin conexión
como alternativa; activos estáticos (`/css`, `/icons`, `/_content`) son caché-primero con revalidación de fondo.
Registrado con `updateViaCache: 'none'` por lo que las actualizaciones de trabajador se aplican de forma confiable. Cachés son versionados
(`cmind-shell-v<n>`) — bump en cambios de caparazón.

### iOS

iOS ignora iconos/pantalla de bienvenida de manifest, por lo que `App.razor` también emite `apple-touch-icon` y
etiquetas meta `apple-mobile-web-app-*`. iOS no tiene `beforeinstallprompt`; usuarios instalan vía *Agregar a
pantalla de inicio* de Safari. `beforeinstallprompt` se captura en `window.deferredInstallPrompt` en Chromium/Android
para una affordance de instalación personalizada.

## Pruebas

- **E2E** — `E2ETests/PwaTests.cs`: manifest servido con `application/manifest+json`, iconos no vacíos incl.
  uno maskable, `display: standalone`, `apple-touch-icon` vinculado, y trabajador de servicio se registra +
  activa. `MobileLayoutTests` / `MobileDialogTests` cubren el caparazón móvil que el PWA instala.
