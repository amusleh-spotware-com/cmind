---
description: "Construir, ejecutar, hacer backtesting de cBots de cTrader (C# y Python, ambos en .NET) desde el IDE Monaco integrado en el navegador, ejecutar en la imagen oficial ghcr.io/spotware/ctrader-console."
---

# Construir y hacer backtesting de cBots

Construir, ejecutar, hacer backtesting de cBots de cTrader (C# **y** Python, ambos en .NET) desde el IDE Monaco integrado en el navegador, ejecutar en la imagen oficial `ghcr.io/spotware/ctrader-console`.

## Construir

- La página **Builder** aloja el editor Monaco; `CBotBuilder` compila el proyecto con `dotnet build` **en un contenedor desechable** (`AppOptions.BuildImage`, directorio de trabajo montado en bind en `/work`), de modo que los objetivos MSBuild de usuarios no confiables no accedan al host. La restauración de NuGet se cachea entre compilaciones a través de un volumen compartido. El host web necesita acceso al socket de Docker.
- Las plantillas de inicio de C# y Python viven en `src/Nodes/Builder/Templates/`.

## Ejecutar y hacer backtesting

- **Instances** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Las transiciones reemplazan la entidad (cambio de id), el id del contenedor se lleva adelante.
- `NodeScheduler` elige el nodo elegible menos cargado; `ContainerDispatcherFactory` enruta al agente HTTP del nodo remoto o al despachador Docker local.
- Los pollers de finalización reconcilian contenedores salidos (los contenedores de backtesting salen por sí solos a través de `--exit-on-stop`); reporte presente → completado (almacena `ReportJson`), faltante → fallido.
- Los registros del contenedor en vivo se transmiten al navegador a través de SignalR; las curvas de capital de backtesting se analizan desde el reporte y se grafican.

## Los datos de mercado de backtesting se cachean por cuenta

cTrader Console descarga datos históricos de tick/bar en su `--data-dir`. Ese directorio es un **caché estable y persistente con clave en la cuenta comercial** (su número de cuenta) — montado en bind desde el disco del nodo en su propia ruta de contenedor (`/mnt/data`), un **montaje separado y no anidado** del directorio de trabajo por instancia. Por lo tanto, cada backtesting en la misma cuenta **reutiliza** los datos ya descargados en lugar de descargarlos nuevamente en cada ejecución. (Anteriormente el directorio de datos vivía bajo el directorio de trabajo por instancia, cuyo id cambia en cada ejecución, lo que forzaba una descarga nueva en cada backtesting.) El directorio de trabajo por instancia efímero aún contiene el algoritmo, los parámetros, la contraseña y el reporte; el caché de datos compartido se cuenta en el uso de datos de backtesting de un nodo y se borra mediante la acción de limpieza del nodo.

## Configuración de backtesting

El diálogo **Backtest** expone la configuración de backtesting de cTrader Console ajustable por el usuario, para que nunca tenga que tocar una línea de comandos:

- **Symbol / Timeframe** — el timeframe es un **desplegable de cada período de cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, y los períodos Renko/Range/Heikin), en las mayúsculas canónicas de la consola, para que siempre elija un `--period` válido.
- **From / To** — la ventana de backtesting (`--start` / `--end`).
- **Data mode** — uno de los tres modos de cTrader (`--data-mode`): **Tick data** (`tick`, preciso), **barras m1** (`m1`, rápido), u **Open prices only** (`open`, más rápido).
- **Starting balance** — por defecto `10000` (`--balance`). Un **saldo de 0 no coloca operaciones y hace que cTrader emita un reporte vacío que luego falla** ("Message expected"), por lo que siempre se envía un saldo distinto de cero.
- **Commission** — `--commission`.
- **Spread** — `--spread`, un **campo numérico en pips que no puede ser menor a 0**. Está **oculto en el modo Tick data**, donde cTrader derive el spread de los datos de tick (no se envía `--spread`).

El directorio de datos (`--data-file` / `--data-dir`) es gestionado por la aplicación misma (un caché por cuenta, ver arriba), no expuesto en el diálogo.

:::note cTrader se bloquea en un backtesting vacío
Si un backtesting produce **sin resultados** — sin operaciones, o sin datos de mercado para las fechas/símbolo elegidos — el escritor de reportes de cTrader Console arroja `Message expected` y sale sin un reporte. La aplicación no puede solucionar ese error aguas arriba, pero lo detecta y marca la instancia como **Failed** con una razón accionable ("no backtest results for the selected range…") en lugar de un rastro de pila sin procesar. Elija un rango de fechas más amplio que tenga datos de mercado disponibles e intente nuevamente.
:::

## Página de detalle de instancia

Abrir una instancia (`/instance/{id}`) muestra su estado en vivo, registros y — para un backtesting — la curva de capital. El **título de la pestaña del navegador** refleja la instancia específica (**nombre del cBot · tipo · símbolo**, p. ej. `TrendBot · Backtest · EURUSD`) de modo que una pestaña de ejecución en vivo y una pestaña de backtesting sean distinguibles de un vistazo. Una ejecución y un backtesting del mismo cBot se rastrean como **linajes** distintos (un id de linaje estable llevado a través de transiciones de estado), por lo que la página sigue exactamente una instancia y nunca mezcla los datos de una ejecución con los de un backtesting.

## Controles de ciclo de vida de instancia

