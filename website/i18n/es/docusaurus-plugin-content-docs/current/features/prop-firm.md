---
description: "Simulación de desafío de empresa prop al por menor (estilo FTMO): el operador debe alcanzar objetivo de ganancia mientras permanece dentro de límites de riesgo (pérdida diaria máxima, reducción máxima…"
---

# Simulación de desafío de empresa prop

Las empresas prop al por menor (estilo FTMO) venden **cuentas de evaluación**: el operador debe alcanzar objetivo de ganancia mientras
permanece dentro de límites de riesgo (pérdida diaria máxima, reducción total máxima/rastreadora, consistencia, límites de tiempo) antes de
financiado. cMind permite al usuario crear **desafío personalizado de cualquier forma de industria**, enlazar a
`TradingAccount`, **ejecutar como operación de copia de trading** — iniciado/detenido, alojado en nodo,
rastreado **en vivo sobre Open API cTrader**. El agregado evalúa cada regla determinísticamente; en
paso o incumplimiento, termina desafío, lo marca, alerta al usuario.

## Dominio (contexto acotado: PropFirm)

`PropFirmChallenge` = agregado raíz (módulo `Core.PropFirm`), referencias su `TradingAccount` por
id fuerte solo (sin FK entre agregados). Es propietario de evaluación de regla, máquina de estado/fase, arrendamiento de nodo.

### Objetos de valor y conjunto de reglas

- **`Money`** (no negativo), **`MoneyAmount`** (firmado), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — lectura alimentada a agregado.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — hechos no equidad.
- **`DailyLossLimit`** `(percent, basis)` — base `Equity` (intradiario, incluye P&L flotante) o `Balance`
  (solo realizado).
- **`DrawdownLimit`** — `Static` (desde balance inicial), `TrailingPercent` (desde equidad pico), o
  `TrailingThresholdDollar` (rastreos equidad pico por cantidad fija en dólares, luego **bloquea en balance inicial**
  una vez equidad alcanza umbral — estilo futuros).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — bloquea paso mientras un día domina ganancia total.
- **`ChallengeRules`** lleva anteriores más `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Matemáticas de regla viven en VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); orquestas de agregado.

### Tipos de desafío y plantillas

`ChallengeTemplates.For(kind)` construye preconfiguración válida para `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, o `Custom` (control completo). UI pre-rellena plantilla; usuario puede ajustar cualquier campo.

### Fases y estado

- **Fases:** `Evaluation → Verification → Funded` (un paso salta Verification).
- **Estado:** `Active`, `Passed`, `Failed`, más ciclo de vida `Stopped` (seguimiento en pausa) — `Create` inicia
  desafío `Active`; `Stop()`/`Resume()` alternan `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Evaluación de regla

- **`RecordEquity(EquitySnapshot, now)`** — rueda día de trading en límites de día (captura ganancia de día anterior para regla de consistencia), actualiza picos/picos diarios, luego **falla en primer incumplimiento**
  (pérdida diaria → reducción → límite de tiempo → inactividad, en orden) o avanza fase cuando objetivo de ganancia,
  mínimo-día-trading, requisitos de consistencia todos se cumplen. Instantáneas fuera de orden y registros en
  desafío terminal lanzan `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evalúa reglas de comportamiento (máximo abierto, tenencia de fin de semana, trading de noticias), marca actividad para regla de inactividad.
- Suave **`PropFirmDrawdownWarning`** se dispara una vez cuando uso de equidad cruza umbral configurable.

Eventos de dominio: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Seguimiento en vivo (Ejecución) — alojado en nodo, auto-sanador

