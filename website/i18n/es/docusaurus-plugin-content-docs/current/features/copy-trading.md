---
description: "Espeja la cuenta maestra de cTrader en una+ cuentas esclavas — entre brokers, entre cID — con control por destino + reconciliación de grado monetario."
---

# Copia de trading

Espeja la cuenta de cTrader **maestra** en una+ cuentas **esclava** — entre brokers, entre cID — con control por destino + reconciliación de grado monetario.

## Conceptos

- **Perfil de copia** — una maestra (`SourceAccountId`) + una+ **destinos**. Ciclo de vida: `Draft → Running → Paused → Stopped` (`Error` en caso de fallo). Agregado raíz: `CopyProfile` (es propietario de `CopyDestination`).
- **Destino** — una cuenta esclava + conjunto completo de reglas para cómo se copia maestra en ella. Toda la configuración por destino, por lo que una maestra alimenta esclavas conservadoras + agresivas a la vez.
- **Host del motor de copia** — trabajador en ejecución para perfil (`CopyEngineHost`). Se suscribe al flujo de ejecución maestro, aplica cada evento a cada destino.
- **Supervisor** — `CopyEngineSupervisor`, servicio de fondo en cada nodo. Aloja perfiles asignados, auto-repara en todo el clúster (véase [escalado](../deployment/scaling.md)).

## Qué se espeja

| Evento maestro | Acción esclava |
|--------------|--------------|
| Posición de mercado / rango de mercado abierta | Abrir una copia dimensionada (etiquetada con id de posición fuente) |
| Orden pendiente de límite / parada / parada-límite | Colocar la orden pendiente coincidente |
| Enmienda de orden pendiente | Enmendar la orden pendiente espejada en su lugar |
| Cancelación de orden pendiente / vencimiento | Cancelar la orden pendiente espejada |
| Cierre parcial | Cerrar la misma proporción de la posición esclava |
| Escala hacia adentro (aumento de volumen) | Abrir el volumen añadido (optar por participar) |
| Cambio de parada de pérdida / parada rastreadora | Enmendar la protección de posición esclava |
| Cierre completo | Cerrar la copia esclava |

Cada copia **etiquetada con id de posición/orden fuente**. Después de reconectar el host reconstruye el estado desde reconciliación: abre copias que el maestro mantiene pero esclava falta, cierra "huérfanos" esclavos que el maestro ya no mantiene — **sin duplicar operaciones**.

## Crear un perfil

El diálogo **Nuevo perfil** en la página Copia de trading recopila todo por adelantado: nombre del perfil, fuente (cuenta maestra), destinos (cuentas esclava) (selección múltiple con botón **Seleccionar todo**; maestra elegida excluida de lista esclava), + conjunto de opciones completo por destino debajo. Todas las entradas **validadas antes de guardar** — nombre/fuente/destino faltante, parámetro de dimensionamiento no positivo, límites de lote negativos/inconsistentes, porcentaje de reducción fuera de rango, sin tipo de orden habilitado, filtro de símbolo vacío, o pares de mapa de símbolos mal formados se muestran como lista de errores + bloquean guardado. Al confirmar, perfil creado + cada esclava seleccionada añadida con configuración elegida.

Las acciones de fila respetan ciclo de vida: **Iniciar** habilitado solo cuando no se está ejecutando, **Parar** + **Pausar** solo cuando se está ejecutando, **Eliminar** deshabilitado mientras se ejecuta + pide confirmación antes de eliminar perfil + destinos.

## Opciones por destino

Establecidas en diálogo Nuevo perfil, en panel por destino de página Copia de trading, o vía `POST /api/copy/profiles/{id}/destinations`:

