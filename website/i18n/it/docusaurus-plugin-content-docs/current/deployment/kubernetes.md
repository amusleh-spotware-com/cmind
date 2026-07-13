---
description: "Helm chart: deploy/helm/cmind. Distribuisce Web, MCP, agenti nodo auto-registranti, Postgres opzionale in-cluster."
---

# Deployment Kubernetes — passo dopo passo

Helm chart: `deploy/helm/cmind`. Distribuisce Web, MCP, agenti nodo auto-registranti, Postgres opzionale in-cluster.

> **Validato** end-to-end su cluster `kind` locale: tutti i pod raggiungono `Ready`, l'agente nodo
> si auto-registra con DNS name headless per pod, `/health` + `/version` restituiscono 200, l'agente
> scalato verso il basso viene marcato unreachable. Il flusso sotto = cosa testato.

## 0. Prerequisiti

- Cluster Kubernetes (EKS/AKS/GKE gestito, o locale `kind`/`k3d`/`minikube`).
- `kubectl` (puntato al contesto target) e `helm` 3.
- Registry container da cui il cluster può scaricare (skip per `kind` locale — carica le immagini invece).

## 1. Build delle tre immagini

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, ecc.), **oppure** per cluster `kind` locale carica
direttamente:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Scegli i secret

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; secret condiviso del cluster per auto-discovery nodo
```

## 3. Installa la chart

Basato su registry (cluster gestito):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (immagini caricate, nessun Postgres esterno, agenti non privileged):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Su `kind`/containerd nessun socket Docker host, quindi `web.dockerSocket.enabled=false`
> (builder in-app/LocalNode non disponibile) e `nodeAgent.privileged=false` (l'agente ancora
> **si auto-registra**; semplicemente non può eseguire container cTrader senza DinD). Per carico reale
> di esecuzione, eseguire gli agenti su node pool dove `nodeAgent.privileged=true` è permesso.

Nessun binario `helm`? Render e applica:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Attendi il rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Aspettarsi: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) e `cmind-node-agent-0`
(StatefulSet) tutti `Ready`. La readiness di Web (`/health`) passa solo una volta migrate DB (le migrate
sono eseguite all'avvio).

## 5. Verifica auto-discovery

```bash
# L'agente nodo dovrebbe apparire nel DB con DNS headless per pod raggiungibile e IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Esempio (verificato):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Scala la capacità aggiungendo repliche — ogni nuovo pod si auto-registra entro un intervallo di heartbeat:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Riconciliazione staleness (verificata): scala l'agente verso il basso, flip a `IsReachable=f` dopo
`discovery.heartbeatTtl`; scala di nuovo verso l'alto, torna online.

## 6. Raggiungi la UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — accedi con il proprietario seeded
```

Accesso esterno: imposta `web.ingress.enabled=true`, `web.ingress.host`, e TLS.

## Perché gli agenti nodo sono uno StatefulSet

Main node dispatcha il lavoro a un **specifico** agente per URL, quindi ogni agente necessita di un DNS name
stabile e individualmente indirizzabile. La chart usa StatefulSet + Service headless; ogni pod
annuncia `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` e si auto-registra sotto il nome del pod.
Stesso meccanismo di discovery che usano i bare cTrader CLI nodes —
vedere [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (dashboard live, hub log). Per eseguire **più di una Web replica**,
impostare la stringa di connessione `signalr` all'endpoint Redis — l'app poi registra il **SignalR Redis
backplane** (`AddStackExchangeRedis`) così i messaggi dell'hub e la negoziazione del circuit fan out
attraverso le repliche e un reconnect atterrando su un pod diverso resta live. Nessuna stringa di connessione
`signalr` = singola-replica in-memory (invariato). Accoppiare con session affinity all'ingresso per i
circuit Blazor Server più fluidi.

## Copy-agent autoscaling e resilienza

Copy-agent hosta socket di trading long-lived, quindi scala su **lavoro, non CPU**. Con
`copyAgent.keda.enabled=true` la chart installa KEDA `ScaledObject` che interroga Postgres per
il conteggio dei profili copy in esecuzione e scala le repliche così ogni pod hosta circa `copyAgent.keda.profilesPerPod`
(default 25), tra `minReplicas`/`maxReplicas`. KEDA legge il DB tramite `TriggerAuthentication` legata al
secret key `copyAgent.keda.connectionSecretKey`. Quando `copyAgent.replicas > 1` (o KEDA scala oltre 1)
la chart aggiunge anche `topologySpreadConstraints` (spread attraverso i nodi) e `PodDisruptionBudget`
(`minAvailable: 1`); su scale-in / rolling update ogni pod rilascia i lease su `SIGTERM`
(`terminationGracePeriodSeconds`, default 30) così il sopravvissuto reclama immediatamente — vedere
[scaling.md](scaling.md).

## Valori chiave

| Valore | Scopo |
|-------|-------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Coordinate immagine (`local` + `Never` per kind). |
| `secrets.existingSecret` | Usa Secret esterno/sealed invece di valori gestiti dalla chart. |
| `postgres.enabled` | `true` = Postgres in-cluster (dev). `false` + `externalDatabase.connectionString` per DB gestito (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA su CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Conteggio agente, privilegio DinD, modalità, capacità. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` per Web builder/LocalNode (solo nodi con Docker runtime). |
| `observability.otlpEndpoint` | Spedire log+traces+metriche a collector OTLP. |

