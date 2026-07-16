---
description: "Construir, ejecutar y hacer backtesting de cBots de cTrader (C# y Python, ambos .NET) desde el editor en navegador Monaco, ejecutar en la imagen oficial ghcr.io/spotware/ctrader-console."
---

# Construir y hacer backtesting de cBots

Construir, ejecutar y hacer backtesting de cBots de cTrader (C# **y** Python, ambos .NET) desde el editor en navegador Monaco, ejecutar en la imagen oficial `ghcr.io/spotware/ctrader-console`.

## Construir

- La página **Builder** aloja el editor Monaco; `CBotBuilder` compila el proyecto con
  `dotnet build` **en contenedor desechable** (`AppOptions.BuildImage`, directorio de trabajo bind-mount
  en `/work`), para que los objetivos MSBuild del usuario no alcancen el host. La restauración de NuGet se cachea
  entre compilaciones a través de un volumen compartido. El host web necesita acceso al socket Docker.
- Las plantillas iniciales de C# + Python viven en `src/Nodes/Builder/Templates/`.

## Ejecutar y hacer backtesting

- **Instances** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Las transiciones reemplazan la entidad (cambio de id),
  se lleva la id del contenedor.
- `NodeScheduler` elige el nodo elegible menos cargado; `ContainerDispatcherFactory` enruta a
  agente HTTP de nodo remoto o distribuidor Docker local.
- Los pollers de finalización reconcilian contenedores cerrados (los contenedores de backtest se cierran automáticamente a través de
  `--exit-on-stop`); si el reporte está presente → completado (almacenar `ReportJson`), si falta → falló.
- Los registros de contenedor en vivo se transmiten al navegador sobre SignalR; las curvas de patrimonio de backtest se analizan desde el
  reporte y se grafican.

## Los datos del mercado de backtesting se cachean por cuenta

La consola cTrader descarga datos de tick/barra históricos en su `--data-dir`. Ese directorio es un
**caché estable y persistente con clave en la cuenta de trading** (su número de cuenta) — bind-montado desde el disco del nodo en su propia ruta de contenedor (`/mnt/data`), un **montaje separado y no anidado** del
directorio de trabajo por instancia. Así que cada backtest en la misma cuenta **reutiliza** los datos
ya descargados en lugar de descargarlos nuevamente en cada ejecución. (Antes, el
directorio de datos vivía bajo el directorio de trabajo por instancia, cuya id cambiaba con cada ejecución, lo que forzaba una descarga
fresca en cada backtest.) El directorio de trabajo por instancia efímero todavía contiene el algoritmo, parámetros, contraseña
y reporte; el caché de datos compartido se cuenta en el uso de datos de backtesting de un nodo y se limpia por la
acción de limpieza de nodo.

## Configuración de backtesting

El diálogo **Backtest** expone cada configuración que la CLI de backtesting de la consola cTrader acepta, para que nunca
tengas que tocar una línea de comandos:

- **From / To** — la ventana de backtest (`--start` / `--end`).
- **Data mode** — `m1` (barras de 1 minuto) o `tick` (`--data-mode`).
- **Starting balance** — por defecto `10000` (`--balance`). Un **saldo de 0 no coloca operaciones y hace que cTrader emita un reporte vacío que luego causa un crash** ("Message expected"), por lo que siempre se envía un saldo distinto de cero.
- **Commission** y **Spread** (`--commission` / `--spread`, spread en pips).
- **Advanced options** — caja de forma libre `name=value` por línea para cualquier otra opción de backtesting que cTrader
  soporta (p. ej. `applyCommissionAutomatically=true`); cada línea se convierte en un argumento CLI `--name value`.

## Página de detalle de instancia

Abrir una instancia (`/instance/{id}`) muestra su estado en vivo, registros y — para un backtest — la curva de patrimonio.
El **título de la pestaña del navegador** refleja la instancia específica (**nombre del cBot · tipo · símbolo**, p. ej.
`TrendBot · Backtest · EURUSD`) para que una pestaña de ejecución en vivo y una pestaña de backtest se distingan de un vistazo.
Una ejecución y un backtest del mismo cBot se rastrean como **linajes** distintos (una id de linaje estable llevada
a través de transiciones de estado), así que la página sigue exactamente una instancia y nunca mezcla los datos de una ejecución con los de un
backtest.

## Controles de ciclo de vida de la instancia

Cada fila de instancia (y su página de detalle) tiene controles correctos según el estado. Una instancia **activa** muestra
**Stop**; una **terminal** (Stopped / Completed / Failed) muestra **Start (▶)** para reiniciarla con
el mismo cBot, cuenta, símbolo, marco de tiempo, conjunto de parámetros e imagen (una ejecución se reinicia como ejecución, un
backtest como backtest). Hacer clic en Stop muestra un aviso "Stopping…" y deshabilita el icono hasta que se
resuelve, y una ejecución recién creada aparece en la lista inmediatamente — sin recarga de página.

Los registros de consola se **persisten cuando una instancia termina** — para una ejecución (en Stop) y para un
**backtest** (en finalización) — por lo que los registros de la última ejecución permanecen visibles en la página de detalle y,
a través de la barra de herramientas de registro, **copiados al portapapeles** (icono Copy logs) o **descargados** (icono Download logs)
incluso después de que el contenedor se haya ido. Ambos actúan en el registro de consola completo de la instancia, no solo en la
cola en pantalla.

Un `.algo` **cargado** nunca fue construido aquí, por lo que su columna **Last Build** en la página de cBots se deja
en blanco (solo muestra una hora de compilación para cBots que construyes en el navegador).

## Editar y re-ejecutar una instancia detenida

Una instancia **detenida** (ejecución o backtest) tiene un control **Edit** — un icono en su fila en la lista **y**
al lado de Start/Stop en su página de detalle — que abre un diálogo **prerellenado** con su configuración actual.
Puedes cambiar la **cuenta de trading, símbolo, marco de tiempo, conjunto de parámetros e imagen tag** (y, para un
backtest, la **ventana y todas las configuraciones de backtest** anteriores), luego **Save & start** la reinicia con las
nuevas configuraciones (reemplazando la instancia detenida). El control está **deshabilitado mientras la instancia está activa** —
solo una instancia detenida puede ser editada.

## Ejecutar desde el editor de código

Hacer clic en **Run** en el editor de código abre un diálogo en lugar de ejecutar una ejecución ciega y codificada:

- **Trading account** (requerida) — la cuenta de cTrader a la que se conecta el cBot.
- **Parameter set** (opcional) — elige un conjunto existente, o déjalo vacío para ejecutar con los **valores de parámetro predeterminados del cBot**. Un botón **+** junto al selector crea un nuevo conjunto de parámetros
  en línea (ver abajo) y lo selecciona.
- **Symbol / Timeframe** tienen por defecto `EURUSD` / `h1` y pueden cambiar; **Cancel** o **Run**.

En **Run** el editor guarda + compila la fuente actual, inicia la instancia en la cuenta elegida
con los parámetros elegidos, luego rastrea los registros de contenedor en vivo. (La corriente de registro reenvía la cookie de autenticación del usuario conectado al hub SignalR `/hubs/logs`, por lo que se conecta en lugar de fallar con
`Invalid negotiation response received`.)

## Conjuntos de parámetros

Un **parameter set** es un conjunto nombrado y reutilizable de anulaciones de parámetros de cBot almacenados como un
objeto JSON plano que asigna cada nombre de parámetro a un valor escalar, p. ej. `{"Period": 14, "Label": "trend"}`. En
tiempo de ejecución/backtest se convierte en el archivo `params.cbotset` de cTrader
(`{ "Parameters": { … } }`). Puedes crear/editar un conjunto como JSON puro desde el diálogo **Parameter
sets** del cBot o en línea desde el diálogo Run.

Cada conjunto de parámetros **pertenece a un cBot**: el diálogo New Parameter Set lista todos tus cBots y debes
**elegir uno** — la creación se bloquea hasta que se selecciona un cBot. El **nombre de un conjunto es único por cBot**:
crear o renombrar un conjunto a un nombre que otro conjunto del mismo cBot ya usa es rechazado (un error claro
en el diálogo, `409 Conflict` en la API). El mismo nombre puede reutilizarse en un **cBot diferente**.

El JSON se **valida** al guardar: debe ser un único objeto plano cuyos valores sean todos escalares
(string / number / bool). Una raíz no-objeto, un array, un objeto anidado, un valor `null`, o un JSON
malformado es rechazado (un error claro en el diálogo, `400 Bad Request` en la API). Un objeto vacío `{}`
está permitido y significa "sin anulaciones".

## Notas de la CLI de cTrader Console

Los backtests necesitan `--data-mode` (por defecto `m1`), fechas como `dd/MM/yyyy HH:mm`, y
el argumento posicional JSON `params.cbotset`; `run` rechaza `--data-dir` (solo backtest). Ver
`ContainerCommandHelpers`.

## Nodos y escalabilidad

La capacidad de ejecución se escala agregando agentes de nodo (auto-registro + heartbeat). Ver
[node discovery](../operations/node-discovery.md) y [scaling](../deployment/scaling.md).

## Se requiere una cuenta de trading

Ejecutar o hacer backtesting de un cBot necesita una cuenta de trading de cTrader para conectarse. Hasta que agregues una bajo
**Trading accounts**, los botones **Run New cBot** / **Backtest New cBot** están deshabilitados (con un
tooltip) y la página muestra un aviso que vincula a la configuración de cuenta — ya no obtendrás un error crudo
`stream connect failed` de un bot sin cuenta.
