---
description: "Crea, ejecuta y realiza backtests de cBots de cTrader (C# y Python, ambos .NET) desde el editor Monaco integrado en el navegador, ejecutados en la imagen oficial ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Crea, ejecuta y realiza backtests de cBots de cTrader (C# **y** Python, ambos .NET) desde el editor Monaco integrado en el navegador, ejecutados en la imagen oficial `ghcr.io/spotware/ctrader-console`.

## Build

- La página **Builder** aloja el editor Monaco; `CBotBuilder` compila el proyecto con `dotnet build` **en un contenedor desechable** (`AppOptions.BuildImage`, directorio de trabajo montado en `/work`), de modo que los destinos MSBuild del usuario no puedan acceder al host. La restauración de NuGet se guarda en caché entre compilaciones a través de un volumen compartido. El host web necesita acceso al socket de Docker.
- Las plantillas de inicio de C# y Python se encuentran en `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). La transición reemplaza la entidad (cambio de id), el id del contenedor se mantiene.
- `NodeScheduler` elige el nodo elegible menos cargado; `ContainerDispatcherFactory` enruta al agente HTTP del nodo remoto o al despachador local de Docker.
- Los pollers de finalización reconcilian los contenedores que salen (los contenedores de backtest se cierran automáticamente mediante `--exit-on-stop`); si el reporte está presente → completado (almacena `ReportJson`), si falta → falló.
- Los registros de contenedor en vivo se transmiten al navegador sobre SignalR; las curvas de equidad de backtest se analizan del reporte y se grafican.

## Backtest market data is cached per account

El cTrader Console descarga datos históricos de tick/bar en su `--data-dir`. Ese directorio es un **caché estable y persistente indexado por la cuenta de trading** (su número de cuenta) — montado desde el disco del nodo en su propia ruta de contenedor (`/mnt/data`), un **montaje separado y no anidado** del directorio de trabajo por instancia. Por lo tanto, cada backtest en la misma cuenta **reutiliza** los datos ya descargados en lugar de descargarlos de nuevo en cada ejecución. (Anteriormente el directorio de datos vivía bajo el directorio de trabajo por instancia, cuyo id cambiaba cada ejecución, lo que forzaba una descarga nueva en cada backtest.) El directorio de trabajo por instancia efímero aún contiene el algoritmo, parámetros, contraseña e informe; el caché de datos compartido se cuenta en el uso de datos de backtest de un nodo y se borra por la acción de limpieza del nodo.

## Backtest settings

El diálogo **Backtest** expone cada configuración que la CLI de backtest de cTrader Console acepta, por lo que nunca tienes que tocar una línea de comando:

- **From / To** — la ventana de backtest (`--start` / `--end`).
- **Data mode** — uno de los tres modos de cTrader (`--data-mode`): **Tick data** (`tick`, preciso), **m1 bars** (`m1`, rápido), u **Open prices only** (`open`, más rápido).
- **Starting balance** — por defecto es `10000` (`--balance`). Un **saldo de 0 no realiza transacciones y hace que cTrader emita un reporte vacío que luego falla** ("Message expected"), por lo que siempre se envía un saldo distinto de cero.
- **Commission** y **Spread** — `--commission` / `--spread` (spread en pips).
- **Data file** (opcional) — una ruta del lado del nodo a un archivo de datos históricos (`--data-file`); déjalo en blanco para usar los datos descargados/en caché.
- **Expose environment variables** — un interruptor que pasa las variables de entorno del host al cBot (la bandera `--environment-variables`).

## Instance detail page

Al abrir una instancia (`/instance/{id}`) se muestra su estado en vivo, registros y, para un backtest, la curva de equidad. El **título de la pestaña del navegador** refleja la instancia específica (**nombre del cBot · tipo · símbolo**, p. ej. `TrendBot · Backtest · EURUSD`) para que una pestaña de ejecución en vivo y una pestaña de backtest sean distinguibles de un vistazo. Una ejecución y un backtest del mismo cBot se rastrean como **linajes** distintos (un id de linaje estable mantenido a través de transiciones de estado), por lo que la página sigue exactamente una instancia y nunca mezcla datos de una ejecución con los de un backtest.

## Instance lifecycle controls

Cada fila de instancia (y su página de detalle) tiene controles correctos para el estado. Una instancia **activa** muestra **Stop**; una **terminal** (Stopped / Completed / Failed) muestra **Start (▶)** para reiniciarla con el mismo cBot, cuenta, símbolo, marco de tiempo, conjunto de parámetros e imagen (una ejecución se reinicia como ejecución, un backtest como backtest). Hacer clic en Stop muestra un aviso "Stopping…" y deshabilita el icono hasta que se resuelva, y una ejecución recién creada aparece en la lista inmediatamente — sin recarga de página.

