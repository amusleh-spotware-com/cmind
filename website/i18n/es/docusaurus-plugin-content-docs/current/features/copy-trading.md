---
description: "Espeja la cuenta cTrader maestra en una o más cuentas esclavas — entre brokers, entre cID — con control por destino + reconciliación de calidad monetaria."
---

# Copia de operaciones

Espeja la cuenta cTrader **maestra** en una o más cuentas **esclavas** — entre brokers, entre cID — con control por destino + reconciliación de calidad monetaria.

## Conceptos

- **Perfil de copia** — una maestra (`SourceAccountId`) + uno o más **destinos**. Ciclo de vida: `Draft → Running → Paused → Stopped` (`Error` en caso de fallo). Raíz agregada: `CopyProfile` (posee `CopyDestination`).
- **Destino** — una cuenta esclava + conjunto completo de reglas para cómo se copia la maestra en ella. Toda la configuración es por destino, así que una maestra alimenta a esclavas conservadoras y agresivas simultáneamente.
- **Host del motor de copia** — worker en ejecución para el perfil (`CopyEngineHost`). Se suscribe a la secuencia de ejecución maestra, aplica cada evento a cada destino.
- **Supervisor** — `CopyEngineSupervisor`, servicio de fondo en cada nodo. Aloja perfiles asignados, se auto-repara entre clusters (ver [escalado](../deployment/scaling.md)).

## Qué se espeja

| Evento maestro | Acción esclava |
|--------------|--------------|
| Posición abierta de mercado / rango de mercado | Abrir una copia dimensionada (etiquetada con el id de posición fuente) |
| Orden pendiente de límite / parada / límite de parada | Colocar la orden pendiente coincidente |
| Enmienda de orden pendiente | Enmendar la orden pendiente reflejada en su lugar |
| Cancelación de orden pendiente / vencimiento | Cancelar la orden pendiente reflejada |
| Cierre parcial | Cerrar la misma proporción de la posición esclava |
| Escala-in (aumento de volumen) | Abrir el volumen añadido (opcional) |
| Cambio de parada de pérdida / parada de seguimiento | Enmendar la protección de la posición esclava |
| Cierre completo | Cerrar la copia esclava |

Cada copia **etiquetada con id de posición/orden fuente**. Después de reconectar, el host reconstruye el estado desde reconciliación: abre copias que maestro tiene pero esclavo no tiene, cierra "huérfanos" esclavos que maestro ya no tiene — **sin duplicar operaciones**.

## Creación de un perfil

El **Nuevo Perfil** abre un formulario **de página completa** dedicado (`/copy-trading/new`), no un diálogo — el conjunto de opciones es lo suficientemente grande para que una página se lea mejor en teléfono y escritorio. Recopila todo por adelantado: nombre del perfil, cuenta fuente (maestra), cuentas de destino (esclava) (selección múltiple con botón **Seleccionar todo**; maestro elegido excluido de lista esclava), + el conjunto completo de opciones por destino. **Solo las cuentas vinculadas a través de la API abierta de cTrader son seleccionables** como maestra o destino — la copia coloca órdenes a través de la API abierta, por lo que una cuenta añadida manualmente (solo cID) no puede copiar y no se enumera; cuando ninguna está vinculada, la página muestra un aviso apuntando a Cuentas de Negociación. Los modos de dimensionamiento, dirección y filtro de símbolo se **representan como etiquetas legibles con una explicación con viñetas por modo** en la ayuda de gestión de dinero. **Cada control lleva un tooltip de ayuda** explicando qué hace y cómo usarlo. Las entradas estructuradas usan **controles validados adecuados** — números/porcentaje vía campos numéricos, modos/dirección/filtro vía selecciones, filtro de símbolo vía lista de chips de símbolo agregar/quitar, y mapa de símbolos vía tabla agregar/quitar de filas `Fuente → Destino (× multiplicador)` — nunca un blob de texto separado por comas. Todas las entradas **validadas antes de guardar** — nombre/fuente/destino faltante, parámetro de dimensionamiento no positivo, límites de lote negativos/inconsistentes, porcentaje de reducción fuera de rango, sin tipo de orden habilitado, o filtro de símbolo vacío aparecen como lista de errores + bloquean guardado. Al crear, el perfil se crea + cada esclavo seleccionado se añade con la configuración elegida, luego la página retorna a la lista de Copia de Operaciones.

