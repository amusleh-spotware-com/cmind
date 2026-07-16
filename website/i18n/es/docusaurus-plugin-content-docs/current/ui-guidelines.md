---
description: "Vinculante para todos los elementos de UI nuevos o modificados en esta aplicación (páginas Blazor, diálogos, componentes). Esta es la fuente de verdad a la que se refiere CLAUDE.md. Si un…"
---

# Directrices de Diseño de UI — OBLIGATORIO

Vinculante para **todos** los elementos de UI nuevos o modificados en esta aplicación (páginas Blazor, diálogos, componentes).
Esta es la fuente de verdad a la que se refiere `CLAUDE.md`. Si una regla te bloquea, detente y pregunta — no
envíes UI que la viole. Basado en `plans/ui-overhaul.md`.

## 1. Mobile-first, siempre

- **Diseña para un teléfono de 360–430px primero**, luego mejora hacia arriba con media queries `min-width` / propiedades de puntos de quiebre de MudBlazor. Nunca desktop-first con sobrescrituras `max-width`.
- **Sin desplazamiento horizontal en ningún ancho 320–1920px.** Si el contenido es más ancho que la ventana gráfica, es un error.
- Objetivos táctiles ≥ **44px** (`var(--app-touch-target)`). Entradas de texto ≥ 16px de fuente (evita el zoom en foco de iOS).
- Respeta los muescas: usa `env(safe-area-inset-*)`; la ventana gráfica ya configura `viewport-fit=cover`.
- Honra `prefers-reduced-motion` — ninguna información esencial transmitida solo por animación.

## 2. Tokens de diseño — sin valores hardcodeados

