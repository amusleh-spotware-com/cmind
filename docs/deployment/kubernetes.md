# Kubernetes deployment — step by step

Helm chart: `deploy/helm/cmind`. Deploys Web, MCP, self-registering node agents, and
(optionally) an in-cluster Postgres.

> **Validated** end-to-end on a local `kind` cluster: all pods reach `Ready`, the node agent
> self-registers with its per-pod headless DNS name, `/health` + `/version` return 200, and a
> scaled-down agent is auto-marked unreachable. The exact flow below is what was tested.

## 0. Prerequisites

- A Kubernetes cluster (managed EKS/AKS/GKE, or local `kind`/`k3d`/`minikube`).
- `kubectl` (pointed at the target context) and `helm` 3.
- A container registry the cluster can pull from (skip for local `kind` — load images instead).

## 1. Build the three images

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push them (`docker push <registry>/cmind-web:1.0.0`, etc.), **or** for a local `kind` cluster load
them directly:

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

> On `kind`/containerd there is no host Docker socket, so `web.dockerSocket.enabled=false`
> (the in-app builder/LocalNode are unavailable) and `nodeAgent.privileged=false` (the agent still
> **self-registers**; it just can't run cTrader containers without DinD). For real workload
> execution, run agents on a node pool where `nodeAgent.privileged=true` is allowed.

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
(StatefulSet) all `Ready`. Web readiness (`/health`) only passes once the DB is migrated (migrations
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

Staleness reconciliation (verified): scale an agent down and it flips to `IsReachable=f` after
`discovery.heartbeatTtl`; scale back up and it returns online.

## 6. Reach the UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — sign in with the seeded owner
```

For external access set `web.ingress.enabled=true`, `web.ingress.host`, and TLS.

## Why node agents are a StatefulSet

The main node dispatches work to a **specific** agent by URL, so each agent needs a stable,
individually-addressable DNS name. The chart uses a StatefulSet + a headless Service; each pod
advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` and self-registers under its pod name.
This is the same discovery mechanism bare external nodes use —
see [../operations/node-discovery.md](../operations/node-discovery.md).

## Key values

| Value | Purpose |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` for kind). |
| `secrets.existingSecret` | Use an external/sealed Secret instead of chart-managed values. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` for a managed DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA on CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count, DinD privilege, mode, capacity. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` for the Web builder/LocalNode (Docker-runtime nodes only). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics to an OTLP collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped in all
environments.

## Teardown

```bash
helm -n cmind uninstall cmind        # or: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local only
```
