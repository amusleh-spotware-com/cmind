---
description: "cMind escala com esforço mínimo do operador. Duas cargas de trabalho com estado — execução run/backtest, copy-trading — ambas usam banco de dados como ponto de coordenação, então…"
---

# Escalabilidade horizontal

cMind escala com esforço mínimo do operador. Duas cargas de trabalho com estado — execução run/backtest,
copy-trading — ambas usam banco de dados como ponto de coordenação, então adicionar réplicas não precisa
nenhum coordenador externo (sem ZooKeeper, sem eleição de líderes).

## Copy-trading (arrendamento auto-cicatrizante)

Cada nó executa `CopyEngineSupervisor` (fechado em `App:Copy:Enabled`). Cada ciclo de reconciliação,
supervisor:

1. **Reclama** cada perfil em execução não atribuído *ou* arrendamento expirado, em um `UPDATE` único atômico —
   dois supervisores em disputa nunca reivindicam o mesmo perfil, então perfil copiado por exatamente um
   nó (sem pedidos duplos).
2. **Renova** arrendamento em perfis que hospeda.
3. Hospeda perfis atribuídos, empurra rotações de token de acesso para host em execução no local (sem
   queda de fluxo de eventos).

Falha do nó → para renovar; uma vez que `App:Copy:LeaseTtl` passa, qualquer nó sobrevivente recupera
seus perfis próximo ciclo, reconstrói estado da reconciliação sem duplicar negociações. **Escalar
para fora** = adicionar réplicas; perfis não atribuídos/livres são coletados automaticamente.

**Escala para cima/atualização contínua graciosa (S1)** = em `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**libera arrendamentos deste nó** (`AssignedNode`/`LeaseExpiresAt` → null) então sobrevivente os recupera
seu *muito próximo* ciclo de reconciliação — **não** após `LeaseTtl` completo. Apenas falha rígida espera o TTL.
`terminationGracePeriodSeconds` do agente de cópia (padrão 30) dá tempo de liberação para terminar antes
pod morrer.

### Botões (`App:Copy`)

| Configuração | Padrão | Notas |
|---------|---------|-------|
| `Enabled` | `false` | Ativar hospedagem de cópia para o nó. |
| `ReconcileInterval` | `30s` | Com que frequência o nó reclama/renova/reconcilia. |
| `LeaseTtl` | `120s` | Graça antes que perfis do nó silencioso sejam reclamados. Mantenha poucos intervalos de reconciliação para que ciclo lento não cause transferência espúria. |
| `NodeName` | nome da máquina | Defina distintamente quando dois supervisores compartilham um host. |

Em supervisores de cópia do Kubernetes executam como Deployment; defina `replicas` para paralelismo desejado. Cada
pod obtém `NodeName` estável (padrão: nome do host do pod), então arrendamentos atribuídos por pod. Banco de dados é
única fonte de verdade — sem sessões pegajosas, sem estado por pod para migrar.

**Distribuição equilibrada (S4):** defina `App:Copy:MaxProfilesPerNode` > 0 para limitar quantos perfis em execução
um nó hospeda. Cada supervisor então reclama **no máximo** seu espaço restante via `FOR UPDATE SKIP LOCKED` ligado
atomicamente reclama, então perfis **se espalham** entre réplicas em vez de supervisor primeiro agarrar todos — nenhum pod único quente / SPOF. Reclamo skip-locked mantém "exatamente um nó
por perfil" garantia (sem duplo-hospedagem) até sob reivindicações concorrentes. `0` (padrão) =
ilimitado (um nó hospeda tudo, inalterado).

**Na escala (S7/S8):** cada pod tremeluz reconciliação por até 20% de `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) então N réplicas não disparam reclamo/renova `UPDATE`
simultaneamente (Postgres thundering-herd). Quando `copyAgent.replicas > 1` o gráfico também se espalha
réplicas entre nós (`topologySpreadConstraints`) e adiciona `PodDisruptionBudget` (`minAvailable: 1`)
então drenagem/atualização nunca leva capacidade de cópia a zero.

## Execução run/backtest

