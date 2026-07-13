---
slug: /features
title: Características — el recorrido completo
description: Todo lo que cMind puede hacer — copia comercial, IA, compilación y backtest, guardias de prop-firm, etiqueta blanca, PWA, MCP y más.
sidebar_label: Descripción general
---

# Características — el recorrido completo 🧭

Bienvenido al gran recorrido. cMind empaca *mucho* en una aplicación, así que aquí está el mapa. Cada capacidad
tiene su propio documento profundo — haz clic en lo que sea que te pique.

## 🔁 Copia comercial

La joya de la corona. Espeja una cuenta maestra en muchas y mantenlas en sincronización incluso cuando el internet
se comporta mal.

- **[Copia comercial](./copy-trading.md)** — el núcleo: espejo, tipos de órdenes, SL/TP, deslizamiento, desync/resync.
- **[Transparencia de ejecución](./copy-execution-transparency.md)** — ve exactamente qué se copió, cuándo y por qué.
- **[Comisiones de rendimiento](./copy-performance-fees.md)** — cobra por tu señal, estilo de marca de agua alta.
- **[Mercado de proveedores](./copy-provider-marketplace.md)** — deja que los comerciantes descubran y sigan proveedores.
- **[Notificaciones](./copy-notifications.md)** — sé avisado cuando algo te necesita.
- **[Recomendador de copia de IA](./ai-copy-recommender.md)** — deja que la IA sugiera a quién copiar.
- **[Ciclo de vida del token de API abierto](./token-lifecycle.md)** — cómo cMind mantiene exactamente un token válido por cID.

## 📊 Tu base de operaciones

- **[Panel de control](./dashboard.md)** — el centro de comandos en vivo y móvil primero: KPIs con sparklines, un gráfico de actividad, un anillo de estado, un feed en vivo y (para administradores) salud del clúster. Se actualiza por sí solo.

## 🧠 Núcleo de IA

No es un cuadro de chat pegado al lado — IA que realmente *hace el trabajo*.

- **[Asistente de IA, agente, guardia de riesgos y alertas](./ai.md)** — generación de estrategia, compilaciones que se auto-reparan, un guardia de riesgo de fondo que puede detener bots automáticamente y alertas inteligentes.

## 🛠️ Compilación y ejecución

- **[Compilación y backtest de cBots](./build-and-backtest.md)** — el IDE Monaco en el navegador, plantillas C#/Python, compilaciones en cajas de arena y curvas de equidad en vivo.
- **[Servidor MCP](./mcp.md)** — expone herramientas de cMind sobre HTTP + SSE para que los clientes de IA puedan manejarlo.

## 🏢 Ejecútalo como un negocio

- **[Etiqueta blanca / marca](./white-label.md)** — rebranding de cada superficie vía config.
- **[Simulación de desafío de prop-firm](./prop-firm.md)** — aplica reglas de pérdida diaria, reducción y objetivo con equidad en vivo.
- **[Alternar características](./feature-toggles.md)** — decide qué ve cada despliegue/inquilino.
- **[Cumplimiento / legal](./compliance.md)** — el rastro de auditoría y superficie legal.

## 📱 La experiencia

- **[Aplicación instalable (PWA)](./pwa.md)** — móvil primero, shell sin conexión, agregar a pantalla de inicio.
- **[Sistema de diseño de interfaz de usuario y móvil primero](../ui-guidelines.md)** — los tokens de diseño y reglas detrás de la apariencia.

## ⚙️ Bajo el capó

Los bits operacionales que mantienen todo funcionando:

- **[Flota de nodos y descubrimiento](../operations/node-discovery.md)** — cómo los nodos se auto-registran y sanan.
- **[Escalado horizontal](../deployment/scaling.md)** — agrega réplicas, sin coordinador externo necesario.
- **[Registro y auditoría](../operations/logging.md)** — registros estructurados + OpenTelemetry.
- **[Despliegue](../deployment/local.md)** — ejecútalo en cualquier lugar.

:::note Mantener documentos honestos
Cada documento de características se mantiene en sincronización con el código — cambia el comportamiento, actualiza el documento, mismo
commit. Si alguna vez ves una desviación, eso es un error: por favor
[abre un problema](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) o envía un PR. 🙏
:::
