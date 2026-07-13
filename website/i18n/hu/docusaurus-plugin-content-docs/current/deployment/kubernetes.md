---
description: "Helm chart: deploy/helm/cmind. Deploys Web, MCP, self-registering node agents, optional in-cluster Postgres."
---

# Kubernetes deployment — step by step

Helm chart: `deploy/helm/cmind`. Deploys Web, MCP, self-registering node agents, optional
in-cluster Postgres.

> **Validated** end-to-end on local `kind` cluster: all pods reach `Ready`, node agent
> self-registers with per-pod headless DNS name, `/health` + `/version` return 200, scaled-down
> agent auto-marked unreachable. Flow below = what tested.

## 0. Prerequisites

- Kubernetes cluster (managed EKS/AKS/GKE, or local `kind`/`k3d`/`minikube`).
- `kubectl` (pointed at target context) and `helm` 3.
- Container registry cluster can pull from (skip for local `kind` — load images instead).

## 1. Build the three images

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, etc.), **or** for local `kind` cluster load
direct:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Pick secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret for node auto-discovery
```

## 3. Install the chart

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

Local `kind` (loaded images, no external Postgres, non-privileged agents):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> On `kind`/containerd no host Docker socket, so `web.dockerSocket.enabled=false`
> (in-app builder/LocalNode unavailable) and `nodeAgent.privileged=false` (agent still
> **self-registers**; just can't run cTrader containers without DinD). For real workload
> execution, run agents on node pool where `nodeAgent.privileged=true` allowed.

No `helm` binary? Render and apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Wait for rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Expect: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) and `cmind-node-agent-0`
(StatefulSet) all `Ready`. Web readiness (`/health`) passes only once DB migrated (migrations
run on startup).

## 5. Verify auto-discovery

```bash
# Node agent should appear in the DB with a per-pod headless DNS BaseUrl and IsReachable=true
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

Scale capacity by adding replicas — each new pod self-registers within one heartbeat interval:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness reconciliation (verified): scale agent down, flips to `IsReachable=f` after
`discovery.heartbeatTtl`; scale back up, returns online.

## 6. Reach the UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — sign in with the seeded owner
```

External access: set `web.ingress.enabled=true`, `web.ingress.host`, and TLS.

## Why node agents are a StatefulSet

Main node dispatches work to **specific** agent by URL, so each agent needs stable,
individually-addressable DNS name. Chart uses StatefulSet + headless Service; each pod
advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` and self-registers under pod name.
Same discovery mechanism bare cTrader CLI nodes use —
see [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (live dashboard, logs hub). To run **more than one Web replica**,
set `signalr` connection string to Redis endpoint — app then registers **SignalR Redis
backplane** (`AddStackExchangeRedis`) so hub messages and circuit negotiation fan across replicas and a
reconnect landing on different pod stays live. No `signalr` connection string = single-replica
in-memory (unchanged). Pair with session affinity at ingress for smoothest Blazor Server circuits.

## Copy-agent autoscaling & resilience

Copy-agent hosts long-lived trading sockets, so scales on **work, not CPU**. With
`copyAgent.keda.enabled=true` chart installs KEDA `ScaledObject` that queries Postgres for
running copy-profile count and scales replicas so each pod hosts about `copyAgent.keda.profilesPerPod`
(default 25), between `minReplicas`/`maxReplicas`. KEDA reads DB via `TriggerAuthentication` bound to
`copyAgent.keda.connectionSecretKey` secret key. When `copyAgent.replicas > 1` (or KEDA scales past 1)
chart also adds `topologySpreadConstraints` (spread across nodes) and `PodDisruptionBudget`
(`minAvailable: 1`); on scale-in / rolling update each pod releases leases on `SIGTERM`
(`terminationGracePeriodSeconds`, default 30) so survivor reclaims immediately — see
[scaling.md](scaling.md).

## Key values

| Value | Purpose |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` for kind). |
| `secrets.existingSecret` | Use external/sealed Secret instead of chart-managed values. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` for managed DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA on CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count, DinD privilege, mode, capacity. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` for Web builder/LocalNode (Docker-runtime nodes only). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics to OTLP collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped in all
environments.

## In-cluster test suite

Run copy-trading suite as Kubernetes `Job` against deployed app, so regression caught
in-cluster same as locally. Copy tests need only Web + Postgres + token cache — **no**
privileged node agents.

One-shot, reproducible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (no secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manual / CI wiring — **deterministic (default, no secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** additionally needs token cache. cTrader **refresh tokens single-use**, so cache
must be **writable**: Job copies Secret into emptyDir at `/app/secrets` via init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # never baked into the image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Value | Purpose |
|-------|---------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Which project + `dotnet test --filter` to run (default: deterministic). |
| `tests.copySecret` | Optional Secret with gitignored `openapi-*.local.json`; copied into **writable** emptyDir at `/app/secrets` for live suite. Empty ⇒ no secret mount. |
| `tests.backoffLimit` | Job retry count (default `0`). |

`LiveCopySecrets` walks up from `/app` to find `secrets/`; live tests skip cleanly when cache
absent. `Dockerfile.tests` SDK-based so runs same assertions as local `dotnet test` — both
deterministic (`101 passed`) and full live (`8 passed`) suites verified running inside this
image locally against Docker before shipping.

## Teardown

```bash
helm -n cmind uninstall cmind        # or: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local only
```

## Running the in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent. Converts repo path to native form (`cygpath -m`) so Docker,
helm and kubectl resolve it on **Windows/git-bash** as well as Linux/macOS — verified end to end on Windows
(kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Environment | Command |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **or** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferred)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Prefer WSL on Windows.** Running inside WSL uses native Linux paths and Docker Desktop's WSL integration,
avoiding all path-translation edge cases — most robust option. Needs `docker`, `kind`, `helm`,
`kubectl` and .NET SDK on WSL PATH (Docker Desktop provides `docker`; install rest in distro,
e.g. `go install sigs.k8s.io/kind@latest`, the helm/kubectl release binaries). `scripts/k8s-e2e.ps1`
wrapper picks WSL with `-Wsl`, falls back to git-bash otherwise.

`kind` + `helm` self-installable if absent (release binaries or `choco install kind kubernetes-helm`);
do not treat as unavailable. See also [../testing/live-copy-trading.md](../testing/live-copy-trading.md).