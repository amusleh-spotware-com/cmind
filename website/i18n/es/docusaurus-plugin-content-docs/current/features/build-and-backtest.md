---
description: "Compila, ejecuta y realiza backtests de cBots de cTrader (C# y Python, ambos .NET) desde el editor Monaco integrado en el navegador, ejecutados en la imagen oficial ghcr.io/spotware/ctrader-console."
---

# Compilar y hacer backtest de cBots

Compila, ejecuta y realiza backtests de cBots de cTrader (C# **y** Python, ambos .NET) desde el editor Monaco integrado en el navegador, ejecutados en la imagen oficial `ghcr.io/spotware/ctrader-console`.

## Compilar

- La página **Builder** aloja el editor Monaco; `CBotBuilder` compila el proyecto con `dotnet build` **en un contenedor desechable** (`AppOptions.BuildImage`, directorio de trabajo montado en `/work`), de modo que los objetivos de MSBuild de usuarios no confiables no puedan alcanzar el host. La restauración de NuGet se almacena en caché entre compilaciones a través de un volumen compartido. El host web necesita acceso al socket de Docker.
- Las plantillas de inicio de C# y Python se encuentran en `src/Nodes/Builder/Templates/`.

## Ejecutar y hacer backtest

- **Instances** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). La transición reemplaza la entidad (cambio de id), el id del contenedor se conserva.
- `NodeScheduler` selecciona el nodo elegible con menos carga; `ContainerDispatcherFactory` enruta al agente HTTP del nodo remoto o al despachador de Docker local.
- Los pollers de finalización concilian contenedores salidos (los contenedores de backtest salen automáticamente mediante `--exit-on-stop`); informe presente → completado (almacena `ReportJson`), faltante → falló.
- Los registros en vivo del contenedor se transmiten al navegador sobre SignalR; las curvas de equidad del backtest se analizan desde el informe y se grafican.

## Los datos del mercado del backtest se almacenan en caché por cuenta

La consola de cTrader descarga datos históricos de tick/bar en su `--data-dir`. Ese directorio es un **caché estable y persistente con clave en la cuenta comercial** (su número de cuenta), montado en bind desde el disco del nodo en su propia ruta de contenedor (`/mnt/data`), un **montaje separado y no anidado** del directorio de trabajo por instancia. Entonces cada backtest en la misma cuenta **reutiliza** los datos ya descargados en lugar de descargarlos nuevamente en cada ejecución. (Anteriormente, el directorio de datos vivía bajo el directorio de trabajo por instancia, cuyo id cambia en cada ejecución, lo que forzaba una descarga nueva en cada backtest.) El directorio de trabajo por instancia efímero aún contiene el algo, los parámetros, la contraseña y el informe; el caché de datos compartido se cuenta en el uso de datos de backtest de un nodo y se borra por la acción de limpieza del nodo.

## Configuración del backtest

El diálogo **Backtest** expone la configuración del backtest de cTrader Console ajustable por el usuario, para que nunca tengas que tocar una línea de comandos:

- **Symbol / Timeframe** — el timeframe es una **lista desplegable de cada período de cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, y los períodos Renko/Range/Heikin), en la mayúscula canónica de la consola, por lo que siempre seleccionas un `--period` válido.
- **From / To** — la ventana del backtest (`--start` / `--end`).
- **Data mode** — uno de los tres modos de cTrader (`--data-mode`): **Tick data** (`tick`, preciso), **m1 bars** (`m1`, rápido), u **Open prices only** (`open`, más rápido).
- **Starting balance** — por defecto `10000` (`--balance`). Un **saldo de 0 no coloca operaciones y hace que cTrader emita un informe vacío en el que luego se bloquea** ("Message expected"), por lo que siempre se envía un saldo distinto de cero.
- **Commission** y **Spread** — `--commission` / `--spread` (spread en pips).

El directorio de datos (`--data-file` / `--data-dir`) se gestiona por la aplicación misma (un caché por cuenta, ver arriba), no se expone en el diálogo.

## Página de detalle de instancia

Abrir una instancia (`/instance/{id}`) muestra su estado en vivo, registros y, para un backtest, la curva de equidad. El **título de la pestaña del navegador** refleja la instancia específica (**nombre del cBot · tipo · símbolo**, p. ej. `TrendBot · Backtest · EURUSD`) para que una pestaña de ejecución en vivo y una pestaña de backtest se puedan distinguir de un vistazo. Una ejecución y un backtest del mismo cBot se rastrean como **linajes** distintos (un id de linaje estable llevado a través de transiciones de estado), por lo que la página sigue exactamente una instancia y nunca mezcla los datos de una ejecución con los de un backtest.

## Controles del ciclo de vida de la instancia

Cada fila de instancia (y su página de detalle) tiene controles correctos del estado. Una instancia **activa** muestra **Stop**; una **terminal** (Stopped / Completed / Failed) muestra **Start (▶)** para relanzarla con el mismo cBot, cuenta, símbolo, timeframe, conjunto de parámetros e imagen (una ejecución se reinicia como ejecución, un backtest como backtest). Al hacer clic en Stop se muestra un aviso "Stopping…" y se deshabilita el icono hasta que se resuelve, y una ejecución recién creada aparece en la lista de inmediato — sin recarga de página.

