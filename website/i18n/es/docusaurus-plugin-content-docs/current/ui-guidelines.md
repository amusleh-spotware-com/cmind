---
description: "Vinculación para cada pieza de interfaz de usuario nueva o modificada en esta aplicación (páginas Blazor, diálogos, componentes). Esta es la fuente de verdad referenciada por CLAUDE.md. Si un…"
---

# Directrices de diseño de interfaz de usuario — OBLIGATORIO

Vinculación para **cada** pieza de interfaz de usuario nueva o modificada en esta aplicación (páginas Blazor, diálogos, componentes).
Esta es la fuente de verdad referenciada por `CLAUDE.md`. Si una regla te bloquea, detente y pregunta — no
envíes interfaz de usuario que la viole. Enraizado en `plans/ui-overhaul.md`.

## 1. Móvil primero, siempre

- **Autor para un teléfono 360–430px primero**, luego mejora hacia arriba con consultas de medios `min-width` / propiedades
  punto de quiebre MudBlazor. Nunca primero de escritorio con anulaciones `max-width`.
- **Sin desplazamiento horizontal a ningún ancho 320–1920px.** Si el contenido es más ancho que el viewport, es un error.
- Objetivos de toque ≥ **44px** (`var(--app-touch-target)`). Entradas de texto ≥ 16px de fuente (impide zoom on focus de iOS).
- Respetar muescas: usa `env(safe-area-inset-*)`; el viewport ya establece `viewport-fit=cover`.
- Honra `prefers-reduced-motion` — ninguna información esencial transmitida solo por animación.

## 2. Tokens de diseño — sin valores codificados

- Todo color/radio/espaciado proviene de **tokens de diseño**: tema MudBlazor (`Web/Components/Theme.cs`) +
  las propiedades personalizadas CSS emitidas por `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nunca codifiques un color hex, radio o cadena de marca en un componente o regla CSS.** Lee un token.
  Los tokens fluyen desde `BrandingOptions` de etiqueta blanca, así que la paleta de un revendedor debe llegar a tu interfaz de usuario de forma libre.
- Nuevo valor que afecte la marca → agrega un token + campo de marca; no lo insertes en línea.

## 3. Diseño y datos receptivos

- **Las tablas se contraen a tarjetas en teléfonos.** Cada `MudTable` establece `Breakpoint="Breakpoint.Sm"` y cada
  `MudTd` tiene una `DataLabel`. Sin tabla ancha bruta en dispositivo móvil. (Plantilla: `Components/Pages/Nodes.razor`.)
- Cuadrículas: `MudItem xs="12" sm="6" md="4"` — ancho completo en teléfono, múltiples columnas hacia arriba.
- Formas de una sola columna en dispositivo móvil; grandes objetivos de toque; `inputmode`/`autocomplete` en entradas; inputmode numérico/decimal
  para dinero/porcentaje.
- Proporciona estados de **carga, vacío y error** en cada lista/detalle — dimensionados para dispositivo móvil.
- La **navegación inferior** móvil (`Components/Layout/BottomNav.razor`) es la navegación principal del teléfono; el
  cajón agrupado es el menú completo. Agrega destinos de alto tráfico allí; manténlo ≤5 elementos.

## 4. Diálogos (crear/editar)

- Todas las acciones agregar/crear/editar/nuevo utilizan un **diálogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nunca
  una forma de página en línea. Los diálogos viven en `Web/Components/Dialogs/`, exponen `[Parameter]`s, devuelven un anidado
  `public sealed record …Result(...)`. Las acciones de fila de lista (inicio/parada/eliminación) permanecen en línea como botones de icono.
- En teléfonos, los diálogos deben ser **pantalla completa / ancho completo** y conocedores del teclado.

## 5. Ayuda en línea — cada control

- Cada opción no obvia, seleccionar, cambiar o acción obtiene un **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — pasar en escritorio, **toque en dispositivo móvil**. Obtén el texto de `docs/` para que
  la orientación se mantenga sincronizada con el comportamiento; actualiza ambas en el mismo commit.

## 6. Etiqueta blanca

- El nombre del producto, logo, descripción, soporte/empresa, colores, favicon todos provienen de `BrandingOptions`.
  Referéncialos (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nunca literal "cMind" o un
  color de marca. El manifiesto PWA, iconos, tema-color y héroe de inicio de sesión son todos marcados.

## 7. PWA

- La aplicación es instalable. Mantén el punto final del manifiesto (`/manifest.webmanifest`) marcado, iconos presentes
  (192/512/maskable + apple-touch), el servicio de trabajador solo app-shell (nunca tocando el circuito Blazor/`_framework`/hubs), 
  y la página sin conexión funcionando. Nueva ruta estática → mantén `scope` del manifiesto.
- Blazor Server necesita un circuito SignalR en vivo → **instalable + app-shell**, no completamente sin conexión. No
  prometas interactividad sin conexión.

## 8. Accesibilidad

- Etiquetas en entradas, `aria-*` en controles personalizados, enfoque visible, orden de enfoque lógico. Porque el tema es
  blanco-etiquetable, verifica **contraste** contra el tema activo, no una paleta fija.

## 9. E2E — ninguna interfaz de usuario se envía sin prueba (bloqueante)

Cada cambio orientado al usuario envía Playwright E2E en `tests/E2ETests`, impulsado como un usuario real, **en emulación de dispositivo móvil** 
más escritorio:

- Nueva ruta → agrégala a `PageSmokeTests` **y** `MobileLayoutTests` (renderiza, nav inferior, sin interfaz de usuario de error).
- Convertir una tabla/página → agregar su ruta al conjunto móvil **sin desbordamiento**.
- Nuevo flujo → un viaje móvil realista (creación/edición/guardar viaje redondo) **y** una ruta infeliz
  (entrada inválida, lista vacía, permiso denegado por rol).
- Nueva punta de ayuda → afirma que se abre al tocar (patrón `HelpTipTests`).
- Usa `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulación de dispositivo).
- `dotnet test` verde antes de "hecho". WebKit emulado ≠ Safari móvil — la puerta de dispositivo real es un paso de lanzamiento separado.

## 10. Definición de hecho (interfaz de usuario)

- [ ] Móvil primero; sin desbordamiento horizontal 320–1920px; objetivos de toque ≥44px.
- [ ] Solo tokens de diseño — cero colores/radios/cadenas de marca codificadas.
- [ ] Tablas → tarjetas en teléfono (`DataLabel` + `Breakpoint.Sm`); estados de carga/vacío/error presentes.
- [ ] Crear/editar vía diálogo; pantalla completa en dispositivo móvil.
- [ ] Cada control tiene un `HelpTip` obtenido de documentos.
- [ ] Etiqueta blanca + PWA respetadas.
- [ ] E2E móvil + escritorio agregado (humo, sin desbordamiento, viaje, ruta infeliz); `dotnet test` verde.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` limpio en archivos modificados.