Cada fila de instancia (y su página de detalle) tiene controles correctos de estado. Una instancia **activa** muestra **Stop**; una **terminal** (Stopped / Completed / Failed) muestra **Start (▶)** para relanzarla con el mismo cBot, cuenta, símbolo, timeframe, conjunto de parámetros e imagen (una ejecución se reinicia como una ejecución, un backtesting como un backtesting). Hacer clic en Stop muestra un aviso "Stopping…" y desactiva el ícono hasta que se resuelve, y una ejecución recién creada aparece en la lista inmediatamente — sin recarga de página.

Los registros de consola se **persisten cuando una instancia termina** — para una ejecución (en Stop) y para un **backtesting** (en la finalización) por igual — para que los registros de la última ejecución permanezcan visibles en la página de detalle y, a través de la barra de herramientas de registro, **copiados al portapapeles** (ícono Copy logs) o **descargados** (ícono Download logs) incluso después de que el contenedor se haya ido. Ambos actúan en el registro de consola completo de la instancia, no solo la cola en pantalla.

Un **backtesting completado** también persiste su **reporte de cTrader** en ambos formatos — el **JSON** sin procesar (el mismo que la curva de capital y el análisis de IA leen) y el reporte **HTML** completo. Ambos se pueden descargar desde la fila de backtesting **y** la página de detalle a través de iconos dedicados. Solo se conservan los **reportes de la última ejecución**, y los iconos están **desactivados** para cualquier backtesting que no se haya iniciado, esté en ejecución o haya fallado (y nunca se muestran para una instancia de ejecución) — solo un backtesting completado tiene un reporte para descargar.

Un `.algo` **cargado** nunca fue construido aquí, por lo que su columna **Last Build** en la página de cBots se deja en blanco (muestra un tiempo de compilación solo para cBots que construye en el navegador).

## Editar y re-ejecutar una instancia detenida

Una instancia **detenida** (ejecución o backtesting) tiene un control **Edit** — un ícono en su fila en la lista **y** al lado de Start/Stop en su página de detalle — que abre un diálogo **prefyllado** con su configuración actual. Puede cambiar la **cuenta comercial, símbolo, timeframe, conjunto de parámetros e etiqueta de imagen** (y, para un backtesting, la **ventana y todas las configuraciones de backtesting** arriba), luego **Save & start** la relanza con la nueva configuración (reemplazando la instancia detenida). El control está **desactivado mientras la instancia está activa** — solo una instancia detenida puede ser editada.

## Ejecutar desde el editor de código

Hacer clic en **Run** en el editor de código abre un diálogo en lugar de disparar una ejecución ciega y codificada:

- **Trading account** (requerido) — la cuenta de cTrader a la que el cBot se conecta.
- **Parameter set** (opcional) — elija un conjunto existente, o déjelo vacío para ejecutar con los **valores de parámetros predeterminados** del cBot. Un botón **+** junto al selector crea un nuevo conjunto de parámetros en línea (ver abajo) y lo selecciona.
- **Symbol / Timeframe** tienen valores predeterminados de `EURUSD` / `h1` y pueden cambiarse; **Cancel** o **Run**.

En **Run** el editor guarda + compila la fuente actual, inicia la instancia en la cuenta elegida con los parámetros elegidos, luego rastrea los registros del contenedor en vivo. (El flujo de registro reenvía la cookie de autenticación del usuario que inició sesión al hub SignalR `/hubs/logs`, para que se conecte en lugar de fallar con `Invalid negotiation response received`.)

## Conjuntos de parámetros

Un **parameter set** es un conjunto nombrado y reutilizable de anulaciones de parámetros de cBot almacenado como un objeto JSON plano que asigna cada nombre de parámetro a un valor escalar, p. ej. `{"Period": 14, "Label": "trend"}`. En tiempo de ejecución/backtesting se convierte en el archivo `params.cbotset` de cTrader (`{ "Parameters": { … } }`). Puede crear/editar un conjunto como JSON sin procesar desde el diálogo **Parameter sets** del cBot o en línea desde el diálogo Run.

Cada conjunto de parámetros **pertenece a un cBot**: el diálogo New Parameter Set enumera todos sus cBots y **debe elegir uno** — la creación se bloquea hasta que se selecciona un cBot. El **nombre de un conjunto es único por cBot**: crear o renombrar un conjunto a un nombre que otro conjunto del mismo cBot ya usa es rechazado (un error claro en el diálogo, `409 Conflict` en la API). El mismo nombre puede reutilizarse en un **cBot diferente**.

El JSON se **valida** al guardar: debe ser un único objeto plano cuyos valores sean todos escalares (string / número / bool). Una raíz no-objeto, una matriz, un objeto anidado, un valor `null`, o JSON malformado es rechazado (un error claro en el diálogo, `400 Bad Request` en la API). Un objeto vacío `{}` está permitido e indica "sin anulaciones".

## Notas de línea de comandos de cTrader Console

Los backtestings necesitan `--data-mode` (por defecto `m1`), fechas como `dd/MM/yyyy HH:mm`, y argumento JSON posicional `params.cbotset`; `run` rechaza `--data-dir` (solo backtesting). Ver `ContainerCommandHelpers`.

## Nodos y escala

La capacidad de ejecución se escala agregando agentes de nodos (auto-registro + heartbeat). Ver [node discovery](../operations/node-discovery.md) y [scaling](../deployment/scaling.md).

## Se requiere una cuenta comercial

Ejecutar o hacer backtesting de un cBot requiere una cuenta comercial de cTrader a la que conectarse. Hasta que agregue una en **Trading accounts**, los botones **Run New cBot** / **Backtest New cBot** están desactivados (con una información sobre herramienta) y la página muestra una solicitud que vincula a la configuración de cuenta — ya no golpea un error `stream connect failed` sin procesar de un bot sin cuenta.
