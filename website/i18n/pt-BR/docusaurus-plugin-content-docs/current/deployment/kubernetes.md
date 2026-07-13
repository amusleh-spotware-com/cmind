---
description: "Gráfico Helm: deploy/helm/cmind. Implanta Web, MCP, agentes de nó com auto-registro, Postgres opcional em cluster."
---

# Implantação Kubernetes — passo a passo

Gráfico Helm: `deploy/helm/cmind`. Implanta Web, MCP, agentes de nó com auto-registro, Postgres
opcional em cluster.

> **Validado** de ponta a ponta em cluster local `kind`: todos os pods chegam a `Ready`, agente de nó
> se auto-registra com nome DNS headless por pod, `/health` + `/version` retornam 200, agente reduzido
> auto-marcado como inacessível. Fluxo abaixo = o que foi testado.

## 0. Pré-requisitos

- Cluster Kubernetes (EKS/AKS/GKE gerenciado, ou local `kind`/`k3d`/`minikube`).
- `kubectl` (apontado para contexto alvo) e `helm` 3.
- Registro de contêiner que o cluster pode extrair (pule para `kind` local — carregue imagens).

## 1. Compile as três imagens

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Envie (`docker push <registry>/cmind-web:1.0.0`, etc.), **ou** para cluster local `kind` carregue
direto:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Escolha segredos

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 caracteres; segredo de cluster compartilhado para descoberta automática de nó
```

## 3. Instale o gráfico

Com base em registro (cluster gerenciado):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (imagens carregadas, sem Postgres externo, agentes não privilegiados):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Em `kind`/containerd sem soquete Docker do host, então `web.dockerSocket.enabled=false`
> (construtor no aplicativo/LocalNode indisponível) e `nodeAgent.privileged=false` (agente ainda
> **se auto-registra**; apenas não pode executar contêineres cTrader sem DinD). Para execução real de carga de trabalho,
> execute agentes em pool de nós onde `nodeAgent.privileged=true` é permitido.

Sem binário `helm`? Renderize e aplique:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Aguarde o rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Espere: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) e `cmind-node-agent-0`
(StatefulSet) todos `Ready`. A prontidão da Web (`/health`) passa apenas uma vez que o BD migra (as migrações
executam na inicialização).

## 5. Verifique descoberta automática

```bash
# Agente de nó deve aparecer no BD com nome DNS headless por pod e IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Exemplo (verificado):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Escale a capacidade adicionando réplicas — cada novo pod se auto-registra em um intervalo de batida cardíaca:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Reconciliação de obsolescência (verificada): escale o agente para baixo, muda para `IsReachable=f` após
`discovery.heartbeatTtl`; escale para cima, volta online.

## 6. Alcance a interface

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — entre com o proprietário semeado
```

Acesso externo: defina `web.ingress.enabled=true`, `web.ingress.host` e TLS.

## Por que agentes de nó são um StatefulSet

Main node despacha trabalho para **agente específico** por URL, então cada agente precisa de estável,
nome DNS individualmente endereçável. O gráfico usa StatefulSet + Serviço headless; cada pod
anuncia `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` e se auto-registra sob nome de pod.
Mesmo mecanismo de descoberta usado pelos nós CLI cTrader nus —
veja [../operations/node-discovery.md](../operations/node-discovery.md).

## Escala web (plano traseiro SignalR, S6)

Aplicativo Web = Blazor Server + SignalR (dashboard ao vivo, hub de logs). Para executar **mais de uma réplica web**,
defina a string de conexão `signalr` para o ponto de extremidade Redis — o aplicativo então registra **plano traseiro do Redis SignalR**
(`AddStackExchangeRedis`) então as mensagens do hub e a negociação de circuito se propagam entre réplicas e uma
reconexão que chega a pod diferente fica ativa. Sem string de conexão `signalr` = réplica única
em memória (inalterada). Combine com afinidade de sessão no ingress para circuitos Blazor Server mais suave.

## Dimensionamento automático e resiliência do agente de cópia

Agente de cópia hospeda soquetes de negociação de longa vida, então escala no **trabalho, não CPU**. Com
`copyAgent.keda.enabled=true` o gráfico instala KEDA `ScaledObject` que consulta Postgres para
contagem de perfil de cópia em execução e escala réplicas para que cada pod hospede cerca de `copyAgent.keda.profilesPerPod`
(padrão 25), entre `minReplicas`/`maxReplicas`. KEDA lê BD via `TriggerAuthentication` vinculado a
chave secreta `copyAgent.keda.connectionSecretKey`. Quando `copyAgent.replicas > 1` (ou KEDA escala para > 1)
o gráfico também adiciona `topologySpreadConstraints` (difundir entre nós) e `PodDisruptionBudget`
(`minAvailable: 1`); na escala para cima / atualização contínua cada pod libera arrendamentos em `SIGTERM`
(`terminationGracePeriodSeconds`, padrão 30) então sobrevivente recupera imediatamente — veja
[scaling.md](scaling.md).

## Valores-chave

| Valor | Finalidade |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Coordenadas de imagem (`local` + `Never` para kind). |
| `secrets.existingSecret` | Use Secret externo/selado em vez de valores gerenciados pelo gráfico. |
| `postgres.enabled` | `true` = Postgres em cluster (dev). `false` + `externalDatabase.connectionString` para BD gerenciado (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA em CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Contagem de agentes, privilégio DinD, modo, capacidade. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` para construtor Web/LocalNode (apenas nós com tempo de execução Docker). |
| `observability.otlpEndpoint` | Envie logs+traces+métricas para coletor OTLP. |

