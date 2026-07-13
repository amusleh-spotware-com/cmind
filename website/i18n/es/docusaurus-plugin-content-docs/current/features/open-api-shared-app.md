---
description: "Enviar una aplicación cTrader Open API para cada usuario (modo compartido white-label), la única URL de redirección para registrarse, y límites de velocidad de cliente por tipo de mensaje."
---

# Shared Open API application & rate limits

Por defecto, cada usuario registra su **propia** aplicación cTrader Open API en **Configuración → Open API**. Un operador de white-label (típicamente un corredor o distribuidor de cTrader) puede enviar **una aplicación Open API compartida para todos los usuarios** — nadie registra la suya; todos autorizan sus cuentas a través de la única app del operador.

## Dos formas de proporcionar la aplicación compartida

La aplicación compartida se proporciona desde la configuración de despliegue **o** desde la interfaz de usuario de configuración del propietario (el valor establecido por el propietario gana). Proporciona una vez y el modo compartido se activa para todos.

### 1. Configuración de despliegue (sembrada al inicio)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // URL pública canónica de ESTE despliegue
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encriptado en reposo; nunca registrado
    }
  }
}
```

Al inicio, la app siembra una aplicación compartida propiedad de la cuenta del propietario (idempotente — nunca sobrescribe un valor de tiempo de ejecución editado por propietario, y re-sembrado es un no-op).

### 2. Configuración del propietario (tiempo de ejecución, sin redeploy)

**Configuración → Open API** (solo propietario) muestra una tarjeta **Aplicación compartida de despliegue**: agregar / editar / eliminar la app compartida, con la URL de redirección mostrada para copy-paste. Los cambios toman efecto para autorizaciones nuevas inmediatamente.

## La URL de redirección (registra esto en cTrader)

Cada aplicación cTrader Open API registra **una** URL de redirección — el **mismo valor único** para la app compartida y para cualquier app por usuario:

```
{tu URL de despliegue}/openapi/callback
```

por ejemplo `https://cmind.yourbroker.com/openapi/callback`.

- La app **muestra el valor exacto** en la página de configuración de Open API (con botón copy) — pégalo en el portal del socio de cTrader cuando crees la aplicación Open API.
- Se compone desde `App:OpenApi:PublicBaseUrl` por lo que permanece estable detrás de un proxy inverso / CDN; cuando se desestablece, regresa al host de solicitud entrante.
- La experiencia de invitación vs usuario normal difiere solo en dónde aterriza el usuario **después** de la devolución de llamada (su lista de cuentas vs una confirmación "cuentas agregadas") — la URL de redirección registrada no cambia.

## Qué ven los usuarios en modo compartido

Cuando existe una aplicación compartida:

- Los usuarios **no obtienen opción** para registrar su propia aplicación Open API — la página de configuración muestra **"Open API es gestionado por tu proveedor"** y un botón **Autorizar cuentas** que usa la app compartida.
- Cualquier aplicación personal preexistente es **eliminada**; sus cuentas autorizadas se reapuntan a la app compartida y deben ser **re-autorizadas** (sus viejos tokens fueron emitidos bajo un id de cliente diferente). Intentar crear una app personal devuelve un error "gestionado por tu proveedor".

## Límites de velocidad de cliente (por tipo de mensaje)

El cliente pacifica mensajes cTrader Open API salientes para que una ráfaga nunca active un bloqueo de límite de velocidad del lado del servidor. Los límites son **por tipo de mensaje**, coincidiendo con los docs de cTrader Open API:

| Categoría | Qué cubre | Defecto |
|---|---|---|
| `General` | mensajes de operaciones + lectura (órdenes, símbolos, consultas de cuenta) | 45 msg/s |
| `HistoricalData` | solicitudes de trendbar / datos de tick (aceleradas más duramente por cTrader) | 5 msg/s |

Una solicitud de datos históricos cuenta contra **ambos** su propio cubo y el cubo general. Los mensajes de latido del corazón y autenticación nunca se pacifican. Los mensajes se encolan y drenan a la velocidad disponible — nada se descarta y se preserva el orden.

Sintoniza si tu corredor negoció límites cTrader **más altos**, o establece una categoría en **`0`** para desactivar el pacing por completo (ilimitado):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Configuración del propietario:** la tarjeta **Límites de velocidad de cliente** en **Configuración → Open API** (la anulación del propietario gana, se aplica a conexiones nuevas / en reconexión).