- **Dimensionamiento** (`MoneyManagementMode` + parámetro): lote fijo, multiplicador de lote/nocional, balance/equidad/margen libre proporcional, riesgo fijo %, apalancamiento fijo, proporcional automático, **riesgo-%-desde-parada** (M7). Más límites mín/máx de lote + fuerza-lote-mín. **Riesgo-desde-parada** dimensiona destino para que arriesgue porcentaje configurado de su *propio* balance, derivado de la **distancia de parada de pérdida maestro** (`maestro arriesga 2% → esclavo auto-arriesga 2%`): `lotes = balance×% ÷ (distanciaParada × tamañoContrato)`. Maestra abierta **sin** parada de pérdida no tiene distancia para dimensionar contra → usa lote de reserva de riesgo máximo configurado **fallback** (M7) si está configurado, sino saltado (`no_stop_loss`) no adivinado. Equidad/margen libre proporcional tamaño desde **equidad** de cuenta real (`balance + Σ P&L flotante`, derivado por Open API cTrader que no entrega equidad), no balance simple — por lo que maestra sentada en ganancia/pérdida abierta dimensiona copias correctamente. Margen usado no expuesto por API de reconciliación, por lo que margen libre tratado como equidad (proxy de fondos disponibles honesto); otros modos leen balance + saltan ronda de revaluación extra.
- **Filtro de dirección**: ambas / solo largo / solo corto. **Invertir**: voltear lado (+ intercambiar SL↔TP) para copia contrarian.
- **Solo gestionar** (Ignorar-Nuevas-Operaciones / Solo-Cerrar): espejar cierres, cierres parciales + cambios de protección en posiciones ya copiadas, pero abrir **ninguna** posición nueva/órdenes pendientes (saltado `manage_only`). Usa para desmontar destino sin cortar copias existentes.
- **Sincronizar-Abierto-en-inicio** / **Sincronizar-Cerrado-en-inicio** (predeterminado activado): en **primer** resincronización del perfil, si se deben abrir copias para posiciones preexistentes de maestro, + si se deben cerrar copias que maestro cerró mientras perfil estaba parado. Ambos se aplican solo al inicio — la reconexión a mitad de carrera siempre reconcilia completamente para que la desincronización se recupere independientemente.
- **Mapa de símbolo** + **filtro de símbolo** (lista blanca / lista negra). Cada entrada de mapa de símbolo lleva **multiplicador de volumen opcional por símbolo** (anulación por símbolo cMAM) dimensionamiento de tamaño de copia para ese símbolo encima del dimensionamiento de destino (1 = sin cambio). Mapa completo importa/exporta como **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; columnas `Source,Destination,VolumeMultiplier`) — cada fila validada a través de objetos de valor de dominio, por lo que archivo mal formado no puede producir mapa inválido.
- **Ventana de horas de trading** (C18) — ventana UTC diaria por destino (`start`/`end` minutos del día, fin exclusivo; `start == end` = todo el día). Nuevas aperturas fuera de ventana saltadas (`trading_hours`); ventana con `start > end` envuelve después de medianoche (p. ej. 22:00–06:00). Las posiciones existentes permanecen gestionadas.
- **Filtro de etiqueta de fuente** (C18, equivalente cTrader del filtro de número mágico MT) — cuando se establece, copiar solo operaciones maestras cuya etiqueta coincida **exactamente** (p. ej. operaciones de un bot, o solo etiqueta manual); sino saltado (`source_label`). Vacío = copiar todo. Llevado en `ExecutionEvent.SourceLabel` desde `TradeData.Label` de posición/orden maestro, honrado en resincronización también.
- **Protección de cuenta** (ZuluGuard / Protección Global de Cuenta) — vigilar **equidad en vivo** de destino (`balance + Σ P&L flotante`, sondeo cada `CopyDefaults.EquityGuardInterval`) contra piso `StopEquity` y/o techo opcional `TakeEquity`. En caso de incumplimiento, aplicar modo: **SoloClose** (detener nuevas copias, seguir gestionando existentes), **Congelado** (detener aperturas), **Vender** (cerrar **cada** copia en destino inmediatamente). Una vez activado, destino bloqueado — sin nuevas aperturas hasta reinicio del host — + alerta `CopyAccountProtectionTriggered` elevada. `SellOut` requiere `StopEquity`; `TakeEquity` debe estar encima de `StopEquity`. **Advertencia sin garantía:** vender fuera usa ejecución de mercado — como el equivalente de cada competidor, no puede garantizar precio de relleno en mercado rápido/brecha.
- **Botón de pánico Aplanar-Todo** (C8) — `POST /api/copy/profiles/{id}/flatten` cierra inmediatamente **cada** posición copiada en cada destino + bloquea contra nuevas aperturas. Enrutado entre procesos: API establece bandera, supervisor entrega a host en ejecución (reutilizando canal de rotación de token), que aplana en su lugar; bandera borrada para que se dispare exactamente una vez (alerta `CopyFlattenAll`). Usuario luego pausa/detiene perfil.
- **Guardia de regla de empresa prop** (C7) — aplicación de usuarios copistas de empresa prop solicitan. Por destino, **límite de pérdida diaria** (pérdida desde equidad de apertura del día) y/o límite de **reducción rastreadora** (pérdida desde equidad pico en ejecución), ambos en moneda de depósito. En caso de incumplimiento destino **aplana automáticamente** (cada copia cerrada) + **bloqueado** resto de día UTC (nuevas aperturas saltadas `prop_lockout`); alerta `CopyPropRuleBreached` se dispara. Bloqueo se borra cuando día UTC se rueda (nueva línea base/pico tomado). Comparte mismo sondeo de equidad en vivo que protección de cuenta.
- **Jitter de ejecución** (C11, desactivado por defecto) — demora aleatoria `0..N` ms antes de colocar cada copia, para descorrelacionar marcas de tiempo de orden casi idénticas en **propias** cuentas del usuario. **Advertencia de cumplimiento:** ayuda para empresas prop que *permiten* copia — **no** herramienta para evadir empresa que la prohíbe; permanecer dentro de reglas de tu empresa es tu responsabilidad.
- **Bloqueo de configuración** (C9) — congelar configuración de destino por período (`POST …/destinations/{id}/lock` con minutos). Mientras está bloqueado, destino no puede ser removido (agregado rechaza con `CopyDestinationConfigLocked`) — guardia deliberado contra cambios impulsivos durante reducción. Bloqueo expira automáticamente en su marca de tiempo.
- **Alerta previa de consistencia** (C10) — advertir (una vez por día UTC) cuando **ganancia diaria** de destino alcanza porcentaje configurado de equidad de apertura del día (`CopyConsistencyThresholdApproaching`), para que regla de consistencia de empresa prop respetada *antes* de que se active. Lado de ganancia, independiente de bloqueo de lado de pérdida; se ejecuta desde misma línea base de día que guardia de regla prop.
- **Filtro de tipo de orden** — elegir exactamente qué tipos de orden maestro copiar: mercado, rango de mercado, límite, parada, parada-límite (banderas `CopyOrderTypes`; predeterminado todo). Selectividad estilo cMAM.
- **Copiar SL / Copiar TP** — espejar parada de pérdida / ganancia objetiva maestro, o gestionar protección independientemente.
- **Copiar parada rastreadora**, **espejar cierre parcial**, **espejar escala hacia adentro** — cada uno independientemente activable.
- **Copiar vencimiento pendiente** (predeterminado activado) — espejar marca de tiempo de vencimiento Buena-Hasta-Fecha de orden pendiente maestro.
- **Copiar deslizamiento maestro** (predeterminado activado) — para órdenes de rango de mercado + parada-límite, colocar orden esclava con deslizamiento exacto de maestro en puntos (precio base tomado desde spot en vivo de esclavo).
- **Guardas**: reducción máxima %, límite de pérdida diaria, retraso máximo de copia, filtro de deslizamiento (saltar copia si precio esclavo se movió más allá de N pips desde entrada maestro). **Retraso máximo de copia** medido contra marca de tiempo real del servidor del evento maestro (`ExecutionEvent.ServerTimestamp`) a través de `TimeProvider` inyectado: señal más antigua que máximo retraso configurado saltada, para que copia antigua nunca se coloque tarde (previamente retraso siempre cero + guardia muerta).
- **Normalización de precisión SL/TP** (M6) — precios de parada de pérdida/ganancia objetiva copiados redondeados a precisión de dígitos de símbolo **destino** antes de enmienda, para que precio maestro en precisión más fina (o desajuste de dígitos entre brokers) nunca active `INVALID_STOPLOSS_TAKEPROFIT` del servidor.
- **Disyuntor de circuito de rechazo / Guardia de Seguidor** (G8) — destino rechazando `CopyDefaults.RejectionBudget` aperturas seguidas es **activado**: sin nuevas aperturas para ventana de enfriamiento (`alerta CopyDestinationTripped` se dispara), deteniendo tormenta de rechazo de martilleo (empresa prop). Las posiciones existentes aún se gestionan + se cierran mientras se activa; disyuntor se reinicia automáticamente después de enfriamiento + copia exitosa borra contador.
- **Techo de cordura de lote** (C14) — tamaño máximo absoluto de copia y/o límite múltiple de maestro. Copia computada excediendo techo absoluto, o excediendo `N×` tamaño de lote propio de maestro, **bloqueado duro** (mostrado como salto `lot_sanity`, contado en `cmind.copy.skipped`) no colocado — defiende contra clase de sobredimensionamiento catastrófico (0.23 lotes maestro convirtiéndose en 3 lotes en cada receptor vía multiplicador desbocado o error de redondeo). Ambas dimensiones predeterminado `0` (desactivado).

