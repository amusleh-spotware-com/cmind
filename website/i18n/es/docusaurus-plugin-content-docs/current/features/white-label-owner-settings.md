---
slug: white-label-owner-settings
sidebar_label: White-label owner settings
---

# Opciones white-label en la configuración del propietario

Cada opción white-label que un despliegue puede establecer a través de configuración (`appsettings`/env) también se **puede establecer en runtime por el propietario de la app**, desde **Settings → Deployment**, sin redeploy. Una override del propietario **gana sobre la configuración**; borrarla revierte la opción al valor configurado por el despliegue (o el valor por defecto incorporado).

Esto refleja cómo un despliegue white-label configura el producto — las mismas perillas, el mismo efecto — para que un operador pueda ajustar branding, compuertas y política en vivo y ver el resultado inmediatamente.

## Dónde vive

- **UI:** la sección **Deployment** solo para propietarios en el diálogo de configuración, y la página con deep-link **`/settings/deployment`**. Las opciones se agrupan en **una pestaña por categoría** (Branding, Theme, Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, con un diálogo en ventana en desktop y una superficie de pantalla completa en teléfonos.
- **API:** `/api/whitelabel` (solo propietario, nunca condicionada por funcionalidad):
  - `GET /api/whitelabel` — cada opción con su valor efectivo, procedencia (`Config` / `Owner` / `Default`) y si hay una override establecida. **Los secretos están enmascarados** (el valor nunca se devuelve).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — establece una override (validada por tipo de opción). Un valor en blanco en un **secreto** mantiene el secreto existente.
  - `DELETE /api/whitelabel/{key}` — borra una override (revierte a config).
  - `POST /api/whitelabel/reset` — borra **todas** las overrides (revierte el despliegue a纯config).

## Cómo toman efecto las overrides

Las overrides del propietario se almacenan como filas `AppSetting` cifradas donde sea necesario y se superponen sobre el `AppOptions` enlazado por un `IOptionsMonitor<AppOptions>` decorado. Como cada consumidor ya lee opciones a través de ese monitor, una override se aplica **en vivo** en toda la app — el tema, el título de la página, la compuerta MFA, las compuertas de proveedor de IA, la lista de brokers permitidos, la política de registro, la configuración del transporte de email, etc. se actualizan en la siguiente lectura (el tema/branding se vuelve a renderizar inmediatamente). Si la base de datos no está brevemente disponible, la capa **falla abierta** a la línea base configurada, así que una lectura de override nunca puede romper la app.

**Las feature flags** son parte de la misma superficie pero se persisten a través del store de override de features existente (`IFeatureGate`), así que la pestaña Features y los interruptores de features independientes nunca divergen.

**Los secretos** (contraseña SMTP, secreto CAPTCHA, secreto de aprovisionamiento) están cifrados en reposo (`ISecretProtector`, propósito `whitelabel.secret`), de solo escritura en la UI, y nunca devueltos por la API.

## Opciones delegadas

Las credenciales de la **aplicación Open API compartida** y los **límites de tasa por tipo de mensaje** se gestionan en la sección de configuración de **Open API** (ver los docs de copy-trading / Open API). Aparecen en el catálogo de Deployment como entradas *delegadas* (solo lectura aquí, con un enlace) para que no se duplique nada y la garantía de sincronía sigue contando para cubrirlas.

## Siempre sincronizados (aplicado)

Añadir una nueva opción white-label a la configuración **debe** mostrarla en la configuración del propietario en el mismo commit. Esto lo aplica `WhiteLabelCatalogParityTests`: refleja cada propiedad del registro de opciones white-label y falla el build a menos que la propiedad esté registrada en `Core/WhiteLabel/WhiteLabelCatalog` (o listada explícitamente en `IntentionallyExcluded` con una razón). Ver el mandato 10 en `CLAUDE.md`.

## Notas

- Habilitar SMTP en un despliegue que comenzó **sin** email configurado necesita un reinicio (el tipo de remitente se elige al inicio); host/credenciales de un remitente ya configurado se actualizan en vivo.
- Las **etiquetas/descripciones** de opciones son identificadores técnicos de las perillas de configuración que se muestran como datos; las etiquetas de pestañas y todo el chrome interactivo están totalmente localizados.