---
description: "Revendedor rebautiza aplicación — nombre del producto, logo, favicon, colores, CSS personalizado — vía configuración de implementación, sin cambio de código. Cada valor de marca por defecto a identidad de stock…"
---

# Marca de etiqueta blanca

Revendedor rebautiza aplicación — nombre del producto, logo, favicon, colores, CSS personalizado — vía configuración de implementación, sin cambio de código. Cada valor de marca **por defecto a identidad de stock**: implementación no configurada se ve igual que antes; revendedor anula solo lo que necesita.

## Modelo

- `Core.Options.BrandingOptions` — vinculado desde `App:Branding`. Basado en string (borde de configuración); cada color validado cuando se construye tema.
- `Core.Branding.HexColor` — objeto de valor para color hexadecimal CSS (`#RGB` / `#RRGGBB`), inmutable, auto-validante.
  El color inválido lanza `DomainException` (`domain.branding.color_invalid`) cuando se construye tema — implementación mal configurada falla rápido al inicio, no renderiza paleta rota.
- `Web.Components.Theme.Build(BrandingOptions)` — produce tema MudBlazor desde marca. Solo entradas de paleta marcada vienen de configuración; tipografía, diseño, tonos de superficie neutral permanecen fijos para que producto mantenga apariencia coherente entre revendedores.
- `Web.Branding.IBrandingThemeProvider` — singleton, construir tema una vez, reconstruir en cambio de opciones.
  Inyectado por `MainLayout`/`EmptyLayout` para `MudThemeProvider`, por barra de aplicación para nombre de producto/logo. `App.razor` lee `IOptionsMonitor<AppOptions>` directamente para `<head>` de página (título, descripción, favicon, color de tema, CSS personalizado).

## Configuración

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Forma de variable de entorno: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Clave | Efecto | Predeterminado |
|-----|--------|---------|
| `ProductName` | Texto de barra de aplicaciones + página `<title>` | `cMind` |
| `LogoUrl` | Imagen de logo de barra de aplicaciones; cuando está vacío, muestra texto de nombre de producto | *(vacío)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | descripción de stock |
| `PrimaryColor` / `SecondaryColor` | acento, icono de cajón, botones | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + superficies; `AppBarColor` impulsa `<meta theme-color>` + manifest PWA `theme_color`, `BackgroundColor` el `background_color` de manifest | paleta oscura |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | colores de estado | stock |
| `CustomCss` | inyectado `<style>` en `<head>` (implementación-confiable) | *(vacío)* |
| `ShowSiteLink` | mostrar el enlace de crédito "Impulsado por cMind" en el panel de control | `true` |
| `RequireMfa` | requerir que cada usuario configure autenticación de dos factores antes de usar la aplicación | `false` |
| `NodesUi` | cuánto de la superficie de Nodos se envía: `Full` (lista + agregar/eliminar manual), `Monitor` (lista de solo lectura, sin agregar/eliminar), `Hidden` (sin nav, sin página, sin API manual) | `Full` |
| `RestrictNodesToOwner` | cuando es `true`, solo el propietario puede ver/gestionar nodos; de lo contrario, toda la superficie del personal de administrador-o-arriba puede. Usuarios normales nunca ven nodos de cualquier forma | `false` |

Los activos referenciados por `LogoUrl`/`FaviconUrl` se sirven desde `wwwroot` de aplicación web (p. ej. montar carpeta `wwwroot/branding/`) o cualquier URL absoluta.

`App:Branding` validado al inicio (`BrandingOptionsValidator`, ejecutado vía `ValidateOnStart`): cada color debe ser hexadecimal válido, `CustomCss` no debe contener `<`/`>` (no puede escapar de etiqueta `<style>`). Implementación mal configurada no arranca con mensaje claro, no renderiza página rota.

## Enlace Powered-by

El panel de control renderiza un pequeño enlace de crédito **"Impulsado por cMind"** que apunta al sitio de documentación del proyecto. Se controla por `App:Branding:ShowSiteLink` y es **`true` por defecto** — una implementación no configurada lo muestra. Un revendedor ejecutando una instancia completamente etiquetada en blanco establece
`App__Branding__ShowSiteLink=false` para eliminarlo completamente.

