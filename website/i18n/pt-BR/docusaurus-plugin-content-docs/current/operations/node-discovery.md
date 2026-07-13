---
description: "Os nós CLI cTrader entram no cluster por auto-registro + heartbeat — nenhuma entrada manual. Mesmo padrão que agentes Consul/Nomad/kubeadm: agente inicia sabendo localização do nó principal…"
---

# Auto-descoberta de nó

Os nós CLI cTrader entram no cluster por **auto-registro + heartbeat** — nenhuma entrada manual. Mesmo padrão que agentes Consul/Nomad/kubeadm: agente inicia sabendo localização principal do nó + segredo de cluster compartilhado, depois continuamente se anuncia.

> Verificado ponta a ponta em Docker Compose e cluster `kind` Kubernetes: agentes auto-registram, aparecem em DB alcançável, auto-marcado inatingível quando heartbeats param após TTL, voltam online quando retomam.

## Como funciona

```
Agente CtraderCliNode                     Principal (Web)
------------------                       ----------
POST /api/nodes/register  ── token de junção ──▶ verificar token (tempo constante)
  { name, baseUrl, mode,                        verificar versão de protocolo
    maxInstances, dataDir,                      upsert CtraderCliNode por nome
    protocolVersion }                           stamp LastHeartbeatAt, IsReachable=true
        ▲                                        └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  cada HeartbeatInterval               NodeHeartbeatMonitor (background):
        └──────────────────────────────────── se agora - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registro == heartbeat.** Agente re-POSTs em `HeartbeatIntervalSeconds`. Primeira chamada cria nó (evento `NodeRegistered`); chamadas posteriores atualizam liveness. Heartbeat retomado após indisponibilidade inverte nó de volta alcançável (evento `NodeCameOnline`).
- **Reconciliação de liveness.** `NodeHeartbeatMonitor` marca nós cuja último heartbeat excede `HeartbeatTtl` inatingível. Agendador (`IsActive`/`AcceptsRun`/`AcceptsBacktest` fechados em capacidade de alcance) para de colocar trabalho até que relatem novamente.
- **Reclamação de instância órfã.** `NodeInstanceReclaimer` (background) transiciona qualquer instância não-terminal encalhada em um nó inatingível para **Falha** (`FailureReason = "Node unreachable - instance reclaimed"`, evento de domínio `InstanceFailed` → notificação do usuário), para que um nó quebrado/particionado nunca possa deixar uma instância presa "Em Execução" para sempre. Reclamação só dispara uma vez que o último heartbeat do nó é obsoleto além de `HeartbeatTtl + InstanceReclaimGrace`, dando a um breve blip uma chance de se recuperar primeiro. Execuções reclamadas **não são reagendadas automaticamente**: um nó particionado mas vivo pode ainda estar executando o container e não há cerca em nível de container, portanto relançar arriscaria execução dupla — o usuário reinicia uma execução reclamada deliberadamente. Os backtests se auto-saem, então um backtest reclamado é simplesmente re-executado.
- **A identidade é nome do nó.** Main faz upsert por `NodeName`, para que pod cuja IP/URL mude na reinicialização mantenha identidade, re-registra novo `AdvertiseUrl`.
- **Modo fixo na primeira inscrição.** Modo de nó (`Run`/`Backtest`/`Mixed`) é tipo persistente, não pode mudar no heartbeat; re-inscrição com modo diferente honrada para liveness mas mudança de modo ignorada (registrada como aviso). Para mudar modo: excluir nó, deixar re-registrar.

## Configuração

Principal (Web) — `App:Discovery`:

| Chave | Padrão | Significado |
|-----|---------|---------|
| `Enabled` | `false` | Interruptor mestre para endpoint de inscrição + monitor. |
| `JoinToken` | — | Segredo de cluster compartilhado (≥ 32 caracteres) agentes devem apresentar. |
| `HeartbeatTtl` | `00:01:30` | Graça antes de nó silencioso marcado inatingível. |
| `InstanceReclaimGrace` | `00:01:00` | Margem extra além de `HeartbeatTtl` antes de instância encalhada em nó inatingível ser reclamada (falha). |
| `MonitorInterval` | `00:00:30` | Com que frequência o monitor e reclamador de instância varrem. |
| `HeartbeatInterval` | `00:00:30` | Valor retornado aos agentes como cadência sugerida. |

Agente (CtraderCliNode) — `NodeAgent`:

| Chave | Significado |
|-----|---------|
| `MainUrl` | URL base do nó principal. Vazio = modo de registro manual (loop no-op). |
| `AdvertiseUrl` | URL principal usa para alcançar **este** agente. |
| `NodeName` | Nome único; padrão para nome da máquina se em branco. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Dica de capacidade honrada pelo agendador. |
| `HeartbeatIntervalSeconds` | Cadência de re-inscrição. |
| `JwtSecret` | Deve igualar principal `JoinToken` — tanto bearer de registro quanto chave de assinatura JWT de despacho. |

## Modelo de segurança (v1)

Nós auto-registrados compartilham **um segredo de cluster** (`JoinToken` == `JwtSecret` de cada agente). Principal assina cada solicitação de despacho como JWT HS256 de 5 minutos com aquele segredo; agente valida. Requisitos:

- Mantenha `JoinToken` ≥ 32 caracteres e gire (atualize `App:Discovery:JoinToken` principal e `NodeAgent:JwtSecret` de cada agente juntos).
- Termine TLS em frente de principal e agentes em produção (proxy reverso / ingresso).
- Agente ainda apenas executa imagens que correspondem a `AllowedImagePrefix`.

**Endurecimento de acompanhamento (não v1):** emita segredo único por nó na inscrição (kubeadm-style bootstrap → credencial por nó) para que um único agente comprometido não possa forjar tokens de despacho para pares. Fluxo de registro já retorna corpo de resposta — lugar natural para devolver segredo por nó cunhado.

## Nós manuais ainda funcionam

`POST /api/nodes` (interface do administrador) continua a registrar nós fixados com seu próprio segredo por nó. A descoberta é aditiva.

Uma implantação com marca branca pode **ocultar os controles manuais** (ou toda a superfície Nodes) e confiar puramente em auto-descoberta: `App:Branding:NodesUi=Monitor` remove adicionar/excluir manual, `Hidden` remove navegação, página e API manual, e `App:Branding:RestrictNodesToOwner` pisos da superfície apenas proprietário. O ponto final de auto-registro + heartbeat aqui não é afetado em cada modo. Veja [Marca branca → Visibilidade da interface Nodes](../features/white-label.md#nodes-ui-visibility).
