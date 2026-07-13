---
description: "Autenticación de dos factores TOTP opcional con inscripción de aplicación de autenticador, códigos de copia de seguridad de uso único, y un interruptor de etiqueta blanca para hacerlo obligatorio para todos los usuarios."
---

# Autenticación de dos factores (2FA)

Las cuentas pueden protegerse con autenticación de dos factores **contraseña de un solo uso basada en tiempo (TOTP)** encima
de la contraseña. Es **optar por participar** desde el perfil del usuario de forma predeterminada, y una implementación de etiqueta blanca puede hacerlo
**obligatorio** para todos. Cualquier aplicación de autenticador RFC 6238 funciona — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — porque la implementación es estándar (SHA-1, 6 dígitos, paso de 30 segundos); no se
componente de servidor propietario está involucrado.

## Cómo funciona

- **Dominio.** MFA vive en el agregado `AppUser` (contexto de acceso). Un usuario se inscribe a través de
  métodos que revelan la intención — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — por lo que los invariantes (un secreto debe confirmarse antes de activarse;
  un código de copia de seguridad es de uso único) se aplican en un lugar.
- **TOTP.** Generación y verificación se encuentran detrás de la interfaz Core `ITotpAuthenticator`, implementada en
  Infraestructura con la biblioteca **Otp.NET**. Verificación tolera ±1 paso de tiempo de sesgo de reloj.
- **Secreto en reposo.** El secreto del autenticador se almacena **encriptado** vía `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — nunca en texto simple.
- **Códigos de copia de seguridad.** Diez códigos de recuperación de uso único se emiten en inscripción, se muestran **una sola vez**, y se almacenan solo
  como hashes SHA-256 (`MfaBackupCodes`). Cada funciona exactamente una vez; un código gastado se rechaza después.

## Habilitarlo (perfil)

En la página **Cuenta** (`/account`) la sección *Autenticación de dos factores* muestra el estado actual:

1. **Habilitar dos factores** abre un diálogo MudBlazor con un **código QR** (renderizado del lado del servidor como SVG vía
   `Net.Codecrete.QrCodeGenerator`) más la clave de configuración manual.
2. Escanéalo, ingresa el código de 6 dígitos para confirmar — esto verifica el secreto pendiente antes de activar.
3. El diálogo luego muestra los **códigos de copia de seguridad**; guárdalos. 2FA ahora está activado.

La misma sección permite a un usuario inscrito **regenerar códigos de copia de seguridad** o **apagar** 2FA — ambos requieren la
contraseña de cuenta para confirmar.

## Iniciar sesión con 2FA

El inicio de sesión es un flujo de **dos pasos** una vez que 2FA está habilitado:

1. **Paso de contraseña** (`POST /api/auth/login`). En caso de éxito la cookie de autenticación **no** se emite aún; en cambio una cookie
   de corta duración (5 minutos), encriptada *pendiente* se establece y se envía al usuario a `/login/2fa`.
2. **Paso de desafío** (`POST /api/auth/login/verify-2fa`). El usuario ingresa un código TOTP **o** cualquier código de copia de seguridad no usado. En
   caso de éxito la cookie pendiente se cae y se emite la cookie de autenticación real.

Los intentos de segundo factor fallidos cuentan hacia el **bloqueo** de cuenta existente (`AuthLockout`), y los puntos finales de autenticación
se cotizan.

## 2FA obligatorio para una implementación de etiqueta blanca

Un revendedor regulado puede requerir 2FA para **cada** cuenta:

```jsonc
// appsettings / ambiente
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Cuando `RequireMfa` está activado y un usuario sin 2FA inicia sesión, el paso de contraseña reporta
`mfaSetupRequired` y `MfaEnforcementMiddleware` redirige sus navegaciones de página a `/account` hasta que
terminen inscripción. Por defecto es `false`, por lo que una implementación no configurada mantiene 2FA opcional. Véase
[Etiqueta blanca](white-label.md).

## Puntos finales

| Método y ruta | Propósito |
| --- | --- |
| `POST /api/auth/login` | Paso de contraseña; devuelve `mfaRequired` (desafío) o inicia sesión |
| `POST /api/auth/login/verify-2fa` | Paso de segundo factor (TOTP o código de copia de seguridad) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pendiente, recuento de códigos de copia de seguridad restantes |
| `POST /api/auth/mfa/setup` | Comenzar inscripción — devuelve secreto, URI `otpauth://`, SVG QR |
| `POST /api/auth/mfa/confirm` | Confirmar un código, activar, devolver códigos de copia de seguridad |
| `POST /api/auth/mfa/disable` | Apagar (contraseña confirmada) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Emitir un nuevo conjunto (contraseña confirmada) |

## Pruebas

- **Unidad** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vectores RFC 6238),
  `AppUserMfaTests.cs` (invariantes de inscripción/transición/uso único), `MfaBackupCodesTests.cs`.
- **Integración** — `IntegrationTests/MfaPersistenceTests.cs` (inscribir → confirmar → consumir, eliminación en cascada)
  e `MfaFlowTests.cs` (flujo HTTP completo de dos pasos con TOTP + código de copia de seguridad, y puerta de inscripción obligatoria).
- **E2E** — `E2ETests/MfaFlowTests.cs`: habilitar desde el perfil (QR + confirmar + códigos de copia de seguridad) y completar un
  inicio de sesión desafiado, en puertos de vista de escritorio y móvil.
