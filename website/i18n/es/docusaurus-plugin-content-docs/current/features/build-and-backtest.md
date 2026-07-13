---
description: "Construir, ejecutar, backtests cBots de cTrader (C# y Python, ambos .NET) desde IDE de Monaco en el navegador, ejecutar en imagen oficial ghcr.io/spotware/ctrader-console."
---

# Construir y hacer backtests de cBots

Construir, ejecutar, hacer backtests de cBots de cTrader (C# **y** Python, ambos .NET) desde IDE de Monaco
en el navegador, ejecutar en imagen oficial `ghcr.io/spotware/ctrader-console`.

## Construcción

- La página **Constructor** aloja editor de Monaco; `CBotBuilder` compila proyecto con
  `dotnet build` **en contenedor descartable** (`AppOptions.BuildImage`, directorio de trabajo montado en enlace
  en `/work`), para que objetivos MSBuild de usuario no confiable no alcancen host. Restauración de NuGet en caché
  en toda construcción a través de volumen compartido. Host web necesita acceso a socket de Docker.
- Plantillas de inicio de C# + Python viven en `src/Nodes/Builder/Templates/`.

## Ejecutar y hacer backtests

- **Instancias** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transición reemplaza entidad (cambio de id),
  id de contenedor llevado.
- `NodeScheduler` elige nodo elegible menos cargado; `ContainerDispatcherFactory` encamina a
  agente HTTP de nodo remoto o despachador local de Docker.
- Los pollers de finalización reconcilian contenedores salidos (contenedores de backtest auto-salen vía
  `--exit-on-stop`); informe presente → completado (almacenar `ReportJson`), faltante → fallido.
- Registros de contenedor en vivo fluyen hacia navegador sobre SignalR; curvas de equidad de backtest analizadas desde
  informe + gráficos.

## Notas de CLI de cTrader Console

Los backtests necesitan `--data-mode` (predeterminado `m1`), fechas como `dd/MM/yyyy HH:mm`, y
argumento posicional JSON `params.cbotset`; `run` rechaza `--data-dir` (solo backtest). Véase
`ContainerCommandHelpers`.

## Nodos y escala

La capacidad de ejecución se escala agregando agentes de nodo (auto-registro + latido). Véase
[descubrimiento de nodo](../operations/node-discovery.md) y [escalado](../deployment/scaling.md).
