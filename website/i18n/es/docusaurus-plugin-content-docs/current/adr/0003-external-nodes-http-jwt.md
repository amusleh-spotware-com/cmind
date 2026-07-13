---
title: 0003 — Los nodos CLI de cTrader son HTTP + JWT, sin SSH/shell
description: Por qué los agentes de nodos remotos exponen solo una API HTTP con JWTs de corta duración y nunca un shell.
---

# 0003 — Los nodos CLI de cTrader son HTTP + JWT, sin SSH/shell

## Contexto

Los contenedores de backtest/ejecución se ejecutan en hosts remotos. El enfoque obvio — SSH y ejecutar docker — da
a la aplicación principal ejecución arbitraria de código remoto y credenciales de larga duración en cada nodo. Ese es un
radio de explosión grande para un sistema que ejecuta cBots de usuario no confiables.

## Decisión

Cada host remoto ejecuta un agente **HTTP** `CtraderCliNode` independiente **sin SSH y sin shell**. La
aplicación principal llama al agente por HTTP; cada solicitud lleva un **JWT HS256** de corta duración (5 minutos,
`iss=app-main` / `aud=app-node`) firmado con el secreto de ese nodo. El agente:

- solo ejecuta imágenes que coinciden con `AllowedImagePrefix` (con un límite de ruta para que `ghcr.io/spotware` no pueda
  coincidir con `ghcr.io/spotware-evil/...`);
- ejecuta docker a través de `ArgumentList` — nunca una cadena de shell;
- es **sin estado**, encontrando contenedores por la etiqueta `app.instance`;
- se auto-registra y envía latidos a `POST /api/nodes/register`; la aplicación principal actualiza el `CtraderCliNode`
  **por nombre**, para que un nodo sobreviva cambios de IP.

## Consecuencias

- Un token de solicitud filtrado expira en minutos; no hay credencial de shell de pie para robar.
- La capacidad del agente es limitada a "ejecutar una imagen permitida" — no puede convertirse en un shell remoto general.
- La identidad del nodo se basa en el nombre, por lo que el reaprovisionamiento de un nodo con una nueva IP no huérfano su historial.