Los registros de consola están **persistidos cuando una instancia termina** — tanto para una ejecución (en Stop) como para un **backtest** (en completación) — por lo que los registros de la última ejecución permanecen visibles en la página de detalle y, a través de la barra de herramientas de registros, se pueden **copiar al portapapeles** (icono de Copiar registros) o **descargar** (icono de Descargar registros) incluso después de que el contenedor desaparezca. Ambos actúan sobre el registro de consola completo de la instancia, no solo la cola en pantalla.

Un `.algo` **cargado** nunca fue construido aquí, por lo que su columna **Last Build** en la página de cBots queda en blanco (solo muestra una hora de compilación para los cBots que compilas en el navegador).

## Edit & re-run a stopped instance

Una instancia **detenida** (ejecución o backtest) tiene un control de **Edit** — un icono en su fila en la lista **y** junto a Start/Stop en su página de detalle — que abre un diálogo **prefillado** con su configuración actual. Puedes cambiar la **cuenta de trading, símbolo, marco de tiempo, conjunto de parámetros e imagen tag** (y, para un backtest, la **ventana y todas las configuraciones de backtest** anteriores), luego **Save & start** lo reinicia con la nueva configuración (reemplazando la instancia detenida). El control está **deshabilitado mientras la instancia está activa** — solo se puede editar una instancia detenida.

## Run from the code editor

Hacer clic en **Run** en el editor de código abre un diálogo en lugar de ejecutar una ejecución ciega y codificada:

- **Trading account** (requerida) — la cuenta de cTrader a la que se conecta el cBot.
- **Parameter set** (opcional) — elige un conjunto existente o déjalo en blanco para ejecutar con los **valores de parámetro predeterminados del cBot**. Un botón **+** junto al selector crea un nuevo conjunto de parámetros en línea (ver abajo) y lo selecciona.
- **Symbol / Timeframe** por defecto son `EURUSD` / `h1` y se pueden cambiar; **Cancel** o **Run**.

En **Run** el editor guarda + compila la fuente actual, inicia la instancia en la cuenta elegida con los parámetros seleccionados, luego mantiene los registros de contenedor en vivo. (La transmisión de registros reenvía la cookie de autenticación del usuario que inició sesión al hub SignalR `/hubs/logs`, por lo que se conecta en lugar de fallar con `Invalid negotiation response received`.)

## Parameter sets

Un **parameter set** es un conjunto nombrado y reutilizable de anulaciones de parámetros de cBot almacenado como un objeto JSON plano que mapea cada nombre de parámetro a un valor escalar, p. ej. `{"Period": 14, "Label": "trend"}`. En tiempo de ejecución/backtest se convierte en el archivo `params.cbotset` de cTrader (`{ "Parameters": { … } }`). Puedes crear/editar un conjunto como JSON bruto desde el diálogo **Parameter sets** del cBot o en línea desde el diálogo Run.

Cada conjunto de parámetros **pertenece a un cBot**: el diálogo New Parameter Set enumera todos tus cBots y **debes seleccionar uno** — la creación se bloquea hasta que se selecciona un cBot. El **nombre de un conjunto es único por cBot**: crear o renombrar un conjunto a un nombre que otro conjunto del mismo cBot ya usa se rechaza (un error claro en el diálogo, `409 Conflict` en la API). El mismo nombre se puede reutilizar en un **cBot diferente**.

El JSON se **valida** al guardar: debe ser un objeto plano único cuyos valores sean todos escalares (string / number / bool). Una raíz no-objeto, un array, un objeto anidado, un valor `null` o JSON malformado se rechaza (un error claro en el diálogo, `400 Bad Request` en la API). Un objeto vacío `{}` está permitido y significa "sin anulaciones".

## cTrader Console CLI notes

Los backtests necesitan `--data-mode` (por defecto `m1`), fechas como `dd/MM/yyyy HH:mm`, y argumento posicional JSON `params.cbotset`; `run` rechaza `--data-dir` (solo backtest). Ver `ContainerCommandHelpers`.

## Nodes & scale

La capacidad de ejecución se escala agregando agentes de nodo (auto-registro + heartbeat). Ver [node discovery](../operations/node-discovery.md) y [scaling](../deployment/scaling.md).

## A trading account is required

Ejecutar o hacer backtest de un cBot requiere una cuenta de trading de cTrader a la que conectarse. Hasta que agregues una bajo **Trading accounts**, los botones **Run New cBot** / **Backtest New cBot** están deshabilitados (con un tooltip) y la página muestra un prompt vinculando a la configuración de cuenta — ya no encuentras un error crudo `stream connect failed` de un bot sin cuenta.
