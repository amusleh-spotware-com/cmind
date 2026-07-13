---
description: "Helm chart: deploy/helm/cmind. Mengelola Web, MCP, self-registering node agents, opsional in-cluster Postgres."
---

# Kubernetes deployment — step by step

Helm chart: `deploy/helm/cmind`. Mengelola Web, MCP, self-registering node agents, opsional
in-cluster Postgres.

> **Validated** end-to-end pada local `kind` cluster: semua pods mencapai `Ready`, node agent
> self-registers dengan per-pod headless DNS name, `/health` + `/version` return 200, scaled-down
> agent auto-marked unreachable. Flow di bawah = apa yang tested.

## 0. Prerequisites

- Kubernetes cluster (managed EKS/AKS/GKE, atau local `kind`/`k3d`/`minikube`).
- `kubectl` (pointed pada target context) dan `helm` 3.
- Container registry cluster dapat pull dari (skip untuk local `kind` — load images sebagai gantinya).

## 1. Bangun tiga images

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, dll), **atau** untuk local `kind` cluster load
direct:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Pilih secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret untuk node auto-discovery
```

## 3. Instal chart

Berbasis registry (managed cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (loaded images, tidak ada external Postgres, non-privileged agents):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Pada `kind`/containerd tidak ada host Docker socket, jadi `web.dockerSocket.enabled=false`
> (in-app builder/LocalNode tidak tersedia) dan `nodeAgent.privileged=false` (agent masih
> **self-registers**; hanya tidak dapat menjalankan cTrader containers tanpa DinD). Untuk real workload
> execution, jalankan agents pada node pool di mana `nodeAgent.privileged=true` diizinkan.

Tidak ada `helm` binary? Render dan apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Tunggu untuk rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Diharapkan: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) dan `cmind-node-agent-0`
(StatefulSet) semua `Ready`. Web readiness (`/health`) lulus hanya sekali DB migrated (migrations
run pada startup).

## 5. Verifikasi auto-discovery

```bash
# Node agent harus muncul dalam DB dengan per-pod headless DNS BaseUrl dan IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Example (verified):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Scale capacity dengan menambahkan replicas — setiap pod baru self-registers dalam satu heartbeat interval:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness reconciliation (verified): scale agent down, flips ke `IsReachable=f` setelah
`discovery.heartbeatTtl`; scale back up, returns online.

## 6. Jangkau UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — sign in dengan seeded owner
```

External access: set `web.ingress.enabled=true`, `web.ingress.host`, dan TLS.

## Mengapa node agents adalah StatefulSet

Main node mendispatch work ke **specific** agent oleh URL, jadi setiap agent memerlukan stable,
individually-addressable DNS name. Chart menggunakan StatefulSet + headless Service; setiap pod
advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` dan self-registers di bawah pod name.
Mekanisme discovery yang sama bare cTrader CLI nodes gunakan —
lihat [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (live dashboard, logs hub). Untuk menjalankan **lebih dari satu Web replica**,
setel `signalr` connection string ke Redis endpoint — app kemudian meregistrasi **SignalR Redis
backplane** (`AddStackExchangeRedis`) jadi hub messages dan circuit negotiation fan di seluruh replicas dan
reconnect landing pada pod berbeda tetap live. Tidak ada `signalr` connection string = single-replica
in-memory (unchanged). Pair dengan session affinity pada ingress untuk smoothest Blazor Server circuits.

## Copy-agent autoscaling & resilience

Copy-agent hosts long-lived trading sockets, jadi scales pada **work, tidak CPU**. Dengan
`copyAgent.keda.enabled=true` chart installs KEDA `ScaledObject` yang queries Postgres untuk
running copy-profile count dan scales replicas jadi setiap pod hosts tentang `copyAgent.keda.profilesPerPod`
(default 25), antara `minReplicas`/`maxReplicas`. KEDA reads DB via `TriggerAuthentication` bound ke
`copyAgent.keda.connectionSecretKey` secret key. Ketika `copyAgent.replicas > 1` (atau KEDA scales past 1)
chart juga adds `topologySpreadConstraints` (spread lintas nodes) dan `PodDisruptionBudget`
(`minAvailable: 1`); pada scale-in / rolling update setiap pod releases leases pada `SIGTERM`
(`terminationGracePeriodSeconds`, default 30) jadi survivor reclaims segera — lihat
[scaling.md](scaling.md).

## Key values

| Value | Purpose |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` untuk kind). |
| `secrets.existingSecret` | Gunakan external/sealed Secret alih-alih chart-managed values. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` untuk managed DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA pada CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count, DinD privilege, mode, capacity. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` untuk Web builder/LocalNode (Docker-runtime nodes saja). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics ke OTLP collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped di semua
environments.

## In-cluster test suite

Jalankan copy-trading suite sebagai Kubernetes `Job` terhadap deployed app, jadi regression caught
in-cluster sama seperti locally. Copy tests hanya memerlukan Web + Postgres + token cache — **tidak ada**
privileged node agents.

One-shot, reproducible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (tidak ada secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manual / CI wiring — **deterministic (default, tidak ada secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** juga memerlukan token cache. cTrader **refresh tokens single-use**, jadi cache
harus **writable**: Job copies Secret ke emptyDir pada `/app/secrets` via init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # tidak pernah baked ke image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Value | Purpose |
|-------|---------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Proyek mana + `dotnet test --filter` untuk jalankan (default: deterministic). |
| `tests.copySecret` | Optional Secret dengan gitignored `openapi-*.local.json`; disalin ke **writable** emptyDir pada `/app/secrets` untuk live suite. Kosong ⇒ tidak ada secret mount. |
| `tests.backoffLimit` | Job retry count (default `0`). |

`LiveCopySecrets` walks up dari `/app` untuk menemukan `secrets/`; live tests skip cleanly ketika cache
absent. `Dockerfile.tests` SDK-based jadi menjalankan same assertions seperti local `dotnet test` — keduanya
deterministic (`101 passed`) dan full live (`8 passed`) suites verified running dalam image ini locally
terhadap Docker sebelum shipping.

## Teardown

```bash
helm -n cmind uninstall cmind        # atau: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local saja
```

## Menjalankan in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent. Converts repo path ke native form (`cygpath -m`) jadi Docker,
helm dan kubectl resolves itu pada **Windows/git-bash** serta Linux/macOS — verified end ke end pada Windows
(kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Environment | Command |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **atau** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferred)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Prefer WSL pada Windows.** Menjalankan di dalam WSL menggunakan native Linux paths dan Docker Desktop's WSL integration,
avoiding semua path-translation edge cases — opsi paling robust. Perlu `docker`, `kind`, `helm`,
`kubectl` dan .NET SDK pada WSL PATH (Docker Desktop menyediakan `docker`; install rest dalam distro,
misalnya `go install sigs.k8s.io/kind@latest`, helm/kubectl release binaries). `scripts/k8s-e2e.ps1`
wrapper picks WSL dengan `-Wsl`, falls back ke git-bash sebaliknya.

`kind` + `helm` self-installable jika absent (release binaries atau `choco install kind kubernetes-helm`);
jangan treat sebagai unavailable. Lihat juga [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
