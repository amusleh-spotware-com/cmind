---
description: "Registro de usuario de autoservicio seguro y controlado por etiqueta blanca — una página de registro en la aplicación y una API de aprovisionamiento de servidor a servidor, con atributos de usuario configurables, aprobación de administrador o verificación de correo electrónico controlada, y guardias anti-abuso. Deshabilitado de forma predeterminada."
---

# Registro de usuario

Por defecto el **propietario/administrador añade usuarios manualmente** (página Usuarios → *Nuevo usuario*). Para implementaciones de etiqueta blanca
que necesitan incorporar usuarios a escala — o integrar la aplicación con otro servicio — cMind también envía una
ruta de **registro de autoservicio seguro**. Está **deshabilitado de forma predeterminada**: una implementación de stock sin cambios
y tanto la página como la API devuelven 404 hasta que una implementación opta por participar.

Hay dos puntos de entrada compartiendo un flujo de dominio:

1. **Página en la aplicación** (`/register`) — página de inscripción marcada, optimizada para dispositivos móviles en la misma shell que `/login`.
2. **API de aprovisionamiento** (`POST /api/provision`) — punto final de servidor a servidor para que un servicio de integración
   cree cuentas, autenticado por un secreto de aprovisionamiento por implementación.

## Qué se registra — minimización de datos

cMind es **herramientas** de trading: construye/ejecuta/backtests cBots y espejos de operaciones sobre credenciales Open API de cTrader *propias*
de cada usuario. **No abre cuentas de trading o custodia dinero de cliente**, por lo que verificación de identidad KYC/AML es
la **obligación del broker**, no de esta plataforma. El formulario de registro por lo tanto
registra **solo un correo electrónico de forma predeterminada** — lo mínimo necesario para proporcionar el servicio (Artículo GDPR 5(1)(c) minimización de datos;
base legal = contrato). cMind deliberadamente envía **ningún** ID nacional / fecha de nacimiento /
campos de dirección.

Cada otro atributo es **optar por participar por implementación** vía `App:Registration:Attributes`, cada uno independientemente
`Off` / `Optional` / `Required`:

| Atributo | Notas |
|---|---|
| `FullName`, `DisplayName`, `Company` | Texto libre, longitud acotada. |
| `Country` | ISO 3166-1 alfa-2, validado contra conjunto de códigos fijo. |
| `Phone` | Formato E.164 (`+14155552671`). |
| `Locale` | Forma BCP-47 (`en-US`), normalizada. |
| `MarketingOptIn` | Separado, casilla **desmarcada** — nunca agrupado con consentimiento obligatorio (CAN-SPAM). |
| `AgeConfirmation` | Una casilla solo; **ninguna** fecha de nacimiento se almacena. |

Los atributos viven en el objeto de valor `UserProfile` propiedad del agregado `AppUser`, validado en
construcción. **Erasura GDPR** (`AppUser.Anonymize()`) borra el perfil y cualquier token de verificación.

**Consentimiento.** Cuando `RequireTermsAcceptance` está activado, el usuario debe aceptar los documentos legales publicados
(Términos, Privacidad, Divulgación de Riesgos). La aceptación se registra a través del agregado `ConsentRecord` existente —
marca de versión, con marca de tiempo, con IP de origen — la misma tienda usada en otros lugares para MiFID/ESMA-grado
mantenimiento de registros.

## Modos de control

Una cuenta auto-registrada no puede iniciar sesión hasta que borre su compuerta (`App:Registration:Mode`):

- **`AdminApproval`** (predeterminado) — la cuenta se pone en cola; un propietario/administrador la aprueba en la página **Usuarios**
  (sección *Aprobación pendiente*). No necesita infraestructura de correo.
- **`EmailVerification`** — un enlace de verificación único que expira se envía por correo; la cuenta se activa cuando
  se abre el enlace. Requiere transporte de correo (`App:Email`). **Si no se configura transporte, este modo
  automáticamente se degrada a `AdminApproval`** al inicio, por lo que habilitar registro nunca silenciosamente se rompe.
- **`Open`** — la cuenta está activa inmediatamente (solo confiable/desarrollo).

Los usuarios auto-registrados siempre se crean como **`User`** (o `Viewer` si está configurado) — el dominio
**se niega duro** a crear un Owner/Admin a través de auto-registro.

## Seguridad y anti-abuso

- **Anti-enumeración.** Un correo electrónico duplicado cede la **misma** neutral `202 Accepted` como inscripción fresca y
  no crea nada — la aplicación nunca divulga si una dirección ya tiene cuenta.
- **Limitación de velocidad.** Los puntos finales públicos se regulan por IP (más duro que el limitador de autenticación).
- **Política de contraseña.** Longitud mínima aplicada; las contraseñas se cutan (Argon2 vía `IPasswordHasher`);
  tokens de verificación se almacenan solo como hashes SHA-256 y son de uso único + expirante.
- **Higiene de correo electrónico.** Lista de permitidos opcional de dominios de correo electrónico y lista negra de bloqueo de proveedor desechable.
- **CAPTCHA (opcional).** reCAPTCHA / hCaptcha / Turnstile vía su contrato de verificación compartido.
- **Puerta de inicio de sesión.** Una cuenta pendiente se rechaza al inicio de sesión con respuesta neutral.

## API de aprovisionamiento (integración)

Con `App:Registration:Api:Enabled` y un `Secret` configurado, otro servicio puede crear usuarios:

```
POST /api/provision
X-Provision-Secret: <el secreto configurado>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

El secreto se compara en tiempo constante. Las cuentas aprovisionadas se crean **activas** (o invitadas con
`MustChangePassword`) dependiendo de `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Habilitarlo

El registro requiere **tanto** la bandera de característica como el interruptor maestro:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // o EmailVerification / Open
    "DefaultRole": "User",             // nunca Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // vacío = cualquiera
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

La sección `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) configura el transporte usado por modo `EmailVerification`; deja `Host` sin configurar para ejecutar sin
correo (el remitente sin operación). Véase [alternancias de características](./feature-toggles.md) y [etiqueta blanca](./white-label.md) para
cómo implementaciones activan características y reautorizan. Cuando registro está habilitado, la página de inicio de sesión muestra un enlace **Crear
cuenta**.

## Probado

Unidad (validación de perfil, guardia de rol `SelfRegister`, transiciones de activación, tokens de uso único, erasura),
integración (404 deshabilitado de forma predeterminada, flujo de aprobación, degradación de verificación de correo electrónico, anti-enumeración, guardias
de abuso, atributos requeridos, aprovisionamiento + secreto malo), y E2E (inicio de sesión predeterminado sin enlace de registro;
la página `/register` renderiza su estado cerrado marcado).
