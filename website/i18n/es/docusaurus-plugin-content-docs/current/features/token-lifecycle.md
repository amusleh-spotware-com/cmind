---
description: "Open API de cTrader permite un token de acceso válido por cTrader ID (cID) a la vez. En el momento que se emite un nuevo token — actualización programada, o…"
---

# Ciclo de vida del token Open API

Open API de cTrader permite **un token de acceso válido por cTrader ID (cID) a la vez**. El momento
que se emite un nuevo token — actualización programada, o re-autorización cuando el usuario vincula otra
cuenta en el mismo cID — el token de acceso anterior se invalida. Un motor de copia ejecutando en un
nodo remoto es que mantiene ese token ahora-muerto, por lo que el nuevo token debe alcanzarlo sin soltar la
conexión en vivo.

## Modelo

- **`OpenApiAuthorization`** es el agregado que mantiene acceso encriptado + tokens de
  actualización de un cID. Un índice único en `(UserId, CtidUserId)` aplica **exactamente una autorización por cID
  por usuario**.
- **`TokenVersion`** — un contador monótono incrementado cada vez que el token rota (`Refresh()`,
  que también cubre la ruta de re-autorización cuando otra cuenta se vincula en el mismo cID). Es el
  marcador de versión para la regla de token único-válido y es lo que un host en ejecución usa para detectar un
  cambio incluso si dos cadenas de token suceden colisionar.
- Los tokens se encriptan en reposo vía `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Nunca se registran o almacenan en texto simple.

## Propagación (intercambio en su lugar elegante)

1. Un token rota → el nuevo token + `TokenVersion` incrementado se persisten.
2. El `CopyEngineSupervisor` en el nodo de alojamiento re-lee el plan cada ciclo de reconciliación y
   calcula una **firma de token** (tokens de acceso + versiones). Un cambio significa una rotación.
3. En lugar de derribar el host y reiniciar (que soltaría el flujo de ejecución del maestro), el supervisor
   **empuja el nuevo token al host en ejecución**.
4. El host re-autentica la cuenta afectada **en el socket existente**
   (`ProtoOAAccountAuthReq` de nuevo) vía `SwapAccessTokenAsync`, luego hace una reconciliación ligera. El
   token anterior muere; el flujo de copia nunca se detiene.

Esto es lo que hace el caso entre cID seguro: un usuario agregando una segunda cuenta del mismo cID
durante la ejecución invalida el token anterior, y el perfil de copia en ejecución continúa en el nuevo.

## Actualización

`OpenApiTokenRefreshService` (fondo) proactivamente actualiza autorizaciones antes de vencimiento;
`OpenApiAuthorization.IsExpiring(threshold, now)` la controla. cTrader rota el token de **actualización**
en cada actualización, por lo que el nuevo token de actualización se persiste inmediatamente; un caché de solo lectura que no puede
persistir se auto-invalidaría (relevante para el Job de prueba en clúster, que monta una copia escribible
del secreto).

### Escalada de falla

Una actualización fallida no es silenciosa. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
registra `RefreshFailedAt`, incrementa `ConsecutiveRefreshFailures`, y siempre genera
`AccessTokenRefreshFailed` (advertencia). Cuando el token está ahora dentro de `App:OpenApi:TokenRefreshCriticalWindow`
(predeterminado 6h) de vencimiento y actualización aún está fallando, escalada **una vez** con
evento de dominio `AccessTokenRefreshCritical` + registro `Critical` para que el propietario pueda re-autorizar antes de
operaciones de copia/empresa prop pierden el token. El contador de falla y la escalada latch se reinician en el siguiente
`Refresh` exitoso. El servicio sigue reintentando cada `TokenRefreshInterval`, por lo que una
interrupción de proveedor/mantenimiento se auto-cura cuando el punto final de actualización devuelve.

## Alerta de invalidación y auto-recuperación (M1)

Una re-autorización parcial/nuevamente en un cID invalida el token que un host de copia en ejecución aún mantiene. Cuando una
llamada de trading rechaza con `OpenApiErrorKind.TokenInvalid`, el host genera una alerta distinta
**`CopyTokenInvalidated`** (registro 1078) — no una falla genérica — para que el canal de notificación sepa que un
token necesita atención. La recuperación es automática: el supervisor re-lee la autorización cada ciclo y,
cuando el token actualizado cambia la firma del token, lo empuja al host en ejecución para un **intercambio
en su lugar** — la copia se reanuda sin re-adición manual. Un perfil `NotLinkable` (token/autorización temporalmente
no resoluble) se re-evalúa igualmente cada ciclo de supervisor y se aloja en el momento que su plan se construye de nuevo.

## Reloj de perro de vivacidad del host (M2)

El supervisor observa la tarea de ejecución de cada perfil alojado. Si un host sale o falla mientras su perfil está
aún asignado a este nodo, el reloj de perro cancela y **reinicia** siguiente ciclo (registro
`CopyHostRestarted`), por lo que un host atascado se auto-cura en lugar de necesitar un reinicio manual — y un fallo de perfil
nunca estanca los otros (aislamiento por-perfil).

## Pruebas

- **Unidad** — `TokenVersion` bumps en `Refresh`; host realiza un intercambio en su lugar sin reinicio;
  invalidación entre cID intercambia tokens de fuente y destino; **un token de destino invalidado genera
  `CopyTokenInvalidated` y auto-recupera en el siguiente empuje de token** (M1); decisión `IsHostDead` del reloj de perro reinicia
  un host completado/fallido y deja un perfil reasignado solo (M2).
- **Integración** — `TokenVersion` persiste + incrementa a través de EF en Postgres real; la firma del
  token cambia en un bump de versión incluso si la cadena sin cambios.
