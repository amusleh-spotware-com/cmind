---
description: "cMind se escala con esfuerzo mínimo del operador. Dos cargas de trabajo con estado — ejecución de ejecución/backtest, copia comercial — ambas usan base de datos como punto de coordinación, así que…"
---

# Escalado horizontal

cMind se escala con esfuerzo mínimo del operador. Dos cargas de trabajo con estado — ejecución de ejecución/backtest, 
copia comercial — ambas usan base de datos como punto de coordinación, por lo que agregar réplicas no necesita
coordinador externo (sin ZooKeeper, sin elección de líder).

## Copia comercial (arrendamiento auto-sanador)

Cada nodo ejecuta `CopyEngineSupervisor` (controlado en `App:Copy:Enabled`). En cada ciclo de reconciliación,
el supervisor:

1. **Reclama** cada perfil en ejecución no asignado *o* arrendamiento expirado, en un `UPDATE` atómico único —
   dos supervisores en carrera nunca reclaman el mismo perfil, por lo que el perfil se copia por exactamente un
   nodo (sin órdenes dobles).
2. **Renueva** el arrendamiento en los perfiles que aloja.
3. Aloja perfiles asignados, empuja rotaciones de token de acceso a anfitrión en ejecución en su lugar (sin
   caída de flujo de eventos).

Bloqueo del nodo → deja de renovar; una vez que `App:Copy:LeaseTtl` pasa, cualquier nodo superviviente reclama
sus perfiles en el siguiente ciclo, reconstruye el estado a partir de la reconciliación sin duplicar transacciones. **Escalado
hacia arriba** = agregar réplicas; perfiles no asignados/libres recogidos automáticamente.

**Escala elegante hacia abajo / actualización gradual (S1)** = en `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**libera los arrendamientos de este nodo** (`AssignedNode`/`LeaseExpiresAt` → null) para que el sobreviviente los reclame
en su *muy próximo* ciclo de reconciliación — **no** después del `LeaseTtl` completo. Solo los bloqueos duros esperan el TTL.
El `terminationGracePeriodSeconds` del agente de copia (predeterminado 30) da tiempo de liberación para terminar antes de
que se mate el pod.

### Controles (`App:Copy`)

| Configuración | Predeterminado | Notas |
|---------|---------|-------|
| `Enabled` | `false` | Activa el alojamiento de copias para el nodo. |
| `ReconcileInterval` | `30s` | Cada cuánto tiempo el nodo reclama/renueva/reconcilia. |
| `LeaseTtl` | `120s` | Período de gracia antes de que el nodo silencioso tenga sus perfiles reclamados. Mantén pocos intervalos de reconciliación para que un ciclo lento no cause traspaso espurio. |
| `NodeName` | nombre de máquina | Establece distintivamente cuando dos supervisores comparten un anfitrión. |

En Kubernetes, los supervisores de copia se ejecutan como Deployment; establece `replicas` al paralelismo deseado. Cada
pod obtiene `NodeName` estable (predeterminado: nombre de host del pod), por lo que arrendamientos atribuidos por pod. La base de datos es
la fuente única de verdad — sin sesiones pegajosas, sin estado por pod para migrar.

**Distribución equilibrada (S4):** establece `App:Copy:MaxProfilesPerNode` > 0 para limitar cuántos perfiles en ejecución
un nodo aloja. Cada supervisor luego reclama **como máximo** su espacio de cabecera restante vía `FOR UPDATE SKIP LOCKED` delimitado
reclamo atómico, por lo que los perfiles **se distribuyen** entre réplicas en lugar de que el primer supervisor agarre todo — sin pod único caliente / SPOF. 
La reclamación skip-locked mantiene la garantía "exactamente un nodo por perfil" (sin doble alojamiento) incluso bajo reclamos concurrentes. `0` (predeterminado) =
sin límite (un nodo aloja todo, sin cambios).

**A escala (S7/S8):** cada pod tiembla la reconciliación en hasta el 20% de `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) para que N réplicas no disparen `UPDATE`  de reclamo/renovación
simultáneamente (Postgres manada ruidosa). Cuando `copyAgent.replicas > 1` el gráfico también distribuye
réplicas entre nodos (`topologySpreadConstraints`) y agrega `PodDisruptionBudget` (`minAvailable: 1`)
así que el drenaje/actualización nunca lleva la capacidad de copia a cero.

## Ejecución de ejecución/backtest

`NodeScheduler` elige el nodo menos cargado elegible honrando `MaxInstances`; agentes de nodos remotos
se auto-registran y envían latidos (`App:Discovery`), `NodeHeartbeatMonitor` marca el nodo como no alcanzable
cuando el latido excede `Discovery:HeartbeatTtl`. Agrega agentes de nodo para agregar capacidad de ejecución;
el agente muerto se enruta automáticamente alrededor.

## Migraciones en escala horizontal / despliegue gradual

Cada réplica web/MCP ejecuta `OwnerSeeder` al iniciar, que aplica migraciones EF y siembra al propietario.
Para hacer eso de forma segura cuando N réplicas se inician al mismo tiempo, migrar + sembrar se ejecuta dentro de un
**bloqueo asesor de sesión Postgres** (`MigrationLock.RunExclusiveAsync`, clave `DatabaseDefaults.MigrationAdvisoryLockKey`):
la primera réplica en adquirirla migra y siembra; el resto se bloquea en el bloqueo, luego encuentra migraciones
ya aplicadas (sin operación) y el propietario ya presente. No se necesita trabajo de migración separado o elección de líder.
Si agregas siembra de primera ejecución, colócala **dentro** del mismo bloque protegido para que sea de escritor único.

## Resiliencia HTTP del agente de nodo

El nodo principal habla con cada agente `CtraderCliNode` sobre HTTP a través de tres clientes divididos por propósito para que un
nodo inestable o la red nunca corrompa el estado:

- **lectura** (`status` / `report` / `stats`) — GETs idempotentes, reintentados en fallas transitorias
  (retroceso exponencial + fluctuación, `NodeAgentHttp.ReadRetryCount`) con tiempos de espera por intento y totales.
- **escritura** (`start` / `stop` / `clean`) — POSTs no idempotentes, tiempos de espera pero **nunca reintentados**: un
  `start` reintentado podría lanzar dos veces un contenedor.
- **stream** (`logs`) — el flujo `docker logs -f` de larga duración obtiene un tiempo de espera infinito y sin
  canalización de resiliencia, por lo que la cola nunca se corta.

Un nodo que permanece no alcanzable se maneja mediante latido + [reclamación de instancia huérfana](../operations/node-discovery.md);
la capa HTTP solo suaviza los parpadeos transitorios.

## Niveles sin estado

Web (Blazor Server + API) y servidor MCP no tienen estado detrás de la base de datos, replican libremente.
La autenticación está basada en cookies; escala web horizontalmente detrás del equilibrador de carga. El servidor MCP es un proceso/Deployment separado
para que se escale independientemente de Web.

## Resiliencia de conexión de base de datos

Cada anfitrión que abre la base de datos utiliza una **estrategia de ejecución con reintentos** para que un
desconexión transitoria o una conmutación por error de Postgres administrada (RDS / parches de Servidor Flexible) se reintente en lugar de
aparecer como un error al usuario:
