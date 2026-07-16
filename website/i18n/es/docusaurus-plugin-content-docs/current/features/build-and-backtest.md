---
description: "Construye, ejecuta y realiza backtests de cBots de cTrader (C# y Python, ambos .NET) desde el editor Monaco integrado en el navegador, ejecutados en la imagen oficial ghcr.io/spotware/ctrader-console."
---

# Construir y hacer backtest de cBots

Construye, ejecuta y realiza backtests de cBots de cTrader (C# **y** Python, ambos .NET) desde el
editor Monaco integrado en el navegador, ejecutados en la imagen oficial `ghcr.io/spotware/ctrader-console`.

## Construir

- La página **Builder** aloja el editor Monaco; `CBotBuilder` compila el proyecto con
  `dotnet build` **en un contenedor desechable** (`AppOptions.BuildImage`, directorio de trabajo montado
  en `/work`), para que los destinos MSBuild del usuario no alcancen el host. La restauración de NuGet se
  cachea entre compilaciones mediante un volumen compartido. El host web necesita acceso al socket de Docker.
- Las plantillas de inicio de C# y Python se encuentran en `src/Nodes/Builder/Templates/`.

## Ejecutar y hacer backtest

- **Instances** = jerarquía de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transición reemplaza la entidad (cambio de id),
  el id del contenedor se conserva.
- `NodeScheduler` elige el nodo elegible menos cargado; `ContainerDispatcherFactory` enruta
  al agente HTTP del nodo remoto o al distribuidor de Docker local.
- Los pollers de finalización reconcilian contenedores salidos (los contenedores de backtest salen
  automáticamente a través de `--exit-on-stop`); el reporte presente → completado (almacena `ReportJson`),
  ausente → falló.
- Los registros de contenedor en vivo se transmiten al navegador sobre SignalR; las curvas de equidad de
  backtest se analizan del reporte y se representan en gráficos.

## Los datos de mercado de backtest se cachean por cuenta

cTrader Console descarga datos históricos de tick/bar en su `--data-dir`. Ese directorio es un
**caché estable y persistente clave en la cuenta de negociación** (su número de cuenta) — montado
desde el disco del nodo en su propia ruta de contenedor (`/mnt/data`), un **montaje separado y no
anidado** del directorio de trabajo por instancia. Entonces cada backtest en la misma cuenta **reutiliza**
los datos ya descargados en lugar de re-descargarlos en cada ejecución. (Anteriormente el
directorio de datos vivía bajo el directorio de trabajo por instancia, cuya id cambiaba en cada ejecución,
lo que forzaba una descarga nueva en cada backtest.) El directorio de trabajo efímero por instancia aún
contiene el algo, parámetros, contraseña e informe; el caché de datos compartido se cuenta en el uso de
datos de backtest de un nodo y se borra por la acción de limpieza de nodos.

## Configuración de backtest

El diálogo **Backtest** expone la configuración de backtest de cTrader Console sintonizable por el
usuario, para que nunca tengas que tocar una línea de comandos:

- **Símbolo / Marco de tiempo** — el marco de tiempo es un **desplegable de cada período de cTrader**
  (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, y los períodos Renko/Range/Heikin),
  en el formato canónico de la consola, para que siempre elijas un `--period` válido.
- **Desde / Hasta** — la ventana de backtest (`--start` / `--end`).
- **Modo de datos** — uno de los tres modos de cTrader (`--data-mode`): **Datos de tick** (`tick`, preciso),
  **barras m1** (`m1`, rápido), o **Solo precios de apertura** (`open`, más rápido).
- **Saldo inicial** — por defecto `10000` (`--balance`). Un **saldo de 0 no coloca operaciones y hace que
  cTrader emita un informe vacío en el que luego se bloquea** ("Message expected"), por lo que siempre se
  envía un saldo distinto de cero.
- **Comisión** — `--commission`.
- **Spread** — `--spread`, un **campo numérico en pips que no puede ser menor que 0**. Está **oculto en el
  modo de datos de Tick**, donde cTrader deriva el spread de los propios datos de tick (no se envía `--spread`).

El directorio de datos (`--data-file` / `--data-dir`) es administrado por la propia aplicación (un caché
por cuenta, ver arriba), no se expone en el diálogo.

:::note cTrader se bloquea en un backtest vacío
Si un backtest produce **sin resultados** — sin operaciones, o sin datos de mercado para las fechas/símbolo
elegido — el escritor de informes propio de cTrader Console lanza `Message expected` y sale sin un informe.
La aplicación no puede reparar ese bug anterior, pero lo detecta y marca la instancia como **Failed** con un
motivo procesable ("no backtest results for the selected range…") en lugar de un seguimiento de pila bruto.
Elige un rango de fechas más amplio que tenga datos de mercado disponibles e intenta de nuevo.
:::

## Página de detalle de instancia

Abrir una instancia (`/instance/{id}`) muestra su estado en vivo, registros y — para un backtest — la
curva de equidad. El **título de la pestaña del navegador** refleja la instancia específica (**nombre del
cBot · tipo · símbolo**, por ejemplo `TrendBot · Backtest · EURUSD`) para que una pestaña de ejecución
en vivo y una pestaña de backtest sean distinguibles de un vistazo. Una ejecución y un backtest del mismo
cBot se rastrean como **linajes** distintos (un id de linaje estable llevado a través de transiciones de
estado), por lo que la página sigue exactamente una instancia y nunca mezcla datos de una ejecución con los
de un backtest.

## Controles del ciclo de vida de la instancia

Cada fila de instancia (y su página de detalle) tiene controles correctos para el estado. Una instancia
**activa** muestra **Detener**; una **terminal** (Stopped / Completed / Failed) muestra **Iniciar (▶)**
para relanzarla con el mismo cBot, cuenta, símbolo, marco de tiempo, conjunto de parámetros e imagen
(una ejecución se reinicia como ejecución, un backtest como backtest). Al hacer clic en Detener se
muestra un aviso "Stopping…" y se deshabilita el icono hasta que se resuelva, y se crea una ejecución
nueva aparece en la lista inmediatamente — sin recarga de página.

Los registros de consola se **persisten cuando una instancia termina** — para una ejecución (en Detener)
y para un **backtest** (en finalización) — para que los registros de la última ejecución permanezcan
visibles en la página de detalle y, a través de la barra de herramientas de registros, **copiado al
portapapeles** (icono Copiar registros) o **descargado** (icono Descargar registros) incluso después de
que el contenedor desaparezca. Ambos actúan sobre el registro de consola completo de la instancia, no
solo la cola en pantalla.

Un `.algo` **subido** nunca fue construido aquí, por lo que su columna **Last Build** en la página de
cBots queda en blanco (muestra una hora de compilación solo para cBots que construyes en el navegador).

## Editar y re-ejecutar una instancia detenida

Una instancia **detenida** (ejecución o backtest) tiene un control **Editar** — un icono en su fila en la
lista **y** junto a Iniciar/Detener en su página de detalle — que abre un diálogo **rellenado previamente**
con su configuración actual. Puedes cambiar la **cuenta de negociación, símbolo, marco de tiempo, conjunto
de parámetros e etiqueta de imagen** (y, para un backtest, la **ventana y todos los ajustes de backtest**
arriba), luego **Guardar e iniciar** relanzarlo con la nueva configuración (reemplazando la instancia
detenida). El control está **deshabilitado mientras la instancia está activa** — solo una instancia
detenida puede editarse.

## Ejecutar desde el editor de código

Al hacer clic en **Run** en el editor de código se abre un diálogo en lugar de ejecutar una ejecución
ciega y codificada:

- **Cuenta de negociación** (requerida) — la cuenta de cTrader a la que se conecta el cBot.
- **Conjunto de parámetros** (opcional) — elige un conjunto existente, o déjalo vacío para ejecutar con
  los **valores de parámetros predeterminados** del cBot. Un botón **+** junto al selector crea un nuevo
  conjunto de parámetros en línea (ver abajo) y lo selecciona.
- **Símbolo / Marco de tiempo** por defecto `EURUSD` / `h1` y se pueden cambiar; **Cancelar** o **Ejecutar**.

En **Ejecutar** el editor guarda + compila la fuente actual, inicia la instancia en la cuenta elegida
con los parámetros elegidos, luego rastrea los registros de contenedor en vivo. (La transmisión de registros
reenvía la cookie de autenticación del usuario conectado al hub SignalR `/hubs/logs`, por lo que se conecta
en lugar de fallar con `Invalid negotiation response received`.)

## Conjuntos de parámetros

Un **conjunto de parámetros** es un conjunto nombrado y reutilizable de anulaciones de parámetros de cBot
almacenado como un objeto JSON plano que asigna cada nombre de parámetro a un valor escalar, por ejemplo
`{"Period": 14, "Label": "trend"}`. En tiempo de ejecución/backtest se convierte en el archivo de cTrader
`params.cbotset` (`{ "Parameters": { … } }`). Puedes crear/editar un conjunto como JSON bruto desde el
diálogo **Parameter sets** del cBot o en línea desde el diálogo Ejecutar.

Cada conjunto de parámetros **pertenece a un cBot**: el diálogo Nuevo Conjunto de Parámetros enumera
todos tus cBots y **debes elegir uno** — la creación se bloquea hasta que se selecciona un cBot. El
**nombre de un conjunto es único por cBot**: crear o renombrar un conjunto a un nombre que otro conjunto
del mismo cBot ya usa es rechazado (un error claro en el diálogo, `409 Conflict` en la API). El mismo
nombre puede ser reutilizado en un **cBot diferente**.

El JSON se **valida** al guardar: debe ser un único objeto plano cuyos valores sean todos escalares
(cadena / número / booleano). Una raíz no objeto, una matriz, un objeto anidado, un valor `null`, o
JSON malformado es rechazado (un error claro en el diálogo, `400 Bad Request` en la API). Un objeto
vacío `{}` está permitido y significa "sin anulaciones".

## Notas de la CLI de cTrader Console

Los backtests necesitan `--data-mode` (por defecto `m1`), fechas como `dd/MM/yyyy HH:mm`, y el
argumento posicional JSON `params.cbotset`; `run` rechaza `--data-dir` (solo backtest). Ver
`ContainerCommandHelpers`.

## Nodos y escala

La capacidad de ejecución se escala agregando agentes de nodos (auto-registro + latido). Ver
[descubrimiento de nodos](../operations/node-discovery.md) y [escalado](../deployment/scaling.md).

## Se requiere una cuenta de negociación

Ejecutar o hacer backtest de un cBot necesita una cuenta de negociación de cTrader para conectarse. Hasta
que agregues una bajo **Trading accounts**, los botones **Run New cBot** / **Backtest New cBot** están
deshabilitados (con una información sobre herramientas) y la página muestra un aviso vinculando a la
configuración de cuenta — ya no golpeas un error bruto `stream connect failed` de un bot sin cuenta.
