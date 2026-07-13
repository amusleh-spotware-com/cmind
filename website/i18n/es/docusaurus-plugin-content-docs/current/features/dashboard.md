---
title: Panel de control
description: El panel de control de cMind — un centro de comando en vivo, optimizado para dispositivos móviles, para tus ejecuciones de cBot, backtests, recursos y clúster de nodos.
---

# Panel de control 📊

Lo primero que ves cuando inicia sesión, y honestamente la página que dejarás abierta todo el día. La
página de aterrizaje (`/`, `Components/Pages/Index.razor`) es un **centro de comando en vivo, optimizado para dispositivos móviles** para la actividad del usuario
inició sesión en ejecuciones de cBot, backtests, recursos y (para administradores) el clúster de nodos. Se actualiza automáticamente, se ve bien en un teléfono, y nunca te hace presionar F5.

## Qué muestra

De arriba a abajo, ordenado por prioridad para un teléfono (cada bloque es un elemento de pila de ancho completo en dispositivos móviles, una
cuadrícula responsiva en tablet/escritorio):

1. **Encabezado** — título, un indicador en vivo (un punto pulsante real; estático bajo `prefers-reduced-motion`), el
   tiempo de última actualización, y un **selector de período** (`1H · 24H · 7D · 30D`) que controla los KPIs y gráfico.
2. **KPIs héroe** — cuatro tarjetas fáciles de leer, cada una con un número grande + un minigrá SVG en línea, y (donde
   es significativo) un **delta vs el período anterior**:
   - **Activo ahora** — ejecuciones + backtests que se están iniciando/ejecutando actualmente.
   - **Tasa de éxito** — completados ÷ (completados + fallidos) durante el período; delta en puntos porcentuales.
   - **Completados** — ejecuciones/backtests terminados este período; delta vs período anterior.
   - **Fallidos** — fallos este período; delta (menos es mejor, por lo que una caída muestra verde).
3. **Gráfico de actividad** — una línea de tiempo de área ApexCharts de iniciados / completados / fallidos por intervalo de tiempo.
4. **Anillo de estado de instancia** — una dona de en ejecución / backtests / pendiente / completado / fallido, total en
   el centro.
5. **Backtests** — un resumen de tres tiles (en ejecución / completados / fallidos), clic para ir a `/backtest`.
6. **Copia de trading** — tus perfiles de copia de trading con un punto de estado en vivo, contador de destinos, y un distintivo **En vivo**
   en perfiles en ejecución; clic para ir a `/copy-trading`.
7. **Agentes de IA** — tus agentes de trading impulsados por personajes con estado de ejecución (arquetipo · estado) y hora de la última acción;
   clic para ir a `/agent-studio`.
8. **Feed de actividad en vivo** — los 20 eventos más recientes (más nuevos primero) con un punto de color de estado y
   marca de tiempo relativa.
9. **Salud del clúster** (solo administradores) — nodos activos vs totales y un indicador de capacidad en uso.
10. **Tiles de recursos** — cBots, cuentas de trading, IDs de cTrader, claves de MCP (clic para ir a sus páginas).

## Personaliza tu panel de control

Cada bloque anterior es un **widget que controlas**. Presiona **Personalizar** (arriba a la derecha del encabezado) para abrir un
diálogo donde **muestras/ocultas** cualquier widget y **reordenas** con flechas arriba/abajo. **Restablecer a predeterminado**
restaura el orden del catálogo. Tu elección se **persiste del lado del servidor por usuario**, por lo que te sigue en todos
navegadores y dispositivos — no solo esta pestaña.

- Los widgets controlados por características y solo para administradores (Copia de trading, Agentes de IA, Salud del clúster) solo aparecen en el
  diálogo cuando tu implementación/rol puede usarlos.
- El catálogo de widgets es una única fuente de verdad en `Core/Dashboard/DashboardWidgets.cs`; la presentación
  (etiqueta + icono + disponibilidad) vive en `Components/Dashboard/DashboardWidgetMeta.cs`.

## Cómo se mantiene en vivo

La página sondea `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` cada 10 segundos y vuelve a renderizar los
widgets en su lugar — sin recarga manual. Una falla de obtención transitoria se ignora y se reintentar en el siguiente ciclo;
el bucle se detiene limpiamente al desechar. La primera carga muestra un esqueleto; una falla persistente muestra una tarjeta de error con **Reintentar**; un usuario sin datos ve KPIs en cero y copia de estado vacío.

