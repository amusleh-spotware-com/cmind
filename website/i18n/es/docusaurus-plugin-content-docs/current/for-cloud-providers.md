---
slug: /for-cloud-providers
title: cMind para proveedores de nube y VPS
description: Por qué un proveedor de nube o VPS debe ofrecer alojamiento administrado de cMind — un producto listo para usar y diferenciado para comerciantes algorítmicos, corredores y empresas de prop-firm, con formas claras de monetizar computación, reventa de etiqueta blanca e IA administrada.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind para proveedores de nube y VPS 🖥️

Ya alquilas computación. cMind es un producto listo para usar y código abierto que puedes envolver esa computación
alrededor: **ofrece alojamiento administrado de cMind** y aterriza una carga de trabajo de alto valor, pegajosa y que consume mucha computación —
comerciantes algorítmicos, corredores, empresas de prop-firm y comunidades comerciales que quieren la plataforma ejecutándose
sin convertirse en el equipo de ops ellos mismos.

:::tip[TL;DR]
Ejecuta la capa sin estado + Postgres + una flota de nodos; entrega a los clientes una URL marcada. Monetiza la
suscripción, la computación, la etiqueta blanca y la IA. → [Despliegúealo en la nube](./deployment/cloud.md)
:::

## Por qué ofrecer cMind administrado

- **Sin costo de construcción.** Es código abierto, con licencia MIT y ya documentado, probado y containerizado.
  Empaquetas y lo operas — no lo construyes.
- **Un producto diferenciado para un nicho lucrativo.** El trading algorítmico es intensivo en computación: backtests y
  nodos en vivo queman CPU, que es *uso facturable* que ya vendes.
- **Clientes pegajosos.** Los comerciantes que construyen y ejecutan estrategias dentro de la plataforma no se van casualmente.
- **Convierte una advertencia en una venta adicional.** cMind es auto-alojado por diseño — para clientes que "no quieren
  ser el equipo de ops", *tú* eres la respuesta.

## Quién compra cMind administrado de ti

- **Cuantas individuales y comerciantes** que quieren que esté alojado. → [Para comerciantes](./for-traders.md)
- **Corredores de cTrader** ejecutando una etiqueta blanca para sus clientes. → [Para corredores](./for-brokers.md)
- **Empresas de prop-firm y copia comercial** que necesitan infraestructura marcada y auditable.

## Qué significa "cMind administrado" para ejecutar

Operas tres niveles; el cliente obtiene una URL web marcada:

| Nivel | Qué es | Dónde se ejecuta |
|---|---|---|
| Sin estado (Web + MCP) | La aplicación + API + servidor MCP | Cualquier plataforma de contenedor, escalada automática |
| Base de datos | PostgreSQL | PostgreSQL administrado (RDS / Flexible Server / tu propio) |
| Flota de nodos | Compila y ejecuta contenedores de cTrader | **VMs o Kubernetes — necesita Docker privilegiado** |

:::warning[Una cosa a escala por adelantado]
Los agentes de nodo construyen y ejecutan contenedores de cTrader, por lo que necesitan **Docker privilegiado**. Eso descarta
tiempos de ejecución de contenedores sin servidor (Azure Container Apps, AWS Fargate) *para los agentes* — ejecuta los en
[Kubernetes](./deployment/kubernetes.md), una VM o EC2. La capa sin estado se ejecuta en cualquier lugar.
:::

Guías de despliegue reales y copiar-pegar hacen esto concreto: [descripción general de nube](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Escalado](./deployment/scaling.md).

## Cómo lo monetizas

- **Suscripción de alojamiento administrado.** Planes mensuales de Iniciador / Equipo / Negocio dimensionados por flota de nodos y
  concurrencia de backtest.
- **Medición de uso y computación.** Factura horas de backtest, horas de nodo en vivo y almacenamiento — naturalmente medido
  por la flota de contenedores que ya ejecutas.
- **Niveles de reventa de etiqueta blanca.** Cobra más por un rebrand completo (logo, colores, PWA,
  `ShowSiteLink=false`) y por habilitar capacidades premium a través de
  [alternar características](./features/feature-toggles.md). → [Etiqueta blanca](./features/white-label.md)
- **IA administrada.** Agrupa una clave de proveedor de IA predeterminada para que todos los usuarios de los clientes obtengan IA sin configuración, y
  marca el uso — u ofrece traer tu propia clave. → [Característica de IA](./features/ai.md)
- **Ingresos compartidos de prop-firm y copia comercial.** Empresas anfitrionas que ejecutan desafíos y comisiones de rendimiento y
  toman un corte de plataforma. → [Prop-firm](./features/prop-firm.md) ·
  [Comisiones de rendimiento](./features/copy-performance-fees.md) ·
  [Mercado de proveedores](./features/copy-provider-marketplace.md)
- **Configuración, incorporación y SLA.** Adjunta servicios profesionales y soporte premium.

## Patrones multiusuario

- **Despliegue por inquilino (recomendado).** Una instancia marcada por cliente — aislamiento fuerte,
  branding por inquilino y base de datos, un token de unión de nodo distinto por inquilino. El branding se lee desde
  `IOptionsMonitor`, por lo que cada instancia lleva su propia identidad.
  → [Branding multiusuario](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Descubrimiento de nodos](./operations/node-discovery.md)
- **Plano de control compartido (avanzado).** Impulsa muchas instancias desde tu propia capa de aprovisionamiento, sembrando
  branding y características por inquilino mediante programación.

## Medición de uso para facturación

Un endpoint **`GET /api/usage`** solo de propietario/administrador devuelve un resumen de solo lectura que un proveedor puede sondear y
facturar — sin ningún dominio o persistencia nuevo, proyecta el estado existente:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Sondéalo por despliegue de inquilino para impulsar precios basados en asientos, basados en flota o basados en carga de trabajo. Empárejalo con
[registro y observabilidad](./operations/logging.md) para medición de computación más fina.

## Manteniendo los márgenes predecibles

Escala nodos según la demanda, comparte niveles de Postgres y escala automáticamente la capa sin estado. Las superficies operacionales que necesitas
ya están ahí:

- [Escalado y auto-sanación](./deployment/scaling.md)
- [Registro y observabilidad](./operations/logging.md)
- [Copia de seguridad y recuperación](./operations/backup-recovery.md)

## Comenzar

1. Configura un despliegue de referencia desde las [guías de nube](./deployment/cloud.md).
2. Plantéalo por inquilino (branding + token de unión + BD) y conecta tu facturación al uso de computación.