## Probe

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agente) — mappate in tutti
gli ambienti.

## Suite di test in-cluster

Esegui la suite copy-trading come `Job` Kubernetes contro l'app distribuita, così la regressione viene catturata
in-cluster come in locale. I test copy necessitano solo Web + Postgres + cache token — **nessun**
agente nodo privileged.

One-shot, riproducibile (kind up → build+load immagini → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # suite copy deterministica (nessun secret)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # riutilizza contesto kube corrente
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Cablaggio manuale / CI — **deterministico (default, nessun secret):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # immagine runner (SDK + progetti test built)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Suite live** necessita in più della cache token. I **refresh token cTrader sono single-use**, quindi la cache
deve essere **scrivibile**: il Job copia il Secret in emptyDir vuoto a `/app/secrets` tramite init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # mai incluso nell'immagine
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Valore | Scopo |
|-------|-------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Quale progetto + `dotnet test --filter` eseguire (default: deterministico). |
| `tests.copySecret` | Secret opzionale con `openapi-*.local.json` gitignored; copiato in emptyDir **scrivibile** a `/app/secrets` per suite live. Vuoto ⇒ nessun mount secret. |
| `tests.backoffLimit` | Conteggio retry Job (default `0`). |

`LiveCopySecrets` cammina su da `/app` per trovare `secrets/`; i test live saltano pulitamente quando la cache
è assente. `Dockerfile.tests` basato su SDK quindi esegue le stesse assertion di `dotnet test` locale — entrambe
le suite deterministica (`101 passed`) e live completa (`8 passed`) verificate in esecuzione dentro questa
immagine in locale contro Docker prima dello shipping.

## Teardown

```bash
helm -n cmind uninstall cmind        # o: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # solo locale
```

## Eseguire la suite in-cluster cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` è indipendente dal SO. Converte il path del repo in forma nativa (`cygpath -m`) così Docker,
helm e kubectl lo risolvono su **Windows/git-bash** oltre che Linux/macOS — verificato end-to-end su Windows
(kind cluster up → immagini built+loaded → chart deploy → test Job in-cluster green → teardown).

| Ambiente | Comando |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **oppure** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferito)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Preferire WSL su Windows.** Eseguire dentro WSL usa path nativi Linux e l'integrazione Docker Desktop WSL,
evitando tutti gli edge case di traduzione path — opzione più robusta. Necessita `docker`, `kind`, `helm`,
`kubectl` e .NET SDK sul PATH WSL (Docker Desktop fornisce `docker`; installare il resto nella distro,
es. `go install sigs.k8s.io/kind@latest`, helm/kubectl release binary). Lo script wrapper `scripts/k8s-e2e.ps1`
seleziona WSL con `-Wsl`, falls back a git-bash altrimenti.

`kind` + `helm` auto-installabili se assenti (release binary o `choco install kind kubernetes-helm`);
non trattarli come non disponibili. Vedere anche [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
