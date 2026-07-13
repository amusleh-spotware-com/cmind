---
description: "Helm chart: deploy/helm/cmind. Nasazuje Web, MCP, samo-se-registrující node agenty, volitelný in-cluster Postgres."
---

# Nasazení na Kubernetes — krok za krokem

Helm chart: `deploy/helm/cmind`. Nasazuje Web, MCP, samo-se-registrující node agenty, volitelný
in-cluster Postgres.

> **Ověřeno** end-to-end na lokálním `kind` clusteru: všechny pody dosáhnou `Ready`, node agent
> se sám zaregistruje s per-pod headless DNS jménem, `/health` + `/version` vrací 200, zmenšený
> agent je auto-marked unreachable. Postup níže = co bylo testováno.

## 0. Předpoklady

- Kubernetes cluster (managed EKS/AKS/GKE, nebo lokální `kind`/`k3d`/`minikube`).
- `kubectl` (namířeno na cílový context) a `helm` 3.
- Container registry, ze kterého cluster může stahovat (přeskočte pro lokální `kind` — images místo toho loadujte).

## 1. Sestavte tři image

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, atd.), **nebo** pro lokální `kind` cluster loadujte
direct:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Zvolte tajemství

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret pro node auto-discovery
```

## 3. Nainstalujte chart

Registry-based (managed cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Lokální `kind` (loaded images, žádný externí Postgres, non-privilegovaní agenti):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Na `kind`/containerd bez host Docker socketu, takže `web.dockerSocket.enabled=false`
> (in-app builder/LocalNode nedostupný) a `nodeAgent.privileged=false` (agent stále
> **samo-se-registruje**; jen nemůže spouštět cTrader containery bez DinD). Pro reálnou úlohu
> spouštějte agenty na node pool kde `nodeAgent.privileged=true` povoleno.

Nemáte `helm` binary? Vykreslete a apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Počkejte na rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Očekávejte: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) a `cmind-node-agent-0`
(StatefulSet) všechny `Ready`. Web readiness (`/health`) projde až po migraci DB (migrace
běží při startu).

## 5. Ověřte auto-discovery

```bash
# Node agent by se měl objevit v DB s per-pod headless DNS BaseUrl a IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Příklad (ověřeno):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Škálujte kapacitu přidáním replik — každý nový pod se sám zaregistruje do jednoho heartbeat intervalu:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Reconciliace stale (ověřeno): zmenšete agenta, přepne na `IsReachable=f` po
`discovery.heartbeatTtl`; zase zvětšete, vrátí se online.

## 6. Dostaňte se k UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — přihlaste se jako seeded owner
```

Externí přístup: nastavte `web.ingress.enabled=true`, `web.ingress.host`, a TLS.

## Proč jsou node agenti StatefulSet

Main node dispatchuje práci na **specifického** agenta podle URL, takže každý agent potřebuje stabilní,
individuálně adresovatelné DNS jméno. Chart používá StatefulSet + headless Service; každý pod
advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` a sám se registruje pod jménem podu.
Stejný discovery mechanismus používají holé cTrader CLI nodes —
viz [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (live dashboard, logs hub). Pro spuštění **více než jedné Web repliky**,
nastavte `signalr` connection string na Redis endpoint — aplikace pak registruje **SignalR Redis
backplane** (`AddStackExchangeRedis`) takže hub messages a circuit negotiation fan across replicas a
reconnect přistávající na jiném podu zůstává live. Žádný `signalr` connection string = single-replica
in-memory (nechanged). Spárujte se session affinity na ingress pro plynulé Blazor Server circuits.

## Copy-agent autoscaling & resilience

Copy-agent hostuje dlouho běžící trading sockety, takže škáluje na **práci, ne CPU**. S
`copyAgent.keda.enabled=true` chart instaluje KEDA `ScaledObject`, která se dotazuje Postgres na
počet běžících copy-profilů a škáluje repliky tak, aby každý pod hostoval asi `copyAgent.keda.profilesPerPod`
(default 25), mezi `minReplicas`/`maxReplicas`. KEDA čte DB přes `TriggerAuthentication` bound k
`copyAgent.keda.connectionSecretKey` secret key. Když `copyAgent.replicas > 1` (nebo KEDA škáluje nad 1)
chart také přidává `topologySpreadConstraints` (spread across nodes) a `PodDisruptionBudget`
(`minAvailable: 1`); při scale-in / rolling update každý pod uvolní lease na `SIGTERM`
(`terminationGracePeriodSeconds`, default 30) takže survivor reclaimed okamžitě — viz
[scaling.md](scaling.md).

## Klíčové hodnoty

| Hodnota | Účel |
|-------|------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image souřadnice (`local` + `Never` pro kind). |
| `secrets.existingSecret` | Použij externí/sealed Secret místo chart-managed hodnot. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` pro managed DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA on CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Počet agentů, DinD privilege, mód, kapacita. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` pro Web builder/LocalNode (pouze Docker-runtime nodes). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics do OTLP collectoru. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapované ve všech
prostředích.

## In-cluster test suite

Spusťte copy-trading suite jako Kubernetes `Job` proti nasazené aplikaci, takže regression je chycena
in-cluster stejně jako lokálně. Copy testy potřebují pouze Web + Postgres + token cache — **žádné**
privilegované node agenty.

One-shot, reprodukovatelné (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (no secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manuální / CI wiring — **deterministic (default, no secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** navíc potřebuje token cache. cTrader **refresh tokeny jsou single-use**, takže cache
musí být **zapisovatelná**: Job kopíruje Secret do emptyDir na `/app/secrets` přes init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nikdy nepečené v image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Hodnota | Účel |
|-------|------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Který projekt + `dotnet test --filter` má běžet (default: deterministic). |
| `tests.copySecret` | Volitelný Secret s gitignored `openapi-*.local.json`; kopírovaný do **zapisovatelné** emptyDir na `/app/secrets` pro live suite. Empty ⇒ žádný secret mount. |
| `tests.backoffLimit` | Počet retry Jobu (default `0`). |

`LiveCopySecrets` prochází nahoru od `/app` pro nalezení `secrets/`; live testy skip čistě když cache
chybí. `Dockerfile.tests` SDK-based takže běhá stejné assertions jako lokální `dotnet test` — obě
deterministic (`101 passed`) i full live (`8 passed`) suites ověřeny běžící inside this
image lokálně proti Docker před shipping.

## Teardown

```bash
helm -n cmind uninstall cmind        # nebo: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # pouze lokální
```

## Spuštění in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent. Konvertuje repo path do nativního formátu (`cygpath -m`) takže Docker,
helm a kubectl ho resolují na **Windows/git-bash** stejně jako Linux/macOS — ověřeno end to end na Windows
(kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Prostředí | Příkaz |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **nebo** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferováno)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Preferujte WSL na Windows.** Běh inside WSL používá nativní Linux paths a Docker Desktop's WSL integration,
čímž se vyhnete všem path-translation edge cases — nerobustnější varianta. Potřebuje `docker`, `kind`, `helm`,
`kubectl` a .NET SDK na WSL PATH (Docker Desktop poskytuje `docker`; zbytek nainstalujte v distro,
např. `go install sigs.k8s.io/kind@latest`, helm/kubectl release binaries). `scripts/k8s-e2e.ps1`
wrapper vybere WSL s `-Wsl`, fallback na git-bash jinak.

`kind` + `helm` self-installable pokud chybí (release binaries nebo `choco install kind kubernetes-helm`);
nepovažujte za nedostupné. Viz také [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