## Backend

- `Endpoints/DashboardEndpoints.cs` asigna `/overview` (y mantiene el `/stats` escalar anterior). Es
  por usuario y controlado por administrador a través de `ICurrentUser`; el reloj proviene de `TimeProvider`. También asigna
  `GET/PUT /api/dashboard/layout` — el diseño de widget del usuario, cargado al iniciar la página y guardado desde el
  diálogo Personalizar.
- **Persistencia de diseño** es el agregado `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): un panel
  por usuario (único en `UserId`), siendo propietario de una lista ordenada de configuraciones de widget (visible + orden) almacenadas como una
  columna `jsonb`. La lista ordenada solo se muta a través de `Apply` / `Reset`, que valida cada
  clave contra el catálogo `DashboardWidgets` y mantiene la colección completa y deduplicada. Las claves desconocidas
  se rechazan con una `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` crea el modelo de lectura compuesto `DashboardOverview`: una instantánea de estado en todo momento
  (conteos agrupados), un conjunto ventanado de instancias materializadas una sola vez, y conteos de recursos/nodos.
  El estado de la instancia y las marcas de tiempo terminales viven en subtipos TPH (no columnas), por lo que las filas se leen en memoria
  a través de los ayudantes compartidos `InstanceEndpoints.GetStartedAt/GetStoppedAt`. Tiempo de evento =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` contiene los DTOs, el plan período→(ventana, conteo de intervalos), y
  `DashboardMath` — puro, determinístico agrupamiento + matemáticas de KPI/delta (sin I/O, `now` se pasa).

Los deltas de KPI comparan la ventana actual contra la inmediatamente anterior (la consulta obtiene una ventana doble
para esto). No hay un **feed de P&L de cuenta en vivo** — la plataforma solo tiene equidad para backtests y
seguimiento de empresas prop — por lo que el panel de control es deliberadamente *operativo* (actividad, rendimiento, tasa de éxito),
no un ticker de saldo de correduría.

## Diseño y fichas

Todo color proviene de fichas de diseño (`var(--app-success|-warning|-error|-info|-primary|-text*)`), por lo que una
paleta etiquetada en blanco fluye gratis — incluyendo el gráfico, cuyas colores de series se leen desde las
fichas resueltas en tiempo de ejecución a través de `window.appReadTokens` (SVG no puede consumir variables CSS directamente). Sin
números hexadecimales codificados en ninguna parte del panel de control. Véase [../ui-guidelines.md](../ui-guidelines.md).

## El enlace "Impulsado por cMind"

El panel de control muestra un pequeño y elegante enlace **"Impulsado por cMind"** que apunta a este sitio de documentación.
Se **muestra de forma predeterminada** — estamos orgullosos del proyecto y ayuda a otros operadores a encontrarlo — pero
es completamente tu decisión. Los revendedores que ejecutan una instancia completamente etiquetada en blanco establecen
`App:Branding:ShowSiteLink` a `false` y desaparece. Véase
[Marca de etiqueta blanca](./white-label.md#enlace-powered-by).

## Pruebas

- **Estilo unitario** (`tests/IntegrationTests/DashboardMathTests.cs`) — agrupamiento, tasa de éxito,
  deltas de período anterior, análisis de período, vacío/límites (evento en `now`, guardia de división por cero).
- **Unidad** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — el agregado `UserDashboard`: semilla predeterminada,
  aplicar orden/visibilidad, anexo omitido, colapso duplicado, rechazo de clave desconocida, reinicio.
- **Integración** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — el modelo
  de lectura contra Postgres real (estado/KPIs/actividad/recursos, salud del nodo de administrador, ruta de usuario vacía), las
  nuevas secciones de backtests/perfiles de copia/agentes, y un **viaje de ida y vuelta** de diseño (guardar diseño personalizado → recargar →
  orden + visibilidad persistentes).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — escritorio + móvil: tarjetas de KPI,
  gráfico, anillo y feed se renderizan; el selector de período cambia el período activo y recarga; un KPI
  profundiza en `/run`; **ocultar un widget persiste en toda recarga**, **Reiniciar** lo trae de vuelta, y
  el diálogo Personalizar funciona en un teléfono sin desbordamiento horizontal. `/` también está en `PageSmokeTests`,
  `MobileLayoutTests` (shell + sin desbordamiento) y `MobileJourneyTests`.
