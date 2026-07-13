---
description: "Los nodos cTrader CLI se unen al clúster mediante auto-registro + latido — sin entrada manual. Mismo patrón que agentes Consul/Nomad/kubeadm: el agente arranca sabiendo…"
---

# Node auto-discovery

Los nodos cTrader CLI se unen al clúster mediante **auto-registro + latido** — sin entrada manual. Mismo patrón que agentes Consul/Nomad/kubeadm: el agente arranca sabiendo ubicación del nodo principal + secreto compartido del clúster, luego se anuncia continuamente a sí mismo.

> Verificado de extremo a extremo en Docker Compose y clúster `kind` Kubernetes: los agentes se auto-registran, aparecen en DB alcanzable, auto-marcados inalcanzables cuando los latidos se detienen más allá de TTL, regresan en línea cuando se reanuden.

## Cómo funciona

```
Agente CtraderCliNode                    Principal (Web)
------------------                      ----------
POST /api/nodes/register  ── token de unión ──▶ verificar token (tiempo constante)
  { nombre, baseUrl, modo,                     verificar versión de protocolo
    maxInstances, dataDir,                     upsert CtraderCliNode por nombre
    protocolVersion }                          marca de tiempo LastHeartbeatAt, IsReachable=true
        ▲                                      └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  cada HeartbeatInterval              NodeHeartbeatMonitor (fondo):
        └──────────────────────────────────── si ahora - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registro == latido.** El agente re-POSTs en `HeartbeatIntervalSeconds`. La primera llamada crea nodo (evento `NodeRegistered`); las llamadas posteriores refrescan vivacidad. El latido reanudado después de una interrupción voltea el nodo de vuelta alcanzable (`NodeCameOnline`).
- **Reconciliación de vivacidad.** `NodeHeartbeatMonitor` marca nodos cuyo último latido excede `HeartbeatTtl` inalcanzables. El planificador (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated en alcanzabilidad) detiene la colocación de trabajo hasta que reportan de nuevo.
- **Reclamación de instancia huérfana.** `NodeInstanceReclaimer` (fondo) transiciona cualquier instancia no terminal varada en un nodo inalcanzable a **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, evento de dominio `InstanceFailed` → notificación del usuario), por lo que un nodo estrellado/particionado nunca puede dejar una instancia atrapada "Running" para siempre. La reclamación solo se dispara una vez que el último latido del nodo es antiguo más allá de `HeartbeatTtl + InstanceReclaimGrace`, dando una breve solución una oportunidad de recuperarse primero. Las ejecuciones reclamadas **no se re-programan automáticamente**: un nodo particionado-pero-vivo aún puede estar ejecutando el contenedor y no hay vallado a nivel de contenedor, por lo que re-lanzar arriesgaría ejecución doble — el usuario reinicia deliberadamente una ejecución reclamada. Los backtests se auto-cierran, por lo que un backtest reclamado simplemente se re-ejecuta.
- **La identidad es el nombre del nodo.** Principal upserts por `NodeName`, por lo que pod cuya IP/URL cambien en reinicio mantiene identidad, re-registra nueva `AdvertiseUrl`.
- **Modo fijo en primer registro.** El modo del nodo (`Run`/`Backtest`/`Mixed`) es tipo persistido, no puede cambiar en latido; re-registro con modo diferente honrado por vivacidad pero cambio de modo ignorado (registrado como advertencia). Para cambiar modo: eliminar nodo, dejar que se re-registre.

## Configuración

Principal (Web) — `App:Discovery`:

| Clave | Defecto | Significado |
|-----|---------|---------|
| `Enabled` | `false` | Interruptor maestro para endpoint de registro + monitor. |
| `JoinToken` | — | Secreto compartido del clúster (≥ 32 caracteres) que los agentes deben presentar. |
| `HeartbeatTtl` | `00:01:30` | Gracia antes de que el nodo silencioso se marque inalcanzable. |
| `InstanceReclaimGrace` | `00:01:00` | Margen extra más allá de `HeartbeatTtl` antes de que una instancia varada en un nodo inalcanzable sea reclamada (fallida). |
| `MonitorInterval` | `00:00:30` | Con qué frecuencia el monitor y el reclamador de instancia barren. |
| `HeartbeatInterval` | `00:00:30` | Valor devuelto a los agentes como cadencia sugerida. |

Agente (CtraderCliNode) — `NodeAgent`:

| Clave | Significado |
|-----|---------|
| `MainUrl` | URL base del nodo principal. Vacío = modo de registro manual (bucle no-op). |
| `AdvertiseUrl` | URL que el principal usa para alcanzar **este** agente. |
| `NodeName` | Nombre único; por defecto al nombre de máquina si está en blanco. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Pista de capacidad honrada por planificador. |
| `HeartbeatIntervalSeconds` | Cadencia de re-registro. |
| `JwtSecret` | Debe igualar `JoinToken` del principal — tanto bearer de registro como clave de firma JWT de dispatch. |

## Modelo de seguridad (v1)

Los nodos auto-registrados comparten **un secreto del clúster** (`JoinToken` == `JwtSecret` de cada agente). El principal firma cada solicitud de dispatch como HS256 JWT de 5 minutos con ese secreto; el agente valida. Requisitos:

- Mantener `JoinToken` ≥ 32 caracteres y rotarlo (actualizar `App:Discovery:JoinToken` del principal y `NodeAgent:JwtSecret` de cada agente juntos).
- Terminar TLS frente al principal y agentes en producción (proxy inverso / ingress).
- El agente aún solo ejecuta imágenes que coinciden con `AllowedImagePrefix`.

**Endurecimiento de seguimiento (no v1):** emitir secreto único por nodo en registro (estilo bootstrap kubeadm → credencial por nodo) para que un agente comprometido único no pueda falsificar tokens de dispatch para pares. El flujo de registro ya devuelve cuerpo de respuesta — lugar natural para entregar secreto por nodo acuñado.

## Los nodos manuales aún funcionan

`POST /api/nodes` (UI admin) continúa registrando nodos fijados con su propio secreto por nodo. El descubrimiento es aditivo.

Un despliegue white-label puede **ocultar los controles manuales** (o toda la superficie de Nodos) y confiar puramente en auto-descubrimiento: `App:Branding:NodesUi=Monitor` cae agregar/eliminar manual, `Hidden` elimina la navegación, página y API manual, y `App:Branding:RestrictNodesToOwner` pisa la superficie solo al propietario. El endpoint de auto-registro + latido aquí no se ve afectado en ningún modo. Ver [White-label → Visibilidad de Nodes UI](../features/white-label.md#nodes-ui-visibility).
