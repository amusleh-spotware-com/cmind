---
title: 0004 — CBotBuilder se ejecuta en el host web en un contenedor de caja de arena
description: Por qué las compilaciones de cBot no confiables suceden en el host web dentro de un contenedor SDK desechable en lugar de en un nodo.
---

# 0004 — `CBotBuilder` se ejecuta en el host web en un contenedor de caja de arena

## Contexto

Construir un cBot de usuario significa ejecutar **MSBuild no confiable** — código arbitrario en tiempo de compilación (objetivos,
generadores de fuentes, scripts de restauración). Necesita el socket Docker para girar un contenedor SDK. Los nodos
ejecutan contenedores de trading y no deberían tampoco tener privilegios de compilación.

## Decisión

`CBotBuilder` se ejecuta **en el host web** (que ya tiene el socket Docker), dentro de un **contenedor SDK desechable**
con:

- un directorio `/work` montado en enlace (solo las entradas/salidas de compilación, no el sistema de archivos del host);
- un volumen compartido `app-nuget-cache` para rendimiento de restauración;
- sin acceso a la red del host más allá de lo que la restauración necesita.

Para que MSBuild no confiable no pueda acceder al sistema de archivos o la red del host. Los contenedores de ejecución/backtest, por
el contrario, se ejecutan en nodos elegidos por `NodeScheduler`.

## Consecuencias

- El privilegio de compilación (socket Docker) está confinado al host web; los nodos solo ejecutan imágenes de trading permitidas.
- Cada compilación está aislada en un contenedor desechable — una compilación maliciosa no puede persistir o escapar.
- El host web debe tener un socket Docker disponible; este es un requisito de despliegue, no opcional.
