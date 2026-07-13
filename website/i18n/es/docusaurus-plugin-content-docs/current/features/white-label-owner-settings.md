---
id: white-label-owner-settings
title: Opciones white-label en la configuración del owner
sidebar_label: Configuración de owner white-label
---

# Opciones white-label en la configuración del owner

Cada opción white-label que un despliegue puede establecer a través de configuración (`appsettings`/env) también se **puede establecer en runtime por el owner**, desde **Settings → Deployment**, sin re-despliegue. Un override del owner **gana sobre la configuración**; limpiarlo revierte la opción al valor configurado del despliegue (o al valor por defecto integrado).

Esto refleja cómo un despliegue white-label configura el producto — las mismas perillas, el mismo efecto — para que un operador pueda ajustar branding, puertas y políticas en vivo y ver el resultado inmediatamente.

## Dónde vive

- **UI:** la sección **Deployment** solo para owners en el diálogo de settings, y la página
  de deep-link **`/settings/deployment`**. Las opciones se agrupan en **una pestaña por categoría** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, con una ventana
  en desktop y una superficie a pantalla completa en teléfonos.
- **API:** `/api/whitelabel` (solo owner, nunca gated por característica):
  - `GET /api/whitelabel` — cada opción con su valor efectivo, procedencia (`Config` / `Owner` /
    `Default`) y si hay un override establecido. **Los secretos están enmascarados** (valor nunca devuelto).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — establece un override (validado por tipo de opción). Un valor en blanco
    en un **secreto** mantiene el secreto existente.
  - `DELETE /api/whitelabel/{key}` — limpia un override (revierte a config).
  - `POST /api/whitelabel/reset` — limpia **todos** los overrides (revierte el despliegue a config puro).

## Cómo toman efecto los overrides

Los overrides del owner se almacenan como filas `AppSetting` encriptadas donde sea necesario y se superponen sobre el
`AppOptions` adjunto por un `IOptionsMonitor<AppOptions>` decorado. Porque cada consumidor ya lee opciones
a través de ese monitor, un override aplica **en vivo** en toda la app — el tema, el título de la página, la puerta MFA,
las puertas de proveedor de IA, la lista de brokers permitidos, la política de registro, la configuración del transportador de email, etc. se actualizan
en la siguiente lectura (el tema/branding re-renderiza inmediatamente). Si la base de datos no está brevemente disponible la
capa **falla abierta** al baseline configurado, así que una lectura de override nunca puede romper la app.

**Las feature flags** son parte de la misma superficie pero se persisten a través del store de override de característica existente
(`IFeatureGate`), así que la pestaña Features y los toggles de característica independientes nunca divergen.

**Los secretos** (contraseña SMTP, secreto CAPTCHA, secreto de aprovisionamiento) están encriptados en reposo
(`ISecretProtector`, propósito `whitelabel.secret`), de solo escritura en la UI, y nunca devueltos por la API.

## Opciones delegadas

Las credenciales de la **aplicación Open API compartida** y los **límites de tasa por tipo de mensaje** se gestionan en la
sección de configuración de Open API (ver los docs de copy-trading / Open API). Aparecen en el catálogo de Deployment
como entradas *delegadas* (solo lectura aquí, con un enlace) para que nada esté duplicado y la garantía de sync aún los cuenta como cubiertos.

## Siempre sincronizados (aplicado)

Agregar una nueva opción white-label a la configuración **debe** mostrarla en la configuración del owner en el mismo
commit. Esto está aplicado por `WhiteLabelCatalogParityTests`: refleja sobre cada propiedad del registro de opciones white-label
y falla el build a menos que la propiedad esté registrada en `Core/WhiteLabel/WhiteLabelCatalog` (o listada explícitamente en
`IntentionallyExcluded` con una razón). Ver mandato 10 en `CLAUDE.md`.

## Notas

- Habilitar SMTP en un despliegue que comenzó **sin** email configurado necesita un reinicio (el
  tipo de remitente se elige al inicio); host/credenciales de un remitente ya configurado se actualizan en vivo.
- Las **etiquetas/descripciones de opciones** son identificadores técnicos de perilla de configuración que se muestran como datos; las etiquetas de pestañas y
  todo el chrome interactivo están completamente localizados.