**Importar / exportar.** El bloque de configuración completo puede ser **exportado a un archivo JSON** e **importado** nuevamente para rellenar el formulario, para que una sintonización se reutilice entre perfiles sin reescribir. El mapa de símbolos igualmente puede ser **exportado / importado como archivo CSV** (`Fuente,Destino,VolumeMultiplier`) — prepara un gran mapa de símbolos de broker en una hoja de cálculo y cárgalo en un paso. Los mismos controles de símbolos e importación/exportación de CSV también están disponibles en el diálogo de destino en la página de Copia de Operaciones.

Las acciones de fila respetan el ciclo de vida: **Iniciar** habilitado solo cuando no se está ejecutando, **Detener** + **Pausar** solo cuando se está ejecutando, **Eliminar** deshabilitado mientras se ejecuta + pide confirmación antes de eliminar perfil + destinos.

## Opciones por destino

Establecido en la página Nuevo Perfil, en el diálogo de destino en la página de Copia de Operaciones, o vía `POST /api/copy/profiles/{id}/destinations`:

- **Dimensionamiento** (`MoneyManagementMode` + parámetro): lote fijo, multiplicador de lote/nocional, balance/equity/margen libre proporcional, riesgo fijo %, apalancamiento fijo, auto-proporcional, **riesgo-%-desde-parada** (M7). Más límites de lote mín/máx + fuerza-lote-mín. **Riesgo-desde-parada** dimensiona destino para arriesgar porcentaje configurado de su propio *balance*, derivado de **distancia de parada de pérdida maestra** (`maestro arriesga 2% → esclavo auto-arriesga 2%`): `lotes = balance×% ÷ (distancia_parada × tamaño_contrato)`. Maestro abierto **sin** parada de pérdida no tiene distancia para dimensionar contra → usa **lote de reserva de riesgo máximo** configurado (M7) si está establecido, sino se salta (`no_stop_loss`) no adivinado. Proporcional-**equity**/**margen libre** dimensiona desde **equity** real de cuenta (`balance + Σ P&L flotante`, derivado por cTrader Open API que no entrega equity), no balance simple — así maestro sentado en ganancia/pérdida abierta dimensiona copias correctamente. Margen usado no expuesto por API de reconciliación, así que margen libre tratado como equity (proxy honesto de fondos disponibles); otros modos leen balance + saltan ronda de revaluación extra.
- **Filtro de dirección**: ambas / solo largo / solo corto. **Invertir**: voltea lado (+ intercambia SL↔TP) para copia contraria.
- **Solo-gestionar** (Ignorar-Operaciones-Nuevas / Solo-Cerrar): espeja cierres, cierres parciales + cambios de protección en posiciones ya copiadas, pero abre **no** nuevas posiciones/órdenes pendientes (se salta `manage_only`). Usar para reducir destino sin cortar copias existentes.
- **Sincronizar-Abierto-al-inicio** / **Sincronizar-Cerrado-al-inicio** (por defecto activado): en **primer** resincronización del perfil, si abrir copias para posiciones preexistentes maestras, + si cerrar copias maestro cerradas mientras perfil parado. Ambas se aplican solo al inicio — resincronización de reconexión mid-run siempre reconcilia completamente para que desincronización se recupere de todos modos.
- **Mapa de símbolos** + **filtro de símbolos** (lista blanca / lista negra). Cada entrada de mapa de símbolos lleva **multiplicador de volumen por símbolo** opcional (anulación por símbolo de cMAM) escala tamaño de copia para ese símbolo en destino de dimensionamiento (1 = sin cambio). Mapa completo importa/exporta como **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; columnas `Fuente,Destino,VolumeMultiplier`) — cada fila validada a través de objetos de valor de dominio, así archivo malformado no puede producir mapa inválido.
- **Ventana de horas de operación** (C18) — ventana UTC diaria por destino (`inicio`/`fin` minutos del día, fin exclusivo; `inicio == fin` = todo el día). Nuevas aperturas fuera de ventana se saltan (`trading_hours`); ventana con `inicio > fin` envuelve pasada medianoche (ej. 22:00–06:00). Posiciones existentes siguen siendo gestionadas.
- **Filtro de etiqueta fuente** (C18, equivalente cTrader de filtro de número mágico MT) — cuando está establecido, copia solo operaciones maestras cuya etiqueta coincida **exactamente** (ej. operaciones de un bot, o etiqueta solo manual); sino se salta (`source_label`). Vacío = copia todo. Llevado en `ExecutionEvent.SourceLabel` desde `TradeData.Label` de posición/orden maestra, honrado en resincronización también.
- **Protección de cuenta** (ZuluGuard / Protección Global de Cuenta) — observa **equity en vivo** de destino (`balance + Σ P&L flotante`, sondeado cada `CopyDefaults.EquityGuardInterval`) contra piso `StopEquity` y/o techo `TakeEquity` opcional. En incumplimiento, aplicar modo: **SoloClose** (detener nuevas copias, mantener gestión existente), **Congelado** (detener apertura), **VenderTodo** (cerrar **cada** copia en destino inmediatamente). Una vez disparado, destino traqueado — sin nuevas aperturas hasta que host reinicie — + alerta `CopyAccountProtectionTriggered` levantada. `VenderTodo` requiere `StopEquity`; `TakeEquity` debe estar por encima de `StopEquity`. **Caveat sin garantía:** vender-todo usa ejecución de mercado — como equivalente de cada competidor, no puede garantizar precio de relleno en mercado rápido/agujereado.
- **Botón de pánico Aplanar-Todo** (C8) — `POST /api/copy/profiles/{id}/flatten` cierra inmediatamente **cada** posición copiada en cada destino + bloquea contra nuevas aperturas. Enrutado entre procesos: API establece bandera, supervisor entrega a host en ejecución (reutilizando canal de rotación de token), que aplana en su lugar; bandera limpiada así dispara exactamente una vez (alerta `CopyFlattenAll`). Usuario luego pausa/detiene perfil.
- **Guardia de regla de firma propietaria** (C7) — cumplimiento que usuarios de copia de firma propietaria piden. Por destino, **tapa de pérdida diaria** (pérdida desde equity de apertura del día) y/o **límite de reducción de seguimiento** (pérdida desde equity pico en ejecución), ambos en moneda de depósito. En incumplimiento destino **auto-aplanado** (cada copia cerrada) + **bloqueado** resto del día UTC (nuevas aperturas se saltan `prop_lockout`); alerta `CopyPropRuleBreached` dispara. Bloqueo se limpia cuando rollo del día UTC (línea base/pico fresco tomado). Comparte mismo sondeo de equity en vivo como protección de cuenta.
- **Jitter de ejecución** (C11, desactivado por defecto) — retraso aleatorio `0..N` ms antes de colocar cada copia, para descorrelacionar timestamps de orden casi idénticos entre **propias** cuentas del usuario. **Caveat de cumplimiento:** ayuda para firmas propietarias que *permiten* copia — **no** herramienta para evadir firma que la prohíbe; mantener dentro de reglas de tu firma es tu responsabilidad.
- **Bloqueo de configuración** (C9) — congelar configuración de destino por período (`POST …/destinations/{id}/lock` con minutos). Mientras está bloqueado, destino no puede ser removido (agregado rechaza con `CopyDestinationConfigLocked`) — guardia deliberada contra cambios impulsivos durante reducción. Bloqueo expira automáticamente en su timestamp.
- **Pre-alerta de consistencia** (C10) — advertir (una vez por día UTC) cuando **ganancia diaria** de destino alcanza porcentaje configurado de equity de apertura del día (`CopyConsistencyThresholdApproaching`), así regla de consistencia de firma propietaria se respeta *antes* de que dispare. Lado de ganancia, independiente de bloqueo lado de pérdida; corre en la misma línea base del día como guardia de regla propietaria.
- **Filtro de tipo de orden** — elegir exactamente qué tipos de orden maestros copiar: mercado, rango de mercado, límite, parada, parada-límite (banderas `CopyOrderTypes`; defecto todos). Selectividad estilo cMAM.
- **Copiar SL / Copiar TP** — espeja parada de pérdida/ganancia de maestro, o gestiona protección independientemente.
- **Copiar parada de seguimiento**, **espeja cierre parcial**, **espeja escala-in** — cada uno independientemente conmutable.
- **Copiar vencimiento pendiente** (por defecto activado) — espeja timestamp de vencimiento Good-Till-Date de orden pendiente maestra.
- **Copiar deslizamiento maestro** (por defecto activado) — para órdenes de rango de mercado + parada-límite, coloca orden esclava con deslizamiento exacto en puntos de maestro (precio base tomado desde spot en vivo de esclavo).
- **Guardias**: máximo porcentaje reducción, tapa pérdida diaria, retraso máximo copia, filtro deslizamiento (saltar copia si precio esclavo se movió más de N pips desde entrada maestra). **Retraso máximo copia** medido contra timestamp del servidor real del evento maestro (`ExecutionEvent.ServerTimestamp`) vía `TimeProvider` inyectado: señal más vieja que retraso máximo configurado se salta, así copia obsoleta nunca se coloca tarde (antes retraso siempre cero + guardia muerto).
- **Normalización de precisión SL/TP** (M6) — precios de parada-pérdida/ganancia copiados redondeados a **precisión de dígitos del símbolo** de destino antes de enmendar, así precio maestro a precisión más fina (o desajuste de dígito entre brokers) nunca dispara `INVALID_STOPLOSS_TAKEPROFIT` del servidor.
- **Disyuntor de rechazo / Guardia de Seguidor** (G8) — destino rechazando `CopyDefaults.RejectionBudget` aperturas en fila se **dispara**: sin nuevas aperturas para ventana de enfriamiento (`CopyDestinationTripped` alerta dispara), deteniendo tormenta de rechazo de martillar (firma propietaria) cuenta. Posiciones existentes siguen siendo gestionadas + cerradas mientras disparadas; disyuntor auto-se reinicia después de enfriamiento + copia exitosa limpia contador.
- **Techo de cordura de lote** (C14) — tamaño de copia máximo absoluto y/o tapa múltiple-de-maestro. Copia computada excediendo tapa absoluta, o excediendo `N×` propio tamaño de lote del maestro, **duro-bloqueado** (superficializado como salto `lot_sanity`, contado en `cmind.copy.skipped`) no colocado — defiende contra clase catastrófica-sobre-tamaño (maestro 0.23-lote convirtiéndose en 3 lotes en cada receptor vía multiplicador desenfrenado o bug de redondeo). Ambas dimensiones defecto `0` (apagado).

## Confiabilidad y casos extremos

El motor se construye para la realidad de que cualquier cosa puede fallar en cualquier momento:

- **Timeout de correlación de relleno pendiente esclavo** (C13) — esclavo esclavo reflejado cuyo maestro pendiente desapareció (ni descansando ni recientemente relleno) cancelado después de timeout de correlación, así copia esclava no puede rellenar sin correlación en posición no gestionada (`CopyPendingTimedOut`). Resincronización también limpia huérfano de orden-id-etiquetado pendiente relleno.
- **Cierre/aplanamiento robusto** (M8) — cerrando huérfano en resincronización, o aplanando en incumplimiento de guardia, tolera posición que broker ya cerró (`POSITION_NOT_FOUND`): cada cierre ejecuta independientemente, así un id obsoleto nunca aborta resincronización o deja cuenta sin aplanar resto.

- **Iniciar con maestro ya en operaciones** — en inicio host reconcilia + abre copias para posiciones existentes maestras.
- **Conexión cae / desincronización** — en reconexión host reconcilia: abre copias faltantes, cierra huérfanos, re-etiqueta pendientes. Sin órdenes duplicadas.
- **Fallo de colocación de orden** — fallo en un destino registrado, nunca bloquea otros destinos.
- **Token válido único por cID** — cTrader invalida token de acceso antiguo de cID momento que nuevo emitido. cMind intercambia token de host en ejecución **en su lugar** (re-auth en socket en vivo) así copia continúa sin soltar stream. Ver [ciclo de vida del token](token-lifecycle.md).

## Auditabilidad

Cada acción emite evento de log estructurado, generado por fuente (`LogMessages`) con id de perfil, cID de destino, ids de orden/posición, + valores — orden colocada/saltada (con razón), cierre parcial, protección aplicada, seguimiento aplicado, pendiente colocada/enmendada/cancelada, vencimiento espejado, deslizamiento rango de mercado espejado, token intercambiado, resumen resincronización. Este es el rastro de auditoría para cumplimiento + resolución de disputa.

Junto con logs, el motor emite **métricas OpenTelemetry** en medidor `cMind.Copy` (registrado en pipeline OTel compartido, exportado vía OTLP / a Azure Monitor como resto): `cmind.copy.latency` (evento maestro → despacho, ms), `cmind.copy.dispatch.duration` (abanico a todos destinos, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (etiquetado por destino), `cmind.copy.skipped` (etiquetado por razón), + `cmind.copy.failed`. Estos hacen regresión de latencia/deslizamiento medible, no solo visible en línea de log — suite en vivo la afirma contra presupuesto.

## API

- `GET /api/copy/profiles` — listar.
- `POST /api/copy/profiles` — crear (con ids de cuenta de destino opcional).
- `GET /api/copy/profiles/{id}` — detalle completo incl. cada opción de destino.
- `POST /api/copy/profiles/{id}/destinations` — agregar un destino con conjunto de opción completo.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remover.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — ciclo de vida.

## Pruebas

- **Unitaria** (`tests/UnitTests/CopyTrading`) — modos de dimensionamiento, filtros de decisión, filtro de tipo de orden, copia de vencimiento, deslizamiento de rango de mercado/parada-límite, toggles SL/TP, cierre parcial, enmienda/cancelación pendiente, inicio-con-abierto, desconexión→desincronización→resincronización, intercambio de token en su lugar, invalidación entre cID. Ejecuta contra `FakeTradingSession`, simulador en memoria fiel a cTrader.
- **Integración** (`tests/IntegrationTests/CopyLive`) — afinidad de nodo/reclamo de arrendamiento, propagación de versión de token en Postgres real.
- **E2E** (`tests/E2ETests`) — viaje de redonda de opción de destino a través de API + UI, ciclo de vida completo.
- **Estrés / DST** (`tests/StressTests`) — prueba de simulación determinista: cargas aleatorizadas sembradas + inyección de fallos (solapa de socket, rechazo de orden, rechazo de rango de mercado, rotación de token, muerte de nodo) conducen `CopyEngineHost` a reposo + afirman invariantes de convergencia. Ver [testing/stress-testing.md](../testing/stress-testing.md). Esta suite superficializó + arregló carrera de inicio real: `OnReconnected` cableado antes de carga de referencia inicial + resincronización, así solapa de socket durante startup podría ejecutar segunda resincronización concurrentemente + corromper diccionarios de estado no concurrentes del host — carga de startup + primer resincronización ahora ejecutan bajo `_stateGate`.
- **En vivo** — cuentas demo cTrader reales; ver [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Ver [dev-credentials.md](../testing/dev-credentials.md) para archivo de credenciales único en vivo + tiers E2E leen.
## Controles de perfil y gestión de destino

Iniciar/detener son botones de icono en cada fila de perfil (deshabilitados cuando la acción no aplica). Cuentas fuente y de destino se muestran por su **número de cuenta**, nunca un id interno. Hacer clic en un perfil abre un **diálogo** para gestionar sus cuentas de destino (agregar/remover con configuración completa por destino).
