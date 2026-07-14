---
slug: /white-label-for-business
title: Etiqueta blanca para negocios
description: Envía cMind como tu propio producto marcado — para empresas de prop-firm, corredores y negocios de copia comercial. Rebranding de cada superficie vía config, sin cambios de código.
sidebar_position: 4
---

# Etiqueta blanca de cMind para tu negocio 🏢

¿Ejecutas una empresa de prop-firm, un escritorio de corredores o un servicio de copia comercial? cMind fue construido desde el primer día para ser
**revendido como tu propio producto**. Cada superficie — el nombre, el logo, el favicon, los colores, incluso
la aplicación de teléfono instalable — se dobla a tu marca. Tus clientes ven *tu* empresa. Sin cambios de código,
sin bifurcación, solo config.

:::tip[TL;DR]
Apunta `App:Branding` a tu nombre, colores y logo. Reinicia. Listo. La referencia técnica completa vive
en la [documentación de características de etiqueta blanca](./features/white-label.md).
:::

## Qué puedes rebranding

| Superficie | Qué cambia |
|---|---|
| **Nombre del producto** | Texto de barra de aplicaciones + título de pestaña del navegador |
| **Logo y favicon** | Tus marcas en todas partes, incluyendo la pestaña del navegador |
| **Colores** | Paleta completa — primario, superficies, colores de estado — fluye a través de toda la interfaz de usuario *y* el CSS propio de la aplicación a través de tokens de diseño |
| **Aplicación instalable (PWA)** | El nombre del icono y el splash de agregar a la pantalla de inicio usan tu marca |
| **Meta / SEO** | Descripción y URL de soporte son tuyas |
| **CSS personalizado** | Inyecta tu propio pulido para el último 5% |

Todo por defecto a la identidad de cMind en stock, por lo que solo anulas lo que te importa.

## El rebrand de 60 segundos

Establece estos en tu despliegue (config JSON o variables de entorno):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Forma de variable de entorno: `App__Branding__ProductName=AcmeFX`. Los colores se validan al inicio —
un valor hex malo falla el arranque con un mensaje claro en lugar de renderizar una página rota. Agradable y
ruidoso, exactamente cuando lo quieres.

## El enlace "Powered by cMind"

Por **defecto**, el panel de control muestra un pequeño y elegante enlace **"Powered by cMind"** que
apunta a los visitantes a este sitio. Está activado por defecto porque estamos orgullosos del proyecto y
ayuda a otros comerciantes a encontrarlo — pero es **tu decisión**.

- **Consérvalo** (predeterminado): un enlace de crédito sutil en el panel. No te cuesta nada, ayuda al proyecto.
- **Ocúltalo**: establece `App__Branding__ShowSiteLink=false` y desaparece completamente — perfecto para un
  despliegue completamente marcado donde el producto es inequívocamente *tuyo*.

Ver la [documentación de características de etiqueta blanca](./features/white-label.md#powered-by-link) para exactamente dónde
se renderiza.

## Branding multiusuario, por cliente

Porque el branding es solo configuración de despliegue, cada despliegue de inquilino puede llevar su propia identidad. Ejecuta una
instancia marcada separada por cliente, o impulsa el branding desde tu propio plano de control — la aplicación lo lee desde
`IOptionsMonitor`, así que incluso puede reconstruir el tema en vivo cuando las opciones cambian.

Empárejalo con:

- **[Alternar características](./features/feature-toggles.md)** — decide qué capacidades ve cada inquilino.
- **[Reglas de prop-firm](./features/prop-firm.md)** — aplica tus reglas de desafío con seguimiento de equidad en vivo.
- **[Comisiones de rendimiento](./features/copy-performance-fees.md)** + **[mercado de proveedores](./features/copy-provider-marketplace.md)** — monetiza la copia comercial.
- **[Cumplimiento](./features/compliance.md)** — mantén el rastro de auditoría que tu regulador preguntará.

## Activos y alojamiento

Suelta tu logo/favicon en la carpeta `wwwroot/branding/` de la aplicación web (o apunta `LogoUrl`/`FaviconUrl`
en cualquier URL absoluta). Despliegúealo de la forma que mejor te convenga — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) o
[AWS](./deployment/cloud-aws.md).

¿Listo para hacerlo tuyo? Comienza con la [referencia técnica de etiqueta blanca →](./features/white-label.md)
