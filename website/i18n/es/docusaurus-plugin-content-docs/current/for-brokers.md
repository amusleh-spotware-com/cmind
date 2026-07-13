---
slug: /for-brokers
title: cMind para corredores de cTrader
description: Por qué un corredor de cTrader debe ejecutar un cMind de etiqueta blanca para sus propios clientes — dar a los comerciantes IA, copia comercial y desafíos de prop-firm bajo tu marca, restringir cuentas a tu corretaje y ganar una ventaja sobre competidores.
keywords:
  - cTrader broker
  - white-label trading platform
  - broker technology
  - copy trading for brokers
  - AI trading tools
  - prop firm software
sidebar_position: 6
---

# cMind para corredores de cTrader 🏦

Ejecutas un corretaje de cTrader. Tus clientes ya pueden comerciar — pero también pueden los clientes de cualquier otro corredor. **cMind te permite entregar a tus comerciantes una plataforma completa de operaciones comerciales potenciada por IA, marcada como tuya**, para que construyan, backtestean, ejecuten, copien y monitoreen estrategias dentro de *tu* ecosistema en lugar de derivarse a una herramienta de terceros. Eso son clientes más pegajosos, más volumen y una verdadera ventaja sobre corredores que no ofrecen nada más que una terminal.

:::tip TL;DR
Ejecuta un cMind de etiqueta blanca para tus clientes. Restringe cuentas a **tu** corretaje, activa IA y
copia comercial, y entrégalo bajo tu marca. → [Etiqueta blanca para negocios](./white-label-for-business.md)
:::

## La ventaja que obtienes sobre otros corredores

- **Diferenciarte en herramientas, no solo en diferenciales.** Da a los clientes generación de cBot de IA, backtesting en un
  clúster administrado, copia comercial y desafíos de prop-firm — capacidades que la mayoría de los corredores simplemente no
  ofrecen.
- **Mantén a los clientes en tu ecosistema.** Cuando los comerciantes construyen y ejecutan sus estrategias dentro de tu plataforma marcada,
  se quedan. La retención es todo el juego.
- **Bajo tu marca, en tu dominio.** Nombre, logo, colores, favicon, incluso la aplicación de teléfono instalable —
  todos tuyos. Nadie ve "cMind". → [Característica de etiqueta blanca](./features/white-label.md)

## Sirve solo tus cuentas (lista de permitidos de corredores)

¿Ejecutar un etiqueta blanca para *tus* clientes? Restringe qué cuentas comerciales de qué corredores los usuarios pueden agregar para que
tu despliegue solo sirva tu libro:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

Cuando la lista de permitidos está configurada, cMind verifica cada cuenta que un usuario intenta agregar — tanto a través de la API abierta de cTrader
como a través del inicio de sesión de cID manual (verificado leyendo el nombre real del corredor de la cuenta) — y rechaza cualquier
cuenta que no esté en tu lista. Déjala vacía y todos los corredores están permitidos (el predeterminado). Ver la
[documentación de características de etiqueta blanca](./features/white-label.md#broker-allowlist) para la mecánica completa.

## Envía una aplicación Open API para todos tus usuarios

Salta el problema por usuario: proporciona **una aplicación Open API de cTrader** y cada cliente autoriza
sus cuentas a través de ella — ningún cliente jamás registra la suya. Registra una única URL de redirección, suelta
las credenciales en config o en la configuración del propietario, y el modo compartido se activa para todos. ¿Negociaste
un límite de mensaje de cTrader más alto? Sintoniza los **límites de tasa de cliente por tipo de mensaje** (o deshabilita el ritmo).
→ [Aplicación Open API compartida y límites de tasa](./features/open-api-shared-app.md)

## Nuevas formas de monetizar

- **IA, sin fricción para los clientes.** Proporciona una clave de proveedor de IA predeterminada a nivel de despliegue y
  cada cliente obtiene características de IA al instante — sin registro en otro lugar. Marca, o agrúpalo en niveles premium.
  Los clientes aún pueden traer su propia clave. → [Característica de IA](./features/ai.md)
- **Desafíos de prop-firm.** Ejecuta desafíos de comerciante financiado con seguimiento de equidad en vivo y reglas aplicadas,
  y cobra por entradas. → [Reglas de prop-firm](./features/prop-firm.md)
- **Negocio de copia comercial.** Comisiones de rendimiento y un mercado de proveedores convierten la copia comercial en
  ingresos. → [Comisiones de rendimiento](./features/copy-performance-fees.md) ·
  [Mercado de proveedores](./features/copy-provider-marketplace.md)
- **Niveles de características.** Decide qué capacidades ve cada segmento de cliente con
  [alternar características](./features/feature-toggles.md).

## Regulado, auditable, multiusuario

- **Los registros de [cumplimiento](./features/compliance.md)** te dan el rastro de auditoría que tu regulador preguntará.
- **[Autenticación de dos factores](./features/two-factor-auth.md)** puede hacerse obligatoria por despliegue.
- **Branding por cliente** — ejecuta una instancia marcada separada por segmento, impulsada desde tu propio plano de control.
  → [Branding multiusuario](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Cómo empezar

1. Lee [Etiqueta blanca para negocios](./white-label-for-business.md) para el rebrand de 60 segundos.
2. Establece `App:Accounts:AllowedBrokers` en tu corretaje y elige tu [conjunto de características](./features/feature-toggles.md).
3. [Despliegalo](./deployment/cloud.md) — Docker, Kubernetes, Azure o AWS.

¿No quieres ejecutar la infraestructura tú mismo? Un proveedor de alojamiento puede operar un cMind administrado para ti
— apúntalo a [Para proveedores de nube y VPS](./for-cloud-providers.md).

## Moldea la hoja de ruta

cMind es código abierto. Los corredores que se construyen sobre él obtienen una voz desproporcionada en dónde va — solicita las
integraciones y controles que necesitas, y contribuye con ellas a través de la
[guía de contribución](./contributing.md).