El enlace se emite por componente de panel de control y lee la bandera a través de `IBrandingThemeProvider` /
`BrandingOptions`, por lo que alternarlo es un cambio de solo configuración (sin reconstrucción). Véase
[Etiqueta blanca para negocios](../white-label-for-business.md#el-enlace-powered-by-cmind) para el
resumen orientado a negocios.

## Lista de brokers permitidos

Una implementación de etiqueta blanca puede restringir qué brokers' cuentas de trading sus usuarios pueden agregar — para que un broker
ejecutando cMind solo para sus propios clientes solo sirva su propio libro. Configurado bajo `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Forma de variable de entorno: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Comportamiento:**

- **Lista vacía (predeterminada) ⇒ sin restricciones.** Cada broker está permitido y **sin verificación se ejecuta** — una
  implementación de stock completamente sin cambios.
- **No vacío ⇒ restringido.** cMind verifica cada cuenta que un usuario intenta agregar contra la lista
  (insensible a mayúsculas):
  - **Enlace de Open API (OAuth)** — el nombre del broker se reporta autoritariamente por la Open API de cTrader, por lo que
    una cuenta no permitida simplemente se **salta** (cuentas permitidas en la misma subvención aún se enlazan); la
    página de autorización le dice al usuario qué brokers fueron saltados.
  - **Manual cID (nombre de usuario / contraseña)** — el broker escrito por el usuario **no** es confiable. cMind **verifica**
    el broker real de la cuenta ejecutando el cBot de sonda del broker enviado a través de CLI de cTrader (leyendo
    `Account.BrokerName`) y persiste ese nombre verificado. Un broker no permitido se rechaza con una
    notificación; una falla de verificación (credenciales malas, sin nodo, tiempo de espera) también se muestra, y la
    cuenta no se agrega.

**Modelo:**

- `Core.Options.AccountsOptions` — vinculado desde `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — objeto de valor (recortado, igualdad insensible a mayúsculas).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; vacío = permitir todo. Obligado como un
  invariante dentro de `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — ejecuta contenedor de sonda en web
  host (que tiene socket de Docker), colas registros, y analiza el broker vía
  `Core.Accounts.BrokerProbeOutput`. Solo invocado cuando la lista permitida está restringida.

**cBot de sonda de broker:** un `broker-probe.algo` preconfigurado se envía con la aplicación web (`src/Web/BrokerProbe/`,
copiado a la salida como `broker-probe/broker-probe.algo`), por lo que el `App:Accounts:BrokerProbeAlgoPath` predeterminado
se resuelve fuera de la caja — una ruta relativa se resuelve contra la base de la aplicación
directorio, una ruta absoluta se usa tal como se proporciona. La fuente vive en `tools/broker-probe/`. Cuando el
algo está ausente, verificación cID manual falla cerrada — cuentas bajo una lista permitida restringida aún pueden ser
enlazadas vía la ruta de Open API, que no necesita sonda.

## Lista de brokers permitidos — pruebas

- **Unidad** — `UnitTests/Accounts/`: objetos de valor `BrokerName`/`BrokerAllowlist`, analizador `BrokerProbeOutput`,
  e invariante de lista permitida `CTraderIdAccount`.
- **Integración** — `IntegrationTests/BrokerAllowlistTests.cs`: punto final cID manual con verificador falso
  (sin restricciones / verificado / no permitido / verificación-falló) + enlazador de Open API saltando cuentas no permitidas. `BrokerVerifierLiveTests.cs` ejecuta la sonda **real** cuando se proporcionan creds de cID + el algo
  (se salta limpiamente de lo contrario).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: una implementación restringida rechaza una adición manual a través de la
  UI real y muestra la notificación "no pudo verificarse" (sin fila de cuenta agregada).

## Visibilidad de UI de nodos

Los nodos son infraestructura que la mayoría de inquilinos nunca gestionan a mano — los agentes CLI de cTrader
[auto-registran y laten](../operations/node-discovery.md), por lo que una implementación de etiqueta blanca puede ocultar los
controles manuales, o la superficie de Nodos completamente, y aún ejecutar un clúster saludable a través de auto-descubrimiento.
Dos claves de marca de solo configuración gobiernan esto:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Forma de variable de entorno: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tres modos:**

- **`Full` (predeterminado)** — el producto de stock: la lista de nodos más los controles **Nuevo Nodo** y **Eliminar**
  manuales. `POST`/`DELETE /api/nodes` funcionan.
- **`Monitor`** — una superficie de solo lectura: la lista y estadísticas en vivo permanecen, pero agregar y eliminar manuales son
  removidos. Los nodos solo aparecen a través de auto-descubrimiento. `POST`/`DELETE /api/nodes` devuelven **404**.
- **`Hidden`** — el enlace nav de Nodos y la página están completamente fuera y la ruta de página redirige a la
  panel de control; la API de agregar/eliminar manual está desactivada. El clúster es solo auto-descubrimiento.

**`RestrictNodesToOwner`** establece piso de quién puede ver y gestionar nodos. `false` predeterminado mantiene el estándar
superficie de personal de **admin-o-arriba** (`AdminOrAbove`); establece `true` para hacerlo **solo-propietario** (`Owner`). De cualquier forma
**usuarios normales nunca ven nodos** — esto solo elige entre solo propietario y la superficie de personal más amplia.

Auto-descubrimiento de nodo **no se ve afectado por ambas claves**: el punto final anonimizado `POST /api/nodes/register` auto-registrar
+ latido siempre funciona, por lo que una implementación `Hidden`/`Monitor` aún crece su clúster
automáticamente.

**Modelo:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — la única fuente de verdad componiendo el modo + restricción de propietario:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), la página (`Pages/Nodes.razor`) y los puntos finales (`NodeEndpoints`) todos lo leen para que
  la UI y API nunca puedan discrepar.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — vinculado desde `App:Branding`.

## Visibilidad de UI de nodos — pruebas

- **Unidad** — `UnitTests/Nodes/NodesUiAccessTests.cs`: visibilidad de página, gestión manual y
  resolución de política requerida en cada modo + marca predeterminada.
- **Integración** — `IntegrationTests/NodeUiGatingTests.cs`: sobre HTTP real + Postgres — `Full` permite una
  adición manual, `Monitor`/`Hidden` 404 agregar y eliminar, y `RestrictNodesToOwner` prohíbe a un administrador mientras el
  propietario aún lee la lista.
- **E2E** — `E2ETests/NodesUiTests.cs` (`Full` predeterminado: enlace nav + página + botón Nuevo Nodo se renderizan) y
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: enlace nav desaparecido, `/nodes` redirige).

## Fichas de diseño (variables CSS)

La marca también alcanza **propia** hoja de estilo de la aplicación + componentes personalizados, no solo MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emite la paleta marcada como propiedades personalizadas de CSS en `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), inyectado en `App.razor` justo después de `site.css`. `site.css` y cada componente leen `var(--app-*)` — **sin colores codificados** — por lo que la paleta de revendedor fluye en todas partes (héroe de inicio de sesión, nav inferior, consejos de ayuda, página sin conexión) de forma gratuita. Tonos de superficie neutral por defecto en `site.css :root`; `CustomCss` (inyectado último) puede anular cualquier ficha. Véase [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA marcada

La aplicación instalable también está marcada — el punto final del manifest (`/manifest.webmanifest`) se construye desde `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → tema/fondo). Véase [pwa.md](pwa.md).

## Pruebas

- **Unidad** — `UnitTests/Branding/HexColorTests.cs`: validación hexadecimal válida/inválida.
- **Integración** — `IntegrationTests/ThemeBuildTests.cs`: colores se mapean en paleta, color inválido lanza;
  `IntegrationTests/BrandingHttpTests.cs`: nombre de producto personalizado/descripción/color de tema se renderizan en `<head>` de página servida (WebApplicationFactory + Postgres), los predeterminados mantienen nombre de stock.
- **E2E** — `E2ETests/BrandingTests.cs`: nombre de producto marcado se renderiza en barra de aplicación en navegador real.
