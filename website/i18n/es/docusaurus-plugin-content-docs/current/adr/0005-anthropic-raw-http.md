---
title: 0005 — El cliente de IA utiliza HTTP puro, no el SDK de Anthropic
description: Por qué IAiClient llama a la API de Anthropic sobre un HttpClient tipado en lugar del SDK oficial, y por qué la IA está completamente puertas en una clave.
---

# 0005 — El cliente de IA utiliza HTTP puro, no el SDK de Anthropic

## Contexto

Cada característica de IA (generación de estrategia, auto-reparación, guardia de riesgos, análisis post-mortem) llama a la API de Anthropic.
Una dependencia de SDK agrega una superficie transitiva que no controlamos, acopla nuestro cadencia de lanzamiento a la suya,
y oculta el contrato exacto de cable que necesitamos razonar para resiliencia y costo.

## Decisión

`IAiClient` llama a Anthropic sobre **HTTP puro** a través de un `HttpClient` tipado — deliberadamente **no** el
SDK. `AiFeatureService` es el único orquestador compartido por puntos finales web, las `AiTools` de MCP y
`AiRiskGuard`. Toda la superficie es **puertas en `AppOptions.Ai.ApiKey`**: sin clave, cada característica
devuelve `AiResult.Fail` y la aplicación se ejecuta sin cambios.

## Consecuencias

- No se requiere clave para compilación, prueba o E2E — CI y dev local ejecutan la aplicación completa sin IA.
- Nos poseemos la forma de solicitud/respuesta, política de reintentos/tiempo de espera y contabilidad de tokens explícitamente.
- Las nuevas características de Anthropic deben ser alambradas a mano; intercambiamos comodidad por control y una superficie de dependencia más pequeña.
  Ver la referencia `claude-api` para id de modelos actuales y parámetros.