`NodeScheduler` escolhe nó elegível menos carregado honrando `MaxInstances`; agentes de nó remoto
se auto-registram e pulsam (`App:Discovery`), `NodeHeartbeatMonitor` marca nó inacessível
quando pulso excede `Discovery:HeartbeatTtl`. Adicione agentes de nó para adicionar capacidade de execução;
agente morto roteado automaticamente.

## Migrações em escala para fora / implementação contínua

Cada réplica Web/MCP executa `OwnerSeeder` na inicialização, que aplica migrações EF e semeia o proprietário.
Para tornar isso seguro quando N réplicas começam ao mesmo tempo, migram + semeiam executam dentro de uma **sessão Postgres
travamento consultivo** (`MigrationLock.RunExclusiveAsync`, chave `DatabaseDefaults.MigrationAdvisoryLockKey`):
a primeira réplica a adquirir migra e semeia; o resto bloqueia na trava, depois encontram migrações
já aplicadas (sem operação) e o proprietário já presente. Nenhum job de migração separado ou eleição de liderança é
necessário. Se você adicionar semeadura de primeira execução, coloque **dentro** do mesmo bloco protegido para que seja single-writer.

## Resiliência HTTP de agente de nó

O nó principal fala com cada agente `CtraderCliNode` sobre HTTP através de três clientes divididos por propósito para que um
nó instável ou rede nunca corrompam estado:

- **ler** (`status` / `report` / `stats`) — GETs idempotentes, retentados em falhas transitórias
  (backoff exponencial + jitter, `NodeAgentHttp.ReadRetryCount`) com timeouts por tentativa e total.
- **escrever** (`start` / `stop` / `clean`) — POSTs não idempotentes, com tempo limite mas **nunca retentados**: um
  `start` retentado poderia iniciar duplo um contêiner.
- **fluxo** (`logs`) — o fluxo longo do `docker logs -f` obtém um tempo limite infinito e sem
  pipeline de resiliência, então o rastreamento nunca é cortado.

Um nó que permanece inacessível é tratado por pulso + [reclamação de instância órfã](../operations/node-discovery.md);
a camada HTTP apenas suaviza blips transitórios.

## Camadas sem estado

Web (Blazor Server + API) e servidor MCP são sem estado atrás do banco de dados, replicam livremente.
A autenticação é baseada em cookie; escale Web horizontalmente atrás do balanceador de carga. Servidor MCP é separado
processo/Deployment para que escale independentemente da Web.

## Resiliência de conexão de banco de dados

Cada host que abre o banco de dados usa uma **estratégia de execução com retentativas** para que uma desconexão transitória
ou um failover gerenciado-Postgres (remendação de Servidor RDS / Flexível) é retentado em vez de
superficial como um erro para o usuário:

- Web e MCP registram o contexto através do componente Aspire Npgsql com `DisableRetry=false`
  e um `CommandTimeout` explícito (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (não-Aspire) registra via `UseAppNpgsql`, que aplica o mesmo
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + timeout de comando de `DatabaseDefaults`.

Todas as escritas são declarações única `SaveChanges` / única `ExecuteUpdate` / única `ExecuteSql`, então o
estratégia de retentativa é segura (sem transação multi-declaração precisa de envoltório `strategy.ExecuteAsync`
manual). Se você adicionar uma transação manual ou múltiplos `SaveChanges` em uma operação lógica, envolva
em `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — caso contrário lança sob retentativa.

## Checklist para escalar para fora

- [ ] Postgres dimensionado para carga de conexão adicionada (cada réplica Web/MCP/nó abre um pool).
- [ ] `App:Copy:Enabled=true` em cada nó que deve hospedar perfis de cópia.
- [ ] Distinto `App:Copy:NodeName` por supervisor co-localizado (K8s: padrão por pod bem).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Agentes de nó implantados onde Docker privilegiado disponível (AKS/EKS/EC2/VM, não Fargate).
- [ ] Multi-réplica Web: defina a string de conexão `signalr` (plano traseiro Redis) **e** ative ingress
      afinidade de sessão (sessões pegajosas) para que circuito Blazor se reconecte a pod ao vivo. Uma excepção de componente
      é capturada pelo `ErrorBoundary` de `MainLayout` (retry amigável, circuito fica vivo).