- Todos los colores/radios/espaciados provienen de **tokens de diseño**: tema MudBlazor (`Web/Components/Theme.cs`) +
  las propiedades personalizadas de CSS emitidas por `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nunca hardcodees un color hexadecimal, radio o cadena de marca en un componente o regla CSS.** Lee un token.
  Los tokens fluyen desde `BrandingOptions` de white-label, por lo que la paleta de un revendedor debe llegar a tu UI sin esfuerzo.
- Nuevo valor que afecta la marca → añade un token + campo de marca; no lo insertes.

## 3. Diseño responsivo y datos

- **Las tablas colapsan a tarjetas en teléfonos.** Cada `MudTable` configura `Breakpoint="Breakpoint.Sm"` y cada
  `MudTd` tiene un `DataLabel`. Sin tabla ancha en crudo en móvil. (Plantilla: `Components/Pages/Nodes.razor`.)
- Cuadrículas: `MudItem xs="12" sm="6" md="4"` — ancho completo en teléfono, multi-columna hacia arriba.
- Formularios de una sola columna en móvil; objetivos de toque grandes; `inputmode`/`autocomplete` en entradas; inputmode numérico/decimal
  para dinero/porcentaje.
- **Controles apropiados para entrada estructurada — nunca un cuadro de texto en crudo para números o listas.** Recopila números,
  dinero, porcentajes, fechas, enumeraciones y cualquier dato de múltiples valores con el control correcto (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, una lista de filas de campos tipificados editable con agregar/eliminar, o una tabla), cada campo
  validado individualmente. Un único `MudTextField` de texto libre que el usuario debe escribir un blob separado por comas/espacios/saltos de línea en —
  que luego analizas — está **prohibido**: es propenso a errores, sin validar e inamistoso
  en un teléfono. **A nadie le gusta escribir un blob.** La entrada de múltiples valores es una lista editable de filas tipificadas (agregar /
  eliminar), o se carga desde datos de dominio existentes (por ejemplo, ejecuta la verificación directamente de una prueba retrospectiva completada
  en lugar de volver a ingresar sus números). `MudTextField` simple es solo para texto genuinamente libre — nombres, notas,
  búsqueda, descripciones.
- Proporciona estados de **carga, vacío y error** en cada lista/detalle — dimensionados para móvil.
- La **navegación de fondo móvil** (`Components/Layout/BottomNav.razor`) es la navegación principal del teléfono; el
  cajón agrupado es el menú completo. Añade destinos de alto tráfico allí; mantenlo ≤5 elementos.

## 4. Diálogos (crear/editar)

- Todas las acciones agregar/crear/editar/nueva usan un **diálogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nunca
  un formulario de página en línea. Los diálogos viven en `Web/Components/Dialogs/`, exponen `[Parameter]`s, devuelven un anidado
  `public sealed record …Result(...)`. Las acciones de fila de lista (iniciar/detener/eliminar) permanecen en línea como botones de icono.
- En teléfonos, los diálogos deben ser **pantalla completa / ancho completo** y conscientes del teclado.

## 5. Ayuda en línea — cada control

- Cada opción no obvia, selector, interruptor o acción obtiene un **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — pasar el ratón en escritorio, **tocar en móvil**. Obtén el texto de `docs/` para que
  la orientación permanezca sincronizada con el comportamiento; actualiza ambos en el mismo commit.

## 6. White-label

- El nombre del producto, logotipo, descripción, soporte/empresa, colores, favicon provienen de `BrandingOptions`.
  Referencialos (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nunca "cMind" literal o un
  color de marca. El manifiesto PWA, iconos, tema-color e imagen principal de inicio de sesión están todos marcados.

## 7. PWA

- La aplicación es instalable. Mantén el punto final del manifiesto (`/manifest.webmanifest`) marcado, iconos presentes
  (192/512/maskable + apple-touch), el service worker solo de app-shell (nunca tocando el
  circuito Blazor/`_framework`/hubs), y la página sin conexión funcionando. Nueva ruta estática → mantén el `scope` del manifiesto.
- Blazor Server necesita un circuito SignalR en vivo → **instalable + app-shell**, no completamente sin conexión. No
  prometas interactividad sin conexión.

## 8. Accesibilidad

- Etiquetas en entradas, `aria-*` en controles personalizados, foco visible, orden de foco lógico. Porque el tema es
  white-labelable, verifica **contraste** contra el tema activo, no una paleta fija.

## 9. E2E — ninguna UI envía sin pruebas (bloqueante)

Cada cambio de interfaz de usuario orientado al usuario envía E2E de Playwright en `tests/E2ETests`, conducido como un usuario real, **en emulación de
dispositivo móvil** más escritorio:

- Nueva ruta → añádela a `PageSmokeTests` **y** `MobileLayoutTests` (renderiza, navegación inferior, sin UI de error).
- Convierte una tabla/página → añade su ruta al conjunto de **sin desbordamiento** móvil.
- Nuevo flujo → un recorrido móvil realista (redonda crear/editar/guardar) **y** una ruta infeliz
  (entrada inválida, lista vacía, permiso denegado por rol).
- Nueva punta de ayuda → afirma que se abre al toque (patrón `HelpTipTests`).
- Usa `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulación de dispositivo).
- `dotnet test` verde antes de "hecho". WebKit emulado ≠ Safari móvil — la puerta de dispositivo real es un paso de lanzamiento separado.

## 10. Definición de hecho (UI)

- [ ] Mobile-first; sin desbordamiento horizontal 320–1920px; objetivos táctiles ≥44px.
- [ ] Solo tokens de diseño — cero colores/radios/cadenas de marca hardcodeados.
- [ ] Tablas → tarjetas en teléfono (`DataLabel` + `Breakpoint.Sm`); estados de carga/vacío/error presentes.
- [ ] La entrada estructurada usa controles validados apropiados (numérico/fecha/selector/lista de filas editable) — sin cuadro de texto en crudo que el usuario escribe un blob de número/valor delimitado.
- [ ] Crear/editar mediante diálogo; pantalla completa en móvil.
- [ ] Cada control tiene un `HelpTip` obtenido de documentos.
- [ ] White-label + PWA respetados.
- [ ] E2E móvil + escritorio añadido (humo, sin desbordamiento, recorrido, ruta infeliz); `dotnet test` verde.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` limpio en archivos tocados.