Los registros de consola se **persisten cuando una instancia finaliza** — para una ejecución (en Stop) y para un **backtest** (en finalización) por igual — por lo que los registros de la última ejecución permanecen visibles en la página de detalle y, a través de la barra de herramientas de registro, **copiados al portapapeles** (icono Copy logs) o **descargados** (icono Download logs) incluso después de que el contenedor se ha ido. Ambos actúan en el registro de consola completo de la instancia, no solo en la cola en pantalla.

Un `.algo` **subido** nunca se compiló aquí, por lo que su columna **Last Build** en la página de cBots se deja en blanco (muestra una hora de compilación solo para cBots que compiles en el navegador).

## Editar y re-ejecutar una instancia detenida

Una instancia **detenida** (ejecución o backtest) tiene un control de **Edit** — un icono en su fila en la lista **y** al lado de Start/Stop en su página de detalle — que abre un diálogo **prefilled** con su configuración actual. Puedes cambiar la **cuenta comercial, símbolo, timeframe, conjunto de parámetros e etiqueta de imagen** (y, para un backtest, la **ventana y toda la configuración del backtest** arriba), luego **Save & start** lo relanza con los nuevos ajustes (reemplazando la instancia detenida). El control se **deshabilita mientras la instancia está activa** — solo una instancia detenida se puede editar.

## Ejecutar desde el editor de código

Al hacer clic en **Run** en el editor de código se abre un diálogo en lugar de disparar una ejecución ciega codificada:

- **Trading account** (requerida) — la cuenta de cTrader a la que se conecta el cBot.
- **Parameter set** (opcional) — elige un conjunto existente, o déjalo vacío para ejecutar con los **valores de parámetro predeterminados del cBot**. Un botón **+** al lado del selector crea un nuevo conjunto de parámetros en línea (ver abajo) y lo selecciona.
- **Symbol / Timeframe** se establecen de forma predeterminada en `EURUSD` / `h1` y se pueden cambiar; **Cancel** o **Run**.

En **Run** el editor guarda + compila el código fuente actual, inicia la instancia en la cuenta elegida con los parámetros elegidos, luego rastrea los registros de contenedor en vivo. (El flujo de registro reenvía la cookie de autenticación del usuario registrado al hub SignalR `/hubs/logs`, por lo que se conecta en lugar de fallar con `Invalid negotiation response received`.)

## Conjuntos de parámetros

Un **parameter set** es un conjunto nombrado y reutilizable de anulaciones de parámetros de cBot almacenados como un objeto JSON plano que asigna cada nombre de parámetro a un valor escalar, p. ej. `{"Period": 14, "Label": "trend"}`. En tiempo de ejecución/backtest se convierte en el archivo `params.cbotset` de cTrader (`{ "Parameters": { … } }`). Puedes crear/editar un conjunto como JSON sin procesar desde el diálogo **Parameter sets** del cBot o en línea desde el diálogo Run.

Cada conjunto de parámetros **pertenece a un cBot**: el diálogo New Parameter Set enumera todos tus cBots y **debes elegir uno** — la creación se bloquea hasta que se selecciona un cBot. El **nombre de un conjunto es único por cBot**: crear o renombrar un conjunto con un nombre que otro conjunto del mismo cBot ya usa se rechaza (un error claro en el diálogo, `409 Conflict` en la API). El mismo nombre se puede reutilizar en un **cBot diferente**.

El JSON está **validado** al guardar: debe ser un único objeto plano cuyos valores sean todos escalares (cadena / número / bool). Una raíz que no sea objeto, una matriz, un objeto anidado, un valor `null`, o JSON mal formado se rechaza (un error claro en el diálogo, `400 Bad Request` en la API). Un objeto vacío `{}` es permitido y significa "sin anulaciones".

## Notas sobre CLI de cTrader Console

Los backtests necesitan `--data-mode` (por defecto `m1`), fechas como `dd/MM/yyyy HH:mm`, y argumento posicional JSON `params.cbotset`; `run` rechaza `--data-dir` (solo backtest). Ver `ContainerCommandHelpers`.

## Nodos y escala

La capacidad de ejecución se escala agregando agentes de nodo (auto-registro + latido). Ver [node discovery](../operations/node-discovery.md) y [scaling](../deployment/scaling.md).

## Se requiere una cuenta comercial

Ejecutar o hacer backtest de un cBot requiere una cuenta comercial de cTrader a la que conectarse. Hasta que agregues una en **Trading accounts**, los botones **Run New cBot** / **Backtest New cBot** están deshabilitados (con una información sobre herramientas) y la página muestra un aviso vinculando a la configuración de la cuenta — ya no golpeas un error bruto `stream connect failed` de un bot sin cuenta.
