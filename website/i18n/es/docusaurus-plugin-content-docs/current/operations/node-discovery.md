---
description: "Los nodos CLI de cTrader se unen al clúster mediante auto-registro + heartbeat — sin entrada manual. Mismo patrón que agentes Consul/Nomad/kubeadm: el agente arranca sabiendo la ubicación del nodo principal…"
---

# Auto-descubrimiento de nodos

Los nodos CLI de cTrader se unen al clúster mediante **auto-registro + heartbeat** — sin entrada manual. Mismo patrón que agentes Consul/Nomad/kubeadm: el agente arranca sabiendo la ubicación del nodo principal y un secreto compartido del clúster, luego se anuncia continuamente.

> Verificado end-to-end en Docker Compose y clúster Kubernetes `kind`: los agentes se auto-registran, aparecen en BD alcanzables, se marcan automáticamente como inalcanzables cuando los heartbeats paran más allá del TTL, vuelven a estar en línea cuando se reanudan.

## Cómo funciona

```
Agente CtraderCliNode                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── token de unión ──▶ verificar token (tiempo constante)
  { name, baseUrl, mode,                    verificar versión de protocolo
    maxInstances, dataDir,                   upsert CtraderCliNode por nombre
    protocolVersion }                        marcar LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  cada HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── si ahora - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registro == heartbeat.** El agente re-POSTa en `HeartbeatIntervalSeconds`. La primera llamada crea el nodo (`NodeRegistered` event); las llamadas posteriores refrescan el liveness. El heartbeat reanudado después de una interrupción vuelve a poner el nodo como alcanzable (`NodeCameOnline`).
- **Reconciliación de liveness.** `NodeHeartbeatMonitor` marca los nodos cuyo último heartbeat excede `HeartbeatTtl` como inalcanzables. El scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated en reachability) deja de colocar trabajo hasta que reporten de nuevo.
- **Reclamo de instancias huérfanas.** `NodeInstanceReclaimer` (background) transiciona cualquier instancia no terminal varada en un nodo inalcanzable a **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, evento de dominio `InstanceFailed` → notificación al usuario), para que un nodo caído/particionado nunca pueda dejar una instancia atascada "Running" para siempre. El reclamo solo se activa una vez que el último heartbeat del nodo está stale más allá de `HeartbeatTtl + InstanceReclaimGrace`, dando a un breve glitch la oportunidad de recuperarse primero. Las **runs reclamadas no se re-planifican automáticamente**: un nodo partitionado-pero-vivo aún puede estar ejecutando el contenedor y no hay cercado a nivel de contenedor, así que relanzar arriesgaría ejecución doble — el usuario reinicia una run reclamada deliberadamente. Los backtests se auto-cierran, así que un backtest reclamado simplemente se vuelve a ejecutar.
- **La identidad es el nombre del nodo.** Main hace upsert por `NodeName`, así que el pod cuya IP/URL cambia en reinicio mantiene la identidad, se re-registra con nuevo `AdvertiseUrl`.
- **Modo fijo en el primer registro.** El modo del nodo (`Run`/`Backtest`/`Mixed`) es un tipo persistido, no puede cambiar en el heartbeat; el re-registro con modo diferente honored por liveness pero el cambio de modo se ignora (logged como warning). Para cambiar el modo: elimina el nodo, deja que se re-registre.

## Configuración

Main (Web) — `App:Discovery`:

| Clave | Por defecto | Significado |
|-------|-----------|-------------|
| `Enabled` | `false` | Switch maestro para endpoint de registro + monitor. |
| `JoinToken` | — | Secreto compartido del clúster (≥ 32 chars) que los agentes deben presentar. |
| `HeartbeatTtl` | `00:01:30` | Gracia antes de que el nodo silencioso se marque inalcanzable. |
| `InstanceReclaimGrace` | `00:01:00` | Margen extra más allá de `HeartbeatTtl` antes de que una instancia varada en un nodo inalcanzable sea reclamada (fallida). |
| `MonitorInterval` | `00:00:30` | Cada cuánto el monitor y el reclaimer de instancias barren. |
| `HeartbeatInterval` | `00:00:30` | Valor devuelto a los agentes como cadencia sugerida. |

Agente (CtraderCliNode) — `NodeAgent`:

| Clave | Significado |
|-------|-------------|
| `MainUrl` | URL base del nodo main. Vacío = modo de registro manual (loop no-op). |
| `AdvertiseUrl` | URL que el main usa para alcanzar **este** agente. |
| `NodeName` | Nombre único; por defecto el nombre de la máquina si está en blanco. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Hint de capacidad honrado por el scheduler. |
| `HeartbeatIntervalSeconds` | Cadencia de re-registro. |
| `JwtSecret` | Debe ser igual al `JoinToken` del main — tanto el bearer del registro como la clave de firma del JWT de dispatch. |

## Modelo de seguridad (v1)

Los nodos auto-registrados comparten **un secreto de clúster** (`JoinToken` == `JwtSecret` de cada agente). El main firma cada request de dispatch como JWT HS256 de 5 minutos con ese secreto; el agente valida. Requisitos:

- Mantén `JoinToken` ≥ 32 chars y ruébalo (actualiza `App:Discovery:JoinToken` del main y `NodeAgent:JwtSecret` de cada agente juntos).
- Termina TLS frente al main y los agentes en producción (reverse proxy / ingress).
- El agente solo ejecuta imágenes que coincidan con `AllowedImagePrefix`.

**Endurecimiento de seguimiento (no v1):** emitir secreto único por nodo en el registro (bootstrap estilo kubeadm → credencial por nodo) para que un solo agente comprometido no pueda falsificar tokens de dispatch para peers. El flujo de registro ya devuelve cuerpo de respuesta — lugar natural para devolver el secreto por nodo recién emitido.

## Los nodos manuales siguen funcionando

`POST /api/nodes` (UI de admin) continúa registrando nodos fijos con su propio secreto por nodo. El descubrimiento es aditivo.

Un despliegue white-label puede **ocultar los controles manuales** (o toda la superficie de Nodes) y depender puramente del
auto-descubrimiento: `App:Branding:NodesUi=Monitor` elimina add/delete manual, `Hidden` oculta la nav, página y
API manual, y `App:Branding:RestrictNodesToOwner` limita la superficie solo al owner. El endpoint de auto-registro +
heartbeat aquí no se ve afectado en ningún modo. Ver
[White-label → Visibilidad de UI de Nodes](../features/white-label.md#nodes-ui-visibility).
