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

## Ejecutar desde el editor de código

Hacer clic en **Ejecutar** en el editor de código abre un diálogo en lugar de lanzar una ejecución ciega y fija:

- **Cuenta de trading** (obligatoria) — la cuenta de cTrader a la que se conecta el cBot.
- **Conjunto de parámetros** (opcional) — elige uno existente o déjalo vacío para ejecutar con los **valores de parámetros predeterminados** del cBot. Un botón **+** junto al selector crea un nuevo conjunto de parámetros en línea (ver abajo) y lo selecciona.
- **Símbolo / Marco temporal** son por defecto `EURUSD` / `h1` y se pueden cambiar; **Cancelar** o **Ejecutar**.

Al **Ejecutar**, el editor guarda y compila el código fuente actual, inicia la instancia en la cuenta elegida con los parámetros elegidos y luego muestra los logs del contenedor en vivo. (El flujo de logs reenvía la cookie de autenticación del usuario conectado al hub de SignalR `/hubs/logs`, por lo que se conecta en vez de fallar con `Invalid negotiation response received`.)

## Conjuntos de parámetros

Un **conjunto de parámetros** es un conjunto con nombre y reutilizable de anulaciones de parámetros del cBot, almacenado como un objeto JSON plano que asigna cada nombre de parámetro a un valor escalar, p. ej. `{"Period": 14, "Label": "trend"}`. En el momento de ejecutar/backtestear se convierte en el archivo `params.cbotset` de cTrader (`{ "Parameters": { … } }`). Puedes crear/editar un conjunto como JSON sin formato desde el diálogo **Conjuntos de parámetros** del cBot o en línea desde el diálogo Ejecutar.

El JSON se **valida** al guardar: debe ser un único objeto plano cuyos valores sean todos escalares (cadena / número / booleano). Se rechaza una raíz que no sea objeto, un array, un objeto anidado, un valor `null` o un JSON mal formado (error claro en el diálogo, `400 Bad Request` en la API). Se permite un objeto vacío `{}` y significa «sin anulaciones».