## Confiabilidad y casos extremos

Motor construido para realidad de que cualquier cosa puede fallar en cualquier momento:

- **Tiempo de espera de correlación de relleno pendiente esclavo** (C13) — orden pendiente esclava espejada cuya orden pendiente maestra desapareció (ni descansando ni recién rellenada) cancelada después de tiempo de espera de correlación, por lo que copia esclava no puede rellenar sin correlación en posición no gestionada (`CopyPendingTimedOut`). Resincronización también limpia orfandad de orden rellena-pendiente etiquetada con id.
- **Cierre/aplanamiento robusto** (M8) — cerrar huérfano en resincronización, o aplanarse en incumplimiento de guardia, tolera posición broker ya cerrada (`POSITION_NOT_FOUND`): cada cierre se ejecuta independientemente, por lo que un id antiguo nunca aborta resincronización o deja resto de cuenta sin aplanar.

- **Empezar con maestra ya en operaciones** — en inicio host reconcilia + abre copias para posiciones existentes maestro.
- **Conexión cae / desincronización** — en reconexión host reconcilia: abre copias faltantes, cierra huérfanos, re-etiqueta pendientes. Sin órdenes duplicadas.
- **Falla de colocación de orden** — falla en un destino registrada, nunca bloquea otros destinos.
- **Token válido único por cID** — cTrader invalida token de acceso antiguo de cID momento nuevo emitido. cMind intercambia token del host en ejecución **en su lugar** (re-auth en socket en vivo) para que copia continúe sin soltar flujo. Véase [ciclo de vida de token](token-lifecycle.md).

