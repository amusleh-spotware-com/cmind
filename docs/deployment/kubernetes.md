# Kubernetes deployment

A Helm chart lives in `deploy/helm/cmind`. It deploys Web, MCP, self-registering node agents, and
(optionally) an in-cluster Postgres.

## Quick start

```bash
# Build & push images (tags: -web, -mcp, -node-agent)
docker build -f Dockerfile.web        -t ghcr.io/your-org/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t ghcr.io/your-org/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t ghcr.io/your-org/cmind-node-agent:1.0.0 .
docker push ghcr.io/your-org/cmind-web:1.0.0
docker push ghcr.io/your-org/cmind-mcp:1.0.0
docker push ghcr.io/your-org/cmind-node-agent:1.0.0

helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=ghcr.io --set image.repository=your-org/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword=$(openssl rand -hex 16) \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken=$(openssl rand -hex 24)
```

Then:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-cmind-web 8080:8080
```

Node agents self-register within one heartbeat interval and appear on the **Nodes** page.

## Auto-discovery in K8s

Node agents run as a **StatefulSet + headless Service** so each pod has a stable DNS name. Each pod
advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` and self-registers to the Web Service.
Scale capacity with `--set nodeAgent.replicas=N`. This is the *same* discovery mechanism used by
bare external nodes — see `docs/operations/node-discovery.md`.

## Important values

| Value | Purpose |
|-------|---------|
| `secrets.existingSecret` | Use an external/sealed Secret instead of chart-managed values. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). Set `false` + `externalDatabase.connectionString` for a managed DB (recommended in prod). |
| `web.ingress.*` | Enable/configure ingress + TLS. |
| `web.autoscaling` / `mcp.autoscaling` | HPA on CPU. |
| `nodeAgent.privileged` | `true` so the agent can start its in-pod dockerd (runs cTrader containers). |
| `web.dockerSocket.enabled` | Mounts the node's `/var/run/docker.sock` for the Web builder/LocalNode. Requires Docker-runtime nodes. |
| `observability.otlpEndpoint` | Ship logs+traces+metrics to an OTLP collector. |

## Runtime caveats

- **Web builder needs Docker.** `CBotBuilder` shells out to Docker. On containerd-only clusters the
  hostPath socket mount won't exist — either run Web on Docker-runtime nodes, disable the builder
  path, or offload all execution to privileged node agents (`web.localNodeEnabled=false`).
- **Node agents are privileged** (DinD). Schedule them on a node pool where that is acceptable, or
  swap in a rootless/sysbox runtime.
- **Probes:** liveness `/alive`, readiness `/health` (Web); `/version` (MCP); `/health` (agent).

## Kustomize

No separate Kustomize base is maintained — render the chart and pipe to kubectl/kustomize:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml > cmind.yaml
kubectl apply -f cmind.yaml
```