El seguimiento refleja pila de alojamiento de copia de trading exactamente; rastreador prop = primo **solo lectura** de
motor de copia.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` en cada nodo, controlado en
  `App:PropFirm:Enabled`. Cada ciclo **reclama** desafíos activos en arrendamiento auto-sanador
  (`AssignedNode` + `LeaseExpiresAt`; desafíos de nodo muerto reclamados una vez arrendamiento caduca —
  misma reclamación `ExecuteUpdate` atómica como copia de trading, por lo que dos nodos nunca doble-rastrean), renueva arrendamientos,
  empuja tokens rotados en su lugar, detiene hosts cuyo desafío izquierdo `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — uno por desafío. Abre `IOpenApiTradingSession`
  para cuenta y, en `App:PropFirm:EquityPollInterval`, recomputa equidad en vivo, alimenta a
  agregado. Intercambia token de acceso en su lugar en rotación (sin caída de sesión). Sale cuando desafío
  ya no `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — matemáticas de equidad fieles a cTrader.
  Equidad **no** entregada por Open API, por lo que derivada: `equity = balance + Σ(P&L no realizado)`,
  donde P&L de cada posición es `diferenciaPrecio × unidades × cotización→tasa depósito + swap + comisión`
  (`unidades = volumen alambre / 100`; largo revalúa en oferta, corto en demanda). Balance desde
  `ProtoOATrader`; posiciones (precio de entrada, swap, comisión) desde reconciliación; oferta/demanda en vivo desde spot
  suscripciones. Puro e aislado — punto caliente de conversión de moneda unidad-probado en su propio.

## Alertas

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) se suscribe a eventos de dominio paso/incumplimiento/advertencia
(registrado como `IDomainEventHandler<>`, enviado después de `SaveChanges` exitoso), notifica usuario
a través de registro de alerta/auditoría estructurado (`LogMessages`). UI en vivo refleja mismo cambio de estado. Esto
= reacción entre contextos — nunca muta agregado de desafío.

## API (`/api/prop-firm`, característica `PropFirm`, rol Usuario+)

| Método | Ruta | Propósito |
|--------|------|---------|
| GET | `/challenges` | listar desafíos del usuario (tipo, fase, estado, equidad en vivo, arrendamiento) |
| GET | `/challenges/{id}` | un desafío |
| GET | `/templates` | preconfiguración de industria para diálogo de creación |
| POST | `/challenges` | crear desde plantilla **o** conjunto de regla totalmente personalizado |
| POST | `/challenges/{id}/start` | reanudar seguimiento (Stopped → Active) |
| POST | `/challenges/{id}/stop` | detener seguimiento (Active → Stopped, liberar arrendamiento) |
| POST | `/challenges/{id}/equity` | registrar instantánea de equidad → re-evaluar (ruta manual/sin-alimentación-en-vivo) |
| DELETE | `/challenges/{id}` | eliminación suave (bloqueada mientras Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` expone lista/crear(desde plantilla)/registrar-equidad/iniciar/parar, controlado en
característica `PropFirm`.

UI: `/prop-firm` (nav *Empresa Prop*, controlado por bandera `PropFirm`) lista desafíos con acciones de fila **Iniciar/Parar/Eliminar** (Iniciar cuando Stopped, Parar cuando Active, Eliminar deshabilitado mientras Active), los crea a través de
`NewPropFirmChallengeDialog` (selector de plantilla + editor de regla completo). Todo crear/editar vía diálogo MudBlazor.

## Alimentación de equidad en vivo — resuelta

La brecha anterior "sin alimentación de P&L de cuenta en vivo" cerrada: cuando `App:PropFirm:Enabled` establecido, nodos rastrean
cuenta en vivo sobre Open API, alimentan equidad automáticamente. Sin ella (predeterminado), dominio y
ruta **manual-equidad** (`POST …/equity`) se ejecutan sin cambios — sin credenciales de cTrader necesarias para construir/probar/E2E.

## Pruebas

- **Unidad** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (avance de fase, mín-días, reducción estática/rastreadora, pérdida diaria, guardas terminal/fuera-de-orden); `PropFirmChallengeRulesTests` (base de pérdida diaria equilibrio vs equidad, dólar-umbral-rastreador rastrear+bloquear, bloque/permiso de consistencia, límite de tiempo, inactividad,
  máximo-exposición, fin de semana, noticias, parar/reanudar, límite de arrendamiento, paso libera arrendamiento, advertencia de reducción);
  `PropFirmValueObjectTests` (rangos de VO + matemáticas de VO de regla); `PropFirmEquityCalculatorTests` (P&L largo/corto,
  swap/comisión, conversión cotización→depósito, fijación de precios faltante); `PropFirmTrackingHostTests` (equidad en vivo
  impulsa paso/fallo contra sesión falsa extendida); `PropFirmAlertNotifierTests`. Tiempo explícito /
  `FakeTimeProvider` — sin lecturas de reloj de pared.
- **Integración** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (viaje de ida y vuelta + registrar-equidad +
  eliminación suave, reglas enriquecidas + viaje de ida y vuelta de arrendamiento) y `PropFirmTrackingLeaseTests` (reclamar, arrendamiento contestado,
  reclamar después de caducidad en dos identidades de nodo) en Postgres real.
- **E2E** — `E2ETests/PropFirmTests.cs`: crear + registrar-equidad a `Passed`; detener→iniciar→flujo de incumplimiento;
  punto final de plantillas.
- **Estrés / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: corrientes de equidad/actividad aleatorias sembradas
  (rodillos de día, picos, caídas, duplicada + instantáneas fuera-de-orden, exposición/fin de semana/noticias) en
  muchos desafíos de regla mixta, aseverando estados terminales pegajosos exactamente-una-vez, invariante de límites-pico-actual,
  fallos razonados.

## Configuración (`App:PropFirm`)

`Enabled` (desactivado por defecto), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