## Sondagens

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agente) — mapeado em todos
ambientes.

## Suite de teste em cluster

Execute suite de copy-trading como Kubernetes `Job` com aplicativo implantado, então regressão capturada
em-cluster igual localmente. Testes de cópia precisam apenas Web + Postgres + cache de token — **sem**
agentes de nó privilegiados.

Uma tacada, reproduzível (kind up → compilar+carregar imagens → implantar → executar Job → afirmar saída 0 → derrubar):

```bash
scripts/k8s-e2e.sh                                   # suite de cópia determinística (sem segredos)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reutilize contexto kube atual
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # ao vivo
```

Manual / fiação CI — **determinística (padrão, sem segredos):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # imagem do executor (SDK + projetos de teste compilados)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Suite ao vivo** adicionalmente precisa cache de token. cTrader **tokens de atualização de uso único**, então cache
deve ser **gravável**: Job copia Secret em emptyDir em `/app/secrets` via init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nunca assado na imagem
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Valor | Finalidade |
|-------|---------|
| `tests.enabled` | Renderize teste `Job` (padrão `false`). |
| `tests.project` / `tests.filter` | Qual projeto + `dotnet test --filter` executar (padrão: determinístico). |
| `tests.copySecret` | Secret opcional com `openapi-*.local.json` gitignored; copiado em **gravável** emptyDir em `/app/secrets` para suite ao vivo. Vazio ⇒ sem montagem secreta. |
| `tests.backoffLimit` | Contagem de tentativa de Job (padrão `0`). |

`LiveCopySecrets` sobe de `/app` para encontrar `secrets/`; testes ao vivo pulam limpo quando cache
ausente. `Dockerfile.tests` baseado em SDK então executa mesmas afirmações que `dotnet test` local — ambas
suites determinísticas (`101 passed`) e ao vivo (`8 passed`) verificadas executando nessa
imagem localmente contra Docker antes de enviar.

## Desmontagem

```bash
helm -n cmind uninstall cmind        # ou: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # apenas local
```

## Executando a suite em cluster entre plataformas (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` independente do SO. Converte caminho do repo para forma nativa (`cygpath -m`) então Docker,
helm e kubectl resolvem em **Windows/git-bash** bem como Linux/macOS — verificado de ponta a ponta em Windows
(cluster kind up → imagens construídas+carregadas → gráfico implantado → Job de teste em cluster verde → desmontagem).

| Ambiente | Comando |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **ou** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferido)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Prefira WSL em Windows.** Executar dentro de WSL usa caminhos Linux nativos e integração WSL do Docker Desktop,
evitando todos os casos extremos de tradução de caminho — opção mais robusta. Precisa `docker`, `kind`, `helm`,
`kubectl` e SDK .NET no WSL PATH (Docker Desktop fornece `docker`; instale o resto na distro,
por exemplo `go install sigs.k8s.io/kind@latest`, os binários de versão helm/kubectl). Wrapper `scripts/k8s-e2e.ps1`
escolhe WSL com `-Wsl`, volta para git-bash caso contrário.

`kind` + `helm` auto-instalável se ausente (binários de versão ou `choco install kind kubernetes-helm`);
não trate como indisponível. Veja também [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