## Auditoría

Cada acción emite evento de registro estructurado, generado por fuente (`LogMessages`) con id de perfil, cID de destino, ids de orden/posición, + valores — orden colocada/saltada (con razón), cierre parcial, protección aplicada, rastreo aplicado, pendiente colocada/enmendada/cancelada, vencimiento espejado, deslizamiento de rango de mercado espejado, token intercambiado, resumen de resincronización. Este es el registro de auditoría para cumplimiento + resolución de disputas.

Junto con registros, motor emite **métricas OpenTelemetry** en medidor `cMind.Copy` (registrado en tubería OTel compartida, exportado sobre OTLP / a Azure Monitor como resto): `cmind.copy.latency` (evento maestro → envío, ms), `cmind.copy.dispatch.duration` (abanico a todos los destinos, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (etiquetado por destino), `cmind.copy.skipped` (etiquetado por razón), + `cmind.copy.failed`. Estos hacen regresión de latencia/deslizamiento medible, no solo visible en línea de registro — suite en vivo aserta contra presupuesto.

## API

- `GET /api/copy/profiles` — listar.
- `POST /api/copy/profiles` — crear (con ids de cuenta de destino opcionales).
- `GET /api/copy/profiles/{id}` — detalle completo incl. cada opción de destino.
- `POST /api/copy/profiles/{id}/destinations` — agregar un destino con conjunto de opción completo.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remover.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — ciclo de vida.

## Pruebas

- **Unidad** (`tests/UnitTests/CopyTrading`) — modos de dimensionamiento, filtros de decisión, filtro de tipo de orden, copia de vencimiento, deslizamiento de rango de mercado/parada-límite, alternancias de SL/TP, cierre parcial, enmienda/cancelación pendiente, inicio-con-abierto, desconexión→desincronización→resincronización, intercambio de token en su lugar, invalidación entre cID. Se ejecuta contra `FakeTradingSession`, simulador en memoria fiel a cTrader.
- **Integración** (`tests/IntegrationTests/CopyLive`) — afinidad de nodo/reclamación de arrendamiento, propagación de versión de token en Postgres real.
- **E2E** (`tests/E2ETests`) — viaje de ida y vuelta de opción de destino a través de API + UI, ciclo de vida completo.
- **Estrés / DST** (`tests/StressTests`) — prueba de simulación determinística: cargas de trabajo aleatorias sembradas + inyección de fallas (agitación de socket, rechazo de orden, rechazo de rango de mercado, rotación de token, muerte de nodo) conducen `CopyEngineHost` a la quietud + aseveran invariantes de convergencia. Véase [testing/stress-testing.md](../testing/stress-testing.md). Esta suite detectó + arregló carrera de inicio real: `OnReconnected` cableada antes de carga de referencia inicial + resincronización, para que agitación de socket durante inicio pudiera ejecutar segunda resincronización concurrentemente + corromper diccionarios de estado no concurrentes del host — carga de inicio + primera resincronización ahora se ejecutan bajo `_stateGate`.
- **En vivo** — cuentas de demostración cTrader real; véase [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Véase [dev-credentials.md](../testing/dev-credentials.md) para archivo de credenciales único que lee capas en vivo + E2E.
