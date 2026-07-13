---
slug: /for-traders
title: cMind para comerciantes de cTrader
description: Por qué un comerciante de cTrader debe auto-alojar cMind — posea su stack y datos, autor, backtest, ejecute y monitoree cBots en una consola potenciada por IA, en su portátil, VPS o teléfono.
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind para comerciantes de cTrader 📈

Ya comercias en cTrader. Ya estás malabareando un editor de código, un backtester, un VPS y tres
pestañas del navegador. **cMind colapsa todo eso en una consola oscura, amigable con el teclado que ejecutas
tú mismo** — y es código abierto, por lo que nada sobre tu ventaja, tus estrategias o tus credenciales
jamás sale de tu cuadro.

:::tip TL;DR
Auto-aloja cMind en un portátil, un VPS barato o un servidor doméstico. Autor, backtest, ejecuta y monitorea cBots
en un lugar, con un núcleo de IA haciendo las tareas. → [Ejecútalo en 5 minutos](./deployment/local.md)
:::

## ¿Por qué auto-alojar en lugar de un servicio alojado?

- **Posee tu stack y tus datos.** Tus cBots, credenciales, tokens e historial de equidad viven en
  **tu** infraestructura — sin terceros, sin bloqueo, sin correo "estamos descontinuando este producto".
- **Es genuinamente tuyo para cambiar.** C# 14 / .NET 10, DDD estricto, EF Core + PostgreSQL, un servidor MCP —
  todo código abierto y hackeable. Bifúrcalo, extiéndelo, envía un PR.
- **Sin barrera de pago por característica.** Traes tu propia clave de IA para cualquier proveedor; cada característica de IA está activada.

¿Prefieres no ejecutar servidores tú mismo? Una empresa de alojamiento puede ejecutar un cMind administrado para ti —
ver [Para proveedores de nube y VPS](./for-cloud-providers.md).

## Una consola, sin malabarismo de pestañas

- **Autor** en un IDE Monaco real (el editor VS Code), con plantillas de C# **y** Python y
  `dotnet build` en cajas de arena en contenedores desechables. → [Compilación y backtest](./features/build-and-backtest.md)
- **Backtest** en una flota de nodos y observa las curvas de equidad transmitirse en vivo.
- **Ejecuta** estrategias en vivo y **monitoréalas** desde un panel de control. → [Panel de control](./features/dashboard.md)
- **Copia** una cuenta maestra en muchas cuentas en corredores e identificadores de cTrader, con reconciliación
  que sobrevive conexiones caídas y tokens rotativos. → [Copia comercial](./features/copy-trading.md)

## IA que hace tareas, no charla pequeña

Traes tu propia clave API (cualquier proveedor soportado — nube o modelo local) y obtén inglés simple → un
cBot compilado real con un bucle de auto-reparación, sintonización de parámetros, análisis post-mortem de backtest y un guardia de riesgos
que puede detener automáticamente un bot que se comporta mal. → [Conoce el núcleo de IA](./features/ai.md)

## Herramientas de grado institucional, para uno

El mismo rigor que paga un escritorio, en tu propio cuadro:

- [Integridad de backtest](./features/backtest-integrity.md) · [Tamaño de posición](./features/position-sizing.md)
- [Salud de la estrategia](./features/strategy-health.md) · [Laboratorio de régimen](./features/regime-lab.md)
- [TCA de ejecución](./features/execution-tca.md) · [Diario de trading](./features/trading-journal.md)
- [Estudio de agentes](./features/agent-studio.md) · [Posicionamiento contrario](./features/contrarian-positioning.md)

## Se ejecuta donde estés

Comienza en tu portátil con `docker compose up`, pasa a un VPS barato o un servidor doméstico cuando estés
listo, y comprueba tus bots desde tu teléfono — cMind es un
[PWA](./features/pwa.md) instalable y móvil-primero. → [Ejecútalo localmente](./deployment/local.md)

¿Quieres que tu cliente de IA lo maneje? Hay un [servidor MCP](./features/mcp.md) integrado.

## Ayuda a mejorarlo

cMind es código abierto y con licencia MIT — la hoja de ruta está moldeada por la comunidad:

- Presentar problemas y solicitudes de características, y votar por lo que importa.
- Agregar plantillas de cBot, adaptadores de proveedores de IA o traducciones de interfaz de usuario.
- Enviar PR — tres niveles de prueba (unidad + integración + E2E) y DDD estricto mantienen la barra alta, y la
  [guía de contribución](./contributing.md) te lleva a través de ello.

¿Listo? → [Lee la introducción](./intro.md) luego [ejecútalo localmente](./deployment/local.md).
